#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// フォントアセット＝タイポグラフィテーブルとグリフの表現（README §1・§3.1）。
    ///
    /// <para>
    /// <b>手で作らない</b>。ベイク仕様（<c>.t2font</c>）をインポーターがインポートし、元フォントからベイクした生成物で、Library に作られる（README §2.4）。
    /// 表現とそのテクスチャはこのアセットのサブアセットで、ほかのアセットを指すのは表現のシェーダの参照だけ。
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

        // 未収録の字を報告済みか（コードポイントごとに 1 回だけ LogError を出す）。アセットの実行時の状態で、直列化しない。
        // 読み込んだアセットはドメインリロード無効の Editor 再生をまたいで生き残るので、再生の開始ごとに世代を進め、
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

        /// <summary>収めた字（昇順）。添字がグリフインデックス（README §3.2）。</summary>
        public IReadOnlyList<int> Codepoints => codepoints;

        /// <summary>グリフの数（cmap を持たないグリフを含む）。</summary>
        public int GlyphCount => glyphs.Length;

        /// <summary>代替グリフ（元フォントの glyph 0）のグリフインデックス。</summary>
        public int Notdef => notdef;

        /// <summary>グリフの表現（README §3.4）。インポートが作ったアセットでは null にならない。</summary>
        public GlyphRepresentation Representation =>
            representation != null ? representation : throw new InvalidOperationException($"フォント '{name}' に表現がありません。再インポートしてください。");

        /// <summary>生成元（生成器のバージョン・ベイク仕様・元フォント）のハッシュ。</summary>
        public string SourceHash => sourceHash;

        /// <summary>
        /// インポーターがベイクした結果からアセットを作る（インポーター専用。既存のアセットを書き換える経路は持たない）。字が昇順でない・グリフインデックスが
        /// タイポグラフィテーブルの外を指す・表現がタイポグラフィテーブルと合わないときは失敗する（README §2.1）。
        /// </summary>
        /// <exception cref="ArgumentException">タイポグラフィテーブルか表現が不正。</exception>
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
                    $"グリフの記録（{glyphs.Length} 個）が字（{codepoints.Length} 個）より少ない。", nameof(glyphs));
            }
            if (notdef < codepoints.Length || notdef >= glyphs.Length)
            {
                throw new ArgumentException(
                    $"代替グリフのグリフインデックス {notdef} が cmap を持たないグリフの範囲（{codepoints.Length}〜{glyphs.Length - 1}）の外です。", nameof(notdef));
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
        /// 字のグリフインデックス（README §3.1）。収録していない字は代替グリフを返して <paramref name="isSubstitute"/> を真にし、コードポイントごとに
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
                    $"[T2Font] '{name}' に字 {TextCodepoints.Display(codepoint)}（U+{codepoint:X4}）がありません。代替グリフで描きます。"
                    + "実行時に組み立てた文字列か、ベイク仕様の文字セットの外の字です。",
                    this);
            }
            isSubstitute = true;
            return notdef;
        }

        /// <summary>グリフ <paramref name="glyph"/> のタイポグラフィテーブルの記録（送り幅とインク境界・em）。</summary>
        public GlyphMetrics Metrics(int glyph)
        {
            return glyphs[glyph];
        }

        /// <summary>
        /// グリフ <paramref name="glyph"/> を描く表現（README §3.1）。今の版では常に <see cref="Representation"/>。1 フォントに複数の表現を
        /// 持たせる拡張の口なので、ホストはグリフごとにこれを通して引く。
        /// </summary>
        public GlyphRepresentation RepresentationOf(int glyph)
        {
            if ((uint)glyph >= (uint)glyphs.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(glyph), $"グリフインデックス {glyph} がタイポグラフィテーブル（{glyphs.Length} 個）の外です。");
            }
            return Representation;
        }
    }
}
