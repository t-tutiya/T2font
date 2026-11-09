#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>
    /// 索引格子（README §4.3）: texel ごとに「その texel の面積のどこかから見て最近傍になる区間」の集合（必要集合）を求める。シェーダは texel の
    /// 集合の区間だけを評価するので、集合が真の最近傍を含めば、画素の符号付き距離は全区間を評価したときと同じになる。
    ///
    /// <para>
    /// 求め方: ①texel の中心から区間への距離を取り、最小＋texel の対角の長さを超える区間を外す（距離は 1-Lipschitz なので、外した区間が
    /// texel の中で最近傍になることは無い＝証明付きの上位集合）。区間の制御点の境界ボックスまでの距離は区間までの距離の下限なので、下限が
    /// 明らかに大きい区間は厳密な距離を解かずに外す ②上位集合が 2 本以上なら、texel の中を通る区間を必ず入れ（その上の点で距離 0 の最近傍）、
    /// texel の閉じた面積を格子でサンプリングし（角と辺を含む）、
    /// 隣り合うサンプルで最近傍が変わる所を 2 分して詰め（その区間で 3 本目が最近傍になりうると Lipschitz で言えるときだけ下る）、見つけた最近傍の
    /// 和を必要集合にする。残る取りこぼしはサンプルの線から外れた、サンプルの間隔より細い領域だけで、そこでの距離の誤差は Lipschitz からその幅の
    /// 2 倍以下に限られる。サンプル点では、近くの点の最近傍を先に測り、境界ボックスまでの距離（下限）がそれより遠い候補は解かない。
    /// </para>
    ///
    /// <para>
    /// 上位集合をそのまま書かないのは、角の外側（頂点が最近傍になる楔の領域）では隣り合う 2 区間への距離が同じ頂点への距離で等しく、上位集合が
    /// いつも 2 本以上になるため。どちらを評価しても距離も符号（頂点の擬似法線は両方の区間が同じ項目から読む）も同じなので、サンプリングで
    /// 1 本（番号の小さい方）に決める。
    /// </para>
    /// </summary>
    internal static class NearestIndex
    {
        // texel の 1 辺のサンプルの区切りの数（(Samples + 1)² 点）と、最近傍が変わる所を 2 分する深さ。
        private const int Samples = 6;
        private const int BisectDepth = 4;

        /// <summary>索引格子（左下のセル・セルの数・セルごとの区間の番号の列〔セルの中心に近い順〕）。</summary>
        public sealed class Grid
        {
            public Grid(int x0, int y0, int width, int height, int[][] cells)
            {
                X0 = x0;
                Y0 = y0;
                Width = width;
                Height = height;
                Cells = cells;
            }

            /// <summary>左下のセル（<c>floor(em × indexEm)</c>）。</summary>
            public int X0 { get; }

            /// <inheritdoc cref="X0"/>
            public int Y0 { get; }

            public int Width { get; }

            public int Height { get; }

            /// <summary>セル（<c>y × Width + x</c>）ごとの区間の番号（<see cref="GlyphEncoder.Result.Segments"/> の添字・セルの中心に近い順）。</summary>
            public int[][] Cells { get; }
        }

        /// <summary>
        /// 範囲 [left, right]×[bottom, top]（em）を覆う索引格子を作る。
        /// </summary>
        public static Grid Build(IReadOnlyList<DecodedSegment> segments, int indexEm, double left, double bottom, double right, double top)
        {
            var x0 = (int)Math.Floor(left * indexEm);
            var y0 = (int)Math.Floor(bottom * indexEm);
            var width = (int)Math.Ceiling(right * indexEm) - x0;
            var height = (int)Math.Ceiling(top * indexEm) - y0;
            var size = 1.0 / indexEm;
            var diagonal = Math.Sqrt(2) * size;

            var curves = new Curve[segments.Count];
            var boxes = new (double MinX, double MinY, double MaxX, double MaxY)[segments.Count];
            for (var i = 0; i < segments.Count; i++)
            {
                curves[i] = segments[i].Curve;
                curves[i].Bounds(out var minX, out var minY, out var maxX, out var maxY);
                boxes[i] = (minX, minY, maxX, maxY);
            }

            var cells = new int[width * height][];
            var distances = new double[segments.Count];
            var candidates = new List<int>();
            var found = new SortedSet<int>();
            var nearest = new int[(Samples + 1) * (Samples + 1)];
            for (var cy = 0; cy < height; cy++)
            {
                for (var cx = 0; cx < width; cx++)
                {
                    var cellLeft = (x0 + cx) * size;
                    var cellBottom = (y0 + cy) * size;
                    var centerX = cellLeft + (0.5 * size);
                    var centerY = cellBottom + (0.5 * size);

                    // 下限の最も小さい区間の厳密な距離は最小の上界。下限が「上界＋対角」を超える区間は候補になりえないので解かない。
                    var nearestBound = 0;
                    for (var i = 0; i < segments.Count; i++)
                    {
                        distances[i] = BoxDistance(boxes[i], centerX, centerY);
                        if (distances[i] < distances[nearestBound])
                        {
                            nearestBound = i;
                        }
                    }
                    var upper = Math.Sqrt(curves[nearestBound].DistanceSquared(centerX, centerY, out _));
                    var min = double.MaxValue;
                    for (var i = 0; i < segments.Count; i++)
                    {
                        if (distances[i] <= upper + diagonal + 1e-12)
                        {
                            distances[i] = i == nearestBound ? upper : Math.Sqrt(curves[i].DistanceSquared(centerX, centerY, out _));
                            min = Math.Min(min, distances[i]);
                        }
                        else
                        {
                            distances[i] = double.MaxValue;
                        }
                    }
                    candidates.Clear();
                    for (var i = 0; i < segments.Count; i++)
                    {
                        if (distances[i] <= min + diagonal + 1e-12)
                        {
                            candidates.Add(i);
                        }
                    }
                    if (candidates.Count == 1)
                    {
                        cells[(cy * width) + cx] = new[] { candidates[0] };
                        continue;
                    }

                    // セルの中を通る区間は、その上の点で距離 0 の最近傍になるので必ず要る（サンプルの間をかすめるだけの区間も落とさない）。
                    found.Clear();
                    foreach (var i in candidates)
                    {
                        if (Crosses(curves[i], cellLeft, cellBottom, cellLeft + size, cellBottom + size, 0))
                        {
                            found.Add(i);
                        }
                    }
                    Sample(curves, boxes, candidates, cellLeft, cellBottom, size, nearest, found);
                    var cell = new int[found.Count];
                    found.CopyTo(cell);
                    // セルの中心に近い順に書く（同じ距離は番号の順）。シェーダは今の最短より外接矩形が遠い区間を解かずに飛ばすので、近い区間を
                    // 先に解くと飛ばしが効く（README §4.3）。found は候補の部分集合なので、中心からの距離は求めてある。
                    Array.Sort(cell, (a, b) => distances[a] != distances[b] ? distances[a].CompareTo(distances[b]) : a.CompareTo(b));
                    cells[(cy * width) + cx] = cell;
                }
            }
            return new Grid(x0, y0, width, height, cells);
        }

        private static void Sample(
            Curve[] curves, (double, double, double, double)[] boxes, List<int> candidates, double left, double bottom, double size,
            int[] nearest, SortedSet<int> found)
        {
            var step = size / Samples;
            var hint = candidates[0];
            for (var j = 0; j <= Samples; j++)
            {
                for (var i = 0; i <= Samples; i++)
                {
                    var n = Nearest(curves, boxes, candidates, left + (step * i), bottom + (step * j), hint);
                    hint = n;
                    nearest[(j * (Samples + 1)) + i] = n;
                    found.Add(n);
                }
            }
            if (found.Count == 1 || found.Count == candidates.Count)
            {
                // 1 本なら境は無い。候補が全部見つかっていれば、2 分しても新しい区間は出ない。
                return;
            }

            // 隣り合うサンプルで最近傍が変わる所を 2 分する（その間の細い領域で最近傍になる 3 本目を拾う）。
            for (var j = 0; j <= Samples; j++)
            {
                for (var i = 0; i <= Samples; i++)
                {
                    var here = nearest[(j * (Samples + 1)) + i];
                    var x = left + (step * i);
                    var y = bottom + (step * j);
                    if (i < Samples && nearest[(j * (Samples + 1)) + i + 1] != here)
                    {
                        Bisect(curves, boxes, candidates, x, y, here, x + step, y, nearest[(j * (Samples + 1)) + i + 1], BisectDepth, found);
                    }
                    if (j < Samples && nearest[((j + 1) * (Samples + 1)) + i] != here)
                    {
                        Bisect(curves, boxes, candidates, x, y, here, x, y + step, nearest[((j + 1) * (Samples + 1)) + i], BisectDepth, found);
                    }
                }
            }
        }

        private static void Bisect(
            Curve[] curves, (double MinX, double MinY, double MaxX, double MaxY)[] boxes, List<int> candidates,
            double ax, double ay, int a, double bx, double by, int b, int depth, SortedSet<int> found)
        {
            if (depth == 0)
            {
                return;
            }
            var mx = (ax + bx) * 0.5;
            var my = (ay + by) * 0.5;
            var m = Nearest(curves, boxes, candidates, mx, my, a);
            found.Add(m);

            // a・b・m 以外の候補が、この区間（長さ L）のどこかで最近傍になるには、中点での距離が最近傍の距離＋L 以内でなければならない
            // （2 つの距離の差は 2-Lipschitz で、中点から端までは L/2）。そういう候補が無ければ、この区間を下っても新しい区間は出ない。
            var length = Math.Sqrt(((bx - ax) * (bx - ax)) + ((by - ay) * (by - ay)));
            var limit = Math.Sqrt(curves[m].DistanceSquared(mx, my, out _)) + length;
            var limit2 = limit * limit;
            var other = false;
            foreach (var c in candidates)
            {
                if (c == a || c == b || c == m)
                {
                    continue;
                }
                var box = boxes[c];
                var dx = Math.Max(0, Math.Max(box.MinX - mx, mx - box.MaxX));
                var dy = Math.Max(0, Math.Max(box.MinY - my, my - box.MaxY));
                if ((dx * dx) + (dy * dy) > limit2)
                {
                    continue;
                }
                if (curves[c].DistanceSquared(mx, my, out _) <= limit2)
                {
                    other = true;
                    break;
                }
            }
            if (!other)
            {
                return;
            }
            if (m != a)
            {
                Bisect(curves, boxes, candidates, ax, ay, a, mx, my, m, depth - 1, found);
            }
            if (m != b)
            {
                Bisect(curves, boxes, candidates, mx, my, m, bx, by, b, depth - 1, found);
            }
        }

        /// <summary>
        /// 候補の中の最近傍（距離が等しければ番号の小さい方）。<paramref name="hint"/>（近くの点の最近傍）を先に測り、境界ボックスまでの距離（下限）が
        /// それより遠い候補は厳密な距離を解かずに飛ばす（下限が等しい候補も、厳密な距離が小さくなりえないので飛ばしてよい）。
        /// </summary>
        private static int Nearest(
            Curve[] curves, (double MinX, double MinY, double MaxX, double MaxY)[] boxes, List<int> candidates, double x, double y, int hint)
        {
            var best = hint;
            var bestDistance = curves[hint].DistanceSquared(x, y, out _);
            foreach (var i in candidates)
            {
                if (i == hint)
                {
                    continue;
                }
                var box = boxes[i];
                var bx = Math.Max(0, Math.Max(box.MinX - x, x - box.MaxX));
                var by = Math.Max(0, Math.Max(box.MinY - y, y - box.MaxY));
                var lower = (bx * bx) + (by * by);
                if (lower > bestDistance || (lower == bestDistance && i > best))
                {
                    continue;
                }
                var d = curves[i].DistanceSquared(x, y, out _);
                if (d < bestDistance || (d == bestDistance && i < best))
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>区間が矩形（閉じた範囲）を通るか。直線は線分と矩形の切り取り、曲線は制御点の境界ボックスで 2 分して弦で判定する。</summary>
        private static bool Crosses(in Curve c, double left, double bottom, double right, double top, int depth)
        {
            c.Bounds(out var minX, out var minY, out var maxX, out var maxY);
            if (maxX < left || minX > right || maxY < bottom || minY > top)
            {
                return false;
            }
            if (Inside(c.X0, c.Y0, left, bottom, right, top) || Inside(c.X1, c.Y1, left, bottom, right, top))
            {
                return true;
            }
            if (c.IsLine || depth >= 12 || c.ControlDeviation() < 1e-9)
            {
                return SegmentCrossesBox(c.X0, c.Y0, c.X1, c.Y1, left, bottom, right, top);
            }
            c.Split(0.5, out var first, out var second);
            return Crosses(first, left, bottom, right, top, depth + 1) || Crosses(second, left, bottom, right, top, depth + 1);
        }

        private static bool Inside(double x, double y, double left, double bottom, double right, double top)
        {
            return x >= left && x <= right && y >= bottom && y <= top;
        }

        // 線分と矩形の交わり（Liang–Barsky の切り取り）。
        private static bool SegmentCrossesBox(double x0, double y0, double x1, double y1, double left, double bottom, double right, double top)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            var t0 = 0.0;
            var t1 = 1.0;
            return Clip(-dx, x0 - left, ref t0, ref t1) && Clip(dx, right - x0, ref t0, ref t1)
                && Clip(-dy, y0 - bottom, ref t0, ref t1) && Clip(dy, top - y0, ref t0, ref t1);
        }

        private static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0)
            {
                return q >= 0;
            }
            var r = q / p;
            if (p < 0)
            {
                if (r > t1)
                {
                    return false;
                }
                if (r > t0)
                {
                    t0 = r;
                }
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }
                if (r < t1)
                {
                    t1 = r;
                }
            }
            return true;
        }

        private static double BoxDistance((double MinX, double MinY, double MaxX, double MaxY) box, double x, double y)
        {
            var dx = Math.Max(0, Math.Max(box.MinX - x, x - box.MaxX));
            var dy = Math.Max(0, Math.Max(box.MinY - y, y - box.MaxY));
            return Math.Sqrt((dx * dx) + (dy * dy));
        }
    }
}
