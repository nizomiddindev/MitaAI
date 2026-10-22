# Game-Context Collector: Scene and Player Room (Step 1)

**Date:** 22 October 2026 · **Author:** Claude · **Status:** implemented and unit-checked; **not integrated, not deployed, not verified at runtime**.

This report covers the first, deliberately small context collector. It records:
- the active Unity scene, which is the chapter identity;
- the raw value of `Location34_Communication.roomPlayer`, which is the player's room in the Chapter 3/4 apartment.

These are kept as two separate facts, and neither is derived from the other. The collector is not connected to `Plugin.cs` or to the AI prompt. Codex will add the diagnostic hook after review.

## Metadata-supported vs runtime-verified

### Codex Review Follow-Up (22 October)

- The shared Release plugin build now passes with zero warnings/errors; existing 358 managed checks also pass. Context checks now total 39 after three scene-cache regression checks. No deployment, game launch, model loading or prompt integration.
- Fixed cache ownership across scene changes: Capture reads active scene identity before the throttle, and ShouldRefresh includes that scene handle. A new scene refreshes even inside the 0.5 s interval. Same-scene calls still reuse the cached result. Unity time access is inside the exception boundary.
- Construct the collector in a known Unity main-thread callback during integration. Matching the constructing thread is not an independent verification that a MonoBehaviour field initializer ran on Unity's main thread. The field-initializer snippet below is a proposal that requires this correction before use.
- Snapshot/gate tests are pure C#; actual Unity enumeration and room values remain unverified. Original 36-check results below describe Claude's earlier delivery; latest count is 39.

| Behaviour | Status | Evidence |
| :--- | :--- | :--- |
| `Location34_Communication` exists, in the global namespace, and derives from `MonoBehaviour` | **Metadata-supported** | This session read the local `MiSide_Dev/BepInEx/interop/Assembly-CSharp.dll` with `System.Reflection.Metadata`. The read was done in a scratch tool, and no game files were changed. |
| `roomPlayer` is a `string` with a getter | **Metadata-supported** | Same metadata read. |
| Reading `roomPlayer` reads a game field rather than calling game code | **Metadata-supported** | The type has `NativeFieldInfoPtr_roomPlayer`. That is the interop wrapper for a native field, so the getter reads the field directly. The game's `PlayerInRoom(string)` method also exists; the collector does not call it. |
| `Location21_Communication.roomPlayer` is an `int`, and the type has a `main` (`Location34_Communication`) | **Metadata-supported**, not used | Same metadata read. It is intentionally left out of this step. |
| The collector compiles against the game interop | **Build-verified** | An isolated scratch project (net6.0/x64, same interop references as the plugin) compiled both new files with 0 warnings and 0 errors. |
| Snapshot rules: raw preservation, missing/invalid/ambiguous → unknown, log format, thread gate, throttle | **Unit-verified** | 36 checks in the new `tests/MitaAI.ContextChecks` (see Test results). |
| `Location34_Communication` is active in `Scene 3 - WeTogether` / `Scene 4 - StartSecret` | **Inferred** (from the discovery report) | Not observed in game. |
| The actual string values of `roomPlayer`, and when they change (on entering a room, in doorways) | **Unknown — needs the runtime test below** | Not observed. No friendly names are assumed. |
| Exactly one eligible instance exists during free roam | **Unknown — needs the runtime test** | If there are several, the collector reports `ambiguous-sources`. |
| Unity behaviour of `FindObjectsOfTypeAll`, `TryCast`, `isActiveAndEnabled` and the scene handle for this type | **Not runtime-verified** for this type | The same calls work for `MitaPerson` and `GameController` in the existing plugin. |

## Design

### `src/MitaAI.Plugin/GameContextSnapshot.cs` (new, pure C#, no Unity)

**`ContextValue`** is either a known raw string or `unknown` together with a reason.
- Known values keep the game's string exactly: no trimming, no case change, no translation, no mapping.
- Unknown reasons: `missing`, `empty`, `invalid`, `scene-not-loaded`, `no-source`, `ambiguous-sources`, `read-failed`, `not-main-thread`, `capture-failed-<ExceptionType>`.

