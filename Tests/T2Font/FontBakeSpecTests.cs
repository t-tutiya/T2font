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
    /// フォントの生成仕様（<c>*.t2font</c>・パッケージの README §7）の読み取りと、生成仕様からの字の表の生成。
    /// 綴りの誤りを既定値で黙って読まないこと（README §2.1）を中心に見る。
    /// </summary>
    [TestFixture]
    public class FontBakeSpecTests
    {
        private const string Sdf = @"""representation"": { ""kind"": ""sdf"" }";

        private static TrueTypeFont? _font;

        private static TrueTypeFont Font => _font ??= new TrueTypeFont(File.ReadAllBytes(TestFonts.Mplus1c()));

        private static FontBakeSpec Parse(string json)
        {
            return FontBakeSpec.Parse(json, "test.t2font", TestFonts.Kinds);
        }

        private static SdfSpecSettings SdfOf(FontBakeSpec spec)
        {
            return (SdfSpecSettings)spec.Representation;
        }

        [Test]
        public void 全キーを読む()
        {
            var spec = Parse(@"{
                // 開発用（コメントを書ける）
                ""source"": ""Assets/Authoring/Fonts/mplus-1c-regular.ttf"",
                ""coverage"": ""corpus"",
                ""charsets"": [""JisX0208"", ""Ascii""],
                ""characters"": ""◀▶𠮷"",
                ""representation"": { ""kind"": ""sdf"", ""emPx"": 40, ""format"": ""BC4"", ""maxPageSize"": 4096, ""cellBox"": [-0.05, -0.25, 1.05, 1.0] }
            }");

            Assert.That(spec.Source, Is.EqualTo("Assets/Authoring/Fonts/mplus-1c-regular.ttf"));
            Assert.That(spec.Coverage, Is.EqualTo(FontCoverage.Corpus));
            Assert.That(spec.Charsets, Is.EqualTo(FontCharsetKind.JisX0208 | FontCharsetKind.Ascii));
            Assert.That(spec.NamedCodepoints(), Is.EqualTo(new[] { 0x25B6, 0x25C0, 0x20BB7 }));
            Assert.That(spec.Representation.Kind, Is.SameAs(SdfRepresentationKind.Instance));

            SdfSpecSettings sdf = SdfOf(spec);
            Assert.That(sdf.EmPx, Is.EqualTo(40));
            Assert.That(sdf.Format, Is.EqualTo(SdfPageFormat.BC4));
            Assert.That(sdf.MaxPageSize, Is.EqualTo(4096));
            Assert.That(sdf.CellBox!.Value.Right, Is.EqualTo(1.05));

            SdfBakeSettings settings = sdf.ToBakeSettings();
            Assert.That(settings.EmPx, Is.EqualTo(40));
            Assert.That(settings.MaxPageWidth, Is.EqualTo(4096));
            Assert.That(settings.MaxPageHeight, Is.EqualTo(4096));
            Assert.That(settings.RangeEm, Is.EqualTo((double)SdfEncoding.RangeEm), "距離域は生成仕様では選べない");
        }

        [Test]
        public void 省略できるキーの既定()
        {
            var spec = Parse(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""characters"": ""A"", " + Sdf + " }");

            Assert.That(spec.Coverage, Is.EqualTo(FontCoverage.Fixed));
            Assert.That(spec.Charsets, Is.EqualTo(FontCharsetKind.None));
            SdfSpecSettings sdf = SdfOf(spec);
            Assert.That(sdf.EmPx, Is.EqualTo(48));
            Assert.That(sdf.Format, Is.EqualTo(SdfPageFormat.R8));
            Assert.That(sdf.MaxPageSize, Is.EqualTo(8192));
            Assert.That(sdf.CellBox.HasValue, Is.False);
        }

        [Test]
        public void 元フォントはPackagesの下にも置ける()
        {
            var spec = Parse(@"{ ""source"": ""Packages/x.y/Fonts/a.ttf"", ""coverage"": ""fixed"", ""characters"": ""A"", " + Sdf + " }");
            Assert.That(spec.Source, Is.EqualTo("Packages/x.y/Fonts/a.ttf"));
        }

        private const string Base = @"""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""characters"": ""A""";

        [TestCase(@"{ " + Base + @", ""emPx"": 48, " + Sdf + " }", "未知のキー \"emPx\"")]
        [TestCase(@"{ " + Base + " }", "キー \"representation\" がありません")]
        [TestCase(@"{ " + Base + @", ""representation"": ""sdf"" }", "representation は { \"kind\"")]
        [TestCase(@"{ " + Base + @", ""representation"": { } }", "representation: キー \"kind\" がありません")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""msdf"" } }", "未知の表現の種類 \"msdf\" です（種類は sdf）")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""emPX"": 48 } }", "representation: 未知のキー \"emPX\"")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""emPx"": ""48"" } }", "emPx は数")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""emPx"": 4 } }", "emPx は 8〜256")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""format"": ""RGBA32"" } }", "format は")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""maxPageSize"": 1000.5 } }", "maxPageSize は")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""cellBox"": [0, 0, 1] } }", "cellBox は")]
        [TestCase(@"{ " + Base + @", ""representation"": { ""kind"": ""sdf"", ""cellBox"": [0, 0, -1, 1] } }", "箱の大きさ")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""source"": ""Assets/b.ttf"", ""coverage"": ""fixed"", ""characters"": ""A"", " + Sdf + " }", "JSON として読めません")]
        [TestCase(@"{ ""coverage"": ""fixed"", ""characters"": ""A"", " + Sdf + " }", "キー \"source\" がありません")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""characters"": ""A"", " + Sdf + " }", "キー \"coverage\" がありません")]
        [TestCase(@"{ ""source"": ""Content/a.ttf"", ""coverage"": ""fixed"", ""characters"": ""A"", " + Sdf + " }", "Assets/ か Packages/ で始まり")]
        [TestCase(@"{ ""source"": ""Assets\\a.ttf"", ""coverage"": ""fixed"", ""characters"": ""A"", " + Sdf + " }", "Assets/ か Packages/ で始まり")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""all"", ""characters"": ""A"", " + Sdf + " }", "coverage は")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": [""Jis""], " + Sdf + " }", "未知の字集合 \"Jis\"")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": [""1""], " + Sdf + " }", "未知の字集合 \"1\"")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": [""Ascii, JisX0208""], " + Sdf + " }", "未知の字集合")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": [""None""], " + Sdf + " }", "未知の字集合")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": ""Ascii"", " + Sdf + " }", "charsets は字集合の名前の配列")]
        [TestCase(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", " + Sdf + " }", "charsets か characters")]
        [TestCase(@"[ ""source"" ]", "最上位は")]
        [TestCase(@"{ " + Base + ", " + Sdf + " } x", "JSON として読めません")]
        public void 誤りは名指して止める(string json, string expected)
        {
            FontSpecException? ex = Assert.Throws<FontSpecException>(() => Parse(json));
            Assert.That(ex!.Message, Does.StartWith("test.t2font: "));
            Assert.That(ex.Message, Does.Contain(expected));
        }

        [Test]
        public void 片割れのサロゲートは追加字にできない()
        {
            // JSON の \uD842 エスケープは Newtonsoft が U+FFFD に置き換えるので、生の片割れ（C# の文字列の中の 1 符号単位）で試す。
            FontSpecException? ex = Assert.Throws<FontSpecException>(
                () => Parse("{ \"source\": \"Assets/a.ttf\", \"coverage\": \"fixed\", \"characters\": \"a\uD842\", " + Sdf + " }"));
            Assert.That(ex!.Message, Does.Contain("characters に不正な字"));
        }

        [Test]
        public void 名指しの追加字が元フォントに無ければ字の表を作らずに止める()
        {
            var spec = Parse("{ \"source\": \"Assets/a.ttf\", \"coverage\": \"fixed\", \"characters\": \"A\U0010FFFD\", " + Sdf + " }");
            FontBakeException? ex = Assert.Throws<FontBakeException>(() => FontSpecBaker.BuildFace(spec, Font, "test.t2font"));
            Assert.That(ex!.Message, Does.Contain("U+10FFFD"));
        }

        [Test]
        public void 固定の字集合の欠けは件数として報告して続ける()
        {
            // JIS X 0208 のうち元フォント（M+ 1c）に無いのは第 2 水準の 1,232 字。
            var spec = Parse(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": [""JisX0208""], ""characters"": ""◀"", " + Sdf + " }");
            FontSpecFace built = FontSpecBaker.BuildFace(spec, Font, "test.t2font");

            Assert.That(built.MissingFromCharsets.Count, Is.EqualTo(1232));
            Assert.That(built.Face.Codepoints.Length, Is.EqualTo(6879 - 1232 + 1));
            Assert.That(GlyphTable.IndexOf(built.Face.Codepoints, '◀'), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 生成元のハッシュは仕様とフォントのどちらが変わっても変わる()
        {
            var bytes = new byte[] { 1, 2, 3 };
            var a = FontSpecBaker.SourceHash("{}", bytes, SdfRepresentationKind.Instance);

            Assert.That(a, Has.Length.EqualTo(64));
            Assert.That(FontSpecBaker.SourceHash("{}", bytes, SdfRepresentationKind.Instance), Is.EqualTo(a));
            Assert.That(FontSpecBaker.SourceHash("{ }", bytes, SdfRepresentationKind.Instance), Is.Not.EqualTo(a));
            Assert.That(FontSpecBaker.SourceHash("{}", new byte[] { 1, 2, 4 }, SdfRepresentationKind.Instance), Is.Not.EqualTo(a));
        }

        [Test]
        public void 字集合の名前は列挙子の綴りそのもの()
        {
            var names = new[] { "Ascii", "JisX0208", "WindowsVariants", "NecRow13", "IbmExtensions", "CommonSymbols", "DevelopmentDefault" };
            foreach (var name in names)
            {
                var spec = Parse($"{{ \"source\": \"Assets/a.ttf\", \"coverage\": \"fixed\", \"charsets\": [\"{name}\"], {Sdf} }}");
                Assert.That(spec.Charsets.ToString(), Is.EqualTo(name));
            }
            IEnumerable<int> development = Parse(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""fixed"", ""charsets"": [""DevelopmentDefault""], " + Sdf + " }")
                .FixedCodepoints();
            Assert.That(development.Count(), Is.EqualTo(FontCharsets.Build(FontCharsetKind.DevelopmentDefault).Count));
        }

        [Test]
        public void 本文へ書き戻して同じ値に読み戻せる()
        {
            var original = Parse(@"{
                ""source"": ""Assets/a.ttf"", ""coverage"": ""corpus"", ""charsets"": [""Ascii"", ""JisX0208""], ""characters"": ""◀"",
                ""representation"": { ""kind"": ""sdf"", ""emPx"": 40.5, ""format"": ""BC4"", ""maxPageSize"": 4096, ""cellBox"": [-0.05, -0.25, 1.05, 1.0] }
            }");
            var text = original.ToJson(new[] { "見出しのコメント" });
            FontBakeSpec back = Parse(text);

            Assert.That(text, Does.StartWith("// 見出しのコメント\n{\n"));
            Assert.That(back.ToJson(Array.Empty<string>()), Is.EqualTo(original.ToJson(Array.Empty<string>())));
            Assert.That(SdfOf(back).EmPx, Is.EqualTo(40.5));
            Assert.That(SdfOf(back).CellBox!.Value.Bottom, Is.EqualTo(-0.25));
            Assert.That(back.Charsets, Is.EqualTo(FontCharsetKind.Ascii | FontCharsetKind.JisX0208));
        }

        [Test]
        public void 字集合だけを替えた生成仕様は字を名指しで持つ()
        {
            var original = Parse(@"{ ""source"": ""Assets/a.ttf"", ""coverage"": ""corpus"", ""charsets"": [""JisX0208""], " + Sdf + " }");
            FontBakeSpec narrowed = original.WithCharacters(new[] { (int)'几', 'あ' });
            FontBakeSpec back = Parse(narrowed.ToJson(Array.Empty<string>()));

            Assert.That(back.Charsets, Is.EqualTo(FontCharsetKind.None));
            Assert.That(back.NamedCodepoints(), Is.EqualTo(new[] { (int)'あ', '几' }));
            Assert.That(back.Representation.Kind, Is.SameAs(SdfRepresentationKind.Instance));
            Assert.Throws<ArgumentException>(() => original.WithCharacters(new[] { 0x0A }));
            Assert.Throws<ArgumentException>(() => original.WithCharacters(Array.Empty<int>()));
        }

        [Test]
        public void 同じ名前の表現の種類は並べられない()
        {
            Assert.Throws<ArgumentException>(() => _ = new GlyphRepresentationKinds(new GlyphRepresentationKind[] { SdfRepresentationKind.Instance, SdfRepresentationKind.Instance }));
        }
    }
}
