# MiSide Mod Research

Inspected on 5 October 2026. Sources are repository code, project files, maintainer documentation, and loader issue reports. GitHub revisions below were read directly and pinned. Repository claims are distinguished from local static evidence and future runtime checks.

## Applicability

| Repository | How it crosses the technical boundary | Applicability to MitaAI |
| --- | --- | --- |
| [MrSago/MiSide-KappiMod](https://github.com/MrSago/MiSide-KappiMod) | Separate BepInEx and MelonLoader entry points drive common mod code. A registered Unity component supplies updates. Harmony observes native dialogue creation. | Strong loader and event-hook precedent. Local type/member presence supports trying the same IL2CPP route. UI library and full mod pack are optional, not MVP dependencies. |
| [Rist8/MiSide-Console-Unlocker](https://github.com/Rist8/MiSide-Console-Unlocker) | An IL2CPP `BasePlugin` registers a MonoBehaviour which reads keys and invokes existing game components. | Small, relevant proof pattern for G0 and the input loop. Its console commands and custom JSON parsing do not belong in MitaAI. |
| [ve3xone/DisableSubsForGameMiSide](https://github.com/ve3xone/DisableSubsForGameMiSide) | A Harmony postfix accesses the dialogue Canvas and native subtitle Text elements. | Confirms a real native presentation surface. Hiding existing subtitles is not a method for generating independent AI dialogue. |
| [QuestionBlock64/MiSide-Multiplayer](https://github.com/QuestionBlock64/MiSide-Multiplayer) | Discovers `MitaPerson` components in loaded scenes, caches them, samples character state, and implements text input with cursor/movement handling. | Useful discovery/input patterns. World and progression synchronization are described as experimental; the full multiplayer system is unnecessary for this mod. |
| [VinerX/NeuroMita](https://github.com/VinerX/NeuroMita) | Archived injected C# mod uses MelonLoader, a local Python socket connection, and existing dialogue instances associated with Mita. | Closest historical AI integration reference. The current release is standalone Unity; archived sources are not maintained and retain machine-specific assembly paths. Use them as evidence and design references. |
| [7ez/miside-rpc](https://github.com/7ez/miside-rpc) | A BepInEx plugin registers an update component for game presence reporting. | Additional evidence for thin observational plugins. It provides no AI dialogue proof. |

These are separate source precedents. No downloaded mod binary was executed, and none has been tested against the local copy during this research.

## Pinned Code Evidence

### KappiMod

Revision: `f01a0763e18a6b8337cc4a831cadbed173c73c35`, committed 9 August 2026.

- [BepInEx entry point](https://github.com/MrSago/MiSide-KappiMod/blob/f01a0763e18a6b8337cc4a831cadbed173c73c35/Loader/BepInEx/KappiBepInExPlugin.cs): loader entry, Harmony setup, registered update component, and interop directory.
- [Dialogue hooks](https://github.com/MrSago/MiSide-KappiMod/blob/f01a0763e18a6b8337cc4a831cadbed173c73c35/Patches/DialogueStartPatch.cs): patches `Dialogue_3DText.Start` and separately handles several other dialogue systems. Therefore one hook should not be advertised as universal dialogue coverage.
- [Project configuration](https://github.com/MrSago/MiSide-KappiMod/blob/f01a0763e18a6b8337cc4a831cadbed173c73c35/KappiMod.csproj): `net6.0`, x64, and loader-specific dependencies.
- [Maintainer README](https://github.com/MrSago/MiSide-KappiMod/blob/f01a0763e18a6b8337cc4a831cadbed173c73c35/README.md): recommends BepInEx `6.0.0-pre.2`, lists MelonLoader `0.7.0`, and warns that dialogue skipping can softlock the game.

MitaAI should reproduce a minimal loader/component pattern and safe observation. It should not inherit the dialogue skipper's progression behaviour.

### Console Unlocker

Revision: `13db883ae17f58b4c17db20cfb6e4fbae45de59b`, committed 15 March 2025.

- [PluginLoader.cs](https://github.com/Rist8/MiSide-Console-Unlocker/blob/13db883ae17f58b4c17db20cfb6e4fbae45de59b/Plugin/PluginLoader.cs): IL2CPP plugin registration and Unity component injection.
- [Plugin.cs](https://github.com/Rist8/MiSide-Console-Unlocker/blob/13db883ae17f58b4c17db20cfb6e4fbae45de59b/Plugin/Plugin.cs): Unity input and existing console/game calls.

The source supports injection and game-component access, rather than verifying MitaAI's eventual reply pipeline. Its project references many external assemblies, so a fresh minimal project is preferable to inheriting that project wholesale.

### Native Subtitle Access

Revision: `09e03e523ea93d297eacfab9e1addc173c33a37b`, committed 19 December 2024.

- [PatchDialogue3DText.cs](https://github.com/ve3xone/DisableSubsForGameMiSide/blob/09e03e523ea93d297eacfab9e1addc173c33a37b/DisableSubsForGameMiSide/PatchDialogue3DText.cs): accesses the dialogue Canvas, symbol Text, and `GameController/Interface/SubtitlesFrame` children.
- [Plugin.cs](https://github.com/ve3xone/DisableSubsForGameMiSide/blob/09e03e523ea93d297eacfab9e1addc173c33a37b/DisableSubsForGameMiSide/Plugin.cs): BepInEx/Harmony wiring.

The known hierarchy paths are runtime probes, not guarantees for every chapter or version. This code establishes that MiSide's native text can be accessed.

### Multiplayer Discovery And Input

Revision: `efc1c28f2c85d3dbe7498feeef942e487d6f7eb2`, committed 13 September 2026.

- [MitaSceneCache.cs](https://github.com/QuestionBlock64/MiSide-Multiplayer/blob/efc1c28f2c85d3dbe7498feeef942e487d6f7eb2/Runtime/Mita/MitaSceneCache.cs): filters loaded scenes and caches Mita transforms.
- [MitaSampler.cs](https://github.com/QuestionBlock64/MiSide-Multiplayer/blob/efc1c28f2c85d3dbe7498feeef942e487d6f7eb2/Runtime/Mita/MitaSampler.cs): samples the existing Mita rather than constructing a separate game recreation.
- [ChatController.cs](https://github.com/QuestionBlock64/MiSide-Multiplayer/blob/efc1c28f2c85d3dbe7498feeef942e487d6f7eb2/Runtime/Chat/ChatController.cs): input, cursor capture/restoration, movement suppression, and scene-aware messages.
- [README](https://github.com/QuestionBlock64/MiSide-Multiplayer/blob/efc1c28f2c85d3dbe7498feeef942e487d6f7eb2/README.md): explicitly describes incomplete stabilization of Mita, objectives, and world synchronization.

The source is especially useful for discovery and input ownership. Static matching of local player/Mita types is encouraging, but it does not prove sampled fields have the same runtime semantics.

### NeuroMita: Historical Injection Versus Current Product

Revision: `bff1361660c2d18eace4b7ac0af2d76072b59d97`, committed 4 October 2026.

- [Current English README](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/README_EN.md): describes the current standalone Unity release.
- [Archive README](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/archive/legacy_mitaai_csharp/README.md): identifies the old C# mod as historical reference and notes original environment dependencies.
- [DialogueControl.cs](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/archive/legacy_mitaai_csharp/MitaAI/ConnectionAndDialogue/DialogueControl.cs): dialogue template cloning, Mita speaker assignment, generated text, timing, and a native voice-loading point.
- [NetworkController.cs](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/archive/legacy_mitaai_csharp/MitaAI/ConnectionAndDialogue/NetworkController.cs): asynchronous loopback TCP requests to Python. One receive followed by JSON parsing makes transport framing a weak point; MitaAI will use framed HTTP messages.
- [MitaCore.cs](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/archive/legacy_mitaai_csharp/MitaAI/MitaCore.cs): hardcodes `Scene 4 - StartSecret`, accesses scene-specific objects, and changes gameplay components during setup.
- [MitaAI.csproj](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/archive/legacy_mitaai_csharp/MitaAI/MitaAI.csproj): historical absolute paths to MelonLoader/game assemblies.

The transferable result is the pattern: external inference feeds the real game's presentation. It does not justify porting the entire archive, its scene edits, or its transport. Local metadata contains the relevant class-owned dialogue fields and `LoadVoice`; template availability and safe execution still require G1.

### Discord Presence

Revision: `a2fb44ec31eb861bc4a381c2359403f111bbbb6b`, committed 27 December 2024.

- [Plugin.cs](https://github.com/7ez/miside-rpc/blob/a2fb44ec31eb861bc4a381c2359403f111bbbb6b/Plugin.cs) and [README](https://github.com/7ez/miside-rpc/blob/a2fb44ec31eb861bc4a381c2359403f111bbbb6b/README.md): BepInEx IL2CPP setup and native observation for presence reporting.

This provides an independent loader/observation reference. It is secondary to the native dialogue and input sources above.

## Loader Failure Investigated

[BepInEx issue #1033](https://github.com/BepInEx/BepInEx/issues/1033) reports inability to download Unity libraries for MiSide under Unity `2021.3.35f1`. The included log shows a network/service lookup error. That report does not establish that a filename suffix caused the failure.

The [pre.2 interop manager](https://github.com/BepInEx/BepInEx/blob/v6.0.0-pre.2/Runtimes/Unity/BepInEx.Unity.IL2CPP/Il2CppInteropManager.cs) constructs the URL using only major/minor/build, provides a configurable library source, and removes cached DLLs before extraction. Therefore manually dropping DLLs into the cache can be undone on the next attempted download.

Direct HTTP HEAD checks from this machine returned:

| Resource | Status on inspection |
| --- | --- |
| [Unity 2021.3.35 libraries](https://unity.bepinex.dev/libraries/2021.3.35.zip) | 200; reported length 1,689,426 bytes |
| Unity URL with `2021.3.35f1.zip` | 404 |
| [Official BepInEx IL2CPP x64 pre.2 package](https://github.com/BepInEx/BepInEx/releases/download/v6.0.0-pre.2/BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip) | 200; reported length 34,146,254 bytes |

These are download availability observations only. The loader package has not been installed or executed. The correct normalized URL is reachable, which reduces one known setup uncertainty.

Official setup references: [IL2CPP installation](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html), [plugin development setup](https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/1_setup.html), [pinned release](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2), and [MelonLoader repository](https://github.com/LavaGang/MelonLoader).

## Reuse Boundaries

The GitHub repository API reports MIT for KappiMod and LGPL-2.1 for the subtitle mod. NeuroMita has [its own licence](https://github.com/VinerX/NeuroMita/blob/bff1361660c2d18eace4b7ac0af2d76072b59d97/Licence.md), including attribution and canonical repository link conditions. No licence was reported for Console Unlocker, Multiplayer, or miside-rpc. Read source as evidence and implement the small adapters independently; review each licence before including third-party source in a distribution.

The planned distribution contains MitaAI's plugin and service. Existing game files, generated game interop, models, and scene assets stay local to the player's installation.

## Current Verification Boundary

Passed: local build identification, PE architecture, standard metadata parsing, relevant class members, installed SDK inventory, and official download availability. The JSON report records hashes so later results can be tied to this exact copy.

Not run: loader injection, interop generation, compiled plugin, native prefab discovery, dialogue playback, player input ownership, live provider requests, and story coexistence. There is no evidence yet for voice, safe movement control, or persistence.

The supported conclusion is that the injected route has concrete precedents and the expected static hooks exist locally. Whether their methods work on this copy is settled by the short G0-G2 runtime probe defined in [the implementation model](implementation-model.md).
