#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 文字列を字（Unicode のコードポイント）の並びとして読む（README §3.2）。
    /// 字はグリフインデックスへ 1 字ずつ引くので、C# の <c>char</c>（UTF-16 の符号単位）で回すと BMP 外の字（𠮟・𩸽・𠮷 など）が
    /// サロゲートの 2 つに割れ、どちらもフォントに無い字になる。
    ///
    /// <para>
    /// サロゲートペアは 1 字。<b>片割れのサロゲートは捨てず、その符号単位の値をそのまま 1 字として返す</b>——フォントに無い字
    /// として代替グリフで見え、誤りが黙って消えない（README §2.1）。
    /// </para>
    /// </summary>
    public static class TextCodepoints
    {
        /// <summary>
        /// <paramref name="text"/> の <paramref name="index"/> から 1 字を読む。<paramref name="length"/> はその字の
        /// 符号単位の数（1 か 2）で、次の字は <c>index + length</c> から始まる。
        /// </summary>
        public static int Read(string text, int index, out int length)
        {
            var c = text[index];
            if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                length = 2;
                return char.ConvertToUtf32(c, text[index + 1]);
            }
            length = 1;
            return c;
        }

        /// <summary><paramref name="text"/> の字の数（サロゲートペアは 1 字）。</summary>
        public static int Count(string text)
        {
            var count = 0;
            for (var i = 0; i < text.Length; count++)
            {
                Read(text, i, out var length);
                i += length;
            }
            return count;
        }

        /// <summary>
        /// 文字列の字（サロゲートペアは 1 字）を、描く字の集合（フォントの文字セット・本文の字）へ足す。制御文字
        /// （U+0000〜001F・U+007F）と BOM（U+FEFF）は描く字ではないので足さない。
        ///
        /// <para>
        /// <b>片割れのサロゲートは失敗させる</b>（<see cref="Read"/> と扱いが違う）。文字セットは「ベイクする字」の要求で、
        /// 片割れは字ではない——黙って捨てると要求が消え、符号単位のまま入れるとベイクできない字が要求に残る。
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">片割れのサロゲートがある。</exception>
        public static void AddDrawable(ISet<int> set, string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                int cp;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                    {
                        throw new ArgumentException($"位置 {i} のサロゲートが片割れです。", nameof(text));
                    }
                    cp = char.ConvertToUtf32(c, text[i + 1]);
                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    throw new ArgumentException($"位置 {i} のサロゲートが片割れです。", nameof(text));
                }
                else
                {
                    cp = c;
                }

                if (cp < 0x20 || cp == 0x7F || cp == 0xFEFF)
                {
                    continue;
                }
                set.Add(cp);
            }
        }

        /// <summary>
        /// 診断の文面や GameObject の名前に使う表示（スカラー値ならその字、片割れのサロゲートは <c>U+XXXX</c>）。
        /// </summary>
        public static string Display(int codepoint)
        {
            var isScalar = codepoint >= 0 && codepoint <= 0x10FFFF && (codepoint < 0xD800 || codepoint > 0xDFFF);
            return isScalar ? char.ConvertFromUtf32(codepoint) : "U+" + codepoint.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
