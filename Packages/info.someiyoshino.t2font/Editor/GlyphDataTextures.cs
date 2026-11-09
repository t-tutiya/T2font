#nullable enable

using info.someiyoshino.T2Font.Bake;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace info.someiyoshino.T2Font.Editor
{
    /// <summary>
    /// データテクスチャ（距離以外の値を持つテクスチャ）を作って検査する（README §4.4）。非圧縮・point・linear・ミップ無し・clamp・非 Readable。
    /// 値は番地や制御点なので、圧縮・縮小・色空間の変換・ミップの平均のどれを受けても壊れる。インポートの設定で黙ってそうならないよう、作った結果の
    /// 形式・寸法・ミップ・filter・Readable を検査し、違えば作らない（README §2.1）。
    /// </summary>
    internal static class GlyphDataTextures
    {
        internal static Texture2D? Build(string assetPath, GlyphDataPage page, out string error)
        {
            var rgba = page.Format == GlyphDataFormat.RGBA8;
            var type = rgba ? TextureImporterType.Default : TextureImporterType.SingleChannel;
            var format = rgba ? TextureImporterFormat.RGBA32 : TextureImporterFormat.R8;
            var expected = rgba ? TextureFormat.RGBA32 : TextureFormat.R8;

            var settings = new TextureGenerationSettings(type);
            settings.assetPath = assetPath;
            settings.enablePostProcessor = false;

            var info = settings.sourceTextureInformation ?? new SourceTextureInformation();
            info.width = page.Width;
            info.height = page.Height;
            info.containsAlpha = rgba;
            info.hdr = false;
            settings.sourceTextureInformation = info;

            var importer = settings.textureImporterSettings ?? new TextureImporterSettings();
            importer.textureType = type;
            if (!rgba)
            {
                importer.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            }
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaSource = rgba ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            // α を透明度として扱うと、α が 0 の texel の rgb が周りの色で埋め直される（値が壊れる）。
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.sRGBTexture = false;
            importer.readable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            settings.textureImporterSettings = importer;

            var platform = settings.platformSettings ?? new TextureImporterPlatformSettings();
            platform.overridden = true;
            platform.maxTextureSize = GlyphDataPage.MaxSize;
            platform.format = format;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.crunchedCompression = false;
            settings.platformSettings = platform;

            // （using で宣言した NativeArray は読み取り専用の変数になり、添字での書き込みがコンパイルできないので、明示的に破棄する。）
            Texture2D? texture;
            var count = page.Width * page.Height;
            var buffer = new NativeArray<Color32>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            try
            {
                var data = page.Data;
                if (rgba)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var o = i * 4;
                        buffer[i] = new Color32(data[o], data[o + 1], data[o + 2], data[o + 3]);
                    }
                }
                else
                {
                    // 単チャネルの取り出しがどの成分を読んでも同じ値になるよう、4 成分に同じ値を入れる。
                    for (var i = 0; i < count; i++)
                    {
                        var v = data[i];
                        buffer[i] = new Color32(v, v, v, v);
                    }
                }
                texture = TextureGenerator.GenerateTexture(settings, buffer).texture as Texture2D;
            }
            finally
            {
                buffer.Dispose();
            }

            if (texture == null)
            {
                error = $"{assetPath}: データテクスチャを作れませんでした（{page.Width}×{page.Height}・{page.Format}）。";
                return null;
            }
            if (texture.format != expected || texture.width != page.Width || texture.height != page.Height
                || texture.mipmapCount != 1 || texture.isReadable || texture.filterMode != FilterMode.Point)
            {
                error = $"{assetPath}: データテクスチャが期待と違います（形式 {texture.format}／期待 {expected}・"
                    + $"寸法 {texture.width}×{texture.height}／期待 {page.Width}×{page.Height}・ミップ {texture.mipmapCount}／期待 1・"
                    + $"filter {texture.filterMode}／期待 Point・Readable {texture.isReadable}／期待 False）。";
                Object.DestroyImmediate(texture);
                return null;
            }
            error = string.Empty;
            return texture;
        }
    }
}
