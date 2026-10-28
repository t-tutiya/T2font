using info.someiyoshino.T2Font;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace T2font.EditModeTests
{
    /// <summary>EditMode のテストが読む、取込済みのフォント資産。</summary>
    internal static class TestFontAssets
    {
        /// <summary>検証用の M+ 1c を SDF で焼いた資産の生成仕様。</summary>
        public const string Mplus1cSdfPath = "Assets/Content/Fonts/mplus-1c-sdf.t2font";

        /// <summary>検証用の M+ 1c（SDF）。取込が失敗していればその旨で落とす。</summary>
        public static T2FontAsset Mplus1cSdf()
        {
            var font = AssetDatabase.LoadAssetAtPath<T2FontAsset>(Mplus1cSdfPath);
            Assert.That(font, Is.Not.Null, $"{Mplus1cSdfPath} の取込が失敗しています（Console の取込の誤りを見る）。");
            return font;
        }

        /// <summary>同梱の SDF のシェーダ。</summary>
        public static Shader SdfShader()
        {
            var shader = Shader.Find("T2Font/SDF");
            Assert.That(shader, Is.Not.Null, "シェーダ T2Font/SDF が見つかりません。");
            return shader;
        }
    }
}
