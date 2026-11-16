import base64
import os
import subprocess
import pymupdf

def get_base64_image(path):
    with open(path, "rb") as f:
        data = f.read()
    return f"data:image/png;base64,{base64.b64encode(data).decode('utf-8')}"

images_dir = r"D:\MitaAI\docs\images"
img_dialogue = get_base64_image(os.path.join(images_dir, "mitaai-ingame-dialogue.png"))
img_humor = get_base64_image(os.path.join(images_dir, "mitaai-ingame-humor.png"))
img_refusal = get_base64_image(os.path.join(images_dir, "mitaai-ingame-refusal.png"))
img_launcher = get_base64_image(os.path.join(images_dir, "mitaai-launcher-main.png"))
img_persona = get_base64_image(os.path.join(images_dir, "mitaai-launcher-persona-test.png"))

svg_diagram = """
<svg viewBox="0 0 960 410" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg" style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;">
  <defs>
    <linearGradient id="cardGrad1" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#1e1b4b" stop-opacity="0.95"/>
      <stop offset="100%" stop-color="#0f172a" stop-opacity="0.98"/>
    </linearGradient>
    <linearGradient id="cardGrad2" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#311042" stop-opacity="0.95"/>
      <stop offset="100%" stop-color="#180d2e" stop-opacity="0.98"/>
    </linearGradient>
    <linearGradient id="cardGrad3" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#064e3b" stop-opacity="0.95"/>
      <stop offset="100%" stop-color="#062e26" stop-opacity="0.98"/>
    </linearGradient>
    <filter id="shadow" x="-3%" y="-3%" width="106%" height="106%">
      <feDropShadow dx="0" dy="3" stdDeviation="4" flood-color="#000000" flood-opacity="0.25"/>
    </filter>
  </defs>

  <!-- Background Canvas -->
  <rect width="960" height="410" rx="10" fill="#0b0f19" stroke="#1e293b" stroke-width="1.2"/>

  <!-- Left: Game Process -->
  <rect x="20" y="20" width="455" height="370" rx="10" fill="url(#cardGrad1)" stroke="#38bdf8" stroke-width="1.2" stroke-opacity="0.6" filter="url(#shadow)"/>
  <rect x="20" y="20" width="455" height="34" rx="10" fill="#0284c7" fill-opacity="0.2"/>
  <circle cx="38" cy="37" r="4.5" fill="#38bdf8"/>
  <text x="52" y="42" fill="#f8fafc" font-size="13" font-weight="700" letter-spacing="0.4">GAME PROCESS: MiSide (Unity 2021 IL2CPP x64)</text>

  <!-- 1. Spatial Targeting -->
  <rect x="36" y="66" width="423" height="58" rx="6" fill="#1e293b" fill-opacity="0.8" stroke="#475569" stroke-width="1"/>
  <text x="48" y="85" fill="#38bdf8" font-size="11.5" font-weight="700">1. Spatial Targeting &amp; Story FSM Gate</text>
  <text x="48" y="101" fill="#cbd5e1" font-size="10">Raycast aim detection (≤2.0m clamped), occlusion check &amp; StoryBusy FSM.</text>
  <text x="48" y="115" fill="#94a3b8" font-size="9.5">Safely yields input/camera ownership to native quests and cutscenes.</text>

  <!-- 2. Presentation Canvas -->
  <rect x="36" y="134" width="423" height="58" rx="6" fill="#1e293b" fill-opacity="0.8" stroke="#475569" stroke-width="1"/>
  <text x="48" y="153" fill="#f472b6" font-size="11.5" font-weight="700">2. Presentation Engine &amp; Subtitle Canvas</text>
  <text x="48" y="169" fill="#cbd5e1" font-size="10">Screen-space Canvas reusing native font ('Rounded Mplus 1c') and shaders.</text>
  <text x="48" y="183" fill="#94a3b8" font-size="9.5">24 chars/s typewriter pacing; unscaled time freezing during pause.</text>

  <!-- 3. Lip-Sync -->
  <rect x="36" y="202" width="423" height="58" rx="6" fill="#1e293b" fill-opacity="0.8" stroke="#475569" stroke-width="1"/>
  <text x="48" y="221" fill="#c084fc" font-size="11.5" font-weight="700">3. Procedural Lip-Sync (Audio_BlendShapeVoice)</text>
  <text x="48" y="237" fill="#cbd5e1" font-size="10">Direct memory binding to Mita's 3D facial mesh blend shapes.</text>
  <text x="48" y="251" fill="#94a3b8" font-size="9.5">Preserves body/story animations; full mouth closure on pause (H/Escape).</text>

  <!-- 4. Context Telemetry -->
  <rect x="36" y="270" width="423" height="58" rx="6" fill="#1e293b" fill-opacity="0.8" stroke="#475569" stroke-width="1"/>
  <text x="48" y="289" fill="#34d399" font-size="11.5" font-weight="700">4. Game Context Telemetry &amp; Formatting</text>
  <text x="48" y="305" fill="#cbd5e1" font-size="10">Active scene &amp; room tracking (Location34_Communication.roomPlayer).</text>
  <text x="48" y="319" fill="#94a3b8" font-size="9.5">Deterministic JSON formatter with anti-injection schema (max 832 chars).</text>

  <!-- Engine Badge -->
  <rect x="36" y="338" width="423" height="36" rx="5" fill="#0f172a" stroke="#334155" stroke-width="1"/>
  <text x="48" y="360" fill="#e2e8f0" font-size="10.5" font-weight="600">Plugin Engine: <tspan fill="#38bdf8">BepInEx 6 IL2CPP C#</tspan> | Frame Overhead: <tspan fill="#4ade80">&lt; 0.1ms/frame</tspan></text>

  <!-- Inter-process Connectors -->
  <!-- Upper REST -->
  <path d="M 475 95 L 535 95" stroke="#60a5fa" stroke-width="2" stroke-dasharray="4,3"/>
  <polygon points="535,91 543,95 535,99" fill="#60a5fa"/>
  <text x="478" y="87" fill="#93c5fd" font-size="9.5" font-weight="700">HTTP REST</text>

  <!-- Lower stdio IPC -->
  <path d="M 475 235 L 535 235" stroke="#e879f9" stroke-width="2"/>
  <polygon points="535,231 543,235 535,239" fill="#e879f9"/>
  <path d="M 543 250 L 483 250" stroke="#4ade80" stroke-width="2"/>
  <polygon points="483,246 475,250 483,254" fill="#4ade80"/>
  <text x="480" y="228" fill="#f0abfc" font-size="9" font-weight="700">stdio JSON</text>
  <text x="482" y="265" fill="#86efac" font-size="9" font-weight="700">PCM Audio</text>

  <!-- Right: Top Box (LLMs) -->
  <rect x="545" y="20" width="395" height="175" rx="10" fill="url(#cardGrad2)" stroke="#ec4899" stroke-width="1.2" stroke-opacity="0.6" filter="url(#shadow)"/>
  <rect x="545" y="20" width="395" height="32" rx="10" fill="#be185d" fill-opacity="0.2"/>
  <circle cx="563" cy="36" r="4.5" fill="#f43f5e"/>
  <text x="577" y="41" fill="#fdf2f8" font-size="12.5" font-weight="700" letter-spacing="0.4">REASONING &amp; CONVERSATION PIPELINE</text>

  <!-- Cloud LLMs -->
  <rect x="562" y="62" width="361" height="56" rx="6" fill="#180d2e" fill-opacity="0.8" stroke="#701a75" stroke-width="1"/>
  <text x="574" y="80" fill="#f472b6" font-size="11" font-weight="700">Cloud High-Speed Endpoints (Groq / OpenRouter)</text>
  <text x="574" y="96" fill="#e2e8f0" font-size="9.5">Groq Llama-3.3 / GPT-OSS: <tspan fill="#4ade80" font-weight="700">~577ms round-trip</tspan></text>
  <text x="574" y="109" fill="#a78bfa" font-size="8.5">Reasoning tag suppression &amp; automated zero-effort think filters.</text>

  <!-- Local LLMs -->
  <rect x="562" y="126" width="361" height="56" rx="6" fill="#180d2e" fill-opacity="0.8" stroke="#701a75" stroke-width="1"/>
  <text x="574" y="144" fill="#c084fc" font-size="11" font-weight="700">Offline Local Endpoints (LM Studio / Ollama)</text>
  <text x="574" y="160" fill="#e2e8f0" font-size="9.5">Llama-3.2 3B Instruct Q4_K_M: <tspan fill="#38bdf8" font-weight="700">~2.5s inference</tspan></text>
  <text x="574" y="173" fill="#a78bfa" font-size="8.5">Fully private, offline, 3.0 GB local VRAM footprint.</text>

  <!-- Right: Bottom Box (Voice Synthesis) -->
  <rect x="545" y="205" width="395" height="185" rx="10" fill="url(#cardGrad3)" stroke="#10b981" stroke-width="1.2" stroke-opacity="0.6" filter="url(#shadow)"/>
  <rect x="545" y="205" width="395" height="32" rx="10" fill="#047857" fill-opacity="0.2"/>
  <circle cx="563" cy="221" r="4.5" fill="#34d399"/>
  <text x="577" y="226" fill="#ecfdf5" font-size="12.5" font-weight="700" letter-spacing="0.4">NEURAL SPEECH WORKER (F5-TTS Russian v2)</text>

  <rect x="562" y="246" width="361" height="98" rx="6" fill="#062e26" fill-opacity="0.8" stroke="#065f46" stroke-width="1"/>
  <text x="574" y="265" fill="#34d399" font-size="11" font-weight="700">Persistent PyTorch 2.6.0+cu124 Worker (Python 3.11)</text>
  <text x="574" y="282" fill="#e2e8f0" font-size="9.5">• Prewarmed CUDA resident engine (no per-turn boot cost).</text>
  <text x="574" y="297" fill="#e2e8f0" font-size="9.5">• Warm 32-step synthesis: <tspan fill="#4ade80" font-weight="700">3.11 – 3.47 s</tspan> (RTF 0.95–0.98).</text>
  <text x="574" y="312" fill="#e2e8f0" font-size="9.5">• 16-step high-speed mode: <tspan fill="#38bdf8" font-weight="700">~1.61 s</tspan> (~48% faster).</text>
  <text x="574" y="327" fill="#a7f3d0" font-size="8.5">• Non-blocking worker retention upon submitted cancellation.</text>

  <rect x="562" y="351" width="361" height="30" rx="5" fill="#042f2e" stroke="#115e59" stroke-width="1"/>
  <text x="574" y="371" fill="#cbd5e1" font-size="9.5">Target Hardware: <tspan fill="#facc15" font-weight="700">RTX 3050 Laptop 6GB</tspan> | Peak VRAM: <tspan fill="#facc15" font-weight="700">4.47 GB</tspan></text>
</svg>
"""

