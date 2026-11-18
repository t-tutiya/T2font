#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace info.someiyoshino.T2font.Tests
{
    /// <summary>
    /// 利用の手引き（<c>docs/manual/</c>）のコード例のジェネレーターと golden 検査（D-USERDOCS）。
    ///
    /// <para>
    /// 手引きの <c>&lt;!-- gen:名前 --&gt;</c> と <c>&lt;!-- /gen --&gt;</c> の間を、<c>Assets/</c> の C# の <c>// manual:begin 名前</c> と
    /// <c>// manual:end 名前</c> の間から作り直す。コード例のファイルは検証のプロジェクトでコンパイルされる（PR の Job B）ので、パッケージの
    /// API が変わればコンパイルが止め、本検査が手引きへの写し忘れを止める。手で写したコード例は、互換を壊しうる 0.x の版で黙って古くなる。
    /// </para>
    ///
    /// <para>
    /// 印は入れ子にしてよい（外側のコード例には内側の印の行を含めない）。名前はリポジトリ全体で一意で、どの手引きからも使われない印は誤り。
    /// 再生成は <c>UPDATE_GENERATED=1</c> の <c>dotnet test</c> か <c>[regen]</c> 付きの push（D-REGEN）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ManualSnippetDocTests
    {
        private const string GenEnd = "<!-- /gen -->";

        private static readonly Regex BeginMarker = new(@"^\s*//\s*manual:begin\s+(?<name>[\w.-]+)\s*$", RegexOptions.Compiled);
        private static readonly Regex EndMarker = new(@"^\s*//\s*manual:end\s+(?<name>[\w.-]+)\s*$", RegexOptions.Compiled);
        private static readonly Regex GenStart = new(@"^<!-- gen:(?<name>[\w.-]+) -->$", RegexOptions.Compiled);

        private static string ManualDirectory => Path.Combine(RepoRoot.Find(), "docs", "manual");

        private static IEnumerable<TestCaseData> ManualFiles()
        {
            if (!Directory.Exists(ManualDirectory))
            {
                yield break;
            }
            foreach (string path in Directory.GetFiles(ManualDirectory, "*.md").OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                yield return new TestCaseData(name).SetName("ManualSnippets_" + name);
            }
        }

        [TestCaseSource(nameof(ManualFiles))]
        public void 手引きのコード例が生成物と一致する(string fileName)
        {
            var problems = new List<string>();
            Dictionary<string, Snippet> snippets = CollectSnippets(RepoRoot.Find(), problems);
            Assert.That(problems, Is.Empty, "コード例の印が壊れています:\n  " + string.Join("\n  ", problems));

            string relative = "docs/manual/" + fileName;
            string text = File.ReadAllText(Path.Combine(ManualDirectory, fileName));
            GeneratedGolden.AssertUpToDate(Expand(text, snippets, relative), relative, "Assets/ の manual:begin〜manual:end の印");
        }

        [Test]
        public void コード例の印は対になりどれも手引きから使われる()
        {
            var problems = new List<string>();
            Dictionary<string, Snippet> snippets = CollectSnippets(RepoRoot.Find(), problems);

            var used = new HashSet<string>(StringComparer.Ordinal);
            if (Directory.Exists(ManualDirectory))
            {
                foreach (string path in Directory.GetFiles(ManualDirectory, "*.md"))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        Match start = GenStart.Match(line);
                        if (start.Success)
                        {
                            used.Add(start.Groups["name"].Value);
                        }
                    }
                }
            }
            problems.AddRange(snippets
                .Where(pair => !used.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Value.Source}: コード例 {pair.Key} をどの手引きも使っていません（<!-- gen:{pair.Key} -->）。"));

            Assert.That(problems, Is.Empty,
                "コード例の印と手引きが食い違っています。印を消すか、手引きに gen の区画を置いてください:\n  " + string.Join("\n  ", problems));
        }

        /// <summary><c>Assets/</c> の C# から、印の間の行を名前ごとに集める。印の誤りは <paramref name="problems"/> に積む。</summary>
        private static Dictionary<string, Snippet> CollectSnippets(string root, List<string> problems)
        {
            var result = new Dictionary<string, Snippet>(StringComparer.Ordinal);
            string assets = Path.Combine(root, "Assets");
            IEnumerable<string> files = Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal);
            foreach (string path in files)
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var open = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                int lineNo = 0;
                foreach (string line in File.ReadAllLines(path))
                {
                    lineNo++;
                    Match begin = BeginMarker.Match(line);
                    if (begin.Success)
                    {
                        string name = begin.Groups["name"].Value;
                        if (open.ContainsKey(name) || result.ContainsKey(name))
                        {
                            problems.Add($"{relative}:{lineNo}: コード例 {name} の名前が重複しています。");
                        }
                        else
                        {
                            open[name] = new List<string>();
                        }
                        continue;
                    }

                    Match end = EndMarker.Match(line);
                    if (end.Success)
                    {
                        string name = end.Groups["name"].Value;
                        if (open.Remove(name, out List<string>? lines))
                        {
                            result[name] = new Snippet(relative, lines);
                        }
                        else
                        {
                            problems.Add($"{relative}:{lineNo}: コード例 {name} の終わりの印に、始まりの印がありません。");
                        }
                        continue;
                    }

                    foreach (List<string> collected in open.Values)
                    {
                        collected.Add(line);
                    }
                }
                foreach (string name in open.Keys.OrderBy(n => n, StringComparer.Ordinal))
                {
                    problems.Add($"{relative}: コード例 {name} の終わりの印（// manual:end {name}）がありません。");
                }
            }
            return result;
        }

        /// <summary>手引きの本文の gen の区画を、コード例で作り直した本文を返す。</summary>
        private static string Expand(string text, IReadOnlyDictionary<string, Snippet> snippets, string relative)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var output = new List<string>();
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];
                output.Add(line);
                i++;
                Match start = GenStart.Match(line);
                if (!start.Success)
                {
                    continue;
                }

                string name = start.Groups["name"].Value;
                int end = Array.IndexOf(lines, GenEnd, i);
                if (end < 0)
                {
                    Assert.Fail($"{relative}: <!-- gen:{name} --> の終わり（{GenEnd}）がありません。");
                }
                if (!snippets.TryGetValue(name, out Snippet? snippet))
                {
                    Assert.Fail($"{relative}: コード例 {name} が Assets/ にありません（// manual:begin {name} と // manual:end {name} の印を置く）。");
                }
                output.AddRange(Render(snippet!));
                output.Add(GenEnd);
                i = end + 1;
            }
            return string.Join("\n", output);
        }

        /// <summary>コード例を C# のフェンスで囲んだ行にする。共通の字下げを除き、先頭に出所のパスをコメントで置く。</summary>
        private static IEnumerable<string> Render(Snippet snippet)
        {
            int indent = snippet.Lines
                .Where(l => l.Trim().Length > 0)
                .Select(l => l.Length - l.TrimStart(' ').Length)
                .DefaultIfEmpty(0)
                .Min();

            yield return "```csharp";
            yield return "// " + snippet.Source;
            foreach (string line in snippet.Lines)
            {
                yield return line.Trim().Length == 0 ? string.Empty : line.Substring(indent).TrimEnd();
            }
            yield return "```";
        }

        /// <summary>コード例 1 つ（出所のリポジトリ相対パスと、印の間の行）。</summary>
        private sealed class Snippet
        {
            public Snippet(string source, List<string> lines)
            {
                Source = source;
                Lines = lines;
            }

            public string Source { get; }

            public List<string> Lines { get; }
        }
    }
}
