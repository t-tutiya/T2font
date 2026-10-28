# 作業規約

本書は **`CLAUDE.md` から読み込まれる汎用の作業規約**で、テンプレート（unity-project-template）由来である（D-TEMPLATE）。
プロジェクト固有の規約は `CLAUDE.md` 側に書き、本書は書き換えずに保つ——テンプレート側の改善を取り込むときは本書を
差し替える（`template-sync.md`）。プロジェクトで規約を変えたくなったら、`CLAUDE.md` に「本書の〜は本プロジェクトでは〜とする」
と上書きを書く。

## 実行環境（3 環境運用）

**作業開始時にまず実行環境を判定し、その環境で実行できない検証を試行しない**（D-ENV3）。
下表は**その環境の中で直接回せるか**であり、**回せない＝検証できない、ではない**（どの環境からも経路③が使える・D-ENV4）。

| 環境 | 判定 | NUnit（`dotnet test Tests/`） | Unity ローカル（`verify-unity-compile.ps1` / Play Mode） | CI（経路③） |
|---|---|---|---|---|
| ① ローカル PC（CLI・Windows） | `CLAUDE_CODE_REMOTE` 未設定 | ✅ | ✅ | ✅（手元が速いので常用しない） |
| ② リモート（claude.ai/code・Linux コンテナ） | `CLAUDE_CODE_REMOTE=true` | SessionStart フックの出力に従う | ❌ 恒久的に不可 | ✅ **②の主たる検証手段** |

- ②では `.claude/hooks/session-start.sh` が `[T2font 環境判定]` を出力する。その出力を正とし、「実行不可」とされた
  検証は**コンテナ内では**試行しない（出力が見当たらなければ `dotnet --version` の成否 1 回で判定）。
- ①（ユーザーのローカル環境）へ委譲が必要なのは **Play Mode テストと実機目視だけ**（Unity コンパイルは経路③の PR で回る）。

### 検証経路③: push → CI（D-ENV4）

`.github/workflows/quality.yml` は `workflow_dispatch` を持つので、**ブランチを push すれば環境を問わず手動実行できる**。

| ジョブ | 内容 | 回る契機 |
|---|---|---|
| `lint-and-unit`（Job A） | `dotnet restore` → `dotnet format whitespace --verify-no-changes` → `dotnet test` → `editorconfig-checker` | PR・`main` への push・手動実行 |
| `unity-compile`（Job B） | GameCI EditMode スモーク＝Unity 側コンパイルゲート | **PR だけ**（D-JOBBPR） |

1. 作業ブランチを push する。
2. GitHub MCP `actions_run_trigger`（`method=run_workflow` / `workflow_id=quality.yml` / `ref=`そのブランチ）で起動する。
   **`inputs` は渡さない**（入力を持たないワークフローに渡すと 422 で拒まれる）。
3. `actions_list`（`list_workflow_jobs`）と `get_job_logs` で結果を読む。

- **手動実行で回るのは Job A だけ**（D-JOBBPR）。Job B は PR を開いている間に `pull_request` トリガで回る。
- **②では検証とコミットの順序が①と逆**になる（コミット → push → CI 確認 →〔赤なら修正コミット〕）。
- **Unity 側に触れた変更は PR の Job B の緑を確かめてからマージする**。対象は `Assets/` 配下の `.cs`・`.asmdef`・`.shader` 等と
  `Packages/`・`ProjectSettings/` の変更で、**Core も含む**（Core の公開 API を変えると、Job A がコンパイルしない RunTime が壊れうる）。
- **Job B は Claude が監視しない**（D-AGENTOPS）。完了まで 7〜15 分と不定で、ポーリングはトークンの浪費になる。Job B の緑は
  **ユーザーが GitHub 上で目視で確かめる**。Claude は push と quality.yml の起動までを行い、Job A の結果だけを読む。Unity 側に
  触れた段も Job A の緑を見て次の段へ進んでよく、段ごとにコミットを分けて push しておく（PR の Job B が赤ならユーザーの報告と
  ログのコンパイルエラーから直す）。
- **生成物の再生成は `regenerate.yml`（D-REGEN）**。生成元を変えたコミットは、**メッセージに `[regen]` を含めて `claude/**` ブランチへ
  push する**。CI が生成物を書き出し、差分を同じブランチへコミットして push する。完了を待って `git pull` してから次を積み、
  そのうえで quality.yml を回す（bot の push では quality.yml が自動起動しない）。

### 検証ステータスの置き場は PR 本文のみ（D-ENV3）

環境制約で実行できなかった検証（NUnit / コンパイル / Play Mode）は、**PR 本文の「未実施検証」チェックリストにのみ**書く。
**`docs/` 配下（roadmap・plans・architecture・decisions を含む）へ「ユーザー検証待ち」等の一時ステータスを書かない**
（マージ後に更新されず陳腐化する）。decisions で触れる必要があれば「検証は PR のチェックリスト参照」とだけ書く。

## 開発ワークフロー

