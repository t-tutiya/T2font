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
    /// ベイカーがインポートの文脈に触れる口（README §7.3 の 4）。サブアセットの受け渡し・シェーダの読み込みと依存・インポートの報告を持つ。
    /// ベイカーは <c>AssetImportContext</c> に直接触れない——インポートが途中で失敗したとき、作りかけのサブアセットをインポーターがまとめて捨てるため。
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

        /// <summary>インポートしているベイク仕様のパス（誤りの文面の頭に付ける）。</summary>
        public string AssetPath => _ctx.assetPath;

        /// <summary>アセットの名前（ベイク仕様のファイル名から拡張子を除いたもの）。サブアセットの名前の頭に使う。</summary>
        public string BaseName { get; }

        /// <summary>
        /// インポートの対象のビルドターゲット。<b>読むとターゲットを切り替えたときに再インポートする依存が張られる</b>ので、ターゲットで
        /// 出力が変わるときだけ読む。
        /// </summary>
        public BuildTarget SelectedBuildTarget => _ctx.selectedBuildTarget;

        internal IReadOnlyList<KeyValuePair<string, Object>> Objects => _objects;

        internal IReadOnlyList<string> ReportLines => _report;

        /// <summary>
        /// パッケージ <paramref name="packageName"/> の中の <paramref name="relativePath"/> のアセットパス（<c>Packages/…</c>）。
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
        /// シェーダを読み、そのインポート結果への依存を張る（シェーダが変われば再インポートする）。
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
                // ファイルはあるが、まだインポートされていない（新しく開いたプロジェクトで、インポートの順がシェーダより先になった）。依存は
                // 張ってあるので、シェーダのインポートの後に Unity がこのフォントを再インポートする。
                throw new GlyphBakeDeferredException($"{AssetPath}: シェーダ {assetPath} のインポートを待ちます（インポートの後に再インポートされます）。");
            }
            throw new FontBakeException($"{AssetPath}: シェーダ {assetPath} がありません。");
        }

        /// <summary>
        /// サブアセット（テクスチャなど）を渡す。<paramref name="identifier"/> は表現の中で一意な名前。インポートが成功したときだけアセットに加わり、
        /// 失敗したときはインポーターが破棄する。
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
                    throw new ArgumentException($"サブアセットの名前 '{identifier}' が重複しています。", nameof(identifier));
                }
            }
            _objects.Add(new KeyValuePair<string, Object>(identifier, obj));
        }

        /// <summary>インポートの報告（Console に 1 行で出す）に足す文。</summary>
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

    /// <summary>インポートに要る別のアセットがまだインポートされていない（依存は張ってあり、そのインポートの後に再インポートされる）。</summary>
    internal sealed class GlyphBakeDeferredException : Exception
    {
        public GlyphBakeDeferredException(string message)
            : base(message)
        {
        }
    }
}
