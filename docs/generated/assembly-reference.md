<!-- 自動生成（Tests/AssemblyReferenceDocTests.cs）。手で編集しない。asmdef を変えたら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# アセンブリ一覧（asmdef）

`Assets/` と `Packages/`（埋め込みパッケージ）配下の `.asmdef` から生成した一覧。構造と置き場の規則は [`architecture/01-assemblies-and-tests.md`](../architecture/01-assemblies-and-tests.md) を読む。

| アセンブリ | 置き場 | 参照 | UnityEngine | プラットフォーム | 条件シンボル |
|---|---|---|---|---|---|
| `Someiyoshino.T2Font` | `Packages/info.someiyoshino.t2font/Runtime/` | `Someiyoshino.T2Font.Core` | 使う | 全て | — |
| `Someiyoshino.T2Font.Bake` | `Packages/info.someiyoshino.t2font/Bake/` | `Someiyoshino.T2Font.Core`・`Newtonsoft.Json.dll` | 使わない | `Editor` | — |
| `Someiyoshino.T2Font.Core` | `Packages/info.someiyoshino.t2font/Core/` | — | 使わない | 全て | — |
| `Someiyoshino.T2Font.Editor` | `Packages/info.someiyoshino.t2font/Editor/` | `Someiyoshino.T2Font.Core`・`Someiyoshino.T2Font`・`Someiyoshino.T2Font.Bake` | 使う | `Editor` | — |
| `Someiyoshino.T2Font.Viewer` | `Packages/info.someiyoshino.t2font/Viewer/` | `Someiyoshino.T2Font.Core`・`Someiyoshino.T2Font` | 使う | 全て | — |
| `T2font` | `Assets/RunTime/` | `T2font.Core` | 使う | 全て | — |
| `T2font.Core` | `Assets/T2font.Core/` | — | 使わない | 全て | — |
| `T2font.CoreTestSupport` | `Assets/Tests/CoreSupport/` | `T2font.Core` | 使わない | 全て | `UNITY_INCLUDE_TESTS` |
| `T2font.EditModeTests` | `Assets/Tests/EditMode/` | `UnityEngine.TestRunner`・`UnityEditor.TestRunner`・`T2font`・`T2font.Core`・`T2font.CoreTestSupport`・`T2font.TestSupport`・`T2font.Editor`・`nunit.framework.dll` | 使う | `Editor` | `UNITY_INCLUDE_TESTS` |
| `T2font.Editor` | `Assets/Editor/` | `T2font`・`T2font.Core` | 使う | `Editor` | — |
| `T2font.PlayModeTests` | `Assets/Tests/PlayMode/` | `UnityEngine.TestRunner`・`UnityEditor.TestRunner`・`T2font`・`T2font.Core`・`T2font.CoreTestSupport`・`T2font.TestSupport`・`nunit.framework.dll` | 使う | 全て | `UNITY_INCLUDE_TESTS` |
| `T2font.TestSupport` | `Assets/Tests/Shared/` | `T2font`・`T2font.Core`・`T2font.CoreTestSupport` | 使う | 全て | `UNITY_INCLUDE_TESTS` |
