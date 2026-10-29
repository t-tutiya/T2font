#nullable enable

using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>字の昇順の配列を引く（<see cref="GlyphTable"/>・パッケージの README §3.2）。</summary>
    [TestFixture]
    public class GlyphTableTests
    {
        private static readonly int[] Codepoints = { ' ', 'A', 'あ', '几', 0x20BB7 };

        [Test]
        public void 添字がそのままグリフインデックスになる()
        {
            for (var i = 0; i < Codepoints.Length; i++)
            {
                Assert.That(GlyphTable.IndexOf(Codepoints, Codepoints[i]), Is.EqualTo(i));
            }
        }

        [Test]
        public void 無い字はマイナス1()
        {
            Assert.That(GlyphTable.IndexOf(Codepoints, 'B'), Is.EqualTo(-1));
            Assert.That(GlyphTable.IndexOf(Codepoints, 0), Is.EqualTo(-1));
            Assert.That(GlyphTable.IndexOf(Codepoints, 0x10FFFF), Is.EqualTo(-1));
            Assert.That(GlyphTable.IndexOf(new int[0], 'A'), Is.EqualTo(-1));
        }

        [Test]
        public void 狭義昇順でなければ偽()
        {
            Assert.That(GlyphTable.IsStrictlyAscending(Codepoints), Is.True);
            Assert.That(GlyphTable.IsStrictlyAscending(new[] { 1, 1 }), Is.False);
            Assert.That(GlyphTable.IsStrictlyAscending(new[] { 2, 1 }), Is.False);
            Assert.That(GlyphTable.IsStrictlyAscending(new int[0]), Is.True);
        }
    }
}
