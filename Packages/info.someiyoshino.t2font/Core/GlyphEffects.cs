#nullable enable

using System;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// エッジスタイルの効果の種類（README §6.3）。表現は描ける効果をこれで宣言し、ホストはエッジスタイルが使う効果
    /// （<see cref="GlyphEdgeStyle.Uses"/>）と照合する。
    /// </summary>
    [Flags]
    public enum GlyphEffects
    {
        /// <summary>効果を使わない（面を描くだけ）。</summary>
        None = 0,

        /// <summary>面の縁のずらし（<c>_FaceDilate</c>）。</summary>
        FaceDilate = 1 << 0,

        /// <summary>面の縁のぼかし（<c>_FaceSoftness</c>）。</summary>
        FaceSoftness = 1 << 1,

        /// <summary>縁取り（<c>_OutlineWidth</c> と <c>_OutlineColor</c>）。</summary>
        Outline = 1 << 2,

        /// <summary>外縁のぼかし（<c>_OutlineSoftness</c>）。</summary>
        OutlineSoftness = 1 << 3,

        /// <summary>全部。</summary>
        All = FaceDilate | FaceSoftness | Outline | OutlineSoftness,
    }
}
