#nullable enable

using System;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>生成器が続行できない誤り（はみ出す字・ページより大きいセルなど）。理由は文面が名指す（README §2.1）。</summary>
    public sealed class FontBakeException : Exception
    {
        public FontBakeException(string message)
            : base(message)
        {
        }
    }

    /// <summary>ベイク仕様の誤り（ファイル名と理由を文面が名指す）。インポートはこれで止まる（README §7.1）。</summary>
    public sealed class FontSpecException : Exception
    {
        public FontSpecException(string message)
            : base(message)
        {
        }
    }
}