- `docs/roadmap.md` の順序（「now / next / later」と「着手候補」表）に従って実装する。仕様が曖昧であったり、より望ましい物が
  ありうる場合は、ユーザーに詳細化を促すか対話形式で設計を詰め、詳細化した設計は `docs/plans/` に置く。
- 機能追加時には対応する NUnit テストと、Play Mode テスト用のコードを作る（置き場は `testing-workflow.md`）。
- 開発はステップ単位で進める。作業の開始時にブランチを作り、1 ステップごとにコンパイルと NUnit テストの全通過を
  確認してからコミットし、次のステップへ進む。すべて完了したら、ユーザーに Play Mode テストの全通過確認とマージを促す。
- **ドキュメント還元は PR の「ドキュメント還元チェックリスト」**（`.github/pull_request_template.md`）で同じ PR のうちに
  済ませる。とりわけ**完了項目の roadmap からの退避を後回しにしない**。
- 設計判断は当月ファイル `docs/archive/decisions/decisions-YYYY-MM.md` の先頭へ追記し、索引 `docs/archive/decisions.md` に 1 行足す。
  規則の正典は `../README.md`「ドキュメント運用ルール」。

## サブエージェントとトークン消費（D-AGENTOPS）

- **サブエージェント（Agent ツール）は原則使わない**（ユーザーが明示したときだけ）。サブエージェントは CLAUDE.md と docs を読み直す
  冷えた状態から始まるので、1 体ごとに主セッションと同じ下読みの分を払う（数体を並列に出すと短期間で利用上限に達する）。
- 調査は主セッションで絞って読む: 文書は見出し（`grep -n "^#"`）で位置を取ってから要る節だけを `sed -n` で、コードは `grep -n` で
  位置を取ってから範囲を読む。ファイル全体を読むのは数百行までのものに限る。
- 使う場合は 1 体・結論だけを返させる指示にし、並列の fan-out をしない。サブエージェントを起こすスキル（`/code-review` 等）も低い労力で回す。

## Play Mode テストのコンパイル整合性

**インターフェース・公開 API・クラスを変更する際は必ず** `grep -rn "変更した名前" Assets/Tests/` で使用箇所を探し、
**インライン定義の Fake・Stub も同一ステップ内で直す**。Unity 側のテストは Job A でコンパイルされないので、直し漏れは PR の Job B まで見えない。
共有 Fake は `Assets/Tests/Shared/`・`Assets/Tests/CoreSupport/`、テスト固有の Fake はテストファイル内にある（`testing-workflow.md`）。

## コードスタイル

- `.editorconfig` の規約に準拠する（強制手段は `code-quality.md`）。
- **`dotnet format` は whitespace 限定で使う**（フルの `dotnet format` はリンクした `Assets/` のファイルから using を消して Unity を壊す）。
- **`csc.rsp` にコメント行を書かない**（Unity のコンパイルが失敗する）。
- 本番 API に省略可能引数（とくに `= null`）を新設しない。既定値が要るのがテストだけなら共有テスト支援へ置く（D-ARGOPT）。
- ファイル名・フォルダ名の変更は、Unity Editor 上で meta ファイルの参照が壊れる危険性がある場合、ユーザーに操作を促す
  （`.meta` を一緒に動かさないと GUID が変わり、参照が外れる）。

## コードコメント規約

コメントは充実させる方針を維持しつつ、**作業ログのノイズ化を防ぐ**:

- **新規コメントは現在形で書く**。「何であるか」「なぜそうでなければならないか（破ると何が壊れるか）」のみを書き、
  旧実装との差分・変更経緯はコミットメッセージと decisions に書く。
- **出自参照は ID 1 個の括弧書き**（例: `（D-DOCDUP）`）。日付・レビュー項目番号・「〜で変更」を重ね書きしない。
- **禁止パターン**: ①削除済みコードの行番号・位置への参照 ②「旧」「かつては」で始まる変更履歴ナラティブ（現在の形の
  **理由**を説明する場合を除く） ③同一内容への日付併記 2 回以上。
- **既存コメントは一括清掃しない**。編集で触ったファイル内の禁止パターンだけをその場で刈る。
- Unity の挙動・ライフサイクル制約など、コードから読み取れない普遍的な制約の説明は最良のコメントであり、積極的に維持・追加する。

## Git コミット（シェル構文の注意）

実行環境によりシェルが異なる（ローカル Windows は Bash / PowerShell 両方、リモートは Linux の Bash）。**使用前に
どのシェルかを確認する**こと。

- **Bash では PowerShell の here-string `git commit -m @'...'@` を絶対に使わない**（`@` がメッセージ先頭にリテラル混入する）。
  複数行は `git commit -m "件名" -m "本文段落"`（段落区切り）か、POSIX here-doc `git commit -F - <<'EOF'` … `EOF`。
- PowerShell の here-string は **PowerShell ツールでのみ**使う（閉じ `'@` は行頭・列 0）。
- コミット後は `git log -1 --format='%s'` で件名を確認し、意図しない先頭文字の混入が無いか検証する。
- 移動だけ・整形だけのコミットは `.git-blame-ignore-revs` に SHA を足す（`code-quality.md`「移動だけのコミットと blame」）。
