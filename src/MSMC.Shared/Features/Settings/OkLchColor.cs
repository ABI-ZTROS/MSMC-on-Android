using System;

namespace io.NET.ZTR_OS.Features.Settings.Colors;

public readonly struct OkLchColor
    {
        public double L { get; }
        public double C { get; }
        public double H { get; }

        public OkLchColor(double l, double c, double h)
        {
            L = l;
            C = c;
            H = h;
        }

        public static OkLchColor FromRgb(byte r, byte g, byte b)
        {
            var lr = SrgbToLinear(r / 255.0);
            var lg = SrgbToLinear(g / 255.0);
            var lb = SrgbToLinear(b / 255.0);

            var l = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb;
            var m = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb;
            var s = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb;

            var l_ = CubeRoot(l);
            var m_ = CubeRoot(m);
            var s_ = CubeRoot(s);

            var L2 = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
            var a = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
            var b2 = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;

            var h = Math.Atan2(b2, a) * 180.0 / Math.PI;
            if (h < 0) h += 360;

            var c = Math.Sqrt(a * a + b2 * b2);

            return new OkLchColor(L2, c, h);
        }

        public static OkLchColor FromColor(MsmcColor color)
        {
            return FromRgb(color.R, color.G, color.B);
        }

        public MsmcColor ToColor()
        {
            var (r, g, b) = ToRgbBytes();
            return MsmcColor.FromRgb(r, g, b);
        }

        public (byte R, byte G, byte B) ToRgbBytes()
        {
            var hr = H * Math.PI / 180.0;
            var a = C * Math.Cos(hr);
            var b2 = C * Math.Sin(hr);

            var l_ = L + 0.3963377774 * a + 0.2158037573 * b2;
            var m_ = L - 0.1055613458 * a - 0.0638541728 * b2;
            var s_ = L - 0.0894841775 * a - 1.2914855480 * b2;

            var l3 = l_ * l_ * l_;
            var m3 = m_ * m_ * m_;
            var s3 = s_ * s_ * s_;

            var lr = 4.0767416621 * l3 - 3.3077115913 * m3 + 0.2309699292 * s3;
            var lg = -1.2684380046 * l3 + 2.6097574011 * m3 - 0.3413193965 * s3;
            var lb = -0.0041960863 * l3 - 0.7034186147 * m3 + 1.7076147010 * s3;

            var r = LinearToSrgb(lr);
            var g = LinearToSrgb(lg);
            var b = LinearToSrgb(lb);

            return (r, g, b);
        }

        public string ToHex()
        {
            var (r, g, b) = ClampToSrgb();
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        public (byte R, byte G, byte B) ClampToSrgb()
        {
            var (r, g, b) = ToRgbBytes();
            if (r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255)
                return (r, g, b);

            double lo = 0, hi = C;
            for (int i = 0; i < 16; i++)
            {
                var mid = (lo + hi) / 2;
                var test = new OkLchColor(L, mid, H).ToRgbBytes();
                if (test.R < 0 || test.R > 255 || test.G < 0 || test.G > 255 || test.B < 0 || test.B > 255)
                    hi = mid;
                else
                    lo = mid;
            }
            return new OkLchColor(L, lo, H).ToRgbBytes();
        }

        public OkLchColor WithL(double l) => new OkLchColor(l, C, H);
        public OkLchColor WithC(double c) => new OkLchColor(L, c, H);
        public OkLchColor WithH(double h) => new OkLchColor(L, C, h);

        public static string[] Generate9StepScale(string baseHex)
        {
            var color = ColorHelper.FromRgbHex(baseHex);
            var oklch = FromColor(color);
            var result = new string[10];

            for (int i = 0; i < 5; i++)
            {
                var step = 5 - i;
                var l = Math.Min(0.96, oklch.L + step * 0.07);
                var c = Math.Max(0, oklch.C + step * -0.02);
                var (r, g, b) = new OkLchColor(l, c, oklch.H).ClampToSrgb();
                result[i] = $"#{r:X2}{g:X2}{b:X2}";
            }

            result[5] = ColorHelper.ToRgbHex(color);

            for (int i = 1; i <= 4; i++)
            {
                var l = Math.Max(0.04, oklch.L - i * 0.06);
                var c = Math.Max(0, oklch.C + i * 0.015);
                var (r, g, b) = new OkLchColor(l, c, oklch.H).ClampToSrgb();
                result[5 + i] = $"#{r:X2}{g:X2}{b:X2}";
            }

            return result;
        }

        private static double SrgbToLinear(double v)
        {
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static byte LinearToSrgb(double v)
        {
            var s = v <= 0.0031308
                ? v * 12.92
                : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
            return (byte)Math.Clamp(Math.Round(s * 255), 0, 255);
        }

        private static double CubeRoot(double v)
        {
            return Math.Pow(v, 1.0 / 3.0);
        }
    }