**`GameContextSnapshot(Scene, PlayerRoom, RoomSources, CapturedAt)`** keeps the scene and the room as independent fields.
- `ToLogLine()` produces a single log line, for example:
  `MITAAI_CONTEXT scene="Scene 3 - WeTogether" room="<raw>" roomSource=Location34_Communication.roomPlayer roomSources=1`
  or, for an unknown room, `... room=unknown roomReason=no-source ...`.
- Quotes, backslashes and control characters are escaped only in the log line; the snapshot keeps the raw value.
- The line contains no conversation text, keys or profile names.

**`GameContextRules`** decides what counts as a usable value:
- A value is `missing` if null, `empty` if blank, and `invalid` if longer than 128 characters or containing control characters.
- An unloaded or invalid active scene is reported as `scene-not-loaded`.
- **Exactly one eligible room source is required.** Zero eligible sources gives `no-source`. Two or more gives `ambiguous-sources`, even if they agree, because the collector cannot tell which one is authoritative.

**`ContextCaptureGate`** limits when a capture can run:
- Capture is only allowed on the thread that created the collector.
- Repeated captures within the minimum interval (0.5 s by default) reuse the last snapshot.
- If the clock moves backwards, or the time value is not finite, the next capture refreshes.

### `src/MitaAI.Plugin/GameContextCollector.cs` (new, Unity)

- `Capture()` checks the owning thread **before any Unity call**. On another thread it returns an all-unknown snapshot with reason `not-main-thread`.
- Searches run only on an explicit `Capture()` call, never per frame. Calls within 0.5 s return the cached snapshot, so accidentally calling it every frame searches at most twice per second.
- It reads `SceneManager.GetActiveScene()` (`name`, `IsValid`, `isLoaded`, `handle`), then lists `Location34_Communication` objects through `Resources.FindObjectsOfTypeAll`, the same pattern the plugin already uses for `MitaPerson`.
- An instance is **eligible** only if it is `isActiveAndEnabled` and its scene is valid, loaded, and the active scene (same handle). Prefabs, inactive copies and copies in other scenes are counted as ineligible and ignored.
- The read is observational only. It reads the active scene and the `roomPlayer` field, and never writes game state, calls game methods, hooks anything or changes input.
- Any exception during capture becomes an all-unknown snapshot with reason `capture-failed-<Type>`. A single failed `roomPlayer` read becomes `read-failed`.
- Only `Location34_Communication.roomPlayer` is used. `Location21_Communication` (int room, Peaceful Mode), `AudioRoom_Room`, `World.nameLocation` and `GlobalGame` are left for later steps.

## Changed files

All of these are new; no existing file was modified.

- `src/MitaAI.Plugin/GameContextSnapshot.cs`
- `src/MitaAI.Plugin/GameContextCollector.cs`
- `tests/MitaAI.ContextChecks/MitaAI.ContextChecks.csproj`: a separate test project that links only `GameContextSnapshot.cs`. It uses the same `MitaAITestFramework` convention as `MitaAI.InputChecks` (net10.0 by default, with a net9.0 override).
- `tests/MitaAI.ContextChecks/Program.cs`
- `reports/game-context-implementation.md` (this report)

**Not modified:** `Plugin.cs`, voice, animation/lip-sync, presentation, launcher, configuration, the existing `MitaAI.InputChecks` project, `AGENTS.md`, `HANDOFF.md`. Uncommitted work by other agents (for example `VoiceLipSync.cs`, `UnityInputStubs.cs`) was not touched.

**Note for Codex:** the plugin project includes every `.cs` file in its folder by default, so the two new source files will be compiled into the next plugin build with no change to the plugin's project file.

## Test results

