#nullable enable

using System;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// SDF の表現の字形 1 つのセル（README §9）。字形番号で引く。
    ///
    /// <para>
    /// <b>単位</b>: plane* は <b>em</b>（ペン原点・ベースライン基準・y 上向き）、atlas* は<b>ページの px</b>（左下原点＝Unity の
    /// テクスチャの行の並びと同じ）。plane と atlas は同じ矩形を 2 つの座標系で書いたもので、<c>atlas = 原点 + plane × emPx</c> が
    /// 厳密に成り立つ（生成器が保証する）。plane は墨の外接に距離域を足した矩形で、描画はこれを quad にし、atlas から UV を作る
    /// ——配置の方式（一様格子か詰めか）を知らない。
    /// </para>
    /// </summary>
    /// <remarks>Unity が直列化する（SDF の表現が配列で持つ）ので、公開フィールドの構造体にしている。墨の無い字形は全部 0。</remarks>
    [Serializable]
    public struct SdfGlyphCell
    {
        /// <summary>アトラスのページ番号（0 始まり）。</summary>
        public int page;

        /// <summary>ページ上の矩形の左端（px）。</summary>
        public int atlasLeft;

        /// <summary>ページ上の矩形の下端（px・左下原点）。</summary>
        public int atlasBottom;

        /// <summary>ページ上の矩形の右端（px・この列を含まない）。</summary>
        public int atlasRight;

        /// <summary>ページ上の矩形の上端（px・この行を含まない）。</summary>
        public int atlasTop;

        /// <summary>quad の左端（em・ペン原点から）。</summary>
        public float planeLeft;

        /// <summary>quad の下端（em・ベースラインから・上が正）。</summary>
        public float planeBottom;

        /// <summary>quad の右端（em）。</summary>
        public float planeRight;

        /// <summary>quad の上端（em）。</summary>
        public float planeTop;

        /// <summary>墨が無い（quad を作らない）字形か。</summary>
        public bool IsEmpty => atlasRight <= atlasLeft || atlasTop <= atlasBottom;
    }
}
