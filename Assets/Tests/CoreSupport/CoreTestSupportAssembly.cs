#nullable enable

namespace info.someiyoshino.T2font.TestSupport
{
    /// <summary>
    /// 共有テスト支援 <c>T2font.CoreTestSupport</c> の置き場を示す型（中身は持たない）。
    ///
    /// <para>
    /// ここには<b>両方のテストの世界（外部 NUnit <c>Tests/</c> と Unity 側）で使う、Core にだけ依存する
    /// テストダブルとテスト用の組み立て関数</b>を置く（D-TESTARCH の R2・D-ARGOPT）。外部 NUnit は csproj が
    /// ディレクトリ参照でリンクし、Unity 側は asmdef 参照で引くので、同じソースを複製せずに共有できる。
    /// 本番 API の省略可能引数（<c>= null</c> の既定値）が欲しくなったら、既定値はここの組み立て関数が持つ。
    /// </para>
    ///
    /// <para>最初の共有ヘルパを足したら、この型は消してよい（asmdef にスクリプトが 1 つも無いと Unity が警告を出すために置いている）。</para>
    /// </summary>
    internal static class CoreTestSupportAssembly
    {
    }
}
