#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>
    /// 和集合輪郭（README §4.1）。重なった輪郭・逆向きの輪郭・辺を共有して接する輪郭を、<b>非ゼロ巻き数で塗った領域の境界</b>へ作り直し、
    /// 全部のセグメントを「塗る側が進行方向の右手」に向ける。
    ///
    /// <para>
    /// <b>なぜ要るか</b>: Rinkaku はシェーダで最近傍のセグメントの向きから符号（内か外か）を決める。重なりの内側の辺が残ると、字の中にその辺への
    /// 距離の筋が出て、縁取りが字の中に描かれる。逆向きの輪郭は向きから決めた符号が反転する。境界だけを残して向きを揃えれば、どの点でも
    /// 最近傍のセグメントの向きが内外を正しく言う。
    /// </para>
    ///
    /// <para>
    /// 手順: ①セグメントどうしの交点を求める（制御点のAABBで 2 分して絞り、弦で交わる所を求めて元の曲線の上で Newton 法で詰める）
    /// ②交点でセグメントを分ける（de Casteljau・正確・端点は共有の頂点に揃える）③分けた片の中点の左右の点の巻き数を元の全セグメントで数え、
    /// 片側だけが非ゼロの片を残して塗る側を右手に向ける ④同じ片の重複を除き、頂点で片を繋いで輪郭にする（分岐する頂点では最も右へ曲がる片へ進む）。
    /// どの段でも繋げなければ失敗の文を返す（黙って生の輪郭を使わない）。
    /// </para>
    /// </summary>
    internal static class OutlineUnion
    {
        // 頂点を同一とみなす距離（em）。フォントの座標は 1/(2·upem) の倍数なので、別の点はこれより遥かに離れている。
        private const double VertexEpsilon = 1e-9;

        // 交点を求める 2 分の打ち切り（制御点の弦からのずれ・em）と、深さの上限。
        private const double FlatEpsilon = 1e-7;
        private const int MaxDepth = 50;

        // 分類で片の中点から左右へずらす距離（em）。交点の誤差より大きく、別の境界との隙間（≥ 1/(2·upem)）より遥かに小さい。
        private const double SideEpsilon = 1e-7;

        /// <summary>和集合輪郭の 1 本（向きを揃えた閉じたセグメントの列）。</summary>
        public sealed class Loop
        {
            public Loop(List<Curve> curves)
            {
                Curves = curves;
            }

            public List<Curve> Curves { get; }
        }

        private struct Piece
        {
            public Curve Curve;
            public int Start;
            public int End;
            public int Contour;
            public bool Keep;
        }

        private readonly struct Hit
        {
            public Hit(int segment, double t, int vertex)
            {
                Segment = segment;
                T = t;
                Vertex = vertex;
            }

            public int Segment { get; }
            public double T { get; }
            public int Vertex { get; }
        }

        /// <summary>
        /// 輪郭（セグメントの閉じた列の集まり）から和集合輪郭を作る。失敗したら null を返し、<paramref name="error"/> に理由を入れる。
        /// </summary>
        /// <param name="contours">輪郭ごとのセグメント（em）。各輪郭は閉じていること（セグメントの終点が次のセグメントの始点に一致し、最後は最初へ戻る）。</param>
        public static List<Loop>? Build(IReadOnlyList<IReadOnlyList<Curve>> contours, out string error)
        {
            var segments = new List<Curve>();
            var contourOf = new List<int>();
            for (var c = 0; c < contours.Count; c++)
            {
                foreach (var curve in contours[c])
                {
                    segments.Add(curve);
                    contourOf.Add(c);
                }
            }
            if (segments.Count == 0)
            {
                error = string.Empty;
                return new List<Loop>();
            }

            // 頂点（座標が一致する端点は輪郭をまたいで 1 つ）。
            var vertices = new List<(double X, double Y)>();
            var starts = new int[segments.Count];
            var ends = new int[segments.Count];
            for (var i = 0; i < segments.Count; i++)
            {
                starts[i] = VertexOf(vertices, segments[i].X0, segments[i].Y0);
                ends[i] = VertexOf(vertices, segments[i].X1, segments[i].Y1);
            }

            var hits = FindHits(segments, starts, ends, vertices);
            var pieces = SplitAll(segments, contourOf, starts, ends, hits, vertices);
            Classify(segments, pieces);
            var kept = Deduplicate(pieces);
            return Chain(kept, vertices, out error);
        }

        private static int VertexOf(List<(double X, double Y)> vertices, double x, double y)
        {
            for (var i = 0; i < vertices.Count; i++)
            {
                if (Math.Abs(vertices[i].X - x) <= VertexEpsilon && Math.Abs(vertices[i].Y - y) <= VertexEpsilon)
                {
                    return i;
                }
            }
            vertices.Add((x, y));
            return vertices.Count - 1;
        }

        // ---------------------------------------------------------------- ① 交点

        private static List<Hit> FindHits(List<Curve> segments, int[] starts, int[] ends, List<(double X, double Y)> vertices)
        {
            var hits = new List<Hit>();
            var raw = new List<(double Ta, double Tb)>();
            for (var i = 0; i < segments.Count; i++)
            {
                segments[i].Bounds(out var aMinX, out var aMinY, out var aMaxX, out var aMaxY);
                for (var j = i + 1; j < segments.Count; j++)
                {
                    segments[j].Bounds(out var bMinX, out var bMinY, out var bMaxX, out var bMaxY);
                    if (aMaxX < bMinX - VertexEpsilon || bMaxX < aMinX - VertexEpsilon
                        || aMaxY < bMinY - VertexEpsilon || bMaxY < aMinY - VertexEpsilon)
                    {
                        continue;
                    }
                    var a = segments[i];
                    var b = segments[j];
                    if (SameGeometry(a, b))
                    {
                        // 同じセグメント（向きは問わない）の重なりは分けずに分類へ回す（逆向きは両側が塗られて消え、同じ向きは重複として除く）。
                        continue;
                    }

                    raw.Clear();
                    Intersect(a, 0, 1, b, 0, 1, 0, raw);
                    foreach (var (ta, tb) in raw)
                    {
                        AddHit(segments, starts, ends, vertices, i, ta, j, tb, hits);
                    }
                }
            }
            return hits;
        }

        private static bool SameGeometry(in Curve a, in Curve b)
        {
            if (a.IsLine != b.IsLine || !Near(a.Cx, a.Cy, b.Cx, b.Cy))
            {
                return false;
            }
            return (Near(a.X0, a.Y0, b.X0, b.Y0) && Near(a.X1, a.Y1, b.X1, b.Y1))
                || (Near(a.X0, a.Y0, b.X1, b.Y1) && Near(a.X1, a.Y1, b.X0, b.Y0));
        }

        private static bool Near(double x0, double y0, double x1, double y1)
        {
            return Math.Abs(x0 - x1) <= VertexEpsilon && Math.Abs(y0 - y1) <= VertexEpsilon;
        }

        private static void Intersect(in Curve a, double a0, double a1, in Curve b, double b0, double b1, int depth, List<(double, double)> raw)
        {
            a.Bounds(out var aMinX, out var aMinY, out var aMaxX, out var aMaxY);
            b.Bounds(out var bMinX, out var bMinY, out var bMaxX, out var bMaxY);
            if (aMaxX < bMinX - VertexEpsilon || bMaxX < aMinX - VertexEpsilon
                || aMaxY < bMinY - VertexEpsilon || bMaxY < aMinY - VertexEpsilon)
            {
                return;
            }

            var flatA = a.ControlDeviation() <= FlatEpsilon;
            var flatB = b.ControlDeviation() <= FlatEpsilon;
            if ((flatA && flatB) || depth >= MaxDepth)
            {
                ChordIntersect(a, a0, a1, b, b0, b1, raw);
                return;
            }

            var splitA = !flatA && (flatB || Extent(a) >= Extent(b));
            if (splitA)
            {
                a.Split(0.5, out var a00, out var a01);
                var am = (a0 + a1) * 0.5;
                Intersect(a00, a0, am, b, b0, b1, depth + 1, raw);
                Intersect(a01, am, a1, b, b0, b1, depth + 1, raw);
            }
            else
            {
                b.Split(0.5, out var b00, out var b01);
                var bm = (b0 + b1) * 0.5;
                Intersect(a, a0, a1, b00, b0, bm, depth + 1, raw);
                Intersect(a, a0, a1, b01, bm, b1, depth + 1, raw);
            }
        }

        private static double Extent(in Curve c)
        {
            c.Bounds(out var minX, out var minY, out var maxX, out var maxY);
            return Math.Max(maxX - minX, maxY - minY);
        }

        // 弦どうしの交点（平行で同じ直線の上なら、互いの端点のうち相手の内側にあるもの）。
        private static void ChordIntersect(in Curve a, double a0, double a1, in Curve b, double b0, double b1, List<(double, double)> raw)
        {
            var rx = a.X1 - a.X0;
            var ry = a.Y1 - a.Y0;
            var sx = b.X1 - b.X0;
            var sy = b.Y1 - b.Y0;
            var qx = b.X0 - a.X0;
            var qy = b.Y0 - a.Y0;
            var denominator = (rx * sy) - (ry * sx);
            var lr = Math.Sqrt((rx * rx) + (ry * ry));
            var ls = Math.Sqrt((sx * sx) + (sy * sy));
            if (lr == 0 || ls == 0)
            {
                return;
            }

            if (Math.Abs(denominator) <= 1e-12 * lr * ls)
            {
                // 平行。同じ直線の上（距離が誤差の内）なら重なりの端を交点にする。
                if (Math.Abs((qx * ry) - (qy * rx)) / lr > VertexEpsilon)
                {
                    return;
                }
                var len2 = lr * lr;
                AddOverlap(((qx * rx) + (qy * ry)) / len2, 0, a0, a1, b0, b1, raw);
                AddOverlap((((b.X1 - a.X0) * rx) + ((b.Y1 - a.Y0) * ry)) / len2, 1, a0, a1, b0, b1, raw);
                var ls2 = ls * ls;
                AddOverlapB(0, ((-qx * sx) - (qy * sy)) / ls2, a0, a1, b0, b1, raw);
                AddOverlapB(1, ((((a.X1 - b.X0) * sx) + ((a.Y1 - b.Y0) * sy)) / ls2), a0, a1, b0, b1, raw);
                return;
            }

            var s = ((qx * sy) - (qy * sx)) / denominator;
            var u = ((qx * ry) - (qy * rx)) / denominator;
            const double slack = 1e-9;
            if (s < -slack || s > 1 + slack || u < -slack || u > 1 + slack)
            {
                return;
            }
            s = Math.Max(0, Math.Min(1, s));
            u = Math.Max(0, Math.Min(1, u));
            raw.Add((a0 + (s * (a1 - a0)), b0 + (u * (b1 - b0))));
        }

        private static void AddOverlap(double sOnA, double endOfB, double a0, double a1, double b0, double b1, List<(double, double)> raw)
        {
            if (sOnA > 0 && sOnA < 1)
            {
                raw.Add((a0 + (sOnA * (a1 - a0)), b0 + (endOfB * (b1 - b0))));
            }
        }

        private static void AddOverlapB(double endOfA, double uOnB, double a0, double a1, double b0, double b1, List<(double, double)> raw)
        {
            if (uOnB > 0 && uOnB < 1)
            {
                raw.Add((a0 + (endOfA * (a1 - a0)), b0 + (uOnB * (b1 - b0))));
            }
        }

        private static void AddHit(
            List<Curve> segments, int[] starts, int[] ends, List<(double X, double Y)> vertices,
            int i, double ta, int j, double tb, List<Hit> hits)
        {
            var a = segments[i];
            var b = segments[j];
            Refine(a, b, ref ta, ref tb);
            a.Evaluate(ta, out var ax, out var ay);
            b.Evaluate(tb, out var bx, out var by);

            var aEnd = EndpointVertex(a, ax, ay, starts[i], ends[i]);
            var bEnd = EndpointVertex(b, bx, by, starts[j], ends[j]);
            if (aEnd >= 0 && bEnd >= 0)
            {
                // 端点どうし。座標が一致する端点は最初から同じ頂点にしてあるので、分けるものは無い。
                return;
            }
            if (aEnd >= 0)
            {
                hits.Add(new Hit(j, tb, aEnd));
                return;
            }
            if (bEnd >= 0)
            {
                hits.Add(new Hit(i, ta, bEnd));
                return;
            }
            var vertex = VertexOf(vertices, (ax + bx) * 0.5, (ay + by) * 0.5);
            hits.Add(new Hit(i, ta, vertex));
            hits.Add(new Hit(j, tb, vertex));
        }

        private static int EndpointVertex(in Curve c, double x, double y, int start, int end)
        {
            if (Near(x, y, c.X0, c.Y0))
            {
                return start;
            }
            if (Near(x, y, c.X1, c.Y1))
            {
                return end;
            }
            return -1;
        }

        // 交点を元の曲線の上で Newton 法で詰める（弦の交点は FlatEpsilon ほどずれうる）。直線どうしは弦が曲線そのものなので詰めない。
        private static void Refine(in Curve a, in Curve b, ref double ta, ref double tb)
        {
            if (a.IsLine && b.IsLine)
            {
                return;
            }
            var t = ta;
            var u = tb;
            for (var iteration = 0; iteration < 6; iteration++)
            {
                a.Evaluate(t, out var ax, out var ay);
                b.Evaluate(u, out var bx, out var by);
                var fx = ax - bx;
                var fy = ay - by;
                a.Derivative(t, out var dax, out var day);
                b.Derivative(u, out var dbx, out var dby);
                // J = [A'(t), −B'(u)]・J (Δt, Δu) = −F。
                var det = (dax * -dby) - (day * -dbx);
                if (Math.Abs(det) < 1e-18)
                {
                    return;
                }
                var dt = ((-fx * -dby) - (-fy * -dbx)) / det;
                var du = ((dax * -fy) - (day * -fx)) / det;
                t += dt;
                u += du;
                if (t < -1e-6 || t > 1 + 1e-6 || u < -1e-6 || u > 1 + 1e-6)
                {
                    return;
                }
            }
            ta = Math.Max(0, Math.Min(1, t));
            tb = Math.Max(0, Math.Min(1, u));
        }

        // ---------------------------------------------------------------- ② 分割

        private static List<Piece> SplitAll(
            List<Curve> segments, List<int> contourOf, int[] starts, int[] ends, List<Hit> hits, List<(double X, double Y)> vertices)
        {
            var bySegment = new List<(double T, int Vertex)>[segments.Count];
            foreach (var hit in hits)
            {
                (bySegment[hit.Segment] ??= new List<(double, int)>()).Add((hit.T, hit.Vertex));
            }

            var pieces = new List<Piece>();
            for (var i = 0; i < segments.Count; i++)
            {
                var points = new List<(double T, int Vertex)> { (0, starts[i]) };
                if (bySegment[i] != null)
                {
                    bySegment[i].Sort((p, q) => p.T.CompareTo(q.T));
                    foreach (var p in bySegment[i])
                    {
                        if (p.Vertex != points[points.Count - 1].Vertex && p.Vertex != ends[i])
                        {
                            points.Add(p);
                        }
                    }
                }
                points.Add((1, ends[i]));

                for (var k = 0; k + 1 < points.Count; k++)
                {
                    var (t0, v0) = points[k];
                    var (t1, v1) = points[k + 1];
                    if (v0 == v1)
                    {
                        continue;
                    }
                    var curve = segments[i].Sub(t0, t1).WithEndpoints(vertices[v0].X, vertices[v0].Y, vertices[v1].X, vertices[v1].Y);
                    pieces.Add(new Piece { Curve = curve, Start = v0, End = v1, Contour = contourOf[i] });
                }
            }
            return pieces;
        }

        // ---------------------------------------------------------------- ③ 分類

        private static void Classify(List<Curve> segments, List<Piece> pieces)
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                piece.Curve.Evaluate(0.5, out var mx, out var my);
                piece.Curve.Derivative(0.5, out var tx, out var ty);
                var length = Math.Sqrt((tx * tx) + (ty * ty));
                if (length == 0)
                {
                    tx = piece.Curve.X1 - piece.Curve.X0;
                    ty = piece.Curve.Y1 - piece.Curve.Y0;
                    length = Math.Sqrt((tx * tx) + (ty * ty));
                    if (length == 0)
                    {
                        continue;
                    }
                }
                // 右手の法線（進行方向 (tx, ty) に対して (ty, −tx)）。
                var nx = ty / length;
                var ny = -tx / length;
                var right = Winding.At(segments, mx + (nx * SideEpsilon), my + (ny * SideEpsilon)) != 0;
                var left = Winding.At(segments, mx - (nx * SideEpsilon), my - (ny * SideEpsilon)) != 0;
                if (right == left)
                {
                    continue;
                }
                piece.Keep = true;
                if (!right)
                {
                    piece.Curve = piece.Curve.Reversed();
                    (piece.Start, piece.End) = (piece.End, piece.Start);
                }
                pieces[i] = piece;
            }
        }

        private static List<Piece> Deduplicate(List<Piece> pieces)
        {
            var kept = new List<Piece>();
            foreach (var piece in pieces)
            {
                if (!piece.Keep)
                {
                    continue;
                }
                var duplicate = false;
                foreach (var other in kept)
                {
                    if (other.Start == piece.Start && other.End == piece.End && other.Curve.IsLine == piece.Curve.IsLine
                        && Near(other.Curve.Cx, other.Curve.Cy, piece.Curve.Cx, piece.Curve.Cy))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    kept.Add(piece);
                }
            }
            return kept;
        }

        // ---------------------------------------------------------------- ④ 鎖

        private static List<Loop>? Chain(List<Piece> pieces, List<(double X, double Y)> vertices, out string error)
        {
            var outgoing = new Dictionary<int, List<int>>();
            for (var i = 0; i < pieces.Count; i++)
            {
                if (!outgoing.TryGetValue(pieces[i].Start, out var list))
                {
                    list = new List<int>();
                    outgoing.Add(pieces[i].Start, list);
                }
                list.Add(i);
            }

            var used = new bool[pieces.Count];
            var loops = new List<Loop>();
            for (var first = 0; first < pieces.Count; first++)
            {
                if (used[first])
                {
                    continue;
                }
                used[first] = true;
                var curves = new List<Curve> { pieces[first].Curve };
                var current = first;
                while (pieces[current].End != pieces[first].Start)
                {
                    var next = ChooseNext(pieces, outgoing, used, current);
                    if (next < 0)
                    {
                        var v = vertices[pieces[current].End];
                        error = FormattableString.Invariant($"和集合輪郭を閉じられません（頂点 ({v.X:0.#####}, {v.Y:0.#####}) em から先へ進むセグメントがありません）。");
                        return null;
                    }
                    used[next] = true;
                    curves.Add(pieces[next].Curve);
                    current = next;
                }
                loops.Add(new Loop(curves));
            }
            error = string.Empty;
            return loops;
        }

        // 頂点から先へ進む片を選ぶ。1 つならそれ。複数（輪郭が点で接する）なら、同じ元の輪郭の片を先に、次に最も右へ曲がる片
        // （塗る側が右手なので、接した 2 つの領域を別の輪郭に分ける）。
        private static int ChooseNext(List<Piece> pieces, Dictionary<int, List<int>> outgoing, bool[] used, int current)
        {
            if (!outgoing.TryGetValue(pieces[current].End, out var candidates))
            {
                return -1;
            }
            var best = -1;
            var bestTurn = double.MaxValue;
            var bestSameContour = false;
            var c = pieces[current].Curve;
            var inX = c.X1 - c.Cx;
            var inY = c.Y1 - c.Cy;
            if (inX == 0 && inY == 0)
            {
                inX = c.X1 - c.X0;
                inY = c.Y1 - c.Y0;
            }
            foreach (var candidate in candidates)
            {
                if (used[candidate])
                {
                    continue;
                }
                var o = pieces[candidate].Curve;
                var outX = o.Cx - o.X0;
                var outY = o.Cy - o.Y0;
                if (outX == 0 && outY == 0)
                {
                    outX = o.X1 - o.X0;
                    outY = o.Y1 - o.Y0;
                }
                // 左回りが正の曲がりの角。右へ曲がるほど小さい。
                var turn = Math.Atan2((inX * outY) - (inY * outX), (inX * outX) + (inY * outY));
                var sameContour = pieces[candidate].Contour == pieces[current].Contour;
                if (best < 0 || (sameContour && !bestSameContour) || (sameContour == bestSameContour && turn < bestTurn))
                {
                    best = candidate;
                    bestTurn = turn;
                    bestSameContour = sameContour;
                }
            }
            return best;
        }
    }
}
