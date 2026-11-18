<!-- 自動生成（Tests/ApiReferenceDocTests.cs）。手で編集しない。パッケージを写し直したら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# API リファレンス

T2Font と Rinkaku の公開型の一覧。パッケージのソース（XML コメント）から生成している。使い方の手順は [利用の手引き](../../manual/README.md)、
契約の正典は各パッケージの README（文中の「README §n」は、その型のパッケージの README の節）。

- **契約の内**: 利用者——ホスト・ベイク仕様を扱うツール・グリフ表現の作者——が使うことを README が想定している型。役割別に並べる。
  互換を壊す変更は各パッケージの CHANGELOG に書かれる。
- **契約の外**: 公開されているが、ベイカーの部品や、検査と計測のための型。版の間で予告なく変わりうるので、利用側のコードから使わない。

型ごとの分類は、ジェネレーター（`Tests/ApiReferenceDocTests.cs`）の表が持つ（D-USERDOCS）。

| ページ | 内容 |
|---|---|
| [`t2font.md`](t2font.md) | T2Font の契約の内（役割別） |
| [`t2font-outside.md`](t2font-outside.md) | T2Font の契約の外 |
| [`rinkaku.md`](rinkaku.md) | Rinkaku の契約の内（役割別） |
| [`rinkaku-outside.md`](rinkaku-outside.md) | Rinkaku の契約の外 |
| [`undocumented.md`](undocumented.md) | XML コメントの無い公開メンバー（パッケージの正本の開発環境へ返す一覧） |

## T2Font — 契約の内

### ホスト（字を画面へ置く側）

