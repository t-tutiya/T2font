#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// フォント資産＝字の表と字形の表現（README §1・§3.1）。
    ///
    /// <para>
    /// <b>手で作らない</b>。生成仕様（<c>.t2font</c>）を取込器が取り込み、元フォントから焼いた生成物で、Library に作られる（README §2.4）。
    /// 表現とそのテクスチャはこの資産の副資産で、ほかの資産を指すのは表現のシェーダの参照だけ。
    /// </para>
    ///
    /// <para>
    /// 字は昇順の配列を二分探索で引く（約 7,000 字で比較 13 回・割り当て無し・README §3.2）。値の単位は <see cref="GlyphMetrics"/> を見よ。
    /// </para>
    /// </summary>
    public sealed class T2FontAsset : ScriptableObject
    {
        [SerializeField] private FontFaceMetrics face;
        [SerializeField] private int[] codepoints = Array.Empty<int>();
        [SerializeField] private GlyphMetrics[] glyphs = Array.Empty<GlyphMetrics>();
        [SerializeField] private int notdef;
        [SerializeField] private GlyphRepresentation? representation;
        [SerializeField] private string sourceHash = string.Empty;

        // 未収録の字を報告済みか（コードポイントごとに 1 回だけ LogError を出す）。資産の実行時の状態で、直列化しない。
        // 読み込んだ資産はドメインリロード無効の Editor 再生をまたいで生き残るので、再生の開始ごとに世代を進め、
        // 前の再生の記録を捨てる（捨てないと、2 回目以降の再生と PlayMode テストで欠字の LogError が出なくなる）。
        private static int _reportGeneration;
        [NonSerialized] private HashSet<int>? _reportedMissing;
        [NonSerialized] private int _reportedGeneration = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginReportGeneration()
        {
            _reportGeneration++;
        }

        /// <summary>フォント単位の値（縦メトリクス・em）。</summary>
        public FontFaceMetrics Face => face;

        /// <summary>収めた字（昇順）。添字が字形番号（README §3.2）。</summary>
        public IReadOnlyList<int> Codepoints => codepoints;

        /// <summary>字形の数（cmap を持たない字形を含む）。</summary>
        public int GlyphCount => glyphs.Length;

        /// <summary>代替字形（元フォントの glyph 0）の字形番号。</summary>
        public int Notdef => notdef;

        /// <summary>字形の表現（README §3.4）。取込が作った資産では null にならない。</summary>
        public GlyphRepresentation Representation =>
            representation != null ? representation : throw new InvalidOperationException($"フォント '{name}' に表現がありません。取り込み直してください。");

        /// <summary>生成元（生成器の版数・生成仕様・元フォント）のハッシュ。</summary>
        public string SourceHash => sourceHash;

        /// <summary>
        /// 取込器が焼いた結果から資産を作る（取込器専用。既存の資産を書き換える経路は持たない）。字が昇順でない・字形番号が
        /// 字の表の外を指す・表現が字の表と合わないときは失敗する（README §2.1）。
        /// </summary>
        /// <exception cref="ArgumentException">字の表か表現が不正。</exception>
        public static T2FontAsset Create(
            FontFaceMetrics face, int[] codepoints, GlyphMetrics[] glyphs, int notdef,
            GlyphRepresentation representation, string sourceHash)
        {
            if (codepoints == null)
            {
                throw new ArgumentNullException(nameof(codepoints));
            }
            if (glyphs == null)
            {
                throw new ArgumentNullException(nameof(glyphs));
            }
            if (representation == null)
            {
                throw new ArgumentNullException(nameof(representation));
            }
            if (sourceHash == null)
            {
                throw new ArgumentNullException(nameof(sourceHash));
            }
            if (!GlyphTable.IsStrictlyAscending(codepoints))
            {
                throw new ArgumentException("字が昇順になっていません。", nameof(codepoints));
            }
            if (glyphs.Length < codepoints.Length)
            {
                throw new ArgumentException(
                    $"字形の記録（{glyphs.Length} 個）が字（{codepoints.Length} 個）より少ない。", nameof(glyphs));
            }
            if (notdef < codepoints.Length || notdef >= glyphs.Length)
            {
                throw new ArgumentException(
                    $"代替字形の番号 {notdef} が cmap を持たない字形の範囲（{codepoints.Length}〜{glyphs.Length - 1}）の外です。", nameof(notdef));
            }
            var problem = representation.Validate(glyphs.Length);
            if (problem != null)
            {
                throw new ArgumentException(problem, nameof(representation));
            }

            var asset = CreateInstance<T2FontAsset>();
            asset.face = face;
            asset.codepoints = codepoints;
            asset.glyphs = glyphs;
            asset.notdef = notdef;
            asset.representation = representation;
            asset.sourceHash = sourceHash;
            return asset;
        }

        /// <summary><paramref name="codepoint"/> の字を収録しているか。</summary>
        public bool Contains(int codepoint)
        {
            return GlyphTable.IndexOf(codepoints, codepoint) >= 0;
        }

        /// <summary>
        /// 字の字形番号（README §3.1）。収録していない字は代替字形を返して <paramref name="isSubstitute"/> を真にし、コードポイントごとに
        /// 1 回 <c>LogError</c> を出す——送り幅 0 で消すと、出荷物で字が黙って消える（README §2.1）。
        /// </summary>
        public int Resolve(int codepoint, out bool isSubstitute)
        {
            var index = GlyphTable.IndexOf(codepoints, codepoint);
            if (index >= 0)
            {
                isSubstitute = false;
                return index;
            }

            if (_reportedMissing == null || _reportedGeneration != _reportGeneration)
            {
                _reportedMissing = new HashSet<int>();
                _reportedGeneration = _reportGeneration;
            }
            if (_reportedMissing.Add(codepoint))
            {
                Debug.LogError(
                    $"[T2Font] '{name}' に字 {TextCodepoints.Display(codepoint)}（U+{codepoint:X4}）がありません。代替字形で描きます。"
                    + "実行時に組み立てた文字列か、生成仕様の字集合の外の字です。",
                    this);
            }
            isSubstitute = true;
            return notdef;
        }

        /// <summary>字形 <paramref name="glyph"/> の字の表の記録（送り幅と墨の外接・em）。</summary>
        public GlyphMetrics Metrics(int glyph)
        {
            return glyphs[glyph];
        }

        /// <summary>
        /// 字形 <paramref name="glyph"/> を描く表現（README §3.1）。今の版では常に <see cref="Representation"/>。1 フォントに複数の表現を
        /// 持たせる拡張の口なので、ホストは字形ごとにこれを通して引く。
        /// </summary>
        public GlyphRepresentation RepresentationOf(int glyph)
        {
            if ((uint)glyph >= (uint)glyphs.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(glyph), $"字形番号 {glyph} が字の表（{glyphs.Length} 個）の外です。");
            }
            return Representation;
        }
    }
}
