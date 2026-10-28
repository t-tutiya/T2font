using System.Runtime.CompilerServices;

// Unity 側のテストが RunTime の internal に触れるための可視化（D-TESTARCH）。
[assembly: InternalsVisibleTo("T2font.EditModeTests")]
[assembly: InternalsVisibleTo("T2font.PlayModeTests")]
[assembly: InternalsVisibleTo("T2font.TestSupport")]
