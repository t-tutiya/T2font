#nullable enable

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// 生成器が作るテクスチャのページ 1 枚（単チャネル 8bit・行優先・行 0 が下端＝Unity のテクスチャと同じ並び）。
    /// 何を符号化するかは表現が決める（SDF は距離・README §9）。
    /// </summary>
    public sealed class FontAtlasPage
    {
        public FontAtlasPage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>画素（<c>y × Width + x</c>）。</summary>
        public byte[] Pixels { get; }
    }
}
