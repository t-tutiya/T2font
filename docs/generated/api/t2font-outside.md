<!-- 自動生成（Tests/ApiReferenceDocTests.cs）。手で編集しない。パッケージを写し直したら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# T2Font の API — 契約の外

公開されているが、ベイカーの部品や、検査と計測のための型。**版の間で予告なく変わりうるので、利用側のコードから使わない**。
索引は [`README.md`](README.md)。文中の「README §n」は [T2Font の README](../../../Packages/info.someiyoshino.t2font/README.md) の節。

## 型

### GlyphTable

名前空間 `info.someiyoshino.T2Font`・アセンブリ `Someiyoshino.T2Font.Core`・ソース [`Packages/info.someiyoshino.t2font/Core/GlyphTable.cs`](../../../Packages/info.someiyoshino.t2font/Core/GlyphTable.cs)

```csharp
public static class GlyphTable
```

字（コードポイント）の昇順の配列を引く（README §3.2）。添字がそのままグリフインデックスになる。辞書を組まず二分探索で引く——約 7,000 字でも比較は 13 回で、ロード時の構築も割り当ても要らない。

#### `IndexOf()`

```csharp
public static int IndexOf(IReadOnlyList<int> codepoints, int codepoint)
```

`codepoints`（狭義昇順）の中の `codepoint` の添字。無ければ -1。

#### `IsStrictlyAscending()`

```csharp
public static bool IsStrictlyAscending(IReadOnlyList<int> codepoints)
```

`codepoints` が狭義昇順か（重複が無いか）。ジェネレーターの出力と、読み込んだアセットの健全性の検査に使う。

### DistanceTransform

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/DistanceTransform.cs`](../../../Packages/info.someiyoshino.t2font/Bake/DistanceTransform.cs)

```csharp
public sealed class DistanceTransform
```

厳密ユークリッド距離変換（Felzenszwalb &amp; Huttenlocher の放物線の下包絡・O(画素数)）。2 値の格子の各画素について、「目標」の画素の中心までの最短距離（画素単位）を求める。

打ち切りの近似（近傍の範囲の中だけ探す）ではなく厳密に解くのは、距離レンジを広げても計算量が変わらず、距離レンジごとに正しさを検証し直す必要が無いため。1 インスタンスは 1 スレッドで使い回す（作業領域を持つ）。

#### `DistanceTransform（構築子）`

```csharp
public DistanceTransform(int maxWidth, int maxHeight)
```

- 引数 `maxWidth`: 扱う格子の最大幅。
- 引数 `maxHeight`: 扱う格子の最大高さ。

#### `Compute()`

```csharp
public void Compute(bool[] target, bool targetValue, int width, int height, double[] distance)
```

`target`[i] == `targetValue` の画素を目標として、各画素から最も近い目標の画素の中心までの距離を `distance` へ書く（目標そのものは 0）。目標が 1 つも無ければ全画素が非常に大きな値になる。

#### `ComputeAt()`

```csharp
public void ComputeAt(bool[] target, bool targetValue, int width, int height, int[] rows, int rowCount, int[] columns, int columnCount, double[] distance)
```

`Compute` と同じ距離を、`rows` と `columns` の交点でだけ求める（結果は行優先で `distance[行の添字 × columnCount + 列の添字]`）。行と列は昇順。

SDF の生成はスーパーサンプリングの格子のうち texel の中心を挟むサンプルしか使わないので、全画素を解くと大半が捨てられる。2 値の格子では縦方向の段は「列の中で最寄りの目標までの距離」になり、放物線の包絡が要らない——行の順に上向きと下向きの 2 回走査して、選んだ行でだけ値を残す（メモリを行の順に読む）。横方向の段は選んだ行だけを包絡で解き、選んだ列でだけ平方根を取る。値は `Compute` と同じ（どちらも厳密）。

### ScanlineRasterizer

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/ScanlineRasterizer.cs`](../../../Packages/info.someiyoshino.t2font/Bake/ScanlineRasterizer.cs)

```csharp
public sealed class ScanlineRasterizer
```

