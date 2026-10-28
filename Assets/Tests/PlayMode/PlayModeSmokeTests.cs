using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace T2font.PlayModeTests
{
    /// <summary>
    /// Play Mode テストアセンブリが組み上がり、フレームループが進むことを確かめるスモークテスト。
    /// 実際のテストを足したら消してよい（asmdef にスクリプトが 1 つも無いと Unity が警告を出すために置いている）。
    /// 要素ごとのテストは <c>Assets/Tests/PlayMode/&lt;要素&gt;/</c> に置き、名前空間を
    /// <c>T2font.PlayModeTests.&lt;要素&gt;</c> にする（docs/guides/testing-workflow.md）。
    /// </summary>
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator フレームループが進む()
        {
            int start = Time.frameCount;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(start));
        }
    }
}
