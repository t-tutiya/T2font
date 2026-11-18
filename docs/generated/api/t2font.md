<!-- 自動生成（Tests/ApiReferenceDocTests.cs）。手で編集しない。パッケージを写し直したら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# T2Font の API — 契約の内

利用者が使うことを README が想定している公開型（役割別）。索引と「契約の内と外」の意味は [`README.md`](README.md)。
文中の「README §n」は [T2Font の README](../../../Packages/info.someiyoshino.t2font/README.md) の節。

## ホスト（字を画面へ置く側）

### T2FontAsset

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font`・ソース [`Packages/info.someiyoshino.t2font/Runtime/T2FontAsset.cs`](../../../Packages/info.someiyoshino.t2font/Runtime/T2FontAsset.cs)

```csharp
public sealed class T2FontAsset : ScriptableObject
```

フォントアセット＝タイポグラフィテーブルとグリフ表現（README §1・§3.1）。

**手で作らない**。ベイク仕様（`.t2font`）をインポーターがインポートし、元フォントからベイクした生成物で、Library に作られる（README §2.4）。グリフ表現とそのテクスチャはこのアセットのサブアセットで、ほかのアセットを指すのはグリフ表現のシェーダの参照だけ。

字は昇順の配列を二分探索で引く（約 7,000 字で比較 13 回・割り当て無し・README §3.2）。値の単位は `GlyphMetrics` を見よ。

#### `Face`

```csharp
public FontFaceMetrics Face { get; }
```

フォント単位の値（縦メトリクス・em）。

#### `Codepoints`

```csharp
public IReadOnlyList<int> Codepoints { get; }
```

収めた字（昇順）。添字がグリフインデックス（README §3.2）。

#### `GlyphCount`

```csharp
public int GlyphCount { get; }
```

グリフの数（cmap を持たないグリフを含む）。

#### `Notdef`

```csharp
public int Notdef { get; }
```

代替グリフ（元フォントの glyph 0）のグリフインデックス。

#### `Representation`

```csharp
public GlyphRepresentation Representation { get; }
```

グリフ表現（README §3.4）。インポートが作ったアセットでは null にならない。

#### `SourceHash`

```csharp
public string SourceHash { get; }
```

生成元（ジェネレーターのバージョン・ベイク仕様・元フォント）のハッシュ。

#### `Create()`

```csharp
public static T2FontAsset Create(FontFaceMetrics face, int[] codepoints, GlyphMetrics[] glyphs, int notdef, GlyphRepresentation representation, string sourceHash)
```

インポーターがベイクした結果からアセットを作る（インポーター専用。既存のアセットを書き換える経路は持たない）。字が昇順でない・グリフインデックスがタイポグラフィテーブルの外を指す・表現がタイポグラフィテーブルと合わないときは失敗する（README §2.1）。

- 例外 `ArgumentException`: タイポグラフィテーブルかグリフ表現が不正。

#### `Contains()`

```csharp
public bool Contains(int codepoint)
```

`codepoint` の字を収録しているか。

#### `Resolve()`

```csharp
public int Resolve(int codepoint, out bool isSubstitute)
```

字のグリフインデックス（README §3.1）。収録していない字は代替グリフを返して `isSubstitute` を真にし、コードポイントごとに 1 回 `LogError` を出す——送り幅 0 で消すと、出荷物で字が黙って消える（README §2.1）。

#### `Metrics()`

```csharp
public GlyphMetrics Metrics(int glyph)
```

グリフ `glyph` のタイポグラフィテーブルの記録（送り幅とインク境界・em）。

#### `RepresentationOf()`

```csharp
public GlyphRepresentation RepresentationOf(int glyph)
```

グリフ `glyph` を描くグリフ表現（README §3.1）。今の版では常に `Representation`。1 フォントに複数のグリフ表現を持たせる拡張の口なので、ホストはグリフごとにこれを通して引く。

### GlyphRepresentation

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font`・ソース [`Packages/info.someiyoshino.t2font/Runtime/GlyphRepresentation.cs`](../../../Packages/info.someiyoshino.t2font/Runtime/GlyphRepresentation.cs)

```csharp
public abstract class GlyphRepresentation : ScriptableObject
```

グリフ表現（README §1・§3.4・§4）。`T2FontAsset` のサブアセットで、インポーターがグリフ表現の種類のベイカーに作らせる。

ホストはグリフ表現の具体型を知らない: グリフインデックスを渡して幾何（`WriteGeometry`）とテクスチャ（`GetTexture`）を受け取り、マテリアルは `GlyphMaterialCache` が `Shader` から作る。グリフ表現を足すパッケージは派生クラスを **クラス名とファイル名を一致させて**置く（Unity が MonoScript を解決するため）。

#### `Kind`

```csharp
public abstract string Kind { get; }
```

グリフ表現の種類の名前（ベイク仕様の `representation.kind` と同じ綴り。診断と検査用）。

#### `Shader`

```csharp
public Shader? Shader { get; }
```

グリフを描くシェーダ（README §5 の契約を満たすもの）。インポーターが参照を入れる。null はインポートの誤り。

#### `Effects`

```csharp
public GlyphEffects Effects { get; }
```

描ける効果（README §6.3）。ベイカーが種類の記述子から写す。

#### `MaterialScope`

```csharp
public virtual Object? MaterialScope { get; }
```

マテリアルを共有してよい範囲（README §4.5）。同じ値のグリフ表現どうしは、エッジスタイルが同じなら同じマテリアルを使う。既定はシェーダ（＝同じ種類の全フォントで共有）。フォント全体のテクスチャをマテリアルに載せるグリフ表現は自分を返す。

#### `ConfigureMaterial()`

```csharp
public virtual void ConfigureMaterial(Material material)
```

マテリアルへグリフ表現の定数を入れる（距離レンジ・フォント全体のテクスチャなど）。`GlyphMaterialCache` がマテリアルを作るときに 1 回だけ呼ぶ。エッジスタイルの uniform（README §6.1）はキャッシュが入れるので、ここでは書かない。

#### `GetTexture()`

```csharp
public abstract Texture? GetTexture(int glyph)
```

グリフのテクスチャ（ホストが `CanvasRenderer.SetTexture` で `_MainTex` に入れる・README §4.4）。インクの無いグリフでは呼ばれない。

#### `WriteGeometry()`

```csharp
public abstract void WriteGeometry(int glyph, in GlyphMetrics metrics, float quadExtentEm, GlyphGeometry geometry)
```

グリフの幾何を em で書く（README §4.2・§4.3）。インクの無いグリフでは何も書かない。UV0.zw はホストの器が書くので、グリフ表現が書くのは位置（em）と UV0.xy だけ。インク境界を `quadExtentEm` だけ広げた範囲を覆う（README §4.3）。ホストは描くエッジスタイルと表示サイズから `GlyphEdgeStyle.QuadExtentEm` で求めて渡す。グリフ表現が覆える範囲（描けると宣言した効果の外延の上限＋サポートする最小の表示サイズの AA の半分）より広い値には、覆える範囲の全部を書く——`WholeQuad` はそのための値。

#### `WholeQuad`

```csharp
public const float WholeQuad = float.PositiveInfinity
```

`WriteGeometry` の `quadExtentEm` に渡すと、グリフ表現が覆える範囲の全部を書く（検査・計測・エッジスタイルを決める前の幾何）。

#### `NarrowToInk()`

```csharp
protected static void NarrowToInk(in GlyphMetrics metrics, float quadExtentEm, ref float left, ref float bottom, ref float right, ref float top)
```

グリフ表現が持つ矩形（em）を、インク境界から `quadExtentEm` だけ広げた矩形へ狭める（広げはしない）。インクの無い記録か `WholeQuad` なら変えない。派生が `WriteGeometry` で使う。

#### `Validate()`

```csharp
public virtual string? Validate(int glyphCount)
```

アセットの健全性の検査。`glyphCount` はタイポグラフィテーブルのグリフの数。問題があればその文面、無ければ null。`T2FontAsset.Create` と利用側の出荷の検査が呼ぶ。

#### `InitializeRepresentation()`

```csharp
protected void InitializeRepresentation(Shader shader, GlyphEffects effects)
```

派生クラスの生成の口から呼ぶ（シェーダと描ける効果を入れる）。

### GlyphGeometry

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font`・ソース [`Packages/info.someiyoshino.t2font/Runtime/GlyphGeometry.cs`](../../../Packages/info.someiyoshino.t2font/Runtime/GlyphGeometry.cs)

```csharp
public sealed class GlyphGeometry
```

グリフ表現がグリフの幾何を書き込む器（README §4.2）。ホストが 1 つを持ち回し、グリフごとに `Clear` して使う（割り当て無し）。

位置は em で受け取り、`CopyTo` で表示サイズを掛けて `Mesh` へ詰める。UV0 は `(u, v, emX, emY)`——zw は **この器が書く**その頂点の em 座標で、シェーダはその微分から 1 表示 px が何 em かを得る（README §5 S3）。グリフ表現は zw を書けないので、この契約は破れない。

#### `VertexCount`

```csharp
public int VertexCount { get; }
```

書いた頂点の数。

#### `IndexCount`

```csharp
public int IndexCount { get; }
```

書いた三角形の頂点番号の数（三角形の数の 3 倍）。

#### `Positions`

```csharp
public IReadOnlyList<Vector3> Positions { get; }
```

書いた頂点の位置（em）。検査用。

#### `Uv0`

```csharp
public IReadOnlyList<Vector4> Uv0 { get; }
```

書いた頂点の UV0（xy＝グリフ表現・zw＝em）。検査用。

#### `Clear()`

```csharp
public void Clear()
```

空にする。

#### `AddVertex()`

```csharp
public int AddVertex(float emX, float emY, float u, float v)
```

頂点を 1 つ書き、その番号を返す。

#### `AddTriangle()`

```csharp
public void AddTriangle(int a, int b, int c)
```

三角形を 1 つ書く（頂点番号は `AddVertex` の返り値）。

#### `AddQuad()`

```csharp
public void AddQuad(float emLeft, float emBottom, float emRight, float emTop, float uLeft, float vBottom, float uRight, float vTop)
```

軸に沿った矩形を 1 つ書く（頂点 4 つ・三角形 2 つ）。位置は em、UV はグリフ表現の値（左下と右上）。

#### `CopyTo()`

```csharp
public void CopyTo(Mesh mesh, float fontSize)
```

書いた幾何を `mesh` へ詰める（位置＝em × `fontSize`）。`mesh` は先に `Clear` する。uGUI の `CanvasRenderer.SetMesh` は中身を写すので、詰めた後に呼ぶのはホストの仕事。

### GlyphMaterialCache

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font`・ソース [`Packages/info.someiyoshino.t2font/Runtime/GlyphMaterialCache.cs`](../../../Packages/info.someiyoshino.t2font/Runtime/GlyphMaterialCache.cs)

```csharp
public sealed class GlyphMaterialCache : IDisposable
```

グリフのマテリアルの置き場（README §4.5）。（グリフ表現の `GlyphRepresentation.MaterialScope`・エッジスタイル）ごとに 1 つ作って共有する。

作る時点は**フォントを読んだ時点**にするのがよい（`Prewarm`）——描画中に作るとヒッチになる。面の色とフェードは頂点色でマテリアルに持たせないので（README §5 S2）、色違いのエッジスタイルはマテリアルを共有する。テクスチャは `CanvasRenderer.SetTexture` で字ごとに渡すので、`MaterialScope` がシェーダのグリフ表現（SDF など）ではフォントが違ってもマテリアルを共有する。

マテリアルは Unity のネイティブのオブジェクトで、参照を手放しても GC では回収されない。所有者は破棄のときに `Dispose` を呼び、フォントを外すときは `Release` でそのフォントに属するマテリアルを捨てる。

#### `Count`

```csharp
public int Count { get; }
```

持っているマテリアルの数。

#### `Get()`

```csharp
public Material Get(GlyphRepresentation representation, in GlyphEdgeStyle style)
```

`representation` で `style` を描くマテリアル。無ければ作る。エッジスタイルが範囲の外か、グリフ表現の描けない効果を使うなら止める（README §6.1・§6.3）——黙って近似すると、グリフ表現によって見た目が変わる。

- 例外 `ArgumentException`: エッジスタイルが範囲の外か、グリフ表現が描けない効果を使う。

#### `Prewarm()`

```csharp
public void Prewarm(GlyphRepresentation representation, IEnumerable<GlyphEdgeStyle> styles)
```

`representation` で `styles` を描くマテリアルを先に作る（フォントを読んだ時点で 1 回）。

#### `Check()`

```csharp
public static string? Check(GlyphRepresentation representation, in GlyphEdgeStyle style)
```

エッジスタイルをグリフ表現で描けるか。描けなければ理由の文面、描けるなら null（README §6.3）。ホストがエッジスタイルをフォントに使う前の検査に使う。

#### `Release()`

```csharp
public void Release(Object scope)
```

`scope`（グリフ表現の `GlyphRepresentation.MaterialScope`）に属するマテリアルを捨てる。

#### `Dispose()`

```csharp
public void Dispose()
```

全マテリアルを捨てる（所有者の破棄のときに呼ぶ）。

