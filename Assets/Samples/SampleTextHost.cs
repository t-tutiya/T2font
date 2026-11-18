#nullable enable

using System.Collections.Generic;
using info.someiyoshino.T2Font;
using UnityEngine;

namespace info.someiyoshino.T2font.Samples
{
    /// <summary>
    /// 利用の手引き「自分のホストの書き方」（<c>docs/manual/02-host-guide.md</c>）のコード例。文字列を、字ごとに 1 つの <c>CanvasRenderer</c> で描く
    /// ホストで、フォントを使い始める時点でマテリアルを用意し、字の配置と描画を分け、フェードは α だけを変えて幾何を作り直さない。
    ///
    /// <para>
    /// 手引きは <c>manual:begin</c> と <c>manual:end</c> の印の間を、ジェネレーター（<c>Tests/ManualSnippetDocTests.cs</c>）で写す（D-USERDOCS）。
    /// 印の名前を変えたら、手引きの <c>gen:</c> の名前も同じ変更で直す。Play Mode でだけ動く（Edit Mode で見るなら検証ビュー <c>T2FontText</c>）。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SampleTextHost : MonoBehaviour
    {
        [SerializeField] private T2FontAsset? font;
        [SerializeField, TextArea(2, 6)] private string text = "T2Font 永あア";
        [SerializeField, Min(1f)] private float fontSize = 48f;
        [SerializeField] private Color faceColor = Color.white;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, GlyphEdgeStyle.OutlineWidthMax)] private float outlineWidth = 0.05f;

        private readonly GlyphGeometry _geometry = new GlyphGeometry();
        private readonly List<PlacedGlyph> _placed = new List<PlacedGlyph>();
        private readonly List<GlyphView> _views = new List<GlyphView>();
        private GlyphMaterialCache? _materials;
        private T2FontAsset? _loadedFont;
        private GlyphEdgeStyle _loadedStyle;
        private bool _dirty = true;

        /// <summary>字 1 つの配置（レイアウトの結果）。<see cref="Pen"/> は行の左上を原点にしたペン原点（px・y 上向き）。</summary>
        private readonly struct PlacedGlyph
        {
            public PlacedGlyph(int codepoint, int glyph, Vector2 pen)
            {
                Codepoint = codepoint;
                Glyph = glyph;
                Pen = pen;
            }

            public int Codepoint { get; }

            public int Glyph { get; }

            public Vector2 Pen { get; }
        }

        /// <summary>字を描く GameObject・レンダラ・Mesh の組。作り直さずに使い回す。</summary>
        private sealed class GlyphView
        {
            public GlyphView(RectTransform transform, CanvasRenderer renderer, Mesh mesh)
            {
                Transform = transform;
                Renderer = renderer;
                Mesh = mesh;
            }

            public RectTransform Transform { get; }

            public CanvasRenderer Renderer { get; }

            public Mesh Mesh { get; }
        }

        /// <summary>描く文字列。</summary>
        public string Text
        {
            get => text;
            set
            {
                text = value ?? string.Empty;
                _dirty = true;
            }
        }

        // manual:begin host-load
        /// <summary>
        /// フォントを使い始める時点で 1 回呼ぶ。エッジスタイルをフォントのグリフ表現で描けるかを確かめ、マテリアルを先に作る
        /// （描画の途中で作るとヒッチになる・README §4.5）。描けなければ誤りを出して false を返す。
        /// </summary>
        private bool LoadFont(T2FontAsset loaded, GlyphEdgeStyle style)
        {
            var representation = loaded.Representation;
            var problem = GlyphMaterialCache.Check(representation, style);
            if (problem != null)
            {
                // 描けないエッジスタイルを黙って近似しない（README §6.3）。
                Debug.LogError($"[SampleTextHost] '{loaded.name}': {problem}", this);
                return false;
            }
            _materials ??= new GlyphMaterialCache();
            _materials.Prewarm(representation, new[] { style });
            return true;
        }
        // manual:end host-load

        // manual:begin host-layout
        /// <summary>
        /// 字を並べる（描画から独立した計算）。ペンは 1 行目の左端・ベースラインから始め、字ごとに送り幅だけ進める。
        /// タイポグラフィテーブルの値は em なので、表示サイズ（1em の px）を掛ける。
        /// </summary>
        private static void Layout(T2FontAsset font, string text, float fontSize, List<PlacedGlyph> placed)
        {
            placed.Clear();
            var face = font.Face;
            var lineStep = (face.ascender - face.descender + face.lineGap) * fontSize;
            var pen = new Vector2(0f, -face.ascender * fontSize);
            for (var i = 0; i < text.Length;)
            {
                // サロゲートペアは 1 字として読む。
                var codepoint = TextCodepoints.Read(text, i, out var length);
                i += length;
                if (codepoint == '\n')
                {
                    pen = new Vector2(0f, pen.y - lineStep);
                    continue;
                }
                // 収録していない字は代替グリフ（.notdef）になり、字ごとに 1 回 LogError が出る（README §3.1）。
                var glyph = font.Resolve(codepoint, out _);
                placed.Add(new PlacedGlyph(codepoint, glyph, pen));
                pen.x += font.Metrics(glyph).advance * fontSize;
            }
        }
        // manual:end host-layout

        // manual:begin host-geometry
        /// <summary>字 1 つの幾何を書いて Mesh へ詰める。インクの無い字（空白）には何も書かれないので false を返す。</summary>
        private bool WriteMesh(T2FontAsset font, in PlacedGlyph placed, in GlyphEdgeStyle style, Mesh mesh)
        {
            var representation = font.RepresentationOf(placed.Glyph);
            _geometry.Clear();
            // 幾何はインク境界をこの幅だけ広げた範囲を覆う。エッジスタイルか表示サイズを変えたら書き直す（README §4.3）。
            representation.WriteGeometry(placed.Glyph, font.Metrics(placed.Glyph), style.QuadExtentEm(fontSize), _geometry);
            if (_geometry.VertexCount == 0)
            {
                return false;
            }
            // 位置は em × 表示サイズ。UV0.zw（頂点の em 座標）は器が書く（README §4.2）。
            _geometry.CopyTo(mesh, fontSize);
            return true;
        }
        // manual:end host-geometry

        // manual:begin host-bind
        /// <summary>字のレンダラへ、マテリアル・テクスチャ・色・幾何を渡す。</summary>
        private void Bind(CanvasRenderer renderer, T2FontAsset font, in PlacedGlyph placed, in GlyphEdgeStyle style, Mesh mesh)
        {
            var representation = font.RepresentationOf(placed.Glyph);
            // materialCount を先に入れないと SetMaterial が効かず、何も描かれない。
            renderer.materialCount = 1;
            renderer.SetMaterial(_materials!.Get(representation, style), 0);
            // テクスチャは字ごとに引く（ページが複数あるフォントでは、字によって違う）。
            renderer.SetTexture(representation.GetTexture(placed.Glyph));
            // 面の色は頂点色の rgb（README §5 S2）。CanvasRenderer の色はバッチのキーに入らない。
            renderer.SetColor(faceColor);
            // SetMesh は中身を Canvas 側へ写す。詰め終えてから呼び、後で Mesh を書き換えたら SetMesh し直す。
            renderer.SetMesh(mesh);
        }
        // manual:end host-bind

        // manual:begin host-fade
        /// <summary>フェード（0〜1）。α は幾何と独立に変えられるので、Mesh を作り直さない（README §5 S2）。</summary>
        public void SetFade(float alpha)
        {
            foreach (var view in _views)
            {
                view.Renderer.SetAlpha(alpha);
            }
        }
        // manual:end host-fade

        /// <summary>今の設定で字を並べ直し、描き直す。</summary>
        public void Rebuild()
        {
            _dirty = false;
            if (font == null)
            {
                Hide(0);
                return;
            }

            var style = new GlyphEdgeStyle(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a, 0f, 0f, outlineWidth, 0f);
            if (font != _loadedFont || style != _loadedStyle)
            {
                ReleaseFont();
                if (!LoadFont(font, style))
                {
                    Hide(0);
                    return;
                }
                _loadedFont = font;
                _loadedStyle = style;
            }

            Layout(font, text, fontSize, _placed);
            var rect = ((RectTransform)transform).rect;
            var topLeft = new Vector2(rect.xMin, rect.yMax);
            var used = 0;
            foreach (var placed in _placed)
            {
                if (used == _views.Count)
                {
                    _views.Add(CreateView());
                }
                var view = _views[used];
                if (!WriteMesh(font, placed, _loadedStyle, view.Mesh))
                {
                    continue;
                }
                view.Transform.localPosition = topLeft + placed.Pen;
                Bind(view.Renderer, font, placed, _loadedStyle, view.Mesh);
                used++;
            }
            Hide(used);
        }

        private GlyphView CreateView()
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer))
            {
                layer = gameObject.layer,
            };
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            return new GlyphView(rt, go.GetComponent<CanvasRenderer>(), new Mesh { name = "SampleTextHost glyph" });
        }

        // 使わなくなった字のレンダラの描画を消す（GameObject と Mesh は次に使うまで残す）。
        private void Hide(int firstUnused)
        {
            for (var i = firstUnused; i < _views.Count; i++)
            {
                _views[i].Renderer.Clear();
            }
        }

        // manual:begin host-release
        /// <summary>フォントをやめるときに、そのフォントのマテリアルを捨てる。</summary>
        private void ReleaseFont()
        {
            if (_loadedFont != null && _materials != null)
            {
                var scope = _loadedFont.Representation.MaterialScope;
                if (scope != null)
                {
                    // スコープがシェーダのグリフ表現（SDF）では、同じシェーダのほかのフォントのマテリアルも捨てる（次の Get で作り直される）。
                    _materials.Release(scope);
                }
            }
            _loadedFont = null;
        }

        private void OnDestroy()
        {
            // Mesh とマテリアルはネイティブのオブジェクトで、参照を手放しても GC では回収されない。
            foreach (var view in _views)
            {
                Destroy(view.Mesh);
            }
            _views.Clear();
            _materials?.Dispose();
            _materials = null;
        }
        // manual:end host-release

        private void OnValidate()
        {
            _dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            _dirty = true;
        }

        private void Update()
        {
            if (_dirty)
            {
                Rebuild();
            }
        }
    }
}
