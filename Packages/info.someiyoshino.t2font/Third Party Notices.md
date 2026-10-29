# Third Party Notices

T2Font のコードは MIT ライセンス（`LICENSE.md`）で、権利者は t-tutiya。本パッケージは第三者のコードを含まない。

## 手法の出典

- **厳密ユークリッド距離変換**（`Bake/DistanceTransform.cs`）: P. F. Felzenszwalb and D. P. Huttenlocher,
  "Distance Transforms of Sampled Functions", Theory of Computing, Vol. 8, 2012. 論文の手法を自前で実装したもので、論文のコードは含まない。
- **TrueType の読み取り**（`Bake/TrueTypeFont.cs`）: OpenType 仕様（`head`・`hhea`・`maxp`・`hmtx`・`cmap`・`loca`・`glyf`）に基づく自前の実装。
- **JIS X 0208 の文字セット**（`Bake/JisX0208Table.cs`）: 規格の字の並び（区点の順）を Unicode へ写した字の一覧。

## 検証用のフォント（パッケージには含まない）

リポジトリ `t-tutiya/T2font` の検証用プロジェクトは、元フォントとして M+ FONTS（M+ 1c ほか）を同梱する。
パッケージを写す先のプロジェクトへは入らない。M+ FONTS のライセンス（フォントの `name` 表の記述）:

> This font is free software. Unlimited permission is granted to use, copy, and distribute it, with or without modification,
> either commercially or noncommercially. THIS FONT IS PROVIDED "AS IS" WITHOUT WARRANTY.