### GlyphEdgeStyle

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/GlyphEdgeStyle.cs`](../../../Packages/info.someiyoshino.t2font/Core/GlyphEdgeStyle.cs)

```csharp
public readonly struct GlyphEdgeStyle : IEquatable<GlyphEdgeStyle>
```

エッジスタイルの値一式＝縁取りの色と、2 つの境界（面の縁・縁取りの外縁）それぞれの位置とぼかし（README §6.1）。値は em で、シェーダの uniform に 1 対 1 で対応する。**どの値も境界 1 本の位置かランプ 1 本の幅だけを動かす**。AA はシェーダが常に画面の 1px で掛けるのでここには無い——ぼかしの 2 値は AA に足す幅である（README §5 S3）。

値が等しいエッジスタイルは同じマテリアルを共有できるので、値等価の構造体にしている（`GlyphMaterialCache` のキー）。面の色とフェードは頂点色で、ここには無い（README §5 S2）。

#### `FaceDilateMin`

```csharp
public const float FaceDilateMin = -0.03f
```

`FaceDilate` の下限。画の半幅（ゴシックの本文書体で約 0.035em）を削り切らない。

#### `FaceDilateMax`

```csharp
public const float FaceDilateMax = 0.04f
```

`FaceDilate` の上限。

#### `FaceSoftnessMax`

```csharp
public const float FaceSoftnessMax = 0.06f
```

`FaceSoftness` の上限（下限は 0）。

#### `OutlineWidthMax`

```csharp
public const float OutlineWidthMax = 0.08f
```

`OutlineWidth` の上限（下限は 0）。これを超えると画数の多い字の内側が縁で埋まり始める。

#### `OutlineSoftnessMax`

```csharp
public const float OutlineSoftnessMax = 0.06f
```

`OutlineSoftness` の上限（下限は 0）。

#### `MaxOuterExtentEm`

```csharp
public const float MaxOuterExtentEm = FaceDilateMax + OutlineWidthMax + (OutlineSoftnessMax / 2f)
```

全キーを上限にした組の外延（em）＝インク境界から外へ、効果が描かれうる最大の距離（README §6.2）。外縁のぼかしは外縁を中心に置くので半分だけ外へ出る。グリフ表現の幾何はこれ＋AA の半分を覆う（README §4.3）。

#### `None`

```csharp
public static readonly GlyphEdgeStyle None
```

効果を使わないエッジスタイル（縁取り無し・ずらし無し・ぼかし無し）。

#### `ColorR, ColorG, ColorB, ColorA`

```csharp
public readonly float ColorR, ColorG, ColorB, ColorA
```

縁取りの色（rgba・sRGB。α は縁取りの帯の不透明度）。

#### `FaceDilate`

```csharp
public readonly float FaceDilate
```

面の縁のずらし（em・＋で太く）。面の縁と縁取りの外縁を**一緒に**動かすので、縁取りの幅は変わらない（`_FaceDilate`）。

#### `FaceSoftness`

```csharp
public readonly float FaceSoftness
```

面の縁のぼかし（em）＝面と縁取りの色の遷移を AA に足す幅。50% の位置は面の縁のまま（`_FaceSoftness`）。

#### `OutlineWidth`

```csharp
public readonly float OutlineWidth
```

縁取りの幅（em）＝面の縁から外への帯の幅（`_OutlineWidth`）。

#### `OutlineSoftness`

```csharp
public readonly float OutlineSoftness
```

縁取りの外縁のぼかし（em）＝外縁の α のランプを AA に足す幅。50% の位置は外縁のまま（`_OutlineSoftness`）。

#### `GlyphEdgeStyle（構築子）`

```csharp
public GlyphEdgeStyle(float colorR, float colorG, float colorB, float colorA, float faceDilate, float faceSoftness, float outlineWidth, float outlineSoftness)
```

#### `Uses`

```csharp
public GlyphEffects Uses { get; }
```

このエッジスタイルが使う効果（値が 0 でないキー・README §6.3）。ホストはグリフ表現の能力（`GlyphRepresentation.Effects`）がこれを含むことを、エッジスタイルをそのフォントに使う前に確かめる。縁取りの色は幅が 0 なら使われない。

#### `OuterExtentEm`

```csharp
public float OuterExtentEm { get; }
```

このエッジスタイルがインク境界から外へ描く最大の距離（em・README §6.2）: 面の縁のずらし＋（縁取りの幅＋外縁のぼかしの半分）と面の縁のぼかしの半分の大きい方。面の縁のずらしが負で縁がインク境界の内側に入るときは 0。

#### `QuadExtentEm()`

```csharp
public float QuadExtentEm(float displayPx)
```

このエッジスタイルを `displayPx` の大きさで描くときに、幾何がインク境界から広げて覆う幅（em・README §4.3）: `OuterExtentEm` ＋ 表示の 1 画素。AA に要るのは半画素で、残りの半画素は表示を 0.5 倍まで縮める演出のための余裕。

#### `FindOutOfRange()`

```csharp
public string? FindOutOfRange()
```

範囲（README §6.1）の外の値があれば、その最初の 1 つを「キー 値（範囲）」の文面で返す。全部が範囲の内なら null。範囲の外のエッジスタイルは、どのグリフ表現でも正しく描けるとは限らない（外延がグリフ表現の幾何を越える）。

#### `Equals()`

```csharp
public bool Equals(GlyphEdgeStyle other)
```

#### `Equals()`

```csharp
public override bool Equals(object? obj)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `GetHashCode()`

```csharp
public override int GetHashCode()
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `operator ==`

```csharp
public static bool operator ==(GlyphEdgeStyle left, GlyphEdgeStyle right)
```

#### `operator !=`

```csharp
public static bool operator !=(GlyphEdgeStyle left, GlyphEdgeStyle right)
```

### GlyphEffects

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/GlyphEffects.cs`](../../../Packages/info.someiyoshino.t2font/Core/GlyphEffects.cs)

```csharp
public enum GlyphEffects
```

エッジスタイルの効果の種類（README §6.3）。グリフ表現は描ける効果をこれで宣言し、ホストはエッジスタイルが使う効果（`GlyphEdgeStyle.Uses`）と照合する。

- `None = 0` — 効果を使わない（面を描くだけ）。
- `FaceDilate = 1 << 0` — 面の縁のずらし（`_FaceDilate`）。
- `FaceSoftness = 1 << 1` — 面の縁のぼかし（`_FaceSoftness`）。
- `Outline = 1 << 2` — 縁取り（`_OutlineWidth` と `_OutlineColor`）。
- `OutlineSoftness = 1 << 3` — 外縁のぼかし（`_OutlineSoftness`）。
- `All = FaceDilate | FaceSoftness | Outline | OutlineSoftness` — 全部。

### GlyphMetrics

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/GlyphMetrics.cs`](../../../Packages/info.someiyoshino.t2font/Core/GlyphMetrics.cs)

```csharp
public struct GlyphMetrics
```

グリフ 1 つのタイポグラフィテーブルの記録（README §3.3）。単位は em で、ペン原点とベースラインを原点とし y は上向き。

ink\* は**インク境界そのもの**で、グリフ表現が描くための余白（距離レンジなど）を含まない——余白はグリフ表現の持ち物で、タイポグラフィテーブルに入れるとグリフ表現を替えたときに値が変わる（README §2.3）。インクの無い字（空白）は ink の幅と高さが 0 で、送り幅だけを持つ。

Unity が直列化する（`T2FontAsset` が配列で持つ）ので、公開フィールドの構造体にしている。

#### `advance`

```csharp
public float advance
```

送り幅（em）。

#### `inkLeft`

```csharp
public float inkLeft
```

インク境界の左端（em・ペン原点から）。

#### `inkBottom`

```csharp
public float inkBottom
```

インク境界の下端（em・ベースラインから・上が正）。

#### `inkRight`

```csharp
public float inkRight
```

インク境界の右端（em）。

#### `inkTop`

```csharp
public float inkTop
```

インク境界の上端（em）。

#### `GlyphMetrics（構築子）`

```csharp
public GlyphMetrics(float advance, float inkLeft, float inkBottom, float inkRight, float inkTop)
```

#### `HasInk`

```csharp
public bool HasInk { get; }
```

インクがある（描く幾何を持つ）か。

### FontFaceMetrics

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/FontFaceMetrics.cs`](../../../Packages/info.someiyoshino.t2font/Core/FontFaceMetrics.cs)

```csharp
public struct FontFaceMetrics
```

フォント単位の値（README §3.1）。単位は em（字の大きさに対する比）で、ベースラインを原点とし y は上向き。タイポグラフィテーブルの一部で、グリフ表現によらず同じ値になる（README §2.3）。

Unity が直列化する（`T2FontAsset` が持つ）ので、公開フィールドの構造体にしている。

#### `ascender`

```csharp
public float ascender
```

アセンダ（em・元フォントの `hhea`）。

#### `descender`

```csharp
public float descender
```

ディセンダ（em・下が負）。

#### `lineGap`

```csharp
public float lineGap
```

行間の追加分（em）。

#### `FontFaceMetrics（構築子）`

```csharp
public FontFaceMetrics(float ascender, float descender, float lineGap)
```

### TextCodepoints

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/TextCodepoints.cs`](../../../Packages/info.someiyoshino.t2font/Core/TextCodepoints.cs)

```csharp
public static class TextCodepoints
```

文字列を字（Unicode のコードポイント）の並びとして読む（README §3.2）。字はグリフインデックスへ 1 字ずつ引くので、C# の `char`（UTF-16 の符号単位）で回すと BMP 外の字（𠮟・𩸽・𠮷 など）がサロゲートの 2 つに割れ、どちらもフォントに無い字になる。

サロゲートペアは 1 字。**片割れのサロゲートは捨てず、その符号単位の値をそのまま 1 字として返す**——フォントに無い字として代替グリフで見え、誤りが黙って消えない（README §2.1）。

#### `Read()`

```csharp
public static int Read(string text, int index, out int length)
```

`text` の `index` から 1 字を読む。`length` はその字の符号単位の数（1 か 2）で、次の字は `index + length` から始まる。

#### `Count()`

```csharp
public static int Count(string text)
```

`text` の字の数（サロゲートペアは 1 字）。

#### `AddDrawable()`

```csharp
public static void AddDrawable(ISet<int> set, string text)
```

文字列の字（サロゲートペアは 1 字）を、描く字の集合（フォントの文字セット・本文の字）へ足す。制御文字（U+0000〜001F・U+007F）と BOM（U+FEFF）は描く字ではないので足さない。

**片割れのサロゲートは失敗させる**（`Read` と扱いが違う）。文字セットは「ベイクする字」の要求で、片割れは字ではない——黙って捨てると要求が消え、符号単位のまま入れるとベイクできない字が要求に残る。

- 例外 `ArgumentException`: 片割れのサロゲートがある。

#### `Display()`

```csharp
public static string Display(int codepoint)
```

診断の文面や GameObject の名前に使う表示（スカラー値ならその字、片割れのサロゲートは `U+XXXX`）。

## 検証ビュー

### T2FontText

名前空間 `info.someiyoshino.T2Font.Viewer`・アセンブリ `Someiyoshino.T2Font.Viewer`・ソース [`Packages/info.someiyoshino.t2font/Viewer/T2FontText.cs`](../../../Packages/info.someiyoshino.t2font/Viewer/T2FontText.cs)

```csharp
public sealed class T2FontText : MonoBehaviour
```

検証ビュー（README §10）。文字列 1 本を、字ごとに 1 つの `CanvasRenderer` で描く。フォントとエッジスタイル（README §6.1）を Inspector で替えられる。

1 字 1 レンダラにしてあるのは、テクスチャとマテリアルの束縛・クリップ・α の扱いを、利用側の一般的なテキスト描画と同じ条件で試すため。グリフ表現の具体型を知らないホストの最小の実装でもある（README §4.1）: グリフインデックスを引き、グリフ表現に幾何を書かせ、テクスチャとマテリアルを貼るだけ。行送りは改行（U+000A）だけで、折り返し・カーニング・縦書きは持たない。

字の GameObject・Mesh・マテリアルは実行時に作る物で、シーンに保存しない（`HideFlags.DontSave`）。値が変わるか有効に戻ると次の `LateUpdate` で作り直し、無効になると描画を消してマテリアルを捨てる。Mesh とマテリアルはネイティブのオブジェクトで GC では回収されないので、明示的に破棄する。

#### `Font`

```csharp
public T2FontAsset? Font { get; set; }
```

描くフォント。

#### `Text`

```csharp
public string Text { get; set; }
```

描く文字列（改行で行を送る）。

#### `FontSize`

```csharp
public float FontSize { get; set; }
```

表示サイズ（1em の px）。

#### `FaceColor`

```csharp
public Color FaceColor { get; set; }
```

面の色（頂点色の rgb・README §5 S2）。

#### `Alpha`

```csharp
public float Alpha { get; set; }
```

フェード（頂点色の α・README §5 S2）。

#### `EdgeStyle`

```csharp
public GlyphEdgeStyle EdgeStyle { get; set; }
```

エッジスタイル（README §6.1）。

#### `ClipToRect`

```csharp
public bool ClipToRect { get; set; }
```

矩形（自分の RectTransform）で字をクリップするか。

#### `GlyphObjects`

```csharp
public IReadOnlyList<GameObject> GlyphObjects { get; }
```

描いている字の GameObject（インクのある字だけ・文字列の順）。検査用。

#### `Rebuild()`

```csharp
public void Rebuild()
```

今の設定で字を作り直す（通常は値を変えた後の `LateUpdate` が呼ぶ）。

## ベイク仕様を扱うツール（Editor と外部 NUnit）

### FontBakeSpec

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontBakeSpec.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontBakeSpec.cs)

```csharp
public sealed class FontBakeSpec
```

フォントのベイク仕様（`*.t2font`・JSON・README §7）。共通のキー（元フォント・網羅の種類・文字セット）と、グリフ表現の種類ごとの `representation` の節を持つ。

**未知のキー・重複したキー・型の違う値は失敗**にする——綴りの誤りを既定値で黙って読むと、作者の意図と違うフォントが黙ってできる（README §2.1）。グリフ表現の種類は必須で、既定のグリフ表現を黙って選ばない。

#### `Extension`

```csharp
public const string Extension = ".t2font"
```

ベイク仕様ファイルの拡張子（インポーターの主拡張子）。

#### `FontBakeSpec（構築子）`

```csharp
public FontBakeSpec(string source, FontCoverage coverage, FontCharsetKind charsets, string characters, GlyphRepresentationSettings representation)
```

#### `Source`

```csharp
public string Source { get; }
```

元フォントのプロジェクトパス（`Assets/` か `Packages/` で始まる・区切りは `/`）。

#### `Coverage`

```csharp
public FontCoverage Coverage { get; }
```

網羅の種類。

#### `Charsets`

```csharp
public FontCharsetKind Charsets { get; }
```

固定の文字セット（元フォントに無い字は上限集合の欠けとして件数だけを報告する）。

#### `Characters`

```csharp
public string Characters { get; }
```

指定の追加文字（元フォントに無ければ失敗する）。

#### `Representation`

```csharp
public GlyphRepresentationSettings Representation { get; }
```

グリフ表現の種類と、その種類の設定。

#### `Parse()`

```csharp
public static FontBakeSpec Parse(string text, string sourceName, GlyphRepresentationKinds kinds)
```

ベイク仕様を読む。`sourceName` は誤りの文面に出す名前（ファイルのパス）、`kinds` は使えるグリフ表現の種類の一覧（README §7.4）。

- 例外 `FontSpecException`: JSON の構文・未知のキー・型・値の範囲・未知のグリフ表現の種類の誤り。

#### `FixedCodepoints()`

```csharp
public SortedSet<int> FixedCodepoints()
```

固定の文字セットのコードポイント。

#### `NamedCodepoints()`

```csharp
public SortedSet<int> NamedCodepoints()
```

指定の追加文字のコードポイント。

#### `WithCharacters()`

```csharp
public FontBakeSpec WithCharacters(IEnumerable<int> codepoints)
```

文字セットだけを `codepoints` に替えたベイク仕様。ほかの値（元フォント・網羅の種類・表現）は写して自己完結にする。字は指定の追加文字（`characters`）として明示し、固定の文字セットは持たない——ベイクする字がベイク仕様の本文だけで決まり、指定文字は元フォントに無ければインポートが失敗するので、黙って欠けない。本文から字を決めて再ベイクする利用側のツールが使う。

- 例外 `ArgumentException`: 字が無い・描く字でない（制御文字・BOM・サロゲート）値がある。

#### `ToJson()`

```csharp
public string ToJson(IEnumerable<string> headerLines)
```

ベイク仕様の本文（JSON）。`Parse` で同じ値に読み戻せる。`headerLines` は先頭に `//` のコメントとして書く（無ければ空の列）。キーは省略できる値も含めて全部書く（既定値が変わっても意味が変わらない）。

