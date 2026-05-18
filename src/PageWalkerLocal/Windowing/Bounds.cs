using PageWalkerLocal.Core;

namespace PageWalkerLocal.Windowing;

public readonly record struct ScreenPoint(int X, int Y);

public readonly record struct ScreenBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public ScreenPoint Center => new(X + Width / 2, Y + Height / 2);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(ScreenPoint point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    public bool Contains(ScreenBounds other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    public ScreenPoint Clamp(ScreenPoint point) =>
        new(Math.Clamp(point.X, X, Right - 1), Math.Clamp(point.Y, Y, Bottom - 1));

    public ScreenBounds Inflate(int dx, int dy) =>
        new(X - dx, Y - dy, Width + dx * 2, Height + dy * 2);

    public static ScreenBounds FromConfig(RectangleConfig rectangle) =>
        new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
