#nullable enable

using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>エッジスタイルの契約（<see cref="GlyphEdgeStyle"/>・パッケージの README §6）。</summary>
    [TestFixture]
    public class GlyphEdgeStyleTests
    {
        private static GlyphEdgeStyle Style(float dilate = 0f, float faceSoftness = 0f, float width = 0f, float outlineSoftness = 0f, float alpha = 1f)
        {
            return new GlyphEdgeStyle(0f, 0f, 0f, alpha, dilate, faceSoftness, width, outlineSoftness);
        }

        [Test]
        public void 外延は全キーを上限にした組の外縁の位置()
        {
            Assert.That(GlyphEdgeStyle.MaxOuterExtentEm, Is.EqualTo(0.15f).Within(1e-6f));
        }

        [Test]
        public void 外延と14pxのAAの半分が同梱のSDFの距離レンジに収まる()
        {
            // 同梱の SDF の距離レンジ（README §9）。範囲を広げるときは、どの表現が描けなくなるかを確かめる。
            const float SdfRangeEm = 0.1875f;
            Assert.That(GlyphEdgeStyle.MaxOuterExtentEm + (0.5f / 14f), Is.LessThanOrEqualTo(SdfRangeEm));
            Assert.That(-GlyphEdgeStyle.FaceDilateMin + (GlyphEdgeStyle.FaceSoftnessMax / 2f) + (0.5f / 14f), Is.LessThanOrEqualTo(SdfRangeEm));
        }

        [Test]
        public void 使う効果は値が0でないキー()
        {
            Assert.That(GlyphEdgeStyle.None.Uses, Is.EqualTo(GlyphEffects.None));
            Assert.That(Style(width: 0.05f).Uses, Is.EqualTo(GlyphEffects.Outline));
            Assert.That(Style(dilate: -0.01f).Uses, Is.EqualTo(GlyphEffects.FaceDilate));
            Assert.That(Style(dilate: 0.01f, faceSoftness: 0.02f, width: 0.05f, outlineSoftness: 0.01f).Uses, Is.EqualTo(GlyphEffects.All));
            // 縁取りの色だけがあっても、幅が 0 なら縁取りは使われない。
            Assert.That(Style(alpha: 0.5f).Uses, Is.EqualTo(GlyphEffects.None));
        }

        [Test]
        public void 範囲の外の値を名指す()
        {
            Assert.That(Style(dilate: 0.04f, faceSoftness: 0.06f, width: 0.08f, outlineSoftness: 0.06f).FindOutOfRange(), Is.Null);
            Assert.That(Style(dilate: -0.03f).FindOutOfRange(), Is.Null);
            StringAssert.StartsWith("FaceDilate", Style(dilate: 0.05f).FindOutOfRange());
            StringAssert.StartsWith("OutlineWidth", Style(width: 0.09f).FindOutOfRange());
            StringAssert.StartsWith("OutlineWidth", Style(width: -0.01f).FindOutOfRange());
            StringAssert.StartsWith("OutlineSoftness", Style(outlineSoftness: float.NaN).FindOutOfRange());
            StringAssert.StartsWith("OutlineColor", Style(alpha: 1.5f).FindOutOfRange());
        }

        [Test]
        public void 値が等しければ等しい()
        {
            Assert.That(Style(width: 0.05f), Is.EqualTo(Style(width: 0.05f)));
            Assert.That(Style(width: 0.05f).GetHashCode(), Is.EqualTo(Style(width: 0.05f).GetHashCode()));
            Assert.That(Style(width: 0.05f) == Style(width: 0.06f), Is.False);
        }
    }
}
