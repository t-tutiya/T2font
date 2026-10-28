# テンプレートとの同期

本プロジェクトは GitHub のテンプレートリポジトリ **unity-project-template** から作られている（D-TEMPLATE）。
本書は、テンプレートとプロジェクトの間で汎用の規約・仕組みの改善をやり取りする手順を持つ。

> 本書は `Tools/init_project.py` の置換の対象外で、テンプレートの中の綴り `TemplateProject` をそのまま書いている。

## 前提

- **汎用の規約・仕組みの正典はテンプレート**である。どのプロジェクトで改善しても、テンプレートへ戻して初めて次のプロジェクトへ届く。
- 「Use this template」で作ったリポジトリは**テンプレートと git の履歴を共有しない**（1 コミットから始まる）。したがって `git merge` では
  取り込めず、**ファイル単位で差し替える**。差し替えやすいように、汎用の規約は `CLAUDE.md` から切り離して `docs/guides/working-agreement.md` に置いてある。
- テンプレートの中ではプロジェクト名を `TemplateProject` と書いている。テンプレートからファイルを持ってくるときは、この綴りを自分の
  プロジェクト名へ置換してから差し替える（`init_project.py` が初期化で行ったのと同じ置換）。

## テンプレートが管理するファイル

下表のファイルは、プロジェクト側では**原則として書き換えない**（プロジェクト固有の事情は `CLAUDE.md` に上書きとして書く）。
「混ぜる」とあるものは、プロジェクトが足した行（除外パターン・ベースライン・キャッシュの鍵など）があれば、差し替えの後に戻す。

| ファイル | 取り込み方 |
|---|---|
| `docs/guides/working-agreement.md`・`docs/guides/template-sync.md` | 差し替える |
| `docs/guides/code-quality.md`・`docs/guides/testing-workflow.md` | 差し替える（プロジェクト固有の節を足していたら混ぜる） |
| `.claude/hooks/session-start.sh`・`.github/workflows/regenerate.yml` | 差し替える |
| `.github/workflows/quality.yml` | 混ぜる（Library キャッシュの鍵を足していたら戻す） |
| `.github/pull_request_template.md` | 混ぜる（プロジェクト固有のチェック項目を足していたら戻す） |
| `.editorconfig`・`.gitattributes`・`.editorconfig-checker.json` | 混ぜる（除外パターンを足していたら戻す） |
| `Tests/DocsReferenceIntegrityTests.cs` | 混ぜる（ベースライン・走査する拡張子を足していたら戻す） |
| `Tests/DecisionsIndexIntegrityTests.cs`・`Tests/DocsIndexCoverageTests.cs`・`Tests/CoreNullableCoverageTests.cs`・`Tests/GeneratedGolden.cs`・`Tests/RepoRoot.cs` | 差し替える |
| `Tools/verify-unity-compile.ps1` | 差し替える |

## テンプレートからプロジェクトへ取り込む

1. テンプレートの `docs/archive/decisions.md` を開き、前回の取り込みより後に足された決定を確かめる（何が変わったかの正典）。
2. 上表のうち変わったファイルを、`TemplateProject` を置換したうえで差し替える。
3. 取り込んだ決定を、プロジェクトの当月の decisions に「テンプレートから継承（決定 ID は共通）」として追記し、索引に 1 行足す。
4. PR を作り、CI（Job A・Job B）の緑を確かめてからマージする。

## プロジェクトからテンプレートへ戻す

1. プロジェクトで汎用の規約・仕組みを改善したら、その PR とは別に、テンプレートへ同じ変更を入れる PR を作る。
   プロジェクト名は `TemplateProject` へ置換し戻す。
2. テンプレートの decisions に決定を足す（プロジェクト側と同じ決定 ID を使う）。
3. テンプレートの CI（Job A）が緑であることを確かめる。

**T2Engine（テンプレートの元になったプロジェクト）**はテンプレートから作られていないので、T2Engine で汎用の規約を改善したときも
同じ手順で手作業で移す。T2Engine の文書には ADV 固有の内容が混ざっているので、汎用の部分だけを持ってくる。
