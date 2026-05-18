using PageWalkerLocal.Core;

namespace PageWalkerLocal.Perception;

public sealed class AgeGateDetector
{
    private static readonly string[] BuiltInSignals =
    [
        "i am 18", "i am over 18", "yes, i am 18", "18+", "over 18", "continue", "enter",
        "мне 18", "мне есть 18", "старше 18", "продолжить", "войти",
        "soy mayor de 18", "tengo 18", "continuar", "entrar",
        "j'ai plus de 18", "continuer", "entrer",
        "ich bin über 18", "weiter", "eintreten",
        "tenho mais de 18", "continuar", "entrar"
    ];

    public IReadOnlyList<CandidateElement> Detect(AppConfig config, PerceptionState state)
    {
        if (!config.AllowAgeGate && !config.AllowSimpleConfirmations)
        {
            return Array.Empty<CandidateElement>();
        }

        var allowed = config.AllowedGateTexts.Concat(BuiltInSignals).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return state.Candidates
            .Where(candidate => allowed.Any(signal => candidate.Text.Contains(signal, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => candidate with
            {
                Kind = CandidateKind.AgeGate,
                Confidence = Math.Max(candidate.Confidence, 0.74),
                Source = $"{candidate.Source}:age-gate"
            })
            .ToArray();
    }
}
