#nullable enable

using UnityEngine;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 字形の表現（README §1・§3.4・§4）。<c>T2FontAsset</c> の副資産で、取込器が表現の種類の焼き器に作らせる。
    ///
    /// <para>
    /// ホストは表現の具体型を知らない: 字形番号を渡して幾何（<see cref="WriteGeometry"/>）とテクスチャ（<see cref="GetTexture"/>）を
    /// 受け取り、マテリアルは <see cref="GlyphMaterialCache"/> が <see cref="Shader"/> から作る。表現を足すパッケージは派生クラスを
    /// <b>クラス名とファイル名を一致させて</b>置く（Unity が MonoScript を解決するため）。
    /// </para>
    /// </summary>
    public abstract class GlyphRepresentation : ScriptableObject
    {
        [SerializeField] private Shader? shader;
        [SerializeField] private GlyphEffects effects;

        /// <summary>表現の種類の名前（生成仕様の <c>representation.kind</c> と同じ綴り。診断と検査用）。</summary>
        public abstract string Kind { get; }

        /// <summary>字形を描くシェーダ（README §5 の契約を満たすもの）。取込器が参照を入れる。null は取込の誤り。</summary>
        public Shader? Shader => shader;

        /// <summary>描ける効果（README §6.3）。焼き器が種類の記述子から写す。</summary>
        public GlyphEffects Effects => effects;

        /// <summary>
        /// マテリアルを共有してよい範囲（README §4.5）。同じ値の表現どうしは、縁の様式が同じなら同じマテリアルを使う。既定はシェーダ
        /// （＝同じ種類の全フォントで共有）。フォント全体のテクスチャをマテリアルに載せる表現は自分を返す。
        /// </summary>
        public virtual Object? MaterialScope => shader;

        /// <summary>
        /// マテリアルへ表現の定数を入れる（距離域・フォント全体のテクスチャなど）。<see cref="GlyphMaterialCache"/> がマテリアルを作るときに
        /// 1 回だけ呼ぶ。縁の様式の uniform（README §6.1）はキャッシュが入れるので、ここでは書かない。
        /// </summary>
        public virtual void ConfigureMaterial(Material material)
        {
        }

        /// <summary>
        /// 字形のテクスチャ（ホストが <c>CanvasRenderer.SetTexture</c> で <c>_MainTex</c> に入れる・README §4.4）。墨の無い字形では
        /// 呼ばれない。
        /// </summary>
        public abstract Texture? GetTexture(int glyph);

        /// <summary>
        /// 字形の幾何を em で書く（README §4.2・§4.3）。墨の無い字形では何も書かない。UV0.zw はホストの器が書くので、表現が書くのは
        /// 位置（em）と UV0.xy だけ。描けると宣言した効果の外延（<see cref="GlyphEdgeStyle.MaxOuterExtentEm"/>）＋AA の半分を覆う（README §4.3）。
        /// </summary>
        public abstract void WriteGeometry(int glyph, in GlyphMetrics metrics, GlyphGeometry geometry);

        /// <summary>
        /// 資産の健全性の検査。<paramref name="glyphCount"/> は字の表の字形の数。問題があればその文面、無ければ null。
        /// <c>T2FontAsset.Create</c> と利用側の出荷の検査が呼ぶ。
        /// </summary>
        public virtual string? Validate(int glyphCount)
        {
            return shader == null ? $"表現 {Kind} にシェーダの参照がありません。" : null;
        }

        /// <summary>派生クラスの生成の口から呼ぶ（シェーダと描ける効果を入れる）。</summary>
        protected void InitializeRepresentation(Shader shader, GlyphEffects effects)
        {
            this.shader = shader;
            this.effects = effects;
        }
    }
}
