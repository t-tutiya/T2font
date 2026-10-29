# 03 章の図を作るスクリプト

[`docs/architecture/03-atlas-to-screen.html`](../../../docs/architecture/03-atlas-to-screen.html)（アトラスから画面へ）の、実データの図・表の数値・画像を作る。
パッケージの Runtime・シェーダ `T2Font/SDF`・ベイカーの出力を変えたら走らせて、図を作り直す。

## 要るもの

- .NET 8 SDK（外部 NUnit と同じ）
- Python 3 と numpy・Pillow

## 走らせ方

リポジトリのルートで:

```bash
python3 Tools/docs-figures/03-atlas-to-screen/build.py
```

1. `Dump/`（パッケージの `Core/` と `Bake/` をリンクしたコンソールプログラム）が、見本のベイク仕様 `Assets/Content/Fonts/mplus-1c-sdf.t2font` を
   インポーターと同じ手順でベイクし、元データ（タイポグラフィテーブル・セル・ページの画素）を一時フォルダへ書き出す。
2. `figures.py`（実データの図）と `schematics.py`（模式図）が、元データと `sdfemu.py`（シェーダの `frag` と同じ式の CPU 版）から SVG・表の行・数値を作り、
   `docs/architecture/03-atlas-to-screen/` の PNG を書き出す。
3. `build.py` が、本書の `<!-- gen:名前 -->` と `<!-- /gen -->` の間だけを書き換える。

## 本書との分担

- **印の間はスクリプトの持ち物**。手で直しても、次に走らせると上書きされる。図や数値を変えたいときはスクリプトを直す。
- **印の外（地の文・表の見出し・CSS）は本書が正本**。スクリプトは触らないので、本書を直接直す。
- 本書にある印とスクリプトが作る値の名前が食い違うと、何も書き換えずに止まる。印を足したり消したりしたら、`build.py` の `values()` も合わせる。

## 出力の決定性

同じ入力からは、同じ HTML と同じ PNG（バイト列まで）が出る。走らせた後に `git diff` が空なら、図は今のパッケージの出力と合っている。
アトラスのハッシュ（本書の末尾の `ComputeContentHash`）が変わったら、ベイカーの出力が変わったということ。

`Dump/` は CI のビルドの対象ではない（`Tests/T2font.Tests.csproj` とは別のプロジェクト）。パッケージの Core・Bake の公開 API を変えて `Dump/` が
ビルドできなくなったら、走らせるときに直す。
