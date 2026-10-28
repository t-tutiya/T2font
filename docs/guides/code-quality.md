# コード品質の強制（format / csc.rsp / CI）

`.editorconfig` に定めた規約を **ツールで実効的に強制** するための手段と、その落とし穴をまとめる。
テストの置き場と実行手順は `testing-workflow.md`、検証経路の使い分けは `working-agreement.md`「実行環境」。

---

## 2 つのコンパイル世界と、その帰結

本プロジェクトのコードは Unity（asmdef）と外部 SDK csproj（`Tests/TemplateProject.Tests.csproj`）の 2 つから見える
（構造の正典は `../architecture/01-assemblies-and-tests.md`）。品質の強制手段は、この非対称の上に組み立てる。

- **外部 csproj は本物の MSBuild プロジェクト**なので `dotnet format`・`dotnet test`・アナライザを即座に効かせられる。ただし対象は
  csproj が**リンクした Unity 非依存のファイル**（Core と共有テスト支援）だけである。
- **Unity 側のコード全体**は `dotnet` ツールでは網羅できない。ここは (a) IDE（Rider / Visual Studio）、(b) 全ファイル横断の
  editorconfig-checker（空白・改行・文字コードのみ）、(c) CI の Job B（Unity のコンパイル）で押さえる。

### nullable を両方の世界で揃える（D-NRTCORE）

Core は両方の世界からコンパイルされるので、nullable の**文脈**と**強度**の両方を揃える。

| 置き場 | 内容 | 意図 |
|---|---|---|
| Core の各 `.cs` の先頭 | `#nullable enable` | ファイル単体で意図が読める。IDE の csproj 生成が `csc.rsp` を反映しない場合にも効く。網羅は `Tests/CoreNullableCoverageTests.cs` が固定する |
| `Tests/TemplateProject.Tests.csproj` | `<Nullable>enable</Nullable>` ＋ `<WarningsAsErrors>Nullable</WarningsAsErrors>` | 外部 NUnit 側の強度 |
| `Assets/TemplateProject.Core/csc.rsp`・`Assets/Tests/CoreSupport/csc.rsp` | `-nullable:enable` ＋ nullable 診断の `-warnaserror+` | 外部 csproj からもコンパイルされる 2 アセンブリを csproj 側と同じ強度に揃える |
| `Assets/RunTime/csc.rsp`・`Assets/Editor/csc.rsp` | 上と同じ | RunTime と Editor は Job A でコンパイルされないので、null の誤りを Job B で確実に止める |

片側だけで設定すると「Unity では無警告で書けて、`dotnet test` で初めて落ちる」食い違いが生まれ、開発者が常には回せない側でしか
顕在化しない。**ソースに書けるもの（ディレクティブ）はソースに書くのが最も確実**である。

> **⚠️ `csc.rsp` にコメント行を書かないこと**。`#` 始まりの行を入れると **Unity のコンパイルが失敗する**（Roslyn の応答ファイルは
> コメントを解釈するが、Unity が `csc.rsp` を渡す経路はそうではない）。失敗の出方が判りにくい——Unity は起動して
> `Unexpected exit code 1` で終わり、結果 XML を出さないので、CI ログ上は環境起因の失敗と見分けがつかない。**したがって `csc.rsp` は
> オプション行だけを置き、「なぜそのオプションなのか」は本節に書く**。原因の特定はローカルの `pwsh Tools/verify-unity-compile.ps1` が早い。

> **警告の件数は Job B のログからは数えられない**——Unity のバッチログはコンパイルエラーだけを出す。数えたいときは該当の診断を一時的に
> `-warnaserror+` へ入れ、Job B のエラーとして読む。

---

## `dotnet format` は whitespace 限定

