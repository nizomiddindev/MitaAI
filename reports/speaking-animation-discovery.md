# Speaking Animation Discovery Report

**Date**: 2026-10-21

**Scope**: Read-only investigation into reusing MiSide's native mouth movement / speaking animations for MitaAI's generated speech (`v0.6.1+`).
**Constraints Preserved**: No plugin, launcher, voice, test, configuration, game, or shared-documentation files were modified. No packages/models were downloaded and the game was not launched.

---

## Executive Summary

MiSide's speaking mouth movement is **not** driven by whole-body `Animator` state transitions, dialogue text printers, or quest triggers. Instead, it is driven by a self-contained component on Mita's head mesh: **`Audio_BlendShapeVoice : MonoBehaviour`** (in `Assembly-CSharp.dll`), which samples real-time audio spectrum data (`AudioSource.GetSpectrumData`) from a bound `AudioSource` (`audioVoice`) during `LateUpdate()` and drives the `"O"` mouth blend shape (`shapeO`) while suppressing conflicting facial expression blend shapes (`shapesOffInSpeak`).

Because `Audio_BlendShapeVoice` runs in `LateUpdate()` and reacts purely to `audioVoice.isPlaying` and `audioVoice.GetSpectrumData(...)`:
1. **Whole-body animations** (`MitaPerson.animMita` Layer 0/1: sitting, cooking, walking, idle, story actions) and **facial emotions** (`MitaPerson.FaceEmotionType` on Animator Layer 2) continue to run without conflict.
2. **Pause and resume** (`AudioSource.Pause()` / `UnPause()`) automatically pause and resume mouth movement because `AudioSource.isPlaying` becomes `false` while paused, causing `Audio_BlendShapeVoice` to smoothly close the mouth (`UpdateBlendShapeODeactive()`).
3. **No quest or dialogue events** are ever invoked by `Audio_BlendShapeVoice`.

**Recommendation**: **Ready for a bounded prototype.** The smallest reliable integration is a lightweight session helper (`VoiceLipSyncSession` or `NativePresentation` hook) that locates the targeted Mita's existing `Audio_BlendShapeVoice` component when `VoicePlayback.Play(...)` starts, saves its original `audioVoice` (`AudioSource`) and `enabled` state, temporarily points `blendShapeVoice.audioVoice` to `VoicePlayback`'s `AudioSource`, and restores the original `audioVoice` and `enabled` state when playback completes, is cancelled, changes scene, loses the target, or yields to `StoryBusy`.

---

## Evidence Classification Legend

- **[Source-Confirmed]**: Verified directly in `D:\MitaAI\src\MitaAI.Plugin\*.cs`.
- **[Binary-Confirmed (ISIL + Metadata)]**: Verified from `MiSide_Dev\BepInEx\interop\Assembly-CSharp.dll` signatures and `Cpp2IL` instruction-level (ISIL) disassembly of `MiSide_Dev\GameAssembly.dll` + `global-metadata.dat`.
- **[Inferred]**: Logically derived from binary/source evidence; requires in-game confirmation.
- **[Runtime-Unverified]**: Not yet tested in a live MiSide gameplay session.

---

## 1. How Native Dialogue Activates and Stops Mouth Movement

### 1.1 Primary Mechanism: `Audio_BlendShapeVoice : MonoBehaviour` [Binary-Confirmed (ISIL + Metadata)]

In `Assembly-CSharp.dll`, Mita's mouth movement is controlled by `Audio_BlendShapeVoice` (`TypeDef` in root namespace).

