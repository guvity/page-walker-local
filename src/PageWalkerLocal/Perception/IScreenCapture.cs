using System.Drawing;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public interface IScreenCapture
{
    CaptureFrame Capture(ScreenBounds bounds);
}

public sealed class CaptureFrame : IDisposable
{
    public CaptureFrame(Bitmap bitmap, ScreenBounds bounds)
    {
        Bitmap = bitmap;
        Bounds = bounds;
        CapturedAt = DateTimeOffset.Now;
    }

    public Bitmap Bitmap { get; }
    public ScreenBounds Bounds { get; }
    public DateTimeOffset CapturedAt { get; }

    public void Dispose() => Bitmap.Dispose();
}
