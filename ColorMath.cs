namespace DialedApp;

public readonly record struct Hsb(double Hue, double Sat, double Bri)
{
    public (byte r, byte g, byte b) ToRgb()
    {
        double s = Sat / 100, v = Bri / 100;
        double c = v * s, hp = (Hue % 360) / 60, x = c * (1 - Math.Abs(hp % 2 - 1)), m = v - c;
        var (r, g, b) = hp switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return ((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}

public static class ColorMath
{
    // sRGB -> CIELAB (D65)
    public static (double L, double a, double b) ToLab(byte r8, byte g8, byte b8)
    {
        static double Lin(double c) { c /= 255; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
        double R = Lin(r8), G = Lin(g8), B = Lin(b8);
        double X = (0.4124564 * R + 0.3575761 * G + 0.1804375 * B) / 0.95047;
        double Y = 0.2126729 * R + 0.7151522 * G + 0.0721750 * B;
        double Z = (0.0193339 * R + 0.1191920 * G + 0.9503041 * B) / 1.08883;
        double fx = F(X), fy = F(Y), fz = F(Z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    // CIE76 Delta E: perceptual distance between two colours
    public static double DeltaE(Hsb a, Hsb b)
    {
        var (r1, g1, b1) = a.ToRgb();
        var (r2, g2, b2) = b.ToRgb();
        var l1 = ToLab(r1, g1, b1);
        var l2 = ToLab(r2, g2, b2);
        return Math.Sqrt(Math.Pow(l1.L - l2.L, 2) + Math.Pow(l1.a - l2.a, 2) + Math.Pow(l1.b - l2.b, 2));
    }
}
