# T2font ドキュメント索引

全ドキュメントの索引と、ドキュメント運用ルールの正典。
プロジェクト概要は リポジトリ直下の [`CLAUDE.md`](../CLAUDE.md)、作業規約は [`guides/working-agreement.md`](guides/working-agreement.md) を参照。

## フォルダの構成（記述の軸ごと・D-DOCTREE）

フォルダは下の「ドキュメント運用ルール」の軸表に対応している。**何を探しているか（なぜ／どうなっている／どう書く／
どう作業する／次に何を）でフォルダが決まる**。

| 場所 | 軸（何を語るか） | 主な読み手 |
|---|---|---|
| [`roadmap.md`](roadmap.md) | 次に何をやるか | 全員 |
| [`reference/`](reference/README.md) | **どう書くか**（データ・スクリプト・設定ファイルの書き方） | データの書き手 |
| [`generated/`](generated/) | **どう書くか／どうなっているか**のうち機械生成の網羅表。**手で編集しない** | 同上 |
| [`architecture/`](architecture/README.md) | **どうなっているか**（構造・動作原理・不変条件） | 開発者・Claude |
| [`guides/`](guides/) | **どう作業するか**（作業規約・テスト・品質・運用） | 開発者・Claude |
| [`plans/`](plans/README.md) | **これから作る物の設計** | 着手する人 |
| [`archive/`](archive/README.md) | **なぜそうしたか**（decisions）・**いつ終わったか**・凍結文書・入力資料 | 経緯を追う人 |

## まず読む（現在地と計画）

| ドキュメント | 役割 |
|---|---|
| [`roadmap.md`](roadmap.md) | **ロードマップ**。順序の正典は「now / next / later」と「着手候補」表のみ |
| [`architecture/README.md`](architecture/README.md) | **アーキテクチャ解説の索引** |
| [`plans/README.md`](plans/README.md) | **設計計画の索引**。これから作る物の生きた設計正典（着手時に読む） |
| [`archive/decisions.md`](archive/decisions.md) | **設計判断ログ（索引）**。「なぜそうしたか」の正典 |

## 書き方（`reference/`）

書き手の入口は [`reference/README.md`](reference/README.md)。

## 自動生成（`generated/`）

**手で編集しない**。直すのは生成元で、再生成は `UPDATE_GENERATED=1` の `dotnet test` か `[regen]` 付きの push（D-REGEN）。

| ドキュメント | 生成元 |
|---|---|
| [`generated/assembly-reference.md`](generated/assembly-reference.md) | `Assets/` 配下の `.asmdef`（生成器は `Tests/AssemblyReferenceDocTests.cs`） |

## 作業手順（`guides/`）

| ドキュメント | 役割 |
|---|---|
| [`guides/working-agreement.md`](guides/working-agreement.md) | **作業規約**（実行環境・検証経路・ワークフロー・コメント規約・コミット）。`CLAUDE.md` が読み込む |
| [`guides/testing-workflow.md`](guides/testing-workflow.md) | テストの三層と置き場・実行手順 |
| [`guides/code-quality.md`](guides/code-quality.md) | `dotnet format`・`csc.rsp`・CI（Job A／Job B）・生成物の改行 |
| [`guides/template-sync.md`](guides/template-sync.md) | テンプレート（unity-project-template）との改善のやり取り |

## 設計計画（`plans/`・着手時に読む）

索引は [`plans/README.md`](plans/README.md)。

## アーカイブ（完了・凍結・歴史／`decisions.md` 以外は通常参照不要）

| ドキュメント | 役割 |
|---|---|
| [`archive/README.md`](archive/README.md) | アーカイブの索引（凍結文書・評価・退避ログ） |
| [`archive/decisions.md`](archive/decisions.md) | 設計判断ログ（索引）。本文は月別ファイル |
| [`archive/completed-roadmap.md`](archive/completed-roadmap.md) | 完了済みロードマップ（索引）。本文は月別ファイル |

## ドキュメント運用ルール

- **正典の所在**: 構造＝`architecture/`、書き方＝`reference/`（機械生成分は `generated/`）、作業規約＝`guides/working-agreement.md`、設計判断の経緯＝`archive/decisions.md`、計画＝`roadmap.md`、これから作る物の設計＝`plans/`。**ファイル名は移設しても変えない**——コード・データ・文書はファイル名と § 番号で参照している（D-DOCTREE）。

