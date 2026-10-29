#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>輪郭の 1 点（フォント単位・y 上向き）。<see cref="OnCurve"/> が false の点は 2 次ベジエの制御点。</summary>
    public readonly struct OutlinePoint
    {
        public OutlinePoint(double x, double y, bool onCurve)
        {
            X = x;
            Y = y;
            OnCurve = onCurve;
        }

        public double X { get; }
        public double Y { get; }
        public bool OnCurve { get; }
    }

    /// <summary>線分 1 本（ラスタライザの入力・ピクセル座標・y 上向き）。</summary>
    public readonly struct OutlineEdge
    {
        public OutlineEdge(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public double X0 { get; }
        public double Y0 { get; }
        public double X1 { get; }
        public double Y1 { get; }
    }

    /// <summary>
    /// グリフの輪郭（フォント単位・y 上向き）。TrueType の輪郭は on-curve 点と、2 次ベジエの制御点（off-curve）の並びで、
    /// 連続する 2 つの off-curve 点の間には中点の on-curve 点が暗黙にある（TrueType の仕様）。
    /// </summary>
    public sealed class GlyphOutline
    {
        private readonly List<OutlinePoint[]> _contours;

        public GlyphOutline(List<OutlinePoint[]> contours)
        {
            _contours = contours;
        }

        /// <summary>輪郭の本数。</summary>
        public int ContourCount => _contours.Count;

        /// <summary>輪郭（読み取り専用の扱い）。</summary>
        public IReadOnlyList<OutlinePoint[]> Contours => _contours;

        /// <summary>インクが無い（輪郭 0 本か、点が 1 つも無い）か。</summary>
        public bool IsEmpty
        {
            get
            {
                foreach (var contour in _contours)
                {
                    if (contour.Length > 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// 制御点を含む全点の境界ボックス（フォント単位）。2 次ベジエは制御点の凸包の中にあるので、曲線の境界ボックスを
        /// 必ず含む（保守的）。インクが無ければ false。
        /// </summary>
        public bool TryGetBounds(out double xMin, out double yMin, out double xMax, out double yMax)
        {
            xMin = yMin = double.MaxValue;
            xMax = yMax = double.MinValue;
            var any = false;
            foreach (var contour in _contours)
            {
                foreach (var p in contour)
                {
                    any = true;
                    xMin = Math.Min(xMin, p.X);
                    yMin = Math.Min(yMin, p.Y);
                    xMax = Math.Max(xMax, p.X);
                    yMax = Math.Max(yMax, p.Y);
                }
            }
            if (!any)
            {
                xMin = yMin = xMax = yMax = 0;
            }
            return any;
        }

        /// <summary>
        /// 輪郭を線分へフラット化する。点は <c>(x × scale + offsetX, y × scale + offsetY)</c> へ写す（ピクセル座標）。
        /// 曲線は、弦と曲線の最大のずれが <paramref name="tolerance"/>（ピクセル）以下になる本数の線分に等分する。
        /// 2 次ベジエを n 等分したときのずれは <c>|p0 − 2·p1 + p2| / (4·n²)</c> 以下なので、本数はそこから決まる。
        /// </summary>
        public void Flatten(double scale, double offsetX, double offsetY, double tolerance, List<OutlineEdge> edges)
        {
            if (tolerance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance));
            }

            foreach (var contour in _contours)
            {
                var n = contour.Length;
                if (n < 2)
                {
                    continue;
                }

                // 開始点（on-curve）を決める。全点が off-curve なら最初の 2 点の中点から始める。
                var startIndex = -1;
                for (var i = 0; i < n; i++)
                {
                    if (contour[i].OnCurve)
                    {
                        startIndex = i;
                        break;
                    }
                }

                double sx;
                double sy;
                int first;
                if (startIndex >= 0)
                {
                    sx = contour[startIndex].X;
                    sy = contour[startIndex].Y;
                    first = startIndex + 1;
                }
                else
                {
                    sx = (contour[0].X + contour[1].X) * 0.5;
                    sy = (contour[0].Y + contour[1].Y) * 0.5;
                    first = 1;
                    startIndex = 0;
                }

                var cx = sx;
                var cy = sy;
                var hasControl = false;
                double qx = 0;
                double qy = 0;
                for (var step = 0; step < n; step++)
                {
                    var p = contour[(first + step) % n];
                    // 全点 off-curve の場合、開始の中点を作った 2 点のうち 2 点目（index 1）から回るので、
                    // 1 周すると index 0 を最後に処理して開始の中点へ戻る。
                    if (p.OnCurve)
                    {
                        if (hasControl)
                        {
                            AddQuad(cx, cy, qx, qy, p.X, p.Y, scale, offsetX, offsetY, tolerance, edges);
                            hasControl = false;
                        }
                        else
                        {
                            AddLine(cx, cy, p.X, p.Y, scale, offsetX, offsetY, edges);
                        }
                        cx = p.X;
                        cy = p.Y;
                    }
                    else
                    {
                        if (hasControl)
                        {
                            var mx = (qx + p.X) * 0.5;
                            var my = (qy + p.Y) * 0.5;
                            AddQuad(cx, cy, qx, qy, mx, my, scale, offsetX, offsetY, tolerance, edges);
                            cx = mx;
                            cy = my;
                        }
                        qx = p.X;
                        qy = p.Y;
                        hasControl = true;
                    }
                }

                // 開始点へ閉じる。
                if (hasControl)
                {
                    AddQuad(cx, cy, qx, qy, sx, sy, scale, offsetX, offsetY, tolerance, edges);
                }
                else
                {
                    AddLine(cx, cy, sx, sy, scale, offsetX, offsetY, edges);
                }
            }
        }

        private static void AddLine(
            double x0, double y0, double x1, double y1, double scale, double ox, double oy, List<OutlineEdge> edges)
        {
            var ax = (x0 * scale) + ox;
            var ay = (y0 * scale) + oy;
            var bx = (x1 * scale) + ox;
            var by = (y1 * scale) + oy;
            if (ax != bx || ay != by)
            {
                edges.Add(new OutlineEdge(ax, ay, bx, by));
            }
        }

        private static void AddQuad(
            double x0, double y0, double x1, double y1, double x2, double y2,
            double scale, double ox, double oy, double tolerance, List<OutlineEdge> edges)
        {
            var p0x = (x0 * scale) + ox;
            var p0y = (y0 * scale) + oy;
            var p1x = (x1 * scale) + ox;
            var p1y = (y1 * scale) + oy;
            var p2x = (x2 * scale) + ox;
            var p2y = (y2 * scale) + oy;

            var ddx = p0x - (2 * p1x) + p2x;
            var ddy = p0y - (2 * p1y) + p2y;
            var dd = Math.Sqrt((ddx * ddx) + (ddy * ddy));
            var segments = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dd / (4 * tolerance))));

            var px = p0x;
            var py = p0y;
            for (var i = 1; i <= segments; i++)
            {
                var t = (double)i / segments;
                var u = 1 - t;
                var x = (u * u * p0x) + (2 * u * t * p1x) + (t * t * p2x);
                var y = (u * u * p0y) + (2 * u * t * p1y) + (t * t * p2y);
                if (px != x || py != y)
                {
                    edges.Add(new OutlineEdge(px, py, x, y));
                }
                px = x;
                py = y;
            }
        }
    }

    /// <summary>
    /// 2 次元のアフィン変換 <c>x' = A·x + C·y + E</c>・<c>y' = B·x + D·y + F</c>（TrueType の複合グリフの部品配置）。
    /// </summary>
    internal readonly struct Affine
    {
        public static readonly Affine Identity = new Affine(1, 0, 0, 1, 0, 0);

        public Affine(double a, double b, double c, double d, double e, double f)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
        }

        public double A { get; }
        public double B { get; }
        public double C { get; }
        public double D { get; }
        public double E { get; }
        public double F { get; }

        public (double X, double Y) Apply(double x, double y)
        {
            return ((A * x) + (C * y) + E, (B * x) + (D * y) + F);
        }

        public Affine Inverse()
        {
            var det = (A * D) - (B * C);
            if (Math.Abs(det) < 1e-12)
            {
                throw new FormatException("複合グリフの変換行列が退化していて、点の照合ができません。");
            }
            var ia = D / det;
            var ib = -B / det;
            var ic = -C / det;
            var id = A / det;
            return new Affine(ia, ib, ic, id, -((ia * E) + (ic * F)), -((ib * E) + (id * F)));
        }
    }
}
