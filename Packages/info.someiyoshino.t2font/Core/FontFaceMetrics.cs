#nullable enable

using System;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// フォント単位の値（README §3.1）。単位は em（字の大きさに対する比）で、ベースラインを原点とし y は上向き。
    /// 字の表の一部で、表現によらず同じ値になる（README §2.3）。
    /// </summary>
    /// <remarks>Unity が直列化する（<c>T2FontAsset</c> が持つ）ので、公開フィールドの構造体にしている。</remarks>
    [Serializable]
    public struct FontFaceMetrics
    {
        /// <summary>アセンダ（em・元フォントの <c>hhea</c>）。</summary>
        public float ascender;

        /// <summary>ディセンダ（em・下が負）。</summary>
        public float descender;

        /// <summary>行間の追加分（em）。</summary>
        public float lineGap;

        public FontFaceMetrics(float ascender, float descender, float lineGap)
        {
            this.ascender = ascender;
            this.descender = descender;
            this.lineGap = lineGap;
        }
    }
}
