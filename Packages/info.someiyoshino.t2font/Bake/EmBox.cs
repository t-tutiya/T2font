#nullable enable

using System;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// em の矩形（ペン原点とベースライン基準・y 上向き）。生成器が墨の外接と、セルが収める墨の箱に使う（倍精度のまま持つ
    /// ——字の表の <see cref="GlyphMetrics"/> は単精度に丸めた値で、焼きはこちらの値で行う）。
    /// </summary>
    public readonly struct EmBox
    {
        public EmBox(double left, double bottom, double right, double top)
        {
            if (!(right > left) || !(top > bottom))
            {
                throw new ArgumentException($"箱の大きさが 0 以下です（{left}, {bottom}, {right}, {top}）。");
            }
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public double Left { get; }
        public double Bottom { get; }
        public double Right { get; }
        public double Top { get; }

        public override string ToString()
        {
            return FormattableString.Invariant($"({Left:0.###}, {Bottom:0.###}, {Right:0.###}, {Top:0.###})");
        }
    }
}