#### Exact Fields & Memory Offsets (`Audio_BlendShapeVoice`)
| Offset | Type | Field Name | Default / Purpose | Evidence |
|---|---|---|---|---|
| `0x18` | `float` | `intensity` | `1.0f` (set in `.ctor` at `RVA 0x2EB690`); scales spectrum sensitivity | ISIL + Metadata |
| `0x20` | `string` | `nameShapeO` | `"O"` (set in `.ctor` at `RVA 0x2EB690`); target mouth blend shape name | ISIL + Metadata |
| `0x28` | `UnityEngine.AudioSource` | `audioVoice` | The `AudioSource` monitored every frame in `LateUpdate()` | ISIL + Metadata |
| `0x30` | `UnityEngine.AnimationCurve` | `animationMouth` | Evaluates open-mouth curve over `timeAnimationFloat` | ISIL + Metadata |
| `0x38` | `Il2CppReferenceArray<Audio_BlendShapeVoice_Intensity>` | `shapesOffInSpeak` | Facial blend shapes temporarily suppressed while speaking | ISIL + Metadata |
| `0x40` | `UnityEngine.AnimationCurve` | `animationOffInSpeak` | Suppression curve evaluated over `intensityOff` | ISIL + Metadata |
| `0x48` | `float` | `intensityOff` | `0.0f..1.0f` ramp weight for suppressing `shapesOffInSpeak` | ISIL + Metadata |
| `0x4C` | `float` | `timeOffInSpeak` | Hold timer (`0.4f` seconds) keeping conflicting shapes suppressed between syllables | ISIL + Metadata |
| `0x50` | `UnityEngine.SkinnedMeshRenderer` | `mesh` | Head `SkinnedMeshRenderer` resolved via `GetComponent<SkinnedMeshRenderer>()` | ISIL + Metadata |
| `0x58` | `int` | `shapeO` | Blend shape index on `mesh.sharedMesh` matching `nameShapeO` (`"O"`) | ISIL + Metadata |
| `0x5C` | `float` | `timeAnimationFloat` | `0.0f..1.0f` cycle timer for `animationMouth` | ISIL + Metadata |
| `0x60` | `float` | `intensotyO` | *(Native typo)* `0.0f..1.0f` active mouth-open envelope weight | ISIL + Metadata |
| `0x64` | `float` | `randomMaxO` | Random peak weight (`Random.Range(60f, 100f)`) refreshed each mouth cycle | ISIL + Metadata |

Supporting element type `Audio_BlendShapeVoice_Intensity : Il2CppSystem.Object`:
- `0x10`: `int indexShape`
- `0x14`: `float intensity`
- `0x18`: `string nameBlendshape`

#### Exact Lifecycle & Frame Loop (`Audio_BlendShapeVoice`) [Binary-Confirmed (ISIL + Metadata)]
1. **`Start()` (`RVA 0x2EB150`)**:
   - Checks `GlobalGame.LanguageVoice == ""` (`GlobalGame` static field `+0x38`). If the user has disabled game voiceovers (`LanguageVoice == ""`), it calls `this.enabled = false`.
   - Even when `this.enabled = false`, `Start()` still executes the rest of initialization:
     - `this.mesh = base.GetComponent<SkinnedMeshRenderer>()`
     - Iterates `0 .. mesh.sharedMesh.blendShapeCount - 1` comparing `mesh.sharedMesh.GetBlendShapeName(i) == this.nameShapeO` (`"O"`) and stores the index in `this.shapeO`.
     - Initializes `this.randomMaxO = Random.Range(60f, 100f)`.
     - Populates `nameBlendshape` on each entry in `this.shapesOffInSpeak`.
2. **`LateUpdate()` (`RVA 0x2EAD30`)**:
   - Checks `this.audioVoice.isPlaying` (`AudioSource.get_isPlaying`, instruction `022`).
     - **Note**: `this.audioVoice` is dereferenced without a Unity `Object != null` check in `LateUpdate()`. It must never be left `null` while `Audio_BlendShapeVoice.enabled` is `true`.
   - **When `audioVoice.isPlaying == true`**:
     - Allocates `float[] samples = new float[64]` and calls `this.audioVoice.GetSpectrumData(samples, 0, FFTWindow.Rectangular)` (`AudioSource.GetSpectrumDataHelper`, instruction `043`).
     - Reads `samples[1]` (`[rdi+0x24]`, low-frequency fundamental voice band) and compares it against a threshold scaled by `AudioListener.volume * this.intensity`.
     - **Voiced syllable (`samples[1]` > threshold)**:
       - Resets hold timer `this.timeOffInSpeak = 0.4f` (`0x3ECCCCCD`).
       - Advances `this.timeAnimationFloat += Time.deltaTime * 5f`; when `> 1f`, wraps to `0f` and picks a new `this.randomMaxO = Random.Range(60f, 100f)`.
       - Ramps `this.intensotyO` toward `1.0f` (`+ Time.deltaTime * 8f`).
       - Evaluates `this.animationMouth.Evaluate(this.timeAnimationFloat) * this.randomMaxO` and lerps `this.mesh.SetBlendShapeWeight(this.shapeO, ...)`!
     - **Silent gap between words (`samples[1]` <= threshold)**:
       - Calls `UpdateBlendShapeODeactive()` (`RVA 0x2EB370`), ramping `this.intensotyO` down toward `0f` and lerping blend shape `shapeO` closed.
   - **When `audioVoice.isPlaying == false`** (stopped or paused):
     - Calls `UpdateBlendShapeODeactive()` (`RVA 0x2EB370`), smoothly closing the mouth.
   - **Conflicting Blend Shape Suppression (`UpdateBlendShape()` at `RVA 0x2EB490`)**:
     - While `this.timeOffInSpeak > 0f`, decrements `timeOffInSpeak -= Time.deltaTime`, ramps `this.intensityOff` toward `1.0f`, and lerps every blend shape index in `this.shapesOffInSpeak` toward `0f` using `this.animationOffInSpeak.Evaluate(this.intensityOff)`.
     - Once `0.4s` of silence elapses (`timeOffInSpeak == 0f`), ramps `this.intensityOff` back down to `0f` so normal facial expression blend shapes on `mesh` are no longer suppressed.
