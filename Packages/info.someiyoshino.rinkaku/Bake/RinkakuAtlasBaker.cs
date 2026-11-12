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
    /// <summary>セルのセグメントの並び（README §4.3 の 4）。</summary>
    public enum RinkakuCellOrder
    {
        /// <summary>
        /// セルの中心に近い順。並びを比べる計測のためのもので、ベイク仕様からは選べない（README §5）。セルのセグメントの集合は変わらないので、
        /// 描く絵は既定の並びと同じ。
        /// </summary>
        NearFirst,

        /// <summary>曲線を先に、直線を後に（それぞれの中はセルの中心に近い順）。既定。</summary>
        CurvesFirst,
    }

    /// <summary>ベイクの設定（README §5）。</summary>
    public sealed class RinkakuBakeSettings
    {
        /// <summary>グリッドの 1em のセルの数（texel の 1 辺＝1/indexEm em）。</summary>
        public int IndexEm { get; set; } = 16;

        /// <summary>1 ページの最大の高さ（texel）。幅は常に <see cref="RinkakuEncoding.PageWidth"/>。</summary>
        public int MaxPageHeight { get; set; } = RinkakuEncoding.MaxPageHeight;

        /// <summary>字ごとの処理を並列に行うか（結果は並列でも直列でも同一）。</summary>
        public bool Parallel { get; set; } = true;

        /// <summary>セルのセグメントの並び。既定（<see cref="RinkakuCellOrder.CurvesFirst"/>）以外は出力が変わる（README §5）。</summary>
        public RinkakuCellOrder CellOrder { get; set; } = RinkakuCellOrder.CurvesFirst;

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

    /// <summary>グリフ 1 つのデータの量とインデックスの形（README §7 の表と検査に使う）。インクの無いグリフは全部 0。</summary>
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

        /// <summary>セグメントの数（和集合輪郭・符号化の後）。</summary>
        public int Segments { get; }

        /// <summary>セグメントテーブルの項目の数（セグメント＋輪郭ごとの終端）。</summary>
        public int Entries { get; }

        /// <summary>インデックスブロックの texel の数。</summary>
        public int IndexTexels { get; }

        /// <summary>セグメントテーブルとレコードの並びの texel の数。</summary>
        public int DataTexels { get; }

        /// <summary>セル 1 つのセグメントの数の最大（1 画素の評価本数の最大）。</summary>
        public int MaxItems { get; }

        /// <summary>全セルのセグメントの数の和（÷ <see cref="Cells"/> が 1 画素の評価本数の平均）。</summary>
        public int TotalItems { get; }

        /// <summary>グリッドセルの数。</summary>
        public int Cells { get; }

        /// <summary>ワイドインデックスか。</summary>
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
    /// Rinkaku のベイカー（純 C#・README §4）。字ごとに和集合輪郭 → 符号化 → グリッドを作り、インデックスブロックとセグメントテーブルをページへ置く。
    /// 和集合輪郭を作れない字があればインポートの失敗にして字を名指す（黙って筋を残さない・README §4.1）。
    /// </summary>
    public static class RinkakuAtlasBaker
    {
        /// <summary>出力のバージョン（README §6）。<b>同じ入力から出る出力が変わる変更をしたら上げる</b>。</summary>
        public const int Version = 5;

        // 失敗の文に名指す字の数の上限。
        private const int MaxNamedFailures = 20;

        // インデックスブロックを置く行の上限。K（ブロックの原点 − グリッドの原点）を UV0.x の y 11bit に収めるための余裕を見た値（README §3.4）。
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
            public byte[] Records = Array.Empty<byte>();
            public int RecordTexels;
            public int MaxItems;
            public int TotalItems;

            public int DataTexels => RecordTexels + (2 * (Encoded?.Entries.Count ?? 0));
        }

        /// <summary>
        /// ベイクする。和集合輪郭を作れない字・座標が持てる範囲の外の字があれば、それらを名指して <see cref="FontBakeException"/> にする。
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
                System.Threading.Tasks.Parallel.For(
                    0, face.GlyphCount, glyph => builds[glyph] = BuildGlyph(face, glyph, settings.IndexEm, settings.CellOrder));
            }
            else
            {
                for (var glyph = 0; glyph < face.GlyphCount; glyph++)
                {
                    builds[glyph] = BuildGlyph(face, glyph, settings.IndexEm, settings.CellOrder);
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

        /// <summary>グリフ 1 つの和集合輪郭・符号化・グリッドを作る。失敗は <see cref="GlyphBuild.Error"/> に入る。</summary>
        internal static GlyphBuild BuildGlyph(FontFaceData face, int glyph, int indexEm, RinkakuCellOrder order = RinkakuCellOrder.CurvesFirst)
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
                build.Error = "和集合輪郭が空です（輪郭が互いに打ち消し合っています）。";
                return build;
            }

            // quad は単精度で持つ（セル）。グリッドは単精度に丸めた quad を少し広げて覆う——quad の縁の画素がグリッドの外のセルを引かない。
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
            if (order == RinkakuCellOrder.CurvesFirst)
            {
                build.Grid = CurvesFirst(build.Grid, encoded);
            }

            if (!EncodeIndex(build, wide: encoded.Entries.Count > RinkakuEncoding.NarrowMaxEntries) && !EncodeIndex(build, wide: true))
            {
                build.Error = "インデックスのレコードの並びがワイドインデックスでも持てる大きさを超えます。";
            }
            else if (build.MaxItems > RinkakuEncoding.MaxItemsPerCell)
            {
                build.Error = FormattableString.Invariant(
                    $"インデックスのセルのセグメントの数 {build.MaxItems} が上限 {RinkakuEncoding.MaxItemsPerCell} を超えます（indexEm を上げてください）。");
            }
            return build;
        }

        // セルのセグメントを、曲線を先・直線を後に並べ直す（それぞれの中の順＝セルの中心に近い順は保つ）。直線かどうかは、シェーダが見る直線の印
        //（項目の制御点の差が (0, 0)・README §3.2）で決める——元に戻したセグメントの形ではなく、シェーダが通る手順で分ける。
        private static NearestIndex.Grid CurvesFirst(NearestIndex.Grid grid, GlyphEncoder.Result encoded)
        {
            var isLine = new bool[encoded.Segments.Count];
            for (var segment = 0; segment < isLine.Length; segment++)
            {
                var entry = encoded.Entries[encoded.Segments[segment].Entry];
                isLine[segment] = entry.Dx == 0 && entry.Dy == 0;
            }

            var cells = new int[grid.Cells.Length][];
            for (var cell = 0; cell < cells.Length; cell++)
            {
                var items = grid.Cells[cell];
                var ordered = new int[items.Length];
                var next = 0;
                foreach (var item in items)
                {
                    if (!isLine[item])
                    {
                        ordered[next++] = item;
                    }
                }
                foreach (var item in items)
                {
                    if (isLine[item])
                    {
                        ordered[next++] = item;
                    }
                }
                cells[cell] = ordered;
            }
            return new NearestIndex.Grid(grid.X0, grid.Y0, grid.Width, grid.Height, cells);
        }

        // インデックスブロックとレコードの並びを符号化する（README §3.3）。ナローインデックスでレコードの並びが持てなければ false。
        //
        // セルはセグメントの番号 2 つ (a, b)。2 本以下は番号そのもの（空きは 0）。3 本以上は a がレコードの位置（セグメントテーブルの先頭から後ろ向きの texel の差）の
        // 下の桁、b が印（短いか長いかと、位置の上の桁）。レコードは 4 本までは番号 4 つ（空きは 0）、5 本以上は [セグメントの数, 番号, …]。同じ内容のレコードは字の中で 1 つにまとめる。
        private static bool EncodeIndex(GlyphBuild build, bool wide)
        {
            var grid = build.Grid!;
            var segments = build.Encoded!.Segments;
            var records = new List<byte>();
            var recordOffsets = new Dictionary<string, int>();
            var cellRecords = new int[grid.Cells.Length];
            var maxItems = 0;
            var totalItems = 0;
            // 番号 1 つのバイト数。
            var unit = wide ? 2 : 1;

            for (var cell = 0; cell < grid.Cells.Length; cell++)
            {
                var items = grid.Cells[cell].Select(s => segments[s].Entry + 1).ToArray();
                maxItems = Math.Max(maxItems, items.Length);
                totalItems += items.Length;
                if (items.Length <= RinkakuEncoding.ItemsInCell)
                {
                    continue;
                }
                var key = string.Join(",", items);
                if (!recordOffsets.TryGetValue(key, out var offset))
                {
                    offset = records.Count / 4;
                    recordOffsets[key] = offset;
                    if (items.Length > RinkakuEncoding.ItemsInRecord)
                    {
                        AppendNumber(records, items.Length, wide);
                    }
                    foreach (var item in items)
                    {
                        AppendNumber(records, item, wide);
                    }
                    // 短いレコードは番号 4 つぶん、長いレコードは texel の境まで、0 で埋める。
                    var end = (offset * 4) + (RinkakuEncoding.ItemsInRecord * unit);
                    while (records.Count < end || records.Count % 4 != 0)
                    {
                        records.Add(0);
                    }
                }
                cellRecords[cell] = offset;
            }

            var recordTexels = records.Count / 4;
            if (recordTexels > (wide ? RinkakuEncoding.WideMaxRecordTexels : RinkakuEncoding.NarrowMaxRecordTexels))
            {
                return false;
            }

            // ナローインデックスは texel 1 つにセル 2 つ（偶数のセルが前半）なので、ブロックの左端の texel は偶数のセルから始まる。
            var shape = new RinkakuGlyphCell { gridX = grid.X0, gridWidth = grid.Width, wide = wide };
            var originCell = RinkakuEncoding.BlockOriginCell(shape);
            var blockWidth = RinkakuEncoding.BlockWidth(shape);
            var block = new byte[blockWidth * grid.Height * 4];
            for (var cell = 0; cell < grid.Cells.Length; cell++)
            {
                var items = grid.Cells[cell].Select(s => segments[s].Entry + 1).ToArray();
                var x = grid.X0 + (cell % grid.Width);
                var cy = cell / grid.Width;
                var column = RinkakuEncoding.TexelColumn(x, wide) - RinkakuEncoding.TexelColumn(originCell, wide);
                var offset = (((cy * blockWidth) + column) * 4) + (wide ? 0 : (x & 1) * 2);
                int a;
                int b;
                if (items.Length <= RinkakuEncoding.ItemsInCell)
                {
                    a = items.Length > 0 ? items[0] : 0;
                    b = items.Length > 1 ? items[1] : 0;
                }
                else
                {
                    // レコードの位置（セグメントテーブルの先頭から後ろ向きの texel の差）: 下の桁を 1 つ目の番号に、上の桁を印に持つ。
                    var back = recordTexels - cellRecords[cell];
                    var lowBits = RinkakuEncoding.RecordLowBits(wide);
                    a = back & ((1 << lowBits) - 1);
                    b = RinkakuEncoding.RecordMarker(wide, items.Length > RinkakuEncoding.ItemsInRecord, back >> lowBits);
                }
                if (wide)
                {
                    block[offset] = (byte)(a & 0xFF);
                    block[offset + 1] = (byte)(a >> 8);
                    block[offset + 2] = (byte)(b & 0xFF);
                    block[offset + 3] = (byte)(b >> 8);
                }
                else
                {
                    block[offset] = (byte)a;
                    block[offset + 1] = (byte)b;
                }
            }

            build.Wide = wide;
            build.Block = block;
            build.BlockWidth = blockWidth;
            build.BlockHeight = grid.Height;
            build.Records = records.ToArray();
            build.RecordTexels = recordTexels;
            build.MaxItems = maxItems;
            build.TotalItems = totalItems;
            return true;
        }

        private static void AppendNumber(List<byte> bytes, int value, bool wide)
        {
            bytes.Add((byte)(value & 0xFF));
            if (wide)
            {
                bytes.Add((byte)(value >> 8));
            }
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
                        $"グリフ {inked[start].Glyph} のインデックスブロック（{inked[start].BlockWidth}×{inked[start].BlockHeight}）が 1 ページに入りません。");
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

        // インデックスブロックを高い順に棚へ詰め（同じ高さはグリフの順）、その下の行からセグメントテーブルを線形に並べる。
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
                bases[b.Glyph] = (int)(cursor + b.RecordTexels);
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
                Array.Copy(b.Records, 0, data, (dataBase - b.RecordTexels) * 4, b.Records.Length);
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
                        $"グリフ {b.Glyph} のインデックスのずれが UV に詰められる範囲の外です（インク境界が em から遠すぎます）。");
                }
                cells[b.Glyph] = cell;
            }
            return page;
        }
    }
}
