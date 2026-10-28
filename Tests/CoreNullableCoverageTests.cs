#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace info.someiyoshino.TemplateProject.Tests
{
    /// <summary>
    /// `Assets/TemplateProject.Core/` 配下の全 .cs が `#nullable enable` を持つことを固定するソース走査ガード（D-NRTCORE）。
    ///
    /// <para>
    /// <b>なぜ必要か</b>: Core は 2 つのツールチェーンでコンパイルされ、nullable の既定が食い違う。
    /// Unity は <c>csc.rsp</c> が無ければ既定 <c>disable</c>、本テストプロジェクトは
    /// <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c> ＋ <c>&lt;WarningsAsErrors&gt;Nullable&lt;/WarningsAsErrors&gt;</c> を持ち、
    /// <c>&lt;Compile Include="..\Assets\TemplateProject.Core\**\*.cs" /&gt;</c> で Core 全体を glob リンクする。
    /// <b>プロジェクト既定はディレクティブの無いファイルにも適用される</b>ため、ディレクティブの有無が分けるのは
    /// 「検査の有無」ではなく「Unity 側から見た公開 API の形状」（oblivious か非 null か）である。
    /// </para>
    ///
    /// <para>
    /// 帰結として、ディレクティブを欠いた Core ファイルは <b>Unity では無警告のまま書けてしまい、
    /// `dotnet test` で初めて CS8625/CS8604 等のエラーとして落ちる</b>。食い違いが顕在化するのが
    /// 開発者が常には回せない側のビルドだけ、という性質が再発の原因なので、
    /// 「新規ファイルにディレクティブを付け忘れた」時点で CI が落ちるようにする。
    /// </para>
    ///
    /// <para>
    /// 対象は Core のみ。`Assets/RunTime/`・`Assets/Editor/` は上記 glob の対象外で同じ非対称が起きないため、
    /// opportunistic 運用（編集で触ったファイルにその場で足す）に委ねる。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CoreNullableCoverageTests
    {
        [Test]
        public void Core配下の全csファイルがnullable有効()
        {
            string coreRoot = FindCoreRoot();
            var offenders = new List<string>();

            foreach (string path in Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (!HasNullableEnableBeforeCode(path))
                {
                    offenders.Add(ToRelative(coreRoot, path));
                }
            }

            offenders.Sort(StringComparer.Ordinal);

            Assert.That(offenders, Is.Empty,
                "次の Core ファイルが `#nullable enable` を欠いています。ファイル先頭（生成物は auto-generated "
                + "ヘッダの直後）へディレクティブを 1 行追加してください。Unity 側の既定が disable のため、"
                + "付け忘れると『Unity では通るが dotnet test で落ちる』食い違いが生まれます:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
        }

        /// <summary>
        /// 最初の<b>コード</b>行に到達する前に <c>#nullable enable</c> が現れるかを判定する。
        /// 行頭固定にしないのは、生成物（<c>.g.cs</c>）が先頭に auto-generated ヘッダを
        /// 必要とするため——Roslyn は先頭コメントで生成物を判定するので、ディレクティブをその前に置くと
        /// 生成物扱いが外れてアナライザの対象になってしまう。
        /// </summary>
        private static bool HasNullableEnableBeforeCode(string path)
        {
            var inBlockComment = false;

            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();

                if (inBlockComment)
                {
                    int end = line.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0)
                    {
                        continue;
                    }

                    line = line.Substring(end + 2).Trim();
                    inBlockComment = false;
                }

                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("/*", StringComparison.Ordinal))
                {
                    // 閉じない場合のみブロックコメント継続。閉じている行はコメントとして読み飛ばす
                    // （`/* … */ using …` のような書き方は Core に無いが、あってもコード行を
                    // 読み飛ばした結果このメソッドは false 側へ倒れるため、判定は安全側になる）。
                    inBlockComment = !line.Contains("*/", StringComparison.Ordinal);
                    continue;
                }

                if (line == "#nullable enable")
                {
                    return true;
                }

                // 他のプリプロセッサ（#if 等）はコードではないので走査を続ける。
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                // using / namespace / [assembly: …] 等の最初のコードに到達した。
                return false;
            }

            return false;
        }

        private static string ToRelative(string coreRoot, string path)
        {
            string norm = path.Replace('\\', '/');
            string root = coreRoot.Replace('\\', '/').TrimEnd('/') + "/";
            return norm.StartsWith(root, StringComparison.Ordinal) ? norm.Substring(root.Length) : norm;
        }

        private static string FindCoreRoot()
        {
            string candidate = Path.Combine(RepoRoot.Find(), "Assets", "TemplateProject.Core");
            if (!Directory.Exists(candidate))
            {
                throw new InvalidOperationException($"Core のフォルダが見つかりません: {candidate}");
            }

            return candidate;
        }
    }
}
