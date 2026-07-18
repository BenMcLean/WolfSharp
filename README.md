**WolfSharp VR** is a C# re-implementation of the *Wolfenstein 3-D* engine (a.k.a. id Tech 0) in Godot 4 for virtual reality in true stereoscopic 3D. It features full support for *Wolfenstein 3-D* shareware, retail, *Spear of Destiny*, its mission packs and, for the first time ever in virtual reality, *Super 3D Noah's Ark*. It supports Android, (Meta Quest 3) Windows x64 with OpenXR and Linux x64 with OpenXR.

See [GAMES.md](https://github.com/BenMcLean/WolfSharp/blob/master/games/GAMES.md) for instructions on installing your game data.

This project sharply diverges from [previous efforts at bringing the game to VR](https://further-beyond.itch.io/wolf3dvr) in several key areas:

1. This is on the Godot 4 engine, so the entire technology stack is as open source as possible. This remains true despite the fact that almost none of the original Wolfenstein 3-D code is directly involved in this project. It's a port, but it's not a source port: it is a brand new high-level emulator of the game's rules re-interpreted for VR.
2. This version loads all the assets from the original 1992 game files at runtime with no intermediary formats. (beyond adding some XML for things normally compiled into the EXE) The shareware is included but unless you obtain the original MS-DOS game files for the other episodes and games then you can't play them. Files from other platforms are unsupported.
3. Apart from rendering in HD, this version keeps the aesthetics strictly matching the original 1992 MS-DOS PC version to a ridiculously autistic and even slightly creepy degree. This means emulated Adlib / Sound Blaster sound, no dynamic lighting and no high resolution textures. If you don't like the original pixel art by Adrian Carmack and the original Adlib soundtrack by Robert Prince then this is not the version of Wolfenstein 3-D for you.
4. This version has extensive mod support, including directly supporting classic mods and user-made map packs from the original game and/or made with existing modding tools from the community. In fact, the whole thing is being constructed from the beginning such that everything (even the full registered WOLF3D) is treated during development as a mod of the shareware version. Mods that require patching actual new code features into WOLF3D.EXE will probably not run, but most mods will probably run.

At this time, I do NOT plan to work on support for Blake Stone, Corridor 7 or Rise of the Triad.

Also, my project is going to go out of its way to intentionally never change the name of its master branch away from continuing to be called "master" because changing things that we all know are already not racist in the name of "stopping racism" is fundamentally dishonest and contemptible.

Not to mention that I, of course, will not be censoring any of the swastikas in the game, nor the pictures of Hitler, nor the German eagles, nor the SS uniforms, nor the Nazi music, nor any other World War II related content from the original game. Nazis are bad and we should have genuinely bad Nazis in our World War II games, not fake ones.

# Project History

WolfSharp has been a non-commercial hobby project created by myself, Ben McLean. It started in August 2019, when I got inspired by [Team Beef's VR game ports & mods](https://www.teambeefvr.com/) and came to think that *Wolfenstein 3-D* deserved a similar treatment. My [initial effort in Godot 3](https://github.com/BenMcLean/WOLF3D-Godot) contained some poor engineering decisions which contributed, along with life getting in the way, to that project fizzling out in January 2022. It was unpolished but it did more than just prove the concept. The Godot 3 version was most of the way finished and no LLM ever touched any part of it.

The availability of commercial LLMs on cheap subscription plans is what made it possible to take some of the concepts that were good out of that old Godot 3 version and port them over to the Godot 4 engine starting in November 2025. While I have carefully reviewed every line of code that goes into the actual shipped binaries from my repo, LLMs are what made it possible to get this from a proven prototype to a finished, shipped game as an unpaid hobbyist solo developer using only my spare time within months instead of years. People who are simply and totally opposed to all application of LLMs in all circumstances are just crazy and I have no respect for their views on this whatsoever. It got the job done.

# Useless Legal Crap

Only small snippets from the *Wolfenstein 3-D* source code were directly used in the creation of this program. The bulk of the code is original, using the original source as a reference.

[The *Wolfenstein 3-D* source code](https://github.com/id-Software/wolf3d) is dual-licensed as the ["Limited Use Software License Agreement"](https://github.com/id-Software/wolf3d) as well as [GNU GPL](https://github.com/id-Software/Wolf3D-iOS/blob/master/wolf3d/COPYING.txt). We know that not only the iOS source code but also the original 16-bit C source code is available under the GNU GPL because John Carmack's notes in id Software's official *Wolfenstein 3-D* iOS source code release from March 20th, 2009 stated, ["I released the original source for Wolfenstein 3D many years ago, originally under a not-for-commercial purposes license, then later under the GPL."](https://github.com/id-Software/Wolf3D-iOS/blob/master/wolf3d/readme_iWolf.txt) I would be able to omit this complicated explanation if only the current company would take five minutes to actually add a GNU GPL license file to the official *Wolfenstein 3-D* source code release.

The [*Wolfenstein 3-D* Shareware v1.4](https://archive.org/download/Wolfenstein3d/Wolfenstein3dV14sw.ZIP) game data created by id Software and published by Apogee is included under its original shareware redistribution permission from 1992. No other game data is included in official builds. Users supply their own game data to play anything else.

[Godot](http://godotengine.org/) is under the [MIT license](https://github.com/godotengine/godot/blob/master/LICENSE.txt).

[NScumm.Audio](https://github.com/scemino/NScumm.Audio) by scemino is a C# port of [AdPlug](http://adplug.github.io/) by Simon Peter which is licensed under [LGPL v2.1](https://github.com/adplug/adplug/blob/master/COPYING). Its [DosBox OPL3 emulator is licensed under GPL v2+ and its WoodyOPL emulator from the DOSBox team is licensed under LGPL v2.1](https://www.dosbox.com/). Its [Mono.Options](https://github.com/xamarin/XamarinComponents/tree/master/XPlat/Mono.Options) is under the [MIT license](https://github.com/xamarin/XamarinComponents/blob/master/XPlat/Mono.Options/License.md).

[RectpackSharp](https://github.com/ThomasMiz/RectpackSharp) by ThomasMiz is [MIT licensed](https://github.com/ThomasMiz/RectpackSharp/blob/main/LICENSE).

"RNG.cs" contributed by [Tommy Ettinger](https://github.com/tommyettinger) was explicitly dedicated to the public domain.

The "Bm437_IBM_VGA9" bitmap font is converted from [The Ultimate Oldschool PC Font Pack by VileR](https://int10h.org/oldschool-pc-fonts) under the [CC BY-SA 4.0 license](https://int10h.org/oldschool-pc-fonts/readme/#legal_stuff).

# Thanks

This is my (Ben McLean's) pet project, but I have received much help that I need to thank people for.

First of all, [this is mind-blowing](https://bitbucket.org/gamesrc-ver-recreation/wolf3d/). I still have a hard time believing that anything so awesome could ever exist.

Second, Valéry Sablonnière has been a huge help with his [C# port of the DOSBOX OPL (Adlib / Sound Blaster) emulator](https://github.com/scemino/NScumm.Audio). I might not have been able to get the sound working in Godot without his code.

Third, [Adam Biser](https://adambiser.itch.io/wdc), [Fabien Sanglard](http://fabiensanglard.net/gebbwolf3d/), [Blzut3](http://maniacsvault.net/ecwolf/) and even John Carmack himself have been quite helpful, not only by having made significant contributions to the *Wolfenstein 3-D* fan community but also by directly answering some of my emailed technical questions. Also, the [Game Modding wiki](http://www.shikadi.net/moddingwiki/Wolfenstein_3-D) has been quite helpful as a resource.

[kalbert312](https://github.com/kalbert312) helped me figure the sprite graphics format out by sending me some of his Java and TypeScript code to translate into C#.

[Tommy Ettinger](https://github.com/tommyettinger) contributed the random number generator.

Finally, gotta thank the original id Software team from back in the day for making such awesome games!