```bash
# 検査（CI / pre-commit 向け・差分があれば非ゼロ終了）。using も意味論も触らない
dotnet format whitespace Tests/TemplateProject.Tests.csproj --verify-no-changes

# ローカル整形（空白・改行・インデントのみ書き換え）
dotnet format whitespace Tests/TemplateProject.Tests.csproj
```

### ⚠️ 引数なしの `dotnet format` は必要な using を消し、Unity を壊す

`dotnet format` はソースを **Unity ではなく外部 csproj のコンパイル文脈**で評価する。

1. csproj は `<ImplicitUsings>enable</ImplicitUsings>` を持ち、.NET SDK が `System.Collections.Generic` 等を**グローバル using として自動注入**する。
2. その結果、**この csproj の視点では**ファイル先頭の `using System.Collections.Generic;` は「冗長」と判定される。
3. `style`/`analyzers` 段が不要 using の削除を**修正適用**する。対象は**リンク**なので、実体の `Assets/...` がそのまま書き換わる。
4. **Unity はグローバル using を注入しないで同じファイルをコンパイルする**ので、削除された using が必須になり CS0246 で落ちる。

**結論: この csproj 経由の意味論的修正は、共有の実体ファイルに対して本質的に安全でない**。共有ファイルへの整形は `whitespace` 限定を
厳守し、不要 using・命名などの意味論的な品質は IDE（＋ Microsoft.Unity.Analyzers）に委ねる。`whitespace` は空白系だけを扱い、
using 削除・コード書き換えを一切行わない。

---

## CI（GitHub Actions）

`.github/workflows/quality.yml` に 2 ジョブを持つ。**CI は PR ゲートであると同時に、リモートセッションにとっての検証経路でもある**
（検証経路③・D-ENV4。手順の正典は `working-agreement.md`）。書き出し側は別ワークフロー `.github/workflows/regenerate.yml`（D-REGEN）。

### Job A: lint & unit（Unity ライセンス不要・高速・無料）

`dotnet format whitespace --verify-no-changes` → `dotnet test` → `editorconfig-checker` の 3 段で、どれもブロッキング。
PR・`main` への push・手動実行（`workflow_dispatch`）で回る。

- `editorconfig-checker/action-editorconfig-checker` は **ec を PATH に入れるだけ**のセットアップ専用で、検査は別の `run: editorconfig-checker` が行う。
- editorconfig-checker は言語非依存で、`Assets/` を含む全ファイルの charset・改行・final newline・行末空白を検査する。
  `.editorconfig-checker.json` で indent 検査を無効化し（Markdown のネスト・Unity 生成の 2 スペース YAML・シェーダのタブで大量の誤検出が出る）、
  Unity が書き戻す生成テキスト資産（`.asset`・`.meta`・`.asmdef`・`ProjectSettings/` 等）を除外している。**ベンダー製の資産や生成物の置き場が
  増えて誤検出が出たら、その置き場を `Exclude` に足す**。

### Job B: Unity コンパイルチェック（GameCI・ライセンス必要）

