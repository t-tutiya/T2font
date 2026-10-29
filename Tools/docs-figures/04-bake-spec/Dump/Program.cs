#nullable enable

using System.Globalization;
using System.Reflection;
using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Bake;
using Newtonsoft.Json;

// docs/architecture/04-bake-spec.html の元データを書き出す。パッケージの Core と Bake をリンクし、インポーターと同じ
// FontBakeSpec.Parse → FontSpecBaker.BuildFace → SdfAtlasBaker.Bake を走らせる。
// 使い方: dotnet run -- <リポジトリのルート> <出力先のフォルダ>
// 出力: dump.json（文字セットごとのコードポイント・JIS X 0208 の表の並び・記号のブロック・Unicode の一般カテゴリ・
// 誤りの例の文面・見本のインポートの報告の値・寸法の見積もり・字を書き出したベイク仕様の例）。

if (args.Length < 2)
{
    Console.Error.WriteLine("使い方: dotnet run -- <リポジトリのルート> <出力先のフォルダ>");
    return 1;
}

var root = args[0];
var outDir = args[1];
const string SamplePath = "Assets/Content/Fonts/mplus-1c-sdf.t2font";
const string ExamplePath = "Assets/Content/Fonts/example.t2font";
var kinds = new GlyphRepresentationKinds(new GlyphRepresentationKind[] { SdfRepresentationKind.Instance });

// 文字セット: 単独のビットの名前ごとと、和の名前（DevelopmentDefault）。
var charsets = new List<object>();
foreach (FontCharsetKind kind in Enum.GetValues(typeof(FontCharsetKind)))
{
    if (kind != FontCharsetKind.None)
    {
        var bits = (int)kind;
        charsets.Add(new { name = kind.ToString(), single = (bits & (bits - 1)) == 0, codepoints = FontCharsets.Build(kind).ToArray() });
    }
}

// JIS X 0208 と CP932 の周辺の表は、区点・CP932 のコードの順に並ぶ（付録はこの順で載せる）。
var tables = new Dictionary<string, int[]>();
foreach (var name in new[] { "NonKanji", "Level1", "Level2", "WindowsVariants", "NecRow13", "IbmExtensions" })
{
    var field = typeof(FontCharsets).Assembly.GetType("info.someiyoshino.T2Font.Bake.JisX0208Table")!
        .GetField(name, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"JisX0208Table.{name} がありません。");
    tables[name] = Codepoints((string)field.GetRawConstantValue()!);
}

int[][] Ranges(string name)
{
    var field = typeof(FontCharsets).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"FontCharsets.{name} がありません。");
    return ((ValueTuple<int, int>[])field.GetValue(null)!).Select(r => new[] { r.Item1, r.Item2 }).ToArray();
}

var all = FontCharsets.Build(FontCharsetKind.DevelopmentDefault);
var categories = all.ToDictionary(cp => cp.ToString(CultureInfo.InvariantCulture), cp => CharUnicodeInfo.GetUnicodeCategory(cp).ToString());

// 誤りの例: 基本の形の 1 行（か 2 行）を書き換えて、インポーターと同じ手順に通した文面。
var baseLines = new[]
{
    "{",
    "    \"source\": \"Assets/Authoring/Fonts/mplus-1c-regular.ttf\",",
    "    \"coverage\": \"fixed\",",
    "    \"charsets\": [\"Ascii\"],",
    "    \"representation\": { \"kind\": \"sdf\", \"emPx\": 48, \"format\": \"R8\" }",
    "}",
};
var fontBytes = File.ReadAllBytes(Path.Combine(root, "Assets/Authoring/Fonts/mplus-1c-regular.ttf"));
var font = new TrueTypeFont(fontBytes);

