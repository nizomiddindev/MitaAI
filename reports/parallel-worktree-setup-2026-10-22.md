# Parallel Worktree Setup

Date: 22 October 2026. Local setup only; no push or new agent task dispatch.

## Checkpoint And Isolation

- Source baseline commit: eaf6a40 (Checkpoint v0.6.2 voice, mouth animation and context diagnostics).
- Codex integration: D:\MitaAI, main.
- Claude Code: D:\MitaAI\local\worktrees\claude-code, codex/claude-work.
- Antigravity: D:\MitaAI\local\worktrees\antigravity, codex/antigravity-work.
- Native git worktree add created the worker checkouts from the source checkpoint. They share Git history, but have independent checked-out files and build outputs. Neither is an app-managed Codex worktree; no Codex chat was created or moved.
- No game/model/venv copies or junctions. Each measured about 2.47 MiB including generated build outputs. Existing tracked launcher distribution files are included; they must not be launched from these worktrees.
- Existing game/voice files remain in the integration folder. Worker plugin builds use -p:GameDirectory=D:\MitaAI\MiSide_Dev for local assembly references. No hard-coded game path was added to the plugin project.

## Checkpoint Review

- Reviewed staged paths: no game, local runtime/model, bin/obj, private config, log or model/audio archive files.
- Scanned text candidates for common OpenAI/OpenRouter, Google, GitHub and private-key credential patterns; no hits. This is a bounded pattern scan, not a guarantee that every possible secret format is detectable.
- Confirmed ignore rules cover game config, local voice files, build output and .env.local. Added .env.* protection. The existing launcher DLL/EXE and dependency/runtime manifests were already tracked distributions; their net9 update was retained in the checkpoint.
- Staged whitespace check passed after replacing Markdown hard-break trailing spaces in two newly tracked reports with ordinary paragraph spacing.

## Verification

Both worker checkouts independently passed:

```powershell
dotnet build src/MitaAI.Plugin/MitaAI.Plugin.csproj -c Release -p:GameDirectory=D:\MitaAI\MiSide_Dev
dotnet run --project tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj -c Release -p:MitaAITestFramework=net9.0
dotnet run --project tests/MitaAI.ContextChecks/MitaAI.ContextChecks.csproj -c Release -p:MitaAITestFramework=net9.0
```

Results: zero plugin build warnings/errors, 358 input/AI/voice checks and 39 context checks per checkout. Unity boundaries are stubbed. No game or real voice model was run. Git status was clean in all three checkouts after testing; build outputs remain ignored. No installation was performed during this setup.

## Use

Open the appropriate new folder in each external application before assigning another task. Read AGENTS.md and docs/parallel-development.md there. Existing conversations do not change directories automatically. Worker agents should commit only explicit task files and return commit IDs; Codex reviews and cherry-picks one delivery at a time. No worker should update shared dependencies, run installers/launcher/game/voice tools, change main, or edit another checkout.

Setup-completion documentation is committed on main and fast-forwarded into both clean worker branches before handoff. Subsequent task branches can diverge normally. The main worktree remains the only deployment location, with the user's plugged-in confirmation required before game launch.
