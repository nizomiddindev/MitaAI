namespace MitaAI.Plugin;

public enum AIProvider
{
    Ollama,
    LMStudio,
    OpenRouter,
    Groq,
    Gemini,
    Custom,
    Fixture
}

public static class AIProviderDefaults
{
    public const string OllamaEndpoint = "http://localhost:11434/v1/chat/completions";
    public const string OllamaDefaultModel = "llama3.2";

    public const string LMStudioEndpoint = "http://localhost:1234/v1/chat/completions";
    public const string LMStudioDefaultModel = "local-model";

    public const string OpenRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    public const string OpenRouterDefaultModel = "openrouter/free";

    public const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public const string GroqDefaultModel = "openai/gpt-oss-20b";

    public const string GeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
    public const string GeminiDefaultModel = "gemini-3.5-flash-lite";

    public const string DefaultSystemPrompt =
        "You are Mita, the cheerful, expressive, and lively anime girl from MiSide! " +
        "You speak with energy, warmth, and charm. Talk directly to the player like a close friend hanging out in the room. " +
        "Keep your replies punchy (1 to 2 sentences, maximum 150 characters) so they fit nicely as game subtitles. " +
        "Never explain your thought process or output reasoning steps. Speak purely in character. " +
        "Wholesome and age-appropriate.";

    public static string GetEffectiveEndpoint(AIProvider provider, string? customEndpoint)
    {
        if (!string.IsNullOrWhiteSpace(customEndpoint))
            return customEndpoint.Trim();

        return provider switch
        {
            AIProvider.Ollama => OllamaEndpoint,
            AIProvider.LMStudio => LMStudioEndpoint,
            AIProvider.OpenRouter => OpenRouterEndpoint,
            AIProvider.Groq => GroqEndpoint,
            AIProvider.Gemini => GeminiEndpoint,
            _ => ""
        };
    }

    public static string GetEffectiveModel(AIProvider provider, string? customModel)
    {
        if (!string.IsNullOrWhiteSpace(customModel))
            return customModel.Trim();

        return provider switch
        {
            AIProvider.Ollama => OllamaDefaultModel,
            AIProvider.LMStudio => LMStudioDefaultModel,
            AIProvider.OpenRouter => OpenRouterDefaultModel,
            AIProvider.Groq => GroqDefaultModel,
            AIProvider.Gemini => GeminiDefaultModel,
            _ => "default"
        };
    }
}