var cases = new (string Name, string Shown, Func<List<string>, List<string>> Edit)[]
{
    ("SYNTAX", "\"coverage\": \"fixed\"", l => Set(l, 2, "    \"coverage\": \"fixed\"")),
    ("TRAILING", "}\n}", l => { l.Add("}"); return l; }),
    ("UNKNOWN_KEY", "\"charset\": [\"Ascii\"],", l => Set(l, 3, "    \"charset\": [\"Ascii\"],")),
    ("DUPLICATE", "\"coverage\": \"fixed\",\n\"coverage\": \"corpus\",", l => { l.Insert(3, "    \"coverage\": \"corpus\","); return l; }),
    ("NO_SOURCE", "（source の行を消す）", l => { l.RemoveAt(1); return l; }),
    ("SOURCE_PATH", "\"source\": \"mplus-1c-regular.ttf\",", l => Set(l, 1, "    \"source\": \"mplus-1c-regular.ttf\",")),
    ("COVERAGE", "\"coverage\": \"Fixed\",", l => Set(l, 2, "    \"coverage\": \"Fixed\",")),
    ("CHARSET_NAME", "\"charsets\": [\"JIS\"],", l => Set(l, 3, "    \"charsets\": [\"JIS\"],")),
    ("CHARSET_STRING", "\"charsets\": \"Ascii\",", l => Set(l, 3, "    \"charsets\": \"Ascii\",")),
    ("NO_CHARS", "\"charsets\": [],", l => Set(l, 3, "    \"charsets\": [],")),
    ("CHAR_MISSING", "\"characters\": \"😀\",", l => { l.Insert(4, "    \"characters\": \"😀\","); return l; }),
    ("NO_REPRESENTATION", "（representation の行を消す）", l => { l = Set(l, 3, "    \"charsets\": [\"Ascii\"]"); l.RemoveAt(4); return l; }),
    ("KIND", "\"representation\": { \"kind\": \"msdf\" }", l => Set(l, 4, "    \"representation\": { \"kind\": \"msdf\" }")),
    ("SDF_KEY", "\"representation\": { \"kind\": \"sdf\", \"emSize\": 48 }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"emSize\": 48 }")),
    ("EMPX_RANGE", "\"representation\": { \"kind\": \"sdf\", \"emPx\": 512 }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"emPx\": 512 }")),
    ("EMPX_TYPE", "\"representation\": { \"kind\": \"sdf\", \"emPx\": \"48\" }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"emPx\": \"48\" }")),
    ("FORMAT", "\"representation\": { \"kind\": \"sdf\", \"format\": \"R16\" }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"format\": \"R16\" }")),
    ("MAX_PAGE", "\"representation\": { \"kind\": \"sdf\", \"maxPageSize\": 1000.5 }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"maxPageSize\": 1000.5 }")),
    ("CELLBOX_SHAPE", "\"representation\": { \"kind\": \"sdf\", \"cellBox\": [0, -0.2, 1] }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"cellBox\": [0, -0.2, 1] }")),
    ("CELLBOX_EMPTY", "\"representation\": { \"kind\": \"sdf\", \"cellBox\": [0, 0, 0, 1] }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"cellBox\": [0, 0, 0, 1] }")),
    ("CELLBOX_SPILL", "\"representation\": { \"kind\": \"sdf\", \"cellBox\": [0, -0.25, 1, 0.8] }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"cellBox\": [0, -0.25, 1, 0.8] }")),
    ("PAGE_SMALL", "\"representation\": { \"kind\": \"sdf\", \"emPx\": 256, \"maxPageSize\": 64 }", l => Set(l, 4, "    \"representation\": { \"kind\": \"sdf\", \"emPx\": 256, \"maxPageSize\": 64 }")),
};

var errors = new Dictionary<string, object>();
foreach (var (name, shown, edit) in cases)
{
    var text = string.Join("\n", edit(baseLines.ToList())) + "\n";
    errors[name] = new { shown, spec = text, message = ImportError(text) ?? throw new InvalidOperationException($"誤りの例 {name} が通ってしまいました。") };
}

// 元フォントの読み取りの誤り（インポーターは「元フォント … を読めません: 」を前に付ける）。
var fontErrors = new Dictionary<string, string>();
foreach (var (name, tag) in new[] { ("TTC", "ttcf"), ("CFF", "OTTO") })
{
    var bytes = new byte[64];
    for (var i = 0; i < 4; i++)
    {
        bytes[i] = (byte)tag[i];
    }
    try
    {
        _ = new TrueTypeFont(bytes);
        throw new InvalidOperationException($"{name} のフォントが読めてしまいました。");
    }
    catch (NotSupportedException e)
    {
        fontErrors[name] = e.Message;
    }
}

// 見本のインポートの報告（インポーターの Debug.Log と同じ値。秒は載せない）。
var sampleText = File.ReadAllText(Path.Combine(root, SamplePath));
var sampleSpec = FontBakeSpec.Parse(sampleText, SamplePath, kinds);
var sampleFace = FontSpecBaker.BuildFace(sampleSpec, new TrueTypeFont(File.ReadAllBytes(Path.Combine(root, sampleSpec.Source))), SamplePath);
var sampleSdf = (SdfSpecSettings)sampleSpec.Representation;
var sampleAtlas = SdfAtlasBaker.Bake(sampleFace.Face, sampleSdf.ToBakeSettings());

// DevelopmentDefault を見本の元フォントに当てたときの字の数と欠け（タイポグラフィテーブルまで。アトラスはベイクしない）。
var develop = FontBakeSpec.Parse(string.Join("\n", Set(baseLines.ToList(), 3, "    \"charsets\": [\"DevelopmentDefault\"],")), ExamplePath, kinds);
var developFace = FontSpecBaker.BuildFace(develop, font, ExamplePath);

