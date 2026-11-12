#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>セグメントテーブルの項目 1 つ（README §3.2）。量子化した値で持つ。</summary>
    internal readonly struct EncodedEntry
    {
        public EncodedEntry(int x, int y, int dx, int dy, int angle, bool terminal)
        {
            X = x;
            Y = y;
            Dx = dx;
            Dy = dy;
            Angle = angle;
            Terminal = terminal;
        }

        /// <summary>始点（<c>(em + 1) × 16384</c>・0〜65535）。</summary>
        public int X { get; }

        /// <inheritdoc cref="X"/>
        public int Y { get; }

        /// <summary>制御点の始点からの差（<c>em × 4096</c>・−2048〜2047）。輪郭の終端の項目は 0。</summary>
        public int Dx { get; }

        /// <inheritdoc cref="Dx"/>
        public int Dy { get; }

        /// <summary>始点での擬似法線の角（0〜255・塗る側を向く）。</summary>
        public int Angle { get; }

        /// <summary>輪郭の終端の項目（セグメントを持たず、直前のセグメントの終点と擬似法線だけを持つ）か。</summary>
        public bool Terminal { get; }

        public void WriteTo(byte[] data, int offset)
        {
            data[offset] = (byte)(X & 0xFF);
            data[offset + 1] = (byte)(X >> 8);
            data[offset + 2] = (byte)(Y & 0xFF);
            data[offset + 3] = (byte)(Y >> 8);
            var dx = Dx & 0xFFF;
            var dy = Dy & 0xFFF;
            data[offset + 4] = (byte)(dx & 0xFF);
            data[offset + 5] = (byte)((dx >> 8) | ((dy & 0xF) << 4));
            data[offset + 6] = (byte)(dy >> 4);
            data[offset + 7] = (byte)Angle;
        }
    }

    /// <summary>
    /// シェーダが見るセグメント 1 本（量子化した値を元に戻したもの・em）。インデックスの構築と検証はこの値で行う——シェーダが評価する幾何と同じにするため。
    /// 直線は制御点の差を (0, 0) にして持つ（README §3.2）。
    /// </summary>
    internal readonly struct DecodedSegment
    {
        public DecodedSegment(int entry, Curve curve, double startNormalX, double startNormalY, double endNormalX, double endNormalY)
        {
            Entry = entry;
            Curve = curve;
            StartNormalX = startNormalX;
            StartNormalY = startNormalY;
            EndNormalX = endNormalX;
            EndNormalY = endNormalY;
        }

        /// <summary>セグメントの始点の項目の番号（0 始まり）。インデックスの項目番号はこれ＋1。</summary>
        public int Entry { get; }

        public Curve Curve { get; }

        /// <summary>始点の擬似法線（単位・塗る側を向く）。</summary>
        public double StartNormalX { get; }

        /// <inheritdoc cref="StartNormalX"/>
        public double StartNormalY { get; }

        /// <summary>終点の擬似法線。</summary>
        public double EndNormalX { get; }

        /// <inheritdoc cref="EndNormalX"/>
        public double EndNormalY { get; }

        /// <summary>
        /// 点の符号付き距離の符号を、このセグメントを最近傍として決める（README §2: 内点は接線の右手の法線、端点は擬似法線・塗る側が正）。
        /// </summary>
        public bool IsInside(double px, double py, double t)
        {
            Curve.Evaluate(t, out var bx, out var by);
            double nx;
            double ny;
            if (t <= 0)
            {
                nx = StartNormalX;
                ny = StartNormalY;
            }
            else if (t >= 1)
            {
                nx = EndNormalX;
                ny = EndNormalY;
            }
            else
            {
                Curve.Derivative(t, out var tx, out var ty);
                nx = ty;
                ny = -tx;
            }
            return ((px - bx) * nx) + ((py - by) * ny) >= 0;
        }
    }

    /// <summary>
    /// 和集合輪郭をセグメントテーブルへ符号化する（README §3.2・§4.2）: ほぼ直線の曲線を直線にし、制御点の差が持てない曲線を 2 分し、始点と制御点を
    /// 量子化し、量子化で長さ 0 になったセグメントを除き、頂点ごとの擬似法線を求める。輪郭ごとにセグメントの項目を並べ、終端の項目で閉じる。
    /// </summary>
    internal static class GlyphEncoder
    {
        // 曲線を直線にする閾値（|P0 − 2C + P1|・em）。弦からのずれはこの 1/4 以下（README §3.2）。小さい曲がりを曲線のまま持つと、
        // シェーダの三次方程式の係数が小さくなり float で条件が悪くなる。
        public const double LineThreshold = 1.0 / 1024;

        // 制御点の差の上限（em）。量子の範囲（±0.5em）に少し余裕を持たせる。
        private const double ControlLimitEm = 0.49;

        /// <summary>符号化の結果。</summary>
        public sealed class Result
        {
            public Result(List<EncodedEntry> entries, List<DecodedSegment> segments)
            {
                Entries = entries;
                Segments = segments;
            }

            public List<EncodedEntry> Entries { get; }

            public List<DecodedSegment> Segments { get; }
        }

        /// <summary>符号化する。座標が −1〜3em の外に出たら null を返し、<paramref name="error"/> に理由を入れる。</summary>
        public static Result? Encode(IReadOnlyList<OutlineUnion.Loop> loops, out string error)
        {
            var entries = new List<EncodedEntry>();
            var segments = new List<DecodedSegment>();
            foreach (var loop in loops)
            {
                var prepared = Prepare(loop.Curves);
                if (!Quantize(prepared, out var points, out var controls, out var lines, out error))
                {
                    return null;
                }
                if (points.Count == 0)
                {
                    continue;
                }

                var n = points.Count;
                var normals = new (double X, double Y)[n];
                var angles = new int[n];
                for (var k = 0; k < n; k++)
                {
                    var previous = (k + n - 1) % n;
                    var (inX, inY) = EndTangent(points[previous], controls[previous], points[k]);
                    var (outX, outY) = StartTangent(points[k], controls[k], points[(k + 1) % n]);
                    var (sx, sy) = Add(RightUnit(inX, inY), RightUnit(outX, outY));
                    if ((sx * sx) + (sy * sy) < 1e-24)
                    {
                        (sx, sy) = RightUnit(outX, outY);
                    }
                    angles[k] = QuantizeAngle(sx, sy);
                    normals[k] = DecodeAngle(angles[k]);
                }

                var first = entries.Count;
                for (var k = 0; k < n; k++)
                {
                    var p = points[k];
                    var c = controls[k];
                    entries.Add(new EncodedEntry(p.X, p.Y, c.X, c.Y, angles[k], false));
                    var next = points[(k + 1) % n];
                    var x0 = DecodeCoordinate(p.X);
                    var y0 = DecodeCoordinate(p.Y);
                    var curve = DecodedCurve(
                        x0, y0, x0 + DecodeControl(c.X), y0 + DecodeControl(c.Y), DecodeCoordinate(next.X), DecodeCoordinate(next.Y));
                    var endNormal = normals[(k + 1) % n];
                    segments.Add(new DecodedSegment(first + k, curve, normals[k].X, normals[k].Y, endNormal.X, endNormal.Y));
                }
                entries.Add(new EncodedEntry(points[0].X, points[0].Y, 0, 0, angles[0], true));
            }
            error = string.Empty;
            return new Result(entries, segments);
        }

        /// <summary>
        /// 元に戻したセグメント。制御点が弦の上（始点と終点の間）にちょうど乗るセグメントは直線そのもの（同じ点の集まり）なので、直線として持ち、距離を
        /// 正射影で求める——縦横の画のセグメントはほぼ全部これに当たる。量子化した値は 2 進の有限小数で桁も少ないので、この判定は倍精度で厳密。
        /// </summary>
        public static Curve DecodedCurve(double x0, double y0, double cx, double cy, double x1, double y1)
        {
            var ax = cx - x0;
            var ay = cy - y0;
            var dx = x1 - x0;
            var dy = y1 - y0;
            if ((ax * dy) - (ay * dx) == 0 && (ax * dx) + (ay * dy) >= 0 && (ax * ax) + (ay * ay) <= (dx * dx) + (dy * dy) && (dx != 0 || dy != 0))
            {
                return Curve.Line(x0, y0, x1, y1);
            }
            return Curve.Quad(x0, y0, cx, cy, x1, y1);
        }

        public static double DecodeCoordinate(int q)
        {
            return (q / RinkakuEncoding.CoordinateScale) - RinkakuEncoding.CoordinateOffset;
        }

        public static double DecodeControl(int q)
        {
            return q / RinkakuEncoding.ControlScale;
        }

        public static (double X, double Y) DecodeAngle(int angle)
        {
            var radians = angle * (2 * Math.PI / RinkakuEncoding.AngleSteps);
            return (Math.Cos(radians), Math.Sin(radians));
        }

        private static int QuantizeAngle(double x, double y)
        {
            var radians = Math.Atan2(y, x);
            var steps = (int)Math.Round(radians / (2 * Math.PI) * RinkakuEncoding.AngleSteps);
            return ((steps % RinkakuEncoding.AngleSteps) + RinkakuEncoding.AngleSteps) % RinkakuEncoding.AngleSteps;
        }

        // ほぼ直線の曲線を直線にし、制御点の差が持てない曲線を 2 分する。
        private static List<Curve> Prepare(List<Curve> curves)
        {
            var result = new List<Curve>();
            var stack = new Stack<Curve>();
            for (var i = curves.Count - 1; i >= 0; i--)
            {
                stack.Push(curves[i]);
            }
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                if (!c.IsLine && IsNearlyLine(c.X0, c.Y0, c.Cx, c.Cy, c.X1, c.Y1))
                {
                    c = Curve.Line(c.X0, c.Y0, c.X1, c.Y1);
                }
                if (!c.IsLine && (Math.Abs(c.Cx - c.X0) > ControlLimitEm || Math.Abs(c.Cy - c.Y0) > ControlLimitEm))
                {
                    c.Split(0.5, out var first, out var second);
                    stack.Push(second);
                    stack.Push(first);
                    continue;
                }
                result.Add(c);
            }
            return result;
        }

        private static bool IsNearlyLine(double x0, double y0, double cx, double cy, double x1, double y1)
        {
            var bx = x0 - (2 * cx) + x1;
            var by = y0 - (2 * cy) + y1;
            return (bx * bx) + (by * by) < LineThreshold * LineThreshold;
        }

        private static bool Quantize(
            List<Curve> curves, out List<(int X, int Y)> points, out List<(int X, int Y)> controls, out List<bool> lines, out string error)
        {
            points = new List<(int, int)>();
            controls = new List<(int, int)>();
            lines = new List<bool>();
            var quantizedStarts = new List<(int X, int Y)>();
            foreach (var c in curves)
            {
                if (!TryQuantizeCoordinate(c.X0, out var qx) || !TryQuantizeCoordinate(c.Y0, out var qy))
                {
                    error = FormattableString.Invariant($"座標 ({c.X0:0.###}, {c.Y0:0.###}) em が持てる範囲（−1〜3em）の外です。");
                    return false;
                }
                quantizedStarts.Add((qx, qy));
            }

            var n = curves.Count;
            for (var k = 0; k < n; k++)
            {
                var start = quantizedStarts[k];
                var end = quantizedStarts[(k + 1) % n];
                var c = curves[k];
                var x0 = DecodeCoordinate(start.X);
                var y0 = DecodeCoordinate(start.Y);
                var x1 = DecodeCoordinate(end.X);
                var y1 = DecodeCoordinate(end.Y);
                var isLine = c.IsLine;
                var control = isLine ? LineControl() : (QuantizeControl(c.Cx - x0), QuantizeControl(c.Cy - y0));
                if (!isLine && IsNearlyLine(x0, y0, x0 + DecodeControl(control.Item1), y0 + DecodeControl(control.Item2), x1, y1))
                {
                    isLine = true;
                    control = LineControl();
                }
                if (start == end && (isLine || control == (0, 0)))
                {
                    // 量子化で長さ 0 になったセグメント（次のセグメントの始点が同じ点なので、除いても輪郭は閉じたまま）。
                    continue;
                }
                if (control.Item1 < RinkakuEncoding.ControlMin || control.Item1 > RinkakuEncoding.ControlMax
                    || control.Item2 < RinkakuEncoding.ControlMin || control.Item2 > RinkakuEncoding.ControlMax)
                {
                    error = FormattableString.Invariant($"制御点の差 ({control.Item1}, {control.Item2}) が持てる範囲の外です。");
                    return false;
                }
                points.Add(start);
                controls.Add(control);
                lines.Add(isLine);
            }
            error = string.Empty;
            return true;
        }

        // 直線は制御点の差を (0, 0) にして持つ（README §3.2）。シェーダはこれを直線の印として三次方程式を解かずに正射影で測る。
        // 制御点が始点に重なる 2 次ベジエは始点から終点への線分そのもの（同じ点の集まり）なので、曲線がこの印を持っても距離は同じ。
        private static (int, int) LineControl()
        {
            return (0, 0);
        }

        private static bool TryQuantizeCoordinate(double v, out int q)
        {
            q = (int)Math.Round((v + RinkakuEncoding.CoordinateOffset) * RinkakuEncoding.CoordinateScale);
            return q >= 0 && q <= 0xFFFF;
        }

        private static int QuantizeControl(double v)
        {
            return (int)Math.Round(v * RinkakuEncoding.ControlScale);
        }

        private static (double, double) StartTangent((int X, int Y) start, (int X, int Y) control, (int X, int Y) end)
        {
            if (control.X != 0 || control.Y != 0)
            {
                return (DecodeControl(control.X), DecodeControl(control.Y));
            }
            return (DecodeCoordinate(end.X) - DecodeCoordinate(start.X), DecodeCoordinate(end.Y) - DecodeCoordinate(start.Y));
        }

        private static (double, double) EndTangent((int X, int Y) start, (int X, int Y) control, (int X, int Y) end)
        {
            var x0 = DecodeCoordinate(start.X);
            var y0 = DecodeCoordinate(start.Y);
            var tx = DecodeCoordinate(end.X) - (x0 + DecodeControl(control.X));
            var ty = DecodeCoordinate(end.Y) - (y0 + DecodeControl(control.Y));
            if (tx == 0 && ty == 0)
            {
                return (DecodeCoordinate(end.X) - x0, DecodeCoordinate(end.Y) - y0);
            }
            return (tx, ty);
        }

        // 進行方向 (x, y) の右手の単位法線（塗る側）。
        private static (double, double) RightUnit(double x, double y)
        {
            var length = Math.Sqrt((x * x) + (y * y));
            return length == 0 ? (0, 0) : (y / length, -x / length);
        }

        private static (double, double) Add((double X, double Y) a, (double X, double Y) b)
        {
            return (a.X + b.X, a.Y + b.Y);
        }
    }
}
