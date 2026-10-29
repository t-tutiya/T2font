using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Conformance;
using NUnit.Framework;

namespace T2font.EditModeTests
{
    /// <summary>同梱の SDF の表現が適合検査（パッケージの README §10）を通ること。</summary>
    [TestFixture]
    public class GlyphConformanceTests
    {
        [Test]
        public void 全効果の外延はエッジスタイルの外延の上限と一致する()
        {
            Assert.That(GlyphConformance.OuterExtentEm(GlyphEffects.All), Is.EqualTo(GlyphEdgeStyle.MaxOuterExtentEm).Within(1e-6f));
            Assert.That(GlyphConformance.OuterExtentEm(GlyphEffects.None), Is.EqualTo(0f));
        }

        [Test]
        public void 同梱のSDFの幾何は適合する()
        {
            Assert.That(GlyphConformance.CheckGeometry(TestFontAssets.Mplus1cSdf()), Is.Empty);
        }

        [Test]
        public void 同梱のSDFの描画は適合する()
        {
            if (!GlyphConformance.CanRender)
            {
                Assert.Ignore("描画装置が無い（batchmode の -nographics など）ので、描画の適合検査を回せません。");
            }
            Assert.That(GlyphConformance.CheckRendering(TestFontAssets.Mplus1cSdf(), 0x25A0), Is.Empty);
        }
    }
}
