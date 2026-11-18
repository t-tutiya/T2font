#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using info.someiyoshino.T2Font;
using info.someiyoshino.T2Font.Bake;
using info.someiyoshino.T2Font.Editor;
using UnityEditor;

namespace info.someiyoshino.T2font.Samples.EditorTools
{
    /// <summary>
    /// 利用の手引き「出荷用に文字セットを絞る」（<c>docs/manual/04-shipping-fonts.md</c>）のコード例。開発用のベイク仕様と、出荷する本文の文字列から、
    /// 本文の字だけを持つ出荷用のベイク仕様を書き、インポートして確かめる。
    ///
    /// <para>
    /// 手引きは <c>manual:begin</c> と <c>manual:end</c> の印の間を、ジェネレーター（<c>Tests/ManualSnippetDocTests.cs</c>）で写す（D-USERDOCS）。
    /// メニューは持たない（どの文字列を本文とするかはプロジェクトが決める）。ビルドの前処理やメニューから呼ぶ。
    /// </para>
    /// </summary>
    public static class ShippingFontSample
    {
        // manual:begin ship-collect
        /// <summary>出荷する字を集める: 本文の全部の字 ∪ 常に入れる字 ∪ ベイク仕様の指定文字。</summary>
        public static SortedSet<int> CollectCodepoints(FontBakeSpec spec, IEnumerable<string> texts)
        {
            var codepoints = new SortedSet<int>();
            foreach (var text in texts)
            {
                // 制御文字と BOM は描く字ではないので足されない。本文の構文は解釈せず、全部の字を集める
                // （集めすぎても字が増えるだけだが、取りこぼすと出荷物で字が欠ける）。
                TextCodepoints.AddDrawable(codepoints, text);
            }
            // 実行時に組み立てる字（数字・記号）は、本文に現れないことがあるので常に入れる。
            codepoints.UnionWith(FontCharsets.Build(FontCharsetKind.Ascii));
            // ベイク仕様の指定文字（characters）は、開発用と同じく出荷用にも入れる。
            codepoints.UnionWith(spec.NamedCodepoints());
            return codepoints;
        }
        // manual:end ship-collect

        // manual:begin ship-plan
        /// <summary>
        /// 開発用のベイク仕様（<paramref name="specPath"/>）から、本文の字だけを持つ出荷用のベイク仕様を作る。元フォントに無い字は
        /// <paramref name="missing"/> に返し、出荷用には入れない（入れるとインポートが失敗する）。
        /// </summary>
        public static FontBakeSpec PlanShippingSpec(string specPath, IEnumerable<string> texts, out List<int> missing)
        {
            // インポーターと同じグリフ表現の種類の一覧で読む（インポートできるベイク仕様を、ここだけが拒むことの無いように）。
            GlyphRepresentationKinds kinds = T2FontImporter.RepresentationKinds();
            FontBakeSpec spec = FontBakeSpec.Parse(File.ReadAllText(specPath), specPath, kinds);
            var font = new TrueTypeFont(File.ReadAllBytes(spec.Source));

            SortedSet<int> wanted = CollectCodepoints(spec, texts);
            missing = wanted.Where(cp => !font.Contains(cp)).ToList();
            // 文字セットを字の明示（characters）に置き換えた、自己完結のベイク仕様。元フォントとグリフ表現の設定は写される。
            return spec.WithCharacters(wanted.Where(font.Contains));
        }
        // manual:end ship-plan

        // manual:begin ship-bake
        /// <summary>出荷用のベイク仕様を <paramref name="outputPath"/>（<c>Assets/</c> の下）へ書いてインポートし、要る字を全部持つことを確かめる。</summary>
        public static T2FontAsset BakeShippingFont(FontBakeSpec shipping, string sourceSpecPath, string outputPath)
        {
            File.WriteAllText(outputPath, shipping.ToJson(new[] { $"出荷用。{sourceSpecPath} から本文の字で作った生成物で、手で直さない。" }));
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);

            // インポートが失敗していれば null（誤りは Console に出ている）。
            T2FontAsset? imported = AssetDatabase.LoadAssetAtPath<T2FontAsset>(outputPath);
            if (imported == null)
            {
                throw new InvalidOperationException($"{outputPath} のインポートに失敗しました。Console の誤りを見てください。");
            }
            T2FontAsset asset = imported;
            var lacking = shipping.NamedCodepoints().Where(cp => !asset.Contains(cp)).ToList();
            if (lacking.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{outputPath} に無い字があります: {string.Join(" ", lacking.Select(TextCodepoints.Display))}");
            }
            return asset;
        }
        // manual:end ship-bake
    }
}