// セルとページの寸法の見積もり（SdfAtlasBaker.Bake の前半＝画素を描く前までと同じ計算）。見本では実際のベイクの結果と照らし合わせる。
var sampleLayout = Layout(sampleFace.Face, sampleSdf.EmPx, sampleSdf.MaxPageSize);
if (sampleLayout.CellW != sampleAtlas.CellWidth || sampleLayout.CellH != sampleAtlas.CellHeight
    || !sampleLayout.Pages.SequenceEqual(sampleAtlas.Pages.Select(p => (p.Width, p.Height))))
{
    throw new InvalidOperationException("寸法の見積もりが SdfAtlasBaker.Bake の結果と合いません。Layout を Bake に合わせてください。");
}
var sizes = new[] { 24, 32, 48, 64, 96 }.Select(emPx =>
{
    var l = Layout(developFace.Face, emPx, 8192);
    return new { emPx, cellW = l.CellW, cellH = l.CellH, l.InkedCount, pages = l.Pages.Select(p => new { Width = p.Item1, Height = p.Item2 }).ToArray() };
}).ToArray();

// 本文から字を決めて書き出す道具の出力の例（開発中のベイク仕様 → FontBakeSpec.WithCharacters → ToJson）。読み戻せることも確かめる。
var developing = new FontBakeSpec("Assets/Authoring/Fonts/mplus-1c-regular.ttf", FontCoverage.Corpus, FontCharsetKind.DevelopmentDefault, string.Empty,
    new SdfSpecSettings(48, SdfPageFormat.R8, 8192, new EmBox(-0.05, -0.25, 1.05, 0.9)));
var exported = developing.WithCharacters(Codepoints("吾輩は猫である。名前はまだ無い。")).ToJson(new[] { "シナリオの本文から書き出したベイク仕様（手で直さない）。" });
FontBakeSpec.Parse(exported, ExamplePath, kinds);

Directory.CreateDirectory(outDir);
var dump = new
{
    charsets,
    tables,
    symbolBlocks = Ranges("SymbolBlocks"),
    symbolExclusions = Ranges("SymbolExclusions"),
    categories,
    baseSpec = string.Join("\n", baseLines) + "\n",
    examplePath = ExamplePath,
    errors,
    fontErrors,
    exported,
    sample = new
    {
        path = SamplePath,
        spec = sampleText,
        codepoints = sampleFace.Face.Codepoints.Length,
        glyphs = sampleFace.Face.GlyphCount,
        missing = sampleFace.MissingFromCharsets.Count,
        kind = sampleSdf.Kind.Name,
        pages = sampleAtlas.Pages.Select(p => new { p.Width, p.Height }).ToArray(),
        format = sampleSdf.Format.ToString(),
        cellW = sampleAtlas.CellWidth,
        cellH = sampleAtlas.CellHeight,
        cellBox = sampleAtlas.CellBox.ToString(),
    },
    develop = new
    {
        codepoints = developFace.Face.Codepoints.Length,
        glyphs = developFace.Face.GlyphCount,
        missing = developFace.MissingFromCharsets.Count,
        sizes,
    },
};
File.WriteAllText(Path.Combine(outDir, "dump.json"), JsonConvert.SerializeObject(dump, Formatting.Indented));
return 0;

// インポーターと同じ順で通し、最初の誤りの文面を返す（通れば null）。ベイカーの誤りの前にベイク仕様の名を付けるのもインポーターと同じ。
string? ImportError(string text)
{
    try
    {
        var spec = FontBakeSpec.Parse(text, ExamplePath, kinds);
        var face = FontSpecBaker.BuildFace(spec, font, ExamplePath);
        SdfAtlasBaker.Bake(face.Face, ((SdfSpecSettings)spec.Representation).ToBakeSettings());
        return null;
    }
    catch (FontSpecException e)
    {
        return e.Message;
    }
    catch (FontBakeException e)
    {
        return e.Message.StartsWith(ExamplePath, StringComparison.Ordinal) ? e.Message : $"{ExamplePath}: {e.Message}";
    }
}

static (int CellW, int CellH, int InkedCount, List<(int, int)> Pages) Layout(FontFaceData face, double emPx, int maxPageSize)
{
    double left = double.MaxValue, bottom = double.MaxValue, right = double.MinValue, top = double.MinValue;
    var inked = 0;
    for (var glyph = 0; glyph < face.GlyphCount; glyph++)
    {
        if (face.Outline(glyph) == null)
        {
            continue;
        }
        inked++;
        var b = face.InkBox(glyph);
        left = Math.Min(left, b.Left);
        bottom = Math.Min(bottom, b.Bottom);
        right = Math.Max(right, b.Right);
        top = Math.Max(top, b.Top);
    }
    var cell = SdfAtlasBaker.ComputeCell(new EmBox(left, bottom, right, top), emPx, emPx * SdfEncoding.RangeEm);
    var plan = AtlasLayout.Plan(inked, cell.Width, cell.Height, maxPageSize, maxPageSize);
    return (cell.Width, cell.Height, inked, plan.Pages.Select(p => (p.Width, p.Height)).ToList());
}

static List<string> Set(List<string> lines, int index, string line)
{
    lines[index] = line;
    return lines;
}

static int[] Codepoints(string text)
{
    var list = new List<int>();
    for (var i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
    {
        list.Add(char.ConvertToUtf32(text, i));
    }
    return list.ToArray();
}
