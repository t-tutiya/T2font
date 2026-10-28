namespace T2font.PlayModeTests
{
    /// <summary>
    /// 共有テスト支援 <c>T2font.TestSupport</c> の置き場を示す型（中身は持たない）。
    ///
    /// <para>
    /// ここには<b>Unity 側のテスト（EditMode／PlayMode）だけで使う共有テストダブル</b>を各 1 個置く
    /// （D-TESTARCH の R2）。Core にだけ依存するものは <c>Assets/Tests/CoreSupport/</c> へ置く。
    /// 名前空間は <c>T2font.PlayModeTests</c> に揃え、PlayMode テストは親名前空間の解決で、
    /// EditMode テストは using を 1 行足して参照する。
    /// </para>
    ///
    /// <para>最初の共有テストダブルを足したら、この型は消してよい（asmdef にスクリプトが 1 つも無いと Unity が警告を出すために置いている）。</para>
    /// </summary>
    internal static class TestSupportAssembly
    {
    }
}
