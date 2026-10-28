# CLAUDE.md

このファイルは、Claude Code (claude.ai/code) がこのリポジトリのコードを扱う際のガイダンスを提供します。
**プロジェクト固有の規約の正典**であり、汎用の作業規約は下で読み込む `docs/guides/working-agreement.md` が持つ（D-TEMPLATE）。
アーキテクチャは **1 項目 1〜2 行＋参照先**だけを置く（要約は正典と二重になる・D-DOCDUP）。

# 作業規約（汎用・テンプレート由来）

@docs/guides/working-agreement.md

プロジェクトで上書きする規約があれば、ここに「working-agreement の〜は本プロジェクトでは〜とする」と書く
（`docs/guides/working-agreement.md` 自体は書き換えない。テンプレートの改善を差し替えで取り込めるようにするため・`docs/guides/template-sync.md`）。

# ドキュメント

**全ドキュメントの索引は [`docs/README.md`](docs/README.md)**（運用ルールの正典は同書「ドキュメント運用ルール」）。
変更を加えた際は常に最新の状態に保つ。主要な正典:

- **フォルダは記述の軸で分かれる（D-DOCTREE）**: `docs/reference/`（どう書くか）・`docs/generated/`（自動生成・**手で編集しない**）・
  `docs/architecture/`（どうなっているか）・`docs/guides/`（どう作業するか）・`docs/plans/`・`docs/archive/`。移設してもファイル名は変えない。
- `docs/architecture/`: アーキテクチャ解説（索引は `docs/architecture/README.md`）。構造や動作が変わったら該当章を更新する。
- `docs/roadmap.md`: 現在地と今後（着手・完了した際は状態を更新する）。`docs/plans/`: これから作る物の設計正典（着手時に参照）。
- `docs/guides/testing-workflow.md`（テストの置き場と実行手順）／`docs/guides/code-quality.md`（`dotnet format`・`csc.rsp`・CI）。
- `docs/archive/`: 完了・凍結・歴史専用。**設計判断ログ `docs/archive/decisions.md`（「なぜ」の正典・決定 ID で追跡）以外は通常参照不要**。

# プロジェクト概要

Unity 6 プロジェクト「TemplateProject」。

（何を作るか・目標・コアコンセプトを 2〜5 行で書く。詳細は `docs/architecture/` へ置き、ここには要約を置かない。）

## ビルド・実行

Unity Editor で開き、Play Mode で実行する。ソースはすべて `Assets/` 以下。
Unity の版は `ProjectSettings/ProjectVersion.txt` が正典（LTS の最新パッチに合わせ、LTS でないマイナー版へは出荷まで上げない・D-UNITYVER）。
開発に使う全 PC と CI を同じ版に揃える。

# アーキテクチャ概要（索引）

要約は置かない。各項目の正典は括弧内。

- **アセンブリと 2 つのコンパイル世界**（`docs/architecture/01-assemblies-and-tests.md`・一覧は `docs/generated/assembly-reference.md`）:
  Unity 非依存のロジックは Core（`Assets/TemplateProject.Core/`）に置き、外部 NUnit で検査する。
- **テストの三層**（`docs/guides/testing-workflow.md`・D-TESTARCH）: 外部 NUnit／EditMode／Play Mode。安い層で表現できるものは安い層へ置く。
