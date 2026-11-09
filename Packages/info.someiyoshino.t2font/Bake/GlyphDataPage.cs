#nullable enable

using System;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>データテクスチャの texel の形式（README §4.4）。</summary>
    public enum GlyphDataFormat
    {
        /// <summary>1 texel＝1 バイト（R）。</summary>
        R8,

        /// <summary>1 texel＝4 バイト（R・G・B・A の順）。</summary>
        RGBA8,
    }

    /// <summary>
    /// グリフ表現のベイカーが作るデータテクスチャの中身 1 枚（README §4.4）。距離以外の値（番地・索引・制御点など）をバイトで持ち、Editor の
    /// <c>GlyphBakeContext.AddDataTexture</c> が非圧縮・point・linear・ミップ無しのテクスチャにして形式と寸法を検査する。
    /// 並びは行優先で、<b>行 0 が下端</b>（Unity のテクスチャと同じ並び・<see cref="FontAtlasPage"/> と同じ）。
    /// </summary>
    public sealed class GlyphDataPage
    {
        /// <summary>データテクスチャの最大の幅と高さ（texel）。</summary>
        public const int MaxSize = 16384;

        /// <exception cref="ArgumentOutOfRangeException">寸法が 1〜<see cref="MaxSize"/> の外。</exception>
        public GlyphDataPage(int width, int height, GlyphDataFormat format)
        {
            if (width < 1 || width > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"幅は 1〜{MaxSize} にしてください。");
            }
            if (height < 1 || height > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"高さは 1〜{MaxSize} にしてください。");
            }

            Width = width;
            Height = height;
            Format = format;
            // 最大でも 16384² × 4＝1GiB なので int に収まる。
            Data = new byte[width * height * BytesPerTexel(format)];
        }

        public int Width { get; }
        public int Height { get; }
        public GlyphDataFormat Format { get; }

        /// <summary>中身（<c>(y × Width + x) × texel のバイト数</c> から texel のバイト数だけ）。</summary>
        public byte[] Data { get; }

        /// <summary>texel 1 つのバイト数。</summary>
        public static int BytesPerTexel(GlyphDataFormat format)
        {
            switch (format)
            {
                case GlyphDataFormat.R8:
                    return 1;
                case GlyphDataFormat.RGBA8:
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
    }
}