- **記述の重複禁止（1 つの事実は 1 か所・D-DOCDUP）**: **同じ内容を 2 つの文書が持たない**。文書は「何を語っているか」＝**軸**で正典が決まり、正典以外は**その軸の記述を持たず、決定 ID か章名で参照する**。

  | 軸（その記述は何を語っているか） | 正典 | 正典以外での書き方 |
  |---|---|---|
  | **なぜそうしたか**（裁定・理由・採らなかった案・経緯・診断） | `archive/decisions/decisions-YYYY-MM.md` | **決定 ID 1 個**（例＝`（D-DOCDUP）`）。理由を要約し直さない |
  | **どうなっているか**（構造・動作原理・不変条件） | `architecture/` の各章 | 章名＋1 行。仕組みを再説明しない |
  | **どう書くか**（データ・スクリプト・設定の文法） | `reference/` の各書（機械生成分は `generated/`） | 節番号で参照。表を複製しない |
  | **どう作業するか** | `guides/` の各書（作業規約は `guides/working-agreement.md`） | 節名で参照 |
  | **いつ何が終わったか** | `archive/completed-roadmap*.md` | — |
  | **次に何をやるか**（現在地・順序） | `roadmap.md` | — |
  | **これから作る物の設計** | `plans/` の各設計文書 | — |

  **軸が重なって 2 文書のどちらにも書けるときは、下の梯子で上に居るほうが持ち、下は参照だけを残す**。
  `archive/decisions/` ＞ `archive/completed-roadmap.md` ＞ `architecture/` ＞ `reference/`・`guides/` ＞ `roadmap.md` ＞ ソースコメント・`CLAUDE.md` ＞ データのコメント。
  **梯子が順位を付けているのは「なぜ」の記述だけ**である——`architecture/` は構造の説明については正典であり、上の軸表が優先する（梯子は同軸の衝突を解く道具で、軸の所有権を動かさない）。

  **梯子の下ほど書ける量が減る**。ソースコメントは**コードから読み取れない制約**だけを現在形で書き（規約は `guides/working-agreement.md`「コードコメント規約」）、データのコメントは**そのファイルの読み方**だけを書く。どちらも裁定の要約を置かない——**決定 ID を 1 個書けば追跡できる**。
  **`CLAUDE.md` はソースコメントと同格**である。全セッションの冒頭で読み込まれるので、要約の肥大はそのまま毎回の文脈コストになり、正典と二重になった記述は片方だけが古くなる。アーキテクチャについては**要約を置かず、1 項目 1〜2 行＋参照先**だけを書く。

- **設計判断の記録**: 当月ファイル `archive/decisions/decisions-YYYY-MM.md` の先頭へ追記し、索引 `archive/decisions.md` に 1 行（`日付 ｜ **太字タイトル（決定 ID）** — 30 字以内の補足`）を足す（当月ファイルが無ければ新規作成）。

- **完了記録の分割（月別・D-CRMONTH）**: 完了した Phase/Step はロードマップから完了記録へ退避する。**索引＝`archive/completed-roadmap.md`／本文＝月別ファイル `archive/completed-roadmap/completed-roadmap-YYYY-MM.md`**。**当月ファイルの先頭へ追記し、索引にも 1 行追加して月見出しの件数を合わせる**。不変条件 2 本は索引冒頭に明記。

- **設計文書のライフサイクル（二択・本節が規則の正典・D-DOCLIFE）**: `plans/` の設計文書は、対応する実装が完了した時点で**次のどちらかを必ず満たす**。①**凍結退避した**——要点を `archive/decisions.md` / 完了記録に集約し、本体を `archive/completed-designs/` へ移して冒頭に凍結バナーを付す（削除しない）。②**凍結しない場合は、冒頭バナーに「本書が現役なのは〜を所有するため」を明記した**——所有物は申し送り・未着手 Step の設計・後続が読む手順や前方制約など、**他のどの正典にも移せていないもの**に限る。移せるものが残っていなければ ① を選ぶ。**所有物は `roadmap.md` のいずれかの区画に作業候補として載せる**。**現役理由の正典は文書冒頭のバナー**で、`plans/README.md` の索引行に複製しない。削除するのは**不採用・陳腐化・上位文書へ改稿された文書のみ**。退避/削除ログは `archive/README.md`。二択のどちらを満たしたかは PR のドキュメント還元チェックリストで確認する。

- **参照の安定アンカー**: ソースコメント・文書からの出自参照は、揮発しやすい設計文書パスではなく**安定アンカー**（`archive/decisions.md` の決定 ID・`architecture/`・`reference/` の各書）を指す。設計文書を移動・退避しても参照が壊れないようにする。

- **散文に個数・バージョンを書かない**: 「〜は N 種」「`FormatVersion = N`」のような**数は、足すたびに黙って古くなる**。散文は「全種別」「現行のバージョン」と書き、**個数と一覧は生成リファレンス（`generated/`）、バージョンはコードの定数**を指す。一覧を書く必要がある場所は 1 か所に限る。

- **索引被覆は機械が見る**: `plans/README.md`・`archive/README.md`（`completed-designs/`・`evaluations/`）はフォルダの `*.md` と 1 対 1、完了記録の索引は月別本文と完了日ごとに同数であることを `Tests/DocsIndexCoverageTests.cs` が検査する（設計判断ログは `Tests/DecisionsIndexIntegrityTests.cs`）。文書を足す・移す・消すときは索引の行を同じコミットで直す。

- **文書を指す参照は機械が見る（D-DOCTREE）**: `Tests/DocsReferenceIntegrityTests.cs` が ①全文書（歴史記録を含む）の Markdown リンク ②生きた文書（`archive/` 以外）のバッククォート記法の `.md` パス ③コード・データ・設定が名指す `docs/…` のパスと `.md` のファイル名、の 3 つが実在する文書へ解決することを検査する。退避・移設では参照を同じコミットで書き換える。**歴史記録はリンクだけを張り直し、地の文の当時のパスは書き換えない**——②が歴史記録を見ないのはこのため。削除・改名した文書を名指したいときは、パスではなく決定 ID を書く。

- **検証ステータスを docs に書かない（D-ENV3）**: 「ユーザー検証待ち」等の一時ステータスは PR 本文のチェックリストにだけ書く（マージ後に更新されず陳腐化する）。
