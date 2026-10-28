#nullable enable

using System.Collections.Generic;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>
    /// 文字列を字（コードポイント）の並びとして読む（<see cref="TextCodepoints"/>・パッケージの README §3.2）。
    /// 字は字形番号へ 1 字ずつ引くので、BMP 外の字がサロゲートの 2 つに割れないことを見る。
    /// </summary>
    [TestFixture]
    public class TextCodepointsTests
    {
        private static List<int> ReadAll(string text)
        {
            var list = new List<int>();
            for (var i = 0; i < text.Length;)
            {
                list.Add(TextCodepoints.Read(text, i, out var length));
                i += length;
            }
            return list;
        }

        [Test]
        public void サロゲートペアは1字()
        {
            Assert.That(ReadAll("a𠮷b"), Is.EqualTo(new[] { 'a', 0x20BB7, 'b' }));
            Assert.That(TextCodepoints.Count("a𠮷b"), Is.EqualTo(3));
            Assert.That(TextCodepoints.Count(""), Is.Zero);
        }

        [Test]
        public void 片割れのサロゲートは捨てずにその値を1字として返す()
        {
            Assert.That(ReadAll("a\uD842"), Is.EqualTo(new[] { 'a', 0xD842 }));
            Assert.That(ReadAll("\uDFB7a"), Is.EqualTo(new[] { 0xDFB7, 'a' }));
            Assert.That(TextCodepoints.Count("\uD842\uD842"), Is.EqualTo(2));
        }

        [Test]
        public void 表示はスカラー値なら字_片割れはUプラス表記()
        {
            Assert.That(TextCodepoints.Display(0x20BB7), Is.EqualTo("𠮷"));
            Assert.That(TextCodepoints.Display('あ'), Is.EqualTo("あ"));
            Assert.That(TextCodepoints.Display(0xD842), Is.EqualTo("U+D842"));
        }

        [Test]
        public void 描く字の集合へは制御文字とBOMを足さない()
        {
            var set = new HashSet<int>();
            TextCodepoints.AddDrawable(set, "a\t\u007F\uFEFF𠮷");
            Assert.That(set, Is.EquivalentTo(new[] { 'a', 0x20BB7 }));
        }

        [Test]
        public void 描く字の集合へ片割れのサロゲートは足せない()
        {
            Assert.Throws<System.ArgumentException>(() => TextCodepoints.AddDrawable(new HashSet<int>(), "a\uD842"));
            Assert.Throws<System.ArgumentException>(() => TextCodepoints.AddDrawable(new HashSet<int>(), "\uDFB7"));
        }
    }
}
