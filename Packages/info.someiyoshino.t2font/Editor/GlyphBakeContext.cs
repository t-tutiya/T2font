#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using info.someiyoshino.T2Font.Bake;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace info.someiyoshino.T2Font.Editor
{
    /// <summary>
    /// 焼き器が取込の文脈に触れる口（README §7.3 の 4）。副資産の受け渡し・シェーダの読み込みと依存・取込の報告を持つ。
    /// 焼き器は <c>AssetImportContext</c> に直接触れない——取込が途中で失敗したとき、作りかけの副資産を取込器がまとめて捨てるため。
    /// </summary>
    public sealed class GlyphBakeContext
    {
        private readonly AssetImportContext _ctx;
        private readonly List<KeyValuePair<string, Object>> _objects = new List<KeyValuePair<string, Object>>();
        private readonly List<string> _report = new List<string>();

        internal GlyphBakeContext(AssetImportContext ctx, string baseName)
        {
            _ctx = ctx;
            BaseName = baseName;
        }

        /// <summary>取り込んでいる生成仕様のパス（誤りの文面の頭に付ける）。</summary>
        public string AssetPath => _ctx.assetPath;

        /// <summary>資産の名前（生成仕様のファイル名から拡張子を除いたもの）。副資産の名前の頭に使う。</summary>
        public string BaseName { get; }

        /// <summary>
        /// 取り込みの対象のビルドターゲット。<b>読むとターゲットを切り替えたときに取り込み直す依存が張られる</b>ので、ターゲットで
        /// 出力が変わるときだけ読む。
        /// </summary>
        public BuildTarget SelectedBuildTarget => _ctx.selectedBuildTarget;

        internal IReadOnlyList<KeyValuePair<string, Object>> Objects => _objects;

        internal IReadOnlyList<string> ReportLines => _report;

        /// <summary>
        /// パッケージ <paramref name="packageName"/> の中の <paramref name="relativePath"/> の資産パス（<c>Packages/…</c>）。
        /// 表現のパッケージが自分のシェーダを指すのに使う——導入の方法（git URL か写しか・README §11）によらず同じパスになる。
        /// </summary>
        /// <exception cref="FontBakeException">そのパッケージが登録されていない（Packages/ の下に無い）。</exception>
        public string PackageAssetPath(string packageName, string relativePath)
        {
            if (packageName == null)
            {
                throw new ArgumentNullException(nameof(packageName));
            }
            if (relativePath == null)
            {
                throw new ArgumentNullException(nameof(relativePath));
            }
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName);
            if (package == null)
            {
                throw new FontBakeException(
                    $"{AssetPath}: パッケージ {packageName} が登録されていません。表現のパッケージは Packages/ の下へフォルダごと置いてください。");
            }
            return package.assetPath + "/" + relativePath;
        }

        /// <summary>
        /// シェーダを読み、その取込結果への依存を張る（シェーダが変われば取り込み直す）。
        /// </summary>
        /// <exception cref="FontBakeException">そのパスにシェーダが無い。</exception>
        public Shader LoadShader(string assetPath)
        {
            if (assetPath == null)
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

            _ctx.DependsOnArtifact(assetPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader != null)
            {
                return shader;
            }
            if (File.Exists(FileUtil.GetPhysicalPath(assetPath)))
            {
                // ファイルはあるが、まだ取り込まれていない（新しく開いたプロジェクトで、取込の順がシェーダより先になった）。依存は
                // 張ってあるので、シェーダの取込の後に Unity がこのフォントを取り込み直す。
                throw new GlyphBakeDeferredException($"{AssetPath}: シェーダ {assetPath} の取込を待ちます（取込の後に取り込み直されます）。");
            }
            throw new FontBakeException($"{AssetPath}: シェーダ {assetPath} がありません。");
        }

        /// <summary>
        /// 副資産（テクスチャなど）を渡す。<paramref name="identifier"/> は表現の中で一意な名前。取込が成功したときだけ資産に加わり、
        /// 失敗したときは取込器が破棄する。
        /// </summary>
        public void AddObject(string identifier, Object obj)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            foreach (var pair in _objects)
            {
                if (pair.Key == identifier)
                {
                    throw new ArgumentException($"副資産の名前 '{identifier}' が重複しています。", nameof(identifier));
                }
            }
            _objects.Add(new KeyValuePair<string, Object>(identifier, obj));
        }

        /// <summary>取込の報告（Console に 1 行で出す）に足す文。</summary>
        public void Report(string text)
        {
            _report.Add(text ?? throw new ArgumentNullException(nameof(text)));
        }

        internal void DestroyObjects()
        {
            foreach (var pair in _objects)
            {
                if (pair.Value != null)
                {
                    Object.DestroyImmediate(pair.Value);
                }
            }
            _objects.Clear();
        }
    }

    /// <summary>取込に要る別の資産がまだ取り込まれていない（依存は張ってあり、その取込の後に取り込み直される）。</summary>
    internal sealed class GlyphBakeDeferredException : Exception
    {
        public GlyphBakeDeferredException(string message)
            : base(message)
        {
        }
    }
}
