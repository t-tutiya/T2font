#nullable enable

using System;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 字形 1 つの字の表の記録（README §3.3）。単位は em で、ペン原点とベースラインを原点とし y は上向き。
    ///
    /// <para>
    /// ink* は<b>墨の外接そのもの</b>で、表現が描くための余白（距離域など）を含まない——余白は表現の持ち物で、字の表に
    /// 入れると表現を替えたときに値が変わる（README §2.3）。墨の無い字（空白）は ink の幅と高さが 0 で、送り幅だけを持つ。
    /// </para>
    /// </summary>
    /// <remarks>Unity が直列化する（<c>T2FontAsset</c> が配列で持つ）ので、公開フィールドの構造体にしている。</remarks>
    [Serializable]
    public struct GlyphMetrics
    {
        /// <summary>送り幅（em）。</summary>
        public float advance;

        /// <summary>墨の外接の左端（em・ペン原点から）。</summary>
        public float inkLeft;

        /// <summary>墨の外接の下端（em・ベースラインから・上が正）。</summary>
        public float inkBottom;

        /// <summary>墨の外接の右端（em）。</summary>
        public float inkRight;

        /// <summary>墨の外接の上端（em）。</summary>
        public float inkTop;

        public GlyphMetrics(float advance, float inkLeft, float inkBottom, float inkRight, float inkTop)
        {
            this.advance = advance;
            this.inkLeft = inkLeft;
            this.inkBottom = inkBottom;
            this.inkRight = inkRight;
            this.inkTop = inkTop;
        }

        /// <summary>墨がある（描く幾何を持つ）か。</summary>
        public bool HasInk => inkRight > inkLeft && inkTop > inkBottom;
    }
}
