# Changelog

本パッケージの版の記録。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、版は [Semantic Versioning](https://semver.org/lang/ja/) に従う
（0.x の間は小さい版でも互換を壊しうる）。

## [Unreleased]

### Added

- パッケージの骨格（アセンブリ・ライセンス・契約の正典としての README）。
- Core: 字の表の型（`FontFaceMetrics`・`GlyphMetrics`）・字形番号の検索（`GlyphTable`）・様式（縁）の契約（`GlyphEdgeStyle`・`GlyphEffects`）・字の走査（`TextCodepoints`）。
- Bake: TrueType の読取・輪郭・ラスタ・厳密距離変換・配置・字集合（JIS X 0208 ほか）、表現と独立した字の表の生成（`FontFaceBuilder`）、SDF のアトラスの生成（`SdfAtlasBaker`）、表現の種類の記述子（`GlyphRepresentationKind`・`SdfRepresentationKind`）、`representation` 節を持つ生成仕様（`FontBakeSpec`）。
