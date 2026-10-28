# MitaAI

**MitaAI** is an injected companion mod for *MiSide* that enables real-time, bidirectional conversation with Mita using modern Large Language Models (LLMs) and local GPU-accelerated voice synthesis.

The project combines an injected BepInEx/Il2Cpp plugin, a persistent local voice worker with mouth blend-shape lip-sync, and a standalone GUI launcher and configurator.

![In-Game Dialogue with Mita](docs/images/mitaai-ingame-dialogue.png)

---

## Key Features (v0.6.2)

- **AI Connectivity**: Connects to **Groq Cloud** (ultra-fast ~0.4s), **LM Studio** (local), **Ollama** (local), **OpenRouter** (cloud free models), or custom OpenAI-compatible endpoints, plus an offline diagnostic fixture.
- **GPU Voice Synthesis (F5-TTS v2)**: Prewarmed Russian speech synthesis powered by an owned background Python worker over stdio JSON IPC. Generates natural dialogue matching Mita's voice in ~3.1–3.5s with zero per-turn CUDA re-initialization penalty and non-blocking cancellation handling.
- **Native Speaking Animation & Lip-Sync**: Dynamically binds to MiSide's native `Audio_BlendShapeVoice` component. Mita visibly speaks in sync with generated audio while preserving story-owned animations (sitting, cooking, walking), and properly closes her mouth during pauses.
- **In-Game Presentation & Subtitles**: Screen-space subtitles rendered using native game fonts and UI shaders. Displays player text in orange with typewriter effects, followed by Mita's AI response in white/pink.
- **Nearby Interaction & Story Awareness**: Aimed `[E]` interaction with distance clamping and occlusion raycasting. Safely yields to native story cutscenes and scripted dialogues.
- **Chat History & Pause Controls**: Press **`H`** to open the chat history overlay, or **`Escape`** to pause the game—both pause speech and mouth movement seamlessly and resume upon exit.
- **Game Context Discovery**: Integrated scene and room detection (`GameContextCollector`) with deterministic offline JSON formatting (`GameContextPromptFormatter`) to prepare grounded situational awareness.

---

## Launcher & Configuration

The standalone GUI launcher ([`Launcher.bat`](Launcher.bat) / `tools/Launcher/MitaAI.Launcher.exe`), built with .NET 9, allows configuring providers, tailoring Mita's persona, measuring latency, and launching the game in one place.

![MitaAI Launcher & Configurator](docs/images/mitaai-launcher-main.png)

### Persona Customization & Live Connection Testing

Fine-tune system prompts, dialogue limits, and test API response times directly before launching the game:

![Persona Customization and Latency Testing](docs/images/mitaai-launcher-persona-test.png)

---

## In-Game Controls

| Key / Shortcut | Action | Description |
|---|---|---|
| **`E`** (Aimed) | **Talk to Mita** | Opens the dialogue composer when looking at nearby Mita. |
| **`Enter`** | **Send Message** | Submits your message to the configured AI provider. |
| **`H`** | **Chat History** | Opens the scrollable conversation history; pauses ongoing speech. |
| **`Escape`** | **Pause Game** | Pauses gameplay, AI voice playback, and speaking animation. |
| **`Ctrl + F9`** | **Context Diagnostic** | Logs a snapshot of the active scene and player room. |

---

## Quick Start

1. **Configure AI**:
   - Open [`Launcher.bat`](Launcher.bat) to start the configurator.
   - Select your provider (**Groq Cloud**, **OpenRouter**, **LM Studio**, or **Ollama**), input your model/key, and click **⚡ Test AI Connection**.
2. **Save & Launch**:
   - Click **💾 Save Configuration**, then click **🎮 Play MiSide**.
3. **Converse with Mita**:
   - Approach Mita in-game (e.g. `Scene 3 - WeTogether`), aim at her, press **`E`**, enter your message, and press **`Enter`**!

---

## Technical Architecture

```
┌────────────────────────────────────────────────────────┐
│                      MiSide Game                       │
│  ┌──────────────────────────────────────────────────┐  │
│  │               MitaAI.Plugin (C#)                 │  │
│  │  - Aimed Raycast & Interactivity Gate            │  │
│  │  - Screen-Space Native Subtitle Canvas           │  │
│  │  - VoiceLipSync (Audio_BlendShapeVoice Binding)  │  │
│  │  - GameContextCollector & Diagnostic Gate        │  │
│  └──────────────────┬─────────────────┬─────────────┘  │
└─────────────────────┼─────────────────┼────────────────┘
                      │ stdio JSON IPC  │ HTTP REST
                      ▼                 ▼
        ┌───────────────────────┐  ┌─────────────────────┐
        │  voice_worker.py (D:) │  │ LLM Endpoint        │
        │  - PyTorch CUDA (F5)  │  │ - Groq / OpenRouter │
        │  - Russian v2 Model   │  │ - LM Studio / Ollama│
        └───────────────────────┘  └─────────────────────┘
```

- **Plugin Engine**: BepInEx 5 / Unity Il2Cpp interop targeting .NET Standard 2.1.
- **Voice Runtime**: Python 3.11 with Torch/CUDA (2.6.0+cu124) running an owned F5-TTS Russian v2 worker over stdio.
- **Launcher**: Standalone WPF application targeting Windows Desktop .NET 9.0 (`net9.0-windows`).
- **Safety & Verification**: Tested across 381+ managed unit checks, 98 context/formatting checks, and 40+ voice worker tests.

---

## Documentation & References

- [Implementation Model & Architecture](docs/implementation-model.md) — Comprehensive technical design and acceptance gates.
- [Handoff & Verification Status](HANDOFF.md) — Live changelog, benchmark results, and current testing state.
- [Parallel Development Protocol](docs/parallel-development.md) — Multi-agent worktree conventions and branch workflows.
- [GitHub Research & Comparative Analysis](docs/research-evidence.md) — Historical research and technical comparison.
- [Standalone Launcher Source](src/MitaAI.Launcher/) & [Executable](tools/Launcher/MitaAI.Launcher.exe)
- [Automated Build & Probe Script](tools/Build-InstallProbe.ps1)
