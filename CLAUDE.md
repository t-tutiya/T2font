# CLAUDE.md

このファイルは、Claude Code (claude.ai/code) がこのリポジトリのコードを扱う際のガイダンスを提供します。
**プロジェクト固有の規約の正典**であり、汎用の作業規約は下で読み込む `docs/guides/working-agreement.md` が持つ（D-TEMPLATE）。
アーキテクチャは **1 項目 1〜2 行＋参照先**だけを置く（要約は正典と二重になる・D-DOCDUP）。

# 作業規約（汎用・テンプレート由来）

@docs/guides/working-agreement.md

プロジェクトで上書きする規約があれば、ここに「working-agreement の〜は本プロジェクトでは〜とする」と書く
（`docs/guides/working-agreement.md` 自体は書き換えない。テンプレートの改善を差し替えで取り込めるようにするため・`docs/guides/template-sync.md`）。

## 本プロジェクトでの上書き

- **working-agreement の「コードコメント規約」の出自参照は、パッケージ `Packages/info.someiyoshino.t2font/` の中では「パッケージの README の節」
  （例: `（README §5）`）とする**。決定 ID と `docs/` のパスはパッケージの中に書かない——パッケージは他のリポジトリへフォルダごと写され、
  写した先に本リポジトリの文書は無い（D-T2FONT）。パッケージの外（`Assets/`・`Tests/`・`docs/`）は working-agreement のまま。
  `Tests/PackageIntegrityTests.cs` が検査する。
- **用語は `docs/reference/terminology.md` の訳語に従う**（D-TERMS）。コード内コメント・例外とログの文面・テストの名前・文書・パッケージの README の
  すべてに当てはまる。一般的でない訳語を避け、カタカナ英語か「カタカナ英語＋する」を使う。新しい用語は表に足してから使う。
- **パッケージの契約を変えたら、パッケージの README を同じコミットで直す**（README が契約の正典）。同じ入力から出る生成物が変わる変更は、
  ベイカーのバージョンを上げる（README §7.5）。利用側へ出す変更は `Packages/info.someiyoshino.t2font/CHANGELOG.md` に書き、`package.json` の版を上げる。

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

Unity 6 プロジェクト「T2font」。成果物は **Unity uGUI の文字描画のためのパッケージ T2Font**（`Packages/info.someiyoshino.t2font/`・MIT）で、
フォントアセットを「タイポグラフィテーブル」（送り幅・インク境界など、描き方によらず同じもの）と差し替えられる「グリフの表現」（SDF など）に分け、表現を足しても
字を置く側のコードが変わらない API を提供する。本リポジトリはパッケージの正本と、それを検証する Unity プロジェクトを兼ねる（D-T2FONT）。
契約の正典はパッケージの README。

## ビルド・実行

Unity Editor で開き、Play Mode で実行する。ソースは `Assets/`（検証用のプロジェクト）と `Packages/info.someiyoshino.t2font/`（成果物のパッケージ）。
Unity の版は `ProjectSettings/ProjectVersion.txt` が正典（LTS の最新パッチに合わせ、LTS でないマイナー版へは出荷まで上げない・D-UNITYVER）。
開発に使う全 PC と CI を同じ版に揃える。

# アーキテクチャ概要（索引）

要約は置かない。各項目の正典は括弧内。

- **アセンブリと 2 つのコンパイル世界**（`docs/architecture/01-assemblies-and-tests.md`・一覧は `docs/generated/assembly-reference.md`）:
  Unity 非依存のロジックは Core（`Assets/T2font.Core/`）に置き、外部 NUnit で検査する。
- **パッケージ T2Font**（パッケージの README・`docs/architecture/01-assemblies-and-tests.md`「パッケージ T2Font」・D-T2FONT）: Core と Bake は
  純 C# で外部 NUnit がリンクする。Runtime・Editor・Viewer は PR の Job B が見る。
- **TrueType から SDF アトラスまで**（`docs/architecture/02-truetype-to-atlas.html`・契約はパッケージの README §7・§9）: 読み取り・タイポグラフィテーブル・ベイクの工程の図解。
  ベイカーの出力を変えたら図が古くなる（図の作り方と出力のハッシュは同書の末尾）。
- **アトラスから画面まで**（`docs/architecture/03-atlas-to-screen.html`・契約はパッケージの README §4・§5）: ホストが字を置き、CanvasRenderer と Canvas を経て
  シェーダがレンダーターゲットへ描き、画面に出るまでの図解。Runtime・シェーダ・ベイカーの出力を変えたら `Tools/docs-figures/03-atlas-to-screen/` で図を作り直す。
- **ベイク仕様の書き方**（`docs/architecture/04-bake-spec.html`・契約はパッケージの README §7・§9）: `.t2font` のキー・アセットを作る手順・失敗の文面・文字セットの中身。
  文字セット・ベイク仕様の読み取り・ベイカーの文面を変えたら `Tools/docs-figures/04-bake-spec/` で表と付録を作り直す。
- **テストの三層**（`docs/guides/testing-workflow.md`・D-TESTARCH）: 外部 NUnit／EditMode／Play Mode。安い層で表現できるものは安い層へ置く。
