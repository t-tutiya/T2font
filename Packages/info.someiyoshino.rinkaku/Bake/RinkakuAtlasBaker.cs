#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using info.someiyoshino.T2Font.Bake;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>ベイクの設定（README §5）。</summary>
    public sealed class RinkakuBakeSettings
    {
        /// <summary>索引格子の 1em のセルの数（texel の 1 辺＝1/indexEm em）。</summary>
        public int IndexEm { get; set; } = 16;

        /// <summary>1 ページの最大の高さ（texel）。幅は常に <see cref="RinkakuEncoding.PageWidth"/>。</summary>
        public int MaxPageHeight { get; set; } = RinkakuEncoding.MaxPageHeight;

        /// <summary>字ごとの処理を並列に行うか（結果は並列でも直列でも同一）。</summary>
        public bool Parallel { get; set; } = true;

        internal void Validate()
        {
            if (IndexEm < RinkakuRepresentationKind.MinIndexEm || IndexEm > RinkakuRepresentationKind.MaxIndexEm)
            {
                throw new ArgumentOutOfRangeException(nameof(IndexEm), IndexEm,
                    $"IndexEm は {RinkakuRepresentationKind.MinIndexEm}〜{RinkakuRepresentationKind.MaxIndexEm} にしてください。");
            }
            if (MaxPageHeight < RinkakuRepresentationKind.MinPageSize || MaxPageHeight > RinkakuEncoding.MaxPageHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxPageHeight), MaxPageHeight,
                    $"MaxPageHeight は {RinkakuRepresentationKind.MinPageSize}〜{RinkakuEncoding.MaxPageHeight} にしてください。");
            }
        }
    }

    /// <summary>グリフ 1 つのデータの量と索引の形（README §7 の表と検査に使う）。インクの無いグリフは全部 0。</summary>
    public readonly struct RinkakuGlyphStats
    {
        public RinkakuGlyphStats(int segments, int entries, int indexTexels, int dataTexels, int maxItems, int totalItems, int cells, bool wide)
        {
            Segments = segments;
            Entries = entries;
            IndexTexels = indexTexels;
            DataTexels = dataTexels;
            MaxItems = maxItems;
            TotalItems = totalItems;
            Cells = cells;
            Wide = wide;
        }

        /// <summary>区間の数（和の輪郭・符号化の後）。</summary>
        public int Segments { get; }

        /// <summary>区間の表の項目の数（区間＋輪郭ごとの終端）。</summary>
        public int Entries { get; }

        /// <summary>索引のブロックの texel の数。</summary>
        public int IndexTexels { get; }

        /// <summary>区間の表と溢れ表の texel の数。</summary>
        public int DataTexels { get; }

        /// <summary>セル 1 つの区間の数の最大（1 画素の評価本数の最大）。</summary>
        public int MaxItems { get; }

        /// <summary>全セルの区間の数の和（÷ <see cref="Cells"/> が 1 画素の評価本数の平均）。</summary>
        public int TotalItems { get; }

        /// <summary>索引格子のセルの数。</summary>
        public int Cells { get; }

        /// <summary>広い索引か。</summary>
        public bool Wide { get; }

        /// <summary>グリフ 1 つのバイト数（ページの棚の隙間を除く）。</summary>
        public int Bytes => (IndexTexels + DataTexels) * 4;
    }

    /// <summary>ベイクした結果（セル・ページ・字ごとの統計）。</summary>
    public sealed class RinkakuAtlas
    {
        internal RinkakuAtlas(int indexEm, RinkakuGlyphCell[] cells, IReadOnlyList<GlyphDataPage> pages, RinkakuGlyphStats[] stats)
        {
            IndexEm = indexEm;
            Cells = cells;
            Pages = pages;
            Stats = stats;
        }

        public int IndexEm { get; }

        /// <summary>グリフインデックス順のセル。</summary>
        public RinkakuGlyphCell[] Cells { get; }

        /// <summary>データのページ（RGBA8・幅 4096）。</summary>
        public IReadOnlyList<GlyphDataPage> Pages { get; }

        /// <summary>グリフインデックス順の統計。</summary>
        public RinkakuGlyphStats[] Stats { get; }

        /// <summary>出力の SHA-256（小文字の 16 進）。出力が変わったことを検出し、バージョンの上げ忘れを止める検査が使う（README §6）。</summary>
        public string ComputeContentHash()
        {
            using var sha = SHA256.Create();
            var header = new StringBuilder();
            header.Append(IndexEm.ToString(CultureInfo.InvariantCulture)).Append('|');
            foreach (var c in Cells)
            {
                header.Append(FormattableString.Invariant(
                    $"{c.page},{c.gridX},{c.gridY},{c.gridWidth},{c.gridHeight},{c.blockX},{c.blockY},{c.dataBase},{c.wide},"));
                header.Append(FormattableString.Invariant($"{c.planeLeft:R},{c.planeBottom:R},{c.planeRight:R},{c.planeTop:R};"));
            }
            var headerBytes = Encoding.UTF8.GetBytes(header.ToString());
            sha.TransformBlock(headerBytes, 0, headerBytes.Length, null, 0);
            foreach (var page in Pages)
            {
                var size = Encoding.UTF8.GetBytes(FormattableString.Invariant($"{page.Width}x{page.Height}|"));
                sha.TransformBlock(size, 0, size.Length, null, 0);
                sha.TransformBlock(page.Data, 0, page.Data.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return string.Concat(sha.Hash!.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// Rinkaku のベイカー（純 C#・README §4）。字ごとに和の輪郭 → 符号化 → 索引格子を作り、索引のブロックと区間の表をページへ置く。
    /// 和の輪郭を作れない字があればインポートの失敗にして字を名指す（黙って筋を残さない・README §4.1）。
    /// </summary>
    public static class RinkakuAtlasBaker
    {
        /// <summary>出力のバージョン（README §6）。<b>同じ入力から出る出力が変わる変更をしたら上げる</b>。</summary>
        public const int Version = 3;

        // 失敗の文に名指す字の数の上限。
        private const int MaxNamedFailures = 20;

        // 索引のブロックを置く行の上限。K（ブロックの原点 − 格子の原点）を UV0.x の y 11bit に収めるための余裕を見た値（README §3.4）。
        private const int BlockRowLimit = 1960;

        /// <summary>グリフ 1 つを作った途中の結果（検査用に公開の内側で使う）。</summary>
        internal sealed class GlyphBuild
        {
            public int Glyph;
            public string? Error;
            public bool Empty;
            public double PlaneLeft;
            public double PlaneBottom;
            public double PlaneRight;
            public double PlaneTop;
            public GlyphEncoder.Result? Encoded;
            public NearestIndex.Grid? Grid;
            public bool Wide;
            public byte[] Block = Array.Empty<byte>();
            public int BlockWidth;
            public int BlockHeight;
            public byte[] Overflow = Array.Empty<byte>();
            public int OverflowTexels;
            public int MaxItems;
            public int TotalItems;

            public int DataTexels => OverflowTexels + (2 * (Encoded?.Entries.Count ?? 0));
        }

        /// <summary>
        /// ベイクする。和の輪郭を作れない字・座標が持てる範囲の外の字があれば、それらを名指して <see cref="FontBakeException"/> にする。
        /// </summary>
        public static RinkakuAtlas Bake(FontFaceData face, RinkakuBakeSettings settings)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            settings.Validate();

            var builds = new GlyphBuild[face.GlyphCount];
            if (settings.Parallel)
            {
                System.Threading.Tasks.Parallel.For(0, face.GlyphCount, glyph => builds[glyph] = BuildGlyph(face, glyph, settings.IndexEm));
            }
            else
            {
                for (var glyph = 0; glyph < face.GlyphCount; glyph++)
                {
                    builds[glyph] = BuildGlyph(face, glyph, settings.IndexEm);
                }
            }

            var failures = builds.Where(b => b.Error != null).ToList();
            if (failures.Count > 0)
            {
                var named = string.Join("・", failures.Take(MaxNamedFailures).Select(b => $"{Describe(face, b.Glyph)}: {b.Error}"));
                var more = failures.Count > MaxNamedFailures ? $" ほか {failures.Count - MaxNamedFailures} 字" : string.Empty;
                throw new FontBakeException($"Rinkaku でベイクできない字が {failures.Count} 字あります（元フォントの輪郭を直してください）: {named}{more}");
            }

            return Layout(builds, settings);
        }

        /// <summary>字を名指す文（<c>U+9B31 '鬱'</c>）。</summary>
        internal static string Describe(FontFaceData face, int glyph)
        {
            var codepoint = face.CodepointOf(glyph);
            if (codepoint < 0)
            {
                return ".notdef";
            }
            return FormattableString.Invariant($"U+{codepoint:X4} '{char.ConvertFromUtf32(codepoint)}'");
        }

        /// <summary>グリフ 1 つの和の輪郭・符号化・索引格子を作る。失敗は <see cref="GlyphBuild.Error"/> に入る。</summary>
        internal static GlyphBuild BuildGlyph(FontFaceData face, int glyph, int indexEm)
        {
            var build = new GlyphBuild { Glyph = glyph };
            var outline = face.Outline(glyph);
            if (outline == null)
            {
                build.Empty = true;
                return build;
            }

            var raw = new List<OutlineSegment>();
            outline.EnumerateSegments(raw);
            double upem = face.UnitsPerEm;
            var contours = new List<IReadOnlyList<Curve>>();
            var current = new List<Curve>();
            var currentContour = -1;
            foreach (var s in raw)
            {
                if (s.Contour != currentContour && current.Count > 0)
                {
                    contours.Add(current);
                    current = new List<Curve>();
                }
                currentContour = s.Contour;
                current.Add(s.IsLine
                    ? Curve.Line(s.X0 / upem, s.Y0 / upem, s.X1 / upem, s.Y1 / upem)
                    : Curve.Quad(s.X0 / upem, s.Y0 / upem, s.ControlX / upem, s.ControlY / upem, s.X1 / upem, s.Y1 / upem));
            }
            if (current.Count > 0)
            {
                contours.Add(current);
            }

            var loops = OutlineUnion.Build(contours, out var error);
            if (loops == null)
            {
                build.Error = error;
                return build;
            }
            var encoded = GlyphEncoder.Encode(loops, out error);
            if (encoded == null)
            {
                build.Error = error;
                return build;
            }
            if (encoded.Segments.Count == 0)
            {
                build.Error = "和の輪郭が空です（輪郭が互いに打ち消し合っています）。";
                return build;
            }

            // quad は単精度で持つ（セル）。索引格子は単精度に丸めた quad を少し広げて覆う——quad の縁の画素が格子の外のセルを引かない。
            var ink = face.InkBox(glyph);
            var extent = RinkakuEncoding.ExtentEm;
            build.PlaneLeft = (float)(ink.Left - extent);
            build.PlaneBottom = (float)(ink.Bottom - extent);
            build.PlaneRight = (float)(ink.Right + extent);
            build.PlaneTop = (float)(ink.Top + extent);
            build.Encoded = encoded;
            const double margin = 1e-6;
            build.Grid = NearestIndex.Build(
                encoded.Segments, indexEm, build.PlaneLeft - margin, build.PlaneBottom - margin, build.PlaneRight + margin, build.PlaneTop + margin);

            if (!EncodeIndex(build, wide: encoded.Entries.Count > RinkakuEncoding.NarrowMaxEntries) && !EncodeIndex(build, wide: true))
            {
                build.Error = "索引の溢れ表が広い索引でも持てる大きさを超えます。";
            }
            else if (build.MaxItems > RinkakuEncoding.MaxItemsPerCell)
            {
                build.Error = FormattableString.Invariant(
                    $"索引のセルの区間の数 {build.MaxItems} が上限 {RinkakuEncoding.MaxItemsPerCell} を超えます（indexEm を上げてください）。");
            }
            return build;
        }

        // 索引のブロックと溢れ表を符号化する（README §3.3）。狭い索引で溢れ表が持てなければ false。
        private static bool EncodeIndex(GlyphBuild build, bool wide)
        {
            var grid = build.Grid!;
            var segments = build.Encoded!.Segments;
            var texelsPerCell = wide ? 2 : 1;
            var block = new byte[grid.Width * texelsPerCell * grid.Height * 4];
            var lists = new List<byte>();
            var listOffsets = new Dictionary<int, int>();
            var maxItems = 0;
            var totalItems = 0;

            for (var cell = 0; cell < grid.Cells.Length; cell++)
            {
                var items = grid.Cells[cell].Select(s => segments[s].Entry + 1).ToArray();
                maxItems = Math.Max(maxItems, items.Length);
                totalItems += items.Length;
                if (items.Length > 4)
                {
                    listOffsets[cell] = lists.Count / 4;
                    if (wide)
                    {
                        AppendU16(lists, items.Length - 2);
                        for (var k = 2; k < items.Length; k++)
                        {
                            AppendU16(lists, items[k]);
                        }
                    }
                    else
                    {
                        lists.Add((byte)(items.Length - 2));
                        for (var k = 2; k < items.Length; k++)
                        {
                            lists.Add((byte)items[k]);
                        }
                    }
                    while (lists.Count % 4 != 0)
                    {
                        lists.Add(0);
                    }
                }
            }

            var overflowTexels = lists.Count / 4;
            if (overflowTexels > (wide ? RinkakuEncoding.WideMaxOverflowTexels : RinkakuEncoding.NarrowMaxOverflowTexels))
            {
                return false;
            }

            for (var cell = 0; cell < grid.Cells.Length; cell++)
            {
                var items = grid.Cells[cell].Select(s => segments[s].Entry + 1).ToArray();
                var cx = cell % grid.Width;
                var cy = cell / grid.Width;
                var offset = ((cy * grid.Width * texelsPerCell) + (cx * texelsPerCell)) * 4;
                var slots = new int[4];
                if (items.Length <= 4)
                {
                    Array.Copy(items, slots, items.Length);
                }
                else
                {
                    // 溢れ: 最初の 2 本と、溢れ表の位置（区間の表の先頭から後ろ向きの texel の差）と印。
                    slots[0] = items[0];
                    slots[1] = items[1];
                    slots[2] = overflowTexels - listOffsets[cell];
                    slots[3] = wide ? RinkakuEncoding.WideOverflowMarker : RinkakuEncoding.NarrowOverflowMarker;
                }
                for (var k = 0; k < 4; k++)
                {
                    if (wide)
                    {
                        block[offset + (k * 2)] = (byte)(slots[k] & 0xFF);
                        block[offset + (k * 2) + 1] = (byte)(slots[k] >> 8);
                    }
                    else
                    {
                        block[offset + k] = (byte)slots[k];
                    }
                }
            }

            build.Wide = wide;
            build.Block = block;
            build.BlockWidth = grid.Width * texelsPerCell;
            build.BlockHeight = grid.Height;
            build.Overflow = lists.ToArray();
            build.OverflowTexels = overflowTexels;
            build.MaxItems = maxItems;
            build.TotalItems = totalItems;
            return true;
        }

        private static void AppendU16(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)(value >> 8));
        }

        // ---------------------------------------------------------------- 配置

        private static RinkakuAtlas Layout(GlyphBuild[] builds, RinkakuBakeSettings settings)
        {
            var inked = builds.Where(b => !b.Empty).ToList();
            var cells = new RinkakuGlyphCell[builds.Length];
            var pages = new List<GlyphDataPage>();
            var start = 0;
            while (start < inked.Count)
            {
                // このページに入る最大の数を探す（倍々で上限を見つけてから 2 分探索）。
                var fits = 0;
                var step = 1;
                while (start + fits < inked.Count && TryPlan(inked.GetRange(start, Math.Min(fits + step, inked.Count - start)), settings, out _))
                {
                    fits = Math.Min(fits + step, inked.Count - start);
                    step *= 2;
                }
                var low = fits;
                var high = Math.Min(fits + step, inked.Count - start);
                while (high - low > 1)
                {
                    var mid = (low + high) / 2;
                    if (TryPlan(inked.GetRange(start, mid), settings, out _))
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }
                fits = low;
                if (fits == 0)
                {
                    throw new FontBakeException(
                        $"グリフ {inked[start].Glyph} の索引のブロック（{inked[start].BlockWidth}×{inked[start].BlockHeight}）が 1 ページに入りません。");
                }

                var group = inked.GetRange(start, fits);
                TryPlan(group, settings, out var plan);
                pages.Add(Fill(group, plan!, pages.Count, cells));
                start += fits;
            }

            var stats = builds.Select(b => b.Empty || b.Encoded == null
                ? default
                : new RinkakuGlyphStats(
                    b.Encoded.Segments.Count, b.Encoded.Entries.Count, b.BlockWidth * b.BlockHeight, b.DataTexels,
                    b.MaxItems, b.TotalItems, b.Grid!.Cells.Length, b.Wide)).ToArray();
            return new RinkakuAtlas(settings.IndexEm, cells, pages, stats);
        }

        private sealed class Plan
        {
            public Plan(Dictionary<int, (int X, int Y)> blocks, Dictionary<int, int> bases, int height)
            {
                Blocks = blocks;
                Bases = bases;
                Height = height;
            }

            public Dictionary<int, (int X, int Y)> Blocks { get; }

            public Dictionary<int, int> Bases { get; }

            public int Height { get; }
        }

        // 索引のブロックを高い順に棚へ詰め（同じ高さはグリフの順）、その下の行から区間の表を線形に並べる。
        private static bool TryPlan(List<GlyphBuild> group, RinkakuBakeSettings settings, out Plan? plan)
        {
            plan = null;
            var blocks = new Dictionary<int, (int, int)>();
            var x = 0;
            var shelfY = 0;
            var shelfHeight = 0;
            foreach (var b in group.OrderByDescending(b => b.BlockHeight).ThenBy(b => b.Glyph))
            {
                if (b.BlockWidth > RinkakuEncoding.PageWidth)
                {
                    return false;
                }
                if (x + b.BlockWidth > RinkakuEncoding.PageWidth)
                {
                    shelfY += shelfHeight;
                    x = 0;
                    shelfHeight = 0;
                }
                blocks[b.Glyph] = (x, shelfY);
                x += b.BlockWidth;
                shelfHeight = Math.Max(shelfHeight, b.BlockHeight);
            }
            var blockRows = shelfY + shelfHeight;
            if (blockRows > BlockRowLimit)
            {
                return false;
            }

            var bases = new Dictionary<int, int>();
            long cursor = (long)blockRows * RinkakuEncoding.PageWidth;
            foreach (var b in group)
            {
                bases[b.Glyph] = (int)(cursor + b.OverflowTexels);
                cursor += b.DataTexels;
            }
            var height = (int)Math.Max(1, (cursor + RinkakuEncoding.PageWidth - 1) / RinkakuEncoding.PageWidth);
            if (height > settings.MaxPageHeight)
            {
                return false;
            }
            plan = new Plan(blocks, bases, height);
            return true;
        }

        private static GlyphDataPage Fill(List<GlyphBuild> group, Plan plan, int pageIndex, RinkakuGlyphCell[] cells)
        {
            var page = new GlyphDataPage(RinkakuEncoding.PageWidth, plan.Height, GlyphDataFormat.RGBA8);
            var data = page.Data;
            foreach (var b in group)
            {
                var (bx, by) = plan.Blocks[b.Glyph];
                for (var row = 0; row < b.BlockHeight; row++)
                {
                    Array.Copy(b.Block, row * b.BlockWidth * 4, data, (((by + row) * RinkakuEncoding.PageWidth) + bx) * 4, b.BlockWidth * 4);
                }

                var dataBase = plan.Bases[b.Glyph];
                Array.Copy(b.Overflow, 0, data, (dataBase - b.OverflowTexels) * 4, b.Overflow.Length);
                var entries = b.Encoded!.Entries;
                for (var e = 0; e < entries.Count; e++)
                {
                    entries[e].WriteTo(data, (dataBase + (2 * e)) * 4);
                }

                var grid = b.Grid!;
                var cell = new RinkakuGlyphCell
                {
                    page = pageIndex,
                    gridX = grid.X0,
                    gridY = grid.Y0,
                    gridWidth = grid.Width,
                    gridHeight = grid.Height,
                    blockX = bx,
                    blockY = by,
                    dataBase = dataBase,
                    wide = b.Wide,
                    planeLeft = (float)b.PlaneLeft,
                    planeBottom = (float)b.PlaneBottom,
                    planeRight = (float)b.PlaneRight,
                    planeTop = (float)b.PlaneTop,
                };
                if (RinkakuEncoding.PackedOffset(cell) < 0)
                {
                    throw new FontBakeException(
                        $"グリフ {b.Glyph} の索引のずれが UV に詰められる範囲の外です（インク境界が em から遠すぎます）。");
                }
                cells[b.Glyph] = cell;
            }
            return page;
        }
    }
}
