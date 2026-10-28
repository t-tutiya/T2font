#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace info.someiyoshino.TemplateProject.Tests
{
    /// <summary>
    /// 文書を指すパス参照が実在するファイルへ解決するかの検査（D-DOCTREE）。
    ///
    /// <para>
    /// <b>なぜ必要か</b>: 文書を移しても、それを指す参照は移した瞬間には何も言わずに壊れる。
    /// <c>docs/README.md</c>「ドキュメント運用ルール」の「文書を指す参照は機械が見る」を人の注意から機械へ移す。
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>① Markdown リンク</b>（<c>docs/</c> の全文書＝歴史記録を含む・<c>CLAUDE.md</c>・<c>README.md</c>・<c>.github/</c>）:
    /// リンク先はリンク元から見た相対パスで実在する。歴史記録も対象なのは、移設時に<b>リンクは張り直し、地の文は
    /// 書き換えない</b>と決めているため（D-DOCTREE）——リンクは辿れることが価値である。</item>
    /// <item><b>② バッククォート記法の <c>.md</c> パス</b>（生きた文書＝<c>docs/archive/</c> 以外）: パスは
    /// 「その文書の置き場」「<c>docs/</c>」「リポジトリルート」のどれかから解決する（文書の地の文は 3 つの起点が
    /// 混在している）。ファイル名だけの参照は <c>docs/</c> 配下のどこかに同名がある。歴史記録を外すのは、そこに
    /// 書かれた当時のパスが「いつどこにあったか」という事実だから。</item>
    /// <item><b>③ コードと設定からの参照</b>（<c>Assets/</c>・<c>Tests/</c>・<c>Tools/</c>・<c>.github/</c>・
    /// <c>.claude/</c> とルートの設定ファイル）: <c>docs/…</c> のパスはリポジトリルートから解決し、<c>.md</c> の
    /// ファイル名だけの参照は文書のどこかに同名がある。生成器の出力先定数もここで見える。</item>
    /// </list>
    ///
    /// <para>
    /// <b>ベースライン運用</b>: ②③の既存の不在は <see cref="KnownUnresolvedDocSpans"/>・
    /// <see cref="KnownUnresolvedCodeReferences"/> に <c>参照元 -&gt; 参照先</c> で 1 行ずつ列挙する。
    /// 追加は失敗、解消して行が余っても失敗する（＝単調に減る）。テンプレートからは空で始まるので、
    /// 不在は見つけたその場で直す。ベースラインは、既存のリポジトリへ本検査を後から入れるときの受け皿である。
    /// </para>
    ///
    /// <para>
    /// <b>見ないもの</b>: 見出しアンカー（<c>#…</c>。日本語見出しのスラッグ規則が表示系ごとに違う）・
    /// フェンスで囲んだコードブロック・プレースホルダを含む綴り（<c>YYYY</c>・<c>NNN</c>・<c>XX</c>・<c>*</c>・
    /// <c>&lt;名前&gt;</c>・<c>…</c> 等）・<c>-study.md</c> のような接尾辞の言及。
    /// </para>
    /// </summary>
    [TestFixture]
    public class DocsReferenceIntegrityTests
    {
        /// <summary>
        /// ② の既知の不在（生きた文書のバッククォート記法）。**新規追加は禁止**——削除・改名済みの文書を名指したいときは
        /// 決定 ID を書く（安定アンカー＝<c>docs/README.md</c>「参照の安定アンカー」）。
        /// </summary>
        private static readonly string[] KnownUnresolvedDocSpans =
        {
            // 書式: "参照元のパス -> 参照先の綴り"。
        };

        /// <summary>
        /// ③ の既知の不在（コード・データ・スクリプトのコメント）。**新規追加は禁止**。
        /// 解消はコメントを決定 ID か現行の正典へ差し替えることで、そのファイルを別の理由で触ったときに行う
        /// （コメントの一括清掃はしない＝<c>docs/guides/working-agreement.md</c>「コードコメント規約」）。
        /// </summary>
        private static readonly string[] KnownUnresolvedCodeReferences =
        {
            // 書式: "参照元のパス -> 参照先の綴り"。
        };

        /// <summary>③で読むディレクトリ（リポジトリ相対）。</summary>
        private static readonly string[] CodeDirectories = { "Assets", "Tests", "Tools", ".github", ".claude" };

        /// <summary>③で読むルート直下の設定ファイル。</summary>
        private static readonly string[] RootConfigFiles =
        {
            ".editorconfig", ".editorconfig-checker.json", ".gitattributes",
        };

        /// <summary>
        /// ③で読む拡張子。<c>.json</c> は <c>Assets/</c> の外だけ読む（<c>Assets/</c> の <c>.json</c> はデータ資産で、
        /// 大きいうえに文書を指さない）。独自のテキスト形式（スクリプト言語・カタログ等）を足したら、その拡張子もここへ足す。
        /// </summary>
        private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".yml", ".yaml", ".ps1", ".sh", ".py", ".cjs", ".pl",
        };

        /// <summary>走査で降りないディレクトリ（ビルド出力・Unity の生成物・VCS）。</summary>
        private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "Library", "Temp", "Logs", "node_modules", ".git",
        };

        /// <summary>本 fixture 自身（ベースラインに不在のパスを並べているので③から外す）。</summary>
        private const string SelfPath = "Tests/DocsReferenceIntegrityTests.cs";

        /// <summary>
        /// Markdown のインラインリンク <c>[表示](先 "題")</c>（画像 <c>![…](…)</c> を含む）。先は <c>&lt;…&gt;</c> で
        /// 囲んでもよい。参照型リンク（<c>[表示][ref]</c>）は使わない約束なので見ない。
        /// </summary>
        private static readonly Regex MarkdownLink =
            new(@"!?\[(?:[^\]\\]|\\.)*\]\(\s*<?(?<target>[^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)", RegexOptions.Compiled);

        /// <summary>インラインのコードスパン。</summary>
        private static readonly Regex CodeSpan = new(@"`(?<body>[^`]*)`", RegexOptions.Compiled);

        /// <summary>コードスパンの中身がまるごと <c>.md</c> のパス（またはファイル名）であるもの。</summary>
        private static readonly Regex MarkdownPath = new(@"^[\w./-]+\.md$", RegexOptions.Compiled);

        /// <summary>具体的なファイルではなく綴りの型を書いたもの（<c>decisions-YYYY-MM.md</c> 等）。</summary>
        private static readonly Regex Placeholder = new(@"YYYY|NNN|XX|[*<>{}…]|\.\.\.", RegexOptions.Compiled);

        /// <summary>URI のスキーム（<c>https:</c>・<c>mailto:</c> 等）。外部リンクは見ない。</summary>
        private static readonly Regex UriScheme = new(@"^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.Compiled);

        /// <summary>
        /// コード中の <c>docs/…</c> のパス。直前がパスの文字なら別のパスの途中（<c>Assets/docs/</c> 等）なので取らない。
        /// </summary>
        private static readonly Regex DocsPathInCode = new(@"(?<![\w/.-])docs/[\w./…-]*", RegexOptions.Compiled);

        /// <summary>コード中の、パスを伴わない <c>.md</c> のファイル名。</summary>
        private static readonly Regex MarkdownNameInCode =
            new(@"(?<![\w/.…-])(?<name>[A-Za-z0-9_][\w.-]*\.md)(?!\w)", RegexOptions.Compiled);

        private static readonly Lazy<string> RootLazy = new(RepoRoot.Find);

        private static string Root => RootLazy.Value;

        // ── ① Markdown リンク ─────────────────────────────────────────────

        /// <summary>①: すべての文書の Markdown リンクが実在するファイル・ディレクトリを指す。</summary>
        [Test]
        public void MarkdownLinks_ResolveToExistingPaths()
        {
            var broken = new List<string>();
            int checkedCount = 0;

            foreach (string file in AllMarkdownFiles())
            {
                string dir = Path.GetDirectoryName(file)!;
                foreach ((int lineNo, string line) in LinesOutsideFences(file))
                {
                    // コードスパンの中の [x](y) はリンクではない。桁位置を保つため空白で塗る。
                    string text = CodeSpan.Replace(line, span => new string(' ', span.Length));
                    foreach (Match m in MarkdownLink.Matches(text))
                    {
                        string target = m.Groups["target"].Value;
                        if (target.StartsWith("#") || UriScheme.IsMatch(target))
                        {
                            continue;
                        }

                        string path = Uri.UnescapeDataString(target.Split('#')[0].Split('?')[0]);
                        checkedCount++;
                        string resolved = path.StartsWith("/")
                            ? Combine(Root, path.TrimStart('/'))
                            : Combine(dir, path);
                        if (!Exists(resolved))
                        {
                            broken.Add($"{Relative(file)}:{lineNo} → {target}");
                        }
                    }
                }
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "Markdown リンクを 1 本も読めませんでした（走査範囲が変わった？）。");
            Assert.That(broken, Is.Empty,
                "Markdown リンクの先が存在しません。文書を移した・消したときは、それを指すリンクを同じコミットで張り直してください" +
                "（歴史記録もリンクは張り直す・地の文は書き換えない＝D-DOCTREE）:\n  " +
                string.Join("\n  ", broken));
        }

        // ── ② 生きた文書のバッククォート記法 ──────────────────────────────────

        /// <summary>②: 生きた文書のバッククォート記法の <c>.md</c> パスが解決する（既知の不在はベースライン）。</summary>
        [Test]
        public void DocSpans_InLiveDocuments_ResolveToExistingDocuments()
        {
            HashSet<string> documentNames = DocumentNames();
            var unresolved = new HashSet<string>(StringComparer.Ordinal);
            int checkedCount = 0;

            foreach (string file in AllMarkdownFiles().Where(f => !IsHistoricalRecord(f)))
            {
                string dir = Path.GetDirectoryName(file)!;
                foreach (var (_, line) in LinesOutsideFences(file))
                {
                    foreach (Match m in CodeSpan.Matches(line))
                    {
                        string span = m.Groups["body"].Value;
                        if (!MarkdownPath.IsMatch(span) || Placeholder.IsMatch(span) || span.StartsWith("-"))
                        {
                            continue;
                        }

                        checkedCount++;
                        bool resolved = span.Contains('/')
                            ? new[] { dir, Path.Combine(Root, "docs"), Root }.Any(b => Exists(Combine(b, span)))
                            : documentNames.Contains(span) || Exists(Combine(dir, span));
                        if (!resolved)
                        {
                            unresolved.Add($"{Relative(file)} -> {span}");
                        }
                    }
                }
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "バッククォート記法のパスを 1 つも読めませんでした（走査範囲が変わった？）。");
            AssertMatchesBaseline(unresolved, KnownUnresolvedDocSpans, nameof(KnownUnresolvedDocSpans),
                "生きた文書のバッククォート記法が存在しない文書を指しています。文書を移したなら新しいパスへ書き換えてください" +
                "（起点は「その文書の置き場」「docs/」「リポジトリルート」のどれでもよい）。削除・改名済みの文書を" +
                "名指したいなら、パスではなく決定 ID を書いてください");
        }

        // ── ③ コードと設定からの参照 ─────────────────────────────────────────

        /// <summary>③: コード・データ・設定が名指す <c>docs/…</c> パスと <c>.md</c> 名が解決する（既知の不在はベースライン）。</summary>
        [Test]
        public void DocReferences_InCodeAndConfig_ResolveToExistingDocuments()
        {
            HashSet<string> documentNames = DocumentNames();
            var unresolved = new HashSet<string>(StringComparer.Ordinal);
            int checkedCount = 0;

            foreach (string file in CodeAndConfigFiles())
            {
                string relative = Relative(file);
                if (relative == SelfPath)
                {
                    continue;
                }

                foreach (string line in File.ReadLines(file))
                {
                    foreach (Match m in DocsPathInCode.Matches(line))
                    {
                        if (Placeholder.IsMatch(m.Value) || ContinuesAsPlaceholder(line, m.Index + m.Length))
                        {
                            continue;
                        }

                        checkedCount++;
                        string path = m.Value.TrimEnd('.');
                        if (!Exists(Combine(Root, path)))
                        {
                            unresolved.Add($"{relative} -> {path}");
                        }
                    }

                    foreach (Match m in MarkdownNameInCode.Matches(line))
                    {
                        string name = m.Groups["name"].Value;
                        if (Placeholder.IsMatch(name))
                        {
                            continue;
                        }

                        checkedCount++;
                        if (!documentNames.Contains(name))
                        {
                            unresolved.Add($"{relative} -> {name}");
                        }
                    }
                }
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "コードからの文書参照を 1 つも読めませんでした（走査範囲が変わった？）。");
            AssertMatchesBaseline(unresolved, KnownUnresolvedCodeReferences, nameof(KnownUnresolvedCodeReferences),
                "コード・データ・設定が存在しない文書を指しています。文書を移したなら新しいパスへ書き換えてください" +
                "（生成器の出力先定数もここで落ちる）。コメントで出自を示すなら、揮発する設計文書のパスではなく" +
                "決定 ID を書いてください（docs/README.md「参照の安定アンカー」）");
        }

        // ── 走査範囲 ─────────────────────────────────────────────────────

        /// <summary><c>docs/</c> の全 <c>.md</c>・ルートの <c>CLAUDE.md</c> と <c>README.md</c>・<c>.github/</c> の <c>.md</c>。</summary>
        private static List<string> AllMarkdownFiles()
        {
            var files = new List<string>();
            files.AddRange(Walk(Path.Combine(Root, "docs"), p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)));
            foreach (string name in new[] { "CLAUDE.md", "README.md" })
            {
                string path = Path.Combine(Root, name);
                if (File.Exists(path))
                {
                    files.Add(path);
                }
            }
            files.AddRange(Walk(Path.Combine(Root, ".github"), p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)));
            return files;
        }

        /// <summary>ファイル名だけの参照の解決先＝全文書のファイル名。</summary>
        private static HashSet<string> DocumentNames()
        {
            return new HashSet<string>(AllMarkdownFiles().Select(p => Path.GetFileName(p)), StringComparer.Ordinal);
        }

        /// <summary>歴史記録（decisions・完了記録・評価・凍結文書）＝<c>docs/archive/</c> 配下。</summary>
        private static bool IsHistoricalRecord(string file)
        {
            return Relative(file).StartsWith("docs/archive/", StringComparison.Ordinal);
        }

        private static List<string> CodeAndConfigFiles()
        {
            var files = new List<string>();
            foreach (string top in CodeDirectories)
            {
                bool insideAssets = top == "Assets";
                files.AddRange(Walk(Path.Combine(Root, top), p => IsCodeOrConfigFile(p, insideAssets)));
            }
            files.AddRange(RootConfigFiles.Select(n => Path.Combine(Root, n)).Where(File.Exists));
            return files;
        }

        private static bool IsCodeOrConfigFile(string path, bool insideAssets)
        {
            string ext = Path.GetExtension(path);
            return CodeExtensions.Contains(ext)
                || (!insideAssets && ext.Equals(".json", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> Walk(string directory, Func<string, bool> accept)
        {
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            foreach (string file in Directory.EnumerateFiles(directory).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (accept(file))
                {
                    yield return file;
                }
            }

            foreach (string sub in Directory.EnumerateDirectories(directory).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (SkippedDirectoryNames.Contains(Path.GetFileName(sub)))
                {
                    continue;
                }
                foreach (string file in Walk(sub, accept))
                {
                    yield return file;
                }
            }
        }

        /// <summary>フェンス（<c>```</c> / <c>~~~</c>）の外の行を 1 始まりの行番号つきで返す。</summary>
        private static IEnumerable<(int LineNo, string Line)> LinesOutsideFences(string file)
        {
            bool inFence = false;
            int lineNo = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNo++;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                {
                    inFence = !inFence;
                    continue;
                }
                if (!inFence)
                {
                    yield return (lineNo, line);
                }
            }
        }

        /// <summary>
        /// パスの直後が綴りの型の続き（<c>docs/…/foo-v*.md</c> の <c>*</c>・<c>$ver</c>・<c>&lt;名前&gt;</c> 等）か。
        /// 直後の <c>&lt;/</c> は XML doc コメントの閉じタグ（<c>&lt;c&gt;docs/…&lt;/c&gt;</c>）なので続きではない。
        /// </summary>
        private static bool ContinuesAsPlaceholder(string line, int end)
        {
            if (end >= line.Length)
            {
                return false;
            }

            char next = line[end];
            if (next == '*' || next == '$' || next == '{' || next == '%' || next == '…')
            {
                return true;
            }
            return next == '<' && (end + 1 >= line.Length || line[end + 1] != '/');
        }

        // ── 判定と報告 ───────────────────────────────────────────────────

        private static void AssertMatchesBaseline(
            HashSet<string> unresolved, string[] baseline, string baselineName, string advice)
        {
            var known = new HashSet<string>(baseline, StringComparer.Ordinal);
            var problems = new List<string>();
            problems.AddRange(unresolved.Except(known).OrderBy(s => s, StringComparer.Ordinal)
                .Select(s => $"新しい不在: {s}"));
            problems.AddRange(known.Except(unresolved).OrderBy(s => s, StringComparer.Ordinal)
                .Select(s => $"解消済み（{baselineName} から行を消す）: {s}"));
            problems.AddRange(baseline.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key)
                .Select(s => $"重複（{baselineName} に同じ行が 2 つ以上ある）: {s}"));

            Assert.That(problems, Is.Empty, advice + ":\n  " + string.Join("\n  ", problems));
        }

        private static string Combine(string baseDirectory, string relative)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static string Relative(string path)
        {
            string root = Root.Replace('\\', '/').TrimEnd('/') + "/";
            string normalized = Path.GetFullPath(path).Replace('\\', '/');
            return normalized.StartsWith(root, StringComparison.Ordinal) ? normalized.Substring(root.Length) : normalized;
        }
    }
}
