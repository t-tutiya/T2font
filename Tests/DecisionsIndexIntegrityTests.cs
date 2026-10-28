#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace info.someiyoshino.TemplateProject.Tests
{
    /// <summary>
    /// 設計判断ログの索引（<c>docs/archive/decisions.md</c>）と月別本文の整合検査（D-DOCLIFE）。
    ///
    /// <para>
    /// <b>なぜ必要か</b>: 索引は「1 行で見つけて本文へ飛ぶ」ための入口で、<b>壊れても何も落ちない</b>。
    /// 規約自体は運用されていても、機械が見ていなければ静かに破れる（月見出しの件数のずれが数週間残った実例が
    /// 元のプロジェクトにある）。だから検査を 1 本足して、規約を台帳ではなく不変条件にする。
    /// </para>
    ///
    /// <para>
    /// <b>検査する不変条件は 2 本だけ</b>:
    /// ①月見出しの「（N 件）」＝その月の索引行数（数え間違い・更新漏れ）。
    /// ②月別本文に現れるすべての日付が、索引にも同じ日付の行を持つ（本文へ書いて索引行を足し忘れる）。
    /// </para>
    ///
    /// <para>
    /// <b>なぜ「見出し数＝索引行数」にしないか</b>: 両者は 2 つの形で正当に食い違う——**既存エントリへの
    /// 追記が独自の索引行を持つ**（本文見出しを増やさない）と、**初稿と採用版の 2 見出しが 1 つの決定を成す**
    /// （索引行を 1 つに畳む）。どちらも実在するので、厳密な 1 対 1 を課すと除外リストが要る。**日付**の
    /// 包含関係なら除外リストなしで成立し、しかも実際に起きる失敗（今日の決定を本文にだけ書く）を捕まえる。
    /// </para>
    ///
    /// <para>正典は <c>docs/archive/decisions.md</c> 冒頭の「月見出しの『（N 件）』が数えているもの」。</para>
    /// </summary>
    [TestFixture]
    public class DecisionsIndexIntegrityTests
    {
        /// <summary>月セクションの見出し。<c>## 2026-08 — [...](...)（35 件・うち 2 件は…）</c>。</summary>
        private static readonly Regex MonthHeading =
            new(@"^##\s+(?<month>\d{4}-\d{2})\s+—.*?（(?<count>\d+)\s*件", RegexOptions.Compiled);

        /// <summary>索引の表行。先頭セルが日付のものだけを数える（表ヘッダ・区切り行は外れる）。</summary>
        private static readonly Regex IndexRow =
            new(@"^\|\s*(?<date>\d{4}-\d{2}-\d{2})\s*\|", RegexOptions.Compiled);

        /// <summary>月別本文のエントリ見出し。<c>## 2026-08-29 タイトル — D-XXX</c>。</summary>
        private static readonly Regex EntryHeading =
            new(@"^##\s+(?<date>\d{4}-\d{2}-\d{2})\b", RegexOptions.Compiled);

        /// <summary>
        /// 索引を読み、月セクションごとに「見出しが宣言した件数」と「実際に並んでいる索引行の日付」を集める。
        /// <c>RowDates</c> は参照型なので、タプルを一覧へ入れた後も <c>currentRows</c> 経由で追記できる。
        /// </summary>
        private static List<(string Month, int DeclaredCount, List<string> RowDates)> ReadIndex(out string indexPath)
        {
            indexPath = Path.Combine(RepoRoot.Find(), "docs", "archive", "decisions.md");
            Assert.That(File.Exists(indexPath), Is.True, $"索引が見つかりません: {indexPath}");

            var sections = new List<(string Month, int DeclaredCount, List<string> RowDates)>();
            List<string>? currentRows = null;

            foreach (string line in File.ReadAllLines(indexPath))
            {
                Match heading = MonthHeading.Match(line);
                if (heading.Success)
                {
                    currentRows = new List<string>();
                    sections.Add((
                        heading.Groups["month"].Value,
                        int.Parse(heading.Groups["count"].Value),
                        currentRows));
                    continue;
                }

                if (line.StartsWith("## "))
                {
                    // 月見出し以外の見出しに入ったら、直前の月セクションは閉じる。
                    currentRows = null;
                    continue;
                }

                Match row = IndexRow.Match(line);
                if (row.Success && currentRows != null)
                {
                    currentRows.Add(row.Groups["date"].Value);
                }
            }

            Assert.That(sections, Is.Not.Empty, "索引から月セクションを 1 つも読めませんでした（書式が変わった？）。");
            return sections;
        }

        /// <summary>不変条件①: 月見出しが宣言する件数＝その月の索引行数。</summary>
        [Test]
        public void MonthHeadingCountMatchesIndexRowCount()
        {
            var sections = ReadIndex(out string indexPath);

            var mismatches = sections
                .Where(s => s.DeclaredCount != s.RowDates.Count)
                .Select(s => $"{s.Month}: 見出しの宣言 {s.DeclaredCount} 件 ≠ 索引行 {s.RowDates.Count} 行")
                .ToList();

            Assert.That(mismatches, Is.Empty,
                $"{indexPath} の月見出しが宣言する件数が索引行数と食い違っています。" +
                "「（N 件）」が数えるのは**索引行数**です（月別本文の見出し数ではない）。" +
                "エントリを足したら索引行と月見出しの数を同時に更新してください:\n  " +
                string.Join("\n  ", mismatches));
        }

        /// <summary>不変条件②: 月別本文に現れる日付は、必ず索引にも同じ日付の行がある。</summary>
        [Test]
        public void EveryEntryDateAppearsInIndex()
        {
            var sections = ReadIndex(out string indexPath);
            string decisionsDir = Path.Combine(RepoRoot.Find(), "docs", "archive", "decisions");

            var missing = new List<string>();

            foreach (var section in sections)
            {
                string bodyPath = Path.Combine(decisionsDir, $"decisions-{section.Month}.md");
                Assert.That(File.Exists(bodyPath), Is.True,
                    $"索引が {section.Month} を宣言していますが本文ファイルがありません: {bodyPath}");

                var indexed = new HashSet<string>(section.RowDates);

                foreach (string date in File.ReadAllLines(bodyPath)
                             .Select(l => EntryHeading.Match(l))
                             .Where(m => m.Success)
                             .Select(m => m.Groups["date"].Value)
                             .Distinct())
                {
                    if (!indexed.Contains(date))
                    {
                        missing.Add($"{section.Month}: 本文に {date} のエントリがあるのに索引行が無い");
                    }
                }
            }

            Assert.That(missing, Is.Empty,
                $"月別本文へ追記したまま {indexPath} に索引行を足し忘れています。" +
                "索引行は `日付 ｜ **太字タイトル（決定 ID）** — 30 字以内の補足` の 1 行です:\n  " +
                string.Join("\n  ", missing));
        }
    }
}
