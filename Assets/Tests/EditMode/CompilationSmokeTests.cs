using System;
using System.Linq;
using NUnit.Framework;

namespace T2font.EditModeTests
{
    /// <summary>
    /// CI のコンパイルゲート用スモークテスト（docs/guides/code-quality.md「Job B」・D-JOBBPR）。
    /// EditMode テストの実行には全 asmdef（RunTime / Core / Editor / テスト）のコンパイル成功が
    /// 前提になるため、本テストが 1 件走ること自体が「Unity 側コンパイルが通っている」証明になる。
    /// 外部 NUnit（Tests/）では検出できない UnityEngine 依存コードの型エラー・asmdef 参照切れを
    /// PR 時点で検出する。テストの中身は最小のアサーションでよい。
    /// </summary>
    [TestFixture]
    public class CompilationSmokeTests
    {
        [Test]
        public void 全アセンブリがコンパイルされテストランナーが起動できる()
        {
            // Core の型が参照解決できる（参照が壊れていればこのファイル自体がコンパイルに失敗する）。
            Assert.That(typeof(info.someiyoshino.T2font.GeneratedText), Is.Not.Null);

            // RunTime と Editor は公開型を持たないうちもアセンブリとしてロードされている。
            string[] loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            Assert.That(loaded, Does.Contain("T2font"));
            Assert.That(loaded, Does.Contain("T2font.Editor"));
        }
    }
}
