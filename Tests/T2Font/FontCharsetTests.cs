#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using info.someiyoshino.T2Font.Bake;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>固定の字集合と優先の級（<see cref="FontCharsets"/>・パッケージの README §7.2・§9）。</summary>
    [TestFixture]
    public class FontCharsetTests
    {
        private static TrueTypeFont? _font;

        private static TrueTypeFont Font => _font ??= new TrueTypeFont(File.ReadAllBytes(TestFonts.Mplus1c()));

        private static SortedSet<int> Set(string text)
        {
            var set = new SortedSet<int>();
            FontCharsets.AddString(set, text);
            return set;
        }

        [Test]
        public void JISX0208の表は区分ごとの字数を持ち重複しない()
        {
            Assert.That(Set(JisX0208Table.NonKanji).Count, Is.EqualTo(524));
            Assert.That(Set(JisX0208Table.Level1).Count, Is.EqualTo(2965));
            Assert.That(Set(JisX0208Table.Level2).Count, Is.EqualTo(3390));
            Assert.That(FontCharsets.Build(FontCharsetKind.JisX0208).Count, Is.EqualTo(6879));

            Assert.That(JisX0208Table.NonKanji[0], Is.EqualTo('　'), "1 区 1 点＝全角空白");
            Assert.That(JisX0208Table.Level1[0], Is.EqualTo('亜'));
            Assert.That(JisX0208Table.Level1[JisX0208Table.Level1.Length - 1], Is.EqualTo('腕'));
            Assert.That(JisX0208Table.Level2[0], Is.EqualTo('弌'));
            Assert.That(JisX0208Table.Level2[JisX0208Table.Level2.Length - 1], Is.EqualTo('熙'));
        }

        [Test]
        public void 開発中の既定はJISと周辺の和()
        {
            var jisFamily = FontCharsetKind.Ascii | FontCharsetKind.JisX0208 | FontCharsetKind.WindowsVariants | FontCharsetKind.NecRow13;
            Assert.That(FontCharsets.Build(jisFamily).Count, Is.EqualTo(7054));
            Assert.That(FontCharsets.Build(FontCharsetKind.IbmExtensions).Count, Is.EqualTo(388));
            Assert.That(FontCharsets.Build(FontCharsetKind.CommonSymbols).Count, Is.EqualTo(2628));

            SortedSet<int> set = FontCharsets.Build(FontCharsetKind.DevelopmentDefault);
            Assert.That(set.Count, Is.EqualTo(9730));
            Assert.That(set.Contains('A'), Is.True);
            Assert.That(set.Contains('～'), Is.True, "Windows の波ダッシュ");
            Assert.That(set.Contains('〜'), Is.True, "JIS の波ダッシュ");
            Assert.That(set.Contains('①'), Is.True);
            Assert.That(set.Contains('髙'), Is.True, "IBM 拡張");
            Assert.That(set.Contains('◀'), Is.True, "幾何学模様（JIS X 0208 の外）");
            Assert.That(set.Contains('—'), Is.True, "U+2014（JIS の対応表は U+2015 へ写す）");
            Assert.That(set.Contains('\u200B'), Is.False, "書式文字（ゼロ幅スペース）は除く");
            Assert.That(set.Contains('\u202E'), Is.False, "双方向の制御は除く");
        }

        [Test]
        public void 元フォントの収録はJIS第2水準だけが欠ける()
        {
            // M+ 1c は第 1 水準・非漢字・ASCII・Windows の 6 字をすべて持ち、第 2 水準 3,390 字のうち 1,232 字を持たない。
            // 開発用フォントでこの 1,232 字は「依頼されたが無い字」として報告される（font-asset-pipeline-study §3.4）。
            IEnumerable<int> mustHave = Set(JisX0208Table.NonKanji)
                .Concat(Set(JisX0208Table.Level1))
                .Concat(Set(JisX0208Table.WindowsVariants))
                .Concat(FontCharsets.Build(FontCharsetKind.Ascii));
            Assert.That(mustHave.Where(cp => !Font.Contains(cp)), Is.Empty);

            var missingLevel2 = Set(JisX0208Table.Level2).Count(cp => !Font.Contains(cp));
            Assert.That(missingLevel2, Is.EqualTo(1232));

            // 開発用の既定（9,730 字）のうち元フォントにあるのは 6,442 字（font-asset-pipeline-study §11.2）。
            Assert.That(FontCharsets.Build(FontCharsetKind.DevelopmentDefault).Count(Font.Contains), Is.EqualTo(6442));
        }

        [TestCase('a', 0)]
        [TestCase('、', 0)]
        [TestCase('①', 0)]
        [TestCase('あ', 1)]
        [TestCase('ヴ', 1)]
        [TestCase('亜', 2)]
        [TestCase('熙', 3)]
        [TestCase('丂', 4)]
        public void 優先の級(char ch, int expected)
        {
            Assert.That(FontCharsets.PriorityClass(ch), Is.EqualTo(expected));
        }

        [Test]
        public void 字はサロゲートペアを1字として数え_制御文字を数えない()
        {
            SortedSet<int> set = Set("a\u0001\t𠮷﻿b");
            Assert.That(set, Is.EqualTo(new[] { 'a', 'b', 0x20BB7 }.Select(c => (int)c)));
        }

        [Test]
        public void 片割れのサロゲートは拒む()
        {
            Assert.Throws<ArgumentException>(() => Set("a\uD842"));
            Assert.Throws<ArgumentException>(() => Set("\uDFB7a"));
        }
    }
}
