#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 同梱の SDF のグリフ表現（README §9）。グリフごとのセル（<see cref="SdfGlyphCell"/>）と、単チャネルの距離のページを持つ。
    ///
    /// <para>
    /// quad はセルの plane（インク境界＋距離レンジの余白・em）で、UV0.xy は同じ矩形のページ上の px ÷ ページの寸法。ページは左下原点で、Unity の
    /// UV と同じ向き。距離レンジは全フォント共通（<see cref="SdfEncoding.RangeEm"/>）なので、マテリアルはフォントに依らず、全 SDF フォントが
    /// エッジスタイルごとに 1 つのマテリアルを共有する（<see cref="GlyphRepresentation.MaterialScope"/> は既定のシェーダのまま・README §4.5）。
    /// </para>
    /// </summary>
    public sealed class SdfRepresentation : GlyphRepresentation
    {
        private static readonly int RangeEmId = Shader.PropertyToID("_RangeEm");

        [SerializeField] private float emPx;
        [SerializeField] private SdfGlyphCell[] cells = Array.Empty<SdfGlyphCell>();
        [SerializeField] private Texture2D[] pages = Array.Empty<Texture2D>();

        public override string Kind => SdfEncoding.KindName;

        /// <summary>ページ上の 1em の px（ベイク仕様の <c>emPx</c>）。</summary>
        public float EmPx => emPx;

        /// <summary>グリフインデックス順のセル。</summary>
        public IReadOnlyList<SdfGlyphCell> Cells => cells;

        /// <summary>距離のページ（単チャネル・linear・ミップ無し）。</summary>
        public IReadOnlyList<Texture2D> Pages => pages;

        /// <summary>
        /// ベイカーがベイクした結果からグリフ表現を作る（ベイカー専用）。<paramref name="effects"/> は種類の記述子の宣言を写す（README §6.3）。
        /// 健全性は <c>T2FontAsset.Create</c> が <see cref="Validate"/> で検査する。
        /// </summary>
        public static SdfRepresentation Create(
            Shader shader, GlyphEffects effects, float emPx, SdfGlyphCell[] cells, Texture2D[] pages)
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }
            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }

            var representation = CreateInstance<SdfRepresentation>();
            representation.InitializeRepresentation(shader, effects);
            representation.emPx = emPx;
            representation.cells = cells;
            representation.pages = pages;
            return representation;
        }

        public override void ConfigureMaterial(Material material)
        {
            material.SetFloat(RangeEmId, SdfEncoding.RangeEm);
        }

        public override Texture? GetTexture(int glyph)
        {
            var cell = cells[glyph];
            return cell.IsEmpty ? null : pages[cell.page];
        }

        public override void WriteGeometry(int glyph, in GlyphMetrics metrics, float quadExtentEm, GlyphGeometry geometry)
        {
            var cell = cells[glyph];
            if (cell.IsEmpty)
            {
                return;
            }

            // セルの矩形（plane）を、インク境界から quadExtentEm だけ広げた矩形へ狭める。ページの UV は plane の上で線形なので、同じ割合で取り直す。
            // 距離は texel の間を bilinear で埋めた近似で、角の外側では真の距離より近く出る（縁が texel の大きさで外へ膨らむ）。ホストが渡す幅は
            // 真の輪郭からの幅なので、ベイクの 1 texel（1/emPx em）を足して覆う——足さないと、ベイクより大きく描いた字の縁が quad の縁で断たれる。
            var left = cell.planeLeft;
            var bottom = cell.planeBottom;
            var right = cell.planeRight;
            var top = cell.planeTop;
            NarrowToInk(metrics, quadExtentEm + (1f / emPx), ref left, ref bottom, ref right, ref top);

            var page = pages[cell.page];
            float width = page.width;
            float height = page.height;
            var texelsPerEmX = (cell.atlasRight - cell.atlasLeft) / (cell.planeRight - cell.planeLeft);
            var texelsPerEmY = (cell.atlasTop - cell.atlasBottom) / (cell.planeTop - cell.planeBottom);
            geometry.AddQuad(
                left, bottom, right, top,
                (cell.atlasLeft + ((left - cell.planeLeft) * texelsPerEmX)) / width,
                (cell.atlasBottom + ((bottom - cell.planeBottom) * texelsPerEmY)) / height,
                (cell.atlasLeft + ((right - cell.planeLeft) * texelsPerEmX)) / width,
                (cell.atlasBottom + ((top - cell.planeBottom) * texelsPerEmY)) / height);
        }

        public override string? Validate(int glyphCount)
        {
            var problem = base.Validate(glyphCount);
            if (problem != null)
            {
                return problem;
            }
            if (cells.Length != glyphCount)
            {
                return $"SDF のセル（{cells.Length} 個）がタイポグラフィテーブルのグリフ（{glyphCount} 個）と合いません。";
            }
            for (var i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null)
                {
                    return $"SDF のページ {i} がありません。";
                }
            }
            for (var glyph = 0; glyph < cells.Length; glyph++)
            {
                var cell = cells[glyph];
                if (cell.IsEmpty)
                {
                    continue;
                }
                if (cell.page < 0 || cell.page >= pages.Length)
                {
                    return $"グリフ {glyph} のページ番号 {cell.page} がページの外です（{pages.Length} 枚）。";
                }
                var page = pages[cell.page];
                if (cell.atlasLeft < 0 || cell.atlasBottom < 0 || cell.atlasRight > page.width || cell.atlasTop > page.height)
                {
                    return $"グリフ {glyph} のセルがページ {cell.page}（{page.width}×{page.height}）からはみ出しています。";
                }
            }
            return null;
        }
    }
}
