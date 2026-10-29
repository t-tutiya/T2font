#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace info.someiyoshino.T2Font.Viewer
{
    /// <summary>
    /// 検証ビュー（README §10）。文字列 1 本を、字ごとに 1 つの <c>CanvasRenderer</c> で描く。フォントとエッジスタイル（README §6.1）を Inspector で替えられる。
    ///
    /// <para>
    /// 1 字 1 レンダラにしてあるのは、テクスチャとマテリアルの束縛・クリップ・α の扱いを、利用側の一般的なテキスト描画と同じ条件で試すため。
    /// 表現の具体型を知らないホストの最小の実装でもある（README §4.1）: グリフインデックスを引き、表現に幾何を書かせ、テクスチャとマテリアルを貼るだけ。
    /// 行送りは改行（U+000A）だけで、折り返し・カーニング・縦書きは持たない。
    /// </para>
    ///
    /// <para>
    /// 字の GameObject・Mesh・マテリアルは実行時に作る物で、シーンに保存しない（<c>HideFlags.DontSave</c>）。値が変わるか有効に戻ると
    /// 次の <c>LateUpdate</c> で作り直し、無効になると描画を消してマテリアルを捨てる。Mesh とマテリアルはネイティブのオブジェクトで GC では
    /// 回収されないので、明示的に破棄する。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class T2FontText : MonoBehaviour
    {
        [SerializeField] private T2FontAsset? font;
        [SerializeField, TextArea(2, 6)] private string text = "T2Font 永あア";
        [SerializeField, Min(1f)] private float fontSize = 48f;

        [Header("面（頂点色）")]
        [SerializeField, ColorUsage(false)] private Color faceColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float alpha = 1f;

        [Header("縁（em）")]
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(GlyphEdgeStyle.FaceDilateMin, GlyphEdgeStyle.FaceDilateMax)] private float faceDilate;
        [SerializeField, Range(0f, GlyphEdgeStyle.FaceSoftnessMax)] private float faceSoftness;
        [SerializeField, Range(0f, GlyphEdgeStyle.OutlineWidthMax)] private float outlineWidth;
        [SerializeField, Range(0f, GlyphEdgeStyle.OutlineSoftnessMax)] private float outlineSoftness;

        [Header("クリップ")]
        [SerializeField] private bool clipToRect;

        private readonly GlyphGeometry _geometry = new GlyphGeometry();
        private readonly List<GameObject> _glyphObjects = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly Vector3[] _corners = new Vector3[4];
        private GlyphMaterialCache? _materials;
        private RectTransform? _rectTransform;
        private bool _dirty = true;

        /// <summary>描くフォント。</summary>
        public T2FontAsset? Font
        {
            get => font;
            set
            {
                font = value;
                _dirty = true;
            }
        }

        /// <summary>描く文字列（改行で行を送る）。</summary>
        public string Text
        {
            get => text;
            set
            {
                text = value ?? string.Empty;
                _dirty = true;
            }
        }

        /// <summary>表示サイズ（1em の px）。</summary>
        public float FontSize
        {
            get => fontSize;
            set
            {
                fontSize = value;
                _dirty = true;
            }
        }

        /// <summary>面の色（頂点色の rgb・README §5 S2）。</summary>
        public Color FaceColor
        {
            get => faceColor;
            set
            {
                faceColor = value;
                _dirty = true;
            }
        }

        /// <summary>フェード（頂点色の α・README §5 S2）。</summary>
        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = value;
                _dirty = true;
            }
        }

        /// <summary>エッジスタイル（README §6.1）。</summary>
        public GlyphEdgeStyle EdgeStyle
        {
            get => new GlyphEdgeStyle(
                outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a, faceDilate, faceSoftness, outlineWidth, outlineSoftness);
            set
            {
                outlineColor = new Color(value.ColorR, value.ColorG, value.ColorB, value.ColorA);
                faceDilate = value.FaceDilate;
                faceSoftness = value.FaceSoftness;
                outlineWidth = value.OutlineWidth;
                outlineSoftness = value.OutlineSoftness;
                _dirty = true;
            }
        }

        /// <summary>矩形（自分の RectTransform）で字をクリップするか。</summary>
        public bool ClipToRect
        {
            get => clipToRect;
            set
            {
                clipToRect = value;
                _dirty = true;
            }
        }

        /// <summary>描いている字の GameObject（インクのある字だけ・文字列の順）。検査用。</summary>
        public IReadOnlyList<GameObject> GlyphObjects => _glyphObjects;

        private RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = (RectTransform)transform);

        /// <summary>今の設定で字を作り直す（通常は値を変えた後の <c>LateUpdate</c> が呼ぶ）。</summary>
        public void Rebuild()
        {
            _dirty = false;
            ClearGlyphs();
            if (font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var style = EdgeStyle;
            var representation = font.Representation;
            var problem = GlyphMaterialCache.Check(representation, style);
            if (problem != null)
            {
                // 描けないエッジスタイルを黙って近似しない（README §6.3）。
                Debug.LogError($"[T2FontText] '{font.name}': {problem}", this);
                return;
            }
            _materials ??= new GlyphMaterialCache();

            var rect = RectTransform.rect;
            var face = font.Face;
            var lineStep = (face.ascender - face.descender + face.lineGap) * fontSize;
            var pen = new Vector2(rect.xMin, rect.yMax - (face.ascender * fontSize));
            Rect? clip = clipToRect ? CanvasRect() : null;

            for (var i = 0; i < text.Length;)
            {
                var codepoint = TextCodepoints.Read(text, i, out var length);
                i += length;
                if (codepoint == '\n')
                {
                    pen = new Vector2(rect.xMin, pen.y - lineStep);
                    continue;
                }

                var glyph = font.Resolve(codepoint, out _);
                var metrics = font.Metrics(glyph);
                var glyphRepresentation = font.RepresentationOf(glyph);
                _geometry.Clear();
                glyphRepresentation.WriteGeometry(glyph, metrics, _geometry);
                if (_geometry.VertexCount > 0)
                {
                    CreateGlyph(codepoint, pen, glyphRepresentation, glyph, style, clip);
                }
                pen.x += metrics.advance * fontSize;
            }
        }

        private void CreateGlyph(int codepoint, Vector2 pen, GlyphRepresentation representation, int glyph, in GlyphEdgeStyle style, Rect? clip)
        {
            var go = new GameObject($"U+{codepoint:X4}", typeof(RectTransform), typeof(CanvasRenderer))
            {
                hideFlags = HideFlags.DontSave,
                layer = gameObject.layer,
            };
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.pivot = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localPosition = new Vector3(pen.x, pen.y, 0f);
            _glyphObjects.Add(go);

            var mesh = new Mesh { name = $"T2FontText U+{codepoint:X4}", hideFlags = HideFlags.DontSave };
            _geometry.CopyTo(mesh, fontSize);
            _meshes.Add(mesh);

            var renderer = go.GetComponent<CanvasRenderer>();
            // materialCount を先に入れないと SetMaterial が効かず、何も描かれない。
            renderer.materialCount = 1;
            renderer.SetMaterial(_materials!.Get(representation, style), 0);
            renderer.SetTexture(representation.GetTexture(glyph));
            // 面の色は頂点色の rgb、フェードは頂点色の α（README §5 S2）。CanvasRenderer の色はバッチの鍵に入らない。
            renderer.SetColor(new Color(faceColor.r, faceColor.g, faceColor.b, alpha));
            if (clip is Rect r)
            {
                renderer.EnableRectClipping(r);
            }
            // SetMesh は中身を Canvas 側へ写すので、詰め終えてから最後に呼ぶ。
            renderer.SetMesh(mesh);
        }

        // 矩形クリップの矩形は最上位の Canvas の空間で渡す（RectMask2D と同じ取り方）。Canvas の下に無ければクリップしない。
        private Rect? CanvasRect()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return null;
            }
            var root = canvas.rootCanvas.transform;
            RectTransform.GetWorldCorners(_corners);
            var min = root.InverseTransformPoint(_corners[0]);
            var max = root.InverseTransformPoint(_corners[2]);
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        private void ClearGlyphs()
        {
            foreach (var go in _glyphObjects)
            {
                Discard(go);
            }
            _glyphObjects.Clear();
            foreach (var mesh in _meshes)
            {
                Discard(mesh);
            }
            _meshes.Clear();

            // 複製などで持ち主の一覧の外に残った字（保存しない子）も捨てる。
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if ((child.hideFlags & HideFlags.DontSave) != 0)
                {
                    Discard(child);
                }
            }
        }

        private static void Discard(Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void OnDisable()
        {
            // 無効化・破棄の途中で子の GameObject を破棄すると、Unity が拒むか二重の破棄の誤りを出す。ここでは描画だけを消し、
            // 字の GameObject は有効に戻った後の LateUpdate で作り直すときに捨てる（GameObject ごと破棄されるなら子も一緒に消える）。
            foreach (var go in _glyphObjects)
            {
                if (go != null)
                {
                    go.GetComponent<CanvasRenderer>().Clear();
                }
            }
            _materials?.Dispose();
            _materials = null;
            _dirty = true;
        }

        private void OnDestroy()
        {
            // Mesh は階層に属さず、誰も破棄しないので、ここで捨てる。
            foreach (var mesh in _meshes)
            {
                Discard(mesh);
            }
            _meshes.Clear();
        }

        private void OnValidate()
        {
            _dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty)
            {
                Rebuild();
            }
        }
    }
}
