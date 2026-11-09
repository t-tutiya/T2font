#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>
    /// 輪郭の区間 1 本（em・y 上向き）。直線か 2 次ベジエで、直線の制御点は中点に置く——<c>B(t) = (1−t)²P0 + 2t(1−t)C + t²P1</c> がどちらでも
    /// そのまま成り立つ（直線は t に比例して進む）。ベイクの幾何（和の輪郭・距離・巻き数）は倍精度のこの型で行う。
    /// </summary>
    internal readonly struct Curve
    {
        public Curve(double x0, double y0, double cx, double cy, double x1, double y1, bool isLine)
        {
            X0 = x0;
            Y0 = y0;
            Cx = isLine ? (x0 + x1) * 0.5 : cx;
            Cy = isLine ? (y0 + y1) * 0.5 : cy;
            X1 = x1;
            Y1 = y1;
            IsLine = isLine;
        }

        public double X0 { get; }
        public double Y0 { get; }
        public double Cx { get; }
        public double Cy { get; }
        public double X1 { get; }
        public double Y1 { get; }
        public bool IsLine { get; }

        public static Curve Line(double x0, double y0, double x1, double y1)
        {
            return new Curve(x0, y0, 0, 0, x1, y1, true);
        }

        public static Curve Quad(double x0, double y0, double cx, double cy, double x1, double y1)
        {
            return new Curve(x0, y0, cx, cy, x1, y1, false);
        }

        /// <summary>向きを逆にした区間。</summary>
        public Curve Reversed()
        {
            return new Curve(X1, Y1, Cx, Cy, X0, Y0, IsLine);
        }

        /// <summary>端点を置き換えた区間（制御点はそのまま。直線は中点を置き直す）。</summary>
        public Curve WithEndpoints(double x0, double y0, double x1, double y1)
        {
            return new Curve(x0, y0, Cx, Cy, x1, y1, IsLine);
        }

        public void Evaluate(double t, out double x, out double y)
        {
            var u = 1 - t;
            x = (u * u * X0) + (2 * u * t * Cx) + (t * t * X1);
            y = (u * u * Y0) + (2 * u * t * Cy) + (t * t * Y1);
        }

        /// <summary>接線 <c>B'(t)</c>。</summary>
        public void Derivative(double t, out double dx, out double dy)
        {
            dx = 2 * (((1 - t) * (Cx - X0)) + (t * (X1 - Cx)));
            dy = 2 * (((1 - t) * (Cy - Y0)) + (t * (Y1 - Cy)));
        }

        /// <summary>t の位置で 2 つに分ける（de Casteljau・正確）。</summary>
        public void Split(double t, out Curve first, out Curve second)
        {
            Evaluate(t, out var mx, out var my);
            if (IsLine)
            {
                first = Line(X0, Y0, mx, my);
                second = Line(mx, my, X1, Y1);
                return;
            }
            var ax = X0 + ((Cx - X0) * t);
            var ay = Y0 + ((Cy - Y0) * t);
            var bx = Cx + ((X1 - Cx) * t);
            var by = Cy + ((Y1 - Cy) * t);
            first = Quad(X0, Y0, ax, ay, mx, my);
            second = Quad(mx, my, bx, by, X1, Y1);
        }

        /// <summary>パラメータの範囲 [t0, t1] の部分（正確）。</summary>
        public Curve Sub(double t0, double t1)
        {
            if (t0 <= 0 && t1 >= 1)
            {
                return this;
            }
            Curve rest;
            if (t0 > 0)
            {
                Split(t0, out _, out rest);
            }
            else
            {
                rest = this;
            }
            if (t1 >= 1)
            {
                return rest;
            }
            var local = (t1 - t0) / (1 - t0);
            rest.Split(local, out var piece, out _);
            return piece;
        }

        /// <summary>制御点を含む境界ボックス（曲線はこの中にある）。</summary>
        public void Bounds(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = Math.Min(X0, Math.Min(Cx, X1));
            minY = Math.Min(Y0, Math.Min(Cy, Y1));
            maxX = Math.Max(X0, Math.Max(Cx, X1));
            maxY = Math.Max(Y0, Math.Max(Cy, Y1));
        }

        /// <summary>制御点の弦からの距離（曲線の弦からのずれはこの半分以下）。直線は 0。</summary>
        public double ControlDeviation()
        {
            if (IsLine)
            {
                return 0;
            }
            var dx = X1 - X0;
            var dy = Y1 - Y0;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length == 0)
            {
                return Math.Sqrt(((Cx - X0) * (Cx - X0)) + ((Cy - Y0) * (Cy - Y0)));
            }
            return Math.Abs(((Cx - X0) * dy) - ((Cy - Y0) * dx)) / length;
        }

        /// <summary>
        /// 点 (px, py) から区間への最短距離の二乗と、そのパラメータ t。曲線は <c>f(t) = (B(t) − p)·B'(t)/2</c>（距離の二乗の導関数の 1/4）の
        /// 根のうち、f が負から正へ変わる所（極小）を求めて端点と比べる。f の極値（f' の根＝2 次方程式）で [0, 1] を f が単調な区間に分け、
        /// 各区間の根を挟み込み付きの Newton 法で取る——三角関数を使う閉形式より速く、どの形の曲線でも根を落とさない。
        /// </summary>
        public double DistanceSquared(double px, double py, out double t)
        {
            if (IsLine)
            {
                var vx = X1 - X0;
                var vy = Y1 - Y0;
                var len2 = (vx * vx) + (vy * vy);
                t = len2 > 0 ? Math.Max(0, Math.Min(1, (((px - X0) * vx) + ((py - Y0) * vy)) / len2)) : 0;
                var ex = X0 + (vx * t) - px;
                var ey = Y0 + (vy * t) - py;
                return (ex * ex) + (ey * ey);
            }

            var ax = Cx - X0;
            var ay = Cy - Y0;
            var bx = X0 - (2 * Cx) + X1;
            var by = Y0 - (2 * Cy) + Y1;
            var dx = X0 - px;
            var dy = Y0 - py;
            // f(t) = c3 t³ + c2 t² + c1 t + c0。
            var c3 = (bx * bx) + (by * by);
            var c2 = 3 * ((ax * bx) + (ay * by));
            var c1 = (2 * ((ax * ax) + (ay * ay))) + (dx * bx) + (dy * by);
            var c0 = (dx * ax) + (dy * ay);

            var best = Dist2(px, py, 0);
            t = 0;
            var end = Dist2(px, py, 1);
            if (end < best)
            {
                best = end;
                t = 1;
            }

            // f' = 3c3 t² + 2c2 t + c1 の根で [0, 1] を分ける（多くて 3 区間）。
            var split0 = 2.0;
            var split1 = 2.0;
            var qa = 3 * c3;
            var qb = 2 * c2;
            if (Math.Abs(qa) > 1e-300)
            {
                var disc = (qb * qb) - (4 * qa * c1);
                if (disc > 0)
                {
                    var sq = Math.Sqrt(disc);
                    var q = -0.5 * (qb + (qb >= 0 ? sq : -sq));
                    var r1 = q / qa;
                    var r2 = q != 0 ? c1 / q : r1;
                    split0 = Math.Min(r1, r2);
                    split1 = Math.Max(r1, r2);
                }
            }
            else if (qb != 0)
            {
                split0 = -c1 / qb;
            }

            // Newton 法の初期値は点の弦への射影（多くの曲線は弦に近く、数回で収まる）。
            var chordX = X1 - X0;
            var chordY = Y1 - Y0;
            var chord2 = (chordX * chordX) + (chordY * chordY);
            var guess = chord2 > 0 ? (((px - X0) * chordX) + ((py - Y0) * chordY)) / chord2 : 0.5;

            var lo = 0.0;
            for (var piece = 0; piece < 3; piece++)
            {
                var hi = piece == 0 ? split0 : piece == 1 ? split1 : 1.0;
                if (!(hi > lo))
                {
                    continue;
                }
                if (hi > 1)
                {
                    hi = 1;
                }
                if (hi > lo)
                {
                    var root = MinimumIn(c3, c2, c1, c0, lo, hi, guess);
                    if (root > 0 && root < 1)
                    {
                        var d = Dist2(px, py, root);
                        if (d < best)
                        {
                            best = d;
                            t = root;
                        }
                    }
                }
                lo = Math.Max(lo, hi);
                if (lo >= 1)
                {
                    break;
                }
            }
            return best;
        }

        // f が単調な区間 [lo, hi] で、f が負から正へ変わる根（距離の極小）を返す。無ければ −1。
        private static double MinimumIn(double c3, double c2, double c1, double c0, double lo, double hi, double guess)
        {
            var flo = (((((c3 * lo) + c2) * lo) + c1) * lo) + c0;
            var fhi = (((((c3 * hi) + c2) * hi) + c1) * hi) + c0;
            if (!(flo < 0 && fhi > 0))
            {
                return -1;
            }
            var x = guess > lo && guess < hi ? guess : 0.5 * (lo + hi);
            for (var iteration = 0; iteration < 60; iteration++)
            {
                var f = (((((c3 * x) + c2) * x) + c1) * x) + c0;
                if (f == 0)
                {
                    return x;
                }
                if (f < 0)
                {
                    lo = x;
                }
                else
                {
                    hi = x;
                }
                if (hi - lo < 1e-14)
                {
                    break;
                }
                var df = (((3 * c3 * x) + (2 * c2)) * x) + c1;
                var next = df > 0 ? x - (f / df) : 0.5 * (lo + hi);
                if (!(next > lo && next < hi))
                {
                    next = 0.5 * (lo + hi);
                }
                // Newton 法は片側から近づくので挟みの幅は縮まらない。一歩が十分小さくなったら止める（極小での t の誤差は距離に 2 乗でしか効かない）。
                if (Math.Abs(next - x) <= 1e-12)
                {
                    return next;
                }
                x = next;
            }
            return x;
        }

        private double Dist2(double px, double py, double t)
        {
            Evaluate(t, out var x, out var y);
            return ((x - px) * (x - px)) + ((y - py) * (y - py));
        }

        /// <summary>
        /// 点 (px, py) から +x 方向へ伸ばした半直線と区間の交差の符号付きの数（非ゼロ巻き数の寄与）。区間を y の単調な部分に分け、
        /// 下端を含み上端を含まない規則で数える（頂点を通る半直線を二重に数えない）。上向きの交差が +1。
        /// </summary>
        public int Crossings(double px, double py)
        {
            if (IsLine)
            {
                return MonotoneCrossing(this, px, py);
            }
            var denominator = Y0 - (2 * Cy) + Y1;
            if (denominator != 0)
            {
                var extremum = (Y0 - Cy) / denominator;
                if (extremum > 0 && extremum < 1)
                {
                    Split(extremum, out var first, out var second);
                    // 分けた点の y を両方で揃える（丸めで単調性が崩れない）。
                    return MonotoneCrossing(first, px, py) + MonotoneCrossing(second, px, py);
                }
            }
            return MonotoneCrossing(this, px, py);
        }

        private static int MonotoneCrossing(in Curve c, double px, double py)
        {
            var ya = c.Y0;
            var yb = c.Y1;
            int direction;
            if (ya < yb)
            {
                if (!(ya <= py && py < yb))
                {
                    return 0;
                }
                direction = 1;
            }
            else if (yb < ya)
            {
                if (!(yb <= py && py < ya))
                {
                    return 0;
                }
                direction = -1;
            }
            else
            {
                return 0;
            }

            // 端点も制御点も点より左なら交わらない（解かずに済ませる）。
            if (c.X0 <= px && c.X1 <= px && c.Cx <= px)
            {
                return 0;
            }
            var t = c.IsLine ? (py - ya) / (yb - ya) : SolveMonotoneY(c, py);
            c.Evaluate(t, out var x, out _);
            return x > px ? direction : 0;
        }

        // y(t) = py を y の単調な区間で解く（根は [0, 1] に 1 つ）。
        private static double SolveMonotoneY(in Curve c, double py)
        {
            var a = c.Y0 - (2 * c.Cy) + c.Y1;
            var b = 2 * (c.Cy - c.Y0);
            var k = c.Y0 - py;
            double t;
            if (Math.Abs(a) < 1e-14)
            {
                t = b != 0 ? -k / b : 0;
            }
            else
            {
                var disc = Math.Max(0, (b * b) - (4 * a * k));
                var sq = Math.Sqrt(disc);
                var q = -0.5 * (b + (b >= 0 ? sq : -sq));
                var r1 = q / a;
                var r2 = q != 0 ? k / q : r1;
                t = Math.Abs(r1 - 0.5) <= Math.Abs(r2 - 0.5) ? r1 : r2;
            }
            return Math.Max(0, Math.Min(1, t));
        }
    }

    /// <summary>巻き数を数える（区間の集まりに対して）。</summary>
    internal static class Winding
    {
        /// <summary>点の非ゼロ巻き数（上向きの交差を +1）。</summary>
        public static int At(IReadOnlyList<Curve> curves, double px, double py)
        {
            var w = 0;
            for (var i = 0; i < curves.Count; i++)
            {
                curves[i].Bounds(out _, out var minY, out var maxX, out var maxY);
                if (py < minY || py >= maxY || maxX <= px)
                {
                    continue;
                }
                w += curves[i].Crossings(px, py);
            }
            return w;
        }
    }
}
