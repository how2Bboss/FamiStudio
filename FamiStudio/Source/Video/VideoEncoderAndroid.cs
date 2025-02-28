﻿using Android.Media;
using Android.Opengl;
using Android.Views;
using Java.Nio;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace FamiStudio
{
    // Based off https://bigflake.com/mediacodec/. Thanks!!!
    class VideoEncoderAndroid
    {
        const long SecondsToMicroSeconds = 1000000;
        const long SecondsToNanoSeconds  = 1000000000;

        private readonly string VideoMimeType = "video/avc";       // H.264 Advanced Video Coding
        private readonly string AudioMimeType = "audio/mp4a-latm"; // AAC

        private Surface surface;
        private MediaCodec videoEncoder;
        private MediaCodec audioEncoder;
        private MediaMuxer muxer;
        private int videoTrackIndex;
        private int audioTrackIndex;
        private int frameIndex;
        private int frameRateNumer;
        private int frameRateDenom;
        private int numAudioChannels;
        private bool muxerStarted;
        private bool abortAudioEncoding;
        private ManualResetEvent muxerStartEvent = new ManualResetEvent(false);
        private Task audioEncodingTask;

        private int audioDataIdx;
        private byte[] audioData;

        private MediaCodec.BufferInfo videoBufferInfo;
        private MediaCodec.BufferInfo audioBufferInfo;

        private const int EGL_RECORDABLE_ANDROID = 0x3142;

        private EGLDisplay eglDisplay = EGL14.EglNoDisplay;
        private EGLContext eglContext = EGL14.EglNoContext;
        private EGLSurface eglSurface = EGL14.EglNoSurface;

        private EGLDisplay prevEglDisplay;
        private EGLContext prevEglContext;
        private EGLSurface prevEglSurfaceRead;
        private EGLSurface prevEglSurfaceDraw;

        public bool AlternateByteOrdering => false;

        private VideoEncoderAndroid()
        {
        }

        public static VideoEncoderAndroid CreateInstance()
        {
            // TODO : Check support!
            return new VideoEncoderAndroid();
        }

        // https://github.com/lanhq147/SampleMediaFrame/blob/e2f20ff9eef73318e5a9b4de15458c5c2eb0fd46/app/src/main/java/com/google/android/exoplayer2/video/av/HWRecorder.java

        public bool BeginEncoding(int resX, int resY, int rateNumer, int rateDenom, int videoBitRate, int audioBitRate, bool stereo, string audioFile, string outputFile)
        {
            videoBufferInfo = new MediaCodec.BufferInfo();
            audioBufferInfo = new MediaCodec.BufferInfo();

            frameRateNumer = rateNumer;
            frameRateDenom = rateDenom;
            numAudioChannels = stereo ? 2 : 1;

            MediaFormat videoFormat = MediaFormat.CreateVideoFormat(VideoMimeType, resX, resY);
            videoFormat.SetInteger(MediaFormat.KeyColorFormat, (int)MediaCodecCapabilities.Formatsurface);
            videoFormat.SetInteger(MediaFormat.KeyBitRate, videoBitRate * 1000);
            videoFormat.SetFloat(MediaFormat.KeyFrameRate, rateNumer / (float)rateDenom);
            videoFormat.SetInteger(MediaFormat.KeyIFrameInterval, 4);
            videoFormat.SetInteger(MediaFormat.KeyProfile, (int)MediaCodecProfileType.Avcprofilehigh);
            videoFormat.SetInteger(MediaFormat.KeyLevel, (int)MediaCodecProfileLevel.Avclevel31);

            videoEncoder = MediaCodec.CreateEncoderByType(VideoMimeType);
            videoEncoder.Configure(videoFormat, null, null, MediaCodecConfigFlags.Encode);
            surface = videoEncoder.CreateInputSurface();
            videoEncoder.Start();
            
            MediaFormat audioFormat = MediaFormat.CreateAudioFormat(AudioMimeType, 44100, numAudioChannels);
            audioFormat.SetInteger(MediaFormat.KeyAacProfile, (int)MediaCodecProfileType.Aacobjectlc);
            audioFormat.SetInteger(MediaFormat.KeyBitRate, audioBitRate * 1000);

            audioEncoder = MediaCodec.CreateEncoderByType(AudioMimeType);
            audioEncoder.Configure(audioFormat, null, null, MediaCodecConfigFlags.Encode);
            audioEncoder.Start();
            
            try
            {
                muxer = new MediaMuxer(outputFile, MuxerOutputType.Mpeg4);
            }
            catch
            {
                return false;
            }

            videoTrackIndex = -1;
            audioTrackIndex = -1;
            muxerStarted = false;

            if (!ElgInitialize())
                return false;

            audioData = File.ReadAllBytes(audioFile);

            if (audioData == null)
                return false;

            DrainEncoder(videoEncoder, videoBufferInfo, videoTrackIndex, false);
            DrainEncoder(audioEncoder, audioBufferInfo, audioTrackIndex, false);

            audioEncodingTask = Task.Factory.StartNew(AudioEncodeThread, TaskCreationOptions.LongRunning);

            return true;
        }

        private void AudioEncodeThread()
        {
            var done = false;

            try
            {
                while (!done && !abortAudioEncoding)
                {
                    done = !WriteAudio();
                    DrainEncoder(audioEncoder, audioBufferInfo, audioTrackIndex, false);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }

        private bool WriteAudio()
        {
            int index = audioEncoder.DequeueInputBuffer(-1);
            if (index >= 0)
            {
                ByteBuffer[] inputBuffers = audioEncoder.GetInputBuffers();
                ByteBuffer buffer = inputBuffers[index];

                var len = Utils.Clamp(audioData.Length - audioDataIdx, 0, buffer.Remaining());
                buffer.Clear();
                buffer.Put(audioData, audioDataIdx, len);

                long presentationTime = (audioDataIdx * SecondsToMicroSeconds) / (44100 * 2 * numAudioChannels);
                audioDataIdx += len;

                var done = audioDataIdx == audioData.Length;
                audioEncoder.QueueInputBuffer(index, 0, len, presentationTime, done ? MediaCodecBufferFlags.EndOfStream : MediaCodecBufferFlags.None);
            }

            return audioDataIdx < audioData.Length;
        }

        public void AddFrame(byte[] image)
        {
            Debug.WriteLine($"Sending frame {frameIndex} to encoder");

            long presentationTime = ComputePresentationTimeNsec(frameIndex++);
            EGLExt.EglPresentationTimeANDROID(eglDisplay, eglSurface, presentationTime);
            CheckEglError();

            EGL14.EglSwapBuffers(eglDisplay, eglSurface);
            CheckEglError();

            DrainEncoder(videoEncoder, videoBufferInfo, videoTrackIndex, false);
        }

        public void EndEncoding(bool abort)
        {
            Debug.WriteLine("Releasing encoder objects");

            abortAudioEncoding = abort;

            if (audioEncodingTask != null)
            {
                audioEncodingTask.Wait();
                audioEncodingTask = null;
            }

            if (!abortAudioEncoding)
            {
                DrainEncoder(videoEncoder, videoBufferInfo, videoTrackIndex, false);
                DrainEncoder(audioEncoder, audioBufferInfo, audioTrackIndex, false);
            }

            if (videoEncoder != null)
            {
                videoEncoder.Stop();
                videoEncoder.Release();
                videoEncoder = null;
            }

            if (audioEncoder != null)
            {
                audioEncoder.Stop();
                audioEncoder.Release();
                audioEncoder = null;
            }

            ElgShutdown();

            if (muxer != null)
            {
                muxer.Stop();
                muxer.Release();
                muxer = null;
            }
        }

        private bool ElgInitialize()
        {
            prevEglContext = EGL14.EglGetCurrentContext();
            prevEglDisplay = EGL14.EglGetCurrentDisplay();
            prevEglSurfaceRead = EGL14.EglGetCurrentSurface(EGL14.EglRead);
            prevEglSurfaceDraw = EGL14.EglGetCurrentSurface(EGL14.EglDraw);

            eglDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
            if (eglDisplay == EGL14.EglNoDisplay)
                return false;

            int[] version = new int[2];
            if (!EGL14.EglInitialize(eglDisplay, version, 0, version, 1))
                return false;

            // Configure EGL for recording and OpenGL ES 2.0.
            int[] attribList =
            {
                EGL14.EglRedSize, 8,
                EGL14.EglGreenSize, 8,
                EGL14.EglBlueSize, 8,
                EGL14.EglAlphaSize, 8,
                EGL14.EglDepthSize, 16,
                EGL14.EglRenderableType, EGL14.EglOpenglEsBit,
                EGL_RECORDABLE_ANDROID, 1,
                EGL14.EglNone
            };
            EGLConfig[] configs = new EGLConfig[1];
            int[] numConfigs = new int[1];
            EGL14.EglChooseConfig(eglDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0);
            CheckEglError();

            // Configure context for OpenGL ES 2.0.
            int[] attrib_list = 
            {
                EGL14.EglContextClientVersion, 2,
                EGL14.EglNone
            };
            eglContext = EGL14.EglCreateContext(eglDisplay, configs[0], EGL14.EglNoContext, attrib_list, 0);
            CheckEglError();

            int[] surfaceAttribs = { EGL14.EglNone };
            eglSurface = EGL14.EglCreateWindowSurface(eglDisplay, configs[0], surface, surfaceAttribs, 0);
            CheckEglError();

            EGL14.EglMakeCurrent(eglDisplay, eglSurface, eglSurface, eglContext);
            CheckEglError();

            return true;
        }

        public void ElgShutdown()
        {
            if (eglDisplay != EGL14.EglNoDisplay)
            {
                EGL14.EglMakeCurrent(eglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
                EGL14.EglDestroySurface(eglDisplay, eglSurface);
                EGL14.EglDestroyContext(eglDisplay, eglContext);
                EGL14.EglReleaseThread();
                EGL14.EglTerminate(eglDisplay);
            }

            surface.Release();

            eglDisplay = EGL14.EglNoDisplay;
            eglContext = EGL14.EglNoContext;
            eglSurface = EGL14.EglNoSurface;

            surface = null;

            EGL14.EglMakeCurrent(prevEglDisplay, prevEglSurfaceDraw, prevEglSurfaceRead, prevEglContext);
        }

        private void CheckEglError()
        {
            Debug.Assert(EGL14.EglGetError() == EGL14.EglSuccess);
        }

        private void DrainEncoder(MediaCodec encoder, MediaCodec.BufferInfo bufferInfo, int trackIndex, bool endOfStream)
        {
            Debug.WriteLine($"DrainEncoder {endOfStream})");

            const int TIMEOUT_USEC = 10000;

            if (endOfStream)
            {
                Debug.WriteLine("Sending EOS to encoder");
                encoder.SignalEndOfInputStream();
            }

            ByteBuffer[] encoderOutputBuffers = encoder.GetOutputBuffers();
            while (true)
            {
                int encoderStatus = encoder.DequeueOutputBuffer(bufferInfo, TIMEOUT_USEC);
                if (encoderStatus == (int)MediaCodecInfoState.TryAgainLater)
                {
                    // no output available yet
                    if (!endOfStream)
                    {
                        break;      // out of while
                    }
                    else
                    {
                        Debug.WriteLine("No output available, spinning to await EOS");
                    }
                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputBuffersChanged)
                {
                    encoderOutputBuffers = encoder.GetOutputBuffers();
                }
                else if (encoderStatus == (int)MediaCodecInfoState.OutputFormatChanged)
                {
                    Debug.Assert(!muxerStarted);
                    MediaFormat newFormat = encoder.OutputFormat;
                    Debug.WriteLine($"Encoder output format changed: {newFormat}");

                    var isVideo = encoder == videoEncoder;

                    if (isVideo)
                    {
                        videoTrackIndex = muxer.AddTrack(newFormat);
                        trackIndex = videoTrackIndex;
                    }
                    else
                    {
                        audioTrackIndex = muxer.AddTrack(newFormat);
                        trackIndex = audioTrackIndex;
                    }

                    // now that we have the Magic Goodies, start the muxer
                    if (videoTrackIndex >= 0 && audioTrackIndex >= 0)
                    {
                        muxer.Start();
                        muxerStarted = true;
                        muxerStartEvent.Set();
                    }
                }
                else if (encoderStatus < 0)
                {
                    Debug.WriteLine($"Unexpected result from encoder.dequeueOutputBuffer: {encoderStatus}");
                }
                else
                {
                    ByteBuffer encodedData = encoderOutputBuffers[encoderStatus];
                    Debug.Assert(encodedData != null);

                    if ((bufferInfo.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                    {
                        Debug.WriteLine("Ignoring BUFFER_FLAG_CODEC_CONFIG");
                        bufferInfo.Size = 0;
                    }

                    if (bufferInfo.Size != 0)
                    {
                        muxerStartEvent.WaitOne();

                        encodedData.Position(bufferInfo.Offset);
                        encodedData.Limit(bufferInfo.Offset + bufferInfo.Size);

                        muxer.WriteSampleData(trackIndex, encodedData, bufferInfo);
                        Debug.WriteLine($"Sent {bufferInfo.Size} bytes to muxer");
                    }

                    encoder.ReleaseOutputBuffer(encoderStatus, false);

                    if ((bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                    {
                        if (!endOfStream)
                        {
                            Debug.WriteLine("Reached end of stream unexpectedly");
                        }
                        else
                        {
                            Debug.WriteLine("End of stream reached");
                        }
                        break; 
                    }
                }
            }
        }

        private long ComputePresentationTimeNsec(int frameIndex)
        {
            return frameIndex * SecondsToNanoSeconds * frameRateDenom / frameRateNumer;
        }
    }
}