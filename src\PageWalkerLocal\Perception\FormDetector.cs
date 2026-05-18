using PageWalkerLocal.Core;

namespace PageWalkerLocal.Perception;

public sealed class FormDetector
{
    public IReadOnlyList<CandidateElement> Detect(AppConfig config, PerceptionState state)
    {
        if (!config.AllowForms)
        {
            return Array.Empty<CandidateElement>();
        }

        return state.Candidates
            .Where(candidate => candidate.Kind == CandidateKind.Input
                && config.AllowedFormFields.Any(field => candidate.Text.Contains(field, StringComparison.OrdinalIgnoreCase)))
            .Select(candidate => candidate with { Kind = CandidateKind.FormField, Confidence = Math.Max(candidate.Confidence, 0.75) })
            .ToArray();
    }
}
