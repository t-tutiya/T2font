# 04 章の表と付録を作るスクリプト

[`docs/architecture/04-bake-spec.html`](../../../docs/architecture/04-bake-spec.html)（ベイク仕様の書き方）の、数・表の行・誤りの文面・付録の文字リストを作る。
パッケージの文字セット（`Bake/FontCharsets.cs`・`Bake/JisX0208Table.cs`）・ベイク仕様の読み取り・ベイカーの文面を変えたら走らせて、作り直す。

## 要るもの

- .NET 8 SDK（外部 NUnit と同じ）
- Python 3（標準ライブラリだけ）

## 走らせ方

リポジトリのルートで:

```bash
python3 Tools/docs-figures/04-bake-spec/build.py
```

1. `Dump/`（パッケージの `Core/` と `Bake/` をリンクしたコンソールプログラム）が、文字セットごとのコードポイント・JIS X 0208 と CP932 の周辺の表の並び・
   記号のブロック・Unicode の一般カテゴリ・誤りの例の文面・見本のインポートの値・emPx ごとの寸法の見積もりを、一時フォルダへ書き出す。
   誤りの例は、本書の基本の形を書き換えたベイク仕様を、インポーターと同じ `FontBakeSpec.Parse` → `FontSpecBaker.BuildFace` → `SdfAtlasBaker.Bake` に通した文面。
2. `build.py` が、元データから表の行・付録の HTML を作り、本書の `<!-- gen:名前 -->` と `<!-- /gen -->` の間だけを書き換える。
   JIS X 0208 の区は Python の `euc_jp`、`WindowsVariants` の JIS の側の字は `cp932` と `shift_jis` で求める。

## 本書との分担

- **印の間はスクリプトの持ち物**。手で直しても、次に走らせると上書きされる。数や文面を変えたいときはスクリプトか、その元のパッケージのコードを直す。
- **印の外（地の文・表の見出し・CSS）は本書が正本**。スクリプトは触らないので、本書を直接直す。
- 本書にある印とスクリプトが作る値の名前が食い違うと、何も書き換えずに止まる。印を足したり消したりしたら、`build.py` の `values()` も合わせる。
- 記号のブロックが増えたら `build.py` の `BLOCK_NAMES` に名前を足す（足さないと止まる）。

## 出力の決定性

同じ入力からは、同じ HTML が出る。走らせた後に `git diff` が空なら、本書は今のパッケージの出力と合っている。
Unicode の一般カテゴリは .NET 8 の `CharUnicodeInfo` から取るので、.NET の版を上げると未割り当ての判定が変わりうる。

`Dump/` は CI のビルドの対象ではない（`Tests/T2font.Tests.csproj` とは別のプロジェクト）。パッケージの Core・Bake の API や、Dump が読む内部の
名前（`JisX0208Table` の定数・`FontCharsets.SymbolBlocks`・`SymbolExclusions`・`SdfAtlasBaker.ComputeCell`・`AtlasLayout.Plan`）を変えて
`Dump/` が動かなくなったら、走らせるときに直す。寸法の見積もりは見本の実際のベイクの結果と照らし合わせ、合わなければ止まる。
