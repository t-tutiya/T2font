<!-- 自動生成（Tests/ApiReferenceDocTests.cs）。手で編集しない。パッケージを写し直したら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# Rinkaku の API — 契約の内

利用者が使うことを README が想定している公開型（役割別）。索引と「契約の内と外」の意味は [`README.md`](README.md)。
文中の「README §n」は [Rinkaku の README](../../../Packages/info.someiyoshino.rinkaku/README.md) の節。

## グリフ表現 Rinkaku

### RinkakuRepresentation

名前空間 `info.someiyoshino.Rinkaku`・アセンブリ `Someiyoshino.Rinkaku`・ソース [`Packages/info.someiyoshino.rinkaku/Runtime/RinkakuRepresentation.cs`](../../../Packages/info.someiyoshino.rinkaku/Runtime/RinkakuRepresentation.cs)

```csharp
public sealed class RinkakuRepresentation : GlyphRepresentation
```

Rinkaku のグリフ表現（README §3.4・§3.5）。グリフごとのセル（`RinkakuGlyphCell`）と、データのページ（RGBA8・幅 4096）を持つ。

quad はセルの plane（インク境界＋外延・em）。**UV0.xy はインデックスとセグメントテーブルの番地**で、quad の 4 頂点で同じ値を持つ: x＝インデックスのずれ `K`（`RinkakuEncoding.PackedOffset`）、y＝セグメントテーブルの番地（`RinkakuEncoding.PackedDataBase`・ワイドインデックスは負）。どちらも 2^24 未満の整数なので float で正確に持て、シェーダは補間しない（`nointerpolation`）で受ける。

グリッドの細かさ（`IndexEm`）はフォントごとの値でマテリアルに載せるので、マテリアルを共有してよい範囲はグリフ表現自身（`MaterialScope`）。テクスチャはフォントの境で替わるので、バッチの割れ方は SDF と同じ。

#### `Kind`

