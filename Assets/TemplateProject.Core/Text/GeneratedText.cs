#nullable enable

using System.Text;

namespace info.someiyoshino.TemplateProject
{
    /// <summary>
    /// リポジトリへコミットされる生成物のテキスト規約——<b>改行は常に LF</b>（D-GENEOL）。
    ///
    /// <para>
    /// <b>なぜ必要か</b>: <c>StringBuilder.AppendLine</c> と Newtonsoft の <c>Formatting.Indented</c> は
    /// どちらも <c>Environment.NewLine</c> を使うため、<b>同じ入力から Windows では CRLF・Linux では LF</b> を吐く。
    /// 生成物は <c>.editorconfig</c>（<c>end_of_line = lf</c>）に従って LF でコミットされているので、
    /// Windows で再生成すると<b>内容が 1 文字も変わっていなくても作業ツリーが汚れる</b>——
    /// <c>git status</c> は M を出す（サイズが変わるため）のに <c>.gitattributes</c> の
    /// <c>text=auto eol=lf</c> が改行を畳んで <c>git diff</c> は空、という最も分かりにくい形で現れる。
    /// </para>
    ///
    /// <para>
    /// 規約は「生成器の出力はプラットフォームに依存してはならない」1 本。<c>AppendLine</c> を使わず
    /// <see cref="AppendLineLf(StringBuilder, string)"/> を使い、改行を選べない整形器（Newtonsoft 等）の
    /// 出力は <see cref="NormalizeNewlines"/> で畳んでから書き出す。
    /// </para>
    /// </summary>
    public static class GeneratedText
    {
        /// <summary>生成物の改行。プラットフォームに依らず LF。</summary>
        public const char Newline = '\n';

        /// <summary><c>StringBuilder.AppendLine(string)</c> の LF 固定版。</summary>
        public static StringBuilder AppendLineLf(this StringBuilder sb, string text)
        {
            return sb.Append(text).Append(Newline);
        }

        /// <summary><c>StringBuilder.AppendLine()</c> の LF 固定版（空行）。</summary>
        public static StringBuilder AppendLineLf(this StringBuilder sb)
        {
            return sb.Append(Newline);
        }

        /// <summary>
        /// CRLF・単独 CR を LF へ畳む。改行を差し替えられない整形器（Newtonsoft の
        /// <c>Formatting.Indented</c> 等）の出力を規約へ合わせる最終段として使う。
        /// JSON では生 CR はトークン間の空白としてしか現れない（文字列中は <c>\r</c> へ
        /// エスケープされる）ため、畳んでも内容は変わらない。
        /// </summary>
        public static string NormalizeNewlines(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
