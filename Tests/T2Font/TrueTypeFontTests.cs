#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using info.someiyoshino.T2Font.Bake;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>
    /// TrueType のリーダー（<see cref="TrueTypeFont"/>・パッケージの README §7.3）。期待値は検証用の M+ 1c
    /// （<c>mplus-1c-regular.ttf</c>）の表を別の実装（Python で表を直に読んだもの）で読んだ値。フォントを差し替えたら値も取り直す。
    /// </summary>
    [TestFixture]
    public class TrueTypeFontTests
    {
        private static TrueTypeFont? _font;

        private static TrueTypeFont Font => _font ??= new TrueTypeFont(File.ReadAllBytes(TestFonts.Mplus1c()));

        [Test]
        public void 基本の表を読む()
        {
            Assert.That(Font.UnitsPerEm, Is.EqualTo(1000));
            Assert.That(Font.GlyphCount, Is.EqualTo(8778));
            Assert.That(Font.Ascender, Is.EqualTo(1075));
            Assert.That(Font.Descender, Is.EqualTo(-320));
            Assert.That(Font.LineGap, Is.EqualTo(90));
        }

        [TestCase('A', 36)]
        [TestCase('g', 74)]
        [TestCase(' ', 3)]
        [TestCase('あ', 2806)]
        [TestCase('几', 3422)]
        [TestCase('◀', 2391)]
        public void cmapでグリフインデックスを引く(char ch, int expected)
        {
            Assert.That(Font.TryGetGlyphIndex(ch, out var gid), Is.True);
            Assert.That(gid, Is.EqualTo(expected));
        }

        [Test]
        public void cmapの全コードポイントは昇順でグリフ0を含まない()
        {
            IReadOnlyList<int> cps = Font.Codepoints;
            Assert.That(cps.Count, Is.EqualTo(8431));
            for (var i = 1; i < cps.Count; i++)
            {
                Assert.That(cps[i], Is.GreaterThan(cps[i - 1]));
            }
            Assert.That(Font.Contains(0x0000), Is.False);
            Assert.That(Font.Contains(0x10FFFD), Is.False);
        }

        [TestCase('A', 663)]
        [TestCase('g', 563)]
        [TestCase(' ', 270)]
        [TestCase('あ', 1000)]
        [TestCase('é', 525)]
        public void 送り幅(char ch, int expected)
        {
            Font.TryGetGlyphIndex(ch, out var gid);
            Assert.That(Font.GetAdvanceWidth(gid), Is.EqualTo(expected));
        }

        // 境界ボックスの期待値は glyf のヘッダ（このフォントは全点の境界ボックスをヘッダに書いている）。
        [TestCase('A', 2, 30, 0, 633, 730)]
        [TestCase('g', 2, 45, -230, 488, 530)]
        [TestCase('あ', 3, 97, -63, 903, 803)]
        [TestCase('几', 1, 40, -80, 977, 770)]
        [TestCase('⺅', 2, 170, -87, 798, 817)]  // ⺅＝複合グリフ（部品 1 つの参照）。輪郭数は部品のもの
        public void 輪郭の境界ボックスと輪郭数(char ch, int contours, int xMin, int yMin, int xMax, int yMax)
        {
            Font.TryGetGlyphIndex(ch, out var gid);
            GlyphOutline outline = Font.GetOutline(gid);

            Assert.That(outline.TryGetBounds(out var x0, out var y0, out var x1, out var y1), Is.True);
            Assert.That(new[] { x0, y0, x1, y1 }, Is.EqualTo(new double[] { xMin, yMin, xMax, yMax }));
            if (ch != '⺅')
            {
                Assert.That(outline.ContourCount, Is.EqualTo(contours));
            }
        }

        [Test]
        public void 複合グリフは部品と同じ輪郭になる()
        {
            Font.TryGetGlyphIndex('⺅', out var composite);
            GlyphOutline a = Font.GetOutline(composite);
            GlyphOutline b = Font.GetOutline(3127);  // ⺅ の部品（オフセット 0・拡縮なし）

            Assert.That(a.ContourCount, Is.EqualTo(b.ContourCount));
            for (var c = 0; c < a.ContourCount; c++)
            {
                Assert.That(a.Contours[c].Length, Is.EqualTo(b.Contours[c].Length));
                for (var i = 0; i < a.Contours[c].Length; i++)
                {
                    Assert.That(a.Contours[c][i].X, Is.EqualTo(b.Contours[c][i].X));
                    Assert.That(a.Contours[c][i].Y, Is.EqualTo(b.Contours[c][i].Y));
                    Assert.That(a.Contours[c][i].OnCurve, Is.EqualTo(b.Contours[c][i].OnCurve));
                }
            }
        }

        [Test]
        public void 空白は輪郭を持たない()
        {
            Font.TryGetGlyphIndex(' ', out var gid);
            Assert.That(Font.GetOutline(gid).IsEmpty, Is.True);
        }

        [Test]
        public void CFFのフォントは明確に失敗する()
        {
            var data = new byte[64];
            data[0] = (byte)'O';
            data[1] = (byte)'T';
            data[2] = (byte)'T';
            data[3] = (byte)'O';

            Assert.Throws<NotSupportedException>(() => _ = new TrueTypeFont(data));
        }

        [Test]
        public void フォントコレクションは書体を選べないので明確に失敗する()
        {
            var data = new byte[64];
            data[0] = (byte)'t';
            data[1] = (byte)'t';
            data[2] = (byte)'c';
            data[3] = (byte)'f';

            NotSupportedException? ex = Assert.Throws<NotSupportedException>(() => _ = new TrueTypeFont(data));
            Assert.That(ex!.Message, Does.Contain("TTC"));
        }

        [Test]
        public void 途中で切れたフォントはFormatExceptionで失敗する()
        {
            var whole = File.ReadAllBytes(TestFonts.Mplus1c());
            var truncated = new byte[400];
            Array.Copy(whole, truncated, truncated.Length);

            Assert.Throws<FormatException>(() => _ = new TrueTypeFont(truncated));
        }

        [Test]
        public void 輪郭をフラット化すると閉じた線分の列になる()
        {
            Font.TryGetGlyphIndex('あ', out var gid);
            var edges = new List<OutlineEdge>();
            Font.GetOutline(gid).Flatten(0.1, 0, 0, 0.05, edges);

            // 閉じた輪郭の集まりなので、端点の出入りの数が釣り合う（各点に入る辺と出る辺が 1 本ずつ）。
            var balance = new Dictionary<(double, double), int>();
            foreach (OutlineEdge e in edges)
            {
                var start = (e.X0, e.Y0);
                var end = (e.X1, e.Y1);
                balance[start] = (balance.TryGetValue(start, out var s) ? s : 0) + 1;
                balance[end] = (balance.TryGetValue(end, out var t) ? t : 0) - 1;
            }
            Assert.That(edges.Count, Is.GreaterThan(20));
            Assert.That(balance.Values, Has.All.EqualTo(0));
        }
    }
}