3. **`Update()` (`RVA 0x2EB5E0`)**:
   - When `this.intensotyO == 0f`, lerps `mesh.GetBlendShapeWeight(shapeO)` toward `0f`.
4. **`RebindBlendshapes()` (`RVA 0x2EAFF0`)**:
   - Re-scans `this.mesh.sharedMesh` for `nameShapeO` (`"O"`) and `shapesOffInSpeak` by name. Called by `MitaClothes.Start()` (`RVA 0x518DC0`) when outfit/head meshes are swapped.

### 1.2 How Native Dialogue Triggers Voice Playback (`Dialogue_3DText` & `DataValues_Sounds`) [Binary-Confirmed (ISIL + Metadata)]

Disassembly of `Dialogue_3DText::Start` (`RVA 0x3D4190`), `Dialogue_3DText::LoadVoice` (`RVA 0x3D3060`), and `Dialogue_3DText::Update` (`RVA 0x3D5230`) confirms how native 3D dialogue interacts with Mita:
- `Dialogue_3DText` holds `GameObject speaker` (`0x60`) and `DataValues_Sounds sounds` (`0xB0`).
- In `Dialogue_3DText::Start()` (instructions `306–405`), if `speaker != null`:
  - If `lookOnPlayer` (`0x70`) is true, it saves `MitaPerson.magnet.lookOnPlayer` (`+0x8A`) into `wasTimeRandomLook` (`0x54`) and calls `MitaPerson.magnet.LookOnPlayer()`.
  - If `emotionStart >= 0` (`0x68`), it calls `MitaPerson.FaceEmotionType(emotionStart)`.
- To play voice audio (instructions `457–538` and `LoadVoice` instructions `026–051`), `Dialogue_3DText` fetches an `AudioClip` via `World.GetVoice(indexString, this, false)` and plays it directly on `sounds.audioVoice` (`[sounds+0x18]`):
  ```csharp
  sounds.audioVoice.pitch = 1.0f;
  sounds.audioVoice.clip = this.audioVoice;
  sounds.audioVoice.Play();
  ```
- **Crucial finding**: `Dialogue_3DText` never calls `Audio_BlendShapeVoice` or sets any speaking parameter on `Animator`. Mouth movement happens automatically because `Audio_BlendShapeVoice.audioVoice` points to the speaker's voice `AudioSource` (`sounds.audioVoice`).

### 1.3 Other Investigated Types (Ruled Out as Primary Mechanism) [Binary-Confirmed (ISIL + Metadata)]
- **`Animator_AudioFloat` (`RVA 0x2E7C80`)**: Reads `anim.GetFloat(floatName)` in `Update()` and drives `audioSource.volume` and `audioSource.pitch`. This is animation-to-audio modulation (e.g., footsteps/ambient loops), **not** audio-to-mouth lip sync.
- **`AudioVoicePlayer` (`RVA 0x3C0CE0`)**: Scene timeline / subtitle helper with `AudioSource audioSource` (`0x28`), `AudioClip[] voices` (`0x20`), and `PlayNextClip()`. Does not drive blend shapes directly.
- **`AudioDialogue` (`RVA 0x3C0880`)**: Selects localized `DataValues_Sounds` by `GlobalGame.LanguageVoice` (`"RU"`, `"JP"`, etc.) and calls `SetAudioVoice(AudioClip)`.

---

## 2. Independence from Native Dialogue, Audio, Quests, and Whole-Body Animations

