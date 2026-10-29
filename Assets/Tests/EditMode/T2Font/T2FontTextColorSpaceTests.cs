using System.Collections.Generic;
using info.someiyoshino.T2Font.Conformance;
using info.someiyoshino.T2Font.Viewer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace T2font.EditModeTests
{
    /// <summary>
    /// Canvas の Vertex Color Always in Gamma Color Space（<c>Canvas.vertexColorAlwaysGammaSpace</c>）を切り替えても、検証ビューの面の色が
    /// 組み込みの UI シェーダで描いた同じ色の四角と揃うこと（パッケージの README §5 S9）。
    ///
    /// <para>
    /// 適合検査は Canvas を通らずに uniform を自分で入れるので、シェーダが uniform を読むことしか確かめない。Canvas が組み込みでないシェーダにも
    /// uniform を入れることは文書に無いので、ここで Canvas を通して描いて確かめる。Screen Space - Camera の Canvas に、左から組み込みの UI シェーダの
    /// 四角・変換を持たないシェーダの四角（対照）・検証ビューの「■」を並べ、カメラで RenderTexture へ描いて、各列の最も明るい画素（面の奥）を比べる。
    /// </para>
    /// <para>
    /// 対照は、Canvas がこの環境で設定を反映していることを示す——反映していなければ、揃うことは何も確かめていない。描画装置が要るので、
    /// 無い環境（batchmode の -nographics など）では Ignore になる。
    /// </para>
    /// </summary>
    [TestFixture]
    public class T2FontTextColorSpaceTests
    {
        private const int Width = 384;
        private const int Height = 128;
        private const float Gray = 0.5f;
        private const float Tolerance = 0.02f;

        private readonly List<CanvasRenderer> _quads = new List<CanvasRenderer>();
        private GameObject _camera;
        private GameObject _canvas;
        private RenderTexture _target;
        private Mesh _quad;
        private Material _control;
        private T2FontText _text;

        [SetUp]
        public void SetUp()
        {
            if (!GlyphConformance.CanRender)
            {
                Assert.Ignore("描画装置が無い（batchmode の -nographics など）ので、Canvas を通した描画を確かめられません。");
            }
            var controlShader = Shader.Find("Sprites/Default");
            if (controlShader == null)
            {
                Assert.Inconclusive("対照のシェーダ Sprites/Default が見つかりません。");
            }

            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _camera = new GameObject("T2FontTextColorSpaceTests Camera", typeof(Camera)) { hideFlags = HideFlags.DontSave };
            var camera = _camera.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            // Canvas の大きさは描画先の画素数で決まる（Screen Space - Camera はカメラの pixelRect に合わせる）。
            camera.targetTexture = _target;

            _canvas = new GameObject("T2FontTextColorSpaceTests Canvas", typeof(RectTransform), typeof(Canvas)) { hideFlags = HideFlags.DontSave };
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;

            _quad = CreateQuad(24f);
            _control = new Material(controlShader) { hideFlags = HideFlags.HideAndDontSave };
            // Image が既定で描くマテリアル（組み込みの UI シェーダ UI/Default）。
            AddQuad("Reference", 1f / 6f, Canvas.GetDefaultCanvasMaterial());
            AddQuad("Control", 3f / 6f, _control);

            var text = new GameObject("Text", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            var rect = (RectTransform)text.transform;
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(5f / 6f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);
            _text = text.AddComponent<T2FontText>();
            _text.Font = TestFontAssets.Mplus1cSdf();
            _text.Text = "■";
            _text.FontSize = 64f;
            _text.FaceColor = new Color(Gray, Gray, Gray);
        }

        [TearDown]
        public void TearDown()
        {
            _quads.Clear();
            if (_canvas != null)
            {
                Object.DestroyImmediate(_canvas);
            }
            if (_camera != null)
            {
                Object.DestroyImmediate(_camera);
            }
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
            }
            if (_quad != null)
            {
                Object.DestroyImmediate(_quad);
            }
            if (_control != null)
            {
                Object.DestroyImmediate(_control);
            }
        }

        [Test]
        public void Canvasの設定を切り替えても面の色が組み込みのUIシェーダの四角と揃う()
        {
            var off = Render(false);
            var on = Render(true);

            Assert.That(off.Reference.r, Is.GreaterThan(0.05f), "組み込みの UI シェーダの四角が描かれていません。");
            Assert.That(off.Face.r, Is.GreaterThan(0.05f), "検証ビューの字が描かれていません。");
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && on.Control.r < off.Control.r + 0.1f)
            {
                Assert.Inconclusive(
                    $"対照（変換を持たないシェーダ）の色が設定で変わりません（無効 {off.Control.r:0.000}・有効 {on.Control.r:0.000}）。"
                    + "Canvas がこの環境で設定を反映していないので、揃うかを確かめられません。");
            }

            Assert.That(on.Reference.r, Is.EqualTo(off.Reference.r).Within(Tolerance), "組み込みの UI シェーダの四角の色が設定で変わりました。");
            Assert.That(off.Face.r, Is.EqualTo(off.Reference.r).Within(Tolerance), "設定が無効のとき、面の色が組み込みの UI シェーダの四角と違います。");
            Assert.That(on.Face.r, Is.EqualTo(on.Reference.r).Within(Tolerance),
                "設定が有効のとき、面の色が組み込みの UI シェーダの四角と違います（Canvas が _UIVertexColorAlwaysGammaSpace を入れていないか、シェーダが直していない）。");
        }

        private Columns Render(bool vertexColorAlwaysGammaSpace)
        {
            _canvas.GetComponent<Canvas>().vertexColorAlwaysGammaSpace = vertexColorAlwaysGammaSpace;
            // 設定を変えた後のバッチを作らせるため、全レンダラの幾何を入れ直す。
            _text.Rebuild();
            foreach (var renderer in _quads)
            {
                renderer.SetMesh(_quad);
            }
            Canvas.ForceUpdateCanvases();

            var camera = _camera.GetComponent<Camera>();
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                camera.Render();
            }
            else
            {
                var request = new RenderPipeline.StandardRequest { destination = _target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    Assert.Inconclusive("描画パイプラインがカメラ 1 台の描画の要求（StandardRequest）に応じません。");
                }
                RenderPipeline.SubmitRenderRequest(camera, request);
            }

            var readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();
                var pixels = readback.GetPixels();
                return new Columns(
                    Brightest(pixels, 0, Width / 3),
                    Brightest(pixels, Width / 3, 2 * Width / 3),
                    Brightest(pixels, 2 * Width / 3, Width));
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }

        private void AddQuad(string name, float anchorX, Material material)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)) { hideFlags = HideFlags.DontSave };
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.sizeDelta = Vector2.zero;

            var renderer = go.GetComponent<CanvasRenderer>();
            // materialCount を先に入れないと SetMaterial が効かず、何も描かれない。
            renderer.materialCount = 1;
            renderer.SetMaterial(material, 0);
            renderer.SetTexture(Texture2D.whiteTexture);
            renderer.SetColor(new Color(Gray, Gray, Gray, 1f));
            _quads.Add(renderer);
        }

        // 中心を原点とする一辺 2 × half の四角。頂点色は白（色は CanvasRenderer の色で掛ける）。
        private static Mesh CreateQuad(float half)
        {
            return new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[] { new Vector3(-half, -half), new Vector3(-half, half), new Vector3(half, half), new Vector3(half, -half) },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 1, 2, 2, 3, 0 },
            };
        }

        // 列の範囲で最も明るい画素。面の奥は被覆が 1 なので、透明の背景に混ざらない面の色そのものになる。
        private static Color Brightest(Color[] pixels, int xMin, int xMax)
        {
            var best = Color.clear;
            for (var y = 0; y < Height; y++)
            {
                for (var x = xMin; x < xMax; x++)
                {
                    var c = pixels[(y * Width) + x];
                    if (c.r + c.g + c.b > best.r + best.g + best.b)
                    {
                        best = c;
                    }
                }
            }
            return best;
        }

        private readonly struct Columns
        {
            public Columns(Color reference, Color control, Color face)
            {
                Reference = reference;
                Control = control;
                Face = face;
            }

            public Color Reference { get; }

            public Color Control { get; }

            public Color Face { get; }
        }
    }
}
