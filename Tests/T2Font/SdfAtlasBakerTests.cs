#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using info.someiyoshino.T2Font.Bake;
using NUnit.Framework;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>
    /// SDF のアトラスの生成（<see cref="SdfAtlasBaker"/>・パッケージの README §9）。画質の基準は輪郭そのもの（平らにした多角形の
    /// 面積と外接）で、塗りと距離変換と縮小を通した距離場が、元の字形と同じ大きさの墨を再構成することを見る。
    /// </summary>
    [TestFixture]
    public class SdfAtlasBakerTests
    {
        private static TrueTypeFont? _font;

        private static TrueTypeFont Font => _font ??= new TrueTypeFont(File.ReadAllBytes(TestFonts.Mplus1c()));

        private static readonly EmBox SpikeBox = new EmBox(-0.04, -0.24, 1.04, 1.02);

        private static FontFaceData Face(string text)
        {
            var set = new SortedSet<int>();
            FontCharsets.AddString(set, text);
            return FontFaceBuilder.Build(Font, set);
        }

        private static int GlyphOf(FontFaceData face, int codepoint)
        {
            return GlyphTable.IndexOf(face.Codepoints, codepoint);
        }

        [Test]
        public void セルのplaneとatlasは同じ矩形を指し_墨と距離域を覆う()
        {
            var settings = new SdfBakeSettings { EmPx = 32, RangeEm = 0.125 };
            FontFaceData face = Face("Agあ几、「");
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, settings);

            for (var glyph = 0; glyph < face.GlyphCount; glyph++)
            {
                SdfGlyphCell c = atlas.Cells[glyph];
                var label = $"字形 {glyph}（U+{face.CodepointOf(glyph):X4}）";
                var widthPx = (c.planeRight - c.planeLeft) * settings.EmPx;
                var heightPx = (c.planeTop - c.planeBottom) * settings.EmPx;
                Assert.That(c.atlasRight - c.atlasLeft, Is.EqualTo(widthPx).Within(1e-3), label + " 幅");
                Assert.That(c.atlasTop - c.atlasBottom, Is.EqualTo(heightPx).Within(1e-3), label + " 高さ");

                GlyphMetrics m = face.Glyphs[glyph];
                var range = settings.RangeEm;
                Assert.That(c.planeLeft, Is.LessThanOrEqualTo(m.inkLeft - range + 1e-6), label);
                Assert.That(c.planeBottom, Is.LessThanOrEqualTo(m.inkBottom - range + 1e-6), label);
                Assert.That(c.planeRight, Is.GreaterThanOrEqualTo(m.inkRight + range - 1e-6), label);
                Assert.That(c.planeTop, Is.GreaterThanOrEqualTo(m.inkTop + range - 1e-6), label);

                FontAtlasPage page = atlas.Pages[c.page];
                Assert.That(c.atlasLeft, Is.GreaterThanOrEqualTo(0));
                Assert.That(c.atlasBottom, Is.GreaterThanOrEqualTo(0));
                Assert.That(c.atlasRight, Is.LessThanOrEqualTo(page.Width));
                Assert.That(c.atlasTop, Is.LessThanOrEqualTo(page.Height));
            }
        }

        [Test]
        public void 墨の無い字形は空のセル()
        {
            FontFaceData face = Face(" A");
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, new SdfBakeSettings { EmPx = 24 });

            Assert.That(atlas.Cells.Length, Is.EqualTo(face.GlyphCount));
            Assert.That(atlas.Cells[GlyphOf(face, ' ')].IsEmpty, Is.True);
            Assert.That(atlas.Cells[GlyphOf(face, 'A')].IsEmpty, Is.False);
        }

        // 距離場から再構成した被覆（シェーダと同じ clamp(距離 + 0.5)）の総和は、輪郭の多角形の面積に一致する。
        // 0.5 以上の texel を数える方法は、画素の中心が縁のどちら側に落ちるかで数 % 揺れるため基準にしない。
        // 外接は 0.5 以上の texel の端で測り、画素の中心での標本化なので輪郭の外接と ±1.5 px で一致する。
        [TestCase('几')]
        [TestCase('埃')]
        [TestCase('鬱')]
        [TestCase('「')]
        [TestCase('、')]
        [TestCase('あ')]
        [TestCase('A')]
        [TestCase('g')]
        public void 再構成した墨の面積と外接は輪郭と一致する(char ch)
        {
            var settings = new SdfBakeSettings { EmPx = 48 };
            FontFaceData face = FontFaceBuilder.Build(Font, new[] { (int)ch });
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, settings);
            SdfGlyphCell c = atlas.Cells[0];
            FontAtlasPage page = atlas.Pages[c.page];

            var coverage = 0.0;
            int xMin = int.MaxValue, xMax = int.MinValue, yMin = int.MaxValue, yMax = int.MinValue;
            for (var y = c.atlasBottom; y < c.atlasTop; y++)
            {
                for (var x = c.atlasLeft; x < c.atlasRight; x++)
                {
                    var value = page.Pixels[(y * page.Width) + x];
                    var distancePx = ((value / 255.0) - 0.5) * 2 * settings.RangePx;
                    coverage += Math.Clamp(distancePx + 0.5, 0, 1);
                    if (value >= 128)
                    {
                        xMin = Math.Min(xMin, x);
                        xMax = Math.Max(xMax, x);
                        yMin = Math.Min(yMin, y);
                        yMax = Math.Max(yMax, y);
                    }
                }
            }

            var edges = new List<OutlineEdge>();
            face.Outline(0)!.Flatten(settings.EmPx / Font.UnitsPerEm, 0, 0, 0.01, edges);
            var area = Math.Abs(edges.Sum(e => (e.X0 * e.Y1) - (e.X1 * e.Y0)) / 2);
            var inkWidth = edges.Max(e => Math.Max(e.X0, e.X1)) - edges.Min(e => Math.Min(e.X0, e.X1));
            var inkHeight = edges.Max(e => Math.Max(e.Y0, e.Y1)) - edges.Min(e => Math.Min(e.Y0, e.Y1));

            Assert.That(coverage, Is.EqualTo(area).Within(area * 0.03), "墨の面積（±3%）");
            Assert.That(xMax - xMin + 1, Is.EqualTo(inkWidth).Within(1.5), "墨の幅");
            Assert.That(yMax - yMin + 1, Is.EqualTo(inkHeight).Within(1.5), "墨の高さ");
        }

        [Test]
        public void 距離域の外は0か255に飽和し_矩形の縁は外側()
        {
            var settings = new SdfBakeSettings { EmPx = 48, RangeEm = 0.125 };
            SdfAtlas atlas = SdfAtlasBaker.Bake(Face("口"), settings);
            SdfGlyphCell c = atlas.Cells[0];
            FontAtlasPage page = atlas.Pages[c.page];

            // 矩形の外周の texel は墨から距離域ほど離れている＝ほぼ 0。
            for (var x = c.atlasLeft; x < c.atlasRight; x++)
            {
                Assert.That(page.Pixels[(c.atlasBottom * page.Width) + x], Is.LessThanOrEqualTo(16));
                Assert.That(page.Pixels[((c.atlasTop - 1) * page.Width) + x], Is.LessThanOrEqualTo(16));
            }
        }

        [Test]
        public void 並列と直列で結果が一致する()
        {
            FontFaceData face = Face("Agあいう几埃鬱「」、。ABCxyz123漢字東京");
            SdfAtlas parallel = SdfAtlasBaker.Bake(face, new SdfBakeSettings { EmPx = 32, Parallel = true });
            SdfAtlas serial = SdfAtlasBaker.Bake(face, new SdfBakeSettings { EmPx = 32, Parallel = false });

            Assert.That(parallel.Pages.Count, Is.EqualTo(serial.Pages.Count));
            for (var p = 0; p < parallel.Pages.Count; p++)
            {
                Assert.That(parallel.Pages[p].Pixels, Is.EqualTo(serial.Pages[p].Pixels));
            }
            Assert.That(parallel.Cells, Is.EqualTo(serial.Cells));
        }

        [Test]
        public void 明示した箱からはみ出す字があれば失敗する()
        {
            var settings = new SdfBakeSettings { EmPx = 24, CellBox = new EmBox(0, 0, 0.5, 0.5) };
            FontBakeException? ex = Assert.Throws<FontBakeException>(() => SdfAtlasBaker.Bake(Face("A"), settings));
            Assert.That(ex!.Message, Does.Contain("A(U+0041"));
        }

        [Test]
        public void 箱を省くと墨の外接の和になる()
        {
            SdfAtlas atlas = SdfAtlasBaker.Bake(Face("Ag"), new SdfBakeSettings { EmPx = 24 });

            // 代替字形（M+ 1c の glyph 0）の墨も和に入る。A と g と .notdef の外接の和。
            Assert.That(atlas.InkBounds.HasValue, Is.True);
            EmBox ink = atlas.InkBounds!.Value;
            Assert.That(atlas.CellBox.Left, Is.EqualTo(ink.Left));
            Assert.That(ink.Left, Is.EqualTo(0.030).Within(1e-9));
            Assert.That(ink.Bottom, Is.EqualTo(-0.230).Within(1e-9));
            Assert.That(ink.Right, Is.EqualTo(0.633).Within(1e-9));
            Assert.That(ink.Top, Is.EqualTo(0.730).Within(1e-9));
        }

        [Test]
        public void 距離域を持つ()
        {
            SdfAtlas atlas = SdfAtlasBaker.Bake(Face("A"), new SdfBakeSettings { EmPx = 40, RangeEm = 0.1 });
            Assert.That(atlas.EmPx, Is.EqualTo(40f));
            Assert.That(atlas.RangeEm, Is.EqualTo(0.1f).Within(1e-7));
        }

        [Test]
        public void ページは優先の級の順に埋まる()
        {
            // 1 ページに 1 セルしか入らない寸法にして、ページ番号＝並び順を見る。
            var box = new EmBox(-0.1, -0.3, 1.1, 1.1);
            var settings = new SdfBakeSettings { EmPx = 16, CellBox = box };
            SdfAtlasBaker.CellGeometry cell = SdfAtlasBaker.ComputeCell(box, settings.EmPx, settings.RangePx);
            settings.MaxPageWidth = cell.Width + 3;
            settings.MaxPageHeight = cell.Height + 3;

            // 熙＝第 2 水準・亜＝第 1 水準・あ＝かな・a＝漢字以外。
            FontFaceData face = Face("熙亜あa");
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, settings);
            int PageOf(char c) => atlas.Cells[GlyphOf(face, c)].page;

            Assert.That(atlas.Cells[face.Notdef].page, Is.EqualTo(0), "代替字形は漢字以外の級の先頭");
            Assert.That(PageOf('a'), Is.LessThan(PageOf('あ')));
            Assert.That(PageOf('あ'), Is.LessThan(PageOf('亜')));
            Assert.That(PageOf('亜'), Is.LessThan(PageOf('熙')));
        }

        [Test]
        public void セルは距離域とガードの余白を持ち_寸法は4の倍数()
        {
            // em48・距離域 9px（0.1875em）: 余白＝ceil(9)+1＝10。墨箱 1.08×1.26em → 52＋20＝72・61＋20＝81→84。
            SdfAtlasBaker.CellGeometry cell = SdfAtlasBaker.ComputeCell(SpikeBox, 48, 48 * SdfEncoding.RangeEm);

            Assert.That(cell.Width, Is.EqualTo(72));
            Assert.That(cell.Height, Is.EqualTo(84));
            Assert.That(cell.OriginX, Is.EqualTo(10 + (0.04 * 48)).Within(1e-9));
            Assert.That(cell.OriginY, Is.EqualTo(10 + (0.24 * 48)).Within(1e-9));
        }

        [Test]
        public void JIS規模は既定の上限で1ページに収まる()
        {
            // JIS X 0208 を M+ 1c で焼いた字数（5,787 字）を、既定の em・距離域のセルで並べる。
            var defaults = new SdfBakeSettings();
            SdfAtlasBaker.CellGeometry cell = SdfAtlasBaker.ComputeCell(SpikeBox, defaults.EmPx, defaults.RangePx);
            AtlasLayout layout = AtlasLayout.Plan(5787, cell.Width, cell.Height, defaults.MaxPageWidth, defaults.MaxPageHeight);

            Assert.That(layout.Pages.Count, Is.EqualTo(1));
            Assert.That(layout.Slots.Count, Is.EqualTo(5787));
            Assert.That(layout.Pages[0].Width, Is.EqualTo(5976));
            Assert.That(layout.Pages[0].Height, Is.EqualTo(5880));
        }

        [Test]
        public void 上限を超えると複数ページになり_寸法はBC4のブロックの倍数()
        {
            AtlasLayout layout = AtlasLayout.Plan(5787, 72, 84, 4096, 4096);

            Assert.That(layout.Pages.Count, Is.EqualTo(3));
            Assert.That(layout.Slots.Count(s => s.Page == 0), Is.EqualTo(56 * 48));
            foreach (AtlasPagePlan p in layout.Pages)
            {
                Assert.That(p.Width % 4, Is.Zero);
                Assert.That(p.Height % 4, Is.Zero);
                Assert.That(p.Width, Is.LessThanOrEqualTo(4096));
                Assert.That(p.Height, Is.LessThanOrEqualTo(4096));
            }
            Assert.That(layout.Pages[2].Width, Is.EqualTo(1584), "最後のページは正方形に近い形");
            Assert.That(layout.Pages[2].Height, Is.EqualTo(1596));
        }

        [Test]
        public void ページに1セルも入らなければ失敗する()
        {
            Assert.Throws<FontBakeException>(() => AtlasLayout.Plan(10, 64, 73, 60, 4096));
        }

        [Test]
        public void 墨のある字が無くても代替字形の1ページがある()
        {
            FontFaceData face = Face(" ");
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, new SdfBakeSettings());
            Assert.That(atlas.Pages.Count, Is.EqualTo(1));
            Assert.That(atlas.Cells.Length, Is.EqualTo(2));
            Assert.That(atlas.Cells[face.Notdef].IsEmpty, Is.False);
        }

        [Test]
        public void 代替字形は目に見える墨を持つ()
        {
            FontFaceData face = Face("Aあ");
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, new SdfBakeSettings { EmPx = 24 });
            SdfGlyphCell notdef = atlas.Cells[face.Notdef];
            FontAtlasPage page = atlas.Pages[notdef.page];

            var inked = 0;
            for (var y = notdef.atlasBottom; y < notdef.atlasTop; y++)
            {
                for (var x = notdef.atlasLeft; x < notdef.atlasRight; x++)
                {
                    inked += page.Pixels[(y * page.Width) + x] >= 128 ? 1 : 0;
                }
            }
            Assert.That(inked, Is.GreaterThan(20));
        }

        // 生成器の出力が変わったら SdfAtlasBaker.Version（字の表が変わったなら FontFaceBuilder.Version）を上げ、この値も
        // 合わせて直す（版数を上げないと古い取込の結果が使われ続ける・パッケージの README §7.5）。golden は CoreCLR x64 で取ったもの
        // ——浮動小数の末尾が違いうる Mono では比べない。
        private const string Golden =
            "1/1:3a1907d317cc309f4ec1f45232e7f38b3e1dc0c0d0fd635c9ea9d976ae967a50/85ed0be3ca2b0cff956b8c28e8cf9c6ae31dd64cddba416c5c749c139ae4e256";

        [Test]
        public void 出力が変わったら版数を上げる()
        {
            FontFaceData face = Face(" Agあ几、「鬱");
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, new SdfBakeSettings { EmPx = 24 });
            var stamp = $"{FontFaceBuilder.Version}/{SdfAtlasBaker.Version}:{face.ComputeContentHash()}/{atlas.ComputeContentHash()}";
            Assert.That(stamp == Golden, Is.True,
                $"出力のハッシュが golden と違う（実際 {stamp}・golden {Golden}）。意図した変更なら版数を上げて golden を直す");
        }

        // 開発用の字集合（DevelopmentDefault・既定値）を全字焼く計時。明示して回す
        // （dotnet test --filter "Name~開発用の字集合を既定値で"）。
        [Test]
        [Explicit("計時用。開発用の全字を焼く")]
        public void 開発用の字集合を既定値で1ページに焼ける()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            FontFaceData face = FontFaceBuilder.Build(Font, FontCharsets.Build(FontCharsetKind.DevelopmentDefault));
            SdfAtlas atlas = SdfAtlasBaker.Bake(face, new SdfBakeSettings());
            watch.Stop();
            TestContext.Progress.WriteLine(
                $"[SdfAtlasBaker] DevelopmentDefault: {face.Codepoints.Length} 字・ページ {atlas.Pages[0].Width}x{atlas.Pages[0].Height}・"
                + $"セル {atlas.CellWidth}x{atlas.CellHeight}・{watch.Elapsed.TotalSeconds:0.0} 秒（論理 CPU {Environment.ProcessorCount}）");

            Assert.That(face.Codepoints.Length, Is.EqualTo(6442));
            Assert.That(atlas.Pages.Count, Is.EqualTo(1));
            Assert.That(atlas.Pages[0].Width, Is.LessThanOrEqualTo(8192));
            Assert.That(atlas.Pages[0].Height, Is.LessThanOrEqualTo(8192));
        }
    }
}
