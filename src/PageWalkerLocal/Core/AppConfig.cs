using System.Text.Json;

namespace PageWalkerLocal.Core;

public sealed class AppConfig
{
    public string TargetMode { get; set; } = "ActiveWindow";
    public List<string> TargetProcessNames { get; set; } = ["chrome", "msedge", "SunBrowser", "AdsPower Global"];
    public RectangleConfig Rectangle { get; set; } = new();

    public bool DryRun { get; set; } = true;
    public string BehaviorProfile { get; set; } = "normal";
    public int? RandomSeed { get; set; }

    public int MaxDepth { get; set; } = 2;
    public int MaxSteps { get; set; } = 120;
    public int MaxScrollsPerPage { get; set; } = 10;
    public int MaxRuntimeSeconds { get; set; } = 300;

    public bool AllowExternalNavigation { get; set; }
    public List<string> AllowedDomains { get; set; } = [];
    public List<string> BlockedTexts { get; set; } = ["payment", "deposit", "buy now", "subscribe", "confirm payment"];

    public bool AllowAgeGate { get; set; } = true;
    public bool AllowSimpleConfirmations { get; set; } = true;
    public bool AllowAcceptButtons { get; set; }
    public List<string> AllowedGateTexts { get; set; } = ["I am 18", "I am over 18", "Yes, I am 18+", "Continue", "Enter"];

    public bool AllowForms { get; set; }
    public List<string> AllowedFormFields { get; set; } = ["email", "name"];
    public Dictionary<string, string> TestFormData { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "test@example.invalid",
        ["name"] = "Test User"
    };

    public bool CloseOwnTabsOnFinish { get; set; } = true;
    public string TechnicalPageAction { get; set; } = "retry";
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 3000;

    public OcrOptions Ocr { get; set; } = new();
    public LocalBrainOptions LocalBrain { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();

    public static AppConfig Load(string path, AppLogger logger)
    {
        if (!File.Exists(path))
        {
            logger.Warning($"Config file not found at '{path}'. Using safe defaults.");
            return new AppConfig();
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var config = JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
        config.Normalize(logger);
        return config;
    }

    private void Normalize(AppLogger logger)
    {
        TargetMode = Is(TargetMode, "Rectangle") ? "Rectangle" : "ActiveWindow";
        BehaviorProfile = BehaviorProfile.Trim().ToLowerInvariant() switch
        {
            "cautious" => "cautious",
            "fast" => "fast",
            "load-test" => "load-test",
            _ => "normal"
        };

        MaxDepth = Math.Max(0, MaxDepth);
        MaxSteps = Math.Clamp(MaxSteps, 1, 10_000);
        MaxScrollsPerPage = Math.Clamp(MaxScrollsPerPage, 0, 500);
        MaxRuntimeSeconds = Math.Clamp(MaxRuntimeSeconds, 1, 86_400);
        RetryCount = Math.Clamp(RetryCount, 0, 20);
        RetryDelayMs = Math.Clamp(RetryDelayMs, 0, 60_000);

        if (Rectangle.Width <= 0 || Rectangle.Height <= 0)
        {
            logger.Warning("Configured rectangle has invalid size. Falling back to 1280x720 at 0,0.");
            Rectangle = new RectangleConfig();
        }
    }

    private static bool Is(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed class RectangleConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
}

public sealed class OcrOptions
{
    public bool Enabled { get; set; } = true;
    public string Engine { get; set; } = "RapidOCR";
    public string ModelsPath { get; set; } = "models/ocr";
}

public sealed class LocalBrainOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "LLamaSharp";
    public string ModelPath { get; set; } = "models/llm/qwen2.5-0.5b-instruct-q4_k_m.gguf";
    public int MaxTokens { get; set; } = 160;
    public double Temperature { get; set; } = 0.2;
    public bool StrictJson { get; set; } = true;
    public double MinConfidence { get; set; } = 0.65;
}

public sealed class LoggingOptions
{
    public string Level { get; set; } = "Information";
    public bool SaveScreenshots { get; set; } = true;
    public bool SaveDecisionJson { get; set; } = true;
}
