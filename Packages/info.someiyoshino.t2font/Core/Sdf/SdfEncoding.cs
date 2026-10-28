#nullable enable

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 同梱の SDF の表現の、全フォントに共通の定数（README §9）。生成器とランタイムとシェーダが同じ値を使う。
    /// </summary>
    public static class SdfEncoding
    {
        /// <summary>
        /// SDF の片側距離域（em）。符号化は <c>d = 0.5 + 距離 / (2 × RangeEm × emPx)</c>（墨の内側が正）で、距離域の外は 0 か 1 に
        /// 飽和する。
        ///
        /// <para>
        /// <b>全フォントで 1 つの値</b>にし、生成仕様では選べない。様式の値（縁取り・ずらし・ぼかし）は em で書き、それが使える
        /// 外延の上限はこの値で決まる。フォントごとに距離域が違うと、同じ様式でもフォントによって縁が描けたり切れたりする。
        /// 0.1875 は様式の外延（<see cref="GlyphEdgeStyle.MaxOuterExtentEm"/>＝0.15em）に表示 14px の AA の半分を足しても収まる値
        /// （README §6.2）。変えると全フォントの作り直しが要る。
        /// </para>
        /// </summary>
        public const float RangeEm = 0.1875f;
    }
}
