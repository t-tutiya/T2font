#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace info.someiyoshino.T2Font.Conformance
{
    /// <summary>
    /// 表現の適合検査（README §10）。表現を足したら、その表現でベイクしたフォントを渡して通す。検査は誤りの文面の一覧を返し、空なら適合
    /// ——テストの枠組みに依らないので、どのパッケージのテストからも呼べる。
    ///
    /// <list type="bullet">
    /// <item><see cref="CheckGeometry"/>: 全グリフの幾何（README §4.2・§4.3）。描画装置は要らない。</item>
    /// <item><see cref="CheckRendering"/>: シェーダの契約（README §5 の S2・S3・S4・S6・S9 と矩形クリップ）を、実際に描いた画素で確かめる。
    /// 描画装置が要る（<see cref="CanRender"/>）。</item>
    /// </list>
    ///
    /// <para>
    /// 描画は <c>CommandBuffer</c> で透明にクリアした RenderTexture へ直接描く（Canvas もカメラも使わない）ので、描画パイプラインに依らない。
    /// 矩形クリップは Canvas と同じくキーワード <c>UNITY_UI_CLIP_RECT</c> と <c>_ClipRect</c> を、頂点色の色空間は Canvas と同じく
    /// <c>_UIVertexColorAlwaysGammaSpace</c> を渡して試す。画素の判定は列の数え方だけで行い、
    /// 上下の向き（プラットフォームによって RenderTexture の y が反転する）に依らない。
    /// </para>
    /// </summary>
    public static class GlyphConformance
    {
        private const int MaxProblems = 20;
        private const float Epsilon = 1e-5f;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ClipRectId = Shader.PropertyToID("_ClipRect");
        private static readonly int VertexColorAlwaysGammaSpaceId = Shader.PropertyToID("_UIVertexColorAlwaysGammaSpace");

        /// <summary>描画の検査を回せるか（描画装置の無い batchmode では回せない）。</summary>
        public static bool CanRender => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        /// <summary>
        /// 描けると宣言した効果（README §6.3）がインク境界から外へ描かれうる最大の距離（em・README §6.2）。全効果なら
        /// <see cref="GlyphEdgeStyle.MaxOuterExtentEm"/>。面の縁のぼかしは縁取りの帯の内側に収まるので、縁取りとの大きい方を取る。
        /// </summary>
        public static float OuterExtentEm(GlyphEffects effects)
        {
            var dilate = (effects & GlyphEffects.FaceDilate) != 0 ? GlyphEdgeStyle.FaceDilateMax : 0f;
            var outline = ((effects & GlyphEffects.Outline) != 0 ? GlyphEdgeStyle.OutlineWidthMax : 0f)
                + ((effects & GlyphEffects.OutlineSoftness) != 0 ? GlyphEdgeStyle.OutlineSoftnessMax / 2f : 0f);
            var face = (effects & GlyphEffects.FaceSoftness) != 0 ? GlyphEdgeStyle.FaceSoftnessMax / 2f : 0f;
            return dilate + Math.Max(outline, face);
        }

        /// <summary>
        /// 全グリフの幾何を検査する（README §4.2・§4.3）: インクの無いグリフは何も書かない・インクのあるグリフは三角形を書きテクスチャを持つ・
        /// UV0.zw が頂点の em 座標と一致する・頂点の境界ボックスがインク境界を効果の外延（<see cref="OuterExtentEm"/>）だけ広げた範囲を覆う。
        /// </summary>
        /// <remarks>覆いは頂点の境界ボックスで見る（quad なら十分。三角形の和が範囲を隙間なく覆うかまでは見ない）。</remarks>
        public static IReadOnlyList<string> CheckGeometry(T2FontAsset font)
        {
            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            var problems = new List<string>();
            var geometry = new GlyphGeometry();
            var failed = 0;
            for (var glyph = 0; glyph < font.GlyphCount; glyph++)
            {
                var problem = CheckGlyphGeometry(font, glyph, geometry);
                if (problem == null)
                {
                    continue;
                }
                failed++;
                if (problems.Count < MaxProblems)
                {
                    problems.Add(problem);
                }
            }
            if (failed > problems.Count)
            {
                problems.Add($"ほか {failed - problems.Count} グリフ");
            }
            return problems;
        }

        /// <summary>
        /// シェーダの契約を描いた画素で検査する。<paramref name="codepoint"/> は、インク部が字の中央の縦の列を上下の水平な縁で横切る、塗り
        /// つぶしの字（「■」U+25A0 など）にする——縁取りの幅と AA の幅を、その列の画素を数えて測る。
        /// <list type="bullet">
        /// <item>S2・S6: 面の色は頂点色の rgb、フェードは頂点色の α で、透明にクリアした先へ描いても α は 1 回だけ掛かる。</item>
        /// <item>S4・S2: 縁取りが宣言どおりの幅で縁の色に塗られ、フェードが縁取りにも同じだけ掛かる（表現が縁取りを描けるときだけ）。</item>
        /// <item>S3: AA の幅が表示サイズによらず画面の約 1px。</item>
        /// <item>S9: <c>_UIVertexColorAlwaysGammaSpace</c> が立てば、Linear のプロジェクトでは頂点色の rgb を linear に直し、立たなければ直さない。</item>
        /// <item>矩形クリップ: <c>UNITY_UI_CLIP_RECT</c> と <c>_ClipRect</c> で矩形の外が透明になる。</item>
        /// </list>
        /// </summary>
        /// <exception cref="InvalidOperationException">描画装置が無い（<see cref="CanRender"/> が偽）。</exception>
        public static IReadOnlyList<string> CheckRendering(T2FontAsset font, int codepoint)
        {
            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }
            if (!CanRender)
            {
                throw new InvalidOperationException("描画装置が無いので描画の検査を回せません。");
            }

            var problems = new List<string>();
            if (!font.Contains(codepoint))
            {
                problems.Add($"フォント '{font.name}' に検査の字 U+{codepoint:X4} がありません。");
                return problems;
            }
            var glyph = font.Resolve(codepoint, out _);
            var representation = font.RepresentationOf(glyph);
            var metrics = font.Metrics(glyph);

            using (var materials = new GlyphMaterialCache())
            {
                var plain = materials.Get(representation, GlyphEdgeStyle.None);

                // S2・S6: 頂点色 (1, 0, 0, 0.5)。透明の上へ描いた画素は premultiplied の (0.5, 0, 0, 0.5) になる。
                var faded = Render(representation, glyph, metrics, plain, 128f, new Color(1f, 0f, 0f, 0.5f), null, false);
                var peak = faded.Peak();
                if (Math.Abs(peak.a - 0.5f) > 0.02f)
                {
                    problems.Add(Invariant($"S2・S6: 頂点色の α 0.5 で描いた面の α が {peak.a:0.000} です（期待 0.5。α が二重に掛かると 0.25）。"));
                }
                else if (!Near(Straight(peak), new Color(1f, 0f, 0f), 0.04f))
                {
                    problems.Add($"S2: 頂点色 (1, 0, 0) で描いた面の色が {Describe(Straight(peak))} です。");
                }

                CheckOutline(problems, representation, glyph, metrics, materials);
                CheckAntialiasing(problems, representation, glyph, metrics, plain);
                CheckVertexColorSpace(problems, representation, glyph, metrics, plain);

                // 矩形クリップ: 矩形が字から遠ければ何も描かれず、字を覆えば描かれる。
                var outside = Render(representation, glyph, metrics, plain, 128f, Color.white, new Rect(1e5f, 1e5f, 1f, 1f), false);
                if (outside.Peak().a > 1f / 255f)
                {
                    problems.Add("UNITY_UI_CLIP_RECT: 矩形の外の画素が描かれています。");
                }
                var inside = Render(representation, glyph, metrics, plain, 128f, Color.white, new Rect(-1e5f, -1e5f, 2e5f, 2e5f), false);
                if (inside.Peak().a < 0.99f)
                {
                    problems.Add("UNITY_UI_CLIP_RECT: 字を覆う矩形でクリップすると、面が描かれません。");
                }
            }
            return problems;
        }

        private static string? CheckGlyphGeometry(T2FontAsset font, int glyph, GlyphGeometry geometry)
        {
            var representation = font.RepresentationOf(glyph);
            var metrics = font.Metrics(glyph);
            geometry.Clear();
            representation.WriteGeometry(glyph, metrics, geometry);

            if (!metrics.HasInk)
            {
                return geometry.VertexCount == 0 ? null : $"{Name(font, glyph)}: インクの無いグリフが幾何（頂点 {geometry.VertexCount}）を書きました。";
            }
            if (geometry.VertexCount == 0 || geometry.IndexCount == 0)
            {
                return $"{Name(font, glyph)}: インクのあるグリフが幾何を書きませんでした。";
            }
            if (representation.GetTexture(glyph) == null)
            {
                return $"{Name(font, glyph)}: インクのあるグリフのテクスチャが null です。";
            }

            float left = float.MaxValue, bottom = float.MaxValue, right = float.MinValue, top = float.MinValue;
            for (var i = 0; i < geometry.VertexCount; i++)
            {
                var p = geometry.Positions[i];
                var uv = geometry.Uv0[i];
                if (uv.z != p.x || uv.w != p.y)
                {
                    return $"{Name(font, glyph)}: UV0.zw {Describe(uv.z, uv.w)} が頂点の em 座標 {Describe(p.x, p.y)} と違います（README §4.2）。";
                }
                left = Math.Min(left, p.x);
                bottom = Math.Min(bottom, p.y);
                right = Math.Max(right, p.x);
                top = Math.Max(top, p.y);
            }

            var extent = OuterExtentEm(representation.Effects);
            if (left > metrics.inkLeft - extent + Epsilon || bottom > metrics.inkBottom - extent + Epsilon
                || right < metrics.inkRight + extent - Epsilon || top < metrics.inkTop + extent - Epsilon)
            {
                return Invariant(
                    $"{Name(font, glyph)}: 幾何 [{left:0.####}, {bottom:0.####}, {right:0.####}, {top:0.####}] がインク境界 ")
                    + Invariant($"[{metrics.inkLeft:0.####}, {metrics.inkBottom:0.####}, {metrics.inkRight:0.####}, {metrics.inkTop:0.####}] ")
                    + Invariant($"を外延 {extent:0.####}em だけ広げた範囲を覆いません（README §4.3）。");
            }
            return null;
        }

        // S4・S2: 縁取りの帯は中央の列で上下 2 本、それぞれ幅 OutlineWidthMax × 表示サイズの画素になる。
        private static void CheckOutline(
            List<string> problems, GlyphRepresentation representation, int glyph, in GlyphMetrics metrics, GlyphMaterialCache materials)
        {
            if ((representation.Effects & GlyphEffects.Outline) == 0)
            {
                return;
            }

            const float size = 200f;
            var style = new GlyphEdgeStyle(0f, 1f, 0f, 1f, 0f, 0f, GlyphEdgeStyle.OutlineWidthMax, 0f);
            var material = materials.Get(representation, style);
            var image = Render(representation, glyph, metrics, material, size, new Color(1f, 0f, 0f, 1f), null, false);
            var green = 0;
            var red = 0;
            foreach (var c in image.CenterColumn())
            {
                if (c.a > 0.9f)
                {
                    var s = Straight(c);
                    green += Near(s, new Color(0f, 1f, 0f), 0.1f) ? 1 : 0;
                    red += Near(s, new Color(1f, 0f, 0f), 0.1f) ? 1 : 0;
                }
            }
            var expected = 2f * GlyphEdgeStyle.OutlineWidthMax * size;
            if (Math.Abs(green - expected) > 4f)
            {
                problems.Add(Invariant($"S4: 縁取りの幅 {GlyphEdgeStyle.OutlineWidthMax}em を {size}px で描いた帯が中央の列で {green}px です（期待 約 {expected:0}px）。"));
            }
            if (red == 0)
            {
                problems.Add("S4: 縁取りを付けると面（頂点色）が描かれません。");
            }

            var faded = Render(representation, glyph, metrics, material, size, new Color(1f, 0f, 0f, 0.5f), null, false);
            if (Math.Abs(faded.Peak().a - 0.5f) > 0.02f)
            {
                problems.Add(Invariant($"S2: 頂点色の α 0.5 で描いた縁取り付きの字の α が {faded.Peak().a:0.000} です（フェードは面と縁取りに 1 回だけ掛かる）。"));
            }
        }

        // S3: 中央の列の上下の縁の、半端な被覆の画素の数。AA が画面の 1px なら、縁 1 本で 2 画素以内に収まる。
        private static void CheckAntialiasing(
            List<string> problems, GlyphRepresentation representation, int glyph, in GlyphMetrics metrics, Material material)
        {
            foreach (var size in new[] { 24f, 200f })
            {
                var image = Render(representation, glyph, metrics, material, size, Color.white, null, false);
                var partial = 0;
                var solid = 0;
                foreach (var c in image.CenterColumn())
                {
                    if (c.a >= 0.96f)
                    {
                        solid++;
                    }
                    else if (c.a > 0.04f)
                    {
                        partial++;
                    }
                }
                if (solid == 0)
                {
                    problems.Add(Invariant($"S3: {size}px で描いた字のインク部が中央の列を通りません（検査の字には「■」のような塗りつぶしの字を渡す）。"));
                }
                else if (partial > 4)
                {
                    problems.Add(Invariant($"S3: {size}px で描いた字の上下の縁の AA が {partial}px あります（期待 縁 1 本で 2px 以内＝画面の約 1px）。"));
                }
            }
        }

        // S9: 灰色の頂点色は、_UIVertexColorAlwaysGammaSpace が立たなければそのまま、立てば（Linear のプロジェクトなら）linear に直して面に塗られる。
        // 0 と 1 は変換で動かないので、S2 の (1, 0, 0) では確かめられない。許容差は UIGammaToLinear の近似と 8bit の読み戻しを吸収する
        // （直す・直さないを取り違えると 0.29 離れる）。
        private static void CheckVertexColorSpace(
            List<string> problems, GlyphRepresentation representation, int glyph, in GlyphMetrics metrics, Material material)
        {
            const float gray = 0.5f;
            var vertexColor = new Color(gray, gray, gray, 1f);

            var asIs = Straight(Render(representation, glyph, metrics, material, 128f, vertexColor, null, false).Peak());
            if (!Near(asIs, vertexColor, 0.02f))
            {
                problems.Add(Invariant(
                    $"S9: _UIVertexColorAlwaysGammaSpace が 0 のとき、頂点色 {Describe(vertexColor)} で描いた面の色が {Describe(asIs)} です（頂点色のまま使う）。"));
            }

            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.GammaToLinearSpace(gray) : gray;
            var expected = new Color(linear, linear, linear);
            var converted = Straight(Render(representation, glyph, metrics, material, 128f, vertexColor, null, true).Peak());
            if (!Near(converted, expected, 0.02f))
            {
                problems.Add(Invariant(
                    $"S9: _UIVertexColorAlwaysGammaSpace が 1 のとき、頂点色 {Describe(vertexColor)} で描いた面の色が {Describe(converted)} です")
                    + Invariant($"（期待 {Describe(expected)}。Linear のプロジェクトでは gamma のまま来た頂点色を linear に直す）。"));
            }
        }

        private static RenderedImage Render(
            GlyphRepresentation representation, int glyph, in GlyphMetrics metrics, Material material, float size, Color vertexColor, Rect? clip,
            bool vertexColorInGamma)
        {
            var geometry = new GlyphGeometry();
            representation.WriteGeometry(glyph, metrics, geometry);
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            geometry.CopyTo(mesh, size);
            var colors = new Color[mesh.vertexCount];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = vertexColor;
            }
            mesh.colors = colors;

            // インク部の中心を画像の中心へ置く（中央の列がインク部の中心を通る）。画像は幾何が収まる大きさにする。
            var bounds = mesh.bounds;
            var centerX = (metrics.inkLeft + metrics.inkRight) * 0.5f * size;
            var centerY = (metrics.inkBottom + metrics.inkTop) * 0.5f * size;
            var width = (2 * Mathf.CeilToInt(Math.Max(Math.Abs(bounds.min.x - centerX), Math.Abs(bounds.max.x - centerX)))) + 8;
            var height = (2 * Mathf.CeilToInt(Math.Max(Math.Abs(bounds.min.y - centerY), Math.Abs(bounds.max.y - centerY)))) + 8;
            // z は正射影の奥行きの範囲の中ほどに置く（クリップ空間の z の規約がプラットフォームで違っても範囲の内に入る）。
            var model = Matrix4x4.Translate(new Vector3((width * 0.5f) - centerX, (height * 0.5f) - centerY, -0.5f));

            Material? clipped = null;
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var command = new CommandBuffer { name = "T2Font conformance" };
            var properties = new MaterialPropertyBlock();
            properties.SetTexture(MainTexId, representation.GetTexture(glyph));
            // Canvas が Vertex Color Always in Gamma Color Space のバッチに入れるのと同じ値。
            properties.SetInteger(VertexColorAlwaysGammaSpaceId, vertexColorInGamma ? 1 : 0);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                var drawn = material;
                if (clip is Rect r)
                {
                    // Canvas が矩形クリップのバッチに渡すのと同じ値（キーワードと、頂点の空間＝ここではメッシュの空間の矩形）。
                    clipped = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
                    clipped.EnableKeyword("UNITY_UI_CLIP_RECT");
                    clipped.SetVector(ClipRectId, new Vector4(r.xMin, r.yMin, r.xMax, r.yMax));
                    drawn = clipped;
                }

                command.SetRenderTarget(target);
                command.ClearRenderTarget(false, true, Color.clear);
                command.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, width, 0f, height, -1f, 1f));
                command.DrawMesh(mesh, model, drawn, 0, 0, properties);
                Graphics.ExecuteCommandBuffer(command);

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                return new RenderedImage(readback.GetPixels(), width, height);
            }
            finally
            {
                RenderTexture.active = previous;
                command.Release();
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(mesh);
                if (clipped != null)
                {
                    Object.DestroyImmediate(clipped);
                }
            }
        }

        // 透明の上へ separate-alpha の Blend で描いた画素は rgb が premultiplied になっている（rgb = 色 × α）。
        private static Color Straight(Color c)
        {
            return c.a > 0f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, 1f) : Color.clear;
        }

        private static bool Near(Color a, Color b, float tolerance)
        {
            return Math.Abs(a.r - b.r) <= tolerance && Math.Abs(a.g - b.g) <= tolerance && Math.Abs(a.b - b.b) <= tolerance;
        }

        private static string Name(T2FontAsset font, int glyph)
        {
            return glyph < font.Codepoints.Count
                ? $"'{font.name}' の字 {TextCodepoints.Display(font.Codepoints[glyph])}（U+{font.Codepoints[glyph]:X4}）"
                : $"'{font.name}' のグリフ {glyph}";
        }

        private static string Describe(Color c)
        {
            return Invariant($"({c.r:0.000}, {c.g:0.000}, {c.b:0.000})");
        }

        private static string Describe(float x, float y)
        {
            return Invariant($"({x:0.######}, {y:0.######})");
        }

        private static string Invariant(FormattableString text)
        {
            return text.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class RenderedImage
        {
            private readonly Color[] _pixels;
            private readonly int _width;
            private readonly int _height;

            public RenderedImage(Color[] pixels, int width, int height)
            {
                _pixels = pixels;
                _width = width;
                _height = height;
            }

            // α の最も大きい画素（面の奥）。
            public Color Peak()
            {
                var best = Color.clear;
                foreach (var c in _pixels)
                {
                    if (c.a > best.a)
                    {
                        best = c;
                    }
                }
                return best;
            }

            public IEnumerable<Color> CenterColumn()
            {
                var x = _width / 2;
                for (var y = 0; y < _height; y++)
                {
                    yield return _pixels[(y * _width) + x];
                }
            }
        }
    }
}
