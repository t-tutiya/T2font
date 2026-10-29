using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Viewer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace info.someiyoshino.T2font.EditorTools
{
    /// <summary>
    /// 目視の比較の場面を作るメニュー（t2font-v0.1-plan S6）。検証用の M+ 1c を、表示サイズ・エッジスタイル・フェード・
    /// クリップを変えて並べた新しい場面を開く。場面は保存しない（必要なら手で保存する）——字は検証ビューが実行時に作るので、
    /// ベイク仕様を再ベイクすれば開き直すだけで最新の表現を見られる。
    /// </summary>
    internal static class T2FontComparisonScene
    {
        private const string FontPath = "Assets/Content/Fonts/mplus-1c-sdf.t2font";
        private const string Sample = "永あア ABC ■□";

        [MenuItem("T2font/比較の場面を作る")]
        private static void Create()
        {
            var font = AssetDatabase.LoadAssetAtPath<T2FontAsset>(FontPath);
            if (font == null)
            {
                EditorUtility.DisplayDialog("T2font", $"{FontPath} を読めません（インポートの誤りを Console で確かめてください）。", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.32f, 0.36f, 0.42f);
            }

            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var y = 16f;

            foreach (var size in new[] { 12f, 16f, 24f, 48f, 96f })
            {
                Add(canvas, font, ref y, size, $"{size}px  {Sample}", GlyphEdgeStyle.None, Color.white, 1f, false);
            }

            var black = new Color(0f, 0f, 0f, 1f);
            Add(canvas, font, ref y, 48f, "outline 0.08  " + Sample, Style(black, 0f, 0f, GlyphEdgeStyle.OutlineWidthMax, 0f), Color.white, 1f, false);
            Add(canvas, font, ref y, 48f, "glow 0.04/0.06  " + Sample, Style(new Color(1f, 0.8f, 0.2f, 1f), 0f, 0f, 0.04f, GlyphEdgeStyle.OutlineSoftnessMax), Color.white, 1f, false);
            Add(canvas, font, ref y, 48f, "dilate +0.04  " + Sample, Style(black, GlyphEdgeStyle.FaceDilateMax, 0f, 0f, 0f), Color.white, 1f, false);
            Add(canvas, font, ref y, 48f, "dilate -0.03  " + Sample, Style(black, GlyphEdgeStyle.FaceDilateMin, 0f, 0f, 0f), Color.white, 1f, false);
            Add(canvas, font, ref y, 48f, "soft 0.06  " + Sample, Style(black, 0f, GlyphEdgeStyle.FaceSoftnessMax, 0f, 0f), Color.white, 1f, false);
            Add(canvas, font, ref y, 48f, "fade 0.5 + outline  " + Sample, Style(black, 0f, 0f, 0.05f, 0f), new Color(1f, 0.4f, 0.4f), 0.5f, false);
            Add(canvas, font, ref y, 96f, "clip " + Sample, GlyphEdgeStyle.None, Color.white, 1f, true);
        }

        private static GlyphEdgeStyle Style(Color outline, float dilate, float faceSoftness, float outlineWidth, float outlineSoftness)
        {
            return new GlyphEdgeStyle(outline.r, outline.g, outline.b, outline.a, dilate, faceSoftness, outlineWidth, outlineSoftness);
        }

        // 行を 1 つ置き、y を次の行の上端へ進める。clip の行は矩形を字の高さの半分にして、下半分を切る。
        private static void Add(
            Canvas canvas, T2FontAsset font, ref float y, float size, string text, GlyphEdgeStyle style, Color face, float alpha, bool clip)
        {
            var lineHeight = (font.Face.ascender - font.Face.descender) * size;
            var go = new GameObject(text, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -y);
            rect.sizeDelta = new Vector2(1600f, clip ? lineHeight * 0.5f : lineHeight);

            var view = go.AddComponent<T2FontText>();
            view.Font = font;
            view.Text = text;
            view.FontSize = size;
            view.EdgeStyle = style;
            view.FaceColor = face;
            view.Alpha = alpha;
            view.ClipToRect = clip;
            view.Rebuild();

            y += lineHeight + 8f;
        }
    }
}