### FontCoverage

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontBakeSpec.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontBakeSpec.cs)

```csharp
public enum FontCoverage
```

フォントが本文をどこまで覆うべきか（README §7.1）。検査の厳しさを決めるのは利用側で、ジェネレーターはこの値で振る舞いを変えない。

- `Corpus` — 本文フォント。利用側は本文の全字を覆うことを検査する。
- `Fixed` — 装飾書体。固定の文字セットだけをベイクし、欠けは実行時の代替グリフで見せる。

### FontCharsetKind

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontCharsets.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontCharsets.cs)

```csharp
public enum FontCharsetKind
```

ジェネレーターの固定の文字セット（README §7.2）。

- `None = 0`
- `Ascii = 1 << 0` — ASCII の印字可能文字（U+0020〜U+007E）95 字。
- `JisX0208 = 1 << 1` — JIS X 0208 の全 6,879 字（非漢字・第 1 水準・第 2 水準）。
- `WindowsVariants = 1 << 2` — Windows（CP932）が JIS 標準と別の Unicode へ写す 6 字（～ ∥ － ￠ ￡ ￢）。
- `NecRow13 = 1 << 3` — NEC 特殊文字（CP932 の 13 区）83 字。
- `IbmExtensions = 1 << 4` — IBM 拡張（CP932 の 0xFA40〜0xFC4B）388 字。人名に多い漢字（髙・﨑・德 など）を含む。
- `CommonSymbols = 1 << 5` — 記号のブロック（Latin-1 補助・一般句読点・文字様記号・数字の形・矢印・数学記号・その他の技術記号・囲み英数字・罫線・ブロック要素・幾何学模様・その他の記号・装飾記号・CJK の記号と句読点・囲み CJK・CJK 互換・半角／全角形）。IME で打てる記号（— © ® ™ é ♥ ♡ ‼ ▶ ◀ など）。書式文字（ゼロ幅・双方向の制御・行と段落の区切り）は除く。
- `DevelopmentDefault = Ascii | JisX0208 | WindowsVariants | NecRow13 | IbmExtensions | CommonSymbols` — 開発中の既定（JIS X 0208 全部＋周辺。開発中は字の欠けを意識しない＝IME で打てる字を広く収める）。元フォントに無い字は上限集合の欠けとして件数だけを報告する（README §7.1）。

### FontCharsets

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontCharsets.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontCharsets.cs)

```csharp
public static class FontCharsets
```

文字セットの組み立てと、アトラスのページへ並べる順（優先クラス）を決める。

#### `Build()`

```csharp
public static SortedSet<int> Build(FontCharsetKind kinds)
```

`kinds` が指す固定の文字セットのコードポイント（昇順・重複なし）。

#### `AddString()`

```csharp
public static void AddString(ISet<int> set, string text)
```

文字列の各字（サロゲートペアは 1 字）を `set` へ足す。規則（制御文字と BOM を足さない・片割れのサロゲートは失敗）の正典は `TextCodepoints.AddDrawable`。

#### `PriorityClass()`

```csharp
public static int PriorityClass(int codepoint)
```

アトラスのページへ並べる優先クラス（小さいほど先＝0 ページに入る）。本文の大半が 0 ページだけで描けるようにする（ページが変わると uGUI のバッチが割れる）。クラスの中はコードポイント順。0＝漢字以外（ASCII・記号・全角英数・ギリシャ・キリル・罫線ほか）／1＝かな／2＝JIS 第 1 水準漢字／3＝JIS 第 2 水準漢字／4＝それ以外の漢字。