| Subsystem | Independent? | Evidence & Explanation |
|---|---|---|
| **Native Dialogue (`Dialogue_3DText`)** | **Yes** [Binary-Confirmed] | `Audio_BlendShapeVoice` has zero references to `Dialogue_3DText`, `GameController`, or `DialogueGlobal`. It only reads `this.audioVoice.isPlaying` and `this.audioVoice.GetSpectrumData(...)`. |
| **Quests / Story Events (`UnityEvent`)** | **Yes** [Binary-Confirmed] | `Audio_BlendShapeVoice` contains no `UnityEvent` fields and invokes no external callbacks. Quest progression in native dialogue is owned by `Dialogue_3DText.eventFinish` (`0x78`), `eventFinishOk` (`0x80`), and `eventDontVoice` (`0xC0`), which are never touched. |
| **Whole-Body Animations (`MitaPerson.animMita`)** | **Yes** [Binary-Confirmed] | `Audio_BlendShapeVoice` does not reference `Animator` at all. It only calls `SkinnedMeshRenderer.GetBlendShapeWeight` / `SetBlendShapeWeight` on `this.mesh` in `Update()` and `LateUpdate()`. |
| **Native Voice `AudioSource` (`sounds.audioVoice`)** | **Yes, if `audioVoice` reference is temporarily swapped** [Binary-Confirmed] | Instead of playing our clip on Mita's native `AudioSource` (which could conflict with spatial falloff, mixers, or `AudioDialogue`), we can keep playback on `VoicePlayback`'s mod-owned 2D `AudioSource` and temporarily set `blendShapeVoice.audioVoice = voicePlayback.Source` during our speech turn, restoring the original `AudioSource` afterward. |

---

## 3. Coexistence with Sitting, Cooking, Walking, and Story Animations

### 3.1 Skeletal Body Animations (`MitaPerson.animMita` Base Layer) [Binary-Confirmed (ISIL + Metadata)]
- `MitaPerson` drives body posture and movement (sitting on the couch, cooking/chopping in the kitchen, walking via `NavMeshAgent` + `"Walk"` bool, or scripted story animations via `AnimationPlay`) through `MitaPerson.animMita` (`0x38`).
- Unity evaluates `Animator` bone transforms and animation curves before `MonoBehaviour.LateUpdate()`.
- Because `Audio_BlendShapeVoice.LateUpdate()` runs after `Animator` evaluation and only modifies blend shape weights (`shapeO` and `shapesOffInSpeak`) on the head `SkinnedMeshRenderer`, Mita's body posture, locomotion, hand props, and head look (`Character_Look` at `MitaPerson+0x28`) remain 100% intact while she speaks.

### 3.2 Facial Expressions (`MitaPerson.FaceEmotionType` / Animator Layer 2) [Binary-Confirmed (ISIL + Metadata)]
- Disassembly of `MitaPerson::FaceEmotionType` (`RVA 0x51C000`) and `MitaPerson::Update` (`RVA 0x51CFB0`) shows that facial expressions use **Animator Layer 2** (`animMita.SetLayerWeight(2, weight)`), controlled by integer parameter `"FaceEmotion"` (`0..20` or `-1`), trigger `"FaceEmotionNext"`, and float `"NoEmotion"`.
- Animator Layer 2 animates facial blend shapes (smile, angry brows, blink, etc.) during the `Animator` update pass.
- `Audio_BlendShapeVoice` was specifically architected by MiSide's developers to layer on top of Animator Layer 2 in `LateUpdate()`:
  - While voiced audio is detected (`timeOffInSpeak > 0`), `UpdateBlendShape()` suppresses only the conflicting mouth blend shapes configured in `shapesOffInSpeak` and drives `shapeO` (`"O"`).
  - Eye blinks, eyebrows, and non-conflicting facial expressions on Layer 2 continue uninterrupted.
  - Within `0.4s` after audio stops or pauses, `intensityOff` and `intensotyO` ramp back to `0`, returning full control of all mouth blend shapes to Animator Layer 2 without resetting any Animator parameters or layer weights.

---

## 4. Support for Currently Targeted Mita & Variant Differences