html_content = f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>MitaAI - University Project Portfolio</title>
  <style>
    @page {{
      size: A4 portrait;
      margin: 11mm 12mm 11mm 12mm;
    }}

    * {{
      box-sizing: border-box;
      margin: 0;
      padding: 0;
    }}

    body {{
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
      color: #1e293b;
      background: #ffffff;
      line-height: 1.4;
      font-size: 8.8pt;
    }}

    .page {{
      height: 274mm;
      max-height: 274mm;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      page-break-after: always;
      break-after: page;
    }}

    .page-content {{
      flex-grow: 1;
    }}

    .page-footer {{
      border-top: 1px solid #cbd5e1;
      padding-top: 4pt;
      margin-top: 6pt;
      font-size: 7.2pt;
      color: #64748b;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }}

    /* Typography */
    h1 {{
      font-size: 17.5pt;
      font-weight: 800;
      color: #0f172a;
      letter-spacing: -0.4px;
      line-height: 1.18;
    }}

    h2 {{
      font-size: 11.5pt;
      font-weight: 700;
      color: #0f172a;
      margin-top: 8pt;
      margin-bottom: 4pt;
      padding-bottom: 2pt;
      border-bottom: 1.2px solid #e2e8f0;
      letter-spacing: -0.2px;
      display: flex;
      align-items: center;
      gap: 5px;
    }}

    h3 {{
      font-size: 9.5pt;
      font-weight: 700;
      color: #1e293b;
      margin-top: 5pt;
      margin-bottom: 2pt;
    }}

    p {{
      margin-bottom: 4.5pt;
      color: #334155;
      text-align: justify;
    }}

    strong {{
      color: #0f172a;
    }}

    /* Header Banner */
    .header {{
      background: linear-gradient(135deg, #0f172a 0%, #1e1b4b 60%, #311042 100%);
      color: #ffffff;
      padding: 12pt 14pt;
      border-radius: 7pt;
      margin-bottom: 8pt;
      box-shadow: 0 3px 8px rgba(15, 23, 42, 0.12);
    }}

    .header-badge-row {{
      display: flex;
      gap: 5px;
      flex-wrap: wrap;
      margin-bottom: 6pt;
    }}

    .badge {{
      display: inline-block;
      padding: 2pt 5.5pt;
      font-size: 7pt;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.4px;
      border-radius: 3.5pt;
      background: rgba(255, 255, 255, 0.15);
      color: #f8fafc;
      border: 1px solid rgba(255, 255, 255, 0.2);
    }}

    .badge-accent {{
      background: linear-gradient(90deg, #ec4899, #f43f5e);
      border: none;
      color: #ffffff;
    }}

    .badge-blue {{
      background: linear-gradient(90deg, #0ea5e9, #3b82f6);
      border: none;
      color: #ffffff;
    }}

    .badge-green {{
      background: linear-gradient(90deg, #10b981, #059669);
      border: none;
      color: #ffffff;
    }}

    .header-subtitle {{
      font-size: 9pt;
      color: #cbd5e1;
      margin-top: 3pt;
      margin-bottom: 6pt;
      font-weight: 400;
    }}

    .header-meta-grid {{
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 6pt;
      margin-top: 6pt;
      padding-top: 6pt;
      border-top: 1px solid rgba(255, 255, 255, 0.15);
      font-size: 7.5pt;
    }}

    .meta-item span {{
      display: block;
      color: #94a3b8;
      font-size: 6.5pt;
      text-transform: uppercase;
      font-weight: 600;
    }}

    .meta-item strong {{
      color: #f8fafc;
      font-size: 8pt;
    }}

    /* Highlights / KPI Grid */
    .kpi-grid {{
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 6pt;
      margin-bottom: 8pt;
    }}

    .kpi-card {{
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 5pt;
      padding: 5.5pt 7.5pt;
      border-left: 3px solid #3b82f6;
    }}

    .kpi-card.purple {{ border-left-color: #8b5cf6; }}
    .kpi-card.pink {{ border-left-color: #ec4899; }}
    .kpi-card.emerald {{ border-left-color: #10b981; }}

    .kpi-val {{
      font-size: 11.5pt;
      font-weight: 800;
      color: #0f172a;
      line-height: 1.1;
    }}

    .kpi-lbl {{
      font-size: 6.8pt;
      font-weight: 700;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.3px;
      margin-top: 1.5pt;
    }}

    .kpi-sub {{
      font-size: 6.5pt;
      color: #94a3b8;
      margin-top: 1.5pt;
    }}

    /* Figures & Gallery */
    .figure-container {{
      margin: 5pt 0;
      border: 1px solid #cbd5e1;
      border-radius: 5pt;
      overflow: hidden;
      background: #f8fafc;
    }}

    .figure-container img {{
      width: 100%;
      height: auto;
      display: block;
    }}

    .figure-caption {{
      padding: 4pt 7.5pt;
      font-size: 7.5pt;
      color: #475569;
      background: #f1f5f9;
      border-top: 1px solid #e2e8f0;
      line-height: 1.3;
    }}

    .figure-caption strong {{
      color: #0f172a;
    }}

    .figure-grid-2 {{
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 6pt;
      margin: 5pt 0;
    }}

    /* Tables */
    table {{
      width: 100%;
      border-collapse: collapse;
      margin: 5pt 0 7pt 0;
      font-size: 7.6pt;
    }}

    th {{
      background: #f1f5f9;
      color: #0f172a;
      text-align: left;
      padding: 3.5pt 5pt;
      font-weight: 700;
      border: 1px solid #cbd5e1;
    }}

    td {{
      padding: 3pt 5pt;
      border: 1px solid #e2e8f0;
      color: #334155;
    }}

    tr:nth-child(even) td {{
      background: #f8fafc;
    }}

    /* Callouts */
    .callout {{
      background: #f8fafc;
      border-left: 3px solid #6366f1;
      padding: 4.5pt 6.5pt;
      border-radius: 0 4pt 4pt 0;
      font-size: 7.6pt;
      margin: 4.5pt 0;
      color: #334155;
    }}

    .callout-title {{
      font-weight: 700;
      color: #1e1b4b;
      margin-bottom: 1.5pt;
    }}

    ul {{
      margin-left: 14pt;
      margin-bottom: 4pt;
    }}

    li {{
      margin-bottom: 2pt;
      color: #334155;
      font-size: 8.2pt;
    }}
  </style>
</head>
<body>

  <!-- ================= PAGE 1 ================= -->
  <div class="page">
    <div class="page-content">
      <div class="header">
        <div class="header-badge-row">
          <span class="badge badge-accent">Capstone / Portfolio Project</span>
          <span class="badge badge-blue">Systems Programming &amp; Low-Level Hooking</span>
          <span class="badge badge-green">Applied Generative AI &amp; Neural Voice</span>
          <span class="badge">Unity IL2CPP C#</span>
        </div>
        <h1>MitaAI: Real-Time Neural Dialogue, GPU Voice Synthesis &amp; Procedural Lip-Sync</h1>
        <div class="header-subtitle">
          An Injected Low-Latency Systems Architecture for Autonomous, Story-Aware Companions in Native 3D Game Engines
        </div>
        <div class="header-meta-grid">
          <div class="meta-item">
            <span>Author / Applicant</span>
            <strong>Nizomiddin</strong>
          </div>
          <div class="meta-item">
            <span>Target Application</span>
            <strong>University Application Project Portfolio</strong>
          </div>
          <div class="meta-item">
            <span>Target Game / Engine</span>
            <strong>MiSide (Unity 2021 IL2CPP x64)</strong>
          </div>
          <div class="meta-item">
            <span>Repository</span>
            <strong>github.com/nizomiddindev/MitaAI</strong>
          </div>
        </div>
      </div>

      <!-- KPI Bar -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <div class="kpi-val">577 ms</div>
          <div class="kpi-lbl">LLM Response Latency</div>
          <div class="kpi-sub">Sub-second cloud &amp; 2.5s local Llama-3.2</div>
        </div>
        <div class="kpi-card purple">
          <div class="kpi-val">3.19 s</div>
          <div class="kpi-lbl">Neural Speech Synthesis</div>
          <div class="kpi-sub">Persistent CUDA worker (RTF: 0.95–0.98)</div>
        </div>
        <div class="kpi-card pink">
          <div class="kpi-val">100% Native</div>
          <div class="kpi-lbl">Blend-Shape Lip-Sync</div>
          <div class="kpi-sub">Bound to Audio_BlendShapeVoice in-game</div>
        </div>
        <div class="kpi-card emerald">
          <div class="kpi-val">519+ Tests</div>
          <div class="kpi-lbl">Verification Suite</div>
          <div class="kpi-sub">381 managed + 98 context + 40 Python</div>
        </div>
      </div>

      <h2>1. Project Abstract &amp; Problem Statement</h2>
      <p>
        Modern narrative video games rely almost exclusively on static, pre-recorded audio files and branching dialogue trees. While modern Large Language Models (LLMs) and neural Text-to-Speech (TTS) models offer unprecedented conversational flexibility, integrating them dynamically into commercial compiled games presents severe engineering bottlenecks: compiled unmanaged game engines (Unity IL2CPP) strip metadata and enforce strict execution threads; neural voice synthesis incurs high cold-start latencies and VRAM overheads; and unconstrained AI responses inevitably break game balance, quest triggers, or character persona.
      </p>
      <p>
        <strong>MitaAI</strong> bridges this gap. It is a production-grade, injected companion modification developed for the game <em>MiSide</em>. The system injects directly into the live Unity 2021.3.35f1 runtime via BepInEx 6 IL2CPP, enabling bidirectional, voice-synthesized, lip-synced conversations with the protagonist Mita. It achieves this while strictly honoring native quest progression, cutscene priority, game pause states, and maintaining 60+ FPS performance on consumer laptop hardware (NVIDIA RTX 3050 6GB).
      </p>

      <h2>2. System Architecture &amp; Dataflow Pipeline</h2>
      <p>
        The system isolates heavy machine learning workloads from Unity's sensitive render thread using an asynchronous multi-process architecture:
      </p>

      <div class="figure-container">
        {svg_diagram}
        <div class="figure-caption">
          <strong>Figure 1: Architectural Pipeline of MitaAI.</strong> The Unity game process communicates via asynchronous HTTP REST to LLM inference endpoints and bidirectional stdio JSON IPC to a dedicated background Python voice worker. Unity's main thread is shielded from compute blocking, maintaining steady 60+ FPS frame rates.
        </div>
      </div>
    </div>
    <div class="page-footer">
      <span>MitaAI Project Portfolio — Page 1 of 4</span>
      <span>github.com/nizomiddindev/MitaAI</span>
    </div>
  </div>

  <!-- ================= PAGE 2 ================= -->
  <div class="page">
    <div class="page-content">
      <h2>3. In-Game Verification &amp; Visual Evidence</h2>
      <p>
        Actual in-game captures demonstrating the real-time dialogue system, subtitle rendering with native typography, dynamic facial animation, and persona adherence during active gameplay.
      </p>

      <div class="figure-container">
        <img src="{img_dialogue}" alt="Flagship In-Game Dialogue" style="max-height: 122mm; object-fit: contain;">
        <div class="figure-caption">
          <strong>Figure 2: Live In-Game Voice Dialogue with Mita (Scene 3 - WeTogether).</strong> The player approaches Mita sitting in her gaming chair, presses [E] to talk, and converses in Russian. Subtitles render via a mod-owned screen Canvas utilizing the game's authentic font ('Rounded Mplus 1c') and custom outline shaders. Simultaneously, Mita's voice plays through an injected native AudioSource while her mouth moves in exact synchronization using procedural blend shapes.
        </div>
      </div>

      <div class="figure-grid-2">
        <div class="figure-container">
          <img src="{img_humor}" alt="Mita Character Persona Witty Banter" style="max-height: 80mm; object-fit: cover;">
          <div class="figure-caption">
            <strong>Figure 3: Witty Character Banter.</strong> When asked to run, Mita responds playfully: <em>"Run? Where, away from you? Nah, I'm too lazy for that."</em> Demonstrates successful injection of persona prompts and dynamic typing animation.
          </div>
        </div>
        <div class="figure-container">
          <img src="{img_refusal}" alt="Immunity to Assistant Drift" style="max-height: 80mm; object-fit: cover;">
          <div class="figure-caption">
            <strong>Figure 4: Anti-Assistant Drift Verification.</strong> When prompted to write Python code, Mita refuses: <em>"Ugh, coding? That sounds so boring. Why can't we just play instead?"</em> Validating adherence to character boundaries.
          </div>
        </div>
      </div>

      <div class="callout" style="margin-top: 6pt;">
        <div class="callout-title">Visual Presentation Engineering Highlights:</div>
        <strong>Font &amp; Shader Fidelity:</strong> Subtitles are constructed using MiSide's authentic font asset discovered at runtime (<code>UI_TextFontLanguage.FontUpdate</code>) with copied Coffee.UIEffects gradient/outline materials. Text reveals at 24 Unicode characters/sec with typewriter animation. Pausing freezes subtitle deadlines in lockstep (<code>PauseTick</code>), ensuring text remains pinned on screen without visual desynchronization.
      </div>
    </div>
    <div class="page-footer">
      <span>MitaAI Project Portfolio — Page 2 of 4</span>
      <span>github.com/nizomiddindev/MitaAI</span>
    </div>
  </div>

  <!-- ================= PAGE 3 ================= -->
  <div class="page">
    <div class="page-content">
      <h2>4. Desktop Launcher &amp; Configurator (.NET 9 WPF)</h2>
      <p>
        To eliminate fragile text configuration and provide seamless user experience, MitaAI includes a standalone desktop suite (<code>MitaAI.Launcher.exe</code>) compiled for Windows Desktop .NET 9.0:
      </p>

      <div class="figure-grid-2">
        <div class="figure-container">
          <img src="{img_launcher}" alt="MitaAI Standalone Launcher" style="max-height: 85mm; object-fit: contain;">
          <div class="figure-caption">
            <strong>Figure 5: Full Launcher &amp; Configurator Suite.</strong> Allows selecting providers (Groq, LM Studio, Ollama, OpenRouter), setting token limits (120 tokens max), and running live latency benchmarks.
          </div>
        </div>
        <div class="figure-container">
          <img src="{img_persona}" alt="Persona & Latency Validation" style="max-height: 85mm; object-fit: contain;">
          <div class="figure-caption">
            <strong>Figure 6: Persona &amp; Constraint Testing.</strong> Real-time validation of system prompts, character tone presets, and API response speed (577ms for Groq, verified live).
          </div>
        </div>
      </div>

      <h2>5. Core Technical Innovations (Part 1)</h2>

      <h3>A. Low-Level Unity IL2CPP Reverse Engineering</h3>
      <p>
        <em>MiSide</em> is compiled using Unity 2021 IL2CPP, where C# source code is converted to C++ and stripped of standard reflection metadata:
      </p>
      <ul>
        <li><strong>Unstripped Primitive Architecture:</strong> Standard Unity IMGUI controls crashed with <code>NotSupportedException: Method unstripping failed</code> inside <code>GUIStateObjects.GetStateObject</code>. MitaAI was re-engineered using pure unstripped engine primitives (<code>GUI.Label</code>, <code>GUI.Button</code>, custom mathematical event dispatchers) and custom screen-space Canvas shaders.</li>
        <li><strong>Audio Clip Memory Binding:</strong> Feeding generated PCM audio into Unity initially triggered <code>RuntimeWrappedException</code> due to IL2CPP array length wrapper mismatches. MitaAI resolves this by discovering the native generated static binding of <code>AudioClip.SetData</code> via Mono.Cecil and passing exact unmanaged buffer pointers.</li>
      </ul>

      <h3>B. Persistent Neural Voice Worker (F5-TTS v2 with PyTorch &amp; CUDA)</h3>
      <p>
        Neural diffusion models typically suffer from substantial initialization overhead (70+ seconds for imports, 4+ seconds for weights). Initializing the model per dialogue turn would destroy immersion:
      </p>
      <div class="callout">
        <div class="callout-title">Prewarmed Stdio IPC Architecture:</div>
        A persistent background Python process (<code>voice_worker.py</code>) runs on an isolated virtual environment (Torch 2.6.0+cu124). It prewarms the CUDA context and loads diffusion weights once at boot. When a dialogue line arrives from Unity, synthesis completes in <strong>3.11–3.47 seconds</strong> (RTF: 0.95–0.98). Cancellation (when a player walks away or triggers a cutscene) marks active generation as abandoned while preserving the warm worker, avoiding expensive 25-second cold restarts.
      </div>
    </div>
    <div class="page-footer">
      <span>MitaAI Project Portfolio — Page 3 of 4</span>
      <span>github.com/nizomiddindev/MitaAI</span>
    </div>
  </div>

  <!-- ================= PAGE 4 ================= -->
  <div class="page">
    <div class="page-content">
      <h2>5. Core Technical Innovations (Part 2)</h2>

      <h3>C. Procedural Facial Lip-Sync &amp; Native Animation Arbitrage</h3>
      <p>
        MitaAI dynamically discovers the active <code>MitaPerson</code> entity in the scene and acquires a lease on its native <code>Audio_BlendShapeVoice</code> component:
      </p>
      <ul>
        <li><strong>Preservation of Narrative Animations:</strong> Because mouth movement operates through procedural blend shapes on top of the underlying skeletal hierarchy, Mita can sit, cook, walk, and react to game physics without skeletal distortion or animation cancellation.</li>
        <li><strong>Pause &amp; State Synchronization:</strong> When the player presses <kbd>H</kbd> (Chat History) or <kbd>Escape</kbd> (Pause Menu), the plugin intercepts <code>Time.timeScale == 0</code>, slides presentation deadlines forward in lockstep, and clamps mouth blend shapes to 0.0, preventing "gaping mouth" glitches while paused.</li>
      </ul>

      <h3>D. Security, Grounded Context &amp; Anti-Prompt Injection</h3>
      <div class="callout">
        <div class="callout-title">Deterministic Boundary Revalidation (GameContextPromptFormatter):</div>
        The collector reads active scene handles and player room triggers (<code>Location34_Communication.roomPlayer</code>) on the Unity main thread. The pure formatter validates strings against control characters, zero-width spaces, and surrogates, strictly bounding output to <strong>832 UTF-16 code units</strong> (worst-case: 829 chars). Unobserved attributes (Mita position, quest flags) are explicitly labeled as <code>"unobserved"</code>, preventing hallucinated actions.
      </div>

      <h2>6. Quantitative Performance Benchmarks</h2>
      <table>
        <thead>
          <tr>
            <th>Subsystem / Metric</th>
            <th>Environment / Model</th>
            <th>Measured Latency</th>
            <th>Resource Footprint</th>
            <th>Status / Gate</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td><strong>Cloud Dialogue LLM</strong></td>
            <td>Groq Cloud (Llama-3.3-70B / GPT-OSS)</td>
            <td><strong>577 ms</strong></td>
            <td>Zero local GPU load</td>
            <td>Verified in-game</td>
          </tr>
          <tr>
            <td><strong>Cloud Multi-turn LLM</strong></td>
            <td>Google AI Studio (Gemini 2.5 Flash Lite)</td>
            <td><strong>1,100 ms</strong></td>
            <td>Zero local GPU load</td>
            <td>Verified in-game</td>
          </tr>
          <tr>
            <td><strong>Offline Local LLM</strong></td>
            <td>LM Studio (Llama-3.2 3B Instruct Q4_K_M)</td>
            <td><strong>2,500 ms</strong></td>
            <td>~3.0 GB VRAM</td>
            <td>Verified in-game</td>
          </tr>
          <tr>
            <td><strong>Neural TTS (32 steps)</strong></td>
            <td>F5-TTS Russian v2 (PyTorch CUDA FP16)</td>
            <td><strong>3.19 s</strong> (for 3.53s line)</td>
            <td>~1.96 GB VRAM peak</td>
            <td>Human approved</td>
          </tr>
          <tr>
            <td><strong>Neural TTS (16 steps)</strong></td>
            <td>F5-TTS Russian v2 (Speed-optimized)</td>
            <td><strong>1.61 s</strong> (48% faster)</td>
            <td>~1.96 GB VRAM peak</td>
            <td>Benchmark pass</td>
          </tr>
          <tr>
            <td><strong>Full Concurrent Load</strong></td>
            <td>MiSide Game + LM Studio + GPU TTS</td>
            <td><strong>7.93 s</strong> (concurrent turn)</td>
            <td><strong>4.47 GB</strong> aggregate peak</td>
            <td>No CUDA OOM</td>
          </tr>
        </tbody>
      </table>

      <h2>7. Verification &amp; Testing Methodology</h2>
      <p style="font-size: 8pt; margin-bottom: 4pt;">
        MitaAI implements rigorous test-driven safety across <strong>519+ automated checks</strong>: 381 managed unit checks (<code>MitaAI.InputChecks</code>), 98 context boundary checks (<code>MitaAI.ContextChecks</code>), and 40+ Python audio/worker tests verifying PCM signal headers, unclipped dynamic range, and IPC failure recovery.
      </p>

      <h2>8. Academic &amp; Technical Competencies Demonstrated</h2>
      <div class="kpi-grid" style="margin-top: 5pt; margin-bottom: 0;">
        <div class="kpi-card">
          <div class="kpi-val" style="font-size: 8.8pt;">Systems &amp; Reversing</div>
          <div class="kpi-sub">IL2CPP memory layout, Mono.Cecil analysis, BepInEx native hooking, Windows stdio IPC.</div>
        </div>
        <div class="kpi-card purple">
          <div class="kpi-val" style="font-size: 8.8pt;">Applied Machine Learning</div>
          <div class="kpi-sub">F5-TTS diffusion sampling, CUDA VRAM profiling, zero-effort reasoning tag suppression.</div>
        </div>
        <div class="kpi-card pink">
          <div class="kpi-val" style="font-size: 8.8pt;">HCI &amp; Graphics</div>
          <div class="kpi-sub">Spatial raycasting, screen Canvas shaders, responsive .NET 9 WPF desktop UI, HUD design.</div>
        </div>
        <div class="kpi-card emerald">
          <div class="kpi-val" style="font-size: 8.8pt;">Software Reliability</div>
          <div class="kpi-sub">Multi-agent Git worktrees, test-driven development (519+ checks), defensive FSM state guards.</div>
        </div>
      </div>
    </div>
    <div class="page-footer">
      <span>MitaAI Project Portfolio — Page 4 of 4</span>
      <span>Source Code: https://github.com/nizomiddindev/MitaAI</span>
      <span>November 2026</span>
    </div>
  </div>

</body>
</html>
"""

html_path = r"D:\MitaAI\docs\MitaAI_Project_Portfolio.html"
pdf_path = r"D:\MitaAI\MitaAI_University_Project_Portfolio.pdf"

with open(html_path, "w", encoding="utf-8") as f:
    f.write(html_content)

print(f"HTML written to {html_path} ({os.path.getsize(html_path)} bytes)")

chrome_exe = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
cmd = [
    chrome_exe,
    "--headless=new",
    "--disable-gpu",
    "--no-pdf-header-footer",
    f"--print-to-pdf={pdf_path}",
    f"file:///{html_path.replace(os.sep, '/')}"
]

print("Executing Chrome headless print-to-pdf...")
result = subprocess.run(cmd, capture_output=True, text=True)
print("Exit code:", result.returncode)

doc = pymupdf.open(pdf_path)
print(f"SUCCESS: PDF created at {pdf_path} ({os.path.getsize(pdf_path)} bytes) with {len(doc)} pages.")

out_dir = r"D:\MitaAI\docs\pdf_previews"
os.makedirs(out_dir, exist_ok=True)
for i, page in enumerate(doc):
    pix = page.get_pixmap(dpi=150)
    out_path = os.path.join(out_dir, f"page_{i+1}.png")
    pix.save(out_path)
    text_preview = page.get_text().strip().split('\\n')[:2]
    print(f"Page {i+1}: {pix.width}x{pix.height} | Top text: {text_preview}")
