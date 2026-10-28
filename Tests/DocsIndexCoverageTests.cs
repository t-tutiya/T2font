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
    /// ドキュメントの索引が実体を覆っているかの検査（D-DOCLIFE）。
    ///
    /// <para>
    /// <b>なぜ必要か</b>: 索引漏れは「在るのに見つけられない」形で出るので、人の注意では止まらない
    /// （凍結文書の索引が半数しか載せていなかった実例が元のプロジェクトにある）。
    /// 設計判断ログの索引は <see cref="DecisionsIndexIntegrityTests"/> が見ているので、本 fixture は残りの
    /// 2 種類を見る。
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><b>月別に分けた記録</b>（<c>archive/completed-roadmap.md</c>）: ①月見出しの「（N 件）」＝その月の
    /// 索引行数 ②本文の <c>## 見出し</c> と索引行が、完了日ごとに同じ数だけある（索引行と本文見出しは同じ
    /// 完了日の文字列を持つ規約なので、1 対 1 で突き合わせられる）。</item>
    /// <item><b>フォルダの一覧</b>（<c>plans/README.md</c>・<c>archive/README.md</c> の <c>completed-designs/</c> 節と
    /// <c>evaluations/</c> 節）: ③フォルダの <c>*.md</c> の集合＝索引行が名指すファイルの集合（漏れも宙吊りも落とす）。</item>
    /// </list>
    ///
    /// <para>正典は <c>docs/archive/completed-roadmap.md</c> 冒頭の不変条件と <c>docs/README.md</c>「ドキュメント運用ルール」。</para>
    ///
    /// <para><b>空から始まる</b>: プロジェクトの開始直後は完了記録も凍結文書も無い。月セクションが 0 個・
    /// フォルダが空（または未作成）でも、索引と実体が食い違っていなければ通る。</para>
    /// </summary>
    [TestFixture]
    public class DocsIndexCoverageTests
    {
        /// <summary>月セクションの見出し。<c>## 2026-09 — [...](...)（17 件）</c>。</summary>
        private static readonly Regex MonthHeading =
            new(@"^##\s+(?<month>\d{4}-\d{2})\s+—.*?（(?<count>\d+)\s*件", RegexOptions.Compiled);

        /// <summary>
        /// 索引表の先頭セルが名指すファイル名。<c>[`XX.md`](path)</c>（リンク）と <c>`XX.md`</c>（素の名前）の両方を
        /// 受ける。リンクなら<b>リンク先</b>のファイル名を採る——表示名とリンク先がずれたときに、実際に辿れる側で数えるため。
        /// </summary>
        private static readonly Regex FirstCellFile =
            new(@"^\|\s*(?:\[[^\]]*\]\((?<link>[^)#\s]+)\)|`(?<name>[^`]+\.md)`)", RegexOptions.Compiled);

        private static readonly char[] HeadingSeparators = { ' ', '\t', '\u3000' };

        private static string DocsDir => Path.Combine(RepoRoot.Find(), "docs");

        // ── 月別に分けた記録（completed-roadmap） ─────────────────────────────

        private sealed class MonthSection
        {
            public string Month = "";
            public int DeclaredCount;
            public readonly List<string> RowDates = new();
        }

        /// <summary>
        /// 索引を読み、月セクションごとに「見出しが宣言した件数」と「索引行の完了日」を集める。完了日は表の先頭セルを
        /// そのまま使う（<c>〜2026-05</c>・<c>2026-06-08〜15</c> のような幅のある表記があるので、日付として解釈しない）。
        /// </summary>
        private static List<MonthSection> ReadCompletedRoadmapIndex(out string indexPath)
        {
            indexPath = Path.Combine(DocsDir, "archive", "completed-roadmap.md");
            Assert.That(File.Exists(indexPath), Is.True, $"索引が見つかりません: {indexPath}");

            var sections = new List<MonthSection>();
            MonthSection? current = null;

            foreach (string line in File.ReadAllLines(indexPath))
            {
                Match heading = MonthHeading.Match(line);
                if (heading.Success)
                {
                    current = new MonthSection
                    {
                        Month = heading.Groups["month"].Value,
                        DeclaredCount = int.Parse(heading.Groups["count"].Value),
                    };
                    sections.Add(current);
                    continue;
                }

                if (line.StartsWith("## "))
                {
                    current = null;
                    continue;
                }

                if (current == null || !line.StartsWith("|"))
                {
                    continue;
                }

                string firstCell = line.Trim().Trim('|').Split('|')[0].Trim();
                if (firstCell == "完了日" || firstCell.Trim('-', ':', ' ').Length == 0)
                {
                    continue; // 表ヘッダと区切り行
                }
                current.RowDates.Add(firstCell);
            }

            return sections;
        }

        /// <summary>不変条件①: 月見出しが宣言する件数＝その月の索引行数。</summary>
        [Test]
        public void CompletedRoadmap_MonthHeadingCountMatchesIndexRowCount()
        {
            var sections = ReadCompletedRoadmapIndex(out string indexPath);

            var mismatches = sections
                .Where(s => s.DeclaredCount != s.RowDates.Count)
                .Select(s => $"{s.Month}: 見出しの宣言 {s.DeclaredCount} 件 ≠ 索引行 {s.RowDates.Count} 行")
                .ToList();

            Assert.That(mismatches, Is.Empty,
                $"{indexPath} の月見出しが宣言する件数が索引行数と食い違っています。" +
                "項目を退避したら索引行と月見出しの数を同時に更新してください:\n  " +
                string.Join("\n  ", mismatches));
        }

        /// <summary>不変条件②: 本文の見出しと索引行が、完了日ごとに同じ数だけある。</summary>
        [Test]
        public void CompletedRoadmap_BodyHeadingsAndIndexRowsMatchPerDate()
        {
            var sections = ReadCompletedRoadmapIndex(out string indexPath);
            string bodyDir = Path.Combine(DocsDir, "archive", "completed-roadmap");

            var problems = new List<string>();

            foreach (MonthSection section in sections)
            {
                string bodyPath = Path.Combine(bodyDir, $"completed-roadmap-{section.Month}.md");
                if (!File.Exists(bodyPath))
                {
                    problems.Add($"{section.Month}: 索引が月を宣言しているのに本文ファイルが無い（{bodyPath}）");
                    continue;
                }

                // 本文見出しは `## 完了日 項目名 — 補足`。完了日は最初の空白までの 1 語。
                var bodyDates = File.ReadAllLines(bodyPath)
                    .Where(l => l.StartsWith("## "))
                    .Select(l => l.Substring(3).Split(HeadingSeparators, StringSplitOptions.RemoveEmptyEntries)[0])
                    .GroupBy(d => d)
                    .ToDictionary(g => g.Key, g => g.Count());
                var indexDates = section.RowDates
                    .GroupBy(d => d)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (string date in bodyDates.Keys.Union(indexDates.Keys).OrderBy(d => d))
                {
                    bodyDates.TryGetValue(date, out int inBody);
                    indexDates.TryGetValue(date, out int inIndex);
                    if (inBody != inIndex)
                    {
                        problems.Add($"{section.Month}: 完了日 {date} の本文見出し {inBody} 個 ≠ 索引行 {inIndex} 行");
                    }
                }
            }

            // 索引が宣言していない月の本文ファイル（索引側の月見出しの足し忘れ）。
            var declared = new HashSet<string>(sections.Select(s => s.Month));
            string[] bodyFiles = Directory.Exists(bodyDir)
                ? Directory.GetFiles(bodyDir, "completed-roadmap-*.md")
                : Array.Empty<string>();
            foreach (string path in bodyFiles)
            {
                string month = Path.GetFileNameWithoutExtension(path).Substring("completed-roadmap-".Length);
                if (!declared.Contains(month))
                {
                    problems.Add($"{month}: 本文ファイルがあるのに索引に月見出しが無い");
                }
            }

            Assert.That(problems, Is.Empty,
                $"{indexPath} と月別本文が食い違っています。完了項目は当月ファイルの先頭へ追記し、" +
                "索引にも同じ完了日の行を 1 行足してください:\n  " +
                string.Join("\n  ", problems));
        }

        // ── フォルダの一覧（plans/・completed-designs/・evaluations/） ──────────────

        /// <summary>
        /// 索引ファイルのうち <paramref name="sectionPrefix"/> で始まる <c>## 見出し</c> の節（null なら全体）から、
        /// 表の先頭セルが名指すファイル名を集める。
        /// </summary>
        private static List<string> ReadIndexedFiles(string indexPath, string? sectionPrefix)
        {
            Assert.That(File.Exists(indexPath), Is.True, $"索引が見つかりません: {indexPath}");

            var files = new List<string>();
            bool inSection = sectionPrefix == null;

            foreach (string line in File.ReadAllLines(indexPath))
            {
                if (sectionPrefix != null && line.StartsWith("## "))
                {
                    inSection = line.StartsWith("## " + sectionPrefix);
                    continue;
                }
                if (!inSection)
                {
                    continue;
                }

                Match m = FirstCellFile.Match(line);
                if (!m.Success)
                {
                    continue;
                }
                string target = m.Groups["link"].Success ? m.Groups["link"].Value : m.Groups["name"].Value;
                if (target.EndsWith(".md"))
                {
                    files.Add(Path.GetFileName(target));
                }
            }
            return files;
        }

        private static IEnumerable<TestCaseData> FolderIndexes()
        {
            yield return new TestCaseData("plans/README.md", null, "plans")
                .SetName("FolderIndex_plans");
            yield return new TestCaseData("archive/README.md", "completed-designs/", "archive/completed-designs")
                .SetName("FolderIndex_completed_designs");
            yield return new TestCaseData("archive/README.md", "evaluations/", "archive/evaluations")
                .SetName("FolderIndex_evaluations");
        }

        /// <summary>不変条件③: フォルダの <c>*.md</c> の集合＝索引行が名指すファイルの集合（重複行も誤り）。</summary>
        [TestCaseSource(nameof(FolderIndexes))]
        public void FolderIndexCoversEveryDocument(string indexRelPath, string? sectionPrefix, string folderRelPath)
        {
            string indexPath = Path.Combine(DocsDir, indexRelPath);
            string folder = Path.Combine(DocsDir, folderRelPath);

            List<string> indexed = ReadIndexedFiles(indexPath, sectionPrefix);

            // 空のフォルダは git に載らないので、未作成＝空として扱う。
            string[] files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.md") : Array.Empty<string>();
            var actual = new HashSet<string>(files
                .Select(p => Path.GetFileName(p))
                .Where(n => n != "README.md"));

            var problems = new List<string>();
            problems.AddRange(actual.Except(indexed).OrderBy(n => n)
                .Select(n => $"索引漏れ: {folderRelPath}/{n} が {indexRelPath} に無い"));
            problems.AddRange(indexed.Except(actual).Distinct().OrderBy(n => n)
                .Select(n => $"宙吊り: {indexRelPath} の行 {n} が {folderRelPath}/ に無い"));
            problems.AddRange(indexed.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(n => n)
                .Select(n => $"重複: {indexRelPath} に {n} の行が 2 つ以上ある"));

            Assert.That(problems, Is.Empty,
                "フォルダと索引が食い違っています。文書を足した・移した・消したときは索引の行も同時に直してください" +
                "（移設時は退避ログ＝archive/README.md「退避・復元・削除ログ」にも 1 行）:\n  " +
                string.Join("\n  ", problems));
        }
    }
}
