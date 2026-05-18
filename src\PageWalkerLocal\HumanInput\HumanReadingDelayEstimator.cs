namespace PageWalkerLocal.HumanInput;

public sealed class HumanReadingDelayEstimator
{
    private readonly Random _random;

    public HumanReadingDelayEstimator(Random random)
    {
        _random = random;
    }

    public int EstimateDelayMs(string visibleText, InteractionProfile profile)
    {
        var wordCount = visibleText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var baseMs = wordCount * (profile.Name switch
        {
            "cautious" => 260,
            "fast" => 95,
            "load-test" => 28,
            _ => 155
        });
        var randomFactor = 0.82 + _random.NextDouble() * 0.44;
        return Math.Clamp((int)(baseMs * randomFactor), profile.MinReadDelayMs, profile.MaxReadDelayMs);
    }
}
