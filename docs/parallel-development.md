# Parallel Development

## Checkouts And Ownership

| Agent | Folder | Branch |
| --- | --- | --- |
| Codex, integration | D:\MitaAI | main |
| Claude Code | D:\MitaAI\local\worktrees\claude-code | codex/claude-work |
| Antigravity | D:\MitaAI\local\worktrees\antigravity | codex/antigravity-work |

Open the assigned folder as the project in the agent's application. An existing chat pointed at D:\MitaAI does not automatically move when a worktree is created. Explicitly verify the folder and branch before starting another task. No new agent task has been dispatched by this setup.

Each checkout has its own files and bin/obj outputs. Git history and refs are shared. Git operations such as branch deletion or reset can still affect other worktrees; do not operate on other agents' branches. A worktree isolates edits, but overlapping changes can still conflict when integrated. Assign bounded tasks and identify shared-file dependencies first.

## Dependencies And Builds

The game remains at D:\MitaAI\MiSide_Dev and voice resources at D:\MitaAI\local\voice. Nothing is copied or linked into worker checkouts. Game assemblies are build references only. Worker agents must not install plugins, launch the game/launcher, run voice workers or GPU benchmarks, install dependencies, alter game/config/model files, or change the shared voice environment.

From either worker checkout, compile the plugin using the explicit shared assembly path:

```powershell
dotnet build src/MitaAI.Plugin/MitaAI.Plugin.csproj -c Release -p:GameDirectory=D:\MitaAI\MiSide_Dev
```

Pure managed tests use the installed .NET 9 runtime:

```powershell
dotnet run --project tests/MitaAI.InputChecks/MitaAI.InputChecks.csproj -c Release -p:MitaAITestFramework=net9.0
dotnet run --project tests/MitaAI.ContextChecks/MitaAI.ContextChecks.csproj -c Release -p:MitaAITestFramework=net9.0
```

Restore/build output is local to each worktree. No Unity executable or voice model is needed for these commands. Voice tools and the launcher resolve resources relative to their own paths: do not run them from these source-only checkouts. Codex coordinates any shared runtime validation in the integration checkout.

## Task Delivery

1. Read AGENTS.md, HANDOFF.md, the assigned task and current code. Check git status and branch.
2. Work only on the assigned branch and files. Do not update shared handoff/instruction files for task progress; write a task-specific report.
3. Run relevant checks. Commit explicit task files, not git add -A. Do not include game assets, models, caches, credentials or private config. Reports should distinguish automated checks from actual gameplay verification.
4. Return the branch, commit IDs, changed files, verification results and limitations. Do not merge into main or push unless requested.
5. Codex reviews the diff, cherry-picks one task at a time, resolves conflicts without discarding either agent's work, and runs integration checks before deployment. Once a delivery is complete and the branch is clean, Codex coordinates updating it to the latest integration baseline before its next task.

Only Codex updates shared HANDOFF.md and deploys with Build-InstallProbe.ps1. The user must confirm plugged-in readiness before a game launch. Do not treat a compiled or installed build as gameplay-verified.

## Private Files

Game files, local runtimes/models, build outputs, logs and private cfg/ini/env/key files are ignored. .env.* variants are also excluded. Ignore rules do not remove secrets already tracked; inspect staged paths and diffs before every commit. Nothing is pushed by this workflow setup.
