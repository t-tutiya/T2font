#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>セル 1 つの置き場（ページ番号と、ページ上の左下の px）。</summary>
    internal readonly struct AtlasSlot
    {
        public AtlasSlot(int page, int x, int y)
        {
            Page = page;
            X = x;
            Y = y;
        }

        public int Page { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>ページ 1 枚の寸法（px）と列数。</summary>
    internal readonly struct AtlasPagePlan
    {
        public AtlasPagePlan(int width, int height, int columns)
        {
            Width = width;
            Height = height;
            Columns = columns;
        }

        public int Width { get; }
        public int Height { get; }
        public int Columns { get; }
    }

    /// <summary>
    /// 一様格子の配置（README §9）。並べた順に 0 ページから
    /// 詰め、1 ページに入りきらなければ次のページへ進む（複数ページが標準）。
    ///
    /// <para>
    /// ページは正方形にしない。入る字が少なければ正方形に近い形（セル数の平方根の列数）、多ければ最大幅まで
    /// 横に広げる。寸法は BC4 のブロック（4 px）の倍数へ切り上げる——BC4 は 4×4 のブロック単位で符号化するので、
    /// 端数のあるテクスチャは圧縮できない。
    /// </para>
    /// </summary>
    internal sealed class AtlasLayout
    {
        private AtlasLayout(IReadOnlyList<AtlasPagePlan> pages, IReadOnlyList<AtlasSlot> slots)
        {
            Pages = pages;
            Slots = slots;
        }

        public IReadOnlyList<AtlasPagePlan> Pages { get; }

        /// <summary>並べた順の i 番目のセルの置き場。</summary>
        public IReadOnlyList<AtlasSlot> Slots { get; }

        public static AtlasLayout Plan(int count, int cellWidth, int cellHeight, int maxPageWidth, int maxPageHeight)
        {
            if (cellWidth <= 0 || cellHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellWidth));
            }

            var maxWidth = maxPageWidth / 4 * 4;
            var maxHeight = maxPageHeight / 4 * 4;
            var maxColumns = maxWidth / cellWidth;
            var maxRows = maxHeight / cellHeight;
            if (maxColumns < 1 || maxRows < 1)
            {
                throw new FontBakeException(
                    $"セル（{cellWidth}×{cellHeight} px）がページの最大寸法（{maxPageWidth}×{maxPageHeight} px）に 1 つも入りません。");
            }
            var capacity = maxColumns * maxRows;

            var pages = new List<AtlasPagePlan>();
            var slots = new List<AtlasSlot>(count);
            var remaining = count;
            while (remaining > 0)
            {
                var onPage = Math.Min(remaining, capacity);
                var columns = (int)Math.Ceiling(Math.Sqrt(onPage * (double)cellHeight / cellWidth));
                columns = Math.Max(1, Math.Min(maxColumns, columns));
                var rows = (onPage + columns - 1) / columns;
                if (rows > maxRows)
                {
                    columns = maxColumns;
                    rows = (onPage + columns - 1) / columns;
                }
                // 決まった行数に要る最小の列数へ戻す（字が少ないと、正方形に近づける列数が丸々 1 列余る）。
                columns = (onPage + rows - 1) / rows;

                var pageIndex = pages.Count;
                pages.Add(new AtlasPagePlan(RoundUp4(columns * cellWidth), RoundUp4(rows * cellHeight), columns));
                for (var i = 0; i < onPage; i++)
                {
                    slots.Add(new AtlasSlot(pageIndex, (i % columns) * cellWidth, (i / columns) * cellHeight));
                }
                remaining -= onPage;
            }
            return new AtlasLayout(pages, slots);
        }

        private static int RoundUp4(int value)
        {
            return (value + 3) / 4 * 4;
        }
    }
}
