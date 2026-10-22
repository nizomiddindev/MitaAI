# Game-Context Offline Prompt Formatter

**Date:** 22 October 2026 · **Author:** Antigravity (`D:\MitaAI\local\worktrees\antigravity`, branch `codex/antigravity-work`) · **Status:** implemented and unit-checked; **not connected to AI prompt, not deployed, not runtime-verified**.

---

## Scope & Summary

This task adds a pure offline formatter (`GameContextPromptFormatter.cs`) that accepts the existing `GameContextSnapshot`, revalidates all fields at the formatter boundary, and serializes the observation into a deterministic, single-line, bounded JSON payload (`schema: "mitaai.game_context.v1"`).

No changes were made to `GameContextCollector.cs`, `GameContextSnapshot.cs`, `Plugin.cs`, `AIChatClient.cs`, voice, animation, launcher, or shared documentation (`AGENTS.md`, `HANDOFF.md`). The formatter is not connected to the AI prompt yet.

---

## Design (`src/MitaAI.Plugin/GameContextPromptFormatter.cs`)

### 1. Boundary Revalidation (`GameContextPromptFormatter.Revalidate`)

Even though `GameContextRules` validates observations during collection, `GameContextPromptFormatter.Revalidate(GameContextSnapshot? snapshot)` re-enforces all invariants at the formatter boundary before serialization:
- **Null snapshot**: Converted to an all-unknown snapshot (`Scene = Unknown("missing")`, `PlayerRoom = Unknown("missing")`, `RoomSources = 0`).
- **Scene (`chapterScene`) revalidation**:
  - If `Scene.IsKnown`, `Scene.Raw` is re-checked for `null` (`"missing"`), empty/whitespace (`"empty"`), length `> GameContextRules.MaxRawLength` (`128`), ASCII/Unicode control characters (`char.IsControl`), Unicode line/paragraph separators (`\u2028`, `\u2029`), and unpaired UTF-16 surrogates (`"invalid"`).
  - If `!Scene.IsKnown`, `Scene.Reason` is validated via `NormalizeReason` (`"missing"` fallback when blank; `"invalid-reason"` if `> 64` chars or containing characters outside `[A-Za-z0-9_.-]`).
- **Unloaded scene propagation**:
  - If `Scene` is unknown with reason `"scene-not-loaded"`, `PlayerRoom` is forced to `Unknown("scene-not-loaded")` with `RoomSources = 0`.
- **Player room (`playerRoom`) & source count revalidation**:
  - Negative `RoomSources` (`< 0`) forces `PlayerRoom` to `Unknown("invalid-sources")` and `RoomSources = 0`.
  - `RoomSources == 0` forces `PlayerRoom` to `Unknown("no-source")` if `PlayerRoom.IsKnown` was true, or preserves the validated unknown reason (such as `"not-main-thread"` or `"capture-failed-..."`).
  - `RoomSources > 1` forces `PlayerRoom` to `Unknown("ambiguous-sources")` and clamps `RoomSources` to `MaxReportedSources` (`999`).
  - `RoomSources == 1` revalidates `PlayerRoom` with the same raw/reason rules as `Scene`.

### 2. Structured Serialization & Prompt-Injection Containment (`GameContextPromptFormatter.Format`)

`Format(GameContextSnapshot? snapshot)` emits a canonical, single-line JSON object:

```json
{"schema":"mitaai.game_context.v1","dataOnly":true,"chapterScene":{"status":"known","raw":"Scene 3 - WeTogether","reason":null},"playerRoom":{"status":"known","raw":"Kitchen","reason":null,"source":"Location34_Communication.roomPlayer","eligibleSources":1},"unobserved":["mitaIdentity","mitaRoom","mitaActivity","questProgress","surroundings"]}
```

When a field is unknown:

```json
{"schema":"mitaai.game_context.v1","dataOnly":true,"chapterScene":{"status":"known","raw":"Scene 3 - WeTogether","reason":null},"playerRoom":{"status":"unknown","raw":null,"reason":"no-source","source":"Location34_Communication.roomPlayer","eligibleSources":0},"unobserved":["mitaIdentity","mitaRoom","mitaActivity","questProgress","surroundings"]}
```