[GameCI](https://game.ci/) の `unity-test-runner` で **EditMode テストのみ**を実行する。EditMode の実行には全 asmdef のコンパイル成功が
前提なので、スモークテスト（`Assets/Tests/EditMode/CompilationSmokeTests.cs`）が 1 件走ることがそのままコンパイルゲートになる。
**PR でだけ回る**（D-JOBBPR）。

**有効化手順（リポジトリごとに 1 回）**:

1. [GameCI Activation](https://game.ci/docs/github/activation) の手順で Personal ライセンスの `.ulf` を取得する。
2. リポジトリの Settings → Secrets and variables → Actions に `UNITY_LICENSE`（`.ulf` の中身）・`UNITY_EMAIL`・`UNITY_PASSWORD` を登録する。
3. 以降の PR で `unity-compile` ジョブが実行される。**secret 未登録の間はジョブが自動スキップ（成功扱い）**される。

**留意点**:

- **EditMode に固定している**: `-nographics` 環境では GPU を要する Play Mode テストが `RenderTexture.Create failed` で偽陽性の失敗になる。
  Play Mode まで広げる場合は GPU 系のテストにカテゴリ属性を付けて除外する。
- **Library をキャッシュする**: 鍵は `packages-lock.json` と `ProjectVersion.txt`。取り込みの重い独自の資産（自前のインポータとその元データ等）を
  足したら、それらも鍵の `hashFiles` に足す（足さないと、変えた後は毎回取り込み直す）。
- Unity のバージョンは `ProjectSettings/ProjectVersion.txt` から自動検出される（版上げで workflow の修正は要らない・D-UNITYVER）。

---

## ローカルの即時フィードバック（pre-commit フック・任意）

CI を待たずにコミット前に弾きたいときは、リポジトリ管理下のフックを使う（クローンしただけでは有効にならない点に注意）。

```bash
# .githooks/pre-commit （実行権限を付与し、git config core.hooksPath .githooks を設定）
#!/bin/sh
dotnet format whitespace Tests/TemplateProject.Tests.csproj --verify-no-changes || {
  echo "空白整形違反。'dotnet format whitespace Tests/TemplateProject.Tests.csproj' で修正してください"; exit 1; }
```

CI が最終ゲート、フックは補助という位置づけにする。

---

## 移動だけのコミットと blame（`.git-blame-ignore-revs`）

大きいファイルを**移動だけ**で分けたコミットや、**整形だけ**のコミットは、ルートの `.git-blame-ignore-revs` に SHA を 1 行ずつ登録する。
GitHub の blame はこのファイルを読んで、その変更を飛ばした元の行の出自を出す。ローカルで同じにするには
`git config blame.ignoreRevsFile .git-blame-ignore-revs` を 1 度だけ設定する。登録するのは本文を変えないコミットだけで、SHA は
そのコミットの後に別のコミットで足す（自分の SHA は自分の中に書けない）。

---

## 生成物の改行は LF 固定（D-GENEOL）

**規約**: リポジトリへコミットされる生成物のテキストは、**生成器の側で LF に固定する**。`.editorconfig` の `end_of_line = lf` と同じ規約を、
ファイルではなく**生成器の出力**に適用する。

**なぜ生成器の側で守るのか**: `StringBuilder.AppendLine`・Newtonsoft の `Formatting.Indented`・Python のテキストモード書き込みは、
いずれも OS の改行を使う。生成物は LF でコミットされているので、Windows で再生成すると内容が 1 文字も変わっていなくても作業ツリーが汚れる:

| 見え方 | 理由 |
|---|---|
| `git status` は `M` を出す | CRLF でファイルサイズが変わり stat 比較が外れる |
| `git diff` は空 | `.gitattributes` の `* text=auto eol=lf` が比較前に改行を畳む |
| `git add` してもインデックスは変わらない | 同じ理由で blob は LF のまま |
| ローカルの `editorconfig-checker` は落ちる | 作業ツリーの実体が `end_of_line = lf` に反する |

CI は毎回クリーンなチェックアウト（＝常に LF）なので、この退行を**ファイル側の検査では原理的に捕まえられない**。

**書き方**:

| 言語・経路 | 使うもの |
|---|---|
| C#・`StringBuilder` | `GeneratedText.AppendLineLf`（`AppendLine` は使わない） |
| C#・Newtonsoft `Formatting.Indented` | `GeneratedText.NormalizeNewlines(...)` で畳んでから書く |
| Python | `open(path, "w", encoding="utf-8", newline="")` |

**ガード**: 生成器を足したら、その生成器の**戻り値**に CR が含まれないことを検査するテストを足す（golden テスト `GeneratedGolden` は
改行を正規化して比較するので、この退行を検出できない）。

**対象外**: コンソール出力・Editor ダイアログ・診断ダンプ・セーブデータのように**コミットされない**テキストは OS 既定の改行のままでよい。
