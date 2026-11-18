# グリフ表現を足す

新しい描き方（ビットマップ・別の距離場・曲線をそのまま描く方法など）を、T2Font のグリフ表現として足す手順。足したグリフ表現は
ベイク仕様の `"kind"` で選べ、**ホストのコードは変わらない**（README §2.2）。手順の正典は README §8 で、本書はそれを、T2Font の公開 API だけで
作られたグリフ表現 **Rinkaku** の実際のコードで追う。

本書のコード例は、パッケージの公開の版のソースから生成で引いている（D-USERDOCS）。型とメンバーの一覧は
[API リファレンス](../generated/api/t2font.md)の「グリフ表現の作者」の節。

## 1. 作る物

| 物 | 置き場（アセンブリ） | 派生元・使う API | 役割 |
|---|---|---|---|
| 種類の記述子と設定 | Bake（純 C#・Editor 専用・UnityEngine を参照しない） | `GlyphRepresentationKind`・`GlyphRepresentationSettings` | 名前・描ける効果・バージョン・ベイク仕様の設定の検査 |
| データを作る処理 | Bake | `FontFaceData`・`GlyphOutline`・`GlyphDataPage` | タイポグラフィテーブルと輪郭から、描くためのデータを作る |
| グリフ表現 | Runtime | `GlyphRepresentation` | 字ごとの幾何とテクスチャ、マテリアルの定数 |
| シェーダ | `Shaders/` | README §5 の S1〜S9 | 描く |
| ベイカー | Editor | `GlyphRepresentationBaker`・`GlyphBakeContext` | テクスチャを作り、グリフ表現のサブアセットを返す |
| 適合検査 | 利用側のテスト（EditMode） | `GlyphConformance` | 契約を満たすかを確かめる |

Bake を UnityEngine から切り離しておくと、データを作る処理を Unity を起動しない外部の NUnit で検査できる（T2Font の README §1）。

## 2. パッケージの形

Rinkaku の形をそのまま使える。`package.json` の `dependencies` に `info.someiyoshino.t2font` を書き、各アセンブリは T2Font の対応する
アセンブリを参照する（参照の網羅は [アセンブリ一覧](../generated/assembly-reference.md) の `Someiyoshino.Rinkaku*` の行）。

```
Packages/<パッケージ名>/
├─ package.json            dependencies: info.someiyoshino.t2font
├─ Core/                   形式の定数（UnityEngine を参照しない・全プラットフォーム）
├─ Bake/                   種類の記述子・データを作る処理（UnityEngine を参照しない・Editor 専用）
├─ Runtime/                グリフ表現（GlyphRepresentation の派生）
├─ Shaders/                シェーダ
└─ Editor/                 ベイカー（GlyphRepresentationBaker の派生）
```

## 3. 種類の記述子

<!-- gen:source:Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs#RinkakuRepresentationKind -->
```csharp
// Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs
/// <summary>
/// Rinkaku のグリフ表現の種類（<c>"kind": "rinkaku"</c>・README §5）。符号付き距離を SDF と同じ意味で持つので、エッジスタイルの効果を全部描ける
/// （README §2）。
/// </summary>
public sealed class RinkakuRepresentationKind : GlyphRepresentationKind
{
    /// <summary>唯一の実体。</summary>
    public static readonly RinkakuRepresentationKind Instance = new RinkakuRepresentationKind();

    /// <summary><c>indexEm</c> の範囲。</summary>
    public const int MinIndexEm = 8;

    /// <inheritdoc cref="MinIndexEm"/>
    public const int MaxIndexEm = 32;

    /// <summary><c>maxPageSize</c> の下限（上限は <see cref="RinkakuEncoding.MaxPageHeight"/>）。</summary>
    public const int MinPageSize = 64;

    private RinkakuRepresentationKind()
    {
    }

    public override string Name => RinkakuEncoding.KindName;

    public override GlyphEffects Effects => GlyphEffects.All;

    public override int Version => RinkakuAtlasBaker.Version;

    public override GlyphRepresentationSettings ParseSettings(SpecSection section)
    {
        section.RequireKnownKeys("indexEm", "maxPageSize");

        var indexEm = section.OptionalNumber("indexEm") ?? 16.0;
        if (indexEm != Math.Floor(indexEm) || indexEm < MinIndexEm || indexEm > MaxIndexEm)
        {
            throw section.Fail("indexEm", FormattableString.Invariant($"indexEm は {MinIndexEm}〜{MaxIndexEm} の整数にしてください: {indexEm}"));
        }

        var maxPageSize = section.OptionalNumber("maxPageSize") ?? RinkakuEncoding.MaxPageHeight;
        if (maxPageSize != Math.Floor(maxPageSize) || maxPageSize < MinPageSize || maxPageSize > RinkakuEncoding.MaxPageHeight)
        {
            throw section.Fail("maxPageSize", FormattableString.Invariant(
                $"maxPageSize は {MinPageSize}〜{RinkakuEncoding.MaxPageHeight} の整数にしてください（ページの高さの上限・幅は常に {RinkakuEncoding.PageWidth}）: {maxPageSize}"));
        }

        return new RinkakuSpecSettings((int)indexEm, (int)maxPageSize);
    }
}
```
<!-- /gen -->

