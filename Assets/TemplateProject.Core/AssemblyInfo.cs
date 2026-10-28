#nullable enable

using System.Runtime.CompilerServices;

// Unity 側のテストが Core の internal に触れるための可視化（D-TESTARCH）。
// 外部 NUnit（Tests/）はソースを自アセンブリへ直リンクするので、ここに名前を足す必要は無い。
[assembly: InternalsVisibleTo("TemplateProject.EditModeTests")]
[assembly: InternalsVisibleTo("TemplateProject.PlayModeTests")]

// 共有テスト支援は本番の内部段を直に駆動してよい。前段だけを検査するために「検証しない経路」を
// 本番 API へ足すのは、テスト都合の製品仕様を作ることになるため、内部段を見せる方を選ぶ（D-ARGOPT）。
[assembly: InternalsVisibleTo("TemplateProject.CoreTestSupport")]
