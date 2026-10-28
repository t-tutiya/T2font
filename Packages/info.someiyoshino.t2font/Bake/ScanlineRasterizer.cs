#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// 線分の集まりを、画素の中心で内外を判定して 2 値に塗る（非ゼロ巻き数規則）。
    ///
    /// <para>
    /// <b>非ゼロ巻き数で塗るのは輪郭の重なりのため</b>: 和文フォントには画の交差を別の輪郭で描き、重ねたまま
    /// 出荷したものがある。偶奇規則だと重なりが抜け、輪郭から直接距離を測る方式では重なりの内側の辺が縁に
    /// 化ける。塗ってから距離を取る（<see cref="DistanceTransform"/>）方式は、ここで重なりが解消されるので影響を受けない
    /// （README §5 S8）。
    /// </para>
    ///
    /// <para>
    /// 座標はピクセル単位・y 上向きで、行 r の中心は y = r + 0.5、列 c の中心は x = c + 0.5。出力の行 0 が下端
    /// （Unity のテクスチャと同じ並び）。1 インスタンスは 1 スレッドで使い回す（作業領域を持つ）。
    /// </para>
    /// </summary>
    public sealed class ScanlineRasterizer
    {
        private readonly List<Crossing>[] _rows;

        public ScanlineRasterizer(int maxHeight)
        {
            if (maxHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHeight));
            }
            _rows = new List<Crossing>[maxHeight];
            for (var i = 0; i < maxHeight; i++)
            {
                _rows[i] = new List<Crossing>();
            }
        }

        private readonly struct Crossing : IComparable<Crossing>
        {
            public Crossing(double x, int winding)
            {
                X = x;
                Winding = winding;
            }

            public double X { get; }
            public int Winding { get; }

            public int CompareTo(Crossing other)
            {
                return X.CompareTo(other.X);
            }
        }

        /// <summary>
        /// <paramref name="edges"/> を幅 <paramref name="width"/>・高さ <paramref name="height"/> の格子へ塗り、
        /// 内側の画素を true にして <paramref name="inside"/>（長さ width × height・行優先・行 0 が下端）へ書く。
        /// </summary>
        public void Fill(IReadOnlyList<OutlineEdge> edges, int width, int height, bool[] inside)
        {
            if (height > _rows.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(height), $"作業領域（{_rows.Length} 行）より高い格子です。");
            }
            if (inside.Length < width * height)
            {
                throw new ArgumentException("出力の配列が格子より小さいです。", nameof(inside));
            }
            Array.Clear(inside, 0, width * height);
            for (var r = 0; r < height; r++)
            {
                _rows[r].Clear();
            }

            foreach (var e in edges)
            {
                if (e.Y0 == e.Y1)
                {
                    continue;  // 水平な辺は走査線と交わらない（端点は隣の辺が受け持つ）
                }

                var winding = e.Y1 > e.Y0 ? 1 : -1;
                var yLow = Math.Min(e.Y0, e.Y1);
                var yHigh = Math.Max(e.Y0, e.Y1);

                // 半開区間 [yLow, yHigh) に中心が入る行だけを数える（頂点を 2 つの辺で二重に数えない）。
                var rStart = Math.Max(0, (int)Math.Ceiling(yLow - 0.5));
                var rEnd = Math.Min(height - 1, (int)Math.Ceiling(yHigh - 0.5) - 1);
                if (rStart > rEnd)
                {
                    continue;
                }

                var slope = (e.X1 - e.X0) / (e.Y1 - e.Y0);
                for (var r = rStart; r <= rEnd; r++)
                {
                    var yc = r + 0.5;
                    _rows[r].Add(new Crossing(e.X0 + ((yc - e.Y0) * slope), winding));
                }
            }

            for (var r = 0; r < height; r++)
            {
                var row = _rows[r];
                if (row.Count < 2)
                {
                    continue;
                }
                row.Sort();

                var sum = 0;
                var rowBase = r * width;
                for (var i = 0; i < row.Count - 1; i++)
                {
                    sum += row[i].Winding;
                    if (sum == 0)
                    {
                        continue;
                    }

                    // 中心 x = c + 0.5 が [x_i, x_{i+1}) に入る列を塗る。
                    var cStart = Math.Max(0, (int)Math.Ceiling(row[i].X - 0.5));
                    var cEnd = Math.Min(width - 1, (int)Math.Ceiling(row[i + 1].X - 0.5) - 1);
                    for (var c = cStart; c <= cEnd; c++)
                    {
                        inside[rowBase + c] = true;
                    }
                }
            }
        }
    }
}