- `Name` はベイク仕様の `"kind"` の綴り。
- `Effects` は描ける効果。**宣言した効果は README §6.1 の範囲の全部で正しく描く**（一部の値だけ描けるなら宣言しない・README §6.3）。ホストは
  エッジスタイルが使う効果とこれを照合し、合わなければ止める。
- `Version` は**同じ入力から出る出力が変わる変更をしたら上げる**。上げると、その種類のフォントだけが再インポートされる（README §7.5）。
- `ParseSettings` は `SpecSection` の型付きの読み口だけで読む。`RequireKnownKeys` で未知のキーを誤りにし、範囲の外は `Fail(キー, 文面)` で
  キー名を名指して止める（README §2.1）。JSON の読み手の型は公開されていない。

設定は `GlyphRepresentationSettings` の派生で持つ。`ToJsonMembers` は、ベイク仕様を書き出すとき（`FontBakeSpec.ToJson`・出荷用のベイク仕様など）に使われる。

<!-- gen:source:Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs#RinkakuSpecSettings -->
```csharp
// Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs
/// <summary>ベイク仕様の Rinkaku の節（README §5 の表）。</summary>
public sealed class RinkakuSpecSettings : GlyphRepresentationSettings
{
    public RinkakuSpecSettings(int indexEm, int maxPageSize)
    {
        IndexEm = indexEm;
        MaxPageSize = maxPageSize;
    }

    public override GlyphRepresentationKind Kind => RinkakuRepresentationKind.Instance;

    /// <summary>グリッドの 1em のセルの数。</summary>
    public int IndexEm { get; }

    /// <summary>1 ページの最大の高さ（texel）。</summary>
    public int MaxPageSize { get; }

    /// <summary>ベイカーの設定。</summary>
    public RinkakuBakeSettings ToBakeSettings()
    {
        return new RinkakuBakeSettings { IndexEm = IndexEm, MaxPageHeight = MaxPageSize };
    }

    public override IEnumerable<KeyValuePair<string, string>> ToJsonMembers()
    {
        yield return new KeyValuePair<string, string>("indexEm", IndexEm.ToString(CultureInfo.InvariantCulture));
        yield return new KeyValuePair<string, string>("maxPageSize", MaxPageSize.ToString(CultureInfo.InvariantCulture));
    }
}
```
<!-- /gen -->

## 4. データを作る

ベイカーにはタイポグラフィテーブルと輪郭が `FontFaceData` で渡る。グリフインデックスごとに `Outline(glyph)` で輪郭（フォント単位・y 上向き・
`UnitsPerEm` で em に直す）を取り、点列のまま読むか、`GlyphOutline.EnumerateSegments` で区間（直線と 2 次ベジエ・暗黙の中点を補った閉じた鎖）に
して読む（README §8）。インク境界は `InkBox(glyph)`、cmap を持たないグリフ（.notdef）も普通のグリフインデックスを持つ（README §3.2）。

