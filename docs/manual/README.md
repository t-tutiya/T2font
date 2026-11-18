# T2Font・Rinkaku 利用の手引き

[English](README.en.md)

T2Font（Unity uGUI の文字描画のパッケージ）と Rinkaku（その上のグリフ表現）を、自分の Unity プロジェクトで使うプログラマ向けの手引き。
やりたいことから、読む文書を選ぶ。

- **契約の正典はパッケージの README**（[T2Font](../../Packages/info.someiyoshino.t2font/README.md)・
  [Rinkaku](../../Packages/info.someiyoshino.rinkaku/README.md)）。API の約束・データの形式・シェーダの規約はそちらが持ち、本手引きは
  手順とコード例を受け持つ。手引きの中の「README §n」は、断りが無ければ T2Font の README の節を指す。
- 前提: Unity 6・uGUI（Canvas と `RectTransform`）・C#。
- 手引きのコード例は、本リポジトリの検証のプロジェクトでコンパイルしているファイルから生成している（D-USERDOCS）。

## 文書

| 文書 | こんなときに読む |
|---|---|
| [`01-quickstart.md`](01-quickstart.md) | 導入して、Canvas に最初の字を出す。グリフ表現を SDF から Rinkaku へ替えて比べる |
| [`02-host-guide.md`](02-host-guide.md) | 自分のテキスト描画（ホスト）に組み込む。フォントの読み込み・字の配置・幾何・マテリアル・フェード・片付け |
| [`03-choosing-representation.md`](03-choosing-representation.md) | SDF と Rinkaku のどちらでベイクするかを決める。設定の調え方 |
| [`04-shipping-fonts.md`](04-shipping-fonts.md) | 出荷のときに、本文で使う字だけでフォントアセットをベイクし直す |
| [`05-troubleshooting.md`](05-troubleshooting.md) | インポートの失敗・字が出ない・見た目・性能・環境の問題を、症状から引く |
| [`06-custom-representation.md`](06-custom-representation.md) | 新しい描き方をグリフ表現として足す（種類の記述子・グリフ表現・シェーダ・ベイカー・適合検査） |

### 英語版 / English

| 文書 | 内容 |
|---|---|
| [`README.en.md`](README.en.md) | 本書の英語版（The English version of this page） |
| [`01-quickstart.en.md`](01-quickstart.en.md) | クイックスタートの英語版 |

## API リファレンス

公開型のシグネチャと説明は [API リファレンス](../generated/api/README.md)（パッケージのソースから生成）。**契約の内**（利用者が使う型・役割別）と
**契約の外**（ベイカーの部品や検査のための公開。版の間で予告なく変わりうる）に分けてある。利用側のコードは契約の内の型だけを使う。

## 仕組みを知る

使うだけなら読まなくてよい。動作の理解や、問題の切り分けに使う。

| 文書 | 内容 |
|---|---|
| [TrueType からアトラスへ](https://t-tutiya.github.io/T2font/architecture/02-truetype-to-atlas.html) | TrueType の構造・タイポグラフィテーブルの生成・SDF のアトラスをベイクする工程 |
| [アトラスから画面へ](https://t-tutiya.github.io/T2font/architecture/03-atlas-to-screen.html) | 字の配置・マテリアル・CanvasRenderer とバッチ・シェーダがレンダーターゲットへ描くまで |
| [ベイク仕様の書き方](https://t-tutiya.github.io/T2font/architecture/04-bake-spec.html) | `.t2font` のキー・アセットを作る手順・インポートの失敗と直し方・文字セットの中身 |
| [Rinkaku の構造](https://t-tutiya.github.io/T2font/architecture/rinkaku/01-structure.html) | SDF の角が丸くなる理由・和集合輪郭・セグメントテーブル・最近傍インデックス・シェーダ・SDF と比べた結果 |
| [Rinkaku の必要集合](https://t-tutiya.github.io/T2font/architecture/rinkaku/02-nearest-set.html) | セルごとに書くセグメントの選び方・取りこぼしの誤差の上界 |
| [Rinkaku の距離の解き方](https://t-tutiya.github.io/T2font/architecture/rinkaku/03-distance.html) | 2 次ベジエへの最短距離・単精度で起きること・Newton 法の 1 反復 |

どれも図を含む HTML で、[GitHub Pages](https://t-tutiya.github.io/T2font/) で読む（リポジトリの中の `docs/architecture/` の HTML は、GitHub の画面ではソースが表示される）。
Rinkaku の 3 章は、パッケージの正本の開発環境の図解を版ごとに写したもの。

## 版と変更

- 導入の URL と最新の版のタグは、リポジトリのルートの [`README.md`](../../README.md)（Install / 導入）。
- 版ごとの変更（互換を壊す変更を含む）は各パッケージの CHANGELOG（[T2Font](../../Packages/info.someiyoshino.t2font/CHANGELOG.md)・
  [Rinkaku](../../Packages/info.someiyoshino.rinkaku/CHANGELOG.md)）。0.x の間は小さい版でも互換を壊しうる。

## 用語

訳語は [`../reference/terminology.md`](../reference/terminology.md) に従う。手引きでよく使う語:

| 語 | 意味 |
|---|---|
| ホスト | 字を画面へ置く側のコード。検証ビュー `T2FontText`、または利用側のテキスト描画 |
| タイポグラフィテーブル | 送り幅・インク境界・縦メトリクス。どのグリフ表現でベイクしても同じ |
| グリフ表現 | 字のインクをどう描くか（SDF・Rinkaku など）。フォントアセットのサブアセット |
| ベイク仕様 | `.t2font` のファイル（JSON）。インポーターが元フォントからフォントアセットをベイクする |
| エッジスタイル | 面の縁のずらしとぼかし・縁取りの幅と色・外縁のぼかし。値は em |
