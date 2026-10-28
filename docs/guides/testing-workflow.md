# テスト作業フロー

テストは**三層**で運用する（安い層優先・D-TESTARCH）。アセンブリの構成は `../architecture/01-assemblies-and-tests.md`。

| 層 | 場所 | 実行方法 | Unity | 対象 |
|------|------|---------|------|------|
| 外部 NUnit | `Tests/`（net8.0） | `dotnet test` | 不要（最速） | 純 Core ロジック（UnityEngine 型ゼロ）・文書とソースの機械検査 |
| EditMode | `Assets/Tests/EditMode/` | Unity Test Runner（EditMode タブ） | 必要（Play 遷移なし・中速） | Unity 型を使う同期ロジック（`.Update()` 手動駆動・自動ライフサイクル非依存） |
| Play Mode | `Assets/Tests/PlayMode/` | Unity Test Runner（PlayMode タブ） | 必要（フレームループ・低速） | `[UnityTest]`・自動ライフサイクル・実描画/実ブート |

加えて、実装直後にコンパイルが通るかをヘッドレス（Unity batchmode）で検証する手段がある（下記「ヘッドレスコンパイル検証」）。

> **実行環境ごとの可否**: 各環境での実行可否と判定方法は `working-agreement.md`「実行環境（3 環境運用）」を参照。リモートセッションのコンテナ内では Unity 系検証は常に不可だが、**ブランチを push して CI を手動実行すれば NUnit を回せ、PR を開けば Unity 側コンパイルも回る**（検証経路③・D-ENV4）。実行できなかった検証は PR 本文のチェックリストにのみ記載する（D-ENV3）。

---

## テスト配置の正典（三層 ＋ (a)/(b)・D-TESTARCH）

**層はコスト（速さ）と依存で決める。安い層で表現できるものは安い層へ置く**。

### 判定基準（上から評価し、最初に該当した層へ置く）

1. **実フレームループが要るか？** `[UnityTest]` コルーチン／`yield return null` で `Awake`/`Start`/`Update` を自動発火させる／実描画・present／手動駆動できない時間進行 → **Play Mode**。
2. **UnityEngine 型を使うか？**（`Vector2`/`AnimationCurve`/`GameObject`/`Material`/`Texture` 等）かつフレームループ不要 → **EditMode**。
3. **それ以外（UnityEngine 型を一切使わない純 Core）** → **外部 NUnit `Tests/`**。

> **EditMode の追加制約＝`Object.Destroy` 不可**: edit mode では `Object.Destroy` が呼べない（Test Runner はエラーとして失敗扱いにする）。
> テストの片付けを `DestroyImmediate` にしても、GameObject の破棄が本番コードの `OnDestroy` を同期で呼び、そこで `Object.Destroy` が
> 呼ばれれば避けられない。**Unity オブジェクトを生成し、その破棄が本番の破棄経路を通るテストは Play Mode に置く**。テストの都合で
> 本番コードの破棄を書き換えない。

> **意図的にエラーログを出すテストの作法**: `[Explicit]` は Unity Test Runner の全体実行を止めない。意図的に LogError を出すテストは、
> テスト本体の先頭で `LogAssert.ignoreFailingMessages = true` を立てる（`[TearDown]` で戻さない。UTF がテストごとに既定値へ戻す）。
> 役目を終えた計測用のテストは、スクリプティング定義シンボルでコンパイルごと囲う（全体実行のたびに赤いログが出続けると、本物のエラーを見落とす）。

### Play Mode 内の (a)/(b)（フォルダで可視化）

- **(a) 統合 ＝ `Assets/Tests/PlayMode/Integration/`**: 実ブート／実アセットを**正典のローダ経由**で読む。**実データの読み取りはここだけ**。
  アサートは**配線・キー**（「ブートで〜が空でない」「予約 ID が解決する」）にし、変動しうる本番の値は assert しない。
- **(b) コンポーネント ＝ `Assets/Tests/PlayMode/<要素>/`**: Fake だけを使い、実データを読まない。実データを読みたくなったら (a) へ移す。
  名前空間は `TemplateProject.PlayModeTests.<要素>`。

### 共有テスト支援（2 つのアセンブリ）

| 置き場 | asmdef | 置くもの | 使う側 |
|---|---|---|---|
| `Assets/Tests/CoreSupport/` | `TemplateProject.CoreTestSupport`（`noEngineReferences`） | Core にだけ依存する Fake・テスト用の組み立て関数 | 外部 NUnit（csproj がリンク）と Unity 側（asmdef 参照）の両方 |
| `Assets/Tests/Shared/` | `TemplateProject.TestSupport` | Unity 側だけで使う共有 Fake | EditMode・PlayMode |