```csharp
public override string Kind { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `IndexEm`

```csharp
public int IndexEm { get; }
```

グリッドの 1em のセルの数（ベイク仕様の `indexEm`）。

#### `Cells`

```csharp
public IReadOnlyList<RinkakuGlyphCell> Cells { get; }
```

グリフインデックス順のセル。

#### `Pages`

```csharp
public IReadOnlyList<Texture2D> Pages { get; }
```

データのページ（RGBA8・幅 4096・point・linear・ミップ無し）。

#### `MaterialScope`

```csharp
public override Object? MaterialScope { get; }
```

グリッドの細かさをマテリアルに載せるので、フォントごとにマテリアルを分ける。

#### `Create()`

```csharp
public static RinkakuRepresentation Create(Shader shader, GlyphEffects effects, int indexEm, RinkakuGlyphCell[] cells, Texture2D[] pages)
```

ベイカーがベイクした結果からグリフ表現を作る（ベイカー専用）。`effects` は種類の記述子の宣言を写す。健全性は `T2FontAsset.Create` が `Validate` で検査する。

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

### RinkakuRepresentationKind

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs)

```csharp
public sealed class RinkakuRepresentationKind : GlyphRepresentationKind
```

Rinkaku のグリフ表現の種類（`"kind": "rinkaku"`・README §5）。符号付き距離を SDF と同じ意味で持つので、エッジスタイルの効果を全部描ける（README §2）。

#### `Instance`

```csharp
public static readonly RinkakuRepresentationKind Instance
```

唯一の実体。

#### `MinIndexEm`

```csharp
public const int MinIndexEm = 8
```

`indexEm` の範囲。

#### `MaxIndexEm`

```csharp
public const int MaxIndexEm = 32
```

#### `MinPageSize`

```csharp
public const int MinPageSize = 64
```

`maxPageSize` の下限（上限は `RinkakuEncoding.MaxPageHeight`）。

#### `Name`

```csharp
public override string Name { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `Effects`

```csharp
public override GlyphEffects Effects { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

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

### RinkakuSpecSettings

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuRepresentationKind.cs)

```csharp
public sealed class RinkakuSpecSettings : GlyphRepresentationSettings
```

ベイク仕様の Rinkaku の節（README §5 の表）。

#### `RinkakuSpecSettings（構築子）`

```csharp
public RinkakuSpecSettings(int indexEm, int maxPageSize)
```

#### `Kind`

```csharp
public override GlyphRepresentationKind Kind { get; }
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

#### `IndexEm`

```csharp
public int IndexEm { get; }
```

グリッドの 1em のセルの数。

#### `MaxPageSize`

```csharp
public int MaxPageSize { get; }
```

1 ページの最大の高さ（texel）。

#### `ToBakeSettings()`

```csharp
public RinkakuBakeSettings ToBakeSettings()
```

ベイカーの設定。

#### `ToJsonMembers()`

```csharp
public override IEnumerable<KeyValuePair<string, string>> ToJsonMembers()
```

基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。

### RinkakuEncoding

名前空間 `info.someiyoshino.Rinkaku`・アセンブリ `Someiyoshino.Rinkaku.Core`・ソース [`Packages/info.someiyoshino.rinkaku/Core/RinkakuEncoding.cs`](../../../Packages/info.someiyoshino.rinkaku/Core/RinkakuEncoding.cs)

```csharp
public static class RinkakuEncoding
```

Rinkaku のデータの形式の定数（README §3）。ベイカー・表現・シェーダが同じ値を使う。値を変えたら README §3 とシェーダを同じ変更で直し、種類の記述子のバージョンを上げる（README §6）。

#### `KindName`

```csharp
public const string KindName = "rinkaku"
```

ベイク仕様の `representation.kind`。

#### `PageWidth`

```csharp
public const int PageWidth = 4096
```

ページの幅（texel）。線形の番地を 2 次元へ直すのに使う固定の値（README §3.1）。

#### `MaxPageHeight`

```csharp
public const int MaxPageHeight = 4096
```

ページの高さの上限（texel）。線形の番地が 2^24 未満に収まり、float で正確に持てる。

#### `CoordinateScale`

```csharp
public const double CoordinateScale = 16384.0
```

始点の座標の量子（1em あたり）と、負の座標を正にする足し値（em）。16bit で −1〜3em を持つ（README §3.2）。

#### `CoordinateOffset`

```csharp
public const double CoordinateOffset = 1.0
```

#### `ControlScale`

```csharp
public const double ControlScale = 4096.0
```

制御点の始点からの差の量子（1em あたり）。符号付き 12bit で ±0.5em を持つ（README §3.2）。

#### `ControlMin`

```csharp
public const int ControlMin = -2048
```

制御点の差の量子の値の範囲（符号付き 12bit）。

#### `ControlMax`

```csharp
public const int ControlMax = 2047
```

#### `AngleSteps`

```csharp
public const int AngleSteps = 256
```

擬似法線の角の段数（8bit・1 段 1.4°）。

#### `NarrowMaxEntries`

```csharp
public const int NarrowMaxEntries = 252
```

ナローインデックス（8bit）で持てる項目の数。項目番号 1〜251 がセグメント、0 が空き、252〜255 がレコードの印（README §3.3）。

#### `NarrowTop`

```csharp
public const int NarrowTop = 255
```

ナローインデックスの番号の最大の値（レコードの印の先頭・`RecordMarker`）。

#### `WideTop`

```csharp
public const int WideTop = 0xFFFF
```

ワイドインデックス（16bit）の番号の最大の値。

#### `WideMaxEntries`

```csharp
public const int WideMaxEntries = 65532
```

ワイドインデックスで持てる項目の数（項目番号 1〜65531）。

#### `RecordMarkers`

```csharp
public const int RecordMarkers = 4
```

レコードの印の数（番号の最大の値から下へ 4 つ）。印は「短いレコードか長いレコードか」と、レコードの位置の上の桁（0 か 1）を持つ。

#### `NarrowMaxRecordTexels`

```csharp
public const int NarrowMaxRecordTexels = 511
```

ナローインデックスのレコードの並びの大きさの上限（texel）: 後ろ向きの差を 8bit＋印の 1bit で持つ。

#### `WideMaxRecordTexels`

```csharp
public const int WideMaxRecordTexels = 131071
```

ワイドインデックスのレコードの並びの大きさの上限（texel）: 後ろ向きの差を 16bit＋印の 1bit で持つ。

#### `RecordMarker()`

```csharp
public static int RecordMarker(bool wide, bool list, int high)
```

レコードを指すセルの 2 つ目の番号（印）。`list` は長いレコード（5 本以上）か、`high` はレコードの位置（セグメントテーブルの先頭から後ろ向きの texel の差）の上の桁（0 か 1。下の桁はセルの 1 つ目の番号）。

#### `IsRecordMarker()`

```csharp
public static bool IsRecordMarker(int second, bool wide, out bool list, out int high)
```

セルの 2 つ目の番号がレコードの印か。印なら、長いレコードかとレコードの位置の上の桁を返す。

#### `RecordLowBits()`

```csharp
public static int RecordLowBits(bool wide)
```

レコードの位置の下の桁の幅（bit）。

#### `ItemsInCell`

```csharp
public const int ItemsInCell = 2
```

セルにじかに持つセグメントの数（これを超えるセルはレコードを指す）。

#### `ItemsInRecord`

```csharp
public const int ItemsInRecord = 4
```

短いレコードが持つセグメントの数の上限（これを超えるセルは長いレコード）。

#### `OffsetBias`

```csharp
public const int OffsetBias = 64
```

グリッドの texel のずれ `K` を UV0.x に詰めるときの足し値と、x・y のビット数（x 13bit・y 11bit＝24bit・README §3.4）。

#### `OffsetXBits`

```csharp
public const int OffsetXBits = 13
```

#### `OffsetYBits`

```csharp
public const int OffsetYBits = 11
```

#### `MaxItemsPerCell`

```csharp
public const int MaxItemsPerCell = 32
```

セル 1 つのセグメントの数（1 画素の評価本数）の上限。シェーダのループの上限と同じ値で、超えるセルを作る字はベイクの失敗にする（黙ってセグメントを落とさない・README §3.3）。

#### `BlockOriginCell()`

```csharp
public static int BlockOriginCell(in RinkakuGlyphCell cell)
```

インデックスブロックの左端の texel が持つ最初のセル（グリッドの x）。ナローインデックスは texel 1 つにセル 2 つ（偶数のセルが前半）なので、グリッドの左端を偶数へ切り下げる。ワイドインデックスは texel 1 つにセル 1 つ。

#### `BlockWidth()`

```csharp
public static int BlockWidth(in RinkakuGlyphCell cell)
```

インデックスブロックの幅（texel）。

#### `TexelColumn()`

```csharp
public static int TexelColumn(int cellX, bool wide)
```

グリッドセル x（`floor(em × indexEm)`）を持つ texel の、`BlockOriginCell` の texel からの列。

#### `PackedOffset()`

```csharp
public static int PackedOffset(in RinkakuGlyphCell cell)
```

セルのインデックスのずれ `K`（ブロックの原点 − グリッドの原点の texel の列）を UV0.x に詰めた値（README §3.4）。範囲の外なら −1。

#### `PackedDataBase()`

```csharp
public static int PackedDataBase(in RinkakuGlyphCell cell)
```

セグメントテーブルの番地を UV0.y に詰めた値（ナローインデックスは正・ワイドインデックスは負・README §3.4）。セグメントテーブルはインデックスブロックより後ろの行なので 0 にならない。

#### `MinDisplayPx`

```csharp
public const double MinDisplayPx = 14.0
```

サポートする最小の表示サイズ（px）。幾何の外延に足す AA の半分（0.5 表示 px）がこれで決まる（README §2）。

#### `ExtentEm`

```csharp
public static double ExtentEm { get; }
```

幾何の外延（em）: インク境界から、全効果の最大の外延（`GlyphEdgeStyle.MaxOuterExtentEm`）＋最小の表示サイズの AA の半分だけ広げる。quad とグリッドはこの範囲を覆う（README §3.3）。

### RinkakuGlyphCell

名前空間 `info.someiyoshino.Rinkaku`・アセンブリ `Someiyoshino.Rinkaku.Core`・ソース [`Packages/info.someiyoshino.rinkaku/Core/RinkakuGlyphCell.cs`](../../../Packages/info.someiyoshino.rinkaku/Core/RinkakuGlyphCell.cs)

```csharp
public struct RinkakuGlyphCell
```

Rinkaku のグリフ表現のグリフ 1 つのセル（README §3.4）。グリフインデックスで引く。

**グリッド**は em 座標を `1/indexEm` 角のグリッドに切ったもので、グリフのグリッドの左下のセルは `(gridX, gridY)`（`floor(em × indexEm)` の値）。ページの上のインデックスブロックの左下は `(blockX, blockY)`（texel・行 0 が下端）。ナローインデックスは texel 1 つにセル 2 つ（偶数のセルが前半）、ワイドインデックス（`wide`）は texel 1 つにセル 1 つを持つ（ブロックの幅は `RinkakuEncoding.BlockWidth`）。**セグメントテーブル**はページの線形の番地 `dataBase`（`y × 4096 + x`）から 1 項目 2 texel で並び、レコードの並びはその直前に置く。plane\* は quad（em・インク境界＋外延）。

Unity が直列化する（グリフ表現が配列で持つ）ので、公開フィールドの構造体にしている。インクの無いグリフは全部 0。

#### `page`

```csharp
public int page
```

ページ番号（0 始まり）。

#### `gridX`

```csharp
public int gridX
```

グリッドの左下のセル（`floor(em × indexEm)`）。

#### `gridY`

```csharp
public int gridY
```

#### `gridWidth`

```csharp
public int gridWidth
```

グリッドセルの数（横・縦）。0 ならインクが無い。

#### `gridHeight`

```csharp
public int gridHeight
```

#### `blockX`

```csharp
public int blockX
```

ページの上のインデックスブロックの左下（texel）。

#### `blockY`

```csharp
public int blockY
```

#### `dataBase`

```csharp
public int dataBase
```

セグメントテーブルの最初の項目の線形の番地（texel）。

#### `wide`

```csharp
public bool wide
```

ワイドインデックス（項目番号 16bit・texel 1 つにセル 1 つ）か。

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

### RinkakuGlyphBaker

名前空間 `info.someiyoshino.Rinkaku.Editor`・アセンブリ `Someiyoshino.Rinkaku.Editor`・ソース [`Packages/info.someiyoshino.rinkaku/Editor/RinkakuGlyphBaker.cs`](../../../Packages/info.someiyoshino.rinkaku/Editor/RinkakuGlyphBaker.cs)

```csharp
public sealed class RinkakuGlyphBaker : GlyphRepresentationBaker
```

Rinkaku のグリフ表現のベイカー（README §4）。`RinkakuAtlasBaker` がベイクしたページをデータテクスチャにし（T2Font の `GlyphBakeContext.AddDataTexture`）、`RinkakuRepresentation` を作る。T2Font のインポーターが Editor の型の一覧から見つける。

#### `ShaderPath`

```csharp
public const string ShaderPath = "Shaders/Rinkaku.shader"
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
