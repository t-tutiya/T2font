using System;
using System.Text.RegularExpressions;
using info.someiyoshino.T2Font;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace T2font.EditModeTests
{
    /// <summary>
    /// 取込器が作った資産（パッケージの README §3・§7・§9）と、<see cref="T2FontAsset"/> の字形番号の解決（README §3.1）。
    /// </summary>
    [TestFixture]
    public class T2FontAssetTests
    {
        [Test]
        public void 取込器は字の表とSDFの表現とページを作る()
        {
            var font = TestFontAssets.Mplus1cSdf();

            Assert.That(font.Contains('永'), Is.True);
            Assert.That(font.Contains('■'), Is.True);
            Assert.That(font.Notdef, Is.EqualTo(font.Codepoints.Count));
            Assert.That(font.GlyphCount, Is.EqualTo(font.Codepoints.Count + 1));
            Assert.That(font.SourceHash, Has.Length.EqualTo(64));

            var sdf = font.Representation as SdfRepresentation;
            Assert.That(sdf, Is.Not.Null);
            Assert.That(sdf.Kind, Is.EqualTo(SdfEncoding.KindName));
            Assert.That(sdf.Effects, Is.EqualTo(GlyphEffects.All));
            Assert.That(sdf.Shader, Is.Not.Null);
            Assert.That(sdf.Shader.name, Is.EqualTo("T2Font/SDF"));
            Assert.That(sdf.Cells.Count, Is.EqualTo(font.GlyphCount));
            Assert.That(sdf.Validate(font.GlyphCount), Is.Null);
            Assert.That(sdf.Pages, Is.Not.Empty);
            foreach (var page in sdf.Pages)
            {
                Assert.That(page.format, Is.EqualTo(TextureFormat.R8));
                Assert.That(page.mipmapCount, Is.EqualTo(1));
                Assert.That(page.isReadable, Is.False);
                Assert.That(page.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(page.filterMode, Is.EqualTo(FilterMode.Bilinear));
            }
        }

        [Test]
        public void 送り幅は字の表の値で表現に依らない()
        {
            var font = TestFontAssets.Mplus1cSdf();
            var glyph = font.Resolve('A', out var isSubstitute);

            Assert.That(isSubstitute, Is.False);
            // M+ 1c の hmtx の値（外部 NUnit の字の表の検査と同じ値）。
            Assert.That(font.Metrics(glyph).advance, Is.EqualTo(0.663f).Within(1e-6f));
        }

        [Test]
        public void 収録していない字は代替字形を返し字ごとに1回だけLogErrorを出す()
        {
            var font = TestFontAssets.Mplus1cSdf();
            // 報告済みの記録は資産の上に再生の開始まで残るので、同じ Editor でテストを回し直しても初めての字になるよう、私用面の字を毎回変える。
            var codepoint = 0xF0000 + (Guid.NewGuid().GetHashCode() & 0xFFFF);
            LogAssert.Expect(LogType.Error, new Regex($@"U\+{codepoint:X4}"));

            var first = font.Resolve(codepoint, out var firstSubstitute);
            var second = font.Resolve(codepoint, out var secondSubstitute);

            Assert.That(first, Is.EqualTo(font.Notdef));
            Assert.That(second, Is.EqualTo(font.Notdef));
            Assert.That(firstSubstitute && secondSubstitute, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Createは昇順でない字と字の表の外の代替字形を拒む()
        {
            var representation = FakeGlyphRepresentation.Create(TestFontAssets.SdfShader(), GlyphEffects.None);
            var glyphs = new GlyphMetrics[3];
            try
            {
                Assert.Throws<ArgumentException>(() => T2FontAsset.Create(default, new[] { 'B', (int)'A' }, glyphs, 2, representation, string.Empty));
                Assert.Throws<ArgumentException>(() => T2FontAsset.Create(default, new[] { 'A', (int)'B' }, glyphs, 1, representation, string.Empty));
                Assert.Throws<ArgumentException>(() => T2FontAsset.Create(default, new[] { 'A', (int)'B' }, glyphs, 3, representation, string.Empty));

                var asset = T2FontAsset.Create(default, new[] { 'A', (int)'B' }, glyphs, 2, representation, string.Empty);
                Assert.That(asset.RepresentationOf(1), Is.SameAs(representation));
                Object.DestroyImmediate(asset);
            }
            finally
            {
                Object.DestroyImmediate(representation);
            }
        }

        [Test]
        public void Createはシェーダの無い表現を拒む()
        {
            var representation = FakeGlyphRepresentation.Create(null, GlyphEffects.None);
            try
            {
                var e = Assert.Throws<ArgumentException>(
                    () => T2FontAsset.Create(default, new[] { (int)'A' }, new GlyphMetrics[2], 1, representation, string.Empty));
                Assert.That(e.Message, Does.Contain("シェーダ"));
            }
            finally
            {
                Object.DestroyImmediate(representation);
            }
        }
    }
}
