#nullable enable

using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Bake;
using Newtonsoft.Json;

// docs/architecture/03-atlas-to-screen.html の図の元データを書き出す。パッケージの Core と Bake をリンクし、インポーターと同じ
// FontBakeSpec.Parse → FontSpecBaker.BuildFace → SdfAtlasBaker.Bake を見本のベイク仕様に対して走らせる。
// 使い方: dotnet run -- <リポジトリのルート> <出力先のフォルダ> [見本の文字列]
// 出力: dump.json（縦メトリクス・見本の文字列の字ごとのグリフインデックス・送り幅・インク境界・輪郭・セル・アトラスのハッシュ）と
// page0.bin（ページ 0 の画素。行 0 が下端）。

if (args.Length < 2)
{
    Console.Error.WriteLine("使い方: dotnet run -- <リポジトリのルート> <出力先のフォルダ> [見本の文字列]");
    return 1;
}

var root = args[0];
var outDir = args[1];
var sample = args.Length > 2 ? args[2] : "T2Font 永あア";
const string SpecPath = "Assets/Content/Fonts/mplus-1c-sdf.t2font";

var specText = File.ReadAllText(Path.Combine(root, SpecPath));
var kinds = new GlyphRepresentationKinds(new GlyphRepresentationKind[] { SdfRepresentationKind.Instance });
var spec = FontBakeSpec.Parse(specText, SpecPath, kinds);
var font = new TrueTypeFont(File.ReadAllBytes(Path.Combine(root, spec.Source)));
var face = FontSpecBaker.BuildFace(spec, font, SpecPath).Face;
var atlas = SdfAtlasBaker.Bake(face, ((SdfSpecSettings)spec.Representation).ToBakeSettings());

var glyphs = new List<object>();
for (var i = 0; i < sample.Length;)
{
    var codepoint = TextCodepoints.Read(sample, i, out var length);
    i += length;
    var found = GlyphTable.IndexOf(face.Codepoints, codepoint);
    var glyph = found >= 0 ? found : face.Notdef;
    var m = face.Glyphs[glyph];
    var c = atlas.Cells[glyph];
    glyphs.Add(new
    {
        ch = char.ConvertFromUtf32(codepoint),
        cp = codepoint,
        glyph,
        found = found >= 0,
        m.advance,
        m.inkLeft,
        m.inkBottom,
        m.inkRight,
        m.inkTop,
        // 輪郭はフォント単位・y 上向き。点ごとに [x, y, on-curve なら 1]。
        contours = face.Outline(glyph)?.Contours
            .Select(contour => contour.Select(p => new[] { p.X, p.Y, p.OnCurve ? 1 : 0 }).ToArray())
            .ToArray(),
        c.page,
        c.atlasLeft,
        c.atlasBottom,
        c.atlasRight,
        c.atlasTop,
        c.planeLeft,
        c.planeBottom,
        c.planeRight,
        c.planeTop,
        empty = c.IsEmpty,
    });
}

Directory.CreateDirectory(outDir);
File.WriteAllBytes(Path.Combine(outDir, "page0.bin"), atlas.Pages[0].Pixels);
var dump = new
{
    face = new { face.Face.ascender, face.Face.descender, face.Face.lineGap },
    glyphCount = face.GlyphCount,
    codepointCount = face.Codepoints.Length,
    notdef = face.Notdef,
    unitsPerEm = face.UnitsPerEm,
    emPx = atlas.EmPx,
    rangeEm = atlas.RangeEm,
    cellW = atlas.CellWidth,
    cellH = atlas.CellHeight,
    cellBox = atlas.CellBox.ToString(),
    pages = atlas.Pages.Select(p => new { p.Width, p.Height }).ToArray(),
    hash = atlas.ComputeContentHash(),
    glyphs,
};
File.WriteAllText(Path.Combine(outDir, "dump.json"), JsonConvert.SerializeObject(dump, Formatting.Indented));
return 0;