### 4.1 Locating `Audio_BlendShapeVoice` on the Current Turn's `MitaPerson` [Source-Confirmed + Binary-Confirmed]
1. In [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L81-L82), `MitaAITestBehaviour` already holds the bound character for the active interaction in `private MitaPerson? target;` (set in [`OpenComposer()`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L408) and validated against `VoiceStamp` in [`BeginVoice()`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1062) and [`TickVoice()`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1079-L1088)). Do **not** scan for a different nearby Mita when starting playback.
2. Disassembly of `MitaPerson` (`RVA 0x51D3F0`) and `MitaClothes::Start` (`RVA 0x518DC0`, instructions `265–307`) shows that `MitaPerson` exposes `Animator animMita` (`0x18`) and `SkinnedMeshRenderer meshHead` (`0x68`), and `Audio_BlendShapeVoice` is attached to the head `SkinnedMeshRenderer` `GameObject`.
3. Given the bound `MitaPerson target`, `VoicePlayback` can discover candidate `Audio_BlendShapeVoice` components in order of specificity:
   - First check `target.meshHead != null ? target.meshHead.GetComponent(Il2CppType.Of<Audio_BlendShapeVoice>())?.TryCast<Audio_BlendShapeVoice>() : null`.
   - Next check `(target.animMita != null ? target.animMita.gameObject : target.gameObject).GetComponentsInChildren(Il2CppType.Of<Audio_BlendShapeVoice>(), false)`.
   - Finally check `target.gameObject.GetComponentsInChildren(Il2CppType.Of<Audio_BlendShapeVoice>(), false)`.

### 4.2 Potential Variant Differences & Edge Cases
1. **Game Voiceover Disabled in Settings (`GlobalGame.LanguageVoice == ""`)** [Binary-Confirmed]:
   - If a player has MiSide's built-in voiceover turned off in game settings, `Audio_BlendShapeVoice.Start()` sets `blendShapeVoice.enabled = false` (`RVA 0x2EB150`, instruction `030`), even though `mesh` and `shapeO` are still initialized.
   - **Mitigation**: When binding our `AudioSource`, record `bool beforeEnabled = blendShapeVoice.enabled;` and `bool enabledByLease = !beforeEnabled;`, set `blendShapeVoice.enabled = true` for the duration of MitaAI playback, and restore `blendShapeVoice.enabled = beforeEnabled` on release only if our binding still owns the component.
2. **Pause When `Time.timeScale == 0` (`Escape` Menu)** [Binary-Confirmed]:
   - Both `Audio_BlendShapeVoice.Update()` (`RVA 0x2EB5E0`) and `UpdateBlendShapeODeactive()` (`RVA 0x2EB370`, called from `LateUpdate()` when `!audioVoice.isPlaying`) multiply their closing lerp step by `Time.deltaTime`.
   - When the game is paused via `Escape`, `Time.timeScale` is `0` (`Time.deltaTime == 0`). In `UpdateBlendShapeODeactive()`, `SetBlendShapeWeight(shapeO, current + (curveTarget - current) * intensotyO)` will keep the mouth frozen open if `intensotyO` and `shapeO` weight are not explicitly zeroed!
   - **Mitigation**: When paused (`shouldPause || Time.timeScale <= 0f`) or releasing while our binding still owns `audioVoice`, explicitly set `intensotyO = 0f`, `timeAnimationFloat = 0f`, `timeOffInSpeak = 0f`, `intensityOff = 0f`, and `mesh.SetBlendShapeWeight(shapeO, 0f)`. Because `UpdateBlendShapeODeactive()` computes `0 + (curveTarget - 0) * 0 = 0` when `intensotyO == 0` and `current == 0`, the mouth closes immediately and stays closed across paused frames without touching `Animator` or invoking native lifecycle methods.
3. **Unverified or Incompatible `Audio_BlendShapeVoice` State** [Binary-Confirmed]:
   - In `Audio_BlendShapeVoice.Start()` (`RVA 0x2EB150`), if `Start()` has not run or `nameShapeO` was not found on `sharedMesh`, `shapeO` remains `0` (default `int`), and `LateUpdate()` dereferences `animationMouth`, `shapesOffInSpeak[i]`, and `animationOffInSpeak` without null checks.
   - **Mitigation**: Validate without calling `Start()` or `RebindBlendshapes()` that `mesh != null`, `mesh.enabled`, `mesh.sharedMesh != null`, `animationMouth != null`, `shapeO >= 0 && shapeO < mesh.sharedMesh.blendShapeCount`, `mesh.sharedMesh.GetBlendShapeName(shapeO) == nameShapeO`, any non-empty `shapesOffInSpeak` has `animationOffInSpeak != null` and valid indices, and `audioVoice != null && !audioVoice.isPlaying`. If any check fails, skip lip-sync and continue normal audio playback.

---

## 5. Recommended Minimal Approach & Lifecycle Integration

