#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>生成仕様から作った字の表と、取込の報告に出すもの。</summary>
    public sealed class FontSpecFace
    {
        internal FontSpecFace(FontFaceData face, IReadOnlyList<int> missingFromCharsets)
        {
            Face = face;
            MissingFromCharsets = missingFromCharsets;
        }

        /// <summary>字の表。</summary>
        public FontFaceData Face { get; }

        /// <summary>
        /// 固定の字集合のうち元フォントに無かった字（昇順）。固定の字集合は上限集合なので欠けは失敗にせず、件数を報告する
        /// （README §7.1）。
        /// </summary>
        public IReadOnlyList<int> MissingFromCharsets { get; }
    }

    /// <summary>
    /// 生成仕様と元フォントから字の表を作る（README §7.3 の 2・3）。取込器と外部 NUnit が同じ手順を通る。表現を作るのは
    /// 各表現の焼き器の仕事で、ここは全表現で共通の部分だけを持つ。
    /// </summary>
    public static class FontSpecBaker
    {
        /// <summary>
        /// 字の表を作る。名指しの追加字が元フォントに無ければ失敗する（作者が明示した要求を黙って捨てない・README §2.1）。
        /// 固定の字集合の欠けは <see cref="FontSpecFace.MissingFromCharsets"/> へ載せて続ける。
        /// </summary>
        /// <exception cref="FontBakeException">追加字が元フォントに無い。</exception>
        public static FontSpecFace BuildFace(FontBakeSpec spec, TrueTypeFont font, string sourceName)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            var named = spec.NamedCodepoints();
            var missingNamed = named.Where(cp => !font.Contains(cp)).ToList();
            if (missingNamed.Count > 0)
            {
                throw new FontBakeException(
                    $"{sourceName}: characters の字が元フォント {spec.Source} にありません（{missingNamed.Count} 字）: "
                    + string.Join(" ", missingNamed.Take(40).Select(cp => char.ConvertFromUtf32(cp) + FormattableString.Invariant($"(U+{cp:X4})"))));
            }

            var requested = spec.FixedCodepoints();
            requested.UnionWith(named);
            var face = FontFaceBuilder.Build(font, requested);
            return new FontSpecFace(face, face.Missing);
        }

        /// <summary>
        /// 生成元（字の表の生成の版数・表現の種類とその版数・生成仕様の本文・元フォントのバイト列）の SHA-256（小文字の 16 進）。
        /// 資産の <c>sourceHash</c> に入れ、「どの入力から作った資産か」を追えるようにする。
        /// </summary>
        public static string SourceHash(string specText, byte[] fontBytes, GlyphRepresentationKind kind)
        {
            if (specText == null)
            {
                throw new ArgumentNullException(nameof(specText));
            }
            if (fontBytes == null)
            {
                throw new ArgumentNullException(nameof(fontBytes));
            }
            if (kind == null)
            {
                throw new ArgumentNullException(nameof(kind));
            }

            using (var sha = SHA256.Create())
            {
                var header = Encoding.UTF8.GetBytes(
                    string.Format(CultureInfo.InvariantCulture, "T2Font/{0}/{1}/{2}\n", FontFaceBuilder.Version, kind.Name, kind.Version)
                    + specText + "\n");
                sha.TransformBlock(header, 0, header.Length, null, 0);
                sha.TransformFinalBlock(fontBytes, 0, fontBytes.Length);
                return SdfAtlas.ToHex(sha.Hash!);
            }
        }
    }
}
