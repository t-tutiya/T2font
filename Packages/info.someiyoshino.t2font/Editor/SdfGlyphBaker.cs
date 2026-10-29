#nullable enable

using System;
using System.Linq;
using info.someiyoshino.T2Font.Bake;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace info.someiyoshino.T2Font.Editor
{
    /// <summary>
    /// 同梱の SDF の表現のベイカー（README §9）。<see cref="SdfAtlasBaker"/> がベイクした距離のページをテクスチャにし、
    /// <see cref="SdfRepresentation"/> を作る。
    /// </summary>
    public sealed class SdfGlyphBaker : GlyphRepresentationBaker
    {
        /// <summary>シェーダのパッケージの中の置き場。</summary>
        public const string ShaderPath = "Shaders/T2Font-SDF.shader";

        private const string PackageName = "info.someiyoshino.t2font";

        // テクスチャの最大寸法。既定の 2048 のままだと、それより大きいページが黙って縮む。
        private const int MaxTextureSize = 16384;

        public override GlyphRepresentationKind Kind => SdfRepresentationKind.Instance;

        public override GlyphRepresentation Bake(GlyphBakeContext context, FontFaceData face, GlyphRepresentationSettings settings)
        {
            if (!(settings is SdfSpecSettings sdf))
            {
                throw new ArgumentException($"SDF のベイカーに別の種類の設定 {settings?.GetType().FullName} が渡されました。", nameof(settings));
            }

            ResolveFormat(context, sdf.Format, out var format, out var expected);
            var shader = context.LoadShader(context.PackageAssetPath(PackageName, ShaderPath));
            var atlas = SdfAtlasBaker.Bake(face, sdf.ToBakeSettings());

            var pages = new Texture2D[atlas.Pages.Count];
            for (var i = 0; i < pages.Length; i++)
            {
                var page = BuildPage(context.AssetPath, atlas.Pages[i], format, expected, false, out var error)
                    ?? throw new FontBakeException(error);
                page.name = $"{context.BaseName} page {i}";
                context.AddObject($"page{i}", page);
                pages[i] = page;
            }

            context.Report(
                $"{pages.Length} ページ（{string.Join("・", atlas.Pages.Select(p => $"{p.Width}×{p.Height}"))}・{sdf.Format}）"
                + $"・セル {atlas.CellWidth}×{atlas.CellHeight}（インク境界 {atlas.CellBox}）");
            return SdfRepresentation.Create(shader, Kind.Effects, atlas.EmPx, atlas.Cells, pages);
        }

        /// <summary>
        /// 距離のページ（単チャネル 8bit）からテクスチャを作る（README §9）。linear・ミップ無し・clamp・bilinear・2 の冪へ縮めない。
        /// 期待の形式・寸法・Readable にならなければ null を返し、<paramref name="error"/> に理由を入れる（黙って違う形で作らない・README §4.4）。
        /// </summary>
        internal static Texture2D? BuildPage(
            string assetPath, FontAtlasPage source, TextureImporterFormat format, TextureFormat expected, bool readable, out string error)
        {
            var settings = new TextureGenerationSettings(TextureImporterType.SingleChannel);
            settings.assetPath = assetPath;
            settings.enablePostProcessor = false;

            var info = settings.sourceTextureInformation ?? new SourceTextureInformation();
            info.width = source.Width;
            info.height = source.Height;
            info.containsAlpha = false;
            info.hdr = false;
            settings.sourceTextureInformation = info;

            var importer = settings.textureImporterSettings ?? new TextureImporterSettings();
            importer.textureType = TextureImporterType.SingleChannel;
            importer.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.sRGBTexture = false;
            importer.readable = readable;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            settings.textureImporterSettings = importer;

            var platform = settings.platformSettings ?? new TextureImporterPlatformSettings();
            platform.overridden = true;
            platform.maxTextureSize = MaxTextureSize;
            platform.format = format;
            platform.textureCompression = format == TextureImporterFormat.R8
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;
            platform.compressionQuality = 100;
            platform.crunchedCompression = false;
            settings.platformSettings = platform;

            // 単チャネルの取り出しがどの成分を読んでも同じ値になるよう、4 成分に同じ値を入れる。
            // （using で宣言した NativeArray は読み取り専用の変数になり、添字での書き込みがコンパイルできないので、明示的に破棄する。）
            Texture2D? texture;
            var buffer = new NativeArray<Color32>(source.Width * source.Height, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            try
            {
                var pixels = source.Pixels;
                for (var i = 0; i < pixels.Length; i++)
                {
                    var v = pixels[i];
                    buffer[i] = new Color32(v, v, v, v);
                }
                texture = TextureGenerator.GenerateTexture(settings, buffer).texture as Texture2D;
            }
            finally
            {
                buffer.Dispose();
            }

            if (texture == null)
            {
                error = $"{assetPath}: ページのテクスチャを作れませんでした（{source.Width}×{source.Height}）。";
                return null;
            }
            if (texture.format != expected || texture.width != source.Width || texture.height != source.Height
                || texture.mipmapCount != 1 || texture.isReadable != readable)
            {
                error = $"{assetPath}: ページのテクスチャが期待と違います（形式 {texture.format}／期待 {expected}・"
                    + $"寸法 {texture.width}×{texture.height}／期待 {source.Width}×{source.Height}・ミップ {texture.mipmapCount}・"
                    + $"Readable {texture.isReadable}／期待 {readable}）。";
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
            error = string.Empty;
            return texture;
        }

        // R8 はターゲットに依存しない。BC4 はデスクトップ向けだけで、選んだときだけターゲットを読む
        // （ターゲットを読むと、ターゲットを切り替えたときに再インポートする依存が張られる）。
        private static void ResolveFormat(
            GlyphBakeContext context, SdfPageFormat requested, out TextureImporterFormat format, out TextureFormat expected)
        {
            if (requested == SdfPageFormat.R8)
            {
                format = TextureImporterFormat.R8;
                expected = TextureFormat.R8;
                return;
            }

            format = TextureImporterFormat.BC4;
            expected = TextureFormat.BC4;
            var target = context.SelectedBuildTarget;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64
                && target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
            {
                throw new FontBakeException(
                    $"{context.AssetPath}: format \"BC4\" はデスクトップ向けのターゲットでしか使えません（現在 {target}）。R8 にしてください。");
            }
        }
    }
}