### 5.1 Why Reusing `Audio_BlendShapeVoice` via a Dedicated `VoiceLipSync` Helper Is Optimal

We evaluated two candidate designs:
- **Option A (Recommended): Dedicated `VoiceLipSync` binding helper owned by `VoicePlayback` that temporarily binds `blendShapeVoice.audioVoice` to `VoicePlayback`'s `AudioSource` during active MitaAI playback.**
  - Uses 100% native curves (`animationMouth`, `animationOffInSpeak`), native syllable randomization (`randomMaxO`), and native conflicting-shape suppression (`shapesOffInSpeak`) while `Time.timeScale > 0`.
  - Explicitly zeroes `intensotyO`, `timeAnimationFloat`, `timeOffInSpeak`, `intensityOff`, and `mesh.SetBlendShapeWeight(shapeO, 0f)` during pause (including `Time.timeScale == 0`) and on stop/release.
  - Touches zero `Animator` parameters and never invokes native lifecycle methods (`Start` / `RebindBlendshapes`).
- **Option B (Contingency Fallback Only): Custom blend shape weight driver in plugin.**
  - Unnecessary unless runtime testing shows `GetSpectrumData` fails on the mod-owned `AudioSource`.

### 5.2 Strict Ownership Rules (Protecting Story & Native State)

To guarantee we never overwrite story-owned state or leave a dangling `AudioSource` reference:
1. **Capture on Bind (`VoicePlayback.Play` in `MitaAITestBehaviour.TickVoice`)**:
   - Refuse if `storyBusy` is true, if `target` is null/inactive/unloaded, or if the component fails validation or its native `audioVoice.isPlaying` is true.
   - Save `beforeAudioVoice = candidate.audioVoice`, `beforeEnabled = candidate.enabled`, `enabledByLease = !beforeEnabled`, and `shapeO = candidate.shapeO`.
   - Zero residual mouth state, set `candidate.audioVoice = modSource`, and set `candidate.enabled = true` if needed.
2. **Use `UnityEngine.Object` Equality (`==`), Not `ReferenceEquals`**:
   - `Il2CppInterop` property getters (`candidate.audioVoice`) construct a fresh C# wrapper around the native `IntPtr` on each access. Ownership checks must use `candidate.audioVoice == modSource` (`UnityEngine.Object.op_Equality`), not `ReferenceEquals`.
3. **Never Leave `audioVoice == null` While `enabled == true`**:
   - Because `Audio_BlendShapeVoice.LateUpdate()` dereferences `this.audioVoice` without a null check when `enabled == true`, releasing the binding must restore `candidate.audioVoice = beforeAudioVoice` (if `beforeAudioVoice != null` and loaded) or set `candidate.enabled = false` before clearing `audioVoice`.
4. **Guard Against Mid-Turn Native Takeover**:
   - If `candidate.audioVoice != modSource` (native code reassigned `audioVoice`) or if `beforeAudioVoice.isPlaying` becomes true, release the lease without clobbering the native `AudioSource`, `enabled` state, or mouth weights.

### 5.3 Exact Lifecycle Integration Points [Source-Confirmed]