距離以外の値（番地・索引・制御点）を持つテクスチャは、中身を `GlyphDataPage`（R8 か RGBA8・行 0 が下端）で作り、ベイカーが
`GlyphBakeContext.AddDataTexture` でテクスチャにする（README §4.4）。Rinkaku のデータを作る処理（`RinkakuAtlasBaker`）は契約の外の型で、
読み物として参照する。

## 5. グリフ表現

<!-- gen:source:Packages/info.someiyoshino.rinkaku/Runtime/RinkakuRepresentation.cs#RinkakuRepresentation.Create,MaterialScope,ConfigureMaterial,GetTexture,WriteGeometry -->
```csharp
// Packages/info.someiyoshino.rinkaku/Runtime/RinkakuRepresentation.cs（抜粋）
/// <summary>
/// ベイカーがベイクした結果からグリフ表現を作る（ベイカー専用）。<paramref name="effects"/> は種類の記述子の宣言を写す。健全性は
/// <c>T2FontAsset.Create</c> が <see cref="Validate"/> で検査する。
/// </summary>
public static RinkakuRepresentation Create(Shader shader, GlyphEffects effects, int indexEm, RinkakuGlyphCell[] cells, Texture2D[] pages)
{
    if (shader == null)
    {
        throw new ArgumentNullException(nameof(shader));
    }
    if (cells == null)
    {
        throw new ArgumentNullException(nameof(cells));
    }
    if (pages == null)
    {
        throw new ArgumentNullException(nameof(pages));
    }

    var representation = CreateInstance<RinkakuRepresentation>();
    representation.InitializeRepresentation(shader, effects);
    representation.indexEm = indexEm;
    representation.cells = cells;
    representation.pages = pages;
    return representation;
}

/// <summary>グリッドの細かさをマテリアルに載せるので、フォントごとにマテリアルを分ける。</summary>
public override Object? MaterialScope => this;

public override void ConfigureMaterial(Material material)
{
    material.SetFloat(IndexEmId, indexEm);
}

public override Texture? GetTexture(int glyph)
{
    var cell = cells[glyph];
    return cell.IsEmpty ? null : pages[cell.page];
}

public override void WriteGeometry(int glyph, in GlyphMetrics metrics, float quadExtentEm, GlyphGeometry geometry)
{
    var cell = cells[glyph];
    if (cell.IsEmpty)
    {
        return;
    }

    // セルの plane（グリッドが覆う範囲）を、インク境界から quadExtentEm だけ広げた矩形へ狭める。インデックスとセグメントテーブルの番地は変わらない
    //（狭い quad はインデックスの内側の部分だけを引く）。
    var left = cell.planeLeft;
    var bottom = cell.planeBottom;
    var right = cell.planeRight;
    var top = cell.planeTop;
    NarrowToInk(metrics, quadExtentEm, ref left, ref bottom, ref right, ref top);

    float offset = RinkakuEncoding.PackedOffset(cell);
    float dataBase = RinkakuEncoding.PackedDataBase(cell);
    geometry.AddQuad(left, bottom, right, top, offset, dataBase, offset, dataBase);
}
```
<!-- /gen -->

- **クラス名とファイル名を一致させる**（ScriptableObject で、Unity が MonoScript を解決するため・README §3.4）。
- 作るときに `InitializeRepresentation(シェーダ, 描ける効果)` を呼ぶ（`protected`）。
- `WriteGeometry` は位置（em）と UV0.xy だけを書く。UV0.zw（頂点の em 座標）は器 `GlyphGeometry` が書く（README §4.2）。インクの無いグリフには
  何も書かない。幾何は、インク境界を `quadExtentEm` だけ広げた範囲を覆い、それより広い値（`WholeQuad` を含む）には覆える範囲の全部を書く。
  `NarrowToInk`（`protected static`）が、持っている矩形をその範囲へ狭める（README §4.3）。