### JisX0208Table

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/JisX0208Table.cs`](../../../Packages/info.someiyoshino.t2font/Bake/JisX0208Table.cs)

```csharp
public static class JisX0208Table
```

JIS X 0208 の全 6,879 字（Unicode・JIS の標準の対応表による）と、Windows（CP932）で入力されやすい周辺の字。生成器の固定の文字セット（README §7.2）の元データ。

表は区点の順（1 区 1 点から）。EUC-JP の 2 バイト（0xA1A1〜0xFEFE）を JIS 標準の対応で Unicode へ写して作った。第 1 水準＝16〜47 区（2,965 字）・第 2 水準＝48〜84 区（3,390 字）・非漢字＝1〜8 区（524 字）。

#### `NonKanji`

```csharp
public const string NonKanji = "\u3000、。，．・：；？！゛゜´｀¨＾￣＿ヽヾゝゞ〃仝々〆〇ー―‐／＼〜‖｜…‥‘’“" + "”（）〔〕［］｛｝〈〉《》「」『』【】＋−±×÷＝≠＜＞≦≧∞∴♂♀°′″℃￥＄" + "¢£％＃＆＊＠§☆★○●◎◇◆□■△▲▽▼※〒→←↑↓〓∈∋⊆⊇⊂⊃∪∩∧∨¬⇒" + "⇔∀∃∠⊥⌒∂∇≡≒≪≫√∽∝∵∫∬Å‰♯♭♪†‡¶◯０１２３４５６７８９ＡＢＣ" + "ＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑ" + "ｒｓｔｕｖｗｘｙｚぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞた" + "だちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょ" + "よらりるれろゎわゐゑをんァアィイゥウェエォオカガキギクグケゲコゴサザシジスズセゼ" + "ソゾタダチヂッツヅテデトドナニヌネノハバパヒビピフブプヘベペホボポマミムメモャヤ" + "ュユョヨラリルレロヮワヰヱヲンヴヵヶΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧ" + "ΨΩαβγδεζηθικλμνξοπρστυφχψωАБВГДЕЁЖЗИЙКЛМ" + "НОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрсту" + "фхцчшщъыьэюя─│┌┐┘└├┬┤┴┼━┃┏┓┛┗┣┳┫┻╋┠┯┨┷┿┝" + "┰┥┸╂"
```

1〜8 区の非漢字（記号・英数・かな・ギリシャ・キリル・罫線）524 字。

#### `Level1`

```csharp
public const string Level1 = "亜唖娃阿哀愛挨姶逢葵茜穐悪握渥旭葦芦鯵梓圧斡扱宛姐虻飴絢綾鮎或粟袷安庵按暗案闇鞍" + "杏以伊位依偉囲夷委威尉惟意慰易椅為畏異移維緯胃萎衣謂違遺医井亥域育郁磯一壱溢逸稲" + "茨芋鰯允印咽員因姻引飲淫胤蔭院陰隠韻吋右宇烏羽迂雨卯鵜窺丑碓臼渦嘘唄欝蔚鰻姥厩浦" + "瓜閏噂云運雲荏餌叡営嬰影映曳栄永泳洩瑛盈穎頴英衛詠鋭液疫益駅悦謁越閲榎厭円園堰奄" + "宴延怨掩援沿演炎焔煙燕猿縁艶苑薗遠鉛鴛塩於汚甥凹央奥往応押旺横欧殴王翁襖鴬鴎黄岡" + "沖荻億屋憶臆桶牡乙俺卸恩温穏音下化仮何伽価佳加可嘉夏嫁家寡科暇果架歌河火珂禍禾稼" + "箇花苛茄荷華菓蝦課嘩貨迦過霞蚊俄峨我牙画臥芽蛾賀雅餓駕介会解回塊壊廻快怪悔恢懐戒" + "拐改魁晦械海灰界皆絵芥蟹開階貝凱劾外咳害崖慨概涯碍蓋街該鎧骸浬馨蛙垣柿蛎鈎劃嚇各" + "廓拡撹格核殻獲確穫覚角赫較郭閣隔革学岳楽額顎掛笠樫橿梶鰍潟割喝恰括活渇滑葛褐轄且" + "鰹叶椛樺鞄株兜竃蒲釜鎌噛鴨栢茅萱粥刈苅瓦乾侃冠寒刊勘勧巻喚堪姦完官寛干幹患感慣憾" + "換敢柑桓棺款歓汗漢澗潅環甘監看竿管簡緩缶翰肝艦莞観諌貫還鑑間閑関陥韓館舘丸含岸巌" + "玩癌眼岩翫贋雁頑顔願企伎危喜器基奇嬉寄岐希幾忌揮机旗既期棋棄機帰毅気汽畿祈季稀紀" + "徽規記貴起軌輝飢騎鬼亀偽儀妓宜戯技擬欺犠疑祇義蟻誼議掬菊鞠吉吃喫桔橘詰砧杵黍却客" + "脚虐逆丘久仇休及吸宮弓急救朽求汲泣灸球究窮笈級糾給旧牛去居巨拒拠挙渠虚許距鋸漁禦" + "魚亨享京供侠僑兇競共凶協匡卿叫喬境峡強彊怯恐恭挟教橋況狂狭矯胸脅興蕎郷鏡響饗驚仰" + "凝尭暁業局曲極玉桐粁僅勤均巾錦斤欣欽琴禁禽筋緊芹菌衿襟謹近金吟銀九倶句区狗玖矩苦" + "躯駆駈駒具愚虞喰空偶寓遇隅串櫛釧屑屈掘窟沓靴轡窪熊隈粂栗繰桑鍬勲君薫訓群軍郡卦袈" + "祁係傾刑兄啓圭珪型契形径恵慶慧憩掲携敬景桂渓畦稽系経継繋罫茎荊蛍計詣警軽頚鶏芸迎" + "鯨劇戟撃激隙桁傑欠決潔穴結血訣月件倹倦健兼券剣喧圏堅嫌建憲懸拳捲検権牽犬献研硯絹" + "県肩見謙賢軒遣鍵険顕験鹸元原厳幻弦減源玄現絃舷言諺限乎個古呼固姑孤己庫弧戸故枯湖" + "狐糊袴股胡菰虎誇跨鈷雇顧鼓五互伍午呉吾娯後御悟梧檎瑚碁語誤護醐乞鯉交佼侯候倖光公" + "功効勾厚口向后喉坑垢好孔孝宏工巧巷幸広庚康弘恒慌抗拘控攻昂晃更杭校梗構江洪浩港溝" + "甲皇硬稿糠紅紘絞綱耕考肯肱腔膏航荒行衡講貢購郊酵鉱砿鋼閤降項香高鴻剛劫号合壕拷濠" + "豪轟麹克刻告国穀酷鵠黒獄漉腰甑忽惚骨狛込此頃今困坤墾婚恨懇昏昆根梱混痕紺艮魂些佐" + "叉唆嵯左差査沙瑳砂詐鎖裟坐座挫債催再最哉塞妻宰彩才採栽歳済災采犀砕砦祭斎細菜裁載" + "際剤在材罪財冴坂阪堺榊肴咲崎埼碕鷺作削咋搾昨朔柵窄策索錯桜鮭笹匙冊刷察拶撮擦札殺" + "薩雑皐鯖捌錆鮫皿晒三傘参山惨撒散桟燦珊産算纂蚕讃賛酸餐斬暫残仕仔伺使刺司史嗣四士" + "始姉姿子屍市師志思指支孜斯施旨枝止死氏獅祉私糸紙紫肢脂至視詞詩試誌諮資賜雌飼歯事" + "似侍児字寺慈持時次滋治爾璽痔磁示而耳自蒔辞汐鹿式識鴫竺軸宍雫七叱執失嫉室悉湿漆疾" + "質実蔀篠偲柴芝屡蕊縞舎写射捨赦斜煮社紗者謝車遮蛇邪借勺尺杓灼爵酌釈錫若寂弱惹主取" + "守手朱殊狩珠種腫趣酒首儒受呪寿授樹綬需囚収周宗就州修愁拾洲秀秋終繍習臭舟蒐衆襲讐" + "蹴輯週酋酬集醜什住充十従戎柔汁渋獣縦重銃叔夙宿淑祝縮粛塾熟出術述俊峻春瞬竣舜駿准" + "循旬楯殉淳準潤盾純巡遵醇順処初所暑曙渚庶緒署書薯藷諸助叙女序徐恕鋤除傷償勝匠升召" + "哨商唱嘗奨妾娼宵将小少尚庄床廠彰承抄招掌捷昇昌昭晶松梢樟樵沼消渉湘焼焦照症省硝礁" + "祥称章笑粧紹肖菖蒋蕉衝裳訟証詔詳象賞醤鉦鍾鐘障鞘上丈丞乗冗剰城場壌嬢常情擾条杖浄" + "状畳穣蒸譲醸錠嘱埴飾拭植殖燭織職色触食蝕辱尻伸信侵唇娠寝審心慎振新晋森榛浸深申疹" + "真神秦紳臣芯薪親診身辛進針震人仁刃塵壬尋甚尽腎訊迅陣靭笥諏須酢図厨逗吹垂帥推水炊" + "睡粋翠衰遂酔錐錘随瑞髄崇嵩数枢趨雛据杉椙菅頗雀裾澄摺寸世瀬畝是凄制勢姓征性成政整" + "星晴棲栖正清牲生盛精聖声製西誠誓請逝醒青静斉税脆隻席惜戚斥昔析石積籍績脊責赤跡蹟" + "碩切拙接摂折設窃節説雪絶舌蝉仙先千占宣専尖川戦扇撰栓栴泉浅洗染潜煎煽旋穿箭線繊羨" + "腺舛船薦詮賎践選遷銭銑閃鮮前善漸然全禅繕膳糎噌塑岨措曾曽楚狙疏疎礎祖租粗素組蘇訴" + "阻遡鼠僧創双叢倉喪壮奏爽宋層匝惣想捜掃挿掻操早曹巣槍槽漕燥争痩相窓糟総綜聡草荘葬" + "蒼藻装走送遭鎗霜騒像増憎臓蔵贈造促側則即息捉束測足速俗属賊族続卒袖其揃存孫尊損村" + "遜他多太汰詑唾堕妥惰打柁舵楕陀駄騨体堆対耐岱帯待怠態戴替泰滞胎腿苔袋貸退逮隊黛鯛" + "代台大第醍題鷹滝瀧卓啄宅托択拓沢濯琢託鐸濁諾茸凧蛸只叩但達辰奪脱巽竪辿棚谷狸鱈樽" + "誰丹単嘆坦担探旦歎淡湛炭短端箪綻耽胆蛋誕鍛団壇弾断暖檀段男談値知地弛恥智池痴稚置" + "致蜘遅馳築畜竹筑蓄逐秩窒茶嫡着中仲宙忠抽昼柱注虫衷註酎鋳駐樗瀦猪苧著貯丁兆凋喋寵" + "帖帳庁弔張彫徴懲挑暢朝潮牒町眺聴脹腸蝶調諜超跳銚長頂鳥勅捗直朕沈珍賃鎮陳津墜椎槌" + "追鎚痛通塚栂掴槻佃漬柘辻蔦綴鍔椿潰坪壷嬬紬爪吊釣鶴亭低停偵剃貞呈堤定帝底庭廷弟悌" + "抵挺提梯汀碇禎程締艇訂諦蹄逓邸鄭釘鼎泥摘擢敵滴的笛適鏑溺哲徹撤轍迭鉄典填天展店添" + "纏甜貼転顛点伝殿澱田電兎吐堵塗妬屠徒斗杜渡登菟賭途都鍍砥砺努度土奴怒倒党冬凍刀唐" + "塔塘套宕島嶋悼投搭東桃梼棟盗淘湯涛灯燈当痘祷等答筒糖統到董蕩藤討謄豆踏逃透鐙陶頭" + "騰闘働動同堂導憧撞洞瞳童胴萄道銅峠鴇匿得徳涜特督禿篤毒独読栃橡凸突椴届鳶苫寅酉瀞" + "噸屯惇敦沌豚遁頓呑曇鈍奈那内乍凪薙謎灘捺鍋楢馴縄畷南楠軟難汝二尼弐迩匂賑肉虹廿日" + "乳入如尿韮任妊忍認濡禰祢寧葱猫熱年念捻撚燃粘乃廼之埜嚢悩濃納能脳膿農覗蚤巴把播覇" + "杷波派琶破婆罵芭馬俳廃拝排敗杯盃牌背肺輩配倍培媒梅楳煤狽買売賠陪這蝿秤矧萩伯剥博" + "拍柏泊白箔粕舶薄迫曝漠爆縛莫駁麦函箱硲箸肇筈櫨幡肌畑畠八鉢溌発醗髪伐罰抜筏閥鳩噺" + "塙蛤隼伴判半反叛帆搬斑板氾汎版犯班畔繁般藩販範釆煩頒飯挽晩番盤磐蕃蛮匪卑否妃庇彼" + "悲扉批披斐比泌疲皮碑秘緋罷肥被誹費避非飛樋簸備尾微枇毘琵眉美鼻柊稗匹疋髭彦膝菱肘" + "弼必畢筆逼桧姫媛紐百謬俵彪標氷漂瓢票表評豹廟描病秒苗錨鋲蒜蛭鰭品彬斌浜瀕貧賓頻敏" + "瓶不付埠夫婦富冨布府怖扶敷斧普浮父符腐膚芙譜負賦赴阜附侮撫武舞葡蕪部封楓風葺蕗伏" + "副復幅服福腹複覆淵弗払沸仏物鮒分吻噴墳憤扮焚奮粉糞紛雰文聞丙併兵塀幣平弊柄並蔽閉" + "陛米頁僻壁癖碧別瞥蔑箆偏変片篇編辺返遍便勉娩弁鞭保舗鋪圃捕歩甫補輔穂募墓慕戊暮母" + "簿菩倣俸包呆報奉宝峰峯崩庖抱捧放方朋法泡烹砲縫胞芳萌蓬蜂褒訪豊邦鋒飽鳳鵬乏亡傍剖" + "坊妨帽忘忙房暴望某棒冒紡肪膨謀貌貿鉾防吠頬北僕卜墨撲朴牧睦穆釦勃没殆堀幌奔本翻凡" + "盆摩磨魔麻埋妹昧枚毎哩槙幕膜枕鮪柾鱒桝亦俣又抹末沫迄侭繭麿万慢満漫蔓味未魅巳箕岬" + "密蜜湊蓑稔脈妙粍民眠務夢無牟矛霧鵡椋婿娘冥名命明盟迷銘鳴姪牝滅免棉綿緬面麺摸模茂" + "妄孟毛猛盲網耗蒙儲木黙目杢勿餅尤戻籾貰問悶紋門匁也冶夜爺耶野弥矢厄役約薬訳躍靖柳" + "薮鑓愉愈油癒諭輸唯佑優勇友宥幽悠憂揖有柚湧涌猶猷由祐裕誘遊邑郵雄融夕予余与誉輿預" + "傭幼妖容庸揚揺擁曜楊様洋溶熔用窯羊耀葉蓉要謡踊遥陽養慾抑欲沃浴翌翼淀羅螺裸来莱頼" + "雷洛絡落酪乱卵嵐欄濫藍蘭覧利吏履李梨理璃痢裏裡里離陸律率立葎掠略劉流溜琉留硫粒隆" + "竜龍侶慮旅虜了亮僚両凌寮料梁涼猟療瞭稜糧良諒遼量陵領力緑倫厘林淋燐琳臨輪隣鱗麟瑠" + "塁涙累類令伶例冷励嶺怜玲礼苓鈴隷零霊麗齢暦歴列劣烈裂廉恋憐漣煉簾練聯蓮連錬呂魯櫓" + "炉賂路露労婁廊弄朗楼榔浪漏牢狼篭老聾蝋郎六麓禄肋録論倭和話歪賄脇惑枠鷲亙亘鰐詫藁" + "蕨椀湾碗腕"
```

第 1 水準漢字（16〜47 区）2,965 字。

#### `Level2`

```csharp
public const string Level2 = "弌丐丕个丱丶丼丿乂乖乘亂亅豫亊舒弍于亞亟亠亢亰亳亶从仍仄仆仂仗仞仭仟价伉佚估佛佝" + "佗佇佶侈侏侘佻佩佰侑佯來侖儘俔俟俎俘俛俑俚俐俤俥倚倨倔倪倥倅伜俶倡倩倬俾俯們倆偃" + "假會偕偐偈做偖偬偸傀傚傅傴傲僉僊傳僂僖僞僥僭僣僮價僵儉儁儂儖儕儔儚儡儺儷儼儻儿兀" + "兒兌兔兢竸兩兪兮冀冂囘册冉冏冑冓冕冖冤冦冢冩冪冫决冱冲冰况冽凅凉凛几處凩凭凰凵凾" + "刄刋刔刎刧刪刮刳刹剏剄剋剌剞剔剪剴剩剳剿剽劍劔劒剱劈劑辨辧劬劭劼劵勁勍勗勞勣勦飭" + "勠勳勵勸勹匆匈甸匍匐匏匕匚匣匯匱匳匸區卆卅丗卉卍凖卞卩卮夘卻卷厂厖厠厦厥厮厰厶參" + "簒雙叟曼燮叮叨叭叺吁吽呀听吭吼吮吶吩吝呎咏呵咎呟呱呷呰咒呻咀呶咄咐咆哇咢咸咥咬哄" + "哈咨咫哂咤咾咼哘哥哦唏唔哽哮哭哺哢唹啀啣啌售啜啅啖啗唸唳啝喙喀咯喊喟啻啾喘喞單啼" + "喃喩喇喨嗚嗅嗟嗄嗜嗤嗔嘔嗷嘖嗾嗽嘛嗹噎噐營嘴嘶嘲嘸噫噤嘯噬噪嚆嚀嚊嚠嚔嚏嚥嚮嚶嚴" + "囂嚼囁囃囀囈囎囑囓囗囮囹圀囿圄圉圈國圍圓團圖嗇圜圦圷圸坎圻址坏坩埀垈坡坿垉垓垠垳" + "垤垪垰埃埆埔埒埓堊埖埣堋堙堝塲堡塢塋塰毀塒堽塹墅墹墟墫墺壞墻墸墮壅壓壑壗壙壘壥壜" + "壤壟壯壺壹壻壼壽夂夊夐夛梦夥夬夭夲夸夾竒奕奐奎奚奘奢奠奧奬奩奸妁妝佞侫妣妲姆姨姜" + "妍姙姚娥娟娑娜娉娚婀婬婉娵娶婢婪媚媼媾嫋嫂媽嫣嫗嫦嫩嫖嫺嫻嬌嬋嬖嬲嫐嬪嬶嬾孃孅孀" + "孑孕孚孛孥孩孰孳孵學斈孺宀它宦宸寃寇寉寔寐寤實寢寞寥寫寰寶寳尅將專對尓尠尢尨尸尹" + "屁屆屎屓屐屏孱屬屮乢屶屹岌岑岔妛岫岻岶岼岷峅岾峇峙峩峽峺峭嶌峪崋崕崗嵜崟崛崑崔崢" + "崚崙崘嵌嵒嵎嵋嵬嵳嵶嶇嶄嶂嶢嶝嶬嶮嶽嶐嶷嶼巉巍巓巒巖巛巫已巵帋帚帙帑帛帶帷幄幃幀" + "幎幗幔幟幢幤幇幵并幺麼广庠廁廂廈廐廏廖廣廝廚廛廢廡廨廩廬廱廳廰廴廸廾弃弉彝彜弋弑" + "弖弩弭弸彁彈彌彎弯彑彖彗彙彡彭彳彷徃徂彿徊很徑徇從徙徘徠徨徭徼忖忻忤忸忱忝悳忿怡" + "恠怙怐怩怎怱怛怕怫怦怏怺恚恁恪恷恟恊恆恍恣恃恤恂恬恫恙悁悍惧悃悚悄悛悖悗悒悧悋惡" + "悸惠惓悴忰悽惆悵惘慍愕愆惶惷愀惴惺愃愡惻惱愍愎慇愾愨愧慊愿愼愬愴愽慂慄慳慷慘慙慚" + "慫慴慯慥慱慟慝慓慵憙憖憇憬憔憚憊憑憫憮懌懊應懷懈懃懆憺懋罹懍懦懣懶懺懴懿懽懼懾戀" + "戈戉戍戌戔戛戞戡截戮戰戲戳扁扎扞扣扛扠扨扼抂抉找抒抓抖拔抃抔拗拑抻拏拿拆擔拈拜拌" + "拊拂拇抛拉挌拮拱挧挂挈拯拵捐挾捍搜捏掖掎掀掫捶掣掏掉掟掵捫捩掾揩揀揆揣揉插揶揄搖" + "搴搆搓搦搶攝搗搨搏摧摯摶摎攪撕撓撥撩撈撼據擒擅擇撻擘擂擱擧舉擠擡抬擣擯攬擶擴擲擺" + "攀擽攘攜攅攤攣攫攴攵攷收攸畋效敖敕敍敘敞敝敲數斂斃變斛斟斫斷旃旆旁旄旌旒旛旙无旡" + "旱杲昊昃旻杳昵昶昴昜晏晄晉晁晞晝晤晧晨晟晢晰暃暈暎暉暄暘暝曁暹曉暾暼曄暸曖曚曠昿" + "曦曩曰曵曷朏朖朞朦朧霸朮朿朶杁朸朷杆杞杠杙杣杤枉杰枩杼杪枌枋枦枡枅枷柯枴柬枳柩枸" + "柤柞柝柢柮枹柎柆柧檜栞框栩桀桍栲桎梳栫桙档桷桿梟梏梭梔條梛梃檮梹桴梵梠梺椏梍桾椁" + "棊椈棘椢椦棡椌棍棔棧棕椶椒椄棗棣椥棹棠棯椨椪椚椣椡棆楹楷楜楸楫楔楾楮椹楴椽楙椰楡" + "楞楝榁楪榲榮槐榿槁槓榾槎寨槊槝榻槃榧樮榑榠榜榕榴槞槨樂樛槿權槹槲槧樅榱樞槭樔槫樊" + "樒櫁樣樓橄樌橲樶橸橇橢橙橦橈樸樢檐檍檠檄檢檣檗蘗檻櫃櫂檸檳檬櫞櫑櫟檪櫚櫪櫻欅蘖櫺" + "欒欖鬱欟欸欷盜欹飮歇歃歉歐歙歔歛歟歡歸歹歿殀殄殃殍殘殕殞殤殪殫殯殲殱殳殷殼毆毋毓" + "毟毬毫毳毯麾氈氓气氛氤氣汞汕汢汪沂沍沚沁沛汾汨汳沒沐泄泱泓沽泗泅泝沮沱沾沺泛泯泙" + "泪洟衍洶洫洽洸洙洵洳洒洌浣涓浤浚浹浙涎涕濤涅淹渕渊涵淇淦涸淆淬淞淌淨淒淅淺淙淤淕" + "淪淮渭湮渮渙湲湟渾渣湫渫湶湍渟湃渺湎渤滿渝游溂溪溘滉溷滓溽溯滄溲滔滕溏溥滂溟潁漑" + "灌滬滸滾漿滲漱滯漲滌漾漓滷澆潺潸澁澀潯潛濳潭澂潼潘澎澑濂潦澳澣澡澤澹濆澪濟濕濬濔" + "濘濱濮濛瀉瀋濺瀑瀁瀏濾瀛瀚潴瀝瀘瀟瀰瀾瀲灑灣炙炒炯烱炬炸炳炮烟烋烝烙焉烽焜焙煥煕" + "熈煦煢煌煖煬熏燻熄熕熨熬燗熹熾燒燉燔燎燠燬燧燵燼燹燿爍爐爛爨爭爬爰爲爻爼爿牀牆牋" + "牘牴牾犂犁犇犒犖犢犧犹犲狃狆狄狎狒狢狠狡狹狷倏猗猊猜猖猝猴猯猩猥猾獎獏默獗獪獨獰" + "獸獵獻獺珈玳珎玻珀珥珮珞璢琅瑯琥珸琲琺瑕琿瑟瑙瑁瑜瑩瑰瑣瑪瑶瑾璋璞璧瓊瓏瓔珱瓠瓣" + "瓧瓩瓮瓲瓰瓱瓸瓷甄甃甅甌甎甍甕甓甞甦甬甼畄畍畊畉畛畆畚畩畤畧畫畭畸當疆疇畴疊疉疂" + "疔疚疝疥疣痂疳痃疵疽疸疼疱痍痊痒痙痣痞痾痿痼瘁痰痺痲痳瘋瘍瘉瘟瘧瘠瘡瘢瘤瘴瘰瘻癇" + "癈癆癜癘癡癢癨癩癪癧癬癰癲癶癸發皀皃皈皋皎皖皓皙皚皰皴皸皹皺盂盍盖盒盞盡盥盧盪蘯" + "盻眈眇眄眩眤眞眥眦眛眷眸睇睚睨睫睛睥睿睾睹瞎瞋瞑瞠瞞瞰瞶瞹瞿瞼瞽瞻矇矍矗矚矜矣矮" + "矼砌砒礦砠礪硅碎硴碆硼碚碌碣碵碪碯磑磆磋磔碾碼磅磊磬磧磚磽磴礇礒礑礙礬礫祀祠祗祟" + "祚祕祓祺祿禊禝禧齋禪禮禳禹禺秉秕秧秬秡秣稈稍稘稙稠稟禀稱稻稾稷穃穗穉穡穢穩龝穰穹" + "穽窈窗窕窘窖窩竈窰窶竅竄窿邃竇竊竍竏竕竓站竚竝竡竢竦竭竰笂笏笊笆笳笘笙笞笵笨笶筐" + "筺笄筍笋筌筅筵筥筴筧筰筱筬筮箝箘箟箍箜箚箋箒箏筝箙篋篁篌篏箴篆篝篩簑簔篦篥籠簀簇" + "簓篳篷簗簍篶簣簧簪簟簷簫簽籌籃籔籏籀籐籘籟籤籖籥籬籵粃粐粤粭粢粫粡粨粳粲粱粮粹粽" + "糀糅糂糘糒糜糢鬻糯糲糴糶糺紆紂紜紕紊絅絋紮紲紿紵絆絳絖絎絲絨絮絏絣經綉絛綏絽綛綺" + "綮綣綵緇綽綫總綢綯緜綸綟綰緘緝緤緞緻緲緡縅縊縣縡縒縱縟縉縋縢繆繦縻縵縹繃縷縲縺繧" + "繝繖繞繙繚繹繪繩繼繻纃緕繽辮繿纈纉續纒纐纓纔纖纎纛纜缸缺罅罌罍罎罐网罕罔罘罟罠罨" + "罩罧罸羂羆羃羈羇羌羔羞羝羚羣羯羲羹羮羶羸譱翅翆翊翕翔翡翦翩翳翹飜耆耄耋耒耘耙耜耡" + "耨耿耻聊聆聒聘聚聟聢聨聳聲聰聶聹聽聿肄肆肅肛肓肚肭冐肬胛胥胙胝胄胚胖脉胯胱脛脩脣" + "脯腋隋腆脾腓腑胼腱腮腥腦腴膃膈膊膀膂膠膕膤膣腟膓膩膰膵膾膸膽臀臂膺臉臍臑臙臘臈臚" + "臟臠臧臺臻臾舁舂舅與舊舍舐舖舩舫舸舳艀艙艘艝艚艟艤艢艨艪艫舮艱艷艸艾芍芒芫芟芻芬" + "苡苣苟苒苴苳苺莓范苻苹苞茆苜茉苙茵茴茖茲茱荀茹荐荅茯茫茗茘莅莚莪莟莢莖茣莎莇莊荼" + "莵荳荵莠莉莨菴萓菫菎菽萃菘萋菁菷萇菠菲萍萢萠莽萸蔆菻葭萪萼蕚蒄葷葫蒭葮蒂葩葆萬葯" + "葹萵蓊葢蒹蒿蒟蓙蓍蒻蓚蓐蓁蓆蓖蒡蔡蓿蓴蔗蔘蔬蔟蔕蔔蓼蕀蕣蕘蕈蕁蘂蕋蕕薀薤薈薑薊薨" + "蕭薔薛藪薇薜蕷蕾薐藉薺藏薹藐藕藝藥藜藹蘊蘓蘋藾藺蘆蘢蘚蘰蘿虍乕虔號虧虱蚓蚣蚩蚪蚋" + "蚌蚶蚯蛄蛆蚰蛉蠣蚫蛔蛞蛩蛬蛟蛛蛯蜒蜆蜈蜀蜃蛻蜑蜉蜍蛹蜊蜴蜿蜷蜻蜥蜩蜚蝠蝟蝸蝌蝎蝴" + "蝗蝨蝮蝙蝓蝣蝪蠅螢螟螂螯蟋螽蟀蟐雖螫蟄螳蟇蟆螻蟯蟲蟠蠏蠍蟾蟶蟷蠎蟒蠑蠖蠕蠢蠡蠱蠶" + "蠹蠧蠻衄衂衒衙衞衢衫袁衾袞衵衽袵衲袂袗袒袮袙袢袍袤袰袿袱裃裄裔裘裙裝裹褂裼裴裨裲" + "褄褌褊褓襃褞褥褪褫襁襄褻褶褸襌褝襠襞襦襤襭襪襯襴襷襾覃覈覊覓覘覡覩覦覬覯覲覺覽覿" + "觀觚觜觝觧觴觸訃訖訐訌訛訝訥訶詁詛詒詆詈詼詭詬詢誅誂誄誨誡誑誥誦誚誣諄諍諂諚諫諳" + "諧諤諱謔諠諢諷諞諛謌謇謚諡謖謐謗謠謳鞫謦謫謾謨譁譌譏譎證譖譛譚譫譟譬譯譴譽讀讌讎" + "讒讓讖讙讚谺豁谿豈豌豎豐豕豢豬豸豺貂貉貅貊貍貎貔豼貘戝貭貪貽貲貳貮貶賈賁賤賣賚賽" + "賺賻贄贅贊贇贏贍贐齎贓賍贔贖赧赭赱赳趁趙跂趾趺跏跚跖跌跛跋跪跫跟跣跼踈踉跿踝踞踐" + "踟蹂踵踰踴蹊蹇蹉蹌蹐蹈蹙蹤蹠踪蹣蹕蹶蹲蹼躁躇躅躄躋躊躓躑躔躙躪躡躬躰軆躱躾軅軈軋" + "軛軣軼軻軫軾輊輅輕輒輙輓輜輟輛輌輦輳輻輹轅轂輾轌轉轆轎轗轜轢轣轤辜辟辣辭辯辷迚迥" + "迢迪迯邇迴逅迹迺逑逕逡逍逞逖逋逧逶逵逹迸遏遐遑遒逎遉逾遖遘遞遨遯遶隨遲邂遽邁邀邊" + "邉邏邨邯邱邵郢郤扈郛鄂鄒鄙鄲鄰酊酖酘酣酥酩酳酲醋醉醂醢醫醯醪醵醴醺釀釁釉釋釐釖釟" + "釡釛釼釵釶鈞釿鈔鈬鈕鈑鉞鉗鉅鉉鉤鉈銕鈿鉋鉐銜銖銓銛鉚鋏銹銷鋩錏鋺鍄錮錙錢錚錣錺錵" + "錻鍜鍠鍼鍮鍖鎰鎬鎭鎔鎹鏖鏗鏨鏥鏘鏃鏝鏐鏈鏤鐚鐔鐓鐃鐇鐐鐶鐫鐵鐡鐺鑁鑒鑄鑛鑠鑢鑞鑪" + "鈩鑰鑵鑷鑽鑚鑼鑾钁鑿閂閇閊閔閖閘閙閠閨閧閭閼閻閹閾闊濶闃闍闌闕闔闖關闡闥闢阡阨阮" + "阯陂陌陏陋陷陜陞陝陟陦陲陬隍隘隕隗險隧隱隲隰隴隶隸隹雎雋雉雍襍雜霍雕雹霄霆霈霓霎" + "霑霏霖霙霤霪霰霹霽霾靄靆靈靂靉靜靠靤靦靨勒靫靱靹鞅靼鞁靺鞆鞋鞏鞐鞜鞨鞦鞣鞳鞴韃韆" + "韈韋韜韭齏韲竟韶韵頏頌頸頤頡頷頽顆顏顋顫顯顰顱顴顳颪颯颱颶飄飃飆飩飫餃餉餒餔餘餡" + "餝餞餤餠餬餮餽餾饂饉饅饐饋饑饒饌饕馗馘馥馭馮馼駟駛駝駘駑駭駮駱駲駻駸騁騏騅駢騙騫" + "騷驅驂驀驃騾驕驍驛驗驟驢驥驤驩驫驪骭骰骼髀髏髑髓體髞髟髢髣髦髯髫髮髴髱髷髻鬆鬘鬚" + "鬟鬢鬣鬥鬧鬨鬩鬪鬮鬯鬲魄魃魏魍魎魑魘魴鮓鮃鮑鮖鮗鮟鮠鮨鮴鯀鯊鮹鯆鯏鯑鯒鯣鯢鯤鯔鯡" + "鰺鯲鯱鯰鰕鰔鰉鰓鰌鰆鰈鰒鰊鰄鰮鰛鰥鰤鰡鰰鱇鰲鱆鰾鱚鱠鱧鱶鱸鳧鳬鳰鴉鴈鳫鴃鴆鴪鴦鶯" + "鴣鴟鵄鴕鴒鵁鴿鴾鵆鵈鵝鵞鵤鵑鵐鵙鵲鶉鶇鶫鵯鵺鶚鶤鶩鶲鷄鷁鶻鶸鶺鷆鷏鷂鷙鷓鷸鷦鷭鷯" + "鷽鸚鸛鸞鹵鹹鹽麁麈麋麌麒麕麑麝麥麩麸麪麭靡黌黎黏黐黔黜點黝黠黥黨黯黴黶黷黹黻黼黽" + "鼇鼈皷鼕鼡鼬鼾齊齒齔齣齟齠齡齦齧齬齪齷齲齶龕龜龠堯槇遙瑤凜熙"
```

第 2 水準漢字（48〜84 区）3,390 字。

#### `WindowsVariants`

```csharp
public const string WindowsVariants = "\uFF5E\u2225\uFF0D\uFFE0\uFFE1\uFFE2"
```

Windows（CP932）が JIS 標準と別の Unicode へ写す 6 字（〜→～・‖→∥・−→－・¢→￠・£→￡・¬→￢）。Windows の IME で打つと右側が入るので、JIS 標準の側と両方を収める。

#### `NecRow13`

```csharp
public const string NecRow13 = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ㍉㌔㌢㍍㌘㌧㌃㌶㍑㍗" + "㌍㌦㌣㌫㍊㌻㎜㎝㎞㎎㎏㏄㎡㍻〝〟№㏍℡㊤㊥㊦㊧㊨㈱㈲㈹㍾㍽㍼≒≡∫∮∑√⊥∠∟⊿" + "∵∩∪"
```

NEC 特殊文字（CP932 の 13 区）83 字。丸数字・ローマ数字・単位記号など、文章に混じりやすいもの。

#### `IbmExtensions`

```csharp
public const string IbmExtensions = "ⅰⅱⅲⅳⅴⅵⅶⅷⅸⅹⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ￢￤＇＂㈱№℡∵纊褜鍈銈蓜俉炻昱棈鋹曻彅丨仡仼伀伃伹佖侒侊侚侔俍偀倢俿倞偆偰偂傔" + "僴僘兊兤冝冾凬刕劜劦勀勛匀匇匤卲厓厲叝﨎咜咊咩哿喆坙坥垬埈埇﨏塚增墲夋奓奛奝奣妤妺孖寀甯寘寬尞岦岺峵崧嵓﨑嵂嵭嶸嶹巐弡弴" + "彧德忞恝悅悊惞惕愠惲愑愷愰憘戓抦揵摠撝擎敎昀昕昻昉昮昞昤晥晗晙晴晳暙暠暲暿曺朎朗杦枻桒柀栁桄棏﨓楨﨔榘槢樰橫橆橳橾櫢櫤毖" + "氿汜沆汯泚洄涇浯涖涬淏淸淲淼渹湜渧渼溿澈澵濵瀅瀇瀨炅炫焏焄煜煆煇凞燁燾犱犾猤猪獷玽珉珖珣珒琇珵琦琪琩琮瑢璉璟甁畯皂皜皞皛" + "皦益睆劯砡硎硤硺礰礼神祥禔福禛竑竧靖竫箞精絈絜綷綠緖繒罇羡羽茁荢荿菇菶葈蒴蕓蕙蕫﨟薰蘒﨡蠇裵訒訷詹誧誾諟諸諶譓譿賰賴贒赶" + "﨣軏﨤逸遧郞都鄕鄧釚釗釞釭釮釤釥鈆鈐鈊鈺鉀鈼鉎鉙鉑鈹鉧銧鉷鉸鋧鋗鋙鋐﨧鋕鋠鋓錥錡鋻﨨錞鋿錝錂鍰鍗鎤鏆鏞鏸鐱鑅鑈閒隆﨩隝隯" + "霳霻靃靍靏靑靕顗顥飯飼餧館馞驎髙髜魵魲鮏鮱鮻鰀鵰鵫鶴鸙黑"
```

IBM 拡張（CP932 の 0xFA40〜0xFC4B）388 字。小文字のローマ数字・記号と、人名に多い漢字（髙・﨑・德 など）。Windows の IME で打てるので、開発中に字の欠けを意識しないために収める（CP932 の写しは Python の `cp932` と同じ）。

### GlyphRepresentationKinds

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs)

```csharp
public sealed class GlyphRepresentationKinds
```

使うグリフ表現の種類の一覧（README §7.4）。利用側のプロジェクトが 1 か所で明示して並べ、ベイク仕様の読み取りとエッジスタイルの照合に渡す。

#### `GlyphRepresentationKinds（構築子）`

```csharp
public GlyphRepresentationKinds(IEnumerable<GlyphRepresentationKind> kinds)
```

- 例外 `ArgumentException`: 同じ名前の種類が 2 つある。

#### `Names`

```csharp
public IEnumerable<string> Names { get; }
```

並べた種類の名前（昇順）。

#### `Find()`

```csharp
public GlyphRepresentationKind? Find(string name)
```

名前で引く。無ければ null。

### T2FontImporter

名前空間 `info.someiyoshino.T2Font.Editor`・アセンブリ `Someiyoshino.T2Font.Editor`・ソース [`Packages/info.someiyoshino.t2font/Editor/T2FontImporter.cs`](../../../Packages/info.someiyoshino.t2font/Editor/T2FontImporter.cs)

```csharp
public sealed class T2FontImporter : ScriptedImporter
```

ベイク仕様（`*.t2font`）のインポーター（README §7）。元フォントからタイポグラフィテーブルを作り、グリフ表現の種類のベイカーにグリフ表現を作らせて、`T2FontAsset`（主）とグリフ表現・テクスチャ（サブアセット）を作る。

生成物は Library に作られ、リポジトリには置かない（README §2.4）。ベイク仕様・元フォント（`DependsOnSourceAsset`）・表現のシェーダ（`DependsOnArtifact`）・表現の種類のバージョン（カスタム依存）のどれかが変われば Unity が再インポートする。**タイポグラフィテーブルの出力が変わったら `FontFaceBuilder.Version` を、グリフ表現の出力が変わったらその種類のバージョンを上げる**（README §7.5）。

誤り（ベイク仕様の誤り・元フォントが無い・指定文字が元フォントに無い・ベイカーの誤り・テクスチャが期待の形にならない）はインポートの失敗にしてアセットを作らない（README §2.1・§7.3）。実行時のロードは null になり、参照が明確に失敗する。

#### `ImporterVersion`

```csharp
public const int ImporterVersion = (FontFaceBuilder.Version * 100) + 1
```

インポーターのバージョン（タイポグラフィテーブルの生成のバージョン × 100 ＋ インポーター自身の改訂）。グリフ表現のバージョンは種類ごとのカスタム依存が持つ。

#### `RepresentationKinds()`

```csharp
public static GlyphRepresentationKinds RepresentationKinds()
```

インポーターが受け付けるグリフ表現の種類の一覧（README §7.4）。利用側のツールがベイク仕様を読むとき（`FontBakeSpec.Parse`）に渡す——インポーターと同じ一覧で読まないと、インポートできるベイク仕様をツールだけが拒む（あるいはその逆）。

- 例外 `InvalidOperationException`: ベイカーを集められない（引数の無い公開の構築子が無い・種類が重複する）。

#### `OnImportAsset()`

```csharp
public override void OnImportAsset(AssetImportContext ctx)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

