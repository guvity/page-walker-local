namespace PageWalkerLocal.HumanInput;

public sealed record InteractionProfile(
    string Name,
    int MinMouseDurationMs,
    int MaxMouseDurationMs,
    int MinPreClickDelayMs,
    int MaxPreClickDelayMs,
    int MinScrollPauseMs,
    int MaxScrollPauseMs,
    int MinReadDelayMs,
    int MaxReadDelayMs,
    double JitterPixels,
    double OvershootChance,
    int DryRunDelayCapMs)
{
    public static InteractionProfile FromName(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "cautious" => new("cautious", 520, 1250, 260, 850, 550, 1600, 1800, 9000, 2.2, 0.18, 150),
            "fast" => new("fast", 180, 520, 80, 240, 120, 520, 450, 2600, 0.9, 0.05, 60),
            "load-test" => new("load-test", 120, 340, 30, 120, 50, 260, 120, 900, 0.4, 0.0, 25),
            _ => new("normal", 320, 850, 160, 520, 260, 900, 900, 5200, 1.5, 0.10, 100)
        };
    }
}