線分の集まりを、画素の中心で内外を判定して 2 値に塗る（非ゼロ巻き数規則）。

**非ゼロ巻き数で塗るのは輪郭の重なりのため**: 和文フォントには画の交差を別の輪郭で描き、重ねたまま出荷したものがある。偶奇規則だと重なりが抜け、輪郭から直接距離を測る方式では重なりの内側の辺が縁に化ける。塗ってから距離を取る（`DistanceTransform`）方式は、ここで重なりが解消されるので影響を受けない（README §5 S8）。

座標はピクセル単位・y 上向きで、行 r の中心は y = r + 0.5、列 c の中心は x = c + 0.5。出力の行 0 が下端（Unity のテクスチャと同じ並び）。1 インスタンスは 1 スレッドで使い回す（作業領域を持つ）。

#### `ScanlineRasterizer（構築子）`

```csharp
public ScanlineRasterizer(int maxHeight)
```

#### `Fill()`

```csharp
public void Fill(IReadOnlyList<OutlineEdge> edges, int width, int height, bool[] inside)
```

`edges` を幅 `width`・高さ `height` の格子へ塗り、内側の画素を true にして `inside`（長さ width × height・行優先・行 0 が下端）へ書く。

### FontAtlasPage

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/FontAtlasPage.cs`](../../../Packages/info.someiyoshino.t2font/Bake/FontAtlasPage.cs)

```csharp
public sealed class FontAtlasPage
```

ジェネレーターが作るテクスチャのページ 1 枚（単チャネル 8bit・行優先・行 0 が下端＝Unity のテクスチャと同じ並び）。何を符号化するかはグリフ表現が決める（SDF は距離・README §9）。

#### `FontAtlasPage（構築子）`

```csharp
public FontAtlasPage(int width, int height)
```

#### `Width`

```csharp
public int Width { get; }
```

#### `Height`

```csharp
public int Height { get; }
```

#### `Pixels`

```csharp
public byte[] Pixels { get; }
```

画素（`y × Width + x`）。

### SdfAtlasBaker

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/SdfAtlasBaker.cs`](../../../Packages/info.someiyoshino.t2font/Bake/SdfAtlasBaker.cs)

```csharp
public static class SdfAtlasBaker
```

タイポグラフィテーブルから SDF のアトラスを作る（README §9）。

方式: ①字を「アトラスの em × スーパーサンプリング倍率」の格子へ非ゼロ巻き数で塗る ②インク部と背景の両方向へ厳密距離変換③アトラスの texel の中心での距離をスーパーサンプリングの格子から取り、`0.5 + 距離 / (2 × 距離レンジ)` を 8bit に符号化④全字を共通の原点・同じ大きさのセルに入れ、優先クラスの順にページへ並べる（一様格子・複数ページ）。字ごとのセルはインク境界＋距離レンジの矩形だけを指す（quad を字の大きさに絞る）ので、描画側は配置の方式を知らない。

UnityEngine を参照しない純 C# で、Unity のインポーターと外部 NUnit の両方から同じコードを呼ぶ。

#### `Version`

```csharp
public const int Version = 1
```

SDF のアトラスの生成のバージョン。**同じ入力から出る出力が変わる変更をしたら必ず上げる**——上げないと古いインポートの結果が使われ続ける（README §7.5）。上げ忘れは外部 NUnit の出力のハッシュの検査が止める。

#### `Bake()`

```csharp
public static SdfAtlas Bake(FontFaceData face, SdfBakeSettings settings)
```

`face` の全グリフ（.notdef を含む）の SDF のアトラスを作る。

- 例外 `FontBakeException`: 明示したセルの箱からはみ出す字がある・ページに 1 セルも入らない。

