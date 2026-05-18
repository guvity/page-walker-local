using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.HumanInput;

public sealed class HumanMousePathGenerator
{
    private readonly Random _random;

    public HumanMousePathGenerator(Random random)
    {
        _random = random;
    }

    public IReadOnlyList<TimedMousePoint> Generate(ScreenPoint start, ScreenPoint target, InteractionProfile profile)
    {
        var distance = Math.Sqrt(Math.Pow(target.X - start.X, 2) + Math.Pow(target.Y - start.Y, 2));
        var duration = _random.Next(profile.MinMouseDurationMs, profile.MaxMouseDurationMs + 1);
        var steps = Math.Clamp((int)(distance / 12), 16, 96);
        var perpendicular = PerpendicularUnit(start, target);
        var curvature = Math.Clamp(distance * 0.18, 25, 180);

        var c1 = new FloatingPoint(
            start.X + (target.X - start.X) * 0.28 + perpendicular.X * RandomRange(-curvature, curvature),
            start.Y + (target.Y - start.Y) * 0.28 + perpendicular.Y * RandomRange(-curvature, curvature));
        var c2 = new FloatingPoint(
            start.X + (target.X - start.X) * 0.72 + perpendicular.X * RandomRange(-curvature, curvature),
            start.Y + (target.Y - start.Y) * 0.72 + perpendicular.Y * RandomRange(-curvature, curvature));

        var finalTarget = target;
        var overshoot = _random.NextDouble() < profile.OvershootChance && distance > 80;
        if (overshoot)
        {
            finalTarget = new ScreenPoint(
                target.X + (int)Math.Round((target.X - start.X) / Math.Max(distance, 1) * RandomRange(4, 14)),
                target.Y + (int)Math.Round((target.Y - start.Y) / Math.Max(distance, 1) * RandomRange(4, 14)));
        }

        var points = new List<TimedMousePoint>(steps + 4);
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var eased = EaseInOut(t);
            var point = Bezier(start, c1, c2, finalTarget, eased);
            if (i > 0 && i < steps - 2)
            {
                point = new FloatingPoint(
                    point.X + RandomRange(-profile.JitterPixels, profile.JitterPixels),
                    point.Y + RandomRange(-profile.JitterPixels, profile.JitterPixels));
            }

            points.Add(new TimedMousePoint(
                new ScreenPoint((int)Math.Round(point.X), (int)Math.Round(point.Y)),
                TimeSpan.FromMilliseconds(duration * t)));
        }

        if (overshoot)
        {
            points.Add(new TimedMousePoint(target, TimeSpan.FromMilliseconds(duration + _random.Next(45, 130))));
        }
        else
        {
            points[^1] = points[^1] with { Point = target };
        }

        return points;
    }

    private double RandomRange(double min, double max) => min + _random.NextDouble() * (max - min);

    private static FloatingPoint PerpendicularUnit(ScreenPoint start, ScreenPoint target)
    {
        var dx = target.X - start.X;
        var dy = target.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
        {
            return new FloatingPoint(0, 1);
        }

        return new FloatingPoint(-dy / length, dx / length);
    }

    private static double EaseInOut(double t) => t < 0.5
        ? 4 * t * t * t
        : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    private static FloatingPoint Bezier(ScreenPoint p0, FloatingPoint p1, FloatingPoint p2, ScreenPoint p3, double t)
    {
        var u = 1 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;
        return new FloatingPoint(
            uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X,
            uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y);
    }
}

public sealed record TimedMousePoint(ScreenPoint Point, TimeSpan At);

internal readonly record struct FloatingPoint(double X, double Y);