| 型 | 概要 |
|---|---|
| [`T2FontAsset`](t2font.md#t2fontasset) | フォントアセット＝タイポグラフィテーブルとグリフ表現（README §1・§3.1）。 |
| [`GlyphRepresentation`](t2font.md#glyphrepresentation) | グリフ表現（README §1・§3.4・§4）。 |
| [`GlyphGeometry`](t2font.md#glyphgeometry) | グリフ表現がグリフの幾何を書き込む器（README §4.2）。 |
| [`GlyphMaterialCache`](t2font.md#glyphmaterialcache) | グリフのマテリアルの置き場（README §4.5）。 |
| [`GlyphEdgeStyle`](t2font.md#glyphedgestyle) | エッジスタイルの値一式＝縁取りの色と、2 つの境界（面の縁・縁取りの外縁）それぞれの位置とぼかし（README §6.1）。 |
| [`GlyphEffects`](t2font.md#glypheffects) | エッジスタイルの効果の種類（README §6.3）。 |
| [`GlyphMetrics`](t2font.md#glyphmetrics) | グリフ 1 つのタイポグラフィテーブルの記録（README §3.3）。 |
| [`FontFaceMetrics`](t2font.md#fontfacemetrics) | フォント単位の値（README §3.1）。 |
| [`TextCodepoints`](t2font.md#textcodepoints) | 文字列を字（Unicode のコードポイント）の並びとして読む（README §3.2）。 |

### 検証ビュー

| 型 | 概要 |
|---|---|
| [`T2FontText`](t2font.md#t2fonttext) | 検証ビュー（README §10）。 |

### ベイク仕様を扱うツール（Editor と外部 NUnit）

| 型 | 概要 |
|---|---|
| [`FontBakeSpec`](t2font.md#fontbakespec) | フォントのベイク仕様（`*.t2font`・JSON・README §7）。 |
| [`FontCoverage`](t2font.md#fontcoverage) | フォントが本文をどこまで覆うべきか（README §7.1）。 |
| [`FontCharsetKind`](t2font.md#fontcharsetkind) | ジェネレーターの固定の文字セット（README §7.2）。 |
| [`FontCharsets`](t2font.md#fontcharsets) | 文字セットの組み立てと、アトラスのページへ並べる順（優先クラス）を決める。 |
| [`JisX0208Table`](t2font.md#jisx0208table) | JIS X 0208 の全 6,879 字（Unicode・JIS の標準の対応表による）と、Windows（CP932）で入力されやすい周辺の字。 |
| [`GlyphRepresentationKinds`](t2font.md#glyphrepresentationkinds) | 使うグリフ表現の種類の一覧（README §7.4）。 |
| [`T2FontImporter`](t2font.md#t2fontimporter) | ベイク仕様（`*.t2font`）のインポーター（README §7）。 |
| [`TrueTypeFont`](t2font.md#truetypefont) | TrueType（`glyf` 輪郭）フォントのリーダー（README §7.3）。 |
| [`FontSpecBaker`](t2font.md#fontspecbaker) | ベイク仕様と元フォントからタイポグラフィテーブルを作る（README §7.3 の 2・3）。 |
| [`FontSpecFace`](t2font.md#fontspecface) | ベイク仕様から作ったタイポグラフィテーブルと、インポートの報告に出すもの。 |
| [`FontSpecException`](t2font.md#fontspecexception) | ベイク仕様の誤り（ファイル名と理由を文面が名指す）。 |

### グリフ表現の作者

| 型 | 概要 |
|---|---|
| [`GlyphRepresentationKind`](t2font.md#glyphrepresentationkind) | グリフ表現の種類の記述子（README §7.4）。 |
| [`GlyphRepresentationSettings`](t2font.md#glyphrepresentationsettings) | グリフ表現の種類ごとの設定（`GlyphRepresentationKind.ParseSettings` の結果）。 |
| [`SpecSection`](t2font.md#specsection) | ベイク仕様の JSON のオブジェクト 1 つを読む補助（誤りの文面にファイル名と行を付ける）。 |
| [`EmBox`](t2font.md#embox) | em の矩形（ペン原点とベースライン基準・y 上向き）。 |
| [`GlyphRepresentationBaker`](t2font.md#glyphrepresentationbaker) | グリフ表現の種類のベイカー（README §7.4）。 |
| [`GlyphBakeContext`](t2font.md#glyphbakecontext) | ベイカーがインポートの文脈に触れる口（README §7.3 の 4）。 |
| [`GlyphDataPage`](t2font.md#glyphdatapage) | グリフ表現のベイカーが作るデータテクスチャの中身 1 枚（README §4.4）。 |
| [`GlyphDataFormat`](t2font.md#glyphdataformat) | データテクスチャの texel の形式（README §4.4）。 |
| [`FontFaceData`](t2font.md#fontfacedata) | タイポグラフィテーブルの生成結果（README §3・§7.3）。 |
| [`FontFaceBuilder`](t2font.md#fontfacebuilder) | TrueType フォントからタイポグラフィテーブルを作る（README §7.3 の 3）。 |
| [`GlyphOutline`](t2font.md#glyphoutline) | グリフの輪郭（フォント単位・y 上向き）。 |
| [`OutlineSegment`](t2font.md#outlinesegment) | 輪郭の区間 1 本（フォント単位・y 上向き・README §8）。 |
| [`OutlinePoint`](t2font.md#outlinepoint) | 輪郭の 1 点（フォント単位・y 上向き）。 |
| [`OutlineEdge`](t2font.md#outlineedge) | 線分 1 本（ラスタライザの入力・ピクセル座標・y 上向き）。 |
| [`FontBakeException`](t2font.md#fontbakeexception) | ジェネレーターが続行できない誤り（はみ出す字・ページより大きいセルなど）。 |
| [`GlyphConformance`](t2font.md#glyphconformance) | グリフ表現の適合検査（README §10）。 |

### 同梱のグリフ表現: SDF

| 型 | 概要 |
|---|---|
| [`SdfRepresentation`](t2font.md#sdfrepresentation) | 同梱の SDF のグリフ表現（README §9）。 |
| [`SdfRepresentationKind`](t2font.md#sdfrepresentationkind) | 同梱の SDF のグリフ表現の種類（`"kind": "sdf"`・README §9）。 |
| [`SdfSpecSettings`](t2font.md#sdfspecsettings) | ベイク仕様の SDF の節（README §9 の表）。 |
| [`SdfPageFormat`](t2font.md#sdfpageformat) | SDF のページのテクスチャ形式（README §9）。 |
| [`SdfEncoding`](t2font.md#sdfencoding) | 同梱の SDF のグリフ表現の、全フォントに共通の定数（README §9）。 |
| [`SdfGlyphCell`](t2font.md#sdfglyphcell) | SDF のグリフ表現のグリフ 1 つのセル（README §9）。 |
| [`SdfGlyphBaker`](t2font.md#sdfglyphbaker) | 同梱の SDF のグリフ表現のベイカー（README §9）。 |

## T2Font — 契約の外

| 型 | 概要 |
|---|---|
| [`GlyphTable`](t2font-outside.md#glyphtable) | 字（コードポイント）の昇順の配列を引く（README §3.2）。 |
| [`DistanceTransform`](t2font-outside.md#distancetransform) | 厳密ユークリッド距離変換（Felzenszwalb &amp; Huttenlocher の放物線の下包絡・O(画素数)）。 |
| [`ScanlineRasterizer`](t2font-outside.md#scanlinerasterizer) | 線分の集まりを、画素の中心で内外を判定して 2 値に塗る（非ゼロ巻き数規則）。 |
| [`FontAtlasPage`](t2font-outside.md#fontatlaspage) | ジェネレーターが作るテクスチャのページ 1 枚（単チャネル 8bit・行優先・行 0 が下端＝Unity のテクスチャと同じ並び）。 |
| [`SdfAtlasBaker`](t2font-outside.md#sdfatlasbaker) | タイポグラフィテーブルから SDF のアトラスを作る（README §9）。 |
| [`SdfAtlas`](t2font-outside.md#sdfatlas) | SDF のアトラスの生成の結果（README §9）。 |
| [`SdfBakeSettings`](t2font-outside.md#sdfbakesettings) | SDF のアトラスの生成の設定（README §9）。 |

## Rinkaku — 契約の内

### グリフ表現 Rinkaku

| 型 | 概要 |
|---|---|
| [`RinkakuRepresentation`](rinkaku.md#rinkakurepresentation) | Rinkaku のグリフ表現（README §3.4・§3.5）。 |
| [`RinkakuRepresentationKind`](rinkaku.md#rinkakurepresentationkind) | Rinkaku のグリフ表現の種類（`"kind": "rinkaku"`・README §5）。 |
| [`RinkakuSpecSettings`](rinkaku.md#rinkakuspecsettings) | ベイク仕様の Rinkaku の節（README §5 の表）。 |
| [`RinkakuEncoding`](rinkaku.md#rinkakuencoding) | Rinkaku のデータの形式の定数（README §3）。 |
| [`RinkakuGlyphCell`](rinkaku.md#rinkakuglyphcell) | Rinkaku のグリフ表現のグリフ 1 つのセル（README §3.4）。 |
| [`RinkakuGlyphBaker`](rinkaku.md#rinkakuglyphbaker) | Rinkaku のグリフ表現のベイカー（README §4）。 |

## Rinkaku — 契約の外

| 型 | 概要 |
|---|---|
| [`RinkakuAtlasBaker`](rinkaku-outside.md#rinkakuatlasbaker) | Rinkaku のベイカー（純 C#・README §4）。 |
| [`RinkakuAtlas`](rinkaku-outside.md#rinkakuatlas) | ベイクした結果（セル・ページ・字ごとの統計）。 |
| [`RinkakuBakeSettings`](rinkaku-outside.md#rinkakubakesettings) | ベイクの設定（README §5）。 |
| [`RinkakuCellOrder`](rinkaku-outside.md#rinkakucellorder) | セルのセグメントの並び（README §4.3 の 4）。 |
| [`RinkakuGlyphStats`](rinkaku-outside.md#rinkakuglyphstats) | グリフ 1 つのデータの量とインデックスの形（README §7 の表と検査に使う）。 |
| [`RinkakuDecoder`](rinkaku-outside.md#rinkakudecoder) | ページのバイトから、シェーダと同じ手順で画素の符号付き距離を求める参照の実装（倍精度・README §3）。 |
