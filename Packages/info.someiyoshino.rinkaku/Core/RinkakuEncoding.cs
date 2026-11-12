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

        /// <summary>
        /// ナローインデックス（8bit）で持てる項目の数。項目番号 1〜251 がセグメント、0 が空き、252〜255 がレコードの印（README §3.3）。
        /// </summary>
        public const int NarrowMaxEntries = 252;

        /// <summary>ナローインデックスの番号の最大の値（レコードの印の先頭・<see cref="RecordMarker"/>）。</summary>
        public const int NarrowTop = 255;

        /// <summary>ワイドインデックス（16bit）の番号の最大の値。</summary>
        public const int WideTop = 0xFFFF;

        /// <summary>ワイドインデックスで持てる項目の数（項目番号 1〜65531）。</summary>
        public const int WideMaxEntries = 65532;

        /// <summary>
        /// レコードの印の数（番号の最大の値から下へ 4 つ）。印は「短いレコードか長いレコードか」と、レコードの位置の上の桁（0 か 1）を持つ。
        /// </summary>
        public const int RecordMarkers = 4;

        /// <summary>ナローインデックスのレコードの並びの大きさの上限（texel）: 後ろ向きの差を 8bit＋印の 1bit で持つ。</summary>
        public const int NarrowMaxRecordTexels = 511;

        /// <summary>ワイドインデックスのレコードの並びの大きさの上限（texel）: 後ろ向きの差を 16bit＋印の 1bit で持つ。</summary>
        public const int WideMaxRecordTexels = 131071;

        /// <summary>
        /// レコードを指すセルの 2 つ目の番号（印）。<paramref name="list"/> は長いレコード（5 本以上）か、<paramref name="high"/> はレコードの位置
        /// （セグメントテーブルの先頭から後ろ向きの texel の差）の上の桁（0 か 1。下の桁はセルの 1 つ目の番号）。
        /// </summary>
        public static int RecordMarker(bool wide, bool list, int high)
        {
            return (wide ? WideTop : NarrowTop) - (high * 2) - (list ? 1 : 0);
        }

        /// <summary>セルの 2 つ目の番号がレコードの印か。印なら、長いレコードかとレコードの位置の上の桁を返す。</summary>
        public static bool IsRecordMarker(int second, bool wide, out bool list, out int high)
        {
            var below = (wide ? WideTop : NarrowTop) - second;
            list = (below & 1) != 0;
            high = below >> 1;
            return below < RecordMarkers;
        }

        /// <summary>レコードの位置の下の桁の幅（bit）。</summary>
        public static int RecordLowBits(bool wide)
        {
            return wide ? 16 : 8;
        }

        /// <summary>セルにじかに持つセグメントの数（これを超えるセルはレコードを指す）。</summary>
        public const int ItemsInCell = 2;

        /// <summary>短いレコードが持つセグメントの数の上限（これを超えるセルは長いレコード）。</summary>
        public const int ItemsInRecord = 4;

        /// <summary>
        /// グリッドの texel のずれ <c>K</c> を UV0.x に詰めるときの足し値と、x・y のビット数（x 13bit・y 11bit＝24bit・README §3.4）。
        /// </summary>
        public const int OffsetBias = 64;

        /// <inheritdoc cref="OffsetBias"/>
        public const int OffsetXBits = 13;

        /// <inheritdoc cref="OffsetBias"/>
        public const int OffsetYBits = 11;

        /// <summary>
        /// セル 1 つのセグメントの数（1 画素の評価本数）の上限。シェーダのループの上限と同じ値で、超えるセルを作る字はベイクの失敗にする
        /// （黙ってセグメントを落とさない・README §3.3）。
        /// </summary>
        public const int MaxItemsPerCell = 32;

        /// <summary>
        /// インデックスブロックの左端の texel が持つ最初のセル（グリッドの x）。ナローインデックスは texel 1 つにセル 2 つ（偶数のセルが前半）なので、グリッドの左端を
        /// 偶数へ切り下げる。ワイドインデックスは texel 1 つにセル 1 つ。
        /// </summary>
        public static int BlockOriginCell(in RinkakuGlyphCell cell)
        {
            return cell.wide ? cell.gridX : cell.gridX & ~1;
        }

        /// <summary>インデックスブロックの幅（texel）。</summary>
        public static int BlockWidth(in RinkakuGlyphCell cell)
        {
            return cell.wide ? cell.gridWidth : ((cell.gridX + cell.gridWidth - BlockOriginCell(cell)) + 1) >> 1;
        }

        /// <summary>グリッドセル x（<c>floor(em × indexEm)</c>）を持つ texel の、<see cref="BlockOriginCell"/> の texel からの列。</summary>
        public static int TexelColumn(int cellX, bool wide)
        {
            return wide ? cellX : cellX >> 1;
        }

        /// <summary>
        /// セルのインデックスのずれ <c>K</c>（ブロックの原点 − グリッドの原点の texel の列）を UV0.x に詰めた値（README §3.4）。範囲の外なら −1。
        /// </summary>
        public static int PackedOffset(in RinkakuGlyphCell cell)
        {
            var x = cell.blockX - TexelColumn(BlockOriginCell(cell), cell.wide) + OffsetBias;
            var y = cell.blockY - cell.gridY + OffsetBias;
            if (x < 0 || x >= (1 << OffsetXBits) || y < 0 || y >= (1 << OffsetYBits))
            {
                return -1;
            }
            return x | (y << OffsetXBits);
        }

        /// <summary>セグメントテーブルの番地を UV0.y に詰めた値（ナローインデックスは正・ワイドインデックスは負・README §3.4）。セグメントテーブルはインデックスブロックより後ろの行なので 0 にならない。</summary>
        public static int PackedDataBase(in RinkakuGlyphCell cell)
        {
            return cell.wide ? -cell.dataBase : cell.dataBase;
        }

        /// <summary>サポートする最小の表示サイズ（px）。幾何の外延に足す AA の半分（0.5 表示 px）がこれで決まる（README §2）。</summary>
        public const double MinDisplayPx = 14.0;

        /// <summary>
        /// 幾何の外延（em）: インク境界から、全効果の最大の外延（<see cref="GlyphEdgeStyle.MaxOuterExtentEm"/>）＋最小の表示サイズの AA の半分だけ
        /// 広げる。quad とグリッドはこの範囲を覆う（README §3.3）。
        /// </summary>
        public static double ExtentEm => GlyphEdgeStyle.MaxOuterExtentEm + (0.5 / MinDisplayPx);
    }
}
