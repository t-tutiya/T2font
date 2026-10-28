<!-- 自動生成（Tests/AssemblyReferenceDocTests.cs）。手で編集しない。asmdef を変えたら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->

# アセンブリ一覧（asmdef）

`Assets/` 配下の `.asmdef` から生成した一覧。構造と置き場の規則は [`architecture/01-assemblies-and-tests.md`](../architecture/01-assemblies-and-tests.md) を読む。

| アセンブリ | 置き場 | 参照 | UnityEngine | プラットフォーム | 条件シンボル |
|---|---|---|---|---|---|
| `TemplateProject` | `Assets/RunTime/` | `TemplateProject.Core` | 使う | 全て | — |
| `TemplateProject.Core` | `Assets/TemplateProject.Core/` | — | 使わない | 全て | — |
| `TemplateProject.CoreTestSupport` | `Assets/Tests/CoreSupport/` | `TemplateProject.Core` | 使わない | 全て | `UNITY_INCLUDE_TESTS` |
| `TemplateProject.EditModeTests` | `Assets/Tests/EditMode/` | `UnityEngine.TestRunner`・`UnityEditor.TestRunner`・`TemplateProject`・`TemplateProject.Core`・`TemplateProject.CoreTestSupport`・`TemplateProject.TestSupport`・`TemplateProject.Editor`・`nunit.framework.dll` | 使う | `Editor` | `UNITY_INCLUDE_TESTS` |
| `TemplateProject.Editor` | `Assets/Editor/` | `TemplateProject`・`TemplateProject.Core` | 使う | `Editor` | — |
| `TemplateProject.PlayModeTests` | `Assets/Tests/PlayMode/` | `UnityEngine.TestRunner`・`UnityEditor.TestRunner`・`TemplateProject`・`TemplateProject.Core`・`TemplateProject.CoreTestSupport`・`TemplateProject.TestSupport`・`nunit.framework.dll` | 使う | 全て | `UNITY_INCLUDE_TESTS` |
| `TemplateProject.TestSupport` | `Assets/Tests/Shared/` | `TemplateProject`・`TemplateProject.Core`・`TemplateProject.CoreTestSupport` | 使う | 全て | `UNITY_INCLUDE_TESTS` |