### SdfAtlas

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/SdfAtlasBaker.cs`](../../../Packages/info.someiyoshino.t2font/Bake/SdfAtlasBaker.cs)

```csharp
public sealed class SdfAtlas
```

SDF のアトラスの生成の結果（README §9）。

#### `EmPx`

```csharp
public float EmPx { get; }
```

アトラス上の 1em の px。

#### `RangeEm`

```csharp
public float RangeEm { get; }
```

片側距離レンジ（em）。

#### `Cells`

```csharp
public SdfGlyphCell[] Cells { get; }
```

グリフインデックス順のセル（タイポグラフィテーブルの `FontFaceData.Glyphs` と同じ並び）。インクの無いグリフは空のセル。

#### `Pages`

```csharp
public IReadOnlyList<FontAtlasPage> Pages { get; }
```

アトラスのページ（符号化した距離・`d = 0.5 + 距離 / (2 × 距離レンジ)` を 0〜255 へ）。

#### `CellBox`

```csharp
public EmBox CellBox { get; }
```

セルのインク境界（em）。

#### `CellWidth`

```csharp
public int CellWidth { get; }
```

セルの幅（アトラス px）。

#### `CellHeight`

```csharp
public int CellHeight { get; }
```

セルの高さ（アトラス px）。

#### `InkBounds`

```csharp
public EmBox? InkBounds { get; }
```

収めた字のインク境界の和（em）。インクのある字が無ければ null。箱を明示したときの見直しに使う。

#### `ComputeContentHash()`

```csharp
public string ComputeContentHash()
```

出力（距離レンジ・全セル・全ページの画素）の SHA-256（小文字の 16 進）。同じ入力から出る出力が変わったことを検出し、バージョン（`SdfAtlasBaker.Version`）の上げ忘れを止める検査が使う（README §7.5）。

### SdfBakeSettings

名前空間 `info.someiyoshino.T2Font.Bake`・アセンブリ `Someiyoshino.T2Font.Bake`・ソース [`Packages/info.someiyoshino.t2font/Bake/SdfAtlasBaker.cs`](../../../Packages/info.someiyoshino.t2font/Bake/SdfAtlasBaker.cs)

```csharp
public sealed class SdfBakeSettings
```

SDF のアトラスの生成の設定（README §9）。既定値は em48・スーパーサンプリング 8 倍（4 倍より 32〜128px の再構成誤差が 2 割以上小さい）。距離レンジは全フォント共通の `SdfEncoding.RangeEm` で、インポーターはこの設定を変えない。

#### `EmPx`

```csharp
public double EmPx { get; set; }
```

アトラス上の 1em の px。

#### `RangeEm`

```csharp
public double RangeEm { get; set; }
```

SDF の片側距離レンジ（em）。縁取り・ずらし・ぼかしが外へ広がれる上限がこれで決まる。アトラス上の px は `EmPx × RangeEm`。既定（＝インポーターが使う値）は全フォント共通の `SdfEncoding.RangeEm`。他の値はジェネレーターの検査にだけ使う。

#### `Supersample`

```csharp
public int Supersample { get; set; }
```

スーパーサンプリングの倍率（字を EmPx × この倍率で塗ってから距離を取り、縮小する）。

#### `CellBox`

```csharp
public EmBox? CellBox { get; set; }
```

セルのインク境界。null なら依頼された字のインク境界の和を使う（はみ出す字は原理的に出ない）。明示した箱からはみ出す字があれば、生成は失敗する（黙って切らない・README §2.1）。

#### `MaxPageWidth`

```csharp
public int MaxPageWidth { get; set; }
```

1 ページの最大幅（px）。BC4 のブロック（4 px）の倍数に丸めて使う。既定の 8192 は、JIS X 0208 の全字（既定値で 5976×5880）も簡体字中国語の規模も 1 ページに収める。**ページは少ないほどよい**——字ごとにテクスチャが違うと uGUI のバッチが割れる。複数ページは上限を超えたときだけ出す。

#### `MaxPageHeight`

```csharp
public int MaxPageHeight { get; set; }
```

1 ページの最大高さ（px）。

#### `FlattenTolerance`

```csharp
public double FlattenTolerance { get; set; }
```

輪郭を線分へフラット化するときの許容誤差（スーパーサンプリング後のピクセル）。

#### `Parallel`

```csharp
public bool Parallel { get; set; }
```

字ごとの距離場の計算を並列に行うか（結果は並列でも直列でも同一）。

#### `RangePx`

```csharp
public double RangePx { get; }
```

アトラス上の片側距離レンジ（px）。
