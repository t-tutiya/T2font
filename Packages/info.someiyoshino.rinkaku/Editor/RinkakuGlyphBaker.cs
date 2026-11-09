#nullable enable

using System;
using System.Linq;
using info.someiyoshino.Rinkaku.Bake;
using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Bake;
using info.someiyoshino.T2Font.Editor;
using UnityEngine;

namespace info.someiyoshino.Rinkaku.Editor
{
    /// <summary>
    /// Rinkaku のグリフ表現のベイカー（README §4）。<see cref="RinkakuAtlasBaker"/> がベイクしたページをデータテクスチャにし（T2Font の
    /// <c>GlyphBakeContext.AddDataTexture</c>）、<see cref="RinkakuRepresentation"/> を作る。T2Font のインポーターが Editor の型の一覧から見つける。
    /// </summary>
    public sealed class RinkakuGlyphBaker : GlyphRepresentationBaker
    {
        /// <summary>シェーダのパッケージの中の置き場。</summary>
        public const string ShaderPath = "Shaders/Rinkaku.shader";

        private const string PackageName = "info.someiyoshino.rinkaku";

        public override GlyphRepresentationKind Kind => RinkakuRepresentationKind.Instance;

        public override GlyphRepresentation Bake(GlyphBakeContext context, FontFaceData face, GlyphRepresentationSettings settings)
        {
            if (!(settings is RinkakuSpecSettings rinkaku))
            {
                throw new ArgumentException($"Rinkaku のベイカーに別の種類の設定 {settings?.GetType().FullName} が渡されました。", nameof(settings));
            }

            var shader = context.LoadShader(context.PackageAssetPath(PackageName, ShaderPath));
            var atlas = RinkakuAtlasBaker.Bake(face, rinkaku.ToBakeSettings());

            var pages = new Texture2D[atlas.Pages.Count];
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i] = context.AddDataTexture($"page{i}", atlas.Pages[i]);
            }

            var inked = atlas.Stats.Where(s => s.Cells > 0).ToList();
            var meanBytes = inked.Count > 0 ? inked.Average(s => s.Bytes) : 0;
            var meanItems = inked.Count > 0 ? inked.Sum(s => (double)s.TotalItems) / inked.Sum(s => s.Cells) : 0;
            var maxItems = inked.Count > 0 ? inked.Max(s => s.MaxItems) : 0;
            context.Report(FormattableString.Invariant(
                $"{pages.Length} ページ（{string.Join("・", atlas.Pages.Select(p => $"{p.Width}×{p.Height}"))}）・indexEm {atlas.IndexEm}"));
            context.Report(FormattableString.Invariant($"1 字 平均 {meanBytes:0} B・評価本数 平均 {meanItems:0.00}／最大 {maxItems}"));
            return RinkakuRepresentation.Create(shader, Kind.Effects, atlas.IndexEm, atlas.Cells, pages);
        }
    }
}
