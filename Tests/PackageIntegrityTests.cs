#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace info.someiyoshino.T2font.Tests
{
    /// <summary>
    /// パッケージ T2Font（<c>Packages/info.someiyoshino.t2font/</c>）が写された先でも成り立つことの検査（D-T2FONT）。
    ///
    /// <para>
    /// <b>なぜ必要か</b>: パッケージは他のリポジトリの <c>Packages/</c> へフォルダごと写される。写した先には本リポジトリの
    /// <c>docs/</c> も決定 ID も無いので、パッケージの中のコメントと文書がそれらを指すと、写した先で辿れない参照になる。
    /// パッケージの中の出自参照はパッケージの README の節（「README §n」）に限る（CLAUDE.md の上書き規約）。
    /// MIT は「すべての写しに許諾の表示を含める」ことを条件にするので、ライセンスの本文もパッケージの中に要る。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PackageIntegrityTests
    {
        private const string PackageFolder = "Packages/info.someiyoshino.t2font";

        // 決定 ID（D-XXXX）・本リポジトリの文書のパス・パッケージの外のプロジェクトの名前空間。
        private static readonly Regex DecisionId = new(@"(?<![\w-])D-[A-Z][A-Z0-9]+", RegexOptions.Compiled);
        private static readonly Regex DocsPath = new(@"(?<![\w/.-])docs/", RegexOptions.Compiled);
        private static readonly Regex ForeignNamespace = new(@"info\.someiyoshino\.(?!t2font\b)\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".md", ".json", ".asmdef", ".shader", ".cginc", ".hlsl", ".rsp",
        };

        [Test]
        public void パッケージの中は外の文書と決定IDを指さない()
        {
            string root = RepoRoot.Find();
            var offenders = new List<string>();
            foreach (string path in PackageFiles(root).Where(p => TextExtensions.Contains(Path.GetExtension(p))))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Regex pattern in new[] { DecisionId, DocsPath, ForeignNamespace })
                    {
                        Match match = pattern.Match(lines[i]);
                        if (match.Success)
                        {
                            offenders.Add($"{relative}:{i + 1}: {match.Value}");
                        }
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "パッケージの中に、写した先で辿れない参照があります。出自はパッケージの README の節（「README §n」）で書いてください:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
        }

        [Test]
        public void パッケージのCSファイルはnullableを有効にする()
        {
            string root = RepoRoot.Find();
            List<string> offenders = PackageFiles(root)
                .Where(p => string.Equals(Path.GetExtension(p), ".cs", StringComparison.OrdinalIgnoreCase))
                .Where(p => !File.ReadLines(p).Any(line => line.Trim() == "#nullable enable"))
                .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
                .ToList();

            Assert.That(offenders, Is.Empty,
                "次のファイルが `#nullable enable` を欠いています（Unity 側と外部 NUnit で公開 API の形を揃えるため）:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
        }

        [Test]
        public void パッケージはMITの許諾の本文を持つ()
        {
            string root = RepoRoot.Find();
            string packageLicense = File.ReadAllText(Path.Combine(root, PackageFolder + "/LICENSE.md"));
            string rootLicense = File.ReadAllText(Path.Combine(root, "LICENSE"));
            Assert.That(packageLicense, Is.EqualTo(rootLicense), "パッケージの中の許諾の本文とリポジトリの LICENSE が食い違っています。");

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, PackageFolder, "package.json")));
            Assert.That(manifest.RootElement.GetProperty("license").GetString(), Is.EqualTo("MIT"));
        }

        private static IEnumerable<string> PackageFiles(string root)
        {
            return Directory.EnumerateFiles(Path.Combine(root, PackageFolder), "*", SearchOption.AllDirectories);
        }
    }
}