Key properties:
- **Chapter/scene and player room are separate**: `chapterScene` and `playerRoom` are independent objects; neither is derived from the other.
- **Zero translation or semantic invention**: Valid raw strings (including casing, leading/trailing spaces, Cyrillic, punctuation, and valid surrogate pairs) are preserved verbatim inside `"raw"` without trimming or friendly-name mapping.
- **Explicit unknown & unobserved state**: Unknown fields report `"status":"unknown"`, `"raw":null`, and a validated `"reason"`. The `"unobserved"` array (`["mitaIdentity","mitaRoom","mitaActivity","questProgress","surroundings"]`) explicitly declares that Mita's identity, Mita's room/position, Mita's activity, quest progress, and surroundings are not observed or inferred.
- **Data-only containment**: Because revalidation rejects all control characters, line/paragraph separators, and unpaired surrogates, and string serialization escapes `"` (`\"`) and `\` (`\\`), raw game strings remain strictly inside JSON string literals and round-trip identically via `System.Text.Json.JsonDocument.Parse`.
- **Documented output-size bound (`MaxOutputLength = 832`)**:
  - Fixed JSON envelope + keys + `unobserved` list + `eligibleSources` (`<= 3` digits): `317` characters.
  - Worst-case `chapterScene.raw` (`128` quotes/backslashes, each escaped to 2 chars): `256` characters.
  - Worst-case `playerRoom.raw` (`128` quotes/backslashes, each escaped to 2 chars): `256` characters.
  - Exact worst-case length: `317 + 256 + 256 = 829` characters (`<= MaxOutputLength = 832`), guarded by an explicit check in `Format`.

---

## Changed Files

- `src/MitaAI.Plugin/GameContextPromptFormatter.cs` (new)
- `tests/MitaAI.ContextChecks/MitaAI.ContextChecks.csproj` (modified: linked `GameContextPromptFormatter.cs`)
- `tests/MitaAI.ContextChecks/Program.cs` (modified: added 59 formatter and boundary-revalidation checks)
- `reports/game-context-formatter.md` (new: this report)

---

## Verification Results

Executed from `D:\MitaAI\local\worktrees\antigravity` on branch `codex/antigravity-work`:

1. **Focused Context Checks (`MitaAI.ContextChecks`)**:
   - Command: `dotnet run --project tests/MitaAI.ContextChecks/MitaAI.ContextChecks.csproj -c Release -p:MitaAITestFramework=net9.0`
   - Result: **PASS: 98 game-context snapshot and formatter checks** (39 existing snapshot/gate checks + 59 new formatter checks).
   - Covered cases:
     - Raw preservation and `JsonDocument.Parse` round-trip for English, lowercase, surrounding spaces, Cyrillic (`"Туалет"`), identifiers, surrogate pairs, and 128-character strings.
     - Independent known/unknown combinations for `chapterScene` and `playerRoom`, plus `null` snapshot and `default(ContextValue)` handling.
     - Boundary rejection of whitespace-only `Known` values, `\n`, `\r`, `\0`, `\u2028`, `\u2029`, unpaired high/low surrogates, and `> 128`-char strings.
     - Boundary rejection/normalization of inconsistent `RoomSources` (`0`, `> 1`, `< 0`, and unloaded scenes) and malformed/overlong reason strings.
     - Prompt-injection containment for quotes, backslashes, JSON delimiters, and instruction-like text.
     - Worst-case escaped output length (`829 <= 832`) and deterministic repeat formatting.
2. **Plugin Release Build (`MitaAI.Plugin`)**:
   - Command: `dotnet build src/MitaAI.Plugin/MitaAI.Plugin.csproj -c Release -p:GameDirectory=D:\MitaAI\MiSide_Dev`
   - Result: **Build succeeded (0 warnings, 0 errors)**.

---

## Remaining Limitations

- **Codex integration review (22 October)**: reviewed original commit 3979942 and cherry-picked it onto main as 66119cf. Independently verified 98 checks in the worker checkout, then the integrated Release plugin build (zero warnings/errors), 98 context checks and 358 input/voice checks. No deployment, prompt connection, game/model launch or push.
- **Serialization is not model instruction isolation**: parse/round-trip tests establish that raw strings cannot add JSON properties or escape their string fields. They do not establish that an LLM will disregard instruction-like content within those strings. The dataOnly flag is descriptive metadata; future prompt integration needs to preserve the distinction between trusted instructions and observed data. The size limit is a UTF-16 character bound, not a byte or token budget.
- **Not connected to AI prompt**: `GameContextPromptFormatter` is an offline helper and is not called by `AIChatClient.cs` or `Plugin.cs`. Prompt integration should wait until the room-walking runtime diagnostic gate in `0.6.2` confirms actual `Location34_Communication.roomPlayer` availability and values in-game.
- **Scope of observations**: Only active scene (`chapterScene`) and `Location34_Communication.roomPlayer` (`playerRoom`) are represented; Peaceful Mode (`Location21_Communication`), `AudioRoom_Room`, Mita's own location/activity/identity, and quest state remain unobserved.
