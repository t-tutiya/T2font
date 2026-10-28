#nullable enable

using System.Text;
using NUnit.Framework;

namespace info.someiyoshino.T2font.Tests
{
    /// <summary>
    /// <see cref="GeneratedText"/> の規約（生成物の改行は LF 固定・D-GENEOL）の検査。
    /// 外部 NUnit が Core のソースをリンクして検査する、という本プロジェクトの基本形の見本も兼ねる。
    /// </summary>
    [TestFixture]
    public class GeneratedTextTests
    {
        [Test]
        public void AppendLineLf_はプラットフォームに依らずLFだけを書く()
        {
            var sb = new StringBuilder();
            sb.AppendLineLf("a").AppendLineLf().AppendLineLf("b");

            Assert.That(sb.ToString(), Is.EqualTo("a\n\nb\n"));
        }

        [Test]
        public void NormalizeNewlines_はCRLFと単独CRをLFへ畳む()
        {
            Assert.That(GeneratedText.NormalizeNewlines("a\r\nb\rc\n"), Is.EqualTo("a\nb\nc\n"));
        }
    }
}