- `dotnet build tests/MitaAI.ContextChecks/MitaAI.ContextChecks.csproj -c Release -p:MitaAITestFramework=net9.0`: 0 warnings, 0 errors.
- `dotnet tests/MitaAI.ContextChecks/bin/Release/net9.0/MitaAI.ContextChecks.dll`: **PASS: 36 game-context snapshot checks.** They cover:
  - scene raw/unloaded/missing/blank;
  - raw room preservation (English, lower case, surrounding spaces, Cyrillic, identifier style, 128-character limit);
  - null/empty/whitespace/control-character/overlong/read-failed values becoming unknown;
  - no source, ineligible-only, one eligible among ineligible copies, conflicting, agreeing duplicates, and valid + missing;
  - scene and room staying independent, and the unavailable snapshot;
  - log-line quoting, escaping and unknown formatting;
  - the thread gate, throttle, backwards clock and non-finite time.
- Isolated interop compile of both new files: 0 warnings, 0 errors.
- **Not run (coordination rule):** the shared plugin build and the existing `MitaAI.InputChecks` suite. Neither depends on these files except through the plugin's default include. Codex should run its usual plugin build at integration. **Not done:** launching MiSide, loading a model, downloading anything, or deploying.

## Proposed integration call (for Codex)

The smallest diagnostic hook is a log line when the player opens the composer with E, which already runs on the Unity main thread inside `Update`.

```csharp
// MitaAITestBehaviour field (constructed on the main thread with the behaviour):
private readonly GameContextCollector contextCollector = new();

// In OpenComposer(), immediately after the existing MITAAI_INPUT_OPENED log line:
Plugin.Logger.LogInfo(contextCollector.Capture().ToLogLine());
```

- `Capture()` does not throw. It returns unknown fields on failure.
- Do **not** add the snapshot to the AI prompt until the runtime test below has recorded the real values.
- The E hook only fires when Mita is aimed at within range, so rooms she is not in cannot be sampled through it. For the walking test, Codex could add a **temporary, test-only** explicit trigger, for example a dedicated debug key in `Update` that calls `Capture()` once per key press. That decision belongs to Codex or the user, and it should be removed or disabled afterwards. It must not run every frame.

## Room-walking runtime test (needed later)

**Preconditions:**
- Codex has integrated the hook (and any optional debug trigger) and installed it with `tools/Build-InstallProbe.ps1` after MiSide was closed.
- Use a Chapter 3 save (`Scene 3 - WeTogether`) in free roam, with no cutscene, native dialogue or choice prompt active.

**Steps:**
1. **Baseline.** In the starting room, trigger one capture. Record the full `MITAAI_CONTEXT` line and, from the player, which room this physically is.
2. **Each room, standing fully inside it (not in a doorway).** Walk through every reachable apartment room in turn. Expected rooms: bedroom, hall/living room, kitchen, bathroom/toilet, corridor if separate. In each room:
   - wait about 2 s, then trigger one capture;
   - record the raw `room` value, `roomSources` and the physical room.
3. **Doorways.** Stand in two doorways and capture. Record whether the value is the previous room, the next room, or unknown.
4. **Return trip.** Walk back through the rooms in reverse order and capture again. This confirms each value is stable and updates in both directions.
5. **Throttle.** Trigger two captures within 0.5 s. The second line should be identical, with the same capture time (not logged; the lines simply match).
6. **Chapter 4.** If a `Scene 4 - StartSecret` save is available, repeat steps 1–2.
7. **Negative check.** In a scene without the apartment controller (the main menu or another chapter), the line should show `room=unknown roomReason=no-source` and still a correct `scene=`.

**Pass criteria:**
- Every physical room produces one stable raw value.
- `roomSources=1` throughout free roam.
- No `ambiguous-sources`, `read-failed` or `capture-failed-*`.
- The scene name matches the chapter.
- No change to gameplay, input, story progression or saves.

**Record:**
- the exact raw strings per physical room, without renaming them;
- doorway behaviour;
- any room that never changes the value.

Only after this, and with the user's approval, map raw values to wording in the prompt.

**Not covered by this step:**
- Peaceful Mode's `Location21_Communication` (int room);
- `AudioRoom_Room` overlap;
- chapters other than 3/4;
- Mita's own room or position;
- any prompt use.
