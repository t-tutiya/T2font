#nullable enable

using System.IO;
using System.Linq;
using info.someiyoshino.T2Font.Bake;
using info.someiyoshino.T2font.Tests;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>
    /// 本プロジェクトの生成仕様（<c>Assets/</c> の下の <c>*.t2font</c>）が、Unity の取込器と同じ純 C# の手順（生成仕様の読取・字の表の生成・
    /// SDF の焼き）を通ること。取込の失敗を Unity を開く前に見つける。
    /// </summary>
    [TestFixture]
    public class ProjectFontSpecTests
    {
        private static string[] SpecPaths()
        {
            var assets = Path.Combine(RepoRoot.Find(), "Assets");
            return Directory.GetFiles(assets, "*" + FontBakeSpec.Extension, SearchOption.AllDirectories).OrderBy(p => p).ToArray();
        }

        [Test]
        public void 生成仕様がある()
        {
            Assert.That(SpecPaths(), Is.Not.Empty);
        }

        [TestCaseSource(nameof(SpecPaths))]
        public void 生成仕様は取込の純CSharpの手順を通る(string path)
        {
            var name = "Assets/" + Path.GetRelativePath(Path.Combine(RepoRoot.Find(), "Assets"), path).Replace('\\', '/');
            var spec = FontBakeSpec.Parse(File.ReadAllText(path), name, TestFonts.Kinds);
            var font = new TrueTypeFont(File.ReadAllBytes(TestFonts.ProjectPath(spec.Source)));

            var face = FontSpecBaker.BuildFace(spec, font, name);
            Assert.That(face.Face.Codepoints, Is.Not.Empty);
            if (spec.Representation is SdfSpecSettings sdf)
            {
                var atlas = SdfAtlasBaker.Bake(face.Face, sdf.ToBakeSettings());
                Assert.That(atlas.Cells, Has.Length.EqualTo(face.Face.GlyphCount));
            }
        }

        [Test]
        public void 検証用のM_1cは適合検査の基準の字を持つ()
        {
            var path = Path.Combine(RepoRoot.Find(), "Assets", "Content", "Fonts", "mplus-1c-sdf.t2font");
            var spec = FontBakeSpec.Parse(File.ReadAllText(path), "mplus-1c-sdf.t2font", TestFonts.Kinds);
            Assert.That(spec.NamedCodepoints(), Does.Contain(0x25A0), "適合検査の描画は「■」を基準の字にする");
        }
    }
}
