#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Bake;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>
    /// Rinkaku のグリフ表現の種類（<c>"kind": "rinkaku"</c>・README §5）。符号付き距離を SDF と同じ意味で持つので、エッジスタイルの効果を全部描ける
    /// （README §2）。
    /// </summary>
    public sealed class RinkakuRepresentationKind : GlyphRepresentationKind
    {
        /// <summary>唯一の実体。</summary>
        public static readonly RinkakuRepresentationKind Instance = new RinkakuRepresentationKind();

        /// <summary><c>indexEm</c> の範囲。</summary>
        public const int MinIndexEm = 8;

        /// <inheritdoc cref="MinIndexEm"/>
        public const int MaxIndexEm = 32;

        /// <summary><c>maxPageSize</c> の下限（上限は <see cref="RinkakuEncoding.MaxPageHeight"/>）。</summary>
        public const int MinPageSize = 64;

        private RinkakuRepresentationKind()
        {
        }

        public override string Name => RinkakuEncoding.KindName;

        public override GlyphEffects Effects => GlyphEffects.All;

        public override int Version => RinkakuAtlasBaker.Version;

        public override GlyphRepresentationSettings ParseSettings(SpecSection section)
        {
            section.RequireKnownKeys("indexEm", "maxPageSize");

            var indexEm = section.OptionalNumber("indexEm") ?? 16.0;
            if (indexEm != Math.Floor(indexEm) || indexEm < MinIndexEm || indexEm > MaxIndexEm)
            {
                throw section.Fail("indexEm", FormattableString.Invariant($"indexEm は {MinIndexEm}〜{MaxIndexEm} の整数にしてください: {indexEm}"));
            }

            var maxPageSize = section.OptionalNumber("maxPageSize") ?? RinkakuEncoding.MaxPageHeight;
            if (maxPageSize != Math.Floor(maxPageSize) || maxPageSize < MinPageSize || maxPageSize > RinkakuEncoding.MaxPageHeight)
            {
                throw section.Fail("maxPageSize", FormattableString.Invariant(
                    $"maxPageSize は {MinPageSize}〜{RinkakuEncoding.MaxPageHeight} の整数にしてください（ページの高さの上限・幅は常に {RinkakuEncoding.PageWidth}）: {maxPageSize}"));
            }

            return new RinkakuSpecSettings((int)indexEm, (int)maxPageSize);
        }
    }

    /// <summary>ベイク仕様の Rinkaku の節（README §5 の表）。</summary>
    public sealed class RinkakuSpecSettings : GlyphRepresentationSettings
    {
        public RinkakuSpecSettings(int indexEm, int maxPageSize)
        {
            IndexEm = indexEm;
            MaxPageSize = maxPageSize;
        }

        public override GlyphRepresentationKind Kind => RinkakuRepresentationKind.Instance;

        /// <summary>グリッドの 1em のセルの数。</summary>
        public int IndexEm { get; }

        /// <summary>1 ページの最大の高さ（texel）。</summary>
        public int MaxPageSize { get; }

        /// <summary>ベイカーの設定。</summary>
        public RinkakuBakeSettings ToBakeSettings()
        {
            return new RinkakuBakeSettings { IndexEm = IndexEm, MaxPageHeight = MaxPageSize };
        }

        public override IEnumerable<KeyValuePair<string, string>> ToJsonMembers()
        {
            yield return new KeyValuePair<string, string>("indexEm", IndexEm.ToString(CultureInfo.InvariantCulture));
            yield return new KeyValuePair<string, string>("maxPageSize", MaxPageSize.ToString(CultureInfo.InvariantCulture));
        }
    }
}
