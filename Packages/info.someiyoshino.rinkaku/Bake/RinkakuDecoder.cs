#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.Rinkaku.Bake
{
    /// <summary>
    /// ページのバイトから、シェーダと同じ手順で画素の符号付き距離を求める参照の実装（倍精度・README §3）。形式（インデックス・レコード・セグメントテーブルの詰め方）の
    /// 正しさを確かめる検査と、GPU の描画と画素ごとに比べる診断が使う。シェーダはこの手順を float で写す。
    /// </summary>
    public static class RinkakuDecoder
    {
        /// <summary>
        /// グリフの点 (px, py)（em）の符号付き距離（インク領域の内側が正）。<paramref name="evaluated"/> は評価したセグメントの数（1 画素の評価本数）。
        /// 点は quad（セルの plane）の中にあること。
        /// </summary>
        public static double SignedDistance(RinkakuAtlas atlas, int glyph, double px, double py, out int evaluated)
        {
            var cell = atlas.Cells[glyph];
            var data = atlas.Pages[cell.page].Data;
            var items = Items(data, cell, atlas.IndexEm, px, py);

            evaluated = items.Count;
            var best = double.MaxValue;
            var inside = false;
            foreach (var item in items)
            {
                var entry = item - 1;
                ReadEntry(data, cell.dataBase + (2 * entry), out var x0, out var y0, out var cx, out var cy, out var n0x, out var n0y);
                ReadEntry(data, cell.dataBase + (2 * entry) + 2, out var x1, out var y1, out _, out _, out var n1x, out var n1y);
                // シェーダは直線も 2 次ベジエとして解く。ここでは同じ点の集まりを同じ判定で直線にしてから測る（距離は同じ）。
                var segment = new DecodedSegment(entry, GlyphEncoder.DecodedCurve(x0, y0, cx, cy, x1, y1), n0x, n0y, n1x, n1y);
                var d = segment.Curve.DistanceSquared(px, py, out var t);
                if (d < best)
                {
                    best = d;
                    inside = segment.IsInside(px, py, t);
                }
            }
            var distance = Math.Sqrt(best);
            return inside ? distance : -distance;
        }

        /// <summary>点が属するグリッドセルの項目番号の列（レコードを含む）。</summary>
        public static List<int> Items(byte[] data, RinkakuGlyphCell cell, int indexEm, double px, double py)
        {
            var wide = cell.wide;
            var cellX = (int)Math.Floor(px * indexEm);
            var tx = RinkakuEncoding.TexelColumn(cellX, wide) + cell.blockX
                - RinkakuEncoding.TexelColumn(RinkakuEncoding.BlockOriginCell(cell), wide);
            var ty = (int)Math.Floor(py * indexEm) + cell.blockY - cell.gridY;
            // 番号 1 つのバイト数。ナローインデックスは texel の前半が偶数のセル、後半が奇数のセル。
            var unit = wide ? 2 : 1;
            var o = Texel(tx, ty) + (wide ? 0 : (cellX & 1) * 2);
            var a = Number(data, o, wide);
            var b = Number(data, o + unit, wide);
            var items = new List<int>();
            if (RinkakuEncoding.IsRecordMarker(b, wide, out var list, out var high))
            {
                var record = (cell.dataBase - (a + (high << RinkakuEncoding.RecordLowBits(wide)))) * 4;
                if (list)
                {
                    var count = Number(data, record, wide);
                    for (var k = 0; k < count; k++)
                    {
                        items.Add(Number(data, record + ((1 + k) * unit), wide));
                    }
                }
                else
                {
                    for (var k = 0; k < RinkakuEncoding.ItemsInRecord; k++)
                    {
                        var item = Number(data, record + (k * unit), wide);
                        if (item != 0)
                        {
                            items.Add(item);
                        }
                    }
                }
            }
            else
            {
                if (a != 0)
                {
                    items.Add(a);
                }
                if (b != 0)
                {
                    items.Add(b);
                }
            }
            return items;
        }

        private static int Number(byte[] data, int offset, bool wide)
        {
            return wide ? data[offset] | (data[offset + 1] << 8) : data[offset];
        }

        /// <summary>セグメントテーブルの項目（線形の番地）を読む: 始点・制御点（em）と擬似法線（単位）。</summary>
        public static void ReadEntry(
            byte[] data, int address, out double x, out double y, out double cx, out double cy, out double nx, out double ny)
        {
            var o = address * 4;
            x = GlyphEncoder.DecodeCoordinate(data[o] | (data[o + 1] << 8));
            y = GlyphEncoder.DecodeCoordinate(data[o + 2] | (data[o + 3] << 8));
            var dx = data[o + 4] | ((data[o + 5] & 0xF) << 8);
            var dy = (data[o + 5] >> 4) | (data[o + 6] << 4);
            cx = x + GlyphEncoder.DecodeControl(SignExtend12(dx));
            cy = y + GlyphEncoder.DecodeControl(SignExtend12(dy));
            (nx, ny) = GlyphEncoder.DecodeAngle(data[o + 7]);
        }

        private static int SignExtend12(int v)
        {
            return v >= 2048 ? v - 4096 : v;
        }

        // 2 次元の texel をバイトの位置へ（RGBA8・行 0 が下端・幅 4096）。
        private static int Texel(int x, int y)
        {
            return ((y * RinkakuEncoding.PageWidth) + x) * 4;
        }
    }
}
