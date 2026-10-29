#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>生成器の固定の文字セット（README §7.2）。</summary>
    [Flags]
    public enum FontCharsetKind
    {
        None = 0,

        /// <summary>ASCII の印字可能文字（U+0020〜U+007E）95 字。</summary>
        Ascii = 1 << 0,

        /// <summary>JIS X 0208 の全 6,879 字（非漢字・第 1 水準・第 2 水準）。</summary>
        JisX0208 = 1 << 1,

        /// <summary>Windows（CP932）が JIS 標準と別の Unicode へ写す 6 字（～ ∥ － ￠ ￡ ￢）。</summary>
        WindowsVariants = 1 << 2,

        /// <summary>NEC 特殊文字（CP932 の 13 区）83 字。</summary>
        NecRow13 = 1 << 3,

        /// <summary>IBM 拡張（CP932 の 0xFA40〜0xFC4B）388 字。人名に多い漢字（髙・﨑・德 など）を含む。</summary>
        IbmExtensions = 1 << 4,

        /// <summary>
        /// 記号のブロック（Latin-1 補助・一般句読点・文字様記号・数字の形・矢印・数学記号・その他の技術記号・囲み英数字・
        /// 罫線・ブロック要素・幾何学模様・その他の記号・装飾記号・CJK の記号と句読点・囲み CJK・CJK 互換・半角／全角形）。
        /// IME で打てる記号（— © ® ™ é ♥ ♡ ‼ ▶ ◀ など）。書式文字（ゼロ幅・双方向の制御・行と段落の区切り）は除く。
        /// </summary>
        CommonSymbols = 1 << 5,

        /// <summary>
        /// 開発中の既定（JIS X 0208 全部＋周辺。開発中は字の欠けを意識しない＝IME で打てる字を広く収める）。
        /// 元フォントに無い字は上限集合の欠けとして件数だけを報告する（README §7.1）。
        /// </summary>
        DevelopmentDefault = Ascii | JisX0208 | WindowsVariants | NecRow13 | IbmExtensions | CommonSymbols,
    }

    /// <summary>
    /// 文字セットの組み立てと、アトラスのページへ並べる順（優先クラス）を決める。
    /// </summary>
    public static class FontCharsets
    {
        // CommonSymbols のブロック（両端を含む）。
        private static readonly (int First, int Last)[] SymbolBlocks =
        {
            (0x00A0, 0x00FF),   // Latin-1 補助
            (0x2000, 0x206F),   // 一般句読点（書式文字は SymbolExclusions で除く）
            (0x2100, 0x214F),   // 文字様記号
            (0x2150, 0x218F),   // 数字の形
            (0x2190, 0x21FF),   // 矢印
            (0x2200, 0x22FF),   // 数学記号
            (0x2300, 0x23FF),   // その他の技術記号
            (0x2460, 0x24FF),   // 囲み英数字
            (0x2500, 0x257F),   // 罫線
            (0x2580, 0x259F),   // ブロック要素
            (0x25A0, 0x25FF),   // 幾何学模様
            (0x2600, 0x26FF),   // その他の記号
            (0x2700, 0x27BF),   // 装飾記号
            (0x3000, 0x303F),   // CJK の記号と句読点
            (0x3200, 0x32FF),   // 囲み CJK 文字・月
            (0x3300, 0x33FF),   // CJK 互換
            (0xFF00, 0xFFEF),   // 半角／全角形
        };

        // 記号のブロックの中の書式文字（描くグリフが無い）。
        private static readonly (int First, int Last)[] SymbolExclusions =
        {
            (0x200B, 0x200F),   // ゼロ幅・方向の印
            (0x2028, 0x202E),   // 行と段落の区切り・双方向の埋め込み
            (0x2060, 0x206F),   // 単語結合子・不可視の演算子・双方向の分離
        };

        private static readonly Lazy<HashSet<int>> Level1Set = new Lazy<HashSet<int>>(() => ToSet(JisX0208Table.Level1));
        private static readonly Lazy<HashSet<int>> Level2Set = new Lazy<HashSet<int>>(() => ToSet(JisX0208Table.Level2));

        /// <summary><paramref name="kinds"/> が指す固定の文字セットのコードポイント（昇順・重複なし）。</summary>
        public static SortedSet<int> Build(FontCharsetKind kinds)
        {
            var set = new SortedSet<int>();
            if ((kinds & FontCharsetKind.Ascii) != 0)
            {
                for (var cp = 0x20; cp <= 0x7E; cp++)
                {
                    set.Add(cp);
                }
            }
            if ((kinds & FontCharsetKind.JisX0208) != 0)
            {
                AddString(set, JisX0208Table.NonKanji);
                AddString(set, JisX0208Table.Level1);
                AddString(set, JisX0208Table.Level2);
            }
            if ((kinds & FontCharsetKind.WindowsVariants) != 0)
            {
                AddString(set, JisX0208Table.WindowsVariants);
            }
            if ((kinds & FontCharsetKind.NecRow13) != 0)
            {
                AddString(set, JisX0208Table.NecRow13);
            }
            if ((kinds & FontCharsetKind.IbmExtensions) != 0)
            {
                AddString(set, JisX0208Table.IbmExtensions);
            }
            if ((kinds & FontCharsetKind.CommonSymbols) != 0)
            {
                foreach (var (first, last) in SymbolBlocks)
                {
                    for (var cp = first; cp <= last; cp++)
                    {
                        if (!IsSymbolExclusion(cp))
                        {
                            set.Add(cp);
                        }
                    }
                }
            }
            return set;
        }

        /// <summary>
        /// 文字列の各字（サロゲートペアは 1 字）を <paramref name="set"/> へ足す。規則（制御文字と BOM を足さない・
        /// 片割れのサロゲートは失敗）の正典は <see cref="TextCodepoints.AddDrawable"/>。
        /// </summary>
        public static void AddString(ISet<int> set, string text)
        {
            TextCodepoints.AddDrawable(set, text);
        }

        /// <summary>
        /// アトラスのページへ並べる優先クラス（小さいほど先＝0 ページに入る）。本文の大半が 0 ページだけで描けるように
        /// する（ページが変わると uGUI のバッチが割れる）。クラスの中はコードポイント順。
        /// 0＝漢字以外（ASCII・記号・全角英数・ギリシャ・キリル・罫線ほか）／1＝かな／2＝JIS 第 1 水準漢字／
        /// 3＝JIS 第 2 水準漢字／4＝それ以外の漢字。
        /// </summary>
        public static int PriorityClass(int codepoint)
        {
            if (IsKana(codepoint))
            {
                return 1;
            }
            if (Level1Set.Value.Contains(codepoint))
            {
                return 2;
            }
            if (Level2Set.Value.Contains(codepoint))
            {
                return 3;
            }
            return IsHan(codepoint) ? 4 : 0;
        }

        private static bool IsSymbolExclusion(int cp)
        {
            foreach (var (first, last) in SymbolExclusions)
            {
                if (cp >= first && cp <= last)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsKana(int cp)
        {
            return (cp >= 0x3040 && cp <= 0x30FF)    // ひらがな・カタカナ
                || (cp >= 0x31F0 && cp <= 0x31FF)    // カタカナ拡張
                || (cp >= 0xFF66 && cp <= 0xFF9F);   // 半角カタカナ
        }

        private static bool IsHan(int cp)
        {
            return (cp >= 0x3400 && cp <= 0x4DBF)    // CJK 統合漢字拡張 A
                || (cp >= 0x4E00 && cp <= 0x9FFF)    // CJK 統合漢字
                || (cp >= 0xF900 && cp <= 0xFAFF)    // CJK 互換漢字
                || (cp >= 0x20000 && cp <= 0x3FFFF); // 拡張 B 以降
        }

        private static HashSet<int> ToSet(string text)
        {
            var set = new HashSet<int>();
            AddString(set, text);
            return set;
        }
    }
}