- 整数の番地を UV0.xy に入れるなら、float が正確に持てる 2^24 未満にし、シェーダは補間しない（`nointerpolation`）で受ける（README §4.2）。
- フォント全体のテクスチャや定数をマテリアルに載せるなら、`MaterialScope` で自分を返し、`ConfigureMaterial` で載せる。既定（シェーダ）のままなら、
  同じ種類の全フォントがマテリアルを共有する（README §4.5）。
- `Validate` は、アセットの健全性の検査（グリフの数とデータの数が合うか）を文面で返す。

## 6. シェーダ

README §5 の S1〜S9 を全部満たす。同梱の [`T2Font-SDF.shader`](../../Packages/info.someiyoshino.t2font/Shaders/T2Font-SDF.shader) か
[`Rinkaku.shader`](../../Packages/info.someiyoshino.rinkaku/Shaders/Rinkaku.shader) から始め、頂点の処理と、面と縁取りの合成（エッジスタイルの
uniform の解釈）を残して、**符号付き距離の求め方だけを替える**のが早い。距離を SDF と同じ意味（インク領域の内側が正・em）で持てば、エッジスタイルは
同じ式のまま全部の効果で成り立つ（Rinkaku の README の冒頭）。

- 入力は POSITION・COLOR・TEXCOORD0（float4）だけ（S1）。AA は UV0.zw の微分から得る、常に画面の 1px（S3）。
- 実行時に切り替えるキーワードは `#pragma multi_compile __ UNITY_UI_CLIP_RECT`（S7。マテリアルを実行時に作るので `shader_feature` は落ちる）。
- データのテクスチャは `Load` で整数の番地から読むと、UV の丸めの誤差が無い（Rinkaku の README §3.5）。`Load` には `#pragma target 3.5` が要る。

## 7. ベイカー

<!-- gen:source:Packages/info.someiyoshino.rinkaku/Editor/RinkakuGlyphBaker.cs#RinkakuGlyphBaker -->
```csharp
// Packages/info.someiyoshino.rinkaku/Editor/RinkakuGlyphBaker.cs
/// <summary>
/// Rinkaku のグリフ表現のベイカー（README §4）。<see cref="RinkakuAtlasBaker"/> がベイクしたページをデータテクスチャにし（T2Font の
/// <c>GlyphBakeContext.AddDataTexture</c>）、<see cref="RinkakuRepresentation"/> を作る。T2Font のインポーターが Editor の型の一覧から見つける。
/// </summary>
public sealed class RinkakuGlyphBaker : GlyphRepresentationBaker
{
    /// <summary>シェーダのパッケージの中の置き場。</summary>
    public const string ShaderPath = "Shaders/Rinkaku.shader";

    private const string PackageName = "info.someiyoshino.rinkaku";

    public override GlyphRepresentationKind Kind => RinkakuRepresentationKind.Instance;

    public override GlyphRepresentation Bake(GlyphBakeContext context, FontFaceData face, GlyphRepresentationSettings settings)
    {
        if (!(settings is RinkakuSpecSettings rinkaku))
        {
            throw new ArgumentException($"Rinkaku のベイカーに別の種類の設定 {settings?.GetType().FullName} が渡されました。", nameof(settings));
        }

        var shader = context.LoadShader(context.PackageAssetPath(PackageName, ShaderPath));
        var atlas = RinkakuAtlasBaker.Bake(face, rinkaku.ToBakeSettings());

        var pages = new Texture2D[atlas.Pages.Count];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = context.AddDataTexture($"page{i}", atlas.Pages[i]);
        }

        var inked = atlas.Stats.Where(s => s.Cells > 0).ToList();
        var meanBytes = inked.Count > 0 ? inked.Average(s => s.Bytes) : 0;
        var meanItems = inked.Count > 0 ? inked.Sum(s => (double)s.TotalItems) / inked.Sum(s => s.Cells) : 0;
        var maxItems = inked.Count > 0 ? inked.Max(s => s.MaxItems) : 0;
        context.Report(FormattableString.Invariant(
            $"{pages.Length} ページ（{string.Join("・", atlas.Pages.Select(p => $"{p.Width}×{p.Height}"))}）・indexEm {atlas.IndexEm}"));
        context.Report(FormattableString.Invariant($"1 字 平均 {meanBytes:0} B・評価本数 平均 {meanItems:0.00}／最大 {maxItems}"));
        return RinkakuRepresentation.Create(shader, Kind.Effects, atlas.IndexEm, atlas.Cells, pages);
    }
}
```
<!-- /gen -->

