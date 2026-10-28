using System;
using System.IO;

namespace info.someiyoshino.T2font.Tests
{
    /// <summary>
    /// 外部 NUnit からリポジトリルート（<c>Assets/</c> の親）を解決する共有ヘルパ。
    /// ソースや文書を読むテストが同じ起点を使うために置く——起点の求め方が分かれると
    /// 「片方は実データを見ているが片方は見ていない」が起こる。
    ///
    /// <para>ビルド出力の位置（<c>bin/Debug/netX/</c>）に依存しないよう、
    /// <c>ProjectSettings/ProjectVersion.txt</c> が在るディレクトリまで遡って探す。</para>
    /// </summary>
    public static class RepoRoot
    {
        public static string Find()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "ProjectSettings", "ProjectVersion.txt")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(
                "リポジトリルートが見つかりません（ProjectSettings/ProjectVersion.txt 不在）。");
        }
    }
}
