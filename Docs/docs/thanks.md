# Acknowledgements

## Libraries 

FamiStudio uses some external libraries for sound emulation, import and export to various formats. Many thanks to the developers working on these.

### Nes_Snd_Emu
Great little NES sound emulation library created by [Blargg](http://www.slack.net/~ant/). It also includes improvements they made later on in Game_Music_Emu. Was also modified by me to add a few more expansions.

### NotSoFatso
Originally a Winamp plugin to play NSF created by [Disch](http://www.vgmpf.com/Wiki/index.php/Not_So,_Fatso!), it was stripped down to only keep the audio emulation core and is used for the NSF import.

### emu2149
Small library that emulates the YM2149 chip (aka PSG), created by by [Mitsutaka Okazaki](https://github.com/okaxaki). It is used for the EPSM emulation.

### emu2413
Very similar to emu2149 and also created by [Mitsutaka Okazaki](https://github.com/okaxaki), but used for the VRC7 emulation. 

### Nuked-OPN2
[OPN2](https://github.com/nukeykt/Nuked-OPN2) is a high-accuracy YM3438 emulator. FamiStudio uses a slightly modified version of this library from [BambooTracker](https://github.com/BambooTracker/BambooTracker/tree/master/BambooTracker/chip/nuked) for EPSM emulation.

### ShineMp3
Created by Gabriel Bouvigne, Pete Everett, Patrick Roberts and others. Used for the MP3 export.

### Vorbis 
Created by the [Xiph.Org Foundation](https://xiph.org/). Used for the OGG/Vorbis export.

### GifDec
[GifDec](https://github.com/lecram/gifdec) is a tiny little C library to read animated GIF files. It does one thing and does it very well. It is used to display the tutorials on all platforms.

### GLFW
[GLFW](https://www.glfw.org/) is a very simple windowing & input management system for OpenGL. It is used by all 3 platforms on Desktop for the main FamiStudio window. The C# bindings are provided by [GLFWDotNet](https://github.com/smack0007/GLFWDotNet).

### Stb
[Stb](https://github.com/nothings/stb) is a fantastic, pure-C, single-file library that does a bunch of low-level things that are useful for game development. FamiStudio uses their True-Type font rasterization and rectangle packing code for its text rendering.

## Demo Songs

A few demo songs are also provided with FamiStudio. Unless mentioned, the demo songs are my re-creations, done by reverse-engineering the NSF.

* Another Winter - Anamanagughi (cover by <a href='https://www.youtube.com/c/How2Bboss'>How2Bboss</a>. Thanks!!!)
* Nice - Full Soundtrack (by <a href='https://www.youtube.com/c/How2Bboss'>How2Bboss</a>. Thanks!!!)
* Dedrecil - Full Soundtrack (by <a href='https://www.youtube.com/c/How2Bboss'>How2Bboss</a>. Thanks!!!)
* Layla : The Iris Missions - Iris (by Supper)
* Tower of Heaven - Indignant Divinity (Flashygoodness, cover by <a href='https://www.youtube.com/watch?v=0qV4dSBOH5s'>Danooct1</a>. Thanks!!!)
* Green Hill Zone Theme - Sonic (cover by <a href='https://www.youtube.com/c/How2Bboss'>How2Bboss</a>. Thanks!!!)
* Mega Man 2 - Stage Select & Dr. Wily's Castle
* Journey To Silius - Intro
* DuckTales! - The Moon
* Castlevania 2 - Bloody Tears
* Shovel Knight - Strike the Earth! (Plains of Passage)
* Shatterhand - Final Stage
* Gimmick - Strange Memories of Death (Improvements contributed by marklincadet. Thanks!!!)
* Silver Surfer : BGM2
* Gradius II - Farewell
* Gyruss - Stage 2
* FamiStudio - Tutorial Song (song created in tutorial)
* Lagrange Point - Theme of Isis & Aqueduct (Contributed by marklincadet. Thanks!!!)
* Megami Tensei II - Explorer  (Contributed by marklincadet. Thanks!!!)
