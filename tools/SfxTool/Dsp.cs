using System;

namespace SfxTool
{
    /// <summary>Signal tools behind the richer <see cref="Sound"/> operations: complex FFT, an FFT equaliser, resonators.</summary>
    public static class Dsp
    {
        /// <summary>In-place complex FFT (radix 2); <paramref name="inverse"/> computes the inverse, scaled by 1/n.</summary>
        public static void Fft(double[] re, double[] im, bool inverse = false)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = (inverse ? 2 : -2) * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int p = i + k, q = i + k + len / 2;
                        double tr = re[q] * cr - im[q] * ci, ti = re[q] * ci + im[q] * cr;
                        re[q] = re[p] - tr; im[q] = im[p] - ti; re[p] += tr; im[p] += ti;
                        double ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
            if (inverse) for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
        }

        /// <summary>
        /// Gain in dB at <paramref name="hz"/> from (frequency, dB) points: linear in dB over log frequency, flat beyond the
        /// first and last point.
        /// </summary>
        public static double GainAt((float hz, float db)[] points, double hz)
        {
            if (hz <= points[0].hz) return points[0].db;
            for (int i = 1; i < points.Length; i++)
                if (hz <= points[i].hz)
                {
                    double t = Math.Log(hz / points[i - 1].hz) / Math.Log(points[i].hz / (double)points[i - 1].hz);
                    return points[i - 1].db + (points[i].db - points[i - 1].db) * t;
                }
            return points[points.Length - 1].db;
        }

        /// <summary>
        /// Zero-phase FFT equaliser: the whole buffer is transformed (padded against wrap-around), each bin scaled by
        /// <see cref="GainAt"/>, and transformed back. Any spectral shape, no phase smearing of attacks.
        /// </summary>
        public static float[] Eq(float[] s, (float hz, float db)[] points)
        {
            int pad = 4096, n = 1;
            while (n < s.Length + 2 * pad) n <<= 1;
            var re = new double[n]; var im = new double[n];
            for (int i = 0; i < s.Length; i++) re[i + pad] = s[i];
            Fft(re, im);
            for (int k = 0; k <= n / 2; k++)
            {
                double g = Math.Pow(10, GainAt(points, k * (double)Sound.Rate / n) / 20);
                re[k] *= g; im[k] *= g;
                if (k > 0 && k < n / 2) { re[n - k] *= g; im[n - k] *= g; }
            }
            Fft(re, im, true);
            var o = new float[s.Length];
            for (int i = 0; i < s.Length; i++) o[i] = (float)re[i + pad];
            return o;
        }

        /// <summary>
        /// A two-pole resonator: rings at <paramref name="hz"/>, its amplitude falling by <paramref name="decayDbPerSecond"/>.
        /// A unit impulse in gives a decaying sine of peak 1 out (input scaled by sin ω), so struck objects can be built
        /// from impulses or noise bursts driving a bank of these (modal synthesis driven by a real excitation).
        /// </summary>
        public static float[] Resonate(float[] x, float hz, float decayDbPerSecond)
        {
            double r = Math.Pow(10, -decayDbPerSecond / (20.0 * Sound.Rate)), w = 2 * Math.PI * hz / Sound.Rate;
            double a1 = 2 * r * Math.Cos(w), a2 = -r * r, g = Math.Sin(w);
            var y = new float[x.Length];
            double y1 = 0, y2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double v = g * x[i] + a1 * y1 + a2 * y2;
                y[i] = (float)v; y2 = y1; y1 = v;
            }
            return y;
        }
    }
}
