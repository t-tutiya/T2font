using System;
using System.IO;
using NUnit.Framework;

namespace info.someiyoshino.TemplateProject.Tests
{
    /// <summary>
    /// 生成物（<c>.g.cs</c>／<c>docs/generated/</c> の生成 Markdown）の golden 検証の共通手順（D-REGEN）。
    ///
    /// <para><b>「正を変えたが再生成し忘れた」ドリフトを失敗として検出する</b>のが目的で、
    /// 生成器を持つテストはすべて同じ手順を踏む——検証と書き出しを別々に実装すると、
    /// 環境変数の綴りや比較の正規化が生成器ごとにずれて「片方だけ落ちない」状態が生まれる。</para>
    ///
    /// <para>使い方: 生成器のテストで <c>AssertUpToDate(生成器の出力, リポジトリ相対の出力先, "正の名前")</c> を呼ぶ。
    /// 見本は <see cref="AssemblyReferenceDocTests"/>。生成器の出力は <c>GeneratedText.AppendLineLf</c> で組み立てる
    /// （改行 LF 固定・D-GENEOL）。
    /// リモートセッションからの再生成は <c>[regen]</c> 付きの push（<c>.github/workflows/regenerate.yml</c>）。</para>
    /// </summary>
    public static class GeneratedGolden
    {
        /// <summary>生成物を書き出すモードに入るための環境変数（全 golden テスト共用）。</summary>
        public const string UpdateVariable = "UPDATE_GENERATED";

        /// <summary>
        /// 生成物（<paramref name="relativePath"/>）が生成器出力（<paramref name="expected"/>）と
        /// 一致することを検証する。<c>UPDATE_GENERATED=1</c> のときは検証せず書き出す（ヘッドレス再生成）。
        /// </summary>
        /// <param name="sourceDescription">正（メタテーブル・宣言表）の名前。失敗メッセージに出す。</param>
        public static void AssertUpToDate(string expected, string relativePath, string sourceDescription)
        {
            var path = Path.Combine(RepoRoot.Find(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, expected);
                Assert.Pass($"{relativePath} を再生成しました: {path}");
                return;
            }

            Assert.That(File.Exists(path), Is.True,
                $"生成物が存在しません: {path}。`{UpdateVariable}=1` で再生成してください。");

            Assert.That(Normalize(File.ReadAllText(path)), Is.EqualTo(Normalize(expected)),
                $"{relativePath} が生成器の出力と一致しません。"
                + $"正（{sourceDescription}）を変更した場合は `{UpdateVariable}=1` で再生成してください。");
        }

        /// <summary>改行コード差・末尾空白を無視して比較するための正規化。</summary>
        private static string Normalize(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');
        }
    }
}
