#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using info.someiyoshino.T2Font.Bake;
using UnityEditor;
using UnityEngine;

namespace info.someiyoshino.T2Font.Editor
{
    /// <summary>
    /// 焼き器の一覧（README §7.4）と、種類ごとの版数の依存（README §7.5）。
    ///
    /// <para>
    /// 版数は種類ごとのカスタム依存（<c>AssetDatabase.RegisterCustomDependency</c>）にし、取込器が自分の種類の依存だけを張る。
    /// 取込器の版数（<c>ScriptedImporter</c> の属性の定数）に全種類の版数を混ぜると、別のパッケージの表現の版数を上げたときに全フォントが
    /// 取り込み直される（和文フォントは 1 本で数十秒かかる）。登録は取込の処理の外でしかできないので、ドメインの読み込みの時点で行う。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class GlyphRepresentationBakers
    {
        static GlyphRepresentationBakers()
        {
            var set = Load();
            foreach (var baker in set.Bakers)
            {
                AssetDatabase.RegisterCustomDependency(
                    DependencyName(baker.Kind.Name), Hash128.Compute(FormattableString.Invariant($"{baker.Kind.Name}/{baker.Kind.Version}")));
            }
        }

        /// <summary>種類 <paramref name="kind"/> の版数のカスタム依存の名前。</summary>
        public static string DependencyName(string kind)
        {
            return "info.someiyoshino.t2font/representation/" + kind;
        }

        /// <summary>焼き器を集める。集められなかったもの（構築子が無い・種類が重複する）は <see cref="BakerSet.Problems"/> に載る。</summary>
        public static BakerSet Load()
        {
            var bakers = new List<GlyphRepresentationBaker>();
            var problems = new List<string>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<GlyphRepresentationBaker>().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    problems.Add($"焼き器 {type.FullName} に引数の無い公開の構築子がありません。");
                    continue;
                }
                bakers.Add((GlyphRepresentationBaker)Activator.CreateInstance(type));
            }

            foreach (var group in bakers.GroupBy(b => b.Kind.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                problems.Add($"種類 \"{group.Key}\" の焼き器が複数あります: {string.Join("・", group.Select(b => b.GetType().FullName))}");
            }
            var unique = bakers.GroupBy(b => b.Kind.Name, StringComparer.Ordinal).Where(g => g.Count() == 1).Select(g => g.Single()).ToList();
            return new BakerSet(unique, problems);
        }

        /// <summary>集めた焼き器。</summary>
        public sealed class BakerSet
        {
            private readonly Dictionary<string, GlyphRepresentationBaker> _byName;

            public BakerSet(IReadOnlyList<GlyphRepresentationBaker> bakers, IReadOnlyList<string> problems)
            {
                Bakers = bakers;
                Problems = problems;
                _byName = bakers.ToDictionary(b => b.Kind.Name, StringComparer.Ordinal);
                Kinds = new GlyphRepresentationKinds(bakers.Select(b => b.Kind));
            }

            /// <summary>種類ごとに 1 つの焼き器。</summary>
            public IReadOnlyList<GlyphRepresentationBaker> Bakers { get; }

            /// <summary>集められなかった焼き器の文面。空でなければ取込を止める（README §7.4）。</summary>
            public IReadOnlyList<string> Problems { get; }

            /// <summary>生成仕様の検査に渡す種類の一覧。</summary>
            public GlyphRepresentationKinds Kinds { get; }

            /// <summary>種類の名前から焼き器を引く。</summary>
            public GlyphRepresentationBaker? Find(string kind)
            {
                return _byName.TryGetValue(kind, out var baker) ? baker : null;
            }
        }
    }
}
