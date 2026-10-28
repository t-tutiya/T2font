# Changelog

本パッケージの版の記録。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、版は [Semantic Versioning](https://semver.org/lang/ja/) に従う
（0.x の間は小さい版でも互換を壊しうる）。

## [Unreleased]

### Added

- パッケージの骨格（アセンブリ・ライセンス・契約の正典としての README）。
- Core: 字の表の型（`FontFaceMetrics`・`GlyphMetrics`）・字形番号の検索（`GlyphTable`）・様式（縁）の契約（`GlyphEdgeStyle`・`GlyphEffects`）・字の走査（`TextCodepoints`）。
- Bake: TrueType の読取・輪郭・ラスタ・厳密距離変換・配置・字集合（JIS X 0208 ほか）、表現と独立した字の表の生成（`FontFaceBuilder`）、SDF のアトラスの生成（`SdfAtlasBaker`）、表現の種類の記述子（`GlyphRepresentationKind`・`SdfRepresentationKind`）、`representation` 節を持つ生成仕様（`FontBakeSpec`）。
- Runtime: フォント資産 `T2FontAsset`（字の表・字形番号の解決・未収録の字の報告）、表現の基底 `GlyphRepresentation`、幾何の器 `GlyphGeometry`（UV0.zw＝em 座標を器が書く）、マテリアルの置き場 `GlyphMaterialCache`（様式と表現の能力の照合）、SDF の表現 `SdfRepresentation` とシェーダ `T2Font/SDF`。
- Editor: `.t2font` の取込器（焼き器を `TypeCache` で集めて種類で振り分ける・種類ごとの版数をカスタム依存にする）、焼き器の基底 `GlyphRepresentationBaker` と文脈 `GlyphBakeContext`、SDF の焼き器 `SdfGlyphBaker`。
