using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MitaAI.Launcher;

public sealed class MainForm : Form
{
    private string gameDirectory = "";
    private string configPath = "";

    // Controls
    private Label lblTitle = null!;
    private Label lblSubtitle = null!;
    private Label lblGameStatus = null!;
    private TextBox txtGamePath = null!;
    private Button btnBrowseGame = null!;

    private ComboBox cmbProvider = null!;
    private TextBox txtEndpoint = null!;
    private ComboBox cmbModel = null!;
    private TextBox txtApiKey = null!;
    private Button btnToggleKey = null!;
    private LinkLabel lnkGetKey = null!;
    private NumericUpDown numMaxTokens = null!;

    private ComboBox cmbPersonality = null!;
    private TextBox txtSystemPrompt = null!;

    private Button btnTest = null!;
    private Label lblTestStatus = null!;
    private TextBox txtTestOutput = null!;

    private Button btnSave = null!;
    private Button btnLaunch = null!;

    private static readonly Color BgDark = Color.FromArgb(24, 18, 28);
    private static readonly Color CardBg = Color.FromArgb(36, 26, 42);
    private static readonly Color AccentPink = Color.FromArgb(255, 94, 151);
    private static readonly Color BorderPurple = Color.FromArgb(74, 46, 86);
    private static readonly Color TextSoft = Color.FromArgb(242, 230, 244);
    private static readonly Color TextMuted = Color.FromArgb(180, 160, 190);
    private static readonly Color InputBg = Color.FromArgb(28, 20, 34);

    public MainForm()
    {
        InitializeUi();
        DetectGamePath();
        LoadConfig();
    }

