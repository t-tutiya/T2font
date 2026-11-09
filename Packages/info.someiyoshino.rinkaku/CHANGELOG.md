# Changelog

本パッケージの版の記録。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、版は [Semantic Versioning](https://semver.org/lang/ja/) に従う
（0.x の間は小さい版でも互換を壊しうる）。

## [Unreleased]

## [0.1.0] - 2026-11-09

### Added

- Core: データの形式の定数 `RinkakuEncoding` とグリフのセル `RinkakuGlyphCell`（README §3）。
- Bake: 種類の記述子 `RinkakuRepresentationKind`（`"kind": "rinkaku"`・設定 `indexEm`・`maxPageSize`・README §5）と、ベイカー
  `RinkakuAtlasBaker`（和の輪郭・区間の表の符号化・最近傍の索引格子・ページの配置・README §4）。
- Runtime: グリフ表現 `RinkakuRepresentation`（README §3.4・§3.5）。Shaders: `Rinkaku/Glyph` と、距離の求め方の `RinkakuDistance.hlsl`（途中の値を
  `RinkakuTrace` に出す）。Editor: ベイカー `RinkakuGlyphBaker`。
- Core: セル 1 つの区間の数の上限 `RinkakuEncoding.MaxItemsPerCell`（32）と、UV0 の詰め方 `PackedOffset`・`PackedDataBase`。
- Bake: セルの区間をセルの中心に近い順に書く（README §4.3・出力のバージョン 2）。Shaders: 外接矩形が今の最短より遠い区間を解かずに飛ばし、
  擬似法線は最も近い区間が端のときだけループの後で読む（README §3.5）。

### Changed

- Bake・Shaders: 直線は制御点の差を (0, 0) にして持ち（直線の印）、シェーダは三次方程式を解かずに射影で測る（README §3.2・§3.5・出力のバージョン 3）。
