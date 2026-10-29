#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// タイポグラフィテーブルの生成結果（README §3・§7.3）。全表現が同じものを受け取り、表現はこれに描くためのデータを足す。
    ///
    /// <para>
    /// グリフインデックスの並びは <c>T2FontAsset</c> と同じ: <c>0 ≤ i &lt; Codepoints.Length</c> は字 <c>Codepoints[i]</c> のグリフ（昇順）、
    /// その後ろに .notdef（<see cref="Notdef"/>）。ベイカーはグリフインデックスごとに <see cref="Outline"/> と <see cref="InkBox"/> を読む。
    /// </para>
    /// </summary>
    public sealed class FontFaceData
    {
        private readonly GlyphOutline?[] _outlines;
        private readonly EmBox[] _inkBoxes;

        internal FontFaceData(
            FontFaceMetrics face, int[] codepoints, GlyphMetrics[] glyphs, GlyphOutline?[] outlines, EmBox[] inkBoxes,
            int unitsPerEm, IReadOnlyList<int> missing)
        {
            Face = face;
            Codepoints = codepoints;
            Glyphs = glyphs;
            _outlines = outlines;
            _inkBoxes = inkBoxes;
            UnitsPerEm = unitsPerEm;
            Missing = missing;
        }

        /// <summary>フォント単位の値（em）。</summary>
        public FontFaceMetrics Face { get; }

        /// <summary>収めた字（昇順）。添字がグリフインデックス。</summary>
        public int[] Codepoints { get; }

        /// <summary>グリフインデックス順のタイポグラフィテーブルの記録。末尾が .notdef。</summary>
        public GlyphMetrics[] Glyphs { get; }

        /// <summary>代替グリフ（元フォントの glyph 0＝.notdef）のグリフインデックス（＝<see cref="Codepoints"/> の長さ）。</summary>
        public int Notdef => Codepoints.Length;

        /// <summary>グリフの数（.notdef を含む）。</summary>
        public int GlyphCount => Glyphs.Length;

        /// <summary>元フォントの em の単位数（<see cref="Outline"/> の座標の単位）。</summary>
        public int UnitsPerEm { get; }

        /// <summary>依頼されたが元フォントにグリフが無かった字（昇順）。扱い（警告か失敗か）は呼び出し側が決める。</summary>
        public IReadOnlyList<int> Missing { get; }

        /// <summary>グリフインデックス <paramref name="glyph"/> の字（.notdef は <see cref="FontFaceBuilder.NotdefCodepoint"/>）。</summary>
        public int CodepointOf(int glyph)
        {
            return glyph < Codepoints.Length ? Codepoints[glyph] : FontFaceBuilder.NotdefCodepoint;
        }

        /// <summary>グリフの輪郭（フォント単位・y 上向き）。インクの無いグリフは null。</summary>
        public GlyphOutline? Outline(int glyph)
        {
            return _outlines[glyph];
        }

        /// <summary>グリフのインク境界（em・倍精度）。インクの無いグリフでは意味を持たない（<see cref="Outline"/> が null）。</summary>
        public EmBox InkBox(int glyph)
        {
            return _inkBoxes[glyph];
        }

        /// <summary>
        /// タイポグラフィテーブル（縦メトリクス・字・グリフごとの記録）の SHA-256（小文字の 16 進）。同じ入力から出るタイポグラフィテーブルが変わったことを検出し、
        /// バージョン（<see cref="FontFaceBuilder.Version"/>）の上げ忘れを止める検査が使う（README §7.5）。
        /// </summary>
        public string ComputeContentHash()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(Face.ascender);
                    writer.Write(Face.descender);
                    writer.Write(Face.lineGap);
                    writer.Write(UnitsPerEm);
                    writer.Write(Codepoints.Length);
                    foreach (var cp in Codepoints)
                    {
                        writer.Write(cp);
                    }
                    writer.Write(Glyphs.Length);
                    foreach (var g in Glyphs)
                    {
                        writer.Write(g.advance);
                        writer.Write(g.inkLeft);
                        writer.Write(g.inkBottom);
                        writer.Write(g.inkRight);
                        writer.Write(g.inkTop);
                    }
                }
                stream.Position = 0;
                using (var sha = SHA256.Create())
                {
                    return SdfAtlas.ToHex(sha.ComputeHash(stream));
                }
            }
        }
    }

    /// <summary>
    /// TrueType フォントからタイポグラフィテーブルを作る（README §7.3 の 3）。表現によらない手順で、送り幅とインク境界は表現を替えても同じになる
    /// （README §2.3）。UnityEngine を参照しない純 C# で、Unity のインポーターと外部 NUnit の両方から同じコードを呼ぶ。
    /// </summary>
    public static class FontFaceBuilder
    {
        /// <summary>
        /// タイポグラフィテーブルの生成のバージョン。<b>同じ入力から出るタイポグラフィテーブルが変わる変更をしたら必ず上げる</b>——インポーターのバージョンがこれから決まり、上げないと
        /// 古いインポートの結果が使われ続ける（README §7.5）。
        /// </summary>
        public const int Version = 1;

        /// <summary>.notdef のグリフが名乗る字（グリフインデックスから字を引くときの印）。</summary>
        public const int NotdefCodepoint = -1;

        /// <summary>
        /// <paramref name="font"/> から <paramref name="codepoints"/> のタイポグラフィテーブルを作る。フォントに無い字は
        /// <see cref="FontFaceData.Missing"/> へ載せて飛ばす。代替グリフ（glyph 0）は必ず足す——元フォントの .notdef が空なら枠を描く
        /// （収録されていない字を、黙って消さずに目に見える形で描くためのもの・README §3.1）。
        /// </summary>
        /// <exception cref="ArgumentException">Unicode のスカラー値でない字がある。</exception>
        public static FontFaceData Build(TrueTypeFont font, IEnumerable<int> codepoints)
        {
            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }
            if (codepoints == null)
            {
                throw new ArgumentNullException(nameof(codepoints));
            }

            var requested = new SortedSet<int>();
            foreach (var cp in codepoints)
            {
                if (cp < 0 || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
                {
                    throw new ArgumentException($"U+{cp:X4} は Unicode のスカラー値ではありません。", nameof(codepoints));
                }
                requested.Add(cp);
            }

            var units = (double)font.UnitsPerEm;
            var mapped = new List<int>(requested.Count);
            var glyphs = new List<GlyphMetrics>(requested.Count + 1);
            var outlines = new List<GlyphOutline?>(requested.Count + 1);
            var inkBoxes = new List<EmBox>(requested.Count + 1);
            var missing = new List<int>();
            foreach (var cp in requested)
            {
                if (!font.TryGetGlyphIndex(cp, out var gid))
                {
                    missing.Add(cp);
                    continue;
                }
                mapped.Add(cp);
                Add(font.GetOutline(gid), font.GetAdvanceWidth(gid) / units, units, glyphs, outlines, inkBoxes);
            }

            var notdefOutline = font.GetOutline(0);
            var notdefAdvance = font.GetAdvanceWidth(0) / units;
            if (!notdefOutline.TryGetBounds(out _, out _, out _, out _))
            {
                notdefOutline = SyntheticNotdef(units);
                notdefAdvance = Math.Max(notdefAdvance, 0.6);
            }
            Add(notdefOutline, notdefAdvance, units, glyphs, outlines, inkBoxes);

            var face = new FontFaceMetrics(
                (float)(font.Ascender / units), (float)(font.Descender / units), (float)(font.LineGap / units));
            return new FontFaceData(face, mapped.ToArray(), glyphs.ToArray(), outlines.ToArray(), inkBoxes.ToArray(), font.UnitsPerEm, missing);
        }

        // インク境界は輪郭の点の境界ボックス（制御点を含む＝曲線の境界ボックスより広いことがある）。幅 0 のグリフ（縦棒 1 本の点列など）でも
        // 箱が作れるよう、右端と上端を左端と下端よりわずかに大きくする。
        private static void Add(
            GlyphOutline outline, double advance, double units,
            List<GlyphMetrics> glyphs, List<GlyphOutline?> outlines, List<EmBox> inkBoxes)
        {
            if (outline.TryGetBounds(out var x0, out var y0, out var x1, out var y1))
            {
                var box = new EmBox(x0 / units, y0 / units, Math.Max(x1, x0 + 1e-6) / units, Math.Max(y1, y0 + 1e-6) / units);
                glyphs.Add(new GlyphMetrics((float)advance, (float)box.Left, (float)box.Bottom, (float)box.Right, (float)box.Top));
                outlines.Add(outline);
                inkBoxes.Add(box);
            }
            else
            {
                glyphs.Add(new GlyphMetrics((float)advance, 0f, 0f, 0f, 0f));
                outlines.Add(null);
                inkBoxes.Add(default);
            }
        }

        // 元フォントの .notdef が空のときの代わり: 0.08〜0.52em × 0〜0.72em の枠（外周と、逆向きの内周）。
        private static GlyphOutline SyntheticNotdef(double units)
        {
            OutlinePoint[] Rect(double left, double bottom, double right, double top, bool clockwise)
            {
                var points = new[]
                {
                    new OutlinePoint(left * units, bottom * units, true),
                    new OutlinePoint(left * units, top * units, true),
                    new OutlinePoint(right * units, top * units, true),
                    new OutlinePoint(right * units, bottom * units, true),
                };
                if (!clockwise)
                {
                    Array.Reverse(points);
                }
                return points;
            }

            return new GlyphOutline(new List<OutlinePoint[]>
            {
                Rect(0.08, 0.0, 0.52, 0.72, true),
                Rect(0.14, 0.06, 0.46, 0.66, false),
            });
        }
    }
}
