using System;
using info.someiyoshino.T2Font;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace T2font.EditModeTests
{
    /// <summary>マテリアルの置き場（パッケージの README §4.5・§6）。</summary>
    [TestFixture]
    public class GlyphMaterialCacheTests
    {
        private static readonly GlyphEdgeStyle Outlined = new GlyphEdgeStyle(0.2f, 0.4f, 0.6f, 1f, 0.01f, 0.02f, 0.05f, 0.03f);

        [Test]
        public void 同じシェーダの表現はエッジスタイルが同じならマテリアルを共有する()
        {
            var shader = TestFontAssets.SdfShader();
            var a = FakeGlyphRepresentation.Create(shader, GlyphEffects.All);
            var b = FakeGlyphRepresentation.Create(shader, GlyphEffects.All);
            using (var cache = new GlyphMaterialCache())
            {
                try
                {
                    Assert.That(cache.Get(a, Outlined), Is.SameAs(cache.Get(b, Outlined)));
                    Assert.That(cache.Get(a, GlyphEdgeStyle.None), Is.Not.SameAs(cache.Get(a, Outlined)));
                    Assert.That(cache.Count, Is.EqualTo(2));
                }
                finally
                {
                    Object.DestroyImmediate(a);
                    Object.DestroyImmediate(b);
                }
            }
        }

        [Test]
        public void エッジスタイルの値をemのままuniformへ入れ_SDFは距離レンジを入れる()
        {
            var font = TestFontAssets.Mplus1cSdf();
            using (var cache = new GlyphMaterialCache())
            {
                var material = cache.Get(font.Representation, Outlined);

                Assert.That(material.shader.name, Is.EqualTo("T2Font/SDF"));
                Assert.That(material.GetFloat("_FaceDilate"), Is.EqualTo(0.01f));
                Assert.That(material.GetFloat("_FaceSoftness"), Is.EqualTo(0.02f));
                Assert.That(material.GetFloat("_OutlineWidth"), Is.EqualTo(0.05f));
                Assert.That(material.GetFloat("_OutlineSoftness"), Is.EqualTo(0.03f));
                Assert.That(material.GetFloat("_RangeEm"), Is.EqualTo(SdfEncoding.RangeEm));
                var expected = new Color(0.2f, 0.4f, 0.6f, 1f);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    expected = expected.linear;
                }
                Assert.That((Color)material.GetVector("_OutlineColor"), Is.EqualTo(expected));
            }
        }

        [Test]
        public void 範囲の外のエッジスタイルと描けない効果は止める()
        {
            var plain = FakeGlyphRepresentation.Create(TestFontAssets.SdfShader(), GlyphEffects.FaceDilate);
            using (var cache = new GlyphMaterialCache())
            {
                try
                {
                    var tooWide = new GlyphEdgeStyle(0f, 0f, 0f, 1f, 0f, 0f, GlyphEdgeStyle.OutlineWidthMax * 2f, 0f);
                    var font = TestFontAssets.Mplus1cSdf();
                    Assert.That(GlyphMaterialCache.Check(font.Representation, tooWide), Does.Contain("OutlineWidth"));
                    Assert.Throws<ArgumentException>(() => cache.Get(font.Representation, tooWide));

                    Assert.That(GlyphMaterialCache.Check(plain, Outlined), Does.Contain("Outline"));
                    Assert.Throws<ArgumentException>(() => cache.Get(plain, Outlined));
                    var dilateOnly = new GlyphEdgeStyle(0f, 0f, 0f, 1f, 0.02f, 0f, 0f, 0f);
                    Assert.That(GlyphMaterialCache.Check(plain, dilateOnly), Is.Null);
                    Assert.That(cache.Count, Is.EqualTo(0));
                }
                finally
                {
                    Object.DestroyImmediate(plain);
                }
            }
        }

        [Test]
        public void Releaseは範囲に属するマテリアルだけを捨てる()
        {
            var shader = TestFontAssets.SdfShader();
            var representation = FakeGlyphRepresentation.Create(shader, GlyphEffects.All);
            var cache = new GlyphMaterialCache();
            try
            {
                var material = cache.Get(representation, Outlined);
                cache.Release(representation);
                Assert.That(cache.Count, Is.EqualTo(1), "範囲はシェーダなので、表現そのものを渡しても捨てない");

                cache.Release(shader);
                Assert.That(cache.Count, Is.EqualTo(0));
                Assert.That(material == null, Is.True, "捨てたマテリアルは破棄される");
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(representation);
            }
        }
    }
}