- インポーターが Editor の `TypeCache` でベイカーを集め、**引数の無い公開の構築子**で作る。同じ種類のベイカーが 2 つあると、インポートが失敗する
  （README §7.4）。
- シェーダは `context.LoadShader(context.PackageAssetPath(パッケージ名, パッケージの中のパス))` で読む。パッケージの導入の方法（git URL・フォルダの写し）に
  依らず解決し、シェーダが変われば再インポートされる依存を張る。
- テクスチャは `AddDataTexture`（データ）か `AddObject`（自分で作った物）でサブアセットにする。インポートが途中で失敗すると、渡したサブアセットは
  インポーターが捨てる。
- `Report` の文はインポートの報告（Console の 1 行）に足される。誤りは `FontBakeException` で止める（アセットは作られない）。
- 返すグリフ表現の種類の名前と描ける効果が記述子と違えば、インポートが失敗する。

## 8. 適合検査

グリフ表現でベイクしたフォントを `GlyphConformance` に渡し、誤りの文面の一覧が空であることを確かめる（README §10）。テストの枠組みに依らないので、
利用側の EditMode のテストから呼ぶ。本リポジトリの同梱の SDF の検査は次のとおり（フォントアセットは検査用のベイク仕様を `AssetDatabase` で読む）。

<!-- gen:source:Assets/Tests/EditMode/T2Font/GlyphConformanceTests.cs#GlyphConformanceTests -->
```csharp
// Assets/Tests/EditMode/T2Font/GlyphConformanceTests.cs
/// <summary>同梱の SDF のグリフ表現が適合検査（パッケージの README §10）を通ること。</summary>
[TestFixture]
public class GlyphConformanceTests
{
    [Test]
    public void 全効果の外延はエッジスタイルの外延の上限と一致する()
    {
        Assert.That(GlyphConformance.OuterExtentEm(GlyphEffects.All), Is.EqualTo(GlyphEdgeStyle.MaxOuterExtentEm).Within(1e-6f));
        Assert.That(GlyphConformance.OuterExtentEm(GlyphEffects.None), Is.EqualTo(0f));
    }

    [Test]
    public void 同梱のSDFの幾何は適合する()
    {
        Assert.That(GlyphConformance.CheckGeometry(TestFontAssets.Mplus1cSdf()), Is.Empty);
    }

    [Test]
    public void 同梱のSDFの描画は適合する()
    {
        if (!GlyphConformance.CanRender)
        {
            Assert.Ignore("描画装置が無い（batchmode の -nographics など）ので、描画の適合検査を回せません。");
        }
        Assert.That(GlyphConformance.CheckRendering(TestFontAssets.Mplus1cSdf(), 0x25A0), Is.Empty);
    }
}
```
<!-- /gen -->

- `CheckGeometry` は全グリフの幾何（UV0.zw・覆う範囲・狭い `quadExtentEm` での範囲）を見る。描画装置は要らない。
- `CheckRendering` は実際に描いた画素で S2・S3・S4・S6・S9・矩形のクリップ・幾何の範囲を見る。描画装置が要る（`CanRender`。batchmode の
  `-nographics` では回らない）。字には「■」（U+25A0）のような塗りつぶしの字を渡す。
- 検査用のベイク仕様は、テストのフォルダに `"kind"` を自分の種類にして置く。元フォントのライセンスに注意する。

## 9. 公開する

- 出力が変わる変更のたびに記述子の `Version` を上げる（その種類のフォントだけが再インポートされる・README §7.5）。
- T2Font に依存するパッケージとして配る。T2Font の版と組で使うなら、Rinkaku のように同じタグで配ると導入を誤りにくい（Rinkaku の README §11）。
