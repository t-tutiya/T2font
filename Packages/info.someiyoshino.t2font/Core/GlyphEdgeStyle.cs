#nullable enable

using System;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 様式（縁）の値一式＝縁取りの色と、2 つの境界（面の縁・縁取りの外縁）それぞれの位置とぼかし（README §6.1）。
    /// 値は em で、シェーダの uniform に 1 対 1 で対応する。<b>どの値も境界 1 本の位置かランプ 1 本の幅だけを動かす</b>。
    /// AA はシェーダが常に画面の 1px で掛けるのでここには無い——ぼかしの 2 値は AA に足す幅である（README §5 S3）。
    ///
    /// <para>
    /// 値が等しい様式は同じマテリアルを共有できるので、値等価の構造体にしている（<c>GlyphMaterialCache</c> の鍵）。
    /// 面の色とフェードは頂点色で、ここには無い（README §5 S2）。
    /// </para>
    /// </summary>
    public readonly struct GlyphEdgeStyle : IEquatable<GlyphEdgeStyle>
    {
        // ── 範囲（em・README §6.1）──
        // 全キーを上限にした組の外延（MaxOuterExtentEm）に、表示 14px の AA の半分（0.5/14em）を足しても、同梱の SDF の
        // 距離域（0.1875em）に収まるように配分してある。表現は描けると宣言した効果をこの範囲の全部で描く（README §6.3）。

        /// <summary><see cref="FaceDilate"/> の下限。画の半幅（ゴシックの本文書体で約 0.035em）を削り切らない。</summary>
        public const float FaceDilateMin = -0.03f;

        /// <summary><see cref="FaceDilate"/> の上限。</summary>
        public const float FaceDilateMax = 0.04f;

        /// <summary><see cref="FaceSoftness"/> の上限（下限は 0）。</summary>
        public const float FaceSoftnessMax = 0.06f;

        /// <summary><see cref="OutlineWidth"/> の上限（下限は 0）。これを超えると画数の多い字の内側が縁で埋まり始める。</summary>
        public const float OutlineWidthMax = 0.08f;

        /// <summary><see cref="OutlineSoftness"/> の上限（下限は 0）。</summary>
        public const float OutlineSoftnessMax = 0.06f;

        /// <summary>
        /// 全キーを上限にした組の外延（em）＝墨の外接から外へ、効果が描かれうる最大の距離（README §6.2）。外縁のぼかしは
        /// 外縁を中心に置くので半分だけ外へ出る。表現の幾何はこれ＋1 表示 px を覆う（README §4.3）。
        /// </summary>
        public const float MaxOuterExtentEm = FaceDilateMax + OutlineWidthMax + (OutlineSoftnessMax / 2f);

        /// <summary>効果を使わない様式（縁取り無し・ずらし無し・ぼかし無し）。</summary>
        public static readonly GlyphEdgeStyle None = new GlyphEdgeStyle(0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f);

        /// <summary>縁取りの色（rgba・sRGB。α は縁取りの帯の不透明度）。</summary>
        public readonly float ColorR, ColorG, ColorB, ColorA;

        /// <summary>
        /// 面の縁のずらし（em・＋で太く）。面の縁と縁取りの外縁を<b>一緒に</b>動かすので、縁取りの幅は変わらない
        /// （<c>_FaceDilate</c>）。
        /// </summary>
        public readonly float FaceDilate;

        /// <summary>面の縁のぼかし（em）＝面と縁取りの色の遷移を AA に足す幅。50% の位置は面の縁のまま（<c>_FaceSoftness</c>）。</summary>
        public readonly float FaceSoftness;

        /// <summary>縁取りの幅（em）＝面の縁から外への帯の幅（<c>_OutlineWidth</c>）。</summary>
        public readonly float OutlineWidth;

        /// <summary>縁取りの外縁のぼかし（em）＝外縁の α のランプを AA に足す幅。50% の位置は外縁のまま（<c>_OutlineSoftness</c>）。</summary>
        public readonly float OutlineSoftness;

        public GlyphEdgeStyle(
            float colorR, float colorG, float colorB, float colorA,
            float faceDilate, float faceSoftness, float outlineWidth, float outlineSoftness)
        {
            ColorR = colorR;
            ColorG = colorG;
            ColorB = colorB;
            ColorA = colorA;
            FaceDilate = faceDilate;
            FaceSoftness = faceSoftness;
            OutlineWidth = outlineWidth;
            OutlineSoftness = outlineSoftness;
        }

        /// <summary>
        /// この様式が使う効果（値が 0 でないキー・README §6.3）。ホストは表現の能力（<c>GlyphRepresentation.Effects</c>）が
        /// これを含むことを、様式をそのフォントに使う前に確かめる。縁取りの色は幅が 0 なら使われない。
        /// </summary>
        public GlyphEffects Uses
        {
            get
            {
                var effects = GlyphEffects.None;
                if (FaceDilate != 0f)
                {
                    effects |= GlyphEffects.FaceDilate;
                }
                if (FaceSoftness != 0f)
                {
                    effects |= GlyphEffects.FaceSoftness;
                }
                if (OutlineWidth != 0f)
                {
                    effects |= GlyphEffects.Outline;
                }
                if (OutlineSoftness != 0f)
                {
                    effects |= GlyphEffects.OutlineSoftness;
                }
                return effects;
            }
        }

        /// <summary>
        /// 範囲（README §6.1）の外の値があれば、その最初の 1 つを「キー 値（範囲）」の文面で返す。全部が範囲の内なら null。
        /// 範囲の外の様式は、どの表現でも正しく描けるとは限らない（外延が表現の幾何を越える）。
        /// </summary>
        public string? FindOutOfRange()
        {
            if (!InRange(FaceDilate, FaceDilateMin, FaceDilateMax))
            {
                return Describe(nameof(FaceDilate), FaceDilate, FaceDilateMin, FaceDilateMax);
            }
            if (!InRange(FaceSoftness, 0f, FaceSoftnessMax))
            {
                return Describe(nameof(FaceSoftness), FaceSoftness, 0f, FaceSoftnessMax);
            }
            if (!InRange(OutlineWidth, 0f, OutlineWidthMax))
            {
                return Describe(nameof(OutlineWidth), OutlineWidth, 0f, OutlineWidthMax);
            }
            if (!InRange(OutlineSoftness, 0f, OutlineSoftnessMax))
            {
                return Describe(nameof(OutlineSoftness), OutlineSoftness, 0f, OutlineSoftnessMax);
            }
            for (var i = 0; i < 4; i++)
            {
                var channel = i == 0 ? ColorR : i == 1 ? ColorG : i == 2 ? ColorB : ColorA;
                if (!InRange(channel, 0f, 1f))
                {
                    return Describe("OutlineColor", channel, 0f, 1f);
                }
            }
            return null;
        }

        public bool Equals(GlyphEdgeStyle other)
        {
            return ColorR == other.ColorR && ColorG == other.ColorG
                && ColorB == other.ColorB && ColorA == other.ColorA
                && FaceDilate == other.FaceDilate && FaceSoftness == other.FaceSoftness
                && OutlineWidth == other.OutlineWidth && OutlineSoftness == other.OutlineSoftness;
        }

        public override bool Equals(object? obj)
        {
            return obj is GlyphEdgeStyle other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ColorR);
            hash.Add(ColorG);
            hash.Add(ColorB);
            hash.Add(ColorA);
            hash.Add(FaceDilate);
            hash.Add(FaceSoftness);
            hash.Add(OutlineWidth);
            hash.Add(OutlineSoftness);
            return hash.ToHashCode();
        }

        public static bool operator ==(GlyphEdgeStyle left, GlyphEdgeStyle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GlyphEdgeStyle left, GlyphEdgeStyle right)
        {
            return !left.Equals(right);
        }

        // NaN は範囲の外（比較がどれも偽になるので、両側の比較が真であることを求める）。
        private static bool InRange(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        private static string Describe(string key, float value, float min, float max)
        {
            return FormattableString.Invariant($"{key} {value}（{min}〜{max}）");
        }
    }
}
