#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 字形のマテリアルの置き場（README §4.5）。（表現の <see cref="GlyphRepresentation.MaterialScope"/>・縁の様式）ごとに 1 つ作って共有する。
    ///
    /// <para>
    /// 作る時点は<b>フォントを読んだ時点</b>にするのがよい（<see cref="Prewarm"/>）——描画中に作るとヒッチになる。面の色とフェードは頂点色で
    /// マテリアルに持たせないので（README §5 S2）、色違いの様式はマテリアルを共有する。テクスチャは <c>CanvasRenderer.SetTexture</c> で字ごとに
    /// 渡すので、<c>MaterialScope</c> がシェーダの表現（SDF など）ではフォントが違ってもマテリアルを共有する。
    /// </para>
    ///
    /// <para>
    /// マテリアルは Unity のネイティブの資産で、参照を手放しても GC では回収されない。所有者は破棄のときに <see cref="Dispose"/> を呼び、
    /// フォントを外すときは <see cref="Release"/> でそのフォントに属するマテリアルを捨てる。
    /// </para>
    /// </summary>
    public sealed class GlyphMaterialCache : IDisposable
    {
        private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
        private static readonly int FaceSoftnessId = Shader.PropertyToID("_FaceSoftness");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineSoftnessId = Shader.PropertyToID("_OutlineSoftness");

        private readonly Dictionary<Key, Material> _materials = new Dictionary<Key, Material>();

        /// <summary>持っているマテリアルの数。</summary>
        public int Count => _materials.Count;

        /// <summary>
        /// <paramref name="representation"/> で <paramref name="style"/> を描くマテリアル。無ければ作る。様式が範囲の外か、表現の描けない
        /// 効果を使うなら止める（README §6.1・§6.3）——黙って近似すると、表現によって見た目が変わる。
        /// </summary>
        /// <exception cref="ArgumentException">様式が範囲の外か、表現が描けない効果を使う。</exception>
        public Material Get(GlyphRepresentation representation, in GlyphEdgeStyle style)
        {
            if (representation == null)
            {
                throw new ArgumentNullException(nameof(representation));
            }
            var scope = representation.MaterialScope;
            if (scope == null)
            {
                throw new ArgumentException($"表現 {representation.Kind}（'{representation.name}'）にシェーダの参照がありません。", nameof(representation));
            }

            var key = new Key(scope, style);
            if (_materials.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var problem = Check(representation, style);
            if (problem != null)
            {
                throw new ArgumentException(problem, nameof(style));
            }

            var material = new Material(representation.Shader)
            {
                name = $"T2Font {representation.Kind} (runtime)",
                hideFlags = HideFlags.DontSave,
            };
            representation.ConfigureMaterial(material);
            WriteEdgeStyle(material, style);
            _materials[key] = material;
            return material;
        }

        /// <summary>
        /// <paramref name="representation"/> で <paramref name="styles"/> を描くマテリアルを先に作る（フォントを読んだ時点で 1 回）。
        /// </summary>
        public void Prewarm(GlyphRepresentation representation, IEnumerable<GlyphEdgeStyle> styles)
        {
            if (styles == null)
            {
                throw new ArgumentNullException(nameof(styles));
            }
            foreach (var style in styles)
            {
                Get(representation, style);
            }
        }

        /// <summary>
        /// 様式を表現で描けるか。描けなければ理由の文面、描けるなら null（README §6.3）。ホストが様式をフォントに使う前の検査に使う。
        /// </summary>
        public static string? Check(GlyphRepresentation representation, in GlyphEdgeStyle style)
        {
            if (representation == null)
            {
                throw new ArgumentNullException(nameof(representation));
            }
            var outOfRange = style.FindOutOfRange();
            if (outOfRange != null)
            {
                return $"様式の値が範囲の外です: {outOfRange}。";
            }
            var missing = style.Uses & ~representation.Effects;
            if (missing != GlyphEffects.None)
            {
                return $"表現 {representation.Kind}（'{representation.name}'）は効果 {missing} を描けません（描ける効果: {representation.Effects}）。";
            }
            return null;
        }

        /// <summary><paramref name="scope"/>（表現の <see cref="GlyphRepresentation.MaterialScope"/>）に属するマテリアルを捨てる。</summary>
        public void Release(Object scope)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }
            var released = new List<Key>();
            foreach (var pair in _materials)
            {
                if (ReferenceEquals(pair.Key.Scope, scope))
                {
                    released.Add(pair.Key);
                    DestroyMaterial(pair.Value);
                }
            }
            foreach (var key in released)
            {
                _materials.Remove(key);
            }
        }

        /// <summary>全マテリアルを捨てる（所有者の破棄のときに呼ぶ）。</summary>
        public void Dispose()
        {
            foreach (var material in _materials.Values)
            {
                DestroyMaterial(material);
            }
            _materials.Clear();
        }

        // 縁の値は em のまま 1 対 1 で uniform へ入れる（README §6.1）。縁取りの色は sRGB で持つので、シェーダが linear で合成する
        // プロジェクトではここで明示的に直して Vector で渡す（Vector は色空間の変換を受けない＝プロパティの属性に頼る暗黙の変換を
        // 持たない）。α は不透明度なので直さない。
        private static void WriteEdgeStyle(Material material, in GlyphEdgeStyle style)
        {
            material.SetFloat(FaceDilateId, style.FaceDilate);
            material.SetFloat(FaceSoftnessId, style.FaceSoftness);
            var outline = new Color(style.ColorR, style.ColorG, style.ColorB, style.ColorA);
            material.SetVector(OutlineColorId, QualitySettings.activeColorSpace == ColorSpace.Linear ? outline.linear : outline);
            material.SetFloat(OutlineWidthId, style.OutlineWidth);
            material.SetFloat(OutlineSoftnessId, style.OutlineSoftness);
        }

        // Editor の再生外（検証ビューは Edit モードでも描く）では Destroy が使えない。
        private static void DestroyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }

        // 範囲は参照の同一性で比べる（UnityEngine.Object の == は破棄済みを null と見なすので、破棄後に鍵が引けなくなる）。
        private readonly struct Key : IEquatable<Key>
        {
            public Key(Object scope, GlyphEdgeStyle style)
            {
                Scope = scope;
                Style = style;
            }

            public Object Scope { get; }

            public GlyphEdgeStyle Style { get; }

            public bool Equals(Key other)
            {
                return ReferenceEquals(Scope, other.Scope) && Style == other.Style;
            }

            public override bool Equals(object? obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(RuntimeHelpers.GetHashCode(Scope), Style);
            }
        }
    }
}
