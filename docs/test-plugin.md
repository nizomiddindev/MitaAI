# MitaAI Text Input Test Plugin

## AI Connectivity & Launcher Test (v0.5.0)

Version 0.5.0 introduces asynchronous AI provider integration (Ollama, LM Studio, OpenRouter, and custom endpoints) and a dedicated Windows GUI launcher (`MitaAI.Launcher.exe` / `Launcher.bat`).

### Quick In-Game Test Workflow

1. **Configure via Launcher**:
   - Double-click `Launcher.bat` (or `tools\Launcher\MitaAI.Launcher.exe`).
   - Pick your AI provider:
     - **OpenRouter (Free)**: Enter your API key (get one free at [openrouter.ai](https://openrouter.ai)). Default model: `meta-llama/llama-3.3-70b-instruct:free`.
     - **LM Studio (Local)**: Start LM Studio local server (`http://localhost:1234`). Key can be `lm-studio` or blank.
     - **Ollama (Local)**: Start Ollama (`http://localhost:11434`). Default model: `llama3.2` or any installed model.
     - **Fixture (Offline)**: Runs the local fallback without network calls.
   - Click **⚡ Test AI Connection** in the launcher to verify the API returns a response and check latency.
   - Click **💾 Save Configuration** to write to `MiSide_Dev\BepInEx\config\local.mitaai.probe.cfg`.
   - Click **▶ Play MiSide** (or start `MiSideFull.exe` manually).

2. **In-Game Conversation Test**:
   - In ordinary gameplay (e.g. kitchen in `Scene 4 - StartSecret`), approach Mita and look at her until the curved **E / Talk to Mita** prompt appears.
   - Press **E**. The orange subtitle composer appears at the bottom center.
   - Type a prompt (e.g. `What do you like to do?`) and press **Enter**.
   - Your message will be echoed briefly in orange, followed by Mita's AI reply in white/pink subtitles with the native rounded font.
   - Verify that camera and player movement are restored immediately upon pressing Enter.

3. **Log & Diagnostic Verification**:
   - Inspect `MiSide_Dev\BepInEx\LogOutput.log` for markers:
     - `MITAAI_AI_TURN_DISPATCHED`: prompt submitted and queued to background HTTP client.
     - `MITAAI_AI_REPLY_RECEIVED`: HTTP 200 response parsed with character count.
     - `MITAAI_SCREEN_SUBTITLE_STARTED`: Mita's AI reply displayed on-screen.
     - `MITAAI_AI_REQUEST_FAILED`: logs network failure or error status code without freezing the game.
   - Inspect Unity `Player.log` for any unexpected engine errors.

## Bottom-Centered Screen Subtitles (0.4.6)

Current presentation uses the game's font and UI text effects on a separate screen-space Text. It does NOT invoke native quest dialogue or native letter animation. The world-space driver experiment below failed: localization index -1 caused GlobalLanguage.GetString/Dialogue_3DText.Start errors. The failing path is removed, not patched globally or disguised as working native playback. Replies are still fixed diagnostic fixtures, not AI or voice.

1. Wait until scripted dialogue/choices/cutscenes finish. Approach Mita, aim and press E. Type `hello there`; the orange draft should be bottom-center with the game's rounded language font.
2. Press Enter and leave E alone until the reply. The submitted orange line should reveal and hold, then be replaced by the white/pink `Hello! I'm right here.` line in the same screen location. Move/turn the camera: text must not drift into the room or pile onto a world point. Share a current screenshot in regular chat.
3. Press E during our text. The previous line/queued reply should clear and typing reopen immediately. Cancel; confirm cursor and movement return, including after pause/focus loss.
4. Observe normal story dialogue/choices. Our prompt, input and reply should yield to native dialogue/cutscene/menu control, without advancing, replacing or stopping the story. A silent animation is not automatically classified as dialogue; report any competing interaction.
5. Retest face-close-up aiming, turning away, distance and walls. Native object interactions retain priority; head-envelope geometry is not exact mesh targeting.

Markers are now MITAAI_SCREEN_SUBTITLE_STARTED and MITAAI_TURN_CANCELLED. No MITAAI_NATIVE_REPLY_STARTED/PROGRESS or mod-owned Dialogue_3DText should run. Check Unity Player.log as well as BepInEx for fresh errors. Compilation and 216 managed checks pass; actual rendering/sequence/control coexistence remain unverified until user observations. Historical instructions below do not describe the current driver.

## Layout And Font Retest (0.4.5)

Version 0.4.4 failed visually: native letters piled onto one point. Version 0.4.5 initializes native letter size/spacing and glyphs, keeps a neutral owned parent, and uses the game's language font selector for the orange editable draft. Compilation and 200 managed checks pass; actual native lifecycle, font match and rendering remain manual checks.

1. Aim at nearby Mita, press E, and type `hello there`. Check the draft's font against native orange player dialogue; send a current screenshot in regular chat.
2. Press Enter. Verify individual letters form a readable line instead of overlapping, then observe the fixed Mita reply. Report Player and Mita displays separately.
3. While our reply is showing, aim at Mita and press E again. It should clear our turn and reopen typing without waiting for the full fixture timeout. Cancel and confirm controls recover. Story dialogue/native object interactions must still take priority.
4. Approach her face closely while aiming at her, then turn away and step back beyond range. Check prompt visibility and remote/through-wall rejection. The new head envelope is an AABB approximation, not pixel-perfect aiming.
5. Inspect both BepInEx LogOutput.log and Unity Player.log after submission. Look for nonzero size in progress, the native font family log and any Dialogue_3DText Start/Update errors. Activation without a screenshot/user confirmation is not a pass.

## Orange Player-Subtitle Test (0.4.4)

The pink typing panel and Player/ENTER tabs are removed. The editable draft now uses an unboxed orange line with white outline, local game subtitle font and a caret. Editing is mod-owned, not the native story driver. On Enter, an isolated native Player-themed dialogue echoes a display excerpt of your message, followed by the fixed Mita reply. The translucent curved E prompt is unchanged. No AI or voice exists yet.

1. In the kitchen, approach Mita, aim until the E prompt appears, and press E. Type `hello`. Check that the orange draft is readable and no pink typing panel appears. Send a screenshot in regular chat.
2. Press Enter. Check whether the orange player line appears and then Mita's `Hello! I'm right here.` appears visibly. Describe both separately; native progress in logs is not proof they render. Send a reply screenshot in regular chat.
3. Check movement/look restoration. Once playback finishes, reopen, test Backspace/caret editing, then cancel. Pause or switch away during another turn; there must be no delayed reply after returning.
4. Check ordinary scripted dialogue and native E interactions for overlap, missing text or unexpected progression. Stop if anything unusual occurs. Do not infer compatibility outside this scene.

All 196 managed checks pass. Current runtime discovered both templates and logged one Player -> Mita sequence, but Unity Player.log also contains native Dialogue_3DText Start/Update null-reference errors and sizeSymbol was zero. Native playback, appearance, cleanup and timing remain unresolved. Reply delay is a length-based estimate, not a completion callback. Display excerpts cap at 240 Unicode text elements; input remains capped at 1,000 UTF-16 code units. No content is logged or persistently saved.

## Transparency Test (0.4.3)

Version 0.4.2 screenshots confirm rounded geometry and outlined white text, but the user requested more transparency. Version 0.4.3 replaces the filled border backing with a hollow outline. Panel fill opacity is 38%, prompt 60%, tabs 70%, key badge 80%; white text stays fully opaque. It does not change reply logic or targeting.

Approach Mita, compare the prompt against the room, and open with E. Send a current prompt/field screenshot in normal chat: the room and Mita should be clearly visible through the pink fill, corners should remain rounded, and text should remain legible. Submit a short line and retain the reply visibility/cleanup tests below. Native symbol size was logged as zero in 0.4.2 despite printing progress; visible native reply is still unverified and must not be assumed fixed.

Build and 185 managed checks pass. Actual visual acceptance is pending.

## Native Presentation Test (0.4.2)

This experiment replaces the fixed reply's IMGUI caption with an isolated native `Dialogue_3DText` when an approved inactive template is available. Fonts/text effects come from the game. Bright pink/lilac rounded nine-slice backgrounds, white outlines and fade-slide are mod-controlled and reference-inspired, not exact native sprite/animation clones. Version 0.4.1 activated a reply but the user saw none; 0.4.2 detaches the clone from possibly inactive quest parents and logs native printing progress.

1. Continue to the kitchen in `Scene 4 - StartSecret`. Stand near Mita and aim at her until E / TALK appears.
2. Press E, type `hello`, then Enter. Send a screenshot of both the input frame and the reply. Check whether the native letters animate and are readable, and the old floating mod caption is gone.
3. Check movement/look after submission, and repeat twice. Escape cancels; if it also opens pause, resume first. Native visual input uses Enter/Escape rather than mouse Send/x.
4. If the field still looks like 0.3.x, the native visual assets were unavailable. If a native-subtitle-unavailable notice appears, the template was missing, rejected, or story dialogue was busy. Report it; don't treat this as a successful native subtitle test.
5. Observe ordinary scripted dialogue and native E interactions without opening the mod during them. Check for missing/overlapping subtitles, unexpected voice, look/emotion changes, story progression or orphan text. Stop if anything unexpected happens.
6. Move very close while aiming at her face/body, then step back and look away. The new aimed world-bounds ray should keep close-up targeting; native object interactions still have priority. Check walls, off-to-side targets and distant interaction rejection.

Markers: `MITAAI_NATIVE_TEMPLATE`, `MITAAI_NATIVE_TEMPLATE_REJECTED`, `MITAAI_NATIVE_UI_INVENTORY`, `MITAAI_NATIVE_UI_READY`, `MITAAI_NATIVE_REPLY_STARTED`, `MITAAI_NATIVE_REPLY_PROGRESS`, `MITAAI_NATIVE_REPLY_UNAVAILABLE`, `MITAAI_NATIVE_REPLY_FAILED`. The reply remains `Hello! I'm right here.`, a fixed fixture, not AI. Compilation and 180 managed checks are verified; native rendering/animation/story coexistence are manual gates. Version 0.4.0 failed template admission and used the old field. Version 0.4.1 discovered native font/frame/key-hint artwork and logged activation, but the current screenshots/user report show no visible reply and inadequate styling. Actual visual and reply testing is still required on 0.4.2.

## Previous Nearby E Test (0.3.x)

The 0.3.x interaction replaces F8 with a nearby pink E prompt. Maximum camera-to-renderer-center distance defaults to 2 Unity units, clamped to 0.8-3 via `Interaction/Range`. The viewport center must overlap Mita's projected model bounds, and a collider ray toward her model center must be unobstructed. Native object interactions suppress the mod prompt. Old `Test/Key = F8` is not read; the new binding is `Interaction/Key = E`.

1. In ordinary gameplay, approach Mita closely and aim at her face/body. Wait for the pink E / Talk to Mita prompt.
2. Step back or look away: the prompt should fade out and remote chat must not open. Check with a wall between you as well.
3. Return, aim at Mita, and press E on the keyboard. The pink field should open near the bottom.
4. Type a message containing E and press Enter or Send. E must remain ordinary message text, not cancel the field. Expect the fixed Hello/right-here reply, not an AI response.
5. Reopen and cancel with Escape or x. If the pause menu appears, resume before retrying. Verify movement and camera return and the draft clears.
6. Confirm ordinary native E interactions still work and do not run together with the mod. Provide a screenshot to check field/prompt layout and animation.

The first styling pass uses pink/mulberry translucent panels, 8px corners, pale text, accent buttons, hover feedback, and a 160ms fade/8px slide. It uses the current Unity GUI font, not a verified native game font clone. The prompt was confirmed visually in 0.3.0, but that version failed keyboard opening. 0.3.1 adds GUI key-event handling alongside legacy input and a bounded 200ms eligibility revalidation window. The user confirmed the pink field opens; logs prove the GUI-event opening path and subsequent pause/resume cursor recovery. A screenshot/animation review and broader interaction checks are still needed.

All 152 managed checks pass, including interaction range/aim boundaries and letter E as text. Actual GUI key routing, collider occlusion, visual rendering, and native interaction coexistence remain manual checks. Aim uses projected bounds, not pixel-perfect mesh geometry; non-collider scenery can escape occlusion checks. The legacy instructions below document earlier probes, not the current default controls.

## Earlier Versions

Version 0.2.3 opens a small keyboard composer while facing the nearest gameplay `MitaPerson` with active model bounds overlapping the viewport within 12 Unity units. This includes seated/close-up models even when the head anchor is off-screen. Submitting a nonblank message displays the configured fixed reply above that same Mita. The typed text is validated and discarded; it is not sent to a service or saved. This verifies input plumbing, not AI reasoning. Native dialogue, voice, memory, actions, and the later pink UI redesign remain out of scope.

## Verified Startup

Version 0.2.0 compiled, installed, and reached the menu, but failed the actual F8 test: the standard `GUI.TextField` implementation invokes stripped `GUIStateObjects.GetStateObject`. Its exception closed input and disabled the probe. The camera/player hierarchy was verified before this failure. See [historical text-input startup evidence](../reports/text-input-startup-2026-10-05.json); successful startup did not imply a working field.

Version 0.2.1 avoided that path using native `Input.inputString`, key states, and plain IMGUI labels/boxes. It reached the menu, but actual gameplay logs show false visibility rejections and one field acquisition immediately followed by a control-change close. The user supplied a seated-Mita screenshot confirming the false rejection. See [historical compatibility evidence](../reports/text-input-compatibility-2026-10-05.json).

Installed 0.2.2 replaces single-point visibility with projected renderer bounds and replaces persistent flag/counter writes with a bounded player-controller suspension. It builds with zero warnings/errors and passes 128 managed checks. The user confirmed typing hello and receiving the fixed reply. Logs record two submissions/captions and no probe error, plus a subsequent locked cursor with free movement/look flags. See [gameplay evidence](../reports/text-input-detection-2026-10-05.json). Actual movement/look restoration and the broader G2 checks still need verification.

The user later confirmed movement after submitting. A subsequent 0.2.2 log recorded a pause/control takeover, followed by free input flags but an unlocked cursor that repeatedly blocked reopening. Installed 0.2.3 retains the captured cursor for same-player recovery and restores it only after normal gameplay resumes. It passes 139 managed checks; actual recovery is awaiting the new in-game test.

The earlier 0.1.0 fixed-caption build also passed startup checks on 5 October 2026. See [the original startup report](../reports/probe-startup-2026-10-05.json).

The user subsequently confirmed that F8 displayed the fixed line at the top of the screen. The log records the chosen Mita in `Scene 4 - StartSecret`, path `World/General/Mita`, and repeated caption requests. Caption positioning/tracking quality still needs checking. This single startup and visible-caption result do not complete the model's broader gameplay gates. See `../HANDOFF.md` for current status and the requested later pink UI redesign.

## Legacy F8 Test

1. Launch `D:\MitaAI\MiSide_Dev\MiSideFull.exe`.
2. Load or continue into ordinary gameplay with Mita present. The menu preview is excluded.
3. Stand near Mita and look toward her.
4. Press **F8**, including while Mita is seated or close up. A small text field should stay open near the bottom of the screen. The verified player's controller is temporarily disabled while typing; movement, look, and interaction should stop and the cursor should be visible.
5. Type a short message and press **Enter**, or click **Send**. The field should close, controls should return, and the caption should read **Mita: Hello! I'm right here.** for eight seconds. Every message deliberately receives the same diagnostic reply.
6. Move or turn the camera: the caption should follow Mita's head/model position and hide when she leaves the screen.
7. Reopen with F8; type something and press **Escape** or F8 to cancel. No new reply should appear. Reopen again: the draft should be empty.
8. Walk away and return to Mita, then reopen with F8. If cancellation also opens the game pause menu, resume first and try F8 again. No wait notice should persist after ordinary player control has returned.

If no on-screen gameplay Mita is found, a short notice asks you to look toward Mita nearby. If the player camera cannot be uniquely bound or control is already blocked, the composer refuses to open. Wait until ordinary gameplay resumes. Menu previews are excluded. Do not use this experimental input probe during story dialogue or cutscenes until coexistence is verified.

## Input Verification Gate

Run these checks in the supported scene before adding network calls:

- Submit 20 turns, including spaces, punctuation, and a long message. Confirm one reply per turn and no leaked movement, camera, interaction, or gameplay hotkeys while typing.
- Try an empty/space-only message. It should not submit or produce a reply; Send stays disabled.
- Exercise Backspace, Delete, caret arrows, Home/End, and Ctrl+V. Paste is bounded and line breaks become spaces. There is no selection, mouse caret placement, or IME composition in this minimal keyboard composer.
- Cancel and reopen repeatedly. Verify the cursor and controls return after each close.
- Switch away from the game while composing, then return. The field should be closed and controls usable.
- Change or reload the scene while composing. Verify no stale Mita reply or input lock survives.
- Try F8 while the game already blocks movement or has a menu open. The composer should refuse to open without unblocking the game.
- Check Escape against the game's pause menu. Native keyboard readings do not suppress other scripts reading legacy input; report any unintended game action.

The release paths restore the captured controller enabled state and, on ordinary close, cursor state. Movement/look flags and the interaction value are not modified. Known scripted animation/look/camera state or pause closes the field and yields the cursor. The controller-suspension approach has a community precedent in [MiSide-Multiplayer ChatController](https://github.com/QuestionBlock64/MiSide-Multiplayer/blob/efc1c28f2c85d3dbe7498feeef942e487d6f7eb2/Runtime/Chat/ChatController.cs); this implementation is independent and preserves initial state. Same-value writes to enabled state remain ambiguous. Controller disable side effects, physics drift, cutscene handover, and subsequent recovery are explicitly unverified. These guards are not evidence of broad chapter compatibility.

In 0.2.3, a same-player control takeover retains a one-shot cursor recovery record. Recovery waits for focus, resumed time, enabled/active player, free input flags/interaction, and no known scripted control. It never locks the cursor while those conditions are blocked. Scene change/player unload discards recovery; a new game-owned cursor mode supersedes it. This is not permission to open during menus or suppress the game's Escape handling.

Managed checks run without Unity using linked production composer/restoration code:

```powershell
dotnet run --project tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj --configuration Release
```

All 139 checks passed on 5 October 2026. They cover bounded/blank input, trimming, caret insertion/deletion, Unicode text elements, 20 turns, duplicate submissions, cancel/reopen, restoration, untouched story flags, observable ownership changes, 20 lease cycles, seated/close-up viewport overlap, and deferred pause/resume cursor recovery. Lease checks execute production code with a stubbed Unity boundary, including repeated release, busy input, scripted handover, cursor normalization, and unloaded players. They do not simulate IL2CPP, actual keyboard/camera behaviour, rendering, or story control.

The current probe selects the closest on-screen candidate when several Mitas exist, and logs the chosen instance. Camera projection alone does not test whether a wall occludes Mita. The initial check should take place while facing her in the same room.

## Logs

The loader log is `MiSide_Dev\BepInEx\LogOutput.log`. Useful markers:

- `MITAAI_PLUGIN_LOADED`: plugin entry point ran.
- `MITAAI_MAIN_THREAD_READY`: Unity called the registered behaviour's update method.
- `MITAAI_SCENE`: active scene changed.
- `MITAAI_MITA`: candidate's instance, scene, and hierarchy path.
- `MITAAI_PLAYER_CANDIDATE`: player hierarchy, camera relationship, and input flags when opening is attempted.
- `MITAAI_INPUT_OPENED`: the field acquired a verified player and Mita binding.
- `MITAAI_INPUT_SUBMITTED`: input length only; message content is not logged.
- `MITAAI_INPUT_CLOSED`: cancellation/submission or lifecycle reason.
- `MITAAI_INPUT_UNAVAILABLE`: missing Mita or busy/unbound player input.
- `MITAAI_MITA_VISIBILITY`: projected model visibility and the camera hierarchy on an F8 attempt.
- `MITAAI_INPUT_TAKEOVER`: specific observed state causing automatic closure.
- `MITAAI_CURSOR_RECOVERED`: captured cursor restored after same-player normal gameplay resumes.
- `MITAAI_CAPTION_SHOWN`: a submitted message started fixed-fixture presentation.
- `MITAAI_INPUT_RELEASE_FAILED`: input release encountered an exception; stop testing and inspect the log.
- `MITAAI_PROBE_FAILED` / `MITAAI_PRESENTATION_FAILED`: a runtime exception disabled the probe while ordinary gameplay continues.

`MITAAI_CAPTION_SHOWN` proves the presentation path was requested, not that the caption was visually correct. The in-game check is still required.

## Configuration And Rebuild

BepInEx generates `BepInEx\config\local.mitaai.probe.cfg` on the first plugin launch. It contains the key, fixed test line, and duration. Caption length is capped at 240 characters and duration at 2-30 seconds.

Close the game before rebuilding and reinstalling:

```powershell
.\tools\Build-InstallProbe.ps1
```

The plugin is built for `net6.0` and x64 against the installed loader and this game's generated interop. Game and loader assemblies are not copied into the plugin's output. The installer records the deployed DLL's SHA-256 in `BepInEx\plugins\MitaAI\install-manifest.json` and refuses to overwrite an unexpectedly changed DLL.

To disable the test plugin, close MiSide and move `BepInEx\plugins\MitaAI\MitaAI.Plugin.dll` outside the `plugins` directory. BepInEx can stay installed.
