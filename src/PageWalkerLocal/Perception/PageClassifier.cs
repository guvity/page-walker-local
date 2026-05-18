using System.Security.Cryptography;
using System.Text;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class PageClassifier
{
    private readonly AppConfig _config;
    private readonly TechPageDetector _techPageDetector;
    private readonly PopupDetector _popupDetector;
    private readonly AgeGateDetector _ageGateDetector;
    private readonly FormDetector _formDetector;

    public PageClassifier(AppConfig config)
    {
        _config = config;
        _techPageDetector = new TechPageDetector();
        _popupDetector = new PopupDetector();
        _ageGateDetector = new AgeGateDetector();
        _formDetector = new FormDetector();
    }

    public PerceptionState Classify(CaptureFrame frame, TargetWindow window, OcrResult ocr, IReadOnlyList<CandidateElement> uiaCandidates)
    {
        var visibleText = BuildVisibleText(window.Title, ocr, uiaCandidates);
        var state = new PerceptionState
        {
            Bounds = frame.Bounds,
            WindowTitle = window.Title,
            VisibleText = visibleText,
            ScreenshotHash = HashBytes(BitmapBytes(frame.Bitmap)),
            TextHash = HashText(visibleText),
            Candidates = uiaCandidates.Concat(FromOcrLines(ocr)).ToList()
        };

        state.IsTechnicalPage = _techPageDetector.IsTechnical(visibleText, state.ClassifierSignals);
        state.HasCaptchaLikeText = ContainsCaptchaLikeText(visibleText);
        if (state.HasCaptchaLikeText)
        {
            state.ClassifierSignals.Add("captcha-like text detected; stopping rather than solving");
        }

        state.Candidates.AddRange(_popupDetector.Detect(_config, state));
        state.Candidates.AddRange(_ageGateDetector.Detect(_config, state));
        state.Candidates.AddRange(_formDetector.Detect(_config, state));
        return state;
    }

    private static IEnumerable<CandidateElement> FromOcrLines(OcrResult ocr)
    {
        var id = 0;
        foreach (var line in ocr.Lines)
        {
            var kind = LooksLikeLink(line.Text)
                ? CandidateKind.Link
                : LooksLikeButton(line.Text)
                    ? CandidateKind.Button
                    : CandidateKind.Text;
            yield return new CandidateElement($"ocr-{id++}", kind, line.Text, line.Bounds, line.Confidence, "ocr");
        }
    }

    private static bool LooksLikeLink(string text) =>
        text.Contains("http", StringComparison.OrdinalIgnoreCase)
        || text.Contains("www.", StringComparison.OrdinalIgnoreCase)
        || text.Contains("read more", StringComparison.OrdinalIgnoreCase)
        || text.Contains("learn more", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeButton(string text)
    {
        if (text.Length > 48)
        {
            return false;
        }

        var signals = new[]
        {
            "ok", "close", "continue", "enter", "skip", "not now", "no thanks", "accept",
            "yes", "next", "submit", "search", "login", "sign in", "start"
        };
        return signals.Any(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildVisibleText(string title, OcrResult ocr, IReadOnlyList<CandidateElement> uiaCandidates)
    {
        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine(ocr.Text);
        foreach (var candidate in uiaCandidates)
        {
            builder.AppendLine(candidate.Text);
        }

        return builder.ToString();
    }

    private static bool ContainsCaptchaLikeText(string text)
    {
        return text.Contains("captcha", StringComparison.OrdinalIgnoreCase)
            || text.Contains("hcaptcha", StringComparison.OrdinalIgnoreCase)
            || text.Contains("recaptcha", StringComparison.OrdinalIgnoreCase)
            || text.Contains("verify you are human", StringComparison.OrdinalIgnoreCase)
            || text.Contains("подтвердите, что вы человек", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BitmapBytes(System.Drawing.Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return stream.ToArray();
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();

    private static string HashText(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
