<!-- 自動生成（Tests/ApiReferenceDocTests.cs）。手で編集しない。パッケージを写し直したら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# Rinkaku の API — 契約の外

公開されているが、ベイカーの部品や、検査と計測のための型。**版の間で予告なく変わりうるので、利用側のコードから使わない**。
索引は [`README.md`](README.md)。文中の「README §n」は [Rinkaku の README](../../../Packages/info.someiyoshino.rinkaku/README.md) の節。

## 型

### RinkakuAtlasBaker

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs)

```csharp
public static class RinkakuAtlasBaker
```

Rinkaku のベイカー（純 C#・README §4）。字ごとに和集合輪郭 → 符号化 → グリッドを作り、インデックスブロックとセグメントテーブルをページへ置く。和集合輪郭を作れない字があればインポートの失敗にして字を名指す（黙って筋を残さない・README §4.1）。

#### `Version`

```csharp
public const int Version = 5
```

出力のバージョン（README §6）。**同じ入力から出る出力が変わる変更をしたら上げる**。

#### `Bake()`

```csharp
public static RinkakuAtlas Bake(FontFaceData face, RinkakuBakeSettings settings)
```

ベイクする。和集合輪郭を作れない字・座標が持てる範囲の外の字があれば、それらを名指して `FontBakeException` にする。

### RinkakuAtlas

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs)

```csharp
public sealed class RinkakuAtlas
```

ベイクした結果（セル・ページ・字ごとの統計）。

#### `IndexEm`

```csharp
public int IndexEm { get; }
```

#### `Cells`

```csharp
public RinkakuGlyphCell[] Cells { get; }
```

グリフインデックス順のセル。

#### `Pages`

```csharp
public IReadOnlyList<GlyphDataPage> Pages { get; }
```

データのページ（RGBA8・幅 4096）。

#### `Stats`

```csharp
public RinkakuGlyphStats[] Stats { get; }
```

グリフインデックス順の統計。

#### `ComputeContentHash()`

```csharp
public string ComputeContentHash()
```

出力の SHA-256（小文字の 16 進）。出力が変わったことを検出し、バージョンの上げ忘れを止める検査が使う（README §6）。

### RinkakuBakeSettings

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs)

```csharp
public sealed class RinkakuBakeSettings
```

ベイクの設定（README §5）。

#### `IndexEm`

```csharp
public int IndexEm { get; set; }
```

グリッドの 1em のセルの数（texel の 1 辺＝1/indexEm em）。

#### `MaxPageHeight`

```csharp
public int MaxPageHeight { get; set; }
```

1 ページの最大の高さ（texel）。幅は常に `RinkakuEncoding.PageWidth`。

#### `Parallel`

```csharp
public bool Parallel { get; set; }
```

字ごとの処理を並列に行うか（結果は並列でも直列でも同一）。

#### `CellOrder`

```csharp
public RinkakuCellOrder CellOrder { get; set; }
```

セルのセグメントの並び。既定（`RinkakuCellOrder.CurvesFirst`）以外は出力が変わる（README §5）。

### RinkakuCellOrder

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs)

```csharp
public enum RinkakuCellOrder
```

セルのセグメントの並び（README §4.3 の 4）。

- `NearFirst` — セルの中心に近い順。並びを比べる計測のためのもので、ベイク仕様からは選べない（README §5）。セルのセグメントの集合は変わらないので、描く絵は既定の並びと同じ。
- `CurvesFirst` — 曲線を先に、直線を後に（それぞれの中はセルの中心に近い順）。既定。

### RinkakuGlyphStats

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuAtlasBaker.cs)

```csharp
public readonly struct RinkakuGlyphStats
```

グリフ 1 つのデータの量とインデックスの形（README §7 の表と検査に使う）。インクの無いグリフは全部 0。

#### `RinkakuGlyphStats（構築子）`

```csharp
public RinkakuGlyphStats(int segments, int entries, int indexTexels, int dataTexels, int maxItems, int totalItems, int cells, bool wide)
```

#### `Segments`

```csharp
public int Segments { get; }
```

セグメントの数（和集合輪郭・符号化の後）。

#### `Entries`

```csharp
public int Entries { get; }
```

セグメントテーブルの項目の数（セグメント＋輪郭ごとの終端）。

#### `IndexTexels`

```csharp
public int IndexTexels { get; }
```

インデックスブロックの texel の数。

#### `DataTexels`

```csharp
public int DataTexels { get; }
```

セグメントテーブルとレコードの並びの texel の数。

#### `MaxItems`

```csharp
public int MaxItems { get; }
```

セル 1 つのセグメントの数の最大（1 画素の評価本数の最大）。

#### `TotalItems`

```csharp
public int TotalItems { get; }
```

全セルのセグメントの数の和（÷ `Cells` が 1 画素の評価本数の平均）。

#### `Cells`

```csharp
public int Cells { get; }
```

グリッドセルの数。

#### `Wide`

```csharp
public bool Wide { get; }
```

ワイドインデックスか。

#### `Bytes`

```csharp
public int Bytes { get; }
```

グリフ 1 つのバイト数（ページの棚の隙間を除く）。

### RinkakuDecoder

名前空間 `info.someiyoshino.Rinkaku.Bake`・アセンブリ `Someiyoshino.Rinkaku.Bake`・ソース [`Packages/info.someiyoshino.rinkaku/Bake/RinkakuDecoder.cs`](../../../Packages/info.someiyoshino.rinkaku/Bake/RinkakuDecoder.cs)

```csharp
public static class RinkakuDecoder
```

ページのバイトから、シェーダと同じ手順で画素の符号付き距離を求める参照の実装（倍精度・README §3）。形式（インデックス・レコード・セグメントテーブルの詰め方）の正しさを確かめる検査と、GPU の描画と画素ごとに比べる診断が使う。シェーダはこの手順を float で写す。

#### `SignedDistance()`

```csharp
public static double SignedDistance(RinkakuAtlas atlas, int glyph, double px, double py, out int evaluated)
```

グリフの点 (px, py)（em）の符号付き距離（インク領域の内側が正）。`evaluated` は評価したセグメントの数（1 画素の評価本数）。点は quad（セルの plane）の中にあること。

#### `Items()`

```csharp
public static List<int> Items(byte[] data, RinkakuGlyphCell cell, int indexEm, double px, double py)
```

点が属するグリッドセルの項目番号の列（レコードを含む）。

#### `ReadEntry()`

```csharp
public static void ReadEntry(byte[] data, int address, out double x, out double y, out double cx, out double cy, out double nx, out double ny)
```

セグメントテーブルの項目（線形の番地）を読む: 始点・制御点（em）と擬似法線（単位）。
