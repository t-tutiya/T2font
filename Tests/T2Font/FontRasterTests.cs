#nullable enable

using System;
using System.Collections.Generic;
using info.someiyoshino.T2Font.Bake;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>ラスタライザ（非ゼロ巻き数・パッケージの README §5 S8）と厳密距離変換。</summary>
    [TestFixture]
    public class FontRasterTests
    {
        private static void AddRect(List<OutlineEdge> edges, double x0, double y0, double x1, double y1, bool counterClockwise)
        {
            if (counterClockwise)
            {
                edges.Add(new OutlineEdge(x0, y0, x1, y0));
                edges.Add(new OutlineEdge(x1, y0, x1, y1));
                edges.Add(new OutlineEdge(x1, y1, x0, y1));
                edges.Add(new OutlineEdge(x0, y1, x0, y0));
            }
            else
            {
                edges.Add(new OutlineEdge(x0, y0, x0, y1));
                edges.Add(new OutlineEdge(x0, y1, x1, y1));
                edges.Add(new OutlineEdge(x1, y1, x1, y0));
                edges.Add(new OutlineEdge(x1, y0, x0, y0));
            }
        }

        private static bool[] Fill(List<OutlineEdge> edges, int width, int height)
        {
            var inside = new bool[width * height];
            new ScanlineRasterizer(height).Fill(edges, width, height, inside);
            return inside;
        }

        [Test]
        public void 長方形は中心が内側の画素だけを塗る()
        {
            var edges = new List<OutlineEdge>();
            AddRect(edges, 1, 1, 5, 4, counterClockwise: true);
            var inside = Fill(edges, 8, 6);

            // 列の中心 1.5〜4.5（4 列）× 行の中心 1.5〜3.5（3 行）。
            for (var y = 0; y < 6; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var expected = x >= 1 && x <= 4 && y >= 1 && y <= 3;
                    Assert.That(inside[(y * 8) + x], Is.EqualTo(expected), $"({x},{y})");
                }
            }
        }

        [Test]
        public void 同じ向きの重なりは抜けない()
        {
            var edges = new List<OutlineEdge>();
            AddRect(edges, 1, 1, 6, 6, counterClockwise: true);
            AddRect(edges, 3, 3, 9, 9, counterClockwise: true);
            var inside = Fill(edges, 10, 10);

            Assert.That(inside[(4 * 10) + 4], Is.True, "重なりの中（偶奇規則なら抜ける所）");
            Assert.That(inside[(2 * 10) + 2], Is.True);
            Assert.That(inside[(7 * 10) + 7], Is.True);
            Assert.That(inside[(8 * 10) + 1], Is.False);
        }

        [Test]
        public void 逆向きの内側の輪郭は穴になる()
        {
            var edges = new List<OutlineEdge>();
            AddRect(edges, 1, 1, 9, 9, counterClockwise: true);
            AddRect(edges, 3, 3, 7, 7, counterClockwise: false);
            var inside = Fill(edges, 10, 10);

            Assert.That(inside[(5 * 10) + 5], Is.False, "穴");
            Assert.That(inside[(2 * 10) + 2], Is.True);
            Assert.That(inside[(8 * 10) + 5], Is.True);
        }

        [Test]
        public void 距離変換は総当たりと一致する()
        {
            const int Width = 23;
            const int Height = 17;
            var target = new bool[Width * Height];
            // 乱数の代わりの決定的な散布（線形合同法）。
            uint state = 12345;
            for (var i = 0; i < target.Length; i++)
            {
                state = (state * 1103515245) + 12345;
                target[i] = (state >> 16) % 11 == 0;
            }

            var distance = new double[Width * Height];
            new DistanceTransform(Width, Height).Compute(target, true, Width, Height, distance);

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var best = double.MaxValue;
                    for (var ty = 0; ty < Height; ty++)
                    {
                        for (var tx = 0; tx < Width; tx++)
                        {
                            if (target[(ty * Width) + tx])
                            {
                                best = Math.Min(best, Math.Sqrt(((x - tx) * (x - tx)) + ((y - ty) * (y - ty))));
                            }
                        }
                    }
                    Assert.That(distance[(y * Width) + x], Is.EqualTo(best).Within(1e-9), $"({x},{y})");
                }
            }
        }

        [TestCase(true, 3u)]
        [TestCase(false, 3u)]
        [TestCase(true, 77u)]
        [TestCase(true, 0u)]
        public void 選んだ行と列の交点での距離は全体を解いたものと同じ(bool targetValue, uint seed)
        {
            const int Width = 29;
            const int Height = 21;
            var target = new bool[Width * Height];
            var state = seed;
            for (var i = 0; i < target.Length; i++)
            {
                state = (state * 1103515245) + 12345;
                target[i] = seed != 0 && (state >> 16) % 7 == 0;
            }

            var full = new double[Width * Height];
            var transform = new DistanceTransform(Width, Height);
            transform.Compute(target, targetValue, Width, Height, full);

            var rows = new[] { 0, 3, 4, 11, 12, 20 };
            var columns = new[] { 1, 2, 9, 10, 27, 28 };
            var sampled = new double[rows.Length * columns.Length];
            transform.ComputeAt(target, targetValue, Width, Height, rows, rows.Length, columns, columns.Length, sampled);

            for (var r = 0; r < rows.Length; r++)
            {
                for (var c = 0; c < columns.Length; c++)
                {
                    var expected = full[(rows[r] * Width) + columns[c]];
                    var actual = sampled[(r * columns.Length) + c];
                    if (expected > 1e9)
                    {
                        Assert.That(actual, Is.GreaterThan(1e9), $"({columns[c]},{rows[r]}) 目標が無い");
                    }
                    else
                    {
                        Assert.That(actual, Is.EqualTo(expected).Within(1e-9), $"({columns[c]},{rows[r]})");
                    }
                }
            }
        }

        [Test]
        public void 距離変換は目標の値を選べる()
        {
            // 中央の 1 画素だけ false。false を目標にすると、その画素からの距離になる。
            var target = new bool[5 * 5];
            Array.Fill(target, true);
            target[(2 * 5) + 2] = false;
            var distance = new double[25];
            new DistanceTransform(5, 5).Compute(target, false, 5, 5, distance);

            Assert.That(distance[(2 * 5) + 2], Is.EqualTo(0));
            Assert.That(distance[(0 * 5) + 0], Is.EqualTo(Math.Sqrt(8)).Within(1e-9));
            Assert.That(distance[(2 * 5) + 4], Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void 距離変換は目標が無ければ非常に大きい値になる()
        {
            var target = new bool[4 * 3];
            var distance = new double[12];
            new DistanceTransform(4, 3).Compute(target, true, 4, 3, distance);

            Assert.That(distance, Has.All.GreaterThan(1e6));
        }
    }
}
