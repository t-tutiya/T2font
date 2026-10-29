# T2font アーキテクチャ解説

内部構造と動作原理をまとめたドキュメント群。**どうなっているか**（構造・動作原理・不変条件）の正典（D-DOCDUP）。
Claude との認識合わせにも使う。構造や動作が変わったら該当章を同じ PR で更新する。

## 構成

章は番号付きのファイルで、番号は**論理順**（土台 → 個々の仕組み → 横断の仕組み）に揃える。章を足したら下の表に 1 行加える。

| 章 | 内容 |
|---|---|
| [`01-assemblies-and-tests.md`](01-assemblies-and-tests.md) | アセンブリ（asmdef）の構成・2 つのコンパイル世界・テストの置き場 |
| [`02-truetype-to-atlas.html`](02-truetype-to-atlas.html) | TrueType の構造とフォントメトリクス・タイポグラフィテーブルの生成・SDF アトラスをベイクする工程（図解の HTML。画像は `02-truetype-to-atlas/`） |

## 書き方の約束

- 構造を説明し、**なぜそうしたか**は書かない（決定 ID 1 個で `../archive/decisions.md` へ引く）。
- 一覧（個数のある表）は手で持たない。asmdef の一覧は [`../generated/assembly-reference.md`](../generated/assembly-reference.md)。
- `CLAUDE.md` の「アーキテクチャ概要（索引）」には、各章を 1〜2 行＋参照先で載せる（要約を置かない）。