    private void InitializeUi()
    {
        Text = "MitaAI Launcher & Configurator (v0.5.0)";
        ClientSize = new Size(720, 830);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScroll = true;
        BackColor = BgDark;
        ForeColor = TextSoft;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        int top = 16;
        int cardLeft = 24;
        int cardWidth = 672;

        // Title Header
        lblTitle = new Label
        {
            Text = "MitaAI Launcher",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = AccentPink,
            Location = new Point(cardLeft, top),
            AutoSize = true
        };
        Controls.Add(lblTitle);

        lblSubtitle = new Label
        {
            Text = "Intelligent Companion Mod for MiSide • Configure AI & Launch",
            ForeColor = TextMuted,
            Location = new Point(cardLeft + 2, top + 34),
            AutoSize = true,
            UseMnemonic = false
        };
        Controls.Add(lblSubtitle);

        top += 66;

        // --- Card 1: Game & Mod Directory ---
        var cardGame = CreateCard(new Point(cardLeft, top), new Size(cardWidth, 82), "Game Directory & Status");
        Controls.Add(cardGame);

        txtGamePath = new TextBox
        {
            Location = new Point(16, 26),
            Size = new Size(524, 26),
            BackColor = InputBg,
            ForeColor = TextSoft,
            BorderStyle = BorderStyle.FixedSingle
        };
        cardGame.Controls.Add(txtGamePath);

        btnBrowseGame = CreateStyledButton("Browse...", new Point(550, 25), new Size(106, 28));
        btnBrowseGame.Click += (s, e) => BrowseGame();
        cardGame.Controls.Add(btnBrowseGame);

        lblGameStatus = new Label
        {
            Text = "Detecting game directory...",
            Location = new Point(16, 56),
            ForeColor = TextMuted,
            AutoSize = true,
            UseMnemonic = false
        };
        cardGame.Controls.Add(lblGameStatus);

        top += 92;

        // --- Card 2: AI Provider Configuration ---
        var cardAi = CreateCard(new Point(cardLeft, top), new Size(cardWidth, 275), "AI Provider & Connection");
        Controls.Add(cardAi);

        // Provider row
        var lblProv = new Label { Text = "Provider:", Location = new Point(16, 28), AutoSize = true, ForeColor = TextSoft };
        cardAi.Controls.Add(lblProv);

        cmbProvider = new ComboBox
        {
            Location = new Point(110, 25),
            Size = new Size(546, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = InputBg,
            ForeColor = TextSoft
        };
        cmbProvider.Items.AddRange(new object[] {
            "Groq Cloud (Free, Ultra-Fast ~0.4s, 14,400 req/day - Recommended)",
            "Google AI Studio / Gemini (Free Tier ~15 RPM, Generous & Smart)",
            "OpenRouter (Free Cloud Pool - Shared Quota)",
            "LM Studio (Local AI Server - Free, Offline & Unlimited)",
            "Ollama (Local AI - Free, Offline & Unlimited)",
            "Custom (OpenAI-Compatible API)",
            "Fixture (Offline Fixed Test Line)"
        });
        cmbProvider.SelectedIndexChanged += (s, e) => OnProviderChanged();
        cardAi.Controls.Add(cmbProvider);

        // Model row
        var lblMod = new Label { Text = "Model:", Location = new Point(16, 66), AutoSize = true, ForeColor = TextSoft };
        cardAi.Controls.Add(lblMod);

        cmbModel = new ComboBox
        {
            Location = new Point(110, 63),
            Size = new Size(546, 28),
            BackColor = InputBg,
            ForeColor = TextSoft
        };
        cardAi.Controls.Add(cmbModel);

        // Endpoint row
        var lblEp = new Label { Text = "Endpoint:", Location = new Point(16, 104), AutoSize = true, ForeColor = TextSoft };
        cardAi.Controls.Add(lblEp);

        txtEndpoint = new TextBox
        {
            Location = new Point(110, 101),
            Size = new Size(546, 26),
            BackColor = InputBg,
            ForeColor = TextSoft,
            BorderStyle = BorderStyle.FixedSingle
        };
        cardAi.Controls.Add(txtEndpoint);

        // API Key row
        var lblKey = new Label { Text = "API Key:", Location = new Point(16, 142), AutoSize = true, ForeColor = TextSoft };
        cardAi.Controls.Add(lblKey);

        txtApiKey = new TextBox
        {
            Location = new Point(110, 139),
            Size = new Size(436, 26),
            BackColor = InputBg,
            ForeColor = TextSoft,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true
        };
        cardAi.Controls.Add(txtApiKey);

        btnToggleKey = CreateStyledButton("Show", new Point(556, 138), new Size(100, 28));
        btnToggleKey.Click += (s, e) =>
        {
            txtApiKey.UseSystemPasswordChar = !txtApiKey.UseSystemPasswordChar;
            btnToggleKey.Text = txtApiKey.UseSystemPasswordChar ? "Show" : "Hide";
        };
        cardAi.Controls.Add(btnToggleKey);

        // MaxTokens row
        var lblTokens = new Label { Text = "Max Tokens:", Location = new Point(16, 178), AutoSize = true, ForeColor = TextSoft };
        cardAi.Controls.Add(lblTokens);

        numMaxTokens = new NumericUpDown
        {
            Location = new Point(110, 175),
            Size = new Size(120, 26),
            Minimum = 20,
            Maximum = 500,
            Value = 120,
            BackColor = InputBg,
            ForeColor = TextSoft
        };
        cardAi.Controls.Add(numMaxTokens);

        var lblTokenHint = new Label
        {
            Text = "Subtitle length limit (120 tokens ≈ 1-2 dialogue sentences)",
            Location = new Point(240, 178),
            AutoSize = true,
            ForeColor = TextMuted
        };
        cardAi.Controls.Add(lblTokenHint);

        lnkGetKey = new LinkLabel
        {
            Text = "Get a free API key here",
            LinkColor = AccentPink,
            ActiveLinkColor = Color.White,
            Location = new Point(110, 212),
            AutoSize = true
        };
        lnkGetKey.LinkClicked += (s, e) =>
        {
            string url = lnkGetKey.Tag as string ?? "https://console.groq.com/keys";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };
        cardAi.Controls.Add(lnkGetKey);

        top += 285;

        // --- Card 3: Personality & Prompt ---
        var cardPrompt = CreateCard(new Point(cardLeft, top), new Size(cardWidth, 148), "Mita Persona & Dialogue Constraints");
        Controls.Add(cardPrompt);

        var lblPreset = new Label { Text = "Preset:", Location = new Point(16, 28), AutoSize = true, ForeColor = TextSoft };
        cardPrompt.Controls.Add(lblPreset);

        cmbPersonality = new ComboBox
        {
            Location = new Point(110, 25),
            Size = new Size(546, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = InputBg,
            ForeColor = TextSoft
        };
        cmbPersonality.Items.AddRange(new object[] {
            "Classic Mita (Friendly, Curious & Wholesome)",
            "Playful & Teasing Mita",
            "Sweet & Caring Companion Mita",
            "Custom Prompt"
        });
        cmbPersonality.SelectedIndexChanged += (s, e) => OnPersonalityPresetChanged();
        cardPrompt.Controls.Add(cmbPersonality);

        txtSystemPrompt = new TextBox
        {
            Location = new Point(16, 62),
            Size = new Size(640, 72),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = InputBg,
            ForeColor = TextSoft,
            BorderStyle = BorderStyle.FixedSingle
        };
        cardPrompt.Controls.Add(txtSystemPrompt);

        top += 158;

        // --- Card 4: Interactive Test Section ---
        var cardTest = CreateCard(new Point(cardLeft, top), new Size(cardWidth, 110), "Test Connection");
        Controls.Add(cardTest);

        btnTest = CreateStyledButton("⚡ Test AI Connection", new Point(16, 24), new Size(180, 32));
        btnTest.Click += async (s, e) => await TestConnectionAsync();
        cardTest.Controls.Add(btnTest);

        lblTestStatus = new Label
        {
            Text = "Ready to test.",
            Location = new Point(208, 30),
            ForeColor = TextMuted,
            AutoSize = true
        };
        cardTest.Controls.Add(lblTestStatus);

        txtTestOutput = new TextBox
        {
            Location = new Point(16, 64),
            Size = new Size(640, 32),
            ReadOnly = true,
            BackColor = InputBg,
            ForeColor = AccentPink,
            BorderStyle = BorderStyle.FixedSingle,
            Text = "Mita's reply preview will appear here..."
        };
        cardTest.Controls.Add(txtTestOutput);

        top += 122;

        // --- Footer Buttons (Centered with generous margin) ---
        btnSave = CreateStyledButton("💾 Save Configuration", new Point(140, top), new Size(200, 42));
        btnSave.Click += (s, e) => SaveConfig(true);
        Controls.Add(btnSave);

        btnLaunch = new Button
        {
            Text = "🎮 Play MiSide",
            Location = new Point(360, top),
            Size = new Size(220, 42),
            BackColor = AccentPink,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnLaunch.FlatAppearance.BorderSize = 0;
        btnLaunch.Click += (s, e) => LaunchGame();
        Controls.Add(btnLaunch);
    }

    private static GroupBox CreateCard(Point location, Size size, string text)
    {
        return new GroupBox
        {
            Text = "  " + text.Replace("&", "&&") + "  ",
            Location = location,
            Size = size,
            BackColor = CardBg,
            ForeColor = AccentPink,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
    }

    private static Button CreateStyledButton(string text, Point location, Size size)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = BorderPurple,
            ForeColor = TextSoft,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = AccentPink;
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }

    private void DetectGamePath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = {
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\MiSide_Dev")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\MiSide_Dev")),
            Path.GetFullPath(Path.Combine(baseDir, "MiSide_Dev")),
            @"D:\MitaAI\MiSide_Dev"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "MiSideFull.exe")))
            {
                gameDirectory = candidate;
                break;
            }
        }

        if (string.IsNullOrEmpty(gameDirectory))
        {
            gameDirectory = baseDir;
        }

        txtGamePath.Text = gameDirectory;
        UpdateGameStatus();
    }

    private void UpdateGameStatus()
    {
        bool exeExists = File.Exists(Path.Combine(gameDirectory, "MiSideFull.exe"));
        string pluginDll = Path.Combine(gameDirectory, @"BepInEx\plugins\MitaAI\MitaAI.Plugin.dll");
        bool pluginExists = File.Exists(pluginDll);
        configPath = Path.Combine(gameDirectory, @"BepInEx\config\local.mitaai.probe.cfg");

        if (exeExists && pluginExists)
        {
            lblGameStatus.Text = "Status: MiSide found & MitaAI Plugin installed (v0.5.0)";
            lblGameStatus.ForeColor = Color.LightGreen;
        }
        else if (exeExists)
        {
            lblGameStatus.Text = "Status: MiSide found, but plugin DLL is not in BepInEx\\plugins\\MitaAI";
            lblGameStatus.ForeColor = Color.Yellow;
        }
        else
        {
            lblGameStatus.Text = "Status: MiSideFull.exe not found in this folder.";
            lblGameStatus.ForeColor = Color.Salmon;
        }
    }

    private void BrowseGame()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select MiSide game folder (containing MiSideFull.exe)",
            SelectedPath = gameDirectory
        };
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            gameDirectory = fbd.SelectedPath;
            txtGamePath.Text = gameDirectory;
            UpdateGameStatus();
            LoadConfig();
        }
    }

    private void OnProviderChanged()
    {
        cmbModel.Items.Clear();
        int idx = cmbProvider.SelectedIndex;

        if (idx == 0) // Groq Cloud
        {
            cmbModel.Items.AddRange(new object[] {
                "openai/gpt-oss-20b",
                "openai/gpt-oss-120b"
            });
            cmbModel.SelectedIndex = 0;
            txtEndpoint.Text = "https://api.groq.com/openai/v1/chat/completions";
            txtApiKey.Enabled = true;
            lnkGetKey.Visible = true;
            lnkGetKey.Text = "Get a free Groq API key here (console.groq.com)";
            lnkGetKey.Tag = "https://console.groq.com/keys";
        }
        else if (idx == 1) // Google AI Studio (Gemini)
        {
            cmbModel.Items.AddRange(new object[] {
                "gemini-3.5-flash-lite",
                "gemini-3.8-flash"
            });
            cmbModel.SelectedIndex = 0;
            txtEndpoint.Text = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
            txtApiKey.Enabled = true;
            lnkGetKey.Visible = true;
            lnkGetKey.Text = "Get a free Google AI Studio key here (aistudio.google.com)";
            lnkGetKey.Tag = "https://aistudio.google.com/app/apikey";
        }
        else if (idx == 2) // OpenRouter
        {
            cmbModel.Items.AddRange(new object[] {
                "openrouter/free",
                "nvidia/nemotron-3.5-lightning:free",
                "google/gemma-4-31b-it:free",
                "nvidia/nemotron-3-super-120b-a12b:free",
                "liquid/lfm-2.5-2.6b:free"
            });
            cmbModel.SelectedIndex = 0;
            txtEndpoint.Text = "https://openrouter.ai/api/v1/chat/completions";
            txtApiKey.Enabled = true;
            lnkGetKey.Visible = true;
            lnkGetKey.Text = "Get a free OpenRouter API key here (openrouter.ai)";
            lnkGetKey.Tag = "https://openrouter.ai/keys";
        }
        else if (idx == 3) // LM Studio
        {
            cmbModel.Items.AddRange(new object[] { "local-model" });
            cmbModel.SelectedIndex = 0;
            txtEndpoint.Text = "http://localhost:1234/v1/chat/completions";
            txtApiKey.Text = "";
            txtApiKey.Enabled = false;
            lnkGetKey.Visible = false;
        }
        else if (idx == 4) // Ollama
        {
            cmbModel.Items.AddRange(new object[] { "llama3.2", "llama3", "mistral", "qwen2.5" });
            cmbModel.SelectedIndex = 0;
            txtEndpoint.Text = "http://localhost:11434/v1/chat/completions";
            txtApiKey.Text = "";
            txtApiKey.Enabled = false;
            lnkGetKey.Visible = false;
        }
        else if (idx == 5) // Custom
        {
            txtApiKey.Enabled = true;
            lnkGetKey.Visible = false;
        }
        else // Fixture
        {
            txtEndpoint.Text = "";
            cmbModel.Text = "fixture";
            txtApiKey.Text = "";
            txtApiKey.Enabled = false;
            lnkGetKey.Visible = false;
        }
    }

    private void OnPersonalityPresetChanged()
    {
        int idx = cmbPersonality.SelectedIndex;
        if (idx == 0) // Classic Mita
        {
            txtSystemPrompt.Text = "You are Mita, the cheerful, expressive, and lively anime girl from MiSide! You speak with energy, warmth, and charm. Talk directly to the player like a close friend hanging out in the room. Keep your replies punchy (1 to 2 sentences, maximum 150 characters) so they fit nicely as game subtitles. Never explain your thought process or output reasoning steps. Speak purely in character. Wholesome and age-appropriate.";
        }
        else if (idx == 1) // Playful Mita
        {
            txtSystemPrompt.Text = "You are Mita from MiSide! You are energetic, teasing, and playful with the player. You love bantering and making things fun. Keep replies under 2 short sentences (max 150 characters) for game subtitles. Speak directly as Mita with zero reasoning or thought notes. Wholesome, cute, and age-appropriate.";
        }
        else if (idx == 2) // Sweet Companion Mita
        {
            txtSystemPrompt.Text = "You are Mita from MiSide. You are sweet, caring, and attentive to the player. You love spending quiet moments together and always want to know what they're up to. Keep replies under 2 short sentences (max 150 characters) for subtitles. Speak only in dialogue. Wholesome and age-appropriate.";
        }
    }

    private void LoadConfig()
    {
        if (!File.Exists(configPath))
        {
            cmbProvider.SelectedIndex = 0;
            cmbPersonality.SelectedIndex = 0;
            return;
        }

        string providerStr = "Groq";
        string modelStr = "";
        string endpointStr = "";
        string keyStr = "";
        string promptStr = "";
        int maxTokensVal = 120;

        foreach (string line in File.ReadAllLines(configPath))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Provider =")) providerStr = trimmed.Substring("Provider =".Length).Trim();
            else if (trimmed.StartsWith("Model =")) modelStr = trimmed.Substring("Model =".Length).Trim();
            else if (trimmed.StartsWith("EndpointUrl =")) endpointStr = trimmed.Substring("EndpointUrl =".Length).Trim();
            else if (trimmed.StartsWith("ApiKey =")) keyStr = trimmed.Substring("ApiKey =".Length).Trim();
            else if (trimmed.StartsWith("SystemPrompt =")) promptStr = trimmed.Substring("SystemPrompt =".Length).Trim();
            else if (trimmed.StartsWith("MaxTokens =") && int.TryParse(trimmed.Substring("MaxTokens =".Length).Trim(), out int tok)) maxTokensVal = tok;
        }

        int providerIdx = providerStr switch
        {
            "Groq" => 0,
            "Gemini" => 1,
            "OpenRouter" => 2,
            "LMStudio" => 3,
            "Ollama" => 4,
            "Custom" => 5,
            "Fixture" => 6,
            _ => 0
        };

        cmbProvider.SelectedIndex = providerIdx;
        if (!string.IsNullOrEmpty(modelStr)) cmbModel.Text = modelStr;
        if (!string.IsNullOrEmpty(endpointStr)) txtEndpoint.Text = endpointStr;
        if (!string.IsNullOrEmpty(keyStr)) txtApiKey.Text = keyStr;
        numMaxTokens.Value = Math.Clamp(maxTokensVal, (int)numMaxTokens.Minimum, (int)numMaxTokens.Maximum);

        if (!string.IsNullOrEmpty(promptStr))
        {
            txtSystemPrompt.Text = promptStr;
            cmbPersonality.SelectedIndex = 3;
        }
        else
        {
            cmbPersonality.SelectedIndex = 0;
        }
    }

    private bool SaveConfig(bool notify = false)
    {
        string providerName = cmbProvider.SelectedIndex switch
        {
            0 => "Groq",
            1 => "Gemini",
            2 => "OpenRouter",
            3 => "LMStudio",
            4 => "Ollama",
            5 => "Custom",
            6 => "Fixture",
            _ => "Groq"
        };

        var managedAiSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Provider"] = providerName,
            ["EndpointUrl"] = txtEndpoint.Text.Trim(),
            ["Model"] = cmbModel.Text.Trim(),
            ["ApiKey"] = txtApiKey.Text.Trim(),
            ["SystemPrompt"] = txtSystemPrompt.Text.Replace("\r", " ").Replace("\n", " ").Trim(),
            ["MaxTokens"] = ((int)numMaxTokens.Value).ToString()
        };

        try
        {
            string dir = Path.GetDirectoryName(configPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath).ToList();
                var remainingKeys = new HashSet<string>(managedAiSettings.Keys, StringComparer.OrdinalIgnoreCase);
                string currentSection = "";
                int lastAiSectionIndex = -1;

                for (int i = 0; i < lines.Count; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        if (currentSection.Equals("AI", StringComparison.OrdinalIgnoreCase))
                        {
                            lastAiSectionIndex = i;
                        }
                        continue;
                    }

                    if (currentSection.Equals("AI", StringComparison.OrdinalIgnoreCase))
                    {
                        lastAiSectionIndex = i;
                        int eqIdx = trimmed.IndexOf('=');
                        if (eqIdx > 0 && !trimmed.StartsWith("#"))
                        {
                            string key = trimmed.Substring(0, eqIdx).Trim();
                            if (managedAiSettings.TryGetValue(key, out string? newVal))
                            {
                                lines[i] = $"{key} = {newVal}";
                                remainingKeys.Remove(key);
                            }
                        }
                    }
                }

                if (remainingKeys.Count > 0)
                {
                    if (lastAiSectionIndex >= 0)
                    {
                        int insertIdx = lastAiSectionIndex + 1;
                        foreach (var key in remainingKeys)
                        {
                            lines.Insert(insertIdx++, $"{key} = {managedAiSettings[key]}");
                        }
                    }
                    else
                    {
                        lines.Add("");
                        lines.Add("[AI]");
                        foreach (var key in remainingKeys)
                        {
                            lines.Add($"{key} = {managedAiSettings[key]}");
                        }
                    }
                }

                File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
            }
            else
            {
                string content = $@"## Settings file for MitaAI Plugin v0.5.0
## Plugin GUID: local.mitaai.probe

[AI]

## AI provider to use: Groq, Gemini, OpenRouter, LMStudio, Ollama, Custom, Fixture.
# Setting type: AIProvider
# Default value: Groq
# Acceptable values: Groq, Gemini, OpenRouter, LMStudio, Ollama, Custom, Fixture
Provider = {providerName}

## Custom API endpoint URL. Leave empty to use provider default.
# Setting type: String
# Default value: 
EndpointUrl = {txtEndpoint.Text.Trim()}

## Model identifier. Leave empty to use provider default.
# Setting type: String
# Default value: 
Model = {cmbModel.Text.Trim()}

## API key (required for cloud providers or authenticated Custom endpoints).
# Setting type: String
# Default value: 
ApiKey = {txtApiKey.Text.Trim()}

## System prompt defining Mita's character and response constraints.
# Setting type: String
# Default value: You are Mita, a friendly, curious, and cheerful companion from the game MiSide.
SystemPrompt = {txtSystemPrompt.Text.Replace("\r", " ").Replace("\n", " ").Trim()}

## Maximum tokens to generate for Mita's reply (10-500).
# Setting type: Int32
# Default value: 120
MaxTokens = {(int)numMaxTokens.Value}

## Temperature for sampling (0.0 to 2.0).
# Setting type: Single
# Default value: 0.7
Temperature = 0.7

## Timeout in seconds for AI requests (3-120).
# Setting type: Single
# Default value: 25
TimeoutSeconds = 25

[Interaction]

## Open the message field when the nearby Mita prompt is available.
# Setting type: KeyCode
# Default value: E
Key = E

## Maximum camera-to-model-center distance in Unity units (0.8-3).
# Setting type: Single
# Default value: 2
Range = 2

[Test]

## Fixed diagnostic reply; used when AI Provider is set to Fixture.
# Setting type: String
# Default value: Hello! I'm right here.
Line = Hello! I'm right here.

## Caption duration in seconds (2-30).
# Setting type: Single
# Default value: 8
DurationSeconds = 8
";
                File.WriteAllText(configPath, content, new UTF8Encoding(false));
            }

            if (notify) MessageBox.Show("Configuration saved successfully to:\n" + configPath, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to save config: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task TestConnectionAsync()
    {
        btnTest.Enabled = false;
        lblTestStatus.Text = "Sending request to AI...";
        lblTestStatus.ForeColor = Color.Yellow;
        txtTestOutput.Text = "Waiting for response...";

        string endpoint = txtEndpoint.Text.Trim();
        string model = cmbModel.Text.Trim();
        string apiKey = txtApiKey.Text.Trim();
        string prompt = "Hello Mita, how are you today?";

        if (cmbProvider.SelectedIndex == 6) // Fixture
        {
            lblTestStatus.Text = "Fixture mode (offline)";
            lblTestStatus.ForeColor = Color.LightGreen;
            txtTestOutput.Text = "Mita: Hello! I'm right here.";
            btnTest.Enabled = true;
            return;
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            lblTestStatus.Text = "Error: Endpoint URL is empty.";
            lblTestStatus.ForeColor = Color.Salmon;
            btnTest.Enabled = true;
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            if (cmbProvider.SelectedIndex == 2) // OpenRouter
            {
                request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/MitaAI");
                request.Headers.TryAddWithoutValidation("X-Title", "MitaAI");
            }

            var body = new JsonObject
            {
                ["model"] = model,
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = txtSystemPrompt.Text.Trim() },
                    new JsonObject { ["role"] = "user", ["content"] = prompt }
                },
                ["max_tokens"] = (int)numMaxTokens.Value,
                ["temperature"] = 0.7
            };

            // Only add reasoning effort none on OpenRouter models that support it
            if (cmbProvider.SelectedIndex == 2 && model.Contains("nemotron", StringComparison.OrdinalIgnoreCase))
            {
                body["reasoning"] = new JsonObject { ["effort"] = "none" };
            }

            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            string responseJson = await response.Content.ReadAsStringAsync();
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                lblTestStatus.Text = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} ({sw.ElapsedMilliseconds}ms)";
                lblTestStatus.ForeColor = Color.Salmon;
                txtTestOutput.Text = "Error: " + responseJson;
            }
            else
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    string reply = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    // Strip any stray thinking preamble if present
                    if (reply.StartsWith("Here's a thinking process", StringComparison.OrdinalIgnoreCase))
                    {
                        int dnl = reply.IndexOf("\n\n");
                        if (dnl >= 0) reply = reply.Substring(dnl + 2).Trim();
                    }
                    lblTestStatus.Text = $"Connected successfully in {sw.ElapsedMilliseconds}ms!";
                    lblTestStatus.ForeColor = Color.LightGreen;
                    txtTestOutput.Text = "Mita: " + reply.Trim();
                }
                else
                {
                    lblTestStatus.Text = "Received empty response choices.";
                    lblTestStatus.ForeColor = Color.Yellow;
                    txtTestOutput.Text = responseJson;
                }
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            lblTestStatus.Text = $"Connection Failed ({sw.ElapsedMilliseconds}ms)";
            lblTestStatus.ForeColor = Color.Salmon;
            txtTestOutput.Text = "Error: " + ex.Message;
        }
        finally
        {
            btnTest.Enabled = true;
        }
    }

    private void LaunchGame()
    {
        if (!SaveConfig(false)) return;
        string exe = Path.Combine(gameDirectory, "MiSideFull.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show("Cannot find MiSideFull.exe at:\n" + exe, "Game Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = gameDirectory,
                UseShellExecute = true
            });
            WindowState = FormWindowState.Minimized;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to launch game: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
