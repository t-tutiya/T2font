#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 字（コードポイント）の昇順の配列を引く（README §3.2）。添字がそのまま字形番号になる。辞書を組まず二分探索で引く
    /// ——約 7,000 字でも比較は 13 回で、ロード時の構築も割り当ても要らない。
    /// </summary>
    public static class GlyphTable
    {
        /// <summary><paramref name="codepoints"/>（狭義昇順）の中の <paramref name="codepoint"/> の添字。無ければ -1。</summary>
        public static int IndexOf(IReadOnlyList<int> codepoints, int codepoint)
        {
            if (codepoints == null)
            {
                throw new ArgumentNullException(nameof(codepoints));
            }

            var lo = 0;
            var hi = codepoints.Count - 1;
            while (lo <= hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                var cp = codepoints[mid];
                if (cp == codepoint)
                {
                    return mid;
                }
                if (cp < codepoint)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return -1;
        }

        /// <summary>
        /// <paramref name="codepoints"/> が狭義昇順か（重複が無いか）。生成器の出力と、読み込んだ資産の健全性の検査に使う。
        /// </summary>
        public static bool IsStrictlyAscending(IReadOnlyList<int> codepoints)
        {
            if (codepoints == null)
            {
                throw new ArgumentNullException(nameof(codepoints));
            }

            for (var i = 1; i < codepoints.Count; i++)
            {
                if (codepoints[i - 1] >= codepoints[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
