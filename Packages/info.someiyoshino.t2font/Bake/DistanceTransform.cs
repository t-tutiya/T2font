#nullable enable

using System;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// 厳密ユークリッド距離変換（Felzenszwalb &amp; Huttenlocher の放物線の下包絡・O(画素数)）。
    /// 2 値の格子の各画素について、「目標」の画素の中心までの最短距離（画素単位）を求める。
    ///
    /// <para>
    /// 打ち切りの近似（窓の中だけ探す）ではなく厳密に解くのは、距離レンジを広げても計算量が変わらず、
    /// 距離レンジごとに正しさを検証し直す必要が無いため。1 インスタンスは 1 スレッドで使い回す（作業領域を持つ）。
    /// </para>
    /// </summary>
    public sealed class DistanceTransform
    {
        // 目標の無い画素の初期値。倍精度なので、目標が 1 つも無い行どうしの差を取っても情報が落ちない桁に置く。
        private const double Infinity = 1e20;

        private readonly double[] _grid;
        private readonly double[] _f;
        private readonly double[] _d;
        private readonly double[] _z;
        private readonly int[] _v;

        /// <param name="maxWidth">扱う格子の最大幅。</param>
        /// <param name="maxHeight">扱う格子の最大高さ。</param>
        public DistanceTransform(int maxWidth, int maxHeight)
        {
            if (maxWidth <= 0 || maxHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWidth));
            }
            var n = Math.Max(maxWidth, maxHeight);
            _grid = new double[maxWidth * maxHeight];
            _f = new double[n];
            _d = new double[n];
            _z = new double[n + 1];
            _v = new int[n];
        }

        /// <summary>
        /// <paramref name="target"/>[i] == <paramref name="targetValue"/> の画素を目標として、各画素から最も近い目標の
        /// 画素の中心までの距離を <paramref name="distance"/> へ書く（目標そのものは 0）。目標が 1 つも無ければ
        /// 全画素が非常に大きな値になる。
        /// </summary>
        public void Compute(bool[] target, bool targetValue, int width, int height, double[] distance)
        {
            if (width * height > _grid.Length || Math.Max(width, height) > _f.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "作業領域より大きい格子です。");
            }

            for (var i = 0; i < width * height; i++)
            {
                _grid[i] = target[i] == targetValue ? 0 : Infinity;
            }

            // 列ごと（縦方向）。
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    _f[y] = _grid[(y * width) + x];
                }
                Transform1D(height);
                for (var y = 0; y < height; y++)
                {
                    _grid[(y * width) + x] = _d[y];
                }
            }

            // 行ごと（横方向）。
            for (var y = 0; y < height; y++)
            {
                var rowBase = y * width;
                for (var x = 0; x < width; x++)
                {
                    _f[x] = _grid[rowBase + x];
                }
                Transform1D(width);
                for (var x = 0; x < width; x++)
                {
                    distance[rowBase + x] = Math.Sqrt(_d[x]);
                }
            }
        }

        /// <summary>1 次元の二乗距離変換（_f → _d）。</summary>
        private void Transform1D(int n)
        {
            var k = 0;
            _v[0] = 0;
            _z[0] = double.NegativeInfinity;
            _z[1] = double.PositiveInfinity;
            for (var q = 1; q < n; q++)
            {
                var s = Intersection(q, _v[k]);
                while (s <= _z[k])
                {
                    k--;
                    s = Intersection(q, _v[k]);
                }
                k++;
                _v[k] = q;
                _z[k] = s;
                _z[k + 1] = double.PositiveInfinity;
            }

            k = 0;
            for (var q = 0; q < n; q++)
            {
                while (_z[k + 1] < q)
                {
                    k++;
                }
                var dq = q - _v[k];
                _d[q] = (dq * (double)dq) + _f[_v[k]];
            }
        }

        /// <summary>
        /// <see cref="Compute"/> と同じ距離を、<paramref name="rows"/> と <paramref name="columns"/> の交点でだけ求める
        /// （結果は行優先で <c>distance[行の添字 × columnCount + 列の添字]</c>）。行と列は昇順。
        ///
        /// <para>
        /// SDF の生成はスーパーサンプリングの格子のうち texel の中心を挟むサンプルしか使わないので、全画素を解くと大半が捨てられる。
        /// 2 値の格子では縦方向の段は「列の中で最寄りの目標までの距離」になり、放物線の包絡が要らない——行の順に
        /// 上向きと下向きの 2 回走査して、選んだ行でだけ値を残す（メモリを行の順に読む）。横方向の段は選んだ行だけを
        /// 包絡で解き、選んだ列でだけ平方根を取る。値は <see cref="Compute"/> と同じ（どちらも厳密）。
        /// </para>
        /// </summary>
        public void ComputeAt(
            bool[] target, bool targetValue, int width, int height,
            int[] rows, int rowCount, int[] columns, int columnCount, double[] distance)
        {
            if (width * height > _grid.Length || Math.Max(width, height) > _f.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "作業領域より大きい格子です。");
            }
            if (rowCount * width > _grid.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rowCount), "作業領域より多い行です。");
            }

            // 縦方向: 各列で最寄りの目標までの距離の 2 乗を、選んだ行でだけ _grid[行の添字 × width + x] に残す。
            var last = _v;
            for (var x = 0; x < width; x++)
            {
                last[x] = int.MinValue;
            }
            var next = 0;
            for (var y = 0; y < height && next < rowCount; y++)
            {
                var rowBase = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (target[rowBase + x] == targetValue)
                    {
                        last[x] = y;
                    }
                }
                if (rows[next] == y)
                {
                    var outBase = next * width;
                    for (var x = 0; x < width; x++)
                    {
                        _grid[outBase + x] = last[x] == int.MinValue ? Infinity : Square(y - last[x]);
                    }
                    next++;
                }
            }
            for (var x = 0; x < width; x++)
            {
                last[x] = int.MaxValue;
            }
            next = rowCount - 1;
            for (var y = height - 1; y >= 0 && next >= 0; y--)
            {
                var rowBase = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (target[rowBase + x] == targetValue)
                    {
                        last[x] = y;
                    }
                }
                if (rows[next] == y)
                {
                    var outBase = next * width;
                    for (var x = 0; x < width; x++)
                    {
                        if (last[x] != int.MaxValue)
                        {
                            _grid[outBase + x] = Math.Min(_grid[outBase + x], Square(last[x] - y));
                        }
                    }
                    next--;
                }
            }

            // 横方向: 選んだ行だけを包絡で解き、選んだ列でだけ平方根を取る。
            for (var r = 0; r < rowCount; r++)
            {
                var inBase = r * width;
                for (var x = 0; x < width; x++)
                {
                    _f[x] = _grid[inBase + x];
                }
                Transform1D(width);
                var outBase = r * columnCount;
                for (var c = 0; c < columnCount; c++)
                {
                    distance[outBase + c] = Math.Sqrt(_d[columns[c]]);
                }
            }
        }

        private static double Square(int value)
        {
            return value * (double)value;
        }

        // q と p を頂点とする放物線の交点の横座標。
        private double Intersection(int q, int p)
        {
            return ((_f[q] + ((double)q * q)) - (_f[p] + ((double)p * p))) / ((2.0 * q) - (2.0 * p));
        }
    }
}