- 本番 API の省略可能引数を避けるため、テストでだけ要る既定値は CoreSupport の組み立て関数が持つ（D-ARGOPT）。
- インターフェースの替え玉は**基底スタブ 1 つから派生させる**（既定は全呼び出しを `NotSupportedException` で拒否し、テストは使う分だけ override する）。
  インターフェースにメンバを足したとき、追随が要るのは基底スタブだけで済む。

### 規約（再発防止・全層共通）

- **R1 逸脱アルゴリズム禁止**: テストダブル（Fake/Stub）は Core のアルゴリズムを**再実装しない**。Core のロジックが要るなら Core を呼ぶ。
- **R2 Fake の単一正典**: 複数のファイルで使う Fake は**各 1 個**——両方の世界で使う Core 依存だけの Fake は `CoreSupport/`、Unity 側だけのものは `Shared/`。
  インライン Fake は 1 ファイル専用のときだけ。**世界をまたいでも**同じ目的の重複を作らない。
- **R3 実データは (a) のみ**: 実アセットの読み取りは (a) `Integration/` に限定し、正典のローダ経由で読む。

---

## ヘッドレスコンパイル検証（ローカル PC・実装直後の最初のチェック）

Unity Editor を開く前に**コンパイルが通るか**をヘッドレス（batchmode）で検証する。スクリプトは `Tools/verify-unity-compile.ps1`。

| 項目 | 内容 |
|------|------|
| PowerShell | 7 以上（`pwsh`） |
| Unity Editor | **同一プロジェクトを開いていないこと**（Library ロック競合のため） |
| Unity 実行ファイル | `ProjectSettings/ProjectVersion.txt` から自動解決（既定の Unity Hub パス）。`-UnityPath` で明示も可 |

```powershell
# 本体＋全テストコード（PlayMode テストアセンブリ含む）のコンパイル検証（高速・コンパイルのみ）
pwsh Tools/verify-unity-compile.ps1

# テストも実際に実行する
pwsh Tools/verify-unity-compile.ps1 -RunTests
```

| 終了コード | 意味 |
|------|------|
| 0 | コンパイル成功（`-RunTests` 時はテストも全通過） |
| 1 | コンパイルエラー（ログから該当行を抽出表示） |
| 2 | テスト失敗（`-RunTests` 時のみ） |
| 3 | 実行エラー（Unity 未検出 / Editor 起動中 / タイムアウト 等） |

ログと結果 XML は `Logs/headless-compile/`（gitignore 済み）に出力される。

> **`-nographics` の既知制約**: `-RunTests` で Play Mode を流すと、GPU を要するテストが `RenderTexture.Create failed` で偽陽性の失敗に
> なることがある。最終確認は GUI のある Editor の Test Runner で行う。CI の Job B が EditMode に固定しているのも同じ理由（`code-quality.md`）。

---

## 外部 NUnit テスト（`Tests/`）

```bash
# 全件
dotnet test Tests/TemplateProject.Tests.csproj

# 特定のテストだけ
dotnet test Tests/TemplateProject.Tests.csproj --filter "FullyQualifiedName~GeneratedTextTests"

# 生成物を書き出す（golden テストが検査する生成物を作り直す・D-REGEN）
UPDATE_GENERATED=1 dotnet test Tests/TemplateProject.Tests.csproj
```

PowerShell では `$env:UPDATE_GENERATED = "1"; dotnet test Tests/TemplateProject.Tests.csproj; Remove-Item Env:UPDATE_GENERATED`。

### 最初から入っている検査

| テスト | 検査すること |
|---|---|
| `Tests/CoreNullableCoverageTests.cs` | Core の全 `.cs` が `#nullable enable` を持つ（D-NRTCORE） |
| `Tests/DocsReferenceIntegrityTests.cs` | 文書のリンク・バッククォートの `.md` パス・コードが名指す `docs/…` が実在する（D-DOCTREE） |
| `Tests/DecisionsIndexIntegrityTests.cs` | 設計判断ログの索引と月別本文の整合（D-DOCLIFE） |
| `Tests/DocsIndexCoverageTests.cs` | 完了記録・`plans/`・`archive/` の索引がフォルダの実体を覆う（D-DOCLIFE・D-CRMONTH） |
| `Tests/AssemblyReferenceDocTests.cs` | 生成物 `docs/generated/assembly-reference.md` が asmdef と一致する（生成器の見本・D-REGEN） |
| `Tests/GeneratedTextTests.cs` | 生成物の改行規約（D-GENEOL）。Core を外部 NUnit で検査する基本形の見本 |

### NUnit テストを追加する手順

1. Core（`Assets/TemplateProject.Core/`）にロジックを書く。新しいファイルは先頭に `#nullable enable` を置く。
2. `Tests/` にテストクラスを足す（名前空間 `info.someiyoshino.TemplateProject.Tests`）。csproj は Core を再帰 glob でリンクしているので、csproj の編集は要らない。
3. `dotnet test` で通す（リモートでは push → CI・`working-agreement.md`「検証経路③」）。
