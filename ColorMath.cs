namespace DialedApp;

public readonly record struct Hsb(double Hue, double Sat, double Bri)
{
    public (byte r, byte g, byte b) ToRgb()
    {
        double s = Math.Clamp(Sat / 100.0, 0.0, 1.0);
        double v = Math.Clamp(Bri / 100.0, 0.0, 1.0);
        double h = ((Hue % 360.0) + 360.0) % 360.0;

        double c = v * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double m = v - c;

        var (red, green, blue) = hp switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return (
            (byte)Math.Round(Math.Clamp((red + m) * 255.0, 0.0, 255.0)),
            (byte)Math.Round(Math.Clamp((green + m) * 255.0, 0.0, 255.0)),
            (byte)Math.Round(Math.Clamp((blue + m) * 255.0, 0.0, 255.0))
        );
    }
}

public static class ColorMath
{
    // sRGB -> CIELAB (D65)
    public static (double L, double a, double b) ToLab(byte r8, byte g8, byte b8)
    {
        static double Linearize(double value)
        {
            double c = value / 255.0;
            return c <= 0.04045
                ? c / 12.92
                : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Pivot(double value) =>
            value > 0.008856451679
                ? Math.Cbrt(value)
                : 7.787037037 * value + 16.0 / 116.0;

        double r = Linearize(r8);
        double g = Linearize(g8);
        double b = Linearize(b8);

        double x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047;
        double y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
        double z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883;

        double fx = Pivot(x);
        double fy = Pivot(y);
        double fz = Pivot(z);

        return (
            116.0 * fy - 16.0,
            500.0 * (fx - fy),
            200.0 * (fy - fz)
        );
    }

    // CIEDE2000 colour difference. Lower is better; 0 means an exact match.
    public static double DeltaE(Hsb first, Hsb second)
    {
        var (r1, g1, b1) = first.ToRgb();
        var (r2, g2, b2) = second.ToRgb();

        var lab1 = ToLab(r1, g1, b1);
        var lab2 = ToLab(r2, g2, b2);

        return Ciede2000(lab1, lab2);
    }

    private static double Ciede2000(
        (double L, double a, double b) first,
        (double L, double a, double b) second)
    {
        const double kL = 1.0;
        const double kC = 1.0;
        const double kH = 1.0;

        double l1 = first.L;
        double a1 = first.a;
        double b1 = first.b;
        double l2 = second.L;
        double a2 = second.a;
        double b2 = second.b;

        double c1 = Math.Sqrt(a1 * a1 + b1 * b1);
        double c2 = Math.Sqrt(a2 * a2 + b2 * b2);
        double cBar = (c1 + c2) / 2.0;

        double g = 0.5 * (1.0 - Math.Sqrt(
            Math.Pow(cBar, 7.0) /
            (Math.Pow(cBar, 7.0) + Math.Pow(25.0, 7.0))));

        double a1Prime = (1.0 + g) * a1;
        double a2Prime = (1.0 + g) * a2;

        double c1Prime = Math.Sqrt(a1Prime * a1Prime + b1 * b1);
        double c2Prime = Math.Sqrt(a2Prime * a2Prime + b2 * b2);

        double h1Prime = HueAngle(a1Prime, b1);
        double h2Prime = HueAngle(a2Prime, b2);

        double deltaLPrime = l2 - l1;
        double deltaCPrime = c2Prime - c1Prime;

        double deltahPrime;
        if (c1Prime * c2Prime == 0.0)
        {
            deltahPrime = 0.0;
        }
        else
        {
            deltahPrime = h2Prime - h1Prime;
            if (deltahPrime > 180.0)
                deltahPrime -= 360.0;
            else if (deltahPrime < -180.0)
                deltahPrime += 360.0;
        }

        double deltaHPrime = 2.0 * Math.Sqrt(c1Prime * c2Prime) *
                             Math.Sin(DegreesToRadians(deltahPrime / 2.0));

        double lBarPrime = (l1 + l2) / 2.0;
        double cBarPrime = (c1Prime + c2Prime) / 2.0;

        double hBarPrime;
        if (c1Prime * c2Prime == 0.0)
        {
            hBarPrime = h1Prime + h2Prime;
        }
        else if (Math.Abs(h1Prime - h2Prime) <= 180.0)
        {
            hBarPrime = (h1Prime + h2Prime) / 2.0;
        }
        else
        {
            hBarPrime = (h1Prime + h2Prime + 360.0) / 2.0;

            if (h1Prime + h2Prime >= 360.0)
                hBarPrime = (h1Prime + h2Prime - 360.0) / 2.0;
        }

        double t =
            1.0
            - 0.17 * Math.Cos(DegreesToRadians(hBarPrime - 30.0))
            + 0.24 * Math.Cos(DegreesToRadians(2.0 * hBarPrime))
            + 0.32 * Math.Cos(DegreesToRadians(3.0 * hBarPrime + 6.0))
            - 0.20 * Math.Cos(DegreesToRadians(4.0 * hBarPrime - 63.0));

        double deltaTheta =
            30.0 * Math.Exp(-Math.Pow((hBarPrime - 275.0) / 25.0, 2.0));

        double rC =
            2.0 * Math.Sqrt(
                Math.Pow(cBarPrime, 7.0) /
                (Math.Pow(cBarPrime, 7.0) + Math.Pow(25.0, 7.0)));

        double sL =
            1.0 +
            (0.015 * Math.Pow(lBarPrime - 50.0, 2.0)) /
            Math.Sqrt(20.0 + Math.Pow(lBarPrime - 50.0, 2.0));

        double sC = 1.0 + 0.045 * cBarPrime;
        double sH = 1.0 + 0.015 * cBarPrime * t;

        double rT = -Math.Sin(DegreesToRadians(2.0 * deltaTheta)) * rC;

        double lTerm = deltaLPrime / (kL * sL);
        double cTerm = deltaCPrime / (kC * sC);
        double hTerm = deltaHPrime / (kH * sH);

        return Math.Sqrt(
            lTerm * lTerm +
            cTerm * cTerm +
            hTerm * hTerm +
            rT * cTerm * hTerm);
    }

    private static double HueAngle(double a, double b)
    {
        if (a == 0.0 && b == 0.0)
            return 0.0;

        double angle = RadiansToDegrees(Math.Atan2(b, a));
        return angle < 0.0 ? angle + 360.0 : angle;
    }

    private static double DegreesToRadians(double degrees) =>
        degrees * Math.PI / 180.0;

    private static double RadiansToDegrees(double radians) =>
        radians * 180.0 / Math.PI;

    // Converts perceptual CIEDE2000 distance to a smooth 0-100 score.
    // 0 Delta E is always 100%; larger perceptual errors decay smoothly.
    public static double ScorePercentFromDeltaE(double deltaE)
    {
        if (!double.IsFinite(deltaE) || deltaE <= 0.0)
            return 100.0;

        const double decay = 18.0;
        return Math.Round(
            Math.Clamp(100.0 * Math.Exp(-deltaE / decay), 0.0, 100.0),
            1);
    }
}