### TrueTypeFont

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/TrueTypeFont.cs`](../../../Packages/info.someiyoshino.t2font/Bake/TrueTypeFont.cs)

```csharp
public sealed class TrueTypeFont
```

TrueType（`glyf` 輪郭）フォントのリーダー（README §7.3）。

読むのはタイポグラフィテーブルと輪郭に要る表だけ——`head`（em の単位・`loca` の形式）・`hhea`（縦メトリクス）・`maxp`（グリフ数）・`hmtx`（送り幅）・`cmap`（字 → グリフインデックス）・`loca`／`glyf`（輪郭）。グリフの置換（`GSUB`）・位置調整（`GPOS`）・ヒンティング命令は読まない（タイポグラフィテーブルは置換を持たず、グリフ表現はどれも輪郭そのものから作るのでヒンティングが要らない）。全グリフ表現が同じリーダーを通る（README §2.3）。

**CFF 輪郭（`OTTO`＝多くの OTF）は未対応**で、渡されたら明確失敗する（空の字を黙って作らない・README §2.1）。

構築後は状態を変えないので、ジェネレーターの並列処理から同時に引いてよい。

#### `TrueTypeFont（構築子）`

```csharp
public TrueTypeFont(byte[] data)
```

単体のフォントを読む。フォントコレクション（TTC）は読まない——ベイク仕様は書体の番号を持たないので、読めばどの書体をベイクするかを黙って決めることになる（README §2.1）。

- 例外 `NotSupportedException`: CFF 輪郭のフォント・フォントコレクション。
- 例外 `FormatException`: 必要な表が無い・表が範囲外を指すなど、壊れたフォント。

#### `UnitsPerEm`

```csharp
public int UnitsPerEm { get; }
```

em の単位数（`head.unitsPerEm`）。

#### `GlyphCount`

```csharp
public int GlyphCount { get; }
```

グリフの数（`maxp.numGlyphs`）。

#### `Ascender`

```csharp
public int Ascender { get; }
```

アセンダ（フォント単位・`hhea`）。

#### `Descender`

```csharp
public int Descender { get; }
```

ディセンダ（フォント単位・下が負・`hhea`）。

#### `LineGap`

```csharp
public int LineGap { get; }
```

行間の追加分（フォント単位・`hhea`）。

#### `Codepoints`

```csharp
public IReadOnlyList<int> Codepoints { get; }
```

`cmap` がグリフを割り当てている全コードポイント（昇順）。

#### `Contains()`

```csharp
public bool Contains(int codepoint)
```

`codepoint` にグリフがあるか（グリフインデックス 0＝.notdef は「無い」）。

#### `TryGetGlyphIndex()`

```csharp
public bool TryGetGlyphIndex(int codepoint, out int glyphIndex)
```

`codepoint` のグリフインデックス。無ければ false。

#### `GetAdvanceWidth()`

```csharp
public int GetAdvanceWidth(int glyphIndex)
```

グリフの送り幅（フォント単位）。

#### `GetOutline()`

```csharp
public GlyphOutline GetOutline(int glyphIndex)
```

グリフの輪郭（フォント単位・y 上向き）。複合グリフは部品を変換して 1 つの輪郭の集まりに展開する。インクの無いグリフ（空白）は輪郭 0 本を返す。

### FontSpecBaker

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontSpecBaker.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontSpecBaker.cs)

```csharp
public static class FontSpecBaker
```

ベイク仕様と元フォントからタイポグラフィテーブルを作る（README §7.3 の 2・3）。インポーターと外部 NUnit が同じ手順を通る。グリフ表現を作るのは各グリフ表現のベイカーの仕事で、ここは全グリフ表現で共通の部分だけを持つ。

#### `BuildFace()`

```csharp
public static FontSpecFace BuildFace(FontBakeSpec spec, TrueTypeFont font, string sourceName)
```

タイポグラフィテーブルを作る。指定の追加文字が元フォントに無ければ失敗する（作者が明示した要求を黙って捨てない・README §2.1）。固定の文字セットの欠けは `FontSpecFace.MissingFromCharsets` へ載せて続ける。

- 例外 `FontBakeException`: 追加字が元フォントに無い。

#### `SourceHash()`

```csharp
public static string SourceHash(string specText, byte[] fontBytes, GlyphRepresentationKind kind)
```

生成元（タイポグラフィテーブルの生成のバージョン・表現の種類とそのバージョン・ベイク仕様の本文・元フォントのバイト列）の SHA-256（小文字の 16 進）。アセットの `sourceHash` に入れ、「どの入力から作ったアセットか」を追えるようにする。

### FontSpecFace

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontSpecBaker.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontSpecBaker.cs)

```csharp
public sealed class FontSpecFace
```

ベイク仕様から作ったタイポグラフィテーブルと、インポートの報告に出すもの。

#### `Face`

```csharp
public FontFaceData Face { get; }
```

タイポグラフィテーブル。

#### `MissingFromCharsets`

```csharp
public IReadOnlyList<int> MissingFromCharsets { get; }
```

固定の文字セットのうち元フォントに無かった字（昇順）。固定の文字セットは上限集合なので欠けは失敗にせず、件数を報告する（README §7.1）。

### FontSpecException

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/BakeExceptions.cs`](../../../Packages/info.someiyoshino.t2font/Bake/BakeExceptions.cs)

