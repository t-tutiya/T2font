#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>SDF のページのテクスチャ形式（README §9）。</summary>
    public enum SdfPageFormat
    {
        /// <summary>非圧縮の単チャネル 8bit（既定）。どのターゲットでも同じ。</summary>
        R8,

        /// <summary>単チャネルのブロック圧縮（R8 の半分）。デスクトップ向けのターゲットだけ。</summary>
        BC4,
    }

    /// <summary>同梱の SDF の表現の種類（<c>"kind": "sdf"</c>・README §9）。全効果を描ける。</summary>
    public sealed class SdfRepresentationKind : GlyphRepresentationKind
    {
        /// <summary>唯一の実体。</summary>
        public static readonly SdfRepresentationKind Instance = new SdfRepresentationKind();

        private SdfRepresentationKind()
        {
        }

        public override string Name => SdfEncoding.KindName;

        /// <summary>距離場から縁の 4 値を全部描ける（README §6.3）。外延は距離域（<see cref="SdfEncoding.RangeEm"/>）に収まる。</summary>
        public override GlyphEffects Effects => GlyphEffects.All;

        public override int Version => SdfAtlasBaker.Version;

        public override GlyphRepresentationSettings ParseSettings(SpecSection section)
        {
            section.RequireKnownKeys("emPx", "format", "maxPageSize", "cellBox");

            var emPx = section.OptionalNumber("emPx") ?? 48.0;
            if (!(emPx >= 8) || emPx > 256)
            {
                throw section.Fail(section.Json["emPx"], FormattableString.Invariant($"emPx は 8〜256 にしてください: {emPx}"));
            }

            var format = SdfPageFormat.R8;
            var formatText = section.OptionalString("format");
            if (formatText != null)
            {
                switch (formatText)
                {
                    case "R8":
                        format = SdfPageFormat.R8;
                        break;
                    case "BC4":
                        format = SdfPageFormat.BC4;
                        break;
                    default:
                        throw section.Fail(section.Json["format"], $"format は \"R8\" か \"BC4\" です: \"{formatText}\"");
                }
            }

            var maxPageSize = section.OptionalNumber("maxPageSize") ?? 8192.0;
            if (maxPageSize != Math.Floor(maxPageSize) || maxPageSize < 64 || maxPageSize > 16384)
            {
                throw section.Fail(section.Json["maxPageSize"], FormattableString.Invariant($"maxPageSize は 64〜16384 の整数にしてください: {maxPageSize}"));
            }

            return new SdfSpecSettings(emPx, format, (int)maxPageSize, section.OptionalBox("cellBox"));
        }
    }

    /// <summary>生成仕様の SDF の節（README §9 の表）。</summary>
    public sealed class SdfSpecSettings : GlyphRepresentationSettings
    {
        public SdfSpecSettings(double emPx, SdfPageFormat format, int maxPageSize, EmBox? cellBox)
        {
            EmPx = emPx;
            Format = format;
            MaxPageSize = maxPageSize;
            CellBox = cellBox;
        }

        public override GlyphRepresentationKind Kind => SdfRepresentationKind.Instance;

        /// <summary>アトラス上の 1em の px。</summary>
        public double EmPx { get; }

        /// <summary>ページのテクスチャ形式。</summary>
        public SdfPageFormat Format { get; }

        /// <summary>1 ページの最大の幅と高さ（px）。</summary>
        public int MaxPageSize { get; }

        /// <summary>セルの墨の箱（null なら依頼した字の墨の外接の和）。</summary>
        public EmBox? CellBox { get; }

        /// <summary>生成器の設定（距離域は全フォント共通の既定のまま）。</summary>
        public SdfBakeSettings ToBakeSettings()
        {
            return new SdfBakeSettings
            {
                EmPx = EmPx,
                CellBox = CellBox,
                MaxPageWidth = MaxPageSize,
                MaxPageHeight = MaxPageSize,
            };
        }

        public override IEnumerable<KeyValuePair<string, string>> ToJsonMembers()
        {
            yield return new KeyValuePair<string, string>("emPx", SpecSection.JsonNumber(EmPx));
            yield return new KeyValuePair<string, string>("format", SpecSection.JsonString(Format.ToString()));
            yield return new KeyValuePair<string, string>("maxPageSize", MaxPageSize.ToString(CultureInfo.InvariantCulture));
            if (CellBox is EmBox box)
            {
                yield return new KeyValuePair<string, string>(
                    "cellBox",
                    "[" + SpecSection.JsonNumber(box.Left) + ", " + SpecSection.JsonNumber(box.Bottom) + ", "
                    + SpecSection.JsonNumber(box.Right) + ", " + SpecSection.JsonNumber(box.Top) + "]");
            }
        }
    }
}
