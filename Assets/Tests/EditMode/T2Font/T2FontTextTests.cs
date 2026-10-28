using System.Linq;
using System.Text.RegularExpressions;
using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Viewer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace T2font.EditModeTests
{
    /// <summary>検証ビュー（パッケージの README §10）が、字ごとに 1 つの CanvasRenderer を作って表現を貼ること。</summary>
    [TestFixture]
    public class T2FontTextTests
    {
        private GameObject _canvas;
        private T2FontText _text;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(800f, 200f);
            _text = go.AddComponent<T2FontText>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void 墨のある字ごとに1つのCanvasRendererを作る()
        {
            _text.Font = TestFontAssets.Mplus1cSdf();
            _text.Text = "A あ\n■";
            _text.Rebuild();

            Assert.That(_text.GlyphObjects.Select(g => g.name), Is.EqualTo(new[] { "U+0041", "U+3042", "U+25A0" }));
            foreach (var glyph in _text.GlyphObjects)
            {
                var renderer = glyph.GetComponent<CanvasRenderer>();
                Assert.That(renderer.materialCount, Is.EqualTo(1));
                Assert.That(renderer.GetMaterial(0).shader.name, Is.EqualTo("T2Font/SDF"));
                Assert.That(renderer.GetAlpha(), Is.EqualTo(1f));
                Assert.That((glyph.hideFlags & HideFlags.DontSave) != 0, Is.True, "字はシーンに保存しない");
            }
            // 改行で行を送る（■ は A より下の行）。
            Assert.That(_text.GlyphObjects[2].transform.localPosition.y, Is.LessThan(_text.GlyphObjects[0].transform.localPosition.y));
        }

        [Test]
        public void 範囲の外の様式は描かずにLogErrorを出す()
        {
            _text.Font = TestFontAssets.Mplus1cSdf();
            _text.Text = "A";
            _text.EdgeStyle = new GlyphEdgeStyle(0f, 0f, 0f, 1f, 0f, 0f, GlyphEdgeStyle.OutlineWidthMax * 2f, 0f);
            LogAssert.Expect(LogType.Error, new Regex("範囲の外"));

            _text.Rebuild();

            Assert.That(_text.GlyphObjects, Is.Empty);
        }

        [Test]
        public void 作り直すと前の字を捨てる()
        {
            _text.Font = TestFontAssets.Mplus1cSdf();
            _text.Text = "AB";
            _text.Rebuild();
            _text.Text = "A";
            _text.Rebuild();

            Assert.That(_text.GlyphObjects, Has.Count.EqualTo(1));
            Assert.That(_text.transform.childCount, Is.EqualTo(1));
        }
    }
}
