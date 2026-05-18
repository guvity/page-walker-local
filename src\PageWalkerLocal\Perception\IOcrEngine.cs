using System.Drawing;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public interface IOcrEngine
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<OcrResult> ReadAsync(Bitmap bitmap, ScreenBounds screenBounds, CancellationToken cancellationToken);
}

public sealed record OcrResult(string Text, IReadOnlyList<OcrTextLine> Lines)
{
    public static OcrResult Empty { get; } = new(string.Empty, Array.Empty<OcrTextLine>());
}

public sealed record OcrTextLine(string Text, ScreenBounds Bounds, double Confidence);
