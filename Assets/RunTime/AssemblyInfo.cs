using System.Runtime.CompilerServices;

// Unity 側のテストが RunTime の internal に触れるための可視化（D-TESTARCH）。
[assembly: InternalsVisibleTo("TemplateProject.EditModeTests")]
[assembly: InternalsVisibleTo("TemplateProject.PlayModeTests")]
[assembly: InternalsVisibleTo("TemplateProject.TestSupport")]