```csharp
public sealed class FontSpecException : Exception
```

ベイク仕様の誤り（ファイル名と理由を文面が名指す）。インポートはこれで止まる（README §7.1）。

#### `FontSpecException（構築子）`

```csharp
public FontSpecException(string message)
```

## グリフ表現の作者

### GlyphRepresentationKind

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs)

```csharp
public abstract class GlyphRepresentationKind
```

グリフ表現の種類の記述子（README §7.4）。ベイク仕様の `representation.kind` の名前・描ける効果・設定の検査を持つ。UnityEngine を参照しないので、外部 NUnit でベイク仕様とエッジスタイルの照合ができる。グリフ表現を足すパッケージは派生を 1 つ置く（README §8）。

#### `Name`

```csharp
public abstract string Name { get; }
```

ベイク仕様に書く名前（小文字の英数字。例 `sdf`）。

#### `Effects`

```csharp
public abstract GlyphEffects Effects { get; }
```

この種類のグリフ表現が描ける効果（README §6.3）。宣言の正典で、ベイカーがグリフ表現のアセットへ写す。

#### `Version`

```csharp
public abstract int Version { get; }
```

この種類のベイカーの出力のバージョン。**同じ入力から出る出力が変わる変更をしたら上げる**（README §7.5）。インポーターはバージョンが変わった種類のベイク仕様を再インポートする。

#### `ParseSettings()`

```csharp
public abstract GlyphRepresentationSettings ParseSettings(SpecSection section)
```

`representation` の節（`kind` を除くキー）を読んで設定にする。未知のキー・型の違う値・範囲の外の値は `FontSpecException`（`SpecSection.Fail`）で止める（README §2.1）。

### GlyphRepresentationSettings

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs)

```csharp
public abstract class GlyphRepresentationSettings
```

グリフ表現の種類ごとの設定（`GlyphRepresentationKind.ParseSettings` の結果）。

#### `Kind`

```csharp
public abstract GlyphRepresentationKind Kind { get; }
```

この設定の種類。

#### `ToJsonMembers()`

```csharp
public abstract IEnumerable<KeyValuePair<string, string>> ToJsonMembers()
```

ベイク仕様へ書き戻すときの `representation` の節のキー（`kind` を除く）と、JSON の値の綴り。省略できる値も全部書く（既定値が変わっても意味が変わらない）。`GlyphRepresentationKind.ParseSettings` で同じ値に読み戻せること。

### SpecSection

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphRepresentationKind.cs)

```csharp
public sealed class SpecSection
```

ベイク仕様の JSON のオブジェクト 1 つを読む補助（誤りの文面にファイル名と行を付ける）。ベイク仕様の最上位と、グリフ表現の種類の `representation` の節の両方がこれで読む——どちらも「未知のキー・型の違う値は失敗」の規則に揃う（README §7.1）。

グリフ表現を足す側（README §8）が使うのは型付きの読み口（`RequireString`・`OptionalNumber`・`OptionalBox` ほか）と `Fail(string, string)` だけで、JSON の読み手の型は公開しない——読み手を差し替えてもグリフ表現の種類の実装は変わらない。

#### `SourceName`

```csharp
public string SourceName { get; }
```

誤りの文面に出すファイル名。

#### `Path`

```csharp
public string Path { get; }
```

誤りの文面に出す節の名前（最上位は空）。

#### `ReservedKeys`

```csharp
public IReadOnlyCollection<string> ReservedKeys { get; }
```

読み手の外で扱うキー。

#### `RequireKnownKeys()`

```csharp
public void RequireKnownKeys(params string[] known)
```

知らないキーがあれば失敗する。`known` 以外のキーを受けない。

#### `RequireString()`

```csharp
public string RequireString(string key)
```

文字列の必須のキー。

#### `OptionalString()`

```csharp
public string? OptionalString(string key)
```

文字列のキー。無ければ null。

#### `OptionalNumber()`

```csharp
public double? OptionalNumber(string key)
```

数のキー。無ければ null。

#### `OptionalBox()`

```csharp
public EmBox? OptionalBox(string key)
```

インク境界 `[左, 下, 右, 上]`（em）のキー。無ければ null。

#### `Fail()`

```csharp
public FontSpecException Fail(string key, string message)
```

キー `key` の値を名指した誤りを作る（キーがあれば行番号を、節の名前があればそれを文面に付ける）。値の範囲の外など、型付きの読み口が見ない誤りをグリフ表現の種類が止めるときに使う。

#### `JsonNumber()`

```csharp
public static string JsonNumber(double value)
```

JSON の数の綴り（往復で同じ値に戻る形）。

#### `JsonString()`

```csharp
public static string JsonString(string value)
```

JSON の文字列の綴り。

### EmBox

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/EmBox.cs`](../../../Packages/info.someiyoshino.t2font/Bake/EmBox.cs)

```csharp
public readonly struct EmBox
```

em の矩形（ペン原点とベースライン基準・y 上向き）。ジェネレーターがグリフのインク境界と、セルが収めるインク境界（cellBox）に使う（倍精度のまま持つ——タイポグラフィテーブルの `GlyphMetrics` は単精度に丸めた値で、ベイクはこちらの値で行う）。

#### `EmBox（構築子）`

```csharp
public EmBox(double left, double bottom, double right, double top)
```

#### `Left`

```csharp
public double Left { get; }
```

#### `Bottom`

```csharp
public double Bottom { get; }
```

#### `Right`

```csharp
public double Right { get; }
```

#### `Top`

```csharp
public double Top { get; }
```

#### `ToString()`

```csharp
public override string ToString()
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

### GlyphRepresentationBaker

