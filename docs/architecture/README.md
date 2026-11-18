# T2font アーキテクチャ解説

内部構造と動作原理をまとめたドキュメント群。**どうなっているか**（構造・動作原理・不変条件）の正典（D-DOCDUP）。
Claude との認識合わせにも使う。構造や動作が変わったら該当章を同じ PR で更新する。

## 構成

章は番号付きのファイルで、番号は**論理順**（土台 → 個々の仕組み → 横断の仕組み）に揃える。章を足したら下の表に 1 行加える。

| 章 | 内容 |
|---|---|
| [`01-assemblies-and-tests.md`](01-assemblies-and-tests.md) | アセンブリ（asmdef）の構成・2 つのコンパイル世界・テストの置き場 |
| [`02-truetype-to-atlas.html`](02-truetype-to-atlas.html) | TrueType の構造とフォントメトリクス・タイポグラフィテーブルの生成・SDF アトラスをベイクする工程（図解の HTML。画像は `02-truetype-to-atlas/`） |
| [`03-atlas-to-screen.html`](03-atlas-to-screen.html) | アセットの読み込み・字の配置と幾何・マテリアル・CanvasRenderer とバッチ・シェーダがレンダーターゲットへ描いて画面に出るまで（図解の HTML。画像は `03-atlas-to-screen/`、図を作るスクリプトは `Tools/docs-figures/03-atlas-to-screen/`） |
| [`04-bake-spec.html`](04-bake-spec.html) | ベイク仕様（`.t2font`）の書き方・フォントアセットを作る手順・インポートの失敗と直し方・文字セットの中身と付録の文字リスト（HTML。表と付録を作るスクリプトは `Tools/docs-figures/04-bake-spec/`） |

**Rinkaku の図解（`rinkaku/`）**: パッケージ Rinkaku の正本の開発環境の図解を、版ごとに写したもの（本リポジトリでは図を作り直さない・D-T2FREPUB）。
契約の正典は Rinkaku の README。番号は Rinkaku の章の番号で、上の 01〜04 章とは別（フォルダで分ける）。

| 章 | 内容 |
|---|---|
| [`rinkaku/01-structure.html`](rinkaku/01-structure.html) | Rinkaku の構造（SDF の角が丸くなる理由・和集合輪郭・セグメントテーブル・最近傍インデックス・ページと UV0・シェーダ・SDF と比べた結果・開発で得た知見。画像は `rinkaku/01-structure/`） |
| [`rinkaku/02-nearest-set.html`](rinkaku/02-nearest-set.html) | Rinkaku の必要集合の作り方（上位集合・Lipschitz の判定・サンプリングと 2 分・取りこぼしの誤差の上界・区間演算の上位集合との比べ） |
| [`rinkaku/03-distance.html`](rinkaku/03-distance.html) | Rinkaku の距離の解き方（2 次ベジエへの最短距離の 3 次方程式・カルダノの公式と三角関数の解・単精度の桁落ち・Newton 法の 1 反復） |

**公開**: HTML の章（02〜04 章と `rinkaku/`）は GitHub の画面では読めないので、`.github/workflows/pages.yml` が `main` への push で
[GitHub Pages](https://t-tutiya.github.io/T2font/) へ配る（目次は `Tools/pages/build_site.py` が各 HTML の `<title>` から作る・D-PAGES）。
HTML の章を足したら、Pages の目次には自動で載る。

## 書き方の約束

- 構造を説明し、**なぜそうしたか**は書かない（決定 ID 1 個で `../archive/decisions.md` へ引く）。
- 一覧（個数のある表）は手で持たない。asmdef の一覧は [`../generated/assembly-reference.md`](../generated/assembly-reference.md)。
- `CLAUDE.md` の「アーキテクチャ概要（索引）」には、各章を 1〜2 行＋参照先で載せる（要約を置かない）。
