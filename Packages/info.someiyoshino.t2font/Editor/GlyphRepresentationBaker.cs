#nullable enable

using info.someiyoshino.T2Font.Bake;

namespace info.someiyoshino.T2Font.Editor
{
    /// <summary>
    /// 表現の種類のベイカー（README §7.4）。タイポグラフィテーブルと設定から表現のサブアセット（とテクスチャ）を作る。
    ///
    /// <para>
    /// インポーターが Editor の <c>TypeCache</c> で派生クラスを集め、引数の無い公開の構築子で作る。種類 1 つにベイカーは 1 つ。
    /// <b>同じ入力から出る出力が変わる変更をしたら、種類の記述子のバージョン（<see cref="GlyphRepresentationKind.Version"/>）を上げる</b>
    /// ——その種類のフォントだけが再インポートされる（README §7.5）。
    /// </para>
    /// </summary>
    public abstract class GlyphRepresentationBaker
    {
        /// <summary>ベイクする種類の記述子。</summary>
        public abstract GlyphRepresentationKind Kind { get; }

        /// <summary>
        /// 表現を作る。<paramref name="settings"/> は <see cref="Kind"/> が検査した設定。テクスチャなどのサブアセットは
        /// <see cref="GlyphBakeContext.AddObject"/> で渡す。誤りは <see cref="FontBakeException"/> で止める（アセットは作られない）。
        /// 返す表現の <see cref="GlyphRepresentation.Effects"/> は <see cref="Kind"/> の宣言と同じにする（インポーターが検査する）。
        /// </summary>
        public abstract GlyphRepresentation Bake(GlyphBakeContext context, FontFaceData face, GlyphRepresentationSettings settings);
    }
}