名前空間 `info.someiyoshino.T2Font.Editor`・アセンブリ `Someiyoshino.T2Font.Editor`・ソース [`Packages/info.someiyoshino.t2font/Editor/GlyphRepresentationBaker.cs`](../../../Packages/info.someiyoshino.t2font/Editor/GlyphRepresentationBaker.cs)

```csharp
public abstract class GlyphRepresentationBaker
```

グリフ表現の種類のベイカー（README §7.4）。タイポグラフィテーブルと設定からグリフ表現のサブアセット（とテクスチャ）を作る。

インポーターが Editor の `TypeCache` で派生クラスを集め、引数の無い公開の構築子で作る。種類 1 つにベイカーは 1 つ。**同じ入力から出る出力が変わる変更をしたら、種類の記述子のバージョン（`GlyphRepresentationKind.Version`）を上げる**——その種類のフォントだけが再インポートされる（README §7.5）。

#### `Kind`

```csharp
public abstract GlyphRepresentationKind Kind { get; }
```

ベイクする種類の記述子。

#### `Bake()`

```csharp
public abstract GlyphRepresentation Bake(GlyphBakeContext context, FontFaceData face, GlyphRepresentationSettings settings)
```

グリフ表現を作る。`settings` は `Kind` が検査した設定。テクスチャなどのサブアセットは `GlyphBakeContext.AddObject` で渡す。誤りは `FontBakeException` で止める（アセットは作られない）。返すグリフ表現の `GlyphRepresentation.Effects` は `Kind` の宣言と同じにする（インポーターが検査する）。

### GlyphBakeContext

名前空間 `info.someiyoshino.T2Font.Editor`・アセンブリ `Someiyoshino.T2Font.Editor`・ソース [`Packages/info.someiyoshino.t2font/Editor/GlyphBakeContext.cs`](../../../Packages/info.someiyoshino.t2font/Editor/GlyphBakeContext.cs)

```csharp
public sealed class GlyphBakeContext
```

ベイカーがインポートの文脈に触れる口（README §7.3 の 4）。サブアセットの受け渡し・シェーダの読み込みと依存・インポートの報告を持つ。ベイカーは `AssetImportContext` に直接触れない——インポートが途中で失敗したとき、作りかけのサブアセットをインポーターがまとめて捨てるため。

#### `AssetPath`

```csharp
public string AssetPath { get; }
```

インポートしているベイク仕様のパス（誤りの文面の頭に付ける）。

#### `BaseName`

```csharp
public string BaseName { get; }
```

アセットの名前（ベイク仕様のファイル名から拡張子を除いたもの）。サブアセットの名前の頭に使う。

#### `SelectedBuildTarget`

```csharp
public BuildTarget SelectedBuildTarget { get; }
```

インポートの対象のビルドターゲット。**読むとターゲットを切り替えたときに再インポートする依存が張られる**ので、ターゲットで出力が変わるときだけ読む。

#### `PackageAssetPath()`

```csharp
public string PackageAssetPath(string packageName, string relativePath)
```

パッケージ `packageName` の中の `relativePath` のアセットパス（`Packages/…`）。グリフ表現のパッケージが自分のシェーダを指すのに使う——導入の方法（git URL か写しか・README §11）によらず同じパスになる。

- 例外 `FontBakeException`: そのパッケージが登録されていない（Packages/ の下に無い）。

#### `LoadShader()`

```csharp
public Shader LoadShader(string assetPath)
```

シェーダを読み、そのインポート結果への依存を張る（シェーダが変われば再インポートする）。

- 例外 `FontBakeException`: そのパスにシェーダが無い。

#### `AddObject()`

```csharp
public void AddObject(string identifier, Object obj)
```

サブアセット（テクスチャなど）を渡す。`identifier` はグリフ表現の中で一意な名前。インポートが成功したときだけアセットに加わり、失敗したときはインポーターが破棄する。

#### `AddDataTexture()`

```csharp
public Texture2D AddDataTexture(string identifier, GlyphDataPage page)
```

データテクスチャ（距離以外の値を持つテクスチャ・README §4.4）を作り、サブアセットとして渡す。非圧縮・point・linear・ミップ無し・clamp・非 Readable で作り、作った結果の形式と寸法を検査する。名前は `{BaseName} {identifier}`。

- 例外 `FontBakeException`: 期待の形式・寸法・ミップ・filter で作れなかった（インポートの失敗になる）。

#### `Report()`

```csharp
public void Report(string text)
```

インポートの報告（Console に 1 行で出す）に足す文。

### GlyphDataPage

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphDataPage.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphDataPage.cs)

```csharp
public sealed class GlyphDataPage
```

グリフ表現のベイカーが作るデータテクスチャの中身 1 枚（README §4.4）。距離以外の値（番地・索引・制御点など）をバイトで持ち、Editor の `GlyphBakeContext.AddDataTexture` が非圧縮・point・linear・ミップ無しのテクスチャにして形式と寸法を検査する。並びは行優先で、**行 0 が下端**（Unity のテクスチャと同じ並び・`FontAtlasPage` と同じ）。

#### `MaxSize`

```csharp
public const int MaxSize = 16384
```

データテクスチャの最大の幅と高さ（texel）。

#### `GlyphDataPage（構築子）`

```csharp
public GlyphDataPage(int width, int height, GlyphDataFormat format)
```

- 例外 `ArgumentOutOfRangeException`: 寸法が 1〜`MaxSize` の外。

#### `Width`

```csharp
public int Width { get; }
```

#### `Height`

```csharp
public int Height { get; }
```

#### `Format`

```csharp
public GlyphDataFormat Format { get; }
```

#### `Data`

```csharp
public byte[] Data { get; }
```

中身（`(y × Width + x) × texel のバイト数` から texel のバイト数だけ）。

#### `BytesPerTexel()`

```csharp
public static int BytesPerTexel(GlyphDataFormat format)
```

texel 1 つのバイト数。

### GlyphDataFormat

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphDataPage.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphDataPage.cs)

```csharp
public enum GlyphDataFormat
```

データテクスチャの texel の形式（README §4.4）。

- `R8` — 1 texel＝1 バイト（R）。
- `RGBA8` — 1 texel＝4 バイト（R・G・B・A の順）。

### FontFaceData

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontFaceBuilder.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontFaceBuilder.cs)

```csharp
public sealed class FontFaceData
```

タイポグラフィテーブルの生成結果（README §3・§7.3）。全グリフ表現が同じものを受け取り、グリフ表現はこれに描くためのデータを足す。

グリフインデックスの並びは `T2FontAsset` と同じ: `0 ≤ i < Codepoints.Length` は字 `Codepoints[i]` のグリフ（昇順）、その後ろに .notdef（`Notdef`）。ベイカーはグリフインデックスごとに `Outline` と `InkBox` を読む。

#### `Face`

```csharp
public FontFaceMetrics Face { get; }
```

フォント単位の値（em）。

#### `Codepoints`

```csharp
public int[] Codepoints { get; }
```

収めた字（昇順）。添字がグリフインデックス。

#### `Glyphs`

```csharp
public GlyphMetrics[] Glyphs { get; }
```

グリフインデックス順のタイポグラフィテーブルの記録。末尾が .notdef。

#### `Notdef`

```csharp
public int Notdef { get; }
```

代替グリフ（元フォントの glyph 0＝.notdef）のグリフインデックス（＝`Codepoints` の長さ）。

#### `GlyphCount`

```csharp
public int GlyphCount { get; }
```

グリフの数（.notdef を含む）。

#### `UnitsPerEm`

```csharp
public int UnitsPerEm { get; }
```

元フォントの em の単位数（`Outline` の座標の単位）。

#### `Missing`

```csharp
public IReadOnlyList<int> Missing { get; }
```

依頼されたが元フォントにグリフが無かった字（昇順）。扱い（警告か失敗か）は呼び出し側が決める。

#### `CodepointOf()`

```csharp
public int CodepointOf(int glyph)
```

グリフインデックス `glyph` の字（.notdef は `FontFaceBuilder.NotdefCodepoint`）。

#### `Outline()`

```csharp
public GlyphOutline? Outline(int glyph)
```

グリフの輪郭（フォント単位・y 上向き）。インクの無いグリフは null。

#### `InkBox()`

```csharp
public EmBox InkBox(int glyph)
```

グリフのインク境界（em・倍精度）。インクの無いグリフでは意味を持たない（`Outline` が null）。

#### `ComputeContentHash()`

```csharp
public string ComputeContentHash()
```

タイポグラフィテーブル（縦メトリクス・字・グリフごとの記録）の SHA-256（小文字の 16 進）。同じ入力から出るタイポグラフィテーブルが変わったことを検出し、バージョン（`FontFaceBuilder.Version`）の上げ忘れを止める検査が使う（README §7.5）。

### FontFaceBuilder

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontFaceBuilder.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontFaceBuilder.cs)

```csharp
public static class FontFaceBuilder
```

TrueType フォントからタイポグラフィテーブルを作る（README §7.3 の 3）。グリフ表現によらない手順で、送り幅とインク境界はグリフ表現を替えても同じになる（README §2.3）。UnityEngine を参照しない純 C# で、Unity のインポーターと外部 NUnit の両方から同じコードを呼ぶ。

#### `Version`

```csharp
public const int Version = 1
```

タイポグラフィテーブルの生成のバージョン。**同じ入力から出るタイポグラフィテーブルが変わる変更をしたら必ず上げる**——インポーターのバージョンがこれから決まり、上げないと古いインポートの結果が使われ続ける（README §7.5）。

#### `NotdefCodepoint`

```csharp
public const int NotdefCodepoint = -1
```

.notdef のグリフが名乗る字（グリフインデックスから字を引くときの印）。

#### `Build()`

```csharp
public static FontFaceData Build(TrueTypeFont font, IEnumerable<int> codepoints)
```

`font` から `codepoints` のタイポグラフィテーブルを作る。フォントに無い字は `FontFaceData.Missing` へ載せて飛ばす。代替グリフ（glyph 0）は必ず足す——元フォントの .notdef が空なら枠を描く（収録されていない字を、黙って消さずに目に見える形で描くためのもの・README §3.1）。

- 例外 `ArgumentException`: Unicode のスカラー値でない字がある。

### GlyphOutline

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs)

```csharp
public sealed class GlyphOutline
```

グリフの輪郭（フォント単位・y 上向き）。TrueType の輪郭は on-curve 点と、2 次ベジエの制御点（off-curve）の並びで、連続する 2 つの off-curve 点の間には中点の on-curve 点が暗黙にある（TrueType の仕様）。

#### `GlyphOutline（構築子）`

```csharp
public GlyphOutline(List<OutlinePoint[]> contours)
```

#### `ContourCount`

```csharp
public int ContourCount { get; }
```

輪郭の本数。

#### `Contours`

```csharp
public IReadOnlyList<OutlinePoint[]> Contours { get; }
```

輪郭（読み取り専用の扱い）。

#### `IsEmpty`

```csharp
public bool IsEmpty { get; }
```

インクが無い（輪郭 0 本か、点が 1 つも無い）か。

#### `TryGetBounds()`

```csharp
public bool TryGetBounds(out double xMin, out double yMin, out double xMax, out double yMax)
```

制御点を含む全点の境界ボックス（フォント単位）。2 次ベジエは制御点の凸包の中にあるので、曲線の境界ボックスを必ず含む（保守的）。インクが無ければ false。

#### `EnumerateSegments()`

```csharp
public void EnumerateSegments(List<OutlineSegment> segments)
```

輪郭を区間（直線と 2 次ベジエ）へ分けて `segments` に足す（README §8）。輪郭ごとに、最初の on-curve 点から点の並びの向きに回り、連続する 2 つの off-curve 点の間には暗黙の中点（on-curve）を置く。全点が off-curve の輪郭は最初の 2 点の中点から始める。点が 2 つ未満の輪郭と、長さ 0 の直線（同じ点の重複）は出さない。`Flatten` はこの区間をフラット化する。

#### `Flatten()`

```csharp
public void Flatten(double scale, double offsetX, double offsetY, double tolerance, List<OutlineEdge> edges)
```

輪郭を線分へフラット化する。点は `(x × scale + offsetX, y × scale + offsetY)` へ写す（ピクセル座標）。曲線は、弦と曲線の最大のずれが `tolerance`（ピクセル）以下になる本数の線分に等分する。2 次ベジエを n 等分したときのずれは `|p0 − 2·p1 + p2| / (4·n²)` 以下なので、本数はそこから決まる。区間は `EnumerateSegments` と同じ規則で分ける。

### OutlineSegment

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs)

```csharp
public readonly struct OutlineSegment
```

輪郭の区間 1 本（フォント単位・y 上向き・README §8）。直線か 2 次ベジエで、`IsLine` が false のとき `ControlX`・`ControlY` が制御点。区間は輪郭の向きに並び、終点は同じ輪郭の次の区間の始点に一致する（輪郭の最後の区間の終点は最初の区間の始点）。

#### `OutlineSegment（構築子）`

```csharp
public OutlineSegment(int contour, double x0, double y0, double x1, double y1)
```

直線の区間。

#### `OutlineSegment（構築子）`

```csharp
public OutlineSegment(int contour, double x0, double y0, double controlX, double controlY, double x1, double y1)
```

2 次ベジエの区間。

#### `Contour`

```csharp
public int Contour { get; }
```

区間が属する輪郭の番号（`GlyphOutline.Contours` の添字）。

#### `X0`

```csharp
public double X0 { get; }
```

#### `Y0`

```csharp
public double Y0 { get; }
```

#### `ControlX`

```csharp
public double ControlX { get; }
```

制御点（直線では始点と同じ値）。

#### `ControlY`

```csharp
public double ControlY { get; }
```

制御点（直線では始点と同じ値）。

#### `X1`

```csharp
public double X1 { get; }
```

#### `Y1`

```csharp
public double Y1 { get; }
```

#### `IsLine`

```csharp
public bool IsLine { get; }
```

直線か（制御点を持たない）。

### OutlinePoint

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs)

```csharp
public readonly struct OutlinePoint
```

輪郭の 1 点（フォント単位・y 上向き）。`OnCurve` が false の点は 2 次ベジエの制御点。

#### `OutlinePoint（構築子）`

