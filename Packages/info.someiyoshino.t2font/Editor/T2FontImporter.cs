#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using info.someiyoshino.T2Font.Bake;
using UnityEditor;
using UnityEditor.AssetImporters;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace info.someiyoshino.T2Font.Editor
{
    /// <summary>
    /// 生成仕様（<c>*.t2font</c>）の取込器（README §7）。元フォントから字の表を作り、表現の種類の焼き器に表現を作らせて、
    /// <see cref="T2FontAsset"/>（主）と表現・テクスチャ（副資産）を作る。
    ///
    /// <para>
    /// 生成物は Library に作られ、リポジトリには置かない（README §2.4）。生成仕様・元フォント（<c>DependsOnSourceAsset</c>）・表現のシェーダ
    /// （<c>DependsOnArtifact</c>）・表現の種類の版数（カスタム依存）のどれかが変われば Unity が取り込み直す。<b>字の表の出力が変わったら
    /// <see cref="FontFaceBuilder.Version"/> を、表現の出力が変わったらその種類の版数を上げる</b>（README §7.5）。
    /// </para>
    ///
    /// <para>
    /// 誤り（生成仕様の誤り・元フォントが無い・名指しの字が元フォントに無い・焼き器の誤り・テクスチャが期待の形にならない）は取込の失敗に
    /// して資産を作らない（README §2.1・§7.3）。実行時のロードは null になり、参照が明確に失敗する。
    /// </para>
    /// </summary>
    [ScriptedImporter(ImporterVersion, "t2font")]
    public sealed class T2FontImporter : ScriptedImporter
    {
        /// <summary>取込器の版数（字の表の生成の版数 × 100 ＋ 取込器自身の改訂）。表現の版数は種類ごとのカスタム依存が持つ。</summary>
        public const int ImporterVersion = (FontFaceBuilder.Version * 100) + 1;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var bakers = GlyphRepresentationBakers.Load();
            if (bakers.Problems.Count > 0)
            {
                ctx.LogImportError($"{ctx.assetPath}: 表現の焼き器を集められません: {string.Join(" ", bakers.Problems)}");
                return;
            }

            var specText = File.ReadAllText(ctx.assetPath);
            FontBakeSpec spec;
            try
            {
                spec = FontBakeSpec.Parse(specText, ctx.assetPath, bakers.Kinds);
            }
            catch (FontSpecException e)
            {
                ctx.LogImportError(e.Message);
                return;
            }

            // 依存は失敗の判定より先に張る（元フォントを置き直せば・表現の版数が変われば取り込み直される）。
            var kind = spec.Representation.Kind;
            var baker = bakers.Find(kind.Name)!;
            ctx.DependsOnCustomDependency(GlyphRepresentationBakers.DependencyName(kind.Name));
            ctx.DependsOnSourceAsset(spec.Source);
            var fontPath = FileUtil.GetPhysicalPath(spec.Source);
            if (!File.Exists(fontPath))
            {
                ctx.LogImportError($"{ctx.assetPath}: 元フォント {spec.Source} がありません。");
                return;
            }

            var fontBytes = File.ReadAllBytes(fontPath);
            var watch = Stopwatch.StartNew();
            FontSpecFace face;
            try
            {
                face = FontSpecBaker.BuildFace(spec, new TrueTypeFont(fontBytes), ctx.assetPath);
            }
            catch (Exception e) when (e is FormatException || e is NotSupportedException)
            {
                ctx.LogImportError($"{ctx.assetPath}: 元フォント {spec.Source} を読めません: {e.Message}");
                return;
            }
            catch (FontBakeException e)
            {
                ctx.LogImportError(Prefixed(ctx.assetPath, e.Message));
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var context = new GlyphBakeContext(ctx, baseName);
            GlyphRepresentation? representation = null;
            T2FontAsset asset;
            try
            {
                representation = baker.Bake(context, face.Face, spec.Representation);
                if (representation == null)
                {
                    throw new FontBakeException($"焼き器 {baker.GetType().FullName} が表現を返しませんでした。");
                }
                if (representation.Kind != kind.Name)
                {
                    throw new FontBakeException($"焼き器 {baker.GetType().FullName} の表現が種類 \"{representation.Kind}\" を名乗っています（期待は \"{kind.Name}\"）。");
                }
                if (representation.Effects != kind.Effects)
                {
                    throw new FontBakeException(
                        $"表現の描ける効果 {representation.Effects} が種類 \"{kind.Name}\" の宣言 {kind.Effects} と違います（README §6.3）。");
                }
                representation.name = $"{baseName} ({kind.Name})";
                asset = T2FontAsset.Create(
                    face.Face.Face, face.Face.Codepoints, face.Face.Glyphs, face.Face.Notdef, representation,
                    FontSpecBaker.SourceHash(specText, fontBytes, kind));
            }
            catch (GlyphBakeDeferredException e)
            {
                Discard(context, representation);
                ctx.LogImportWarning(e.Message);
                return;
            }
            catch (Exception e) when (e is FontBakeException || e is ArgumentException)
            {
                Discard(context, representation);
                ctx.LogImportError(Prefixed(ctx.assetPath, e.Message));
                return;
            }
            watch.Stop();

            asset.name = baseName;
            foreach (var pair in context.Objects)
            {
                ctx.AddObjectToAsset("representation/" + pair.Key, pair.Value);
            }
            ctx.AddObjectToAsset("representation", representation);
            ctx.AddObjectToAsset("font", asset);
            ctx.SetMainObject(asset);

            Debug.Log(Report(ctx.assetPath, face, kind.Name, context, watch.Elapsed.TotalSeconds));
        }

        // 取込を失敗させるときは、作りかけの副資産を残さない（ctx に渡していない物は誰も破棄しない）。
        private static void Discard(GlyphBakeContext context, GlyphRepresentation? representation)
        {
            context.DestroyObjects();
            if (representation != null)
            {
                Object.DestroyImmediate(representation);
            }
        }

        // 焼き器の誤りは生成仕様の名を持たないことがあるので、どの仕様の誤りか分かるよう前に付ける。
        private static string Prefixed(string assetPath, string message)
        {
            return message.StartsWith(assetPath, StringComparison.Ordinal) ? message : $"{assetPath}: {message}";
        }

        private static string Report(string assetPath, FontSpecFace face, string kind, GlyphBakeContext context, double seconds)
        {
            var text = new StringBuilder();
            text.Append($"[T2Font] {assetPath}: {face.Face.Codepoints.Length} 字・字形 {face.Face.GlyphCount}・{kind}");
            foreach (var line in context.ReportLines)
            {
                text.Append("・").Append(line);
            }
            text.Append($"・{seconds:0.0} 秒");
            if (face.MissingFromCharsets.Count > 0)
            {
                // 固定の字集合は上限集合なので、欠けは件数だけを知らせる（警告にしない・README §7.1）。
                text.Append($"・固定の字集合のうち元フォントに無い字 {face.MissingFromCharsets.Count} 字");
            }
            return text.ToString();
        }
    }
}
