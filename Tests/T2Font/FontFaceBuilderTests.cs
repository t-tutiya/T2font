#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using info.someiyoshino.T2Font.Bake;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>
    /// タイポグラフィテーブルの生成（<see cref="FontFaceBuilder"/>・パッケージの README §3・§7.3）。表現によらない値（送り幅・インク境界・縦メトリクス・
    /// グリフインデックスの並び・代替グリフ）を、検証用の M+ 1c の表から見る。
    /// </summary>
    [TestFixture]
    public class FontFaceBuilderTests
    {
        private static TrueTypeFont? _font;

        private static TrueTypeFont Font => _font ??= new TrueTypeFont(File.ReadAllBytes(TestFonts.Mplus1c()));

        private static IEnumerable<int> Codepoints(string text)
        {
            var set = new SortedSet<int>();
            FontCharsets.AddString(set, text);
            return set;
        }

        [Test]
        public void フォントに無い字はMissingへ載り_空白は送り幅だけを持つ()
        {
            FontFaceData face = FontFaceBuilder.Build(Font, Codepoints("Aあ 几").Append(0x10FFFD));

            Assert.That(face.Missing, Is.EqualTo(new[] { 0x10FFFD }));
            Assert.That(face.Codepoints, Is.EqualTo(new[] { ' ', 'A', 'あ', '几' }.Select(c => (int)c)));
            Assert.That(GlyphTable.IsStrictlyAscending(face.Codepoints), Is.True);

            GlyphMetrics space = face.Glyphs[GlyphTable.IndexOf(face.Codepoints, ' ')];
            Assert.That(space.HasInk, Is.False);
            Assert.That(space.advance, Is.EqualTo(0.27f).Within(1e-6));
            Assert.That(face.Outline(GlyphTable.IndexOf(face.Codepoints, ' ')), Is.Null);

            GlyphMetrics a = face.Glyphs[GlyphTable.IndexOf(face.Codepoints, 'A')];
            Assert.That(a.HasInk, Is.True);
            Assert.That(a.advance, Is.EqualTo(0.663f).Within(1e-6));
        }

        [Test]
        public void インク境界は輪郭の境界ボックスそのもので余白を含まない()
        {
            FontFaceData face = FontFaceBuilder.Build(Font, Codepoints("Ag"));
            GlyphMetrics a = face.Glyphs[GlyphTable.IndexOf(face.Codepoints, 'A')];
            GlyphMetrics g = face.Glyphs[GlyphTable.IndexOf(face.Codepoints, 'g')];

            // M+ 1c の glyf のヘッダのグリフ境界ボックス（A: 30, 0, 633, 730・g: 45, -230, 488, 530・upem 1000）。
            Assert.That(new[] { a.inkLeft, a.inkBottom, a.inkRight, a.inkTop }, Is.EqualTo(new[] { 0.030f, 0f, 0.633f, 0.730f }).Within(1e-6f));
            Assert.That(new[] { g.inkLeft, g.inkBottom, g.inkRight, g.inkTop }, Is.EqualTo(new[] { 0.045f, -0.230f, 0.488f, 0.530f }).Within(1e-6f));
        }

        [Test]
        public void フォント単位の値をemで持つ()
        {
            FontFaceData face = FontFaceBuilder.Build(Font, Codepoints("A"));

            Assert.That(face.Face.ascender, Is.EqualTo(1.075f).Within(1e-6));
            Assert.That(face.Face.descender, Is.EqualTo(-0.32f).Within(1e-6));
            Assert.That(face.Face.lineGap, Is.EqualTo(0.09f).Within(1e-6));
            Assert.That(face.UnitsPerEm, Is.EqualTo(1000));
        }

        [Test]
        public void 代替グリフは元フォントのglyph0でタイポグラフィテーブルの末尾に置く()
        {
            FontFaceData face = FontFaceBuilder.Build(Font, Codepoints("Aあ"));

            Assert.That(face.Notdef, Is.EqualTo(face.Codepoints.Length));
            Assert.That(face.GlyphCount, Is.EqualTo(face.Codepoints.Length + 1));
            Assert.That(face.CodepointOf(face.Notdef), Is.EqualTo(FontFaceBuilder.NotdefCodepoint));
            Assert.That(face.Glyphs[face.Notdef].advance, Is.EqualTo(0.364f).Within(1e-6), "M+ 1c の glyph 0 の送り幅");
            Assert.That(face.Glyphs[face.Notdef].HasInk, Is.True);
            Assert.That(face.Outline(face.Notdef), Is.Not.Null);
        }

        [Test]
        public void 字が1つも無くても代替グリフはある()
        {
            FontFaceData face = FontFaceBuilder.Build(Font, Array.Empty<int>());
            Assert.That(face.Codepoints, Is.Empty);
            Assert.That(face.GlyphCount, Is.EqualTo(1));
        }

        [Test]
        public void スカラー値でないコードポイントは拒む()
        {
            Assert.Throws<ArgumentException>(() => FontFaceBuilder.Build(Font, new[] { 0xD800 }));
            Assert.Throws<ArgumentException>(() => FontFaceBuilder.Build(Font, new[] { 0x110000 }));
        }
    }
}