```csharp
public OutlinePoint(double x, double y, bool onCurve)
```

#### `X`

```csharp
public double X { get; }
```

#### `Y`

```csharp
public double Y { get; }
```

#### `OnCurve`

```csharp
public bool OnCurve { get; }
```

### OutlineEdge

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs`](../../../Packages/info.someiyoshino.t2font/Bake/GlyphOutline.cs)

```csharp
public readonly struct OutlineEdge
```

線分 1 本（ラスタライザの入力・ピクセル座標・y 上向き）。

#### `OutlineEdge（構築子）`

```csharp
public OutlineEdge(double x0, double y0, double x1, double y1)
```

#### `X0`

```csharp
public double X0 { get; }
```

#### `Y0`

```csharp
public double Y0 { get; }
```

#### `X1`

```csharp
public double X1 { get; }
```

#### `Y1`

```csharp
public double Y1 { get; }
```

### FontBakeException

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/BakeExceptions.cs`](../../../Packages/info.someiyoshino.t2font/Bake/BakeExceptions.cs)

```csharp
public sealed class FontBakeException : Exception
```

ジェネレーターが続行できない誤り（はみ出す字・ページより大きいセルなど）。理由は文面が名指す（README §2.1）。

#### `FontBakeException（構築子）`

```csharp
public FontBakeException(string message)
```

### GlyphConformance

名前空間 `info.someiyoshino.T2Font.Conformance`・アセンブリ `Someiyoshino.T2Font.Conformance`・ソース [`Packages/info.someiyoshino.t2font/Conformance/GlyphConformance.cs`](../../../Packages/info.someiyoshino.t2font/Conformance/GlyphConformance.cs)

```csharp
public static class GlyphConformance
```

グリフ表現の適合検査（README §10）。グリフ表現を足したら、そのグリフ表現でベイクしたフォントを渡して通す。検査は誤りの文面の一覧を返し、空なら適合——テストの枠組みに依らないので、どのパッケージのテストからも呼べる。

- `CheckGeometry`: 全グリフの幾何（README §4.2・§4.3）。描画装置は要らない。
- `CheckRendering`: シェーダの契約（README §5 の S2・S3・S4・S6・S9 と矩形クリップ）を、実際に描いた画素で確かめる。描画装置が要る（`CanRender`）。

描画は `CommandBuffer` で透明にクリアした RenderTexture へ直接描く（Canvas もカメラも使わない）ので、描画パイプラインに依らない。矩形クリップは Canvas と同じくキーワード `UNITY_UI_CLIP_RECT` と `_ClipRect` を、頂点色の色空間は Canvas と同じく `_UIVertexColorAlwaysGammaSpace` を渡して試す。画素の判定は列の数え方だけで行い、上下の向き（プラットフォームによって RenderTexture の y が反転する）に依らない。

#### `CanRender`

```csharp
public static bool CanRender { get; }
```

描画の検査を回せるか（描画装置の無い batchmode では回せない）。

#### `OuterExtentEm()`

```csharp
public static float OuterExtentEm(GlyphEffects effects)
```

描けると宣言した効果（README §6.3）がインク境界から外へ描かれうる最大の距離（em・README §6.2）。全効果なら `GlyphEdgeStyle.MaxOuterExtentEm`。面の縁のぼかしは縁取りの帯の内側に収まるので、縁取りとの大きい方を取る。

#### `CheckGeometry()`

```csharp
public static IReadOnlyList<string> CheckGeometry(T2FontAsset font)
```

全グリフの幾何を検査する（README §4.2・§4.3）: インクの無いグリフは何も書かない・インクのあるグリフは三角形を書きテクスチャを持つ・UV0.zw が頂点の em 座標と一致する・頂点の境界ボックスがインク境界を効果の外延（`OuterExtentEm`）だけ広げた範囲を覆う。

覆いは頂点の境界ボックスで見る（quad なら十分。三角形の和が範囲を隙間なく覆うかまでは見ない）。

#### `CheckRendering()`

```csharp
public static IReadOnlyList<string> CheckRendering(T2FontAsset font, int codepoint)
```

シェーダの契約を描いた画素で検査する。`codepoint` は、インク部が字の中央の縦の列を上下の水平な縁で横切る、塗りつぶしの字（「■」U+25A0 など）にする——縁取りの幅と AA の幅を、その列の画素を数えて測る。

- S2・S6: 面の色は頂点色の rgb、フェードは頂点色の α で、透明にクリアした先へ描いても α は 1 回だけ掛かる。
- S4・S2: 縁取りが宣言どおりの幅で縁の色に塗られ、フェードが縁取りにも同じだけ掛かる（グリフ表現が縁取りを描けるときだけ）。
- S3: AA の幅が表示サイズによらず画面の約 1px。
- S9: `_UIVertexColorAlwaysGammaSpace` が立てば、Linear のプロジェクトでは頂点色の rgb を linear に直し、立たなければ直さない。
- 矩形クリップ: `UNITY_UI_CLIP_RECT` と `_ClipRect` で矩形の外が透明になる。

- 例外 `InvalidOperationException`: 描画装置が無い（`CanRender` が偽）。

## 同梱のグリフ表現: SDF

### SdfRepresentation

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font`・ソース [`Packages/info.someiyoshino.t2font/Runtime/SdfRepresentation.cs`](../../../Packages/info.someiyoshino.t2font/Runtime/SdfRepresentation.cs)

```csharp
public sealed class SdfRepresentation : GlyphRepresentation
```

同梱の SDF のグリフ表現（README §9）。グリフごとのセル（`SdfGlyphCell`）と、単チャネルの距離のページを持つ。

quad はセルの plane（インク境界＋距離レンジの余白・em）で、UV0.xy は同じ矩形のページ上の px ÷ ページの寸法。ページは左下原点で、Unity の UV と同じ向き。距離レンジは全フォント共通（`SdfEncoding.RangeEm`）なので、マテリアルはフォントに依らず、全 SDF フォントがエッジスタイルごとに 1 つのマテリアルを共有する（`GlyphRepresentation.MaterialScope` は既定のシェーダのまま・README §4.5）。

#### `Kind`

```csharp
public override string Kind { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `EmPx`

```csharp
public float EmPx { get; }
```

ページ上の 1em の px（ベイク仕様の `emPx`）。

#### `Cells`

```csharp
public IReadOnlyList<SdfGlyphCell> Cells { get; }
```

グリフインデックス順のセル。

#### `Pages`

```csharp
public IReadOnlyList<Texture2D> Pages { get; }
```

距離のページ（単チャネル・linear・ミップ無し）。

#### `Create()`

```csharp
public static SdfRepresentation Create(Shader shader, GlyphEffects effects, float emPx, SdfGlyphCell[] cells, Texture2D[] pages)
```

ベイカーがベイクした結果からグリフ表現を作る（ベイカー専用）。`effects` は種類の記述子の宣言を写す（README §6.3）。健全性は `T2FontAsset.Create` が `Validate` で検査する。

#### `ConfigureMaterial()`

```csharp
public override void ConfigureMaterial(Material material)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `GetTexture()`

```csharp
public override Texture? GetTexture(int glyph)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `WriteGeometry()`

```csharp
public override void WriteGeometry(int glyph, in GlyphMetrics metrics, float quadExtentEm, GlyphGeometry geometry)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `Validate()`

```csharp
public override string? Validate(int glyphCount)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

### SdfRepresentationKind

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/SdfRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/SdfRepresentationKind.cs)

```csharp
public sealed class SdfRepresentationKind : GlyphRepresentationKind
```

同梱の SDF のグリフ表現の種類（`"kind": "sdf"`・README §9）。全効果を描ける。

#### `Instance`

```csharp
public static readonly SdfRepresentationKind Instance
```

唯一の実体。

#### `Name`

```csharp
public override string Name { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `Effects`

```csharp
public override GlyphEffects Effects { get; }
```

距離場からエッジスタイルの 4 値を全部描ける（README §6.3）。外延は距離レンジ（`SdfEncoding.RangeEm`）に収まる。

#### `Version`

```csharp
public override int Version { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `ParseSettings()`

```csharp
public override GlyphRepresentationSettings ParseSettings(SpecSection section)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

### SdfSpecSettings

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/SdfRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/SdfRepresentationKind.cs)

```csharp
public sealed class SdfSpecSettings : GlyphRepresentationSettings
```

ベイク仕様の SDF の節（README §9 の表）。

#### `SdfSpecSettings（構築子）`

```csharp
public SdfSpecSettings(double emPx, SdfPageFormat format, int maxPageSize, EmBox? cellBox)
```

#### `Kind`

```csharp
public override GlyphRepresentationKind Kind { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `EmPx`

```csharp
public double EmPx { get; }
```

アトラス上の 1em の px。

#### `Format`

```csharp
public SdfPageFormat Format { get; }
```

ページのテクスチャ形式。

#### `MaxPageSize`

```csharp
public int MaxPageSize { get; }
```

1 ページの最大の幅と高さ（px）。

#### `CellBox`

```csharp
public EmBox? CellBox { get; }
```

セルのインク境界（null なら依頼した字のインク境界の和）。

#### `ToBakeSettings()`

```csharp
public SdfBakeSettings ToBakeSettings()
```

ジェネレーターの設定（距離レンジは全フォント共通の既定のまま）。

#### `ToJsonMembers()`

```csharp
public override IEnumerable<KeyValuePair<string, string>> ToJsonMembers()
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

### SdfPageFormat

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/SdfRepresentationKind.cs`](../../../Packages/info.someiyoshino.t2font/Bake/SdfRepresentationKind.cs)

```csharp
public enum SdfPageFormat
```

SDF のページのテクスチャ形式（README §9）。

- `R8` — 非圧縮の単チャネル 8bit（既定）。どのターゲットでも同じ。
- `BC4` — 単チャネルのブロック圧縮（R8 の半分）。デスクトップ向けのターゲットだけ。

### SdfEncoding

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/Sdf/SdfEncoding.cs`](../../../Packages/info.someiyoshino.t2font/Core/Sdf/SdfEncoding.cs)

```csharp
public static class SdfEncoding
```

同梱の SDF のグリフ表現の、全フォントに共通の定数（README §9）。ジェネレーターとランタイムとシェーダが同じ値を使う。

#### `KindName`

```csharp
public const string KindName = "sdf"
```

ベイク仕様の `representation.kind` の綴り（種類の記述子とグリフ表現のアセットが同じ値を名乗る）。

#### `RangeEm`

```csharp
public const float RangeEm = 0.1875f
```

SDF の片側距離レンジ（em）。符号化は `d = 0.5 + 距離 / (2 × RangeEm × emPx)`（インク領域の内側が正）で、距離レンジの外は 0 か 1 に飽和する。

**全フォントで 1 つの値**にし、ベイク仕様では選べない。エッジスタイルの値（縁取り・ずらし・ぼかし）は em で書き、それが使える外延の上限はこの値で決まる。フォントごとに距離レンジが違うと、同じエッジスタイルでもフォントによって縁が描けたり切れたりする。0.1875 はエッジスタイルの外延（`GlyphEdgeStyle.MaxOuterExtentEm`＝0.15em）に表示 14px の AA の半分を足しても収まる値（README §6.2）。変えると全フォントの作り直しが要る。

### SdfGlyphCell

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/Sdf/SdfGlyphCell.cs`](../../../Packages/info.someiyoshino.t2font/Core/Sdf/SdfGlyphCell.cs)

```csharp
public struct SdfGlyphCell
```

SDF のグリフ表現のグリフ 1 つのセル（README §9）。グリフインデックスで引く。

**単位**: plane\* は **em**（ペン原点・ベースライン基準・y 上向き）、atlas\* は**ページの px**（左下原点＝Unity のテクスチャの行の並びと同じ）。plane と atlas は同じ矩形を 2 つの座標系で書いたもので、`atlas = 原点 + plane × emPx` が厳密に成り立つ（ジェネレーターが保証する）。plane はインク境界に距離レンジを足した矩形で、描画はこれを quad にし、atlas から UV を作る——配置の方式（一様格子か詰めか）を知らない。

Unity が直列化する（SDF のグリフ表現が配列で持つ）ので、公開フィールドの構造体にしている。インクの無いグリフは全部 0。

#### `page`

```csharp
public int page
```

アトラスのページ番号（0 始まり）。

#### `atlasLeft`

```csharp
public int atlasLeft
```

ページ上の矩形の左端（px）。

#### `atlasBottom`

```csharp
public int atlasBottom
```

ページ上の矩形の下端（px・左下原点）。

#### `atlasRight`

```csharp
public int atlasRight
```

ページ上の矩形の右端（px・この列を含まない）。

#### `atlasTop`

```csharp
public int atlasTop
```

ページ上の矩形の上端（px・この行を含まない）。

#### `planeLeft`

```csharp
public float planeLeft
```

quad の左端（em・ペン原点から）。

#### `planeBottom`

```csharp
public float planeBottom
```

quad の下端（em・ベースラインから・上が正）。

#### `planeRight`

```csharp
public float planeRight
```

quad の右端（em）。

#### `planeTop`

```csharp
public float planeTop
```

quad の上端（em）。

#### `IsEmpty`

```csharp
public bool IsEmpty { get; }
```

インクが無い（quad を作らない）グリフか。

### SdfGlyphBaker

名前空間 `info.someiyoshino.T2Font.Editor`・アセンブリ `Someiyoshino.T2Font.Editor`・ソース [`Packages/info.someiyoshino.t2font/Editor/SdfGlyphBaker.cs`](../../../Packages/info.someiyoshino.t2font/Editor/SdfGlyphBaker.cs)

```csharp
public sealed class SdfGlyphBaker : GlyphRepresentationBaker
```

同梱の SDF のグリフ表現のベイカー（README §9）。`SdfAtlasBaker` がベイクした距離のページをテクスチャにし、`SdfRepresentation` を作る。

#### `ShaderPath`

```csharp
public const string ShaderPath = "Shaders/T2Font-SDF.shader"
```

シェーダのパッケージの中の置き場。

#### `Kind`

```csharp
public override GlyphRepresentationKind Kind { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `Bake()`

```csharp
public override GlyphRepresentation Bake(GlyphBakeContext context, FontFaceData face, GlyphRepresentationSettings settings)
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。
