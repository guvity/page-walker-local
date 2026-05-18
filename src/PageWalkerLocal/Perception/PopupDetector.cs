using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class PopupDetector
{
    private static readonly string[] CloseTexts =
    [
        "x", "×", "close", "no thanks", "not now", "later", "skip", "ok", "continue",
        "закрыть", "нет спасибо", "не сейчас", "позже", "пропустить", "ок",
        "cerrar", "ahora no", "omitir",
        "fermer", "pas maintenant",
        "schließen", "nicht jetzt"
    ];

    public IReadOnlyList<CandidateElement> Detect(AppConfig config, PerceptionState state)
    {
        var results = new List<CandidateElement>();
        foreach (var candidate in state.Candidates)
        {
            var text = candidate.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var isAccept = text.Contains("accept", StringComparison.OrdinalIgnoreCase)
                || text.Contains("принять", StringComparison.OrdinalIgnoreCase);
            if (isAccept && !config.AllowAcceptButtons)
            {
                continue;
            }

            if (CloseTexts.Any(close => text.Equals(close, StringComparison.OrdinalIgnoreCase)
                    || text.Contains(close, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(candidate with
                {
                    Kind = CandidateKind.PopupClose,
                    Confidence = Math.Max(candidate.Confidence, 0.72),
                    Source = $"{candidate.Source}:popup"
                });
            }
        }

        var genericTopRight = new ScreenBounds(state.Bounds.Right - 72, state.Bounds.Y + 24, 48, 48);
        results.Add(new CandidateElement("popup-top-right-x-heuristic", CandidateKind.PopupClose, "X", genericTopRight, 0.45, "heuristic"));
        return results;
    }
}
