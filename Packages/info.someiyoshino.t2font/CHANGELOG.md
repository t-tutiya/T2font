# Changelog

本パッケージの版の記録。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、版は [Semantic Versioning](https://semver.org/lang/ja/) に従う
（0.x の間は小さい版でも互換を壊しうる）。

## [Unreleased]

## [0.3.0] - 2026-11-09

### Added

- `T2FontImporter.RepresentationKinds()`（README §7.4）: インポーターが受け付けるグリフ表現の種類の一覧。利用側のツールがベイク仕様を読むときに `FontBakeSpec.Parse` へ渡す。
- `JisX0208Table` を公開にした（README §7.2）: 固定の文字セットの元データの表。利用側が文字セットの名前に無い組み合わせ（非漢字だけなど）を組み立てる。
- `GlyphOutline.EnumerateSegments` と `OutlineSegment`（README §8）: 輪郭を直線と 2 次ベジエの区間に分けて返す。曲線をそのまま使うグリフ表現のベイカーが使う。
  `Flatten` は同じ区間をフラット化する（出力は変わらない）。
- `GlyphBakeContext.AddDataTexture` と `GlyphDataPage`（README §4.4）: データテクスチャ（R8 か RGBA8）を非圧縮・point・linear・ミップ無し・clamp・
  非 Readable で作って検査し、サブアセットとして渡す。グリフ表現のベイカーがテクスチャの形式を自分で検査しなくてよくなる。

### Changed

- `SpecSection`（README §8）: JSON の読み手の型を公開しないようにした。コンストラクタ・`Json`・`Fail(JToken?, string)` は内部へ下げ、
  グリフ表現の種類は型付きの読み口と、キー名で値を名指す `Fail(string key, string message)` を使う。**グリフ表現を足すパッケージは
  `section.Fail(section.Json["キー"], …)` を `section.Fail("キー", …)` へ書き換える**。
- README §11: 正本は利用側へ写したフォルダに置いて直接直し、版は公開し直すときに上げる運用にした。公開の版は `v0.3.0`。
- 用語をさらに揃えた（README・コメント・ベイク仕様の読み取りとインポートの誤りの文面・例外の文面）。主な言い換え: 表現→グリフ表現・
  生成器→ジェネレーター・鍵→キー・道具→ツール・窓→範囲。API とベイクの出力は変わらない。

## [0.2.0] - 2026-10-29

### Added

- シェーダの契約 S9（README §5）: 面の色は linear で使い、uniform `_UIVertexColorAlwaysGammaSpace` が立てば（Linear のプロジェクトで）頂点色の rgb を
  `UIGammaToLinear` で linear に直す。**表現を足すパッケージのシェーダも S9 を満たす必要がある**（適合検査が確かめる）。
- 適合検査 `GlyphConformance.CheckRendering` が S9 を確かめる（灰色の頂点色で、uniform が立つときと立たないときの面の色を見る）。

### Fixed

- Canvas の Vertex Color Always in Gamma Color Space（`Canvas.vertexColorAlwaysGammaSpace`）を有効にすると、`T2Font/SDF` が gamma のまま来た頂点色を
  linear として扱い、面の色が明るくずれていた（sRGB の 128 が 188 に出る）。シェーダが組み込みの UI シェーダと同じ関数で linear に直し、同じ色の
  `Image` などと揃う。

### Changed

- 用語を一般的なカタカナの訳語へ揃えた（README・コメント・インポートの誤りと報告の文面・適合検査の文面）。主な言い換え: 焼く→ベイクする・
  焼き器→ベイカー・取込器→インポーター・生成仕様→ベイク仕様・字形→グリフ・字形番号→グリフインデックス・字の表→タイポグラフィテーブル・
  墨の外接→インク境界・様式（縁）→エッジスタイル・距離域→距離レンジ・資産→アセット・版数→バージョン。API とベイクの出力は変わらない。

## [0.1.0] - 2026-10-28

最初の公開版。

### Added

- パッケージの骨格（アセンブリ・ライセンス・契約の正典としての README）。導入は git URL かフォルダの写しの 2 通り（README §11）。
- Core: 字の表の型（`FontFaceMetrics`・`GlyphMetrics`）・字形番号の検索（`GlyphTable`）・様式（縁）の契約（`GlyphEdgeStyle`・`GlyphEffects`）・字の走査（`TextCodepoints`）。
- Bake: TrueType の読取・輪郭・ラスタ・厳密距離変換・配置・字集合（JIS X 0208 ほか）、表現と独立した字の表の生成（`FontFaceBuilder`）、SDF のアトラスの生成（`SdfAtlasBaker`）、表現の種類の記述子（`GlyphRepresentationKind`・`SdfRepresentationKind`）、`representation` 節を持つ生成仕様（`FontBakeSpec`）。
- Runtime: フォント資産 `T2FontAsset`（字の表・字形番号の解決・未収録の字の報告）、表現の基底 `GlyphRepresentation`、幾何の器 `GlyphGeometry`（UV0.zw＝em 座標を器が書く）、マテリアルの置き場 `GlyphMaterialCache`（様式と表現の能力の照合）、SDF の表現 `SdfRepresentation` とシェーダ `T2Font/SDF`。
- Editor: `.t2font` の取込器（焼き器を `TypeCache` で集めて種類で振り分ける・種類ごとの版数をカスタム依存にする）、焼き器の基底 `GlyphRepresentationBaker` と文脈 `GlyphBakeContext`、SDF の焼き器 `SdfGlyphBaker`。
- Viewer: 検証ビュー `T2FontText`（字ごとに 1 つの `CanvasRenderer` で描く）。
- Conformance: 表現の適合検査 `GlyphConformance`（幾何の検査と、描いた画素でのシェーダの契約の検査）。

[Unreleased]: https://github.com/t-tutiya/T2font/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/t-tutiya/T2font/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/t-tutiya/T2font/releases/tag/v0.1.0
