#nullable enable

using System.IO;
using info.someiyoshino.T2Font.Bake;
using info.someiyoshino.T2font.Tests;

namespace info.someiyoshino.T2Font.Tests
{
    /// <summary>パッケージのテストが使う実ファイルの場所と、使う表現の種類の一覧。</summary>
    internal static class TestFonts
    {
        /// <summary>検証用の元フォント（M+ 1c・TrueType・upem 1000）。期待値はこのファイルの表から取ったもの。</summary>
        public static string Mplus1c()
        {
            return Path.Combine(RepoRoot.Find(), "Assets", "Authoring", "Fonts", "mplus-1c-regular.ttf");
        }

        /// <summary>ベイク仕様の source（プロジェクトパス）を実ファイルのパスにする。</summary>
        public static string ProjectPath(string projectPath)
        {
            return Path.Combine(RepoRoot.Find(), projectPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>本パッケージが持つ表現の種類だけの一覧（パッケージの README §7.4）。</summary>
        public static GlyphRepresentationKinds Kinds { get; } = new GlyphRepresentationKinds(new GlyphRepresentationKind[] { SdfRepresentationKind.Instance });
    }
}
