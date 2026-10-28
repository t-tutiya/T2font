#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace info.someiyoshino.TemplateProject.Tests
{
    /// <summary>
    /// アセンブリ一覧（<c>docs/generated/assembly-reference.md</c>）の生成器と golden 検査。
    ///
    /// <para>
    /// <b>なぜ生成物か</b>: asmdef の参照関係を文書の表へ手で書き写すと、asmdef を変えたときに表だけが古くなる。
    /// 一覧は 1 か所に限り（D-DOCDUP）、その 1 か所を機械が asmdef から作る。<c>docs/generated/</c> の中は
    /// 手で直さない（D-DOCTREE）。構造の説明は <c>docs/architecture/01-assemblies-and-tests.md</c> が持つ。
    /// </para>
    ///
    /// <para>
    /// 生成器を足すときの見本も兼ねる: 出力は <see cref="GeneratedText.AppendLineLf(StringBuilder, string)"/> で
    /// 組み立て（D-GENEOL）、検査と書き出しは <see cref="GeneratedGolden.AssertUpToDate"/> に任せる（D-REGEN）。
    /// 行はアセンブリ名の順に並べる——名前は共通の接頭辞を持つので、プロジェクト名を変えても行の順は変わらない。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssemblyReferenceDocTests
    {
        private const string OutputPath = "docs/generated/assembly-reference.md";

        [Test]
        public void アセンブリ一覧が生成物と一致する()
        {
            GeneratedGolden.AssertUpToDate(Generate(RepoRoot.Find()), OutputPath, "Assets/ 配下の .asmdef");
        }

        /// <summary><paramref name="root"/>（リポジトリルート）の <c>Assets/</c> 配下の asmdef から一覧を作る。</summary>
        internal static string Generate(string root)
        {
            var rows = new List<(string Name, string Line)>();
            foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "Assets"), "*.asmdef", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement asmdef = doc.RootElement;

                string name = asmdef.GetProperty("name").GetString()!;
                string folder = relative.Substring(0, relative.LastIndexOf('/') + 1);
                string references = Join(Strings(asmdef, "references").Concat(Strings(asmdef, "precompiledReferences")));
                string engine = IsTrue(asmdef, "noEngineReferences") ? "使わない" : "使う";
                List<string> platforms = Strings(asmdef, "includePlatforms");
                string platformText = platforms.Count == 0 ? "全て" : Join(platforms);
                string defines = Join(Strings(asmdef, "defineConstraints"));

                rows.Add((name, $"| `{name}` | `{folder}` | {references} | {engine} | {platformText} | {defines} |"));
            }

            var sb = new StringBuilder();
            sb.AppendLineLf("<!-- 自動生成（Tests/AssemblyReferenceDocTests.cs）。手で編集しない。asmdef を変えたら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->");
            sb.AppendLineLf();
            sb.AppendLineLf("# アセンブリ一覧（asmdef）");
            sb.AppendLineLf();
            sb.AppendLineLf("`Assets/` 配下の `.asmdef` から生成した一覧。構造と置き場の規則は [`architecture/01-assemblies-and-tests.md`](../architecture/01-assemblies-and-tests.md) を読む。");
            sb.AppendLineLf();
            sb.AppendLineLf("| アセンブリ | 置き場 | 参照 | UnityEngine | プラットフォーム | 条件シンボル |");
            sb.AppendLineLf("|---|---|---|---|---|---|");
            foreach (var row in rows.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                sb.AppendLineLf(row.Line);
            }
            return sb.ToString();
        }

        private static List<string> Strings(JsonElement asmdef, string property)
        {
            if (!asmdef.TryGetProperty(property, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }
            return array.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        private static bool IsTrue(JsonElement asmdef, string property)
        {
            return asmdef.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;
        }

        private static string Join(IEnumerable<string> items)
        {
            List<string> list = items.ToList();
            return list.Count == 0 ? "—" : string.Join("・", list.Select(i => $"`{i}`"));
        }
    }
}
