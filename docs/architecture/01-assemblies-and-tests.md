# 01 アセンブリとテストの構成

## 2 つのコンパイル世界

本プロジェクトのコードは 2 つのコンパイラから見える。

| 世界 | 対象 | コンパイラ | 回る場所 |
|---|---|---|---|
| **Unity（asmdef）** | `Assets/` 配下の全 asmdef | Unity の C# コンパイル | Unity Editor・`Tools/verify-unity-compile.ps1`・CI の Job B（PR だけ） |
| **外部 SDK csproj** | `Tests/T2font.Tests.csproj` が**リンク**した Unity 非依存のソース | .NET SDK（`dotnet`） | `dotnet test`・CI の Job A |

両方の世界から見えるのは **Core**（`Assets/T2font.Core/`）と**共有テスト支援**（`Assets/Tests/CoreSupport/`）だけである。
どちらも asmdef に `noEngineReferences: true` を持つので UnityEngine を参照できず、外部 csproj は両フォルダを再帰 glob でリンクする
（コピーではなくリンクなので、Unity 側の編集がそのままテストへ反映される）。

**帰結**:

- ロジックは可能な限り Core に置く。Core のテストは Unity を起動せずに数十秒で回り、リモートセッションからも CI の Job A で回せる（D-ENV4）。
- RunTime（`Assets/RunTime/`）と Editor（`Assets/Editor/`）のコンパイル誤りは Job A では見えず、PR の Job B か Unity Editor で初めて見える（D-JOBBPR）。
- 2 つの世界で nullable の文脈と強度を揃える（Core の全 `.cs` に `#nullable enable`・各 asmdef の隣の `csc.rsp`・D-NRTCORE）。手順と落とし穴は `../guides/code-quality.md`。

## アセンブリの層

依存は上から下へだけ向く（下の層は上の層を知らない）。全 asmdef の参照関係の網羅表は [`../generated/assembly-reference.md`](../generated/assembly-reference.md)（自動生成）。

| 層 | アセンブリ | 置き場 | 役割 |
|---|---|---|---|
| Editor 拡張 | `T2font.Editor` | `Assets/Editor/` | メニュー・インポータ等の Editor 専用コード |
| ランタイム | `T2font` | `Assets/RunTime/` | MonoBehaviour・UnityEngine に依存するコード |
| Core | `T2font.Core` | `Assets/T2font.Core/` | Unity 非依存のロジック（外部 NUnit で検査できる） |

テスト側のアセンブリ（`Assets/Tests/` 配下）は `UNITY_INCLUDE_TESTS` を条件シンボルに持ち、プレイヤービルドに入らない。

| アセンブリ | 置き場 | 役割 |
|---|---|---|
| `T2font.CoreTestSupport` | `Assets/Tests/CoreSupport/` | 両方の世界で共有する、Core にだけ依存するテスト支援（D-TESTARCH・D-ARGOPT） |
| `T2font.TestSupport` | `Assets/Tests/Shared/` | Unity 側のテストだけで共有するテストダブル |
| `T2font.EditModeTests` | `Assets/Tests/EditMode/` | Unity 型を使う同期テスト。CI のコンパイルゲート（`CompilationSmokeTests`）もここ |
| `T2font.PlayModeTests` | `Assets/Tests/PlayMode/` | フレームループが要るテスト |

`internal` はテストアセンブリへ `InternalsVisibleTo` で見せる（各層の `AssemblyInfo.cs`）。外部 NUnit はソースを直接リンクするので不要。

## テストの置き場

どのテストをどの層へ置くかの判定基準と規約（R1〜R3）は `../guides/testing-workflow.md`（D-TESTARCH）。
