#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// SDF のアトラスの生成の設定（README §9）。既定値は em48・超標本化 8 倍（4 倍より 32〜128px の再構成誤差が 2 割以上小さい）。
    /// 距離域は全フォント共通の <see cref="SdfEncoding.RangeEm"/> で、取込器はこの設定を変えない。
    /// </summary>
    public sealed class SdfBakeSettings
    {
        /// <summary>アトラス上の 1em の px。</summary>
        public double EmPx { get; set; } = 48;

        /// <summary>
        /// SDF の片側距離域（em）。縁取り・ずらし・ぼかしが外へ広がれる上限がこれで決まる。アトラス上の px は <c>EmPx × RangeEm</c>。
        /// 既定（＝取込器が使う値）は全フォント共通の <see cref="SdfEncoding.RangeEm"/>。他の値は生成器の検査にだけ使う。
        /// </summary>
        public double RangeEm { get; set; } = SdfEncoding.RangeEm;

        /// <summary>超標本化の倍率（字を EmPx × この倍率で塗ってから距離を取り、縮小する）。</summary>
        public int Supersample { get; set; } = 8;

        /// <summary>
        /// セルの墨の箱。null なら依頼された字の墨の外接の和を使う（はみ出す字は原理的に出ない）。
        /// 明示した箱からはみ出す字があれば、生成は失敗する（黙って切らない・README §2.1）。
        /// </summary>
        public EmBox? CellBox { get; set; }

        /// <summary>
        /// 1 ページの最大幅（px）。BC4 のブロック（4 px）の倍数に丸めて使う。既定の 8192 は、JIS X 0208 の全字（既定値で 5976×5880）も
        /// 簡体字中国語の規模も 1 ページに収める。<b>ページは少ないほどよい</b>——字ごとにテクスチャが違うと uGUI のバッチが割れる。
        /// 複数ページは上限を超えたときだけ出す。
        /// </summary>
        public int MaxPageWidth { get; set; } = 8192;

        /// <summary>1 ページの最大高さ（px）。</summary>
        public int MaxPageHeight { get; set; } = 8192;

        /// <summary>輪郭を線分へ平らにするときの許容誤差（超標本化後のピクセル）。</summary>
        public double FlattenTolerance { get; set; } = 0.1;

        /// <summary>字ごとの距離場の計算を並列に行うか（結果は並列でも直列でも同一）。</summary>
        public bool Parallel { get; set; } = true;

        /// <summary>アトラス上の片側距離域（px）。</summary>
        public double RangePx => EmPx * RangeEm;

        internal void Validate()
        {
            if (!(EmPx >= 4))
            {
                throw new ArgumentOutOfRangeException(nameof(EmPx), EmPx, "EmPx は 4 以上にしてください。");
            }
            if (!(RangeEm > 0) || RangeEm > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(RangeEm), RangeEm, "RangeEm は 0 より大きく 1 以下にしてください。");
            }
            if (Supersample < 1 || Supersample > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(Supersample), Supersample, "Supersample は 1〜16 にしてください。");
            }
            if (MaxPageWidth < 4 || MaxPageHeight < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxPageWidth), "ページの最大寸法は 4 px 以上にしてください。");
            }
            if (!(FlattenTolerance > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(FlattenTolerance), FlattenTolerance, "FlattenTolerance は正にしてください。");
            }
        }
    }

    /// <summary>SDF のアトラスの生成の結果（README §9）。</summary>
    public sealed class SdfAtlas
    {
        internal SdfAtlas(
            float emPx, float rangeEm, SdfGlyphCell[] cells, IReadOnlyList<FontAtlasPage> pages,
            EmBox cellBox, int cellWidth, int cellHeight, EmBox? inkBounds)
        {
            EmPx = emPx;
            RangeEm = rangeEm;
            Cells = cells;
            Pages = pages;
            CellBox = cellBox;
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            InkBounds = inkBounds;
        }

        /// <summary>アトラス上の 1em の px。</summary>
        public float EmPx { get; }

        /// <summary>片側距離域（em）。</summary>
        public float RangeEm { get; }

        /// <summary>字形番号順のセル（字の表の <see cref="FontFaceData.Glyphs"/> と同じ並び）。墨の無い字形は空のセル。</summary>
        public SdfGlyphCell[] Cells { get; }

        /// <summary>アトラスのページ（符号化した距離・<c>d = 0.5 + 距離 / (2 × 距離域)</c> を 0〜255 へ）。</summary>
        public IReadOnlyList<FontAtlasPage> Pages { get; }

        /// <summary>セルの墨の箱（em）。</summary>
        public EmBox CellBox { get; }

        /// <summary>セルの幅（アトラス px）。</summary>
        public int CellWidth { get; }

        /// <summary>セルの高さ（アトラス px）。</summary>
        public int CellHeight { get; }

        /// <summary>収めた字の墨の外接の和（em）。墨のある字が無ければ null。箱を明示したときの見直しに使う。</summary>
        public EmBox? InkBounds { get; }

        /// <summary>
        /// 出力（距離域・全セル・全ページの画素）の SHA-256（小文字の 16 進）。同じ入力から出る出力が変わったことを検出し、
        /// 版数（<see cref="SdfAtlasBaker.Version"/>）の上げ忘れを止める検査が使う（README §7.5）。
        /// </summary>
        public string ComputeContentHash()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(EmPx);
                    writer.Write(RangeEm);
                    writer.Write(Cells.Length);
                    foreach (var c in Cells)
                    {
                        writer.Write(c.page);
                        writer.Write(c.atlasLeft);
                        writer.Write(c.atlasBottom);
                        writer.Write(c.atlasRight);
                        writer.Write(c.atlasTop);
                        writer.Write(c.planeLeft);
                        writer.Write(c.planeBottom);
                        writer.Write(c.planeRight);
                        writer.Write(c.planeTop);
                    }
                    writer.Write(Pages.Count);
                    foreach (var page in Pages)
                    {
                        writer.Write(page.Width);
                        writer.Write(page.Height);
                        writer.Write(page.Pixels);
                    }
                }
                stream.Position = 0;
                using (var sha = SHA256.Create())
                {
                    return ToHex(sha.ComputeHash(stream));
                }
            }
        }

        internal static string ToHex(byte[] hash)
        {
            var text = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                text.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// 字の表から SDF のアトラスを作る（README §9）。
    ///
    /// <para>
    /// 方式: ①字を「アトラスの em × 超標本化倍率」の格子へ非ゼロ巻き数で塗る ②墨と地の両方向へ厳密距離変換
    /// ③アトラスの texel の中心での距離を超標本化の格子から取り、<c>0.5 + 距離 / (2 × 距離域)</c> を 8bit に符号化
    /// ④全字を共通の原点・同じ大きさのセルに入れ、優先の級の順にページへ並べる（一様格子・複数ページ）。
    /// 字ごとのセルは墨＋距離域の矩形だけを指す（quad を字の大きさに絞る）ので、描画側は配置の方式を知らない。
    /// </para>
    ///
    /// <para>UnityEngine を参照しない純 C# で、Unity の取込器と外部 NUnit の両方から同じコードを呼ぶ。</para>
    /// </summary>
    public static class SdfAtlasBaker
    {
        /// <summary>
        /// SDF のアトラスの生成の版数。<b>同じ入力から出る出力が変わる変更をしたら必ず上げる</b>——上げないと古い取込の結果が
        /// 使われ続ける（README §7.5）。上げ忘れは外部 NUnit の出力のハッシュの検査が止める。
        /// </summary>
        public const int Version = 1;

        /// <summary><paramref name="face"/> の全字形（.notdef を含む）の SDF のアトラスを作る。</summary>
        /// <exception cref="FontBakeException">明示したセルの箱からはみ出す字がある・ページに 1 セルも入らない。</exception>
        public static SdfAtlas Bake(FontFaceData face, SdfBakeSettings settings)
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

            double inkLeft = double.MaxValue, inkBottom = double.MaxValue, inkRight = double.MinValue, inkTop = double.MinValue;
            var inkedGlyphs = new List<int>(face.GlyphCount);
            for (var glyph = 0; glyph < face.GlyphCount; glyph++)
            {
                if (face.Outline(glyph) == null)
                {
                    continue;
                }
                inkedGlyphs.Add(glyph);
                var b = face.InkBox(glyph);
                inkLeft = Math.Min(inkLeft, b.Left);
                inkBottom = Math.Min(inkBottom, b.Bottom);
                inkRight = Math.Max(inkRight, b.Right);
                inkTop = Math.Max(inkTop, b.Top);
            }

            EmBox? inkBounds = inkLeft <= inkRight ? new EmBox(inkLeft, inkBottom, inkRight, inkTop) : (EmBox?)null;
            var box = settings.CellBox ?? inkBounds ?? new EmBox(0, 0, 1, 1);
            if (settings.CellBox.HasValue)
            {
                CheckSpill(face, inkedGlyphs, box);
            }

            var emPx = settings.EmPx;
            var rangePx = settings.RangePx;
            var cell = ComputeCell(box, emPx, rangePx);

            // 墨のある字形だけがセルを使う。並びは優先の級 → 字（.notdef は字 -1 で漢字以外の級の先頭＝ページ 0 の先頭）。
            var ordered = inkedGlyphs
                .OrderBy(g => FontCharsets.PriorityClass(face.CodepointOf(g)))
                .ThenBy(g => face.CodepointOf(g))
                .ToList();
            var plan = AtlasLayout.Plan(ordered.Count, cell.Width, cell.Height, settings.MaxPageWidth, settings.MaxPageHeight);
            var pages = plan.Pages.Select(p => new FontAtlasPage(p.Width, p.Height)).ToArray();

            var cells = new SdfGlyphCell[face.GlyphCount];
            var context = new RenderContext(face, settings, cell.Width, cell.Height, cell.OriginX, cell.OriginY);
            if (settings.Parallel && ordered.Count > 1)
            {
                Parallel.For(
                    0,
                    ordered.Count,
                    () => new GlyphWorkspace(cell.Width * settings.Supersample, cell.Height * settings.Supersample),
                    (i, _, workspace) =>
                    {
                        cells[ordered[i]] = RenderGlyph(context, ordered[i], plan.Slots[i], pages, workspace);
                        return workspace;
                    },
                    _ => { });
            }
            else
            {
                var workspace = new GlyphWorkspace(cell.Width * settings.Supersample, cell.Height * settings.Supersample);
                for (var i = 0; i < ordered.Count; i++)
                {
                    cells[ordered[i]] = RenderGlyph(context, ordered[i], plan.Slots[i], pages, workspace);
                }
            }

            return new SdfAtlas((float)emPx, (float)settings.RangeEm, cells, pages, box, cell.Width, cell.Height, inkBounds);
        }

        /// <summary>
        /// セルの寸法と、セルの左下からペン原点までの距離（アトラス px）。
        ///
        /// <para>
        /// 墨箱の四方に「距離域を整数へ切り上げた幅＋1 texel」の余白を取る。+1 はガードで、quad の縁で bilinear が
        /// 矩形の外を半 texel 読んでも、読むのは同じ字の飽和した値（ほぼ 0）になり、隣のセルの字がにじまない。
        /// セルの寸法は 4 の倍数へ切り上げる——BC4 の 4×4 ブロックが 2 つの字にまたがると、片方の字の値の幅が
        /// もう片方の量子化を粗くする。
        /// </para>
        /// </summary>
        internal static CellGeometry ComputeCell(EmBox box, double emPx, double rangePx)
        {
            var margin = (int)Math.Ceiling(rangePx - 1e-9) + 1;
            var width = RoundUp4((int)Math.Ceiling(((box.Right - box.Left) * emPx) - 1e-9) + (2 * margin));
            var height = RoundUp4((int)Math.Ceiling(((box.Top - box.Bottom) * emPx) - 1e-9) + (2 * margin));
            return new CellGeometry(width, height, margin - (box.Left * emPx), margin - (box.Bottom * emPx));
        }

        private static int RoundUp4(int value)
        {
            return (value + 3) / 4 * 4;
        }

        private static void CheckSpill(FontFaceData face, List<int> inkedGlyphs, EmBox box)
        {
            const double Epsilon = 1e-9;
            var spilled = new StringBuilder();
            var count = 0;
            foreach (var glyph in inkedGlyphs)
            {
                var b = face.InkBox(glyph);
                if (b.Left < box.Left - Epsilon || b.Bottom < box.Bottom - Epsilon
                    || b.Right > box.Right + Epsilon || b.Top > box.Top + Epsilon)
                {
                    count++;
                    if (count <= 40)
                    {
                        var cp = face.CodepointOf(glyph);
                        spilled.Append(cp == FontFaceBuilder.NotdefCodepoint
                            ? FormattableString.Invariant($".notdef({b}) ")
                            : char.ConvertFromUtf32(cp) + FormattableString.Invariant($"(U+{cp:X4} {b}) "));
                    }
                }
            }
            if (count > 0)
            {
                throw new FontBakeException(
                    $"セルの墨の箱 {box} からはみ出す字が {count} 字あります（切って収めると縁が欠けるので止めます）。"
                    + "箱を広げるか、その字を字集合から外してください: " + spilled.ToString().TrimEnd());
            }
        }

        private static SdfGlyphCell RenderGlyph(RenderContext ctx, int glyph, AtlasSlot slot, FontAtlasPage[] pages, GlyphWorkspace ws)
        {
            // 字の矩形は墨＋距離域（texel の格子へ外向きに丸め、セルの中に収める）。plane は同じ矩形の em 表現。
            // 矩形の外の texel は墨から距離域より遠い＝符号化すると 0 に飽和するので、ページの初期値（0）のままでよく、
            // 塗りも距離変換も矩形の中だけで行う（墨は矩形の中にあるので、矩形の中の距離は全体で解いたものと同じ）。
            var emPx = ctx.Settings.EmPx;
            var rangePx = ctx.Settings.RangePx;
            var b = ctx.Face.InkBox(glyph);
            var left = Clamp((int)Math.Floor(ctx.OriginX + (b.Left * emPx) - rangePx), 0, ctx.CellWidth);
            var right = Clamp((int)Math.Ceiling(ctx.OriginX + (b.Right * emPx) + rangePx), 0, ctx.CellWidth);
            var bottom = Clamp((int)Math.Floor(ctx.OriginY + (b.Bottom * emPx) - rangePx), 0, ctx.CellHeight);
            var top = Clamp((int)Math.Ceiling(ctx.OriginY + (b.Top * emPx) + rangePx), 0, ctx.CellHeight);
            var texWidth = right - left;
            var texHeight = top - bottom;

            var k = ctx.Settings.Supersample;
            var hiWidth = texWidth * k;
            var hiHeight = texHeight * k;

            ws.Edges.Clear();
            var scale = emPx * k / ctx.Face.UnitsPerEm;
            ctx.Face.Outline(glyph)!.Flatten(scale, (ctx.OriginX - left) * k, (ctx.OriginY - bottom) * k, ctx.Settings.FlattenTolerance, ws.Edges);
            ws.Rasterizer.Fill(ws.Edges, hiWidth, hiHeight, ws.Inside);

            // texel の値は、超標本化の格子で texel の中心を挟む標本（倍率が偶数なら中央の 2×2、奇数なら中央の 1 つ）の
            // 平均＝texel の中心での距離。ブロック全体の平均は取らない——角の近くの非線形な距離場をならして角を丸める。
            // 距離変換は標本の行と列の交点でだけ解く（DistanceTransform.ComputeAt）。
            var tapLow = (k - 1) / 2;
            var tapHigh = k / 2;
            var taps = tapHigh - tapLow + 1;
            var sampleRows = texHeight * taps;
            var sampleColumns = texWidth * taps;
            for (var t = 0; t < texHeight; t++)
            {
                for (var j = 0; j < taps; j++)
                {
                    ws.Rows[(t * taps) + j] = (t * k) + tapLow + j;
                }
            }
            for (var t = 0; t < texWidth; t++)
            {
                for (var j = 0; j < taps; j++)
                {
                    ws.Columns[(t * taps) + j] = (t * k) + tapLow + j;
                }
            }

            // 外側の標本 → 最寄りの内側の画素まで／内側の標本 → 最寄りの外側の画素まで。
            ws.Distance.ComputeAt(ws.Inside, true, hiWidth, hiHeight, ws.Rows, sampleRows, ws.Columns, sampleColumns, ws.ToInside);
            ws.Distance.ComputeAt(ws.Inside, false, hiWidth, hiHeight, ws.Rows, sampleRows, ws.Columns, sampleColumns, ws.ToOutside);

            // 画素の中心どうしの距離から輪郭までの距離へ（境界は隣り合う中心の中間＝0.5 画素）。内側を正にする。
            var page = pages[slot.Page];
            var tapCount = taps * taps;
            for (var cy = 0; cy < texHeight; cy++)
            {
                for (var cx = 0; cx < texWidth; cx++)
                {
                    double sum = 0;
                    for (var ty = 0; ty < taps; ty++)
                    {
                        var sampleRow = (cy * taps) + ty;
                        var hiRowBase = ws.Rows[sampleRow] * hiWidth;
                        for (var tx = 0; tx < taps; tx++)
                        {
                            var sampleColumn = (cx * taps) + tx;
                            var sample = (sampleRow * sampleColumns) + sampleColumn;
                            sum += ws.Inside[hiRowBase + ws.Columns[sampleColumn]]
                                ? ws.ToOutside[sample] - 0.5
                                : -(ws.ToInside[sample] - 0.5);
                        }
                    }
                    var signedPx = sum / tapCount / k;
                    var encoded = 0.5 + (signedPx / (2 * rangePx));
                    var value = (int)Math.Round(Math.Min(1.0, Math.Max(0.0, encoded)) * 255.0, MidpointRounding.AwayFromZero);
                    page.Pixels[((slot.Y + bottom + cy) * page.Width) + slot.X + left + cx] = (byte)value;
                }
            }

            return new SdfGlyphCell
            {
                page = slot.Page,
                atlasLeft = slot.X + left,
                atlasBottom = slot.Y + bottom,
                atlasRight = slot.X + right,
                atlasTop = slot.Y + top,
                planeLeft = (float)((left - ctx.OriginX) / emPx),
                planeBottom = (float)((bottom - ctx.OriginY) / emPx),
                planeRight = (float)((right - ctx.OriginX) / emPx),
                planeTop = (float)((top - ctx.OriginY) / emPx),
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        internal readonly struct CellGeometry
        {
            public CellGeometry(int width, int height, double originX, double originY)
            {
                Width = width;
                Height = height;
                OriginX = originX;
                OriginY = originY;
            }

            /// <summary>セルの幅（アトラス px・4 の倍数）。</summary>
            public int Width { get; }

            /// <summary>セルの高さ（アトラス px・4 の倍数）。</summary>
            public int Height { get; }

            /// <summary>セルの左端からペン原点まで（アトラス px）。</summary>
            public double OriginX { get; }

            /// <summary>セルの下端からベースラインまで（アトラス px）。</summary>
            public double OriginY { get; }
        }

        private sealed class RenderContext
        {
            public RenderContext(FontFaceData face, SdfBakeSettings settings, int cellWidth, int cellHeight, double originX, double originY)
            {
                Face = face;
                Settings = settings;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
                OriginX = originX;
                OriginY = originY;
            }

            public FontFaceData Face { get; }
            public SdfBakeSettings Settings { get; }
            public int CellWidth { get; }
            public int CellHeight { get; }
            public double OriginX { get; }
            public double OriginY { get; }
        }

        // 1 スレッドが使い回す作業領域（寸法は超標本化したセル＝字の矩形の上限）。
        private sealed class GlyphWorkspace
        {
            public GlyphWorkspace(int width, int height)
            {
                Rasterizer = new ScanlineRasterizer(height);
                Distance = new DistanceTransform(width, height);
                Inside = new bool[width * height];
                ToInside = new double[width * height];
                ToOutside = new double[width * height];
                Rows = new int[height];
                Columns = new int[width];
            }

            public List<OutlineEdge> Edges { get; } = new List<OutlineEdge>();
            public ScanlineRasterizer Rasterizer { get; }
            public DistanceTransform Distance { get; }
            public bool[] Inside { get; }
            public double[] ToInside { get; }
            public double[] ToOutside { get; }
            public int[] Rows { get; }
            public int[] Columns { get; }
        }
    }
}
