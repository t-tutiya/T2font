# Changelog

本パッケージの版の記録。形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、版は [Semantic Versioning](https://semver.org/lang/ja/) に従う
（0.x の間は小さい版でも互換を壊しうる）。

## [Unreleased]

## [0.2.1] - 2026-11-12

### Changed

- Shaders: `RinkakuDistance.hlsl` が、セグメントの 3 点の AABB で遠いセグメントを解かずに飛ばす検査をやめ、セルのセグメントを全部解く（README §3.5・§7）。
  曲線優先の並び（0.2.0）の後は検査の利得が無く、統合 GPU の 40px 以上で描画の時間が 2〜11% 縮む（24px の明朝の画の密な字は 1% ほど遅くなる）。
  データの形式・ベイクの出力・描く絵は変わらない。`RinkakuTrace.itemD2` は飛ばしたセグメントの印（−2）を返さなくなる。

## [0.2.0] - 2026-11-12

### Changed

- **データの形式を変えた**（README §3.3・§3.4・出力のバージョン 5）: グリッドセルをセグメントの番号 2 つ（ナローインデックスは texel 1 つにセル 2 つ・ワイドインデックスは
  texel 1 つにセル 1 つ）にし、3 本以上のセルは字の中でまとめたレコードを 1 回読む。5 本以上のセルの溢れ表は長いレコードに置き換わる。インデックスブロックが
  ほぼ半分になり、漢字の 1 字のデータ量が約 2 割減る。3 本以上のセルで読みが 1 回増える（本文の大きさで描画の時間 +2% ほど）。ナローインデックスで持てる項目は
  252 個まで（`RinkakuEncoding.NarrowMaxEntries`）。`RinkakuEncoding` の溢れ表の定数は、レコードの印の関数（`RecordMarker`・`IsRecordMarker`・
  `RecordLowBits`）と定数（`RecordMarkers`・`NarrowMaxRecordTexels`・`WideMaxRecordTexels`・`ItemsInCell`・`ItemsInRecord`）に替わる。
  `RinkakuEncoding.BlockOriginCell`・`BlockWidth`・`TexelColumn` を足した。`RinkakuDistance.hlsl` の `RinkakuSignedDistanceInCell` は、texel の
  どちらの半分かの引数 `half` を取る。
- Bake: セルのセグメントを、曲線を先・直線を後（それぞれの中はセルの中心に近い順）に書く（README §4.3 の 4・出力のバージョン 4）。ウェーブが曲線の
  手順を進めるループの回数が減り、本文の大きさで描画の時間が 4〜7% 縮む。描く絵・データ量・シェーダは変わらない。
- Runtime: `RinkakuRepresentation.WriteGeometry` が T2Font の幾何の契約（`quadExtentEm`）に従い、quad をホストが渡した幅まで狭める（README §2）。
  データの形式とベイクの出力は変わらない。

- README とコードのコメント・例外の文面の用語を変えた（区間 → セグメント・索引 → インデックス・和の輪郭 → 和集合輪郭・格子 → グリッド・
  外接矩形 → AABB・射影 → 正射影・床 → 固定オーバーヘッド）。契約と動作は変わらない。この版より前の記述は当時の語のまま。

### Added

- Bake: セルのセグメントの並びの設定 `RinkakuBakeSettings.CellOrder`（`RinkakuCellOrder`・README §5）。既定は `CurvesFirst`。`NearFirst`
  （出力のバージョン 3 までの並び）は並びを比べる計測用で、ベイク仕様からは選べない。

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
