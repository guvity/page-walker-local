namespace PageWalkerLocal.HumanInput;

public sealed class HumanScrollGenerator
{
    private readonly Random _random;

    public HumanScrollGenerator(Random random)
    {
        _random = random;
    }

    public IReadOnlyList<ScrollStep> Generate(InteractionProfile profile, int visibleTextLength)
    {
        var steps = profile.Name == "load-test" ? _random.Next(1, 3) : _random.Next(2, 6);
        var result = new List<ScrollStep>(steps + 1);
        for (var i = 0; i < steps; i++)
        {
            var magnitude = profile.Name switch
            {
                "cautious" => _random.Next(180, 460),
                "fast" => _random.Next(420, 980),
                "load-test" => _random.Next(720, 1320),
                _ => _random.Next(280, 760)
            };
            result.Add(new ScrollStep(-RoundWheelDelta(magnitude), _random.Next(profile.MinScrollPauseMs, profile.MaxScrollPauseMs + 1)));
        }

        if (profile.Name != "load-test" && visibleTextLength > 600 && _random.NextDouble() < 0.22)
        {
            result.Add(new ScrollStep(RoundWheelDelta(_random.Next(90, 220)), _random.Next(profile.MinScrollPauseMs, profile.MaxScrollPauseMs + 1)));
        }

        return result;
    }

    private static int RoundWheelDelta(int value)
    {
        var units = Math.Max(1, (int)Math.Round(value / 120.0));
        return units * 120;
    }
}

public sealed record ScrollStep(int WheelDelta, int PauseMs);
