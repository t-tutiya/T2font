#nullable enable

using System;
using System.Collections.Generic;
using info.someiyoshino.T2Font;
using UnityEngine;
using Object = UnityEngine.Object;

namespace info.someiyoshino.Rinkaku
{
    /// <summary>
    /// Rinkaku のグリフ表現（README §3.4・§3.5）。グリフごとのセル（<see cref="RinkakuGlyphCell"/>）と、データのページ（RGBA8・幅 4096）を持つ。
    ///
    /// <para>
    /// quad はセルの plane（インク境界＋外延・em）。<b>UV0.xy はインデックスとセグメントテーブルの番地</b>で、quad の 4 頂点で同じ値を持つ: x＝インデックスのずれ <c>K</c>
    /// （<see cref="RinkakuEncoding.PackedOffset"/>）、y＝セグメントテーブルの番地（<see cref="RinkakuEncoding.PackedDataBase"/>・ワイドインデックスは負）。どちらも
    /// 2^24 未満の整数なので float で正確に持て、シェーダは補間しない（<c>nointerpolation</c>）で受ける。
    /// </para>
    ///
    /// <para>
    /// グリッドの細かさ（<see cref="IndexEm"/>）はフォントごとの値でマテリアルに載せるので、マテリアルを共有してよい範囲はグリフ表現自身
    /// （<see cref="MaterialScope"/>）。テクスチャはフォントの境で替わるので、バッチの割れ方は SDF と同じ。
    /// </para>
    /// </summary>
    public sealed class RinkakuRepresentation : GlyphRepresentation
    {
        private static readonly int IndexEmId = Shader.PropertyToID("_IndexEm");

        [SerializeField] private int indexEm;
        [SerializeField] private RinkakuGlyphCell[] cells = Array.Empty<RinkakuGlyphCell>();
        [SerializeField] private Texture2D[] pages = Array.Empty<Texture2D>();

        public override string Kind => RinkakuEncoding.KindName;

        /// <summary>グリッドの 1em のセルの数（ベイク仕様の <c>indexEm</c>）。</summary>
        public int IndexEm => indexEm;

        /// <summary>グリフインデックス順のセル。</summary>
        public IReadOnlyList<RinkakuGlyphCell> Cells => cells;

        /// <summary>データのページ（RGBA8・幅 4096・point・linear・ミップ無し）。</summary>
        public IReadOnlyList<Texture2D> Pages => pages;

        /// <summary>グリッドの細かさをマテリアルに載せるので、フォントごとにマテリアルを分ける。</summary>
        public override Object? MaterialScope => this;

        /// <summary>
        /// ベイカーがベイクした結果からグリフ表現を作る（ベイカー専用）。<paramref name="effects"/> は種類の記述子の宣言を写す。健全性は
        /// <c>T2FontAsset.Create</c> が <see cref="Validate"/> で検査する。
        /// </summary>
        public static RinkakuRepresentation Create(Shader shader, GlyphEffects effects, int indexEm, RinkakuGlyphCell[] cells, Texture2D[] pages)
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

            var representation = CreateInstance<RinkakuRepresentation>();
            representation.InitializeRepresentation(shader, effects);
            representation.indexEm = indexEm;
            representation.cells = cells;
            representation.pages = pages;
            return representation;
        }

        public override void ConfigureMaterial(Material material)
        {
            material.SetFloat(IndexEmId, indexEm);
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

            // セルの plane（グリッドが覆う範囲）を、インク境界から quadExtentEm だけ広げた矩形へ狭める。インデックスとセグメントテーブルの番地は変わらない
            //（狭い quad はインデックスの内側の部分だけを引く）。
            var left = cell.planeLeft;
            var bottom = cell.planeBottom;
            var right = cell.planeRight;
            var top = cell.planeTop;
            NarrowToInk(metrics, quadExtentEm, ref left, ref bottom, ref right, ref top);

            float offset = RinkakuEncoding.PackedOffset(cell);
            float dataBase = RinkakuEncoding.PackedDataBase(cell);
            geometry.AddQuad(left, bottom, right, top, offset, dataBase, offset, dataBase);
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
                return $"Rinkaku のセル（{cells.Length} 個）がタイポグラフィテーブルのグリフ（{glyphCount} 個）と合いません。";
            }
            if (indexEm < 1)
            {
                return $"Rinkaku の indexEm（{indexEm}）が正ではありません。";
            }
            for (var i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page == null)
                {
                    return $"Rinkaku のページ {i} がありません。";
                }
                if (page.width != RinkakuEncoding.PageWidth || page.height > RinkakuEncoding.MaxPageHeight || page.format != TextureFormat.RGBA32
                    || page.mipmapCount != 1 || page.filterMode != FilterMode.Point)
                {
                    return $"Rinkaku のページ {i} の形式が違います（{page.width}×{page.height}・{page.format}・ミップ {page.mipmapCount}・{page.filterMode}）。";
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
                if (cell.blockX < 0 || cell.blockY < 0 || cell.blockX + RinkakuEncoding.BlockWidth(cell) > page.width
                    || cell.blockY + cell.gridHeight > page.height)
                {
                    return $"グリフ {glyph} のインデックスブロックがページ {cell.page}（{page.width}×{page.height}）からはみ出しています。";
                }
                if (cell.dataBase <= 0 || cell.dataBase >= page.width * page.height)
                {
                    return $"グリフ {glyph} のセグメントテーブルの番地 {cell.dataBase} がページ {cell.page} の外です。";
                }
                if (RinkakuEncoding.PackedOffset(cell) < 0)
                {
                    return $"グリフ {glyph} のインデックスのずれが UV に詰められる範囲の外です。";
                }
            }
            return null;
        }
    }
}
