using System.Drawing;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class ScreenCaptureGdi : IScreenCapture
{
    public CaptureFrame Capture(ScreenBounds bounds)
    {
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Capture bounds cannot be empty.", nameof(bounds));
        }

        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        return new CaptureFrame(bitmap, bounds);
    }
}