| Lifecycle Event | Current Code Location | Speaking Animation Action |
|---|---|---|
| **Text arrives / TTS synthesis starts** | [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L198-L203) (`Update`), [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L451-L456) (`SubmitMessage`) -> [`BeginVoice`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1044-L1073) | **Do nothing.** Mouth remains closed while waiting for synthesis or subtitle visibility. |
| **Actual audio playback starts** | [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1100-L1105) (`MitaAITestBehaviour.TickVoice` -> `voicePlayback.Play(waitingAudio, Plugin.VoiceVolume.Value, target, native.StoryBusy)`) | After `source.Play()` succeeds in [`VoicePlayback.Play`](file:///d:/MitaAI/src/MitaAI.Plugin/VoicePlayback.cs#L23-L51), `VoiceLipSync.TryBind` validates and binds `Audio_BlendShapeVoice` on the turn's existing `MitaAITestBehaviour.target`. |
| **Pause / Resume (`H` History, `Escape` / `Time.timeScale == 0`, Focus Loss)** | [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1099-L1106) (`TickVoice`), [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1155-L1158) (`OnApplicationFocus`), [`VoicePlayback.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/VoicePlayback.cs#L53-L59) (`Tick`) | `voicePlayback.Tick(paused, native.StoryBusy)` pauses/unpauses `source` and calls `lipSync.Tick(paused, storyBusy)`. When `paused` or `Time.timeScale <= 0`, `VoiceLipSync` explicitly zeroes `intensotyO`, `timeAnimationFloat`, `timeOffInSpeak`, `intensityOff`, and sets `mesh.SetBlendShapeWeight(shapeO, 0f)`. |
| **Normal Playback Completion** | [`VoicePlayback.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/VoicePlayback.cs#L58) (`Tick` when `!shouldPause && !source.isPlaying` -> `Stop()`) | `VoicePlayback.Stop()` calls `lipSync.Release()`, closing the mouth and restoring `beforeAudioVoice` and `beforeEnabled` if still owned. |
| **Cancellation / Reset / Failure / Shutdown** | [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1122-L1154) (`CancelVoice`, `ShutdownVoice`, `DisableAfterFailure`, `ResetBinding`, `OnDisable`, `OnDestroy`, `OnApplicationQuit`) -> [`VoicePlayback.Stop`](file:///d:/MitaAI/src/MitaAI.Plugin/VoicePlayback.cs#L61-L67) / [`Dispose`](file:///d:/MitaAI/src/MitaAI.Plugin/VoicePlayback.cs#L69-L75) | Every path calls `voicePlayback.Stop()` / `Dispose()`, which unconditionally calls `lipSync.Release()`. |
| **Story / Cutscene Interruption (`StoryBusy`)** | [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L262-L267) (`LateUpdate`), [`Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs#L1079-L1084) (`TickVoice`) | `CancelVoice()` and `lipSync.Tick(..., storyBusy)` immediately release the binding and restore `beforeAudioVoice`. |

### 5.4 Prototype Files & Touch Points
1. **`src/MitaAI.Plugin/VoiceLipSync.cs`** (new):
   - Dedicated binding helper managing validation, ownership tracking, explicit `Time.timeScale == 0` / pause mouth closure, takeover detection, and safe restoration.
2. **`src/MitaAI.Plugin/VoicePlayback.cs`**:
   - Owns `VoiceLipSync`, discovers `Audio_BlendShapeVoice` candidates on the passed `MitaPerson? target` in `Play(...)`, updates `lipSync.Tick(...)` in `Tick(...)` / `LateTick(...)`, and calls `lipSync.Release()` in `Stop()` (covering `Dispose()` and all stop paths).
3. **`src/MitaAI.Plugin/Plugin.cs`**:
   - Passes `target` and `native.StoryBusy` to `voicePlayback.Play(...)` and `native.StoryBusy` to `voicePlayback.Tick(...)` / `LateTick(...)`.
4. **`tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj`, `UnityInputStubs.cs`, `Program.cs`**:
   - Links `VoiceLipSync.cs` and verifies candidate validation, ownership preservation, native takeover non-interference, missing/incompatible graceful fallback, and explicit mouth closure when paused with `Time.timeScale == 0`.

---

## 6. Short In-Game Acceptance Test Plan

Once implementation is approved and deployed:
1. **Speech Start Delay Check**:
   - Aim at Mita and press `E` to trigger a spoken Russian response.
   - Verify Mita's mouth does **not** move while `[Mita is thinking...]` is shown or while F5-TTS is generating audio, and **starts moving at the exact moment audible Russian speech begins**.
2. **Coexistence with Posture/Activity Check**:
   - Trigger speech while Mita is walking, sitting, or performing an idle/room animation.
   - Verify her body animation and facial expression continue naturally while her mouth opens and closes with syllables.
3. **Pause & Resume Check (`H` and `Escape`)**:
   - During a spoken reply, press `H` (chat history) and then `Escape` (pause menu).
   - Verify audio pauses and Mita's mouth closes smoothly while paused, then resumes moving in sync when unpaused.
4. **Completion & Re-Interaction Check**:
   - Let the spoken reply finish naturally.
   - Verify Mita's mouth closes completely, `BepInEx/LogOutput.log` logs lip-sync release, and pressing `E` for another turn works again.
5. **Story / Scene Interruption Check**:
   - Trigger a spoken reply immediately before a scripted story interaction or scene transition.
   - Verify MitaAI speech cancels cleanly, restores `Audio_BlendShapeVoice.audioVoice` to the native `AudioSource`, and native story dialogue still animates Mita's mouth normally.

---

## 7. Remaining Uncertainties Requiring Gameplay Verification

1. **`AudioSource.GetSpectrumData` Amplitude on the Mod-Owned 2D `AudioSource`** [Runtime-Unverified]:
   - `Audio_BlendShapeVoice.LateUpdate()` samples `this.audioVoice.GetSpectrumData(samples, 0, FFTWindow.Rectangular)` (`samples[1]`) and compares it against a threshold scaled by `AudioListener.volume * this.intensity`. Live gameplay testing is required to confirm that the mod-owned 2D `AudioSource` (default volume `0.8f`) produces natural mouth movement amplitude without needing an `intensity` adjustment.
2. **Script Execution Order Between `Audio_BlendShapeVoice.LateUpdate` and `MitaAITestBehaviour.LateUpdate` During `Time.timeScale == 0` Pause** [Runtime-Unverified]:
   - `VoiceLipSync` explicitly sets `intensotyO = 0f`, `timeAnimationFloat = 0f`, `timeOffInSpeak = 0f`, `intensityOff = 0f`, and `mesh.SetBlendShapeWeight(shapeO, 0f)` in both `Update()` (`VoicePlayback.Tick`) and `LateUpdate()` (`VoicePlayback.LateTick`). Because `UpdateBlendShapeODeactive()` evaluates `current + (target - current) * intensotyO` (`0 + (target - 0) * 0 = 0`), zeroing `intensotyO` and `shapeO` in `Update()` guarantees `Audio_BlendShapeVoice.LateUpdate()` keeps the weight at `0` even if `Audio_BlendShapeVoice.LateUpdate()` runs after `MitaAITestBehaviour.LateUpdate()`. This should still be visually confirmed in-game during an `Escape` pause.
3. **Per-Variant Prefab Coverage** [Runtime-Unverified]:
   - Confirm in `BepInEx/LogOutput.log` (`MITAAI_VOICE_PLAYING ... lipSync=bound`) that the active Mita in the test save exposes a compatible `Audio_BlendShapeVoice` with a verified `shapeO` index and non-null `audioVoice`.

---

## 8. Prototype Implementation & Verification Summary

### Codex Review Follow-Up

- Independently rebuilt the Release plugin (zero warnings/errors) and ran 358 managed checks successfully. Native animation behavior remains stubbed in these checks; visual pause closure and spectrum response require real gameplay verification.
- Added a release guard for changed head meshes: CloseMouth verifies the cached index still names the mouth shape before writing its weight. A regression with a replacement mesh that uses the old mouth index for Blink failed before this fix and passed after it. Changes limited to VoiceLipSync.cs and two checks in Program.cs, plus project/report documentation.
- No deployment, game launch or GPU/model loading. User must be notified and confirm plugged-in readiness before a game test. The original 356-check result below is Antigravity's earlier verification; the latest managed count is 358.

- **Build & Test Results**:
  - `dotnet build src/MitaAI.Plugin/MitaAI.Plugin.csproj --nologo --no-restore`: **0 warnings, 0 errors**.
  - `dotnet run --project tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj -p:MitaAITestFramework=net9.0 --no-restore`: **PASS: 356 managed checks** (including 27 new `VoiceLipSync` checks covering candidate validation, `meshHead` prioritization, explicit `Time.timeScale == 0` pause closure vs. native `deltaTime` behavior, story/native voice ownership preservation, mid-turn native takeover handling, unloaded native source safety, and graceful fallback on missing/incompatible components).
- **Deployment Status**:
  - **Not deployed and MiSide was not launched.** Ready for coordinated installation and manual gameplay acceptance testing.
- **Files Changed in `D:\MitaAI`**:
  - [`src/MitaAI.Plugin/VoiceLipSync.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/VoiceLipSync.cs) (created)
  - [`src/MitaAI.Plugin/VoicePlayback.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/VoicePlayback.cs) (modified)
  - [`src/MitaAI.Plugin/Plugin.cs`](file:///d:/MitaAI/src/MitaAI.Plugin/Plugin.cs) (modified)
  - [`tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj`](file:///d:/MitaAI/tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj) (modified)
  - [`tests/MitaAI.InputChecks/UnityInputStubs.cs`](file:///d:/MitaAI/tests/MitaAI.InputChecks/UnityInputStubs.cs) (modified)
  - [`tests/MitaAI.InputChecks/Program.cs`](file:///d:/MitaAI/tests/MitaAI.InputChecks/Program.cs) (modified)
  - [`reports/speaking-animation-discovery.md`](file:///d:/MitaAI/reports/speaking-animation-discovery.md) (updated)
