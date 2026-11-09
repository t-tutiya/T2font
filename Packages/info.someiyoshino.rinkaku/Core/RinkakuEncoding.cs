#nullable enable

using info.someiyoshino.T2Font;

namespace info.someiyoshino.Rinkaku
{
    /// <summary>
    /// Rinkaku のデータの形式の定数（README §3）。ベイカー・表現・シェーダが同じ値を使う。値を変えたら README §3 とシェーダを同じ変更で直し、
    /// 種類の記述子のバージョンを上げる（README §6）。
    /// </summary>
    public static class RinkakuEncoding
    {
        /// <summary>ベイク仕様の <c>representation.kind</c>。</summary>
        public const string KindName = "rinkaku";

        /// <summary>ページの幅（texel）。線形の番地を 2 次元へ直すのに使う固定の値（README §3.1）。</summary>
        public const int PageWidth = 4096;

        /// <summary>ページの高さの上限（texel）。線形の番地が 2^24 未満に収まり、float で正確に持てる。</summary>
        public const int MaxPageHeight = 4096;

        /// <summary>始点の座標の量子（1em あたり）と、負の座標を正にする足し値（em）。16bit で −1〜3em を持つ（README §3.2）。</summary>
        public const double CoordinateScale = 16384.0;

        /// <inheritdoc cref="CoordinateScale"/>
        public const double CoordinateOffset = 1.0;

        /// <summary>制御点の始点からの差の量子（1em あたり）。符号付き 12bit で ±0.5em を持つ（README §3.2）。</summary>
        public const double ControlScale = 4096.0;

        /// <summary>制御点の差の量子の値の範囲（符号付き 12bit）。</summary>
        public const int ControlMin = -2048;

        /// <inheritdoc cref="ControlMin"/>
        public const int ControlMax = 2047;

        /// <summary>擬似法線の角の段数（8bit・1 段 1.4°）。</summary>
        public const int AngleSteps = 256;

        /// <summary>狭い索引（8bit）で持てる項目の数。項目番号 1〜254 が区間、0 が空き、255 が溢れの印（README §3.3）。</summary>
        public const int NarrowMaxEntries = 255;

        /// <summary>狭い索引の溢れの印（texel の 4 番目のバイト）。</summary>
        public const int NarrowOverflowMarker = 255;

        /// <summary>狭い索引の溢れ表の大きさの上限（texel・番地を 8bit の後ろ向きの差で持つ）。</summary>
        public const int NarrowMaxOverflowTexels = 255;

        /// <summary>広い索引（16bit）の溢れの印。</summary>
        public const int WideOverflowMarker = 0xFFFF;

        /// <summary>広い索引で持てる項目の数（項目番号 1〜65534）。</summary>
        public const int WideMaxEntries = 65535;

        /// <summary>広い索引の溢れ表の大きさの上限（texel）。</summary>
        public const int WideMaxOverflowTexels = 65535;

        /// <summary>
        /// 索引格子の texel のずれ <c>K</c> を UV0.x に詰めるときの足し値と、x・y のビット数（x 13bit・y 11bit＝24bit・README §3.4）。
        /// </summary>
        public const int OffsetBias = 64;

        /// <inheritdoc cref="OffsetBias"/>
        public const int OffsetXBits = 13;

        /// <inheritdoc cref="OffsetBias"/>
        public const int OffsetYBits = 11;

        /// <summary>
        /// セル 1 つの区間の数（1 画素の評価本数）の上限。シェーダのループの上限と同じ値で、超えるセルを作る字はベイクの失敗にする
        /// （黙って区間を落とさない・README §3.3）。
        /// </summary>
        public const int MaxItemsPerCell = 32;

        /// <summary>
        /// セルの索引のずれ <c>K</c>（ブロックの原点 − 格子の原点 × texel の数）を UV0.x に詰めた値（README §3.4）。範囲の外なら −1。
        /// </summary>
        public static int PackedOffset(in RinkakuGlyphCell cell)
        {
            var texelsPerCell = cell.wide ? 2 : 1;
            var x = cell.blockX - (cell.gridX * texelsPerCell) + OffsetBias;
            var y = cell.blockY - cell.gridY + OffsetBias;
            if (x < 0 || x >= (1 << OffsetXBits) || y < 0 || y >= (1 << OffsetYBits))
            {
                return -1;
            }
            return x | (y << OffsetXBits);
        }

        /// <summary>区間の表の番地を UV0.y に詰めた値（狭い索引は正・広い索引は負・README §3.4）。区間の表は索引のブロックより後ろの行なので 0 にならない。</summary>
        public static int PackedDataBase(in RinkakuGlyphCell cell)
        {
            return cell.wide ? -cell.dataBase : cell.dataBase;
        }

        /// <summary>支える最小の表示サイズ（px）。幾何の外延に足す AA の半分（0.5 表示 px）がこれで決まる（README §2）。</summary>
        public const double MinDisplayPx = 14.0;

        /// <summary>
        /// 幾何の外延（em）: インク境界から、全効果の最大の外延（<see cref="GlyphEdgeStyle.MaxOuterExtentEm"/>）＋最小の表示サイズの AA の半分だけ
        /// 広げる。quad と索引格子はこの範囲を覆う（README §3.3）。
        /// </summary>
        public static double ExtentEm => GlyphEdgeStyle.MaxOuterExtentEm + (0.5 / MinDisplayPx);
    }
}
