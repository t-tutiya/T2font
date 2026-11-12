#nullable enable

using System;

namespace info.someiyoshino.Rinkaku
{
    /// <summary>
    /// Rinkaku のグリフ表現のグリフ 1 つのセル（README §3.4）。グリフインデックスで引く。
    ///
    /// <para>
    /// <b>グリッド</b>は em 座標を <c>1/indexEm</c> 角のグリッドに切ったもので、グリフのグリッドの左下のセルは <c>(gridX, gridY)</c>
    /// （<c>floor(em × indexEm)</c> の値）。ページの上のインデックスブロックの左下は <c>(blockX, blockY)</c>（texel・行 0 が下端）。ナローインデックスは texel 1 つに
    /// セル 2 つ（偶数のセルが前半）、ワイドインデックス（<see cref="wide"/>）は texel 1 つにセル 1 つを持つ（ブロックの幅は
    /// <see cref="RinkakuEncoding.BlockWidth"/>）。<b>セグメントテーブル</b>はページの線形の番地 <see cref="dataBase"/>（<c>y × 4096 + x</c>）から 1 項目 2 texel で
    /// 並び、レコードの並びはその直前に置く。plane* は quad（em・インク境界＋外延）。
    /// </para>
    /// </summary>
    /// <remarks>Unity が直列化する（グリフ表現が配列で持つ）ので、公開フィールドの構造体にしている。インクの無いグリフは全部 0。</remarks>
    [Serializable]
    public struct RinkakuGlyphCell
    {
        /// <summary>ページ番号（0 始まり）。</summary>
        public int page;

        /// <summary>グリッドの左下のセル（<c>floor(em × indexEm)</c>）。</summary>
        public int gridX;

        /// <inheritdoc cref="gridX"/>
        public int gridY;

        /// <summary>グリッドセルの数（横・縦）。0 ならインクが無い。</summary>
        public int gridWidth;

        /// <inheritdoc cref="gridWidth"/>
        public int gridHeight;

        /// <summary>ページの上のインデックスブロックの左下（texel）。</summary>
        public int blockX;

        /// <inheritdoc cref="blockX"/>
        public int blockY;

        /// <summary>セグメントテーブルの最初の項目の線形の番地（texel）。</summary>
        public int dataBase;

        /// <summary>ワイドインデックス（項目番号 16bit・texel 1 つにセル 1 つ）か。</summary>
        public bool wide;

        /// <summary>quad の左端（em・ペン原点から）。</summary>
        public float planeLeft;

        /// <summary>quad の下端（em・ベースラインから・上が正）。</summary>
        public float planeBottom;

        /// <summary>quad の右端（em）。</summary>
        public float planeRight;

        /// <summary>quad の上端（em）。</summary>
        public float planeTop;

        /// <summary>インクが無い（quad を作らない）グリフか。</summary>
        public bool IsEmpty => gridWidth <= 0 || gridHeight <= 0;
    }
}
