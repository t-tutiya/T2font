#nullable enable

using System;
using System.Collections.Generic;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// TrueType（<c>glyf</c> 輪郭）フォントの読取器（README §7.3）。
    ///
    /// <para>
    /// 読むのは字の表と輪郭に要る表だけ——<c>head</c>（em の単位・<c>loca</c> の形式）・<c>hhea</c>（縦メトリクス）・
    /// <c>maxp</c>（字形数）・<c>hmtx</c>（送り幅）・<c>cmap</c>（字 → 字形番号）・<c>loca</c>／<c>glyf</c>（輪郭）。
    /// 字形の置換（<c>GSUB</c>）・位置調整（<c>GPOS</c>）・ヒンティング命令は読まない（字の表は置換を持たず、
    /// 表現はどれも輪郭そのものから作るのでヒンティングが要らない）。全表現が同じ読取器を通る（README §2.3）。
    /// </para>
    ///
    /// <para>
    /// <b>CFF 輪郭（<c>OTTO</c>＝多くの OTF）は未対応</b>で、渡されたら明確失敗する（空の字を黙って作らない・
    /// README §2.1）。
    /// </para>
    ///
    /// <para>
    /// 構築後は状態を変えないので、生成器の並列処理から同時に引いてよい。
    /// </para>
    /// </summary>
    public sealed class TrueTypeFont
    {
        private const uint TagTrueTypeVersion = 0x00010000;
        private const uint TagTrue = 0x74727565;  // 'true'（Apple の TrueType）
        private const uint TagOtto = 0x4F54544F;  // 'OTTO'（CFF 輪郭）
        private const uint TagTtcf = 0x74746366;  // 'ttcf'（フォントコレクション）

        // 複合字形の入れ子の上限。仕様上の上限は無いが、循環参照した壊れたフォントで無限再帰しないための柵。
        private const int MaxCompositeDepth = 16;

        private readonly byte[] _data;
        private readonly int _glyfOffset;
        private readonly int _glyfLength;
        private readonly int _locaOffset;
        private readonly bool _locaLong;
        private readonly int _hmtxOffset;
        private readonly int _numberOfHMetrics;
        private readonly Dictionary<int, int> _cmap;
        private readonly int[] _codepoints;

        /// <summary>
        /// 単体のフォントを読む。フォントコレクション（TTC）は読まない——生成仕様は書体の番号を持たないので、読めば
        /// どの書体を焼くかを黙って決めることになる（README §2.1）。
        /// </summary>
        /// <exception cref="NotSupportedException">CFF 輪郭のフォント・フォントコレクション。</exception>
        /// <exception cref="FormatException">必要な表が無い・表が範囲外を指すなど、壊れたフォント。</exception>
        public TrueTypeFont(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));

            var sfntVersion = ReadU32(0);
            if (sfntVersion == TagTtcf)
            {
                throw new NotSupportedException(
                    "フォントコレクション（TTC）は未対応です。単体のフォント（.ttf）を指定してください（生成仕様は書体の番号を持ちません）。");
            }
            if (sfntVersion == TagOtto)
            {
                throw new NotSupportedException(
                    "CFF 輪郭（OTTO）のフォントは未対応です。TrueType 輪郭（glyf）のフォントを使ってください。");
            }
            if (sfntVersion != TagTrueTypeVersion && sfntVersion != TagTrue)
            {
                throw new FormatException($"TrueType フォントではありません（sfnt version 0x{sfntVersion:X8}）。");
            }

            var tables = ReadTableDirectory();
            var head = RequireTable(tables, "head", 54);
            var hhea = RequireTable(tables, "hhea", 36);
            var maxp = RequireTable(tables, "maxp", 6);
            var hmtx = RequireTable(tables, "hmtx", 4);
            var cmap = RequireTable(tables, "cmap", 4);
            var loca = RequireTable(tables, "loca", 2);
            var glyf = RequireTable(tables, "glyf", 0);

            UnitsPerEm = ReadU16(head.Offset + 18);
            if (UnitsPerEm == 0)
            {
                throw new FormatException("head.unitsPerEm が 0 です。");
            }
            _locaLong = ReadI16(head.Offset + 50) != 0;

            Ascender = ReadI16(hhea.Offset + 4);
            Descender = ReadI16(hhea.Offset + 6);
            LineGap = ReadI16(hhea.Offset + 8);
            _numberOfHMetrics = ReadU16(hhea.Offset + 34);

            GlyphCount = ReadU16(maxp.Offset + 4);
            if (_numberOfHMetrics == 0 || _numberOfHMetrics > GlyphCount)
            {
                throw new FormatException($"hhea.numberOfHMetrics（{_numberOfHMetrics}）が字形数（{GlyphCount}）と合いません。");
            }
            if (hmtx.Length < (4 * _numberOfHMetrics) + (2 * (GlyphCount - _numberOfHMetrics)))
            {
                throw new FormatException("hmtx 表が字形数に対して短すぎます。");
            }
            _hmtxOffset = hmtx.Offset;

            var locaEntries = GlyphCount + 1;
            if (loca.Length < locaEntries * (_locaLong ? 4 : 2))
            {
                throw new FormatException("loca 表が字形数に対して短すぎます。");
            }
            _locaOffset = loca.Offset;
            _glyfOffset = glyf.Offset;
            _glyfLength = glyf.Length;

            _cmap = ReadCharacterMap(cmap.Offset, cmap.Length);
            _codepoints = new int[_cmap.Count];
            _cmap.Keys.CopyTo(_codepoints, 0);
            Array.Sort(_codepoints);
        }

        /// <summary>em の単位数（<c>head.unitsPerEm</c>）。</summary>
        public int UnitsPerEm { get; }

        /// <summary>字形の数（<c>maxp.numGlyphs</c>）。</summary>
        public int GlyphCount { get; }

        /// <summary>アセンダ（フォント単位・<c>hhea</c>）。</summary>
        public int Ascender { get; }

        /// <summary>ディセンダ（フォント単位・下が負・<c>hhea</c>）。</summary>
        public int Descender { get; }

        /// <summary>行間の追加分（フォント単位・<c>hhea</c>）。</summary>
        public int LineGap { get; }

        /// <summary><c>cmap</c> が字形を割り当てている全コードポイント（昇順）。</summary>
        public IReadOnlyList<int> Codepoints => _codepoints;

        /// <summary><paramref name="codepoint"/> に字形があるか（字形番号 0＝.notdef は「無い」）。</summary>
        public bool Contains(int codepoint)
        {
            return _cmap.ContainsKey(codepoint);
        }

        /// <summary><paramref name="codepoint"/> の字形番号。無ければ false。</summary>
        public bool TryGetGlyphIndex(int codepoint, out int glyphIndex)
        {
            return _cmap.TryGetValue(codepoint, out glyphIndex);
        }

        /// <summary>字形の送り幅（フォント単位）。</summary>
        public int GetAdvanceWidth(int glyphIndex)
        {
            CheckGlyphIndex(glyphIndex);
            var metric = glyphIndex < _numberOfHMetrics ? glyphIndex : _numberOfHMetrics - 1;
            return ReadU16(_hmtxOffset + (4 * metric));
        }

        /// <summary>
        /// 字形の輪郭（フォント単位・y 上向き）。複合字形は部品を変換して 1 つの輪郭の集まりに展開する。
        /// 墨の無い字形（空白）は輪郭 0 本を返す。
        /// </summary>
        public GlyphOutline GetOutline(int glyphIndex)
        {
            CheckGlyphIndex(glyphIndex);
            var contours = new List<OutlinePoint[]>();
            AppendGlyph(glyphIndex, Affine.Identity, contours, 0);
            return new GlyphOutline(contours);
        }

        // ── 表の目録 ─────────────────────────────────────────────

        private readonly struct TableRecord
        {
            public TableRecord(int offset, int length)
            {
                Offset = offset;
                Length = length;
            }

            public int Offset { get; }
            public int Length { get; }
        }

        private Dictionary<string, TableRecord> ReadTableDirectory()
        {
            var numTables = ReadU16(4);
            var tables = new Dictionary<string, TableRecord>(StringComparer.Ordinal);
            for (var i = 0; i < numTables; i++)
            {
                var record = 12 + (16 * i);
                var tag = new string(new[]
                {
                    (char)ReadU8(record), (char)ReadU8(record + 1), (char)ReadU8(record + 2), (char)ReadU8(record + 3),
                });
                var offset = CheckedOffset(ReadU32(record + 8), 0);
                var length = (int)Math.Min(ReadU32(record + 12), int.MaxValue);
                if ((long)offset + length > _data.Length)
                {
                    throw new FormatException($"表 '{tag}' がファイルの外を指しています。");
                }
                tables[tag] = new TableRecord(offset, length);
            }
            return tables;
        }

        private static TableRecord RequireTable(Dictionary<string, TableRecord> tables, string tag, int minLength)
        {
            if (!tables.TryGetValue(tag, out TableRecord record))
            {
                throw new FormatException($"必要な表 '{tag}' がありません。");
            }
            if (record.Length < minLength)
            {
                throw new FormatException($"表 '{tag}' が短すぎます（{record.Length} バイト）。");
            }
            return record;
        }

        // ── cmap ─────────────────────────────────────────────────

        /// <summary>
        /// 字 → 字形番号の表を読む。選ぶ下位表は「全 Unicode（format 12）」を優先し、無ければ
        /// 「BMP（format 4）」——どちらも Unicode 符号化（platform 0 か platform 3 の encoding 1／10）に限る。
        /// </summary>
        private Dictionary<int, int> ReadCharacterMap(int cmapOffset, int cmapLength)
        {
            var count = ReadU16(cmapOffset + 2);
            var best = -1;
            var bestRank = int.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var record = cmapOffset + 4 + (8 * i);
                var platform = ReadU16(record);
                var encoding = ReadU16(record + 2);
                var subOffset = cmapOffset + CheckedOffset(ReadU32(record + 4), 0);
                if (subOffset + 2 > cmapOffset + cmapLength)
                {
                    throw new FormatException("cmap の下位表が表の外を指しています。");
                }
                var format = ReadU16(subOffset);
                var rank = RankSubtable(platform, encoding, format);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = subOffset;
                }
            }

            if (best < 0)
            {
                throw new FormatException("Unicode の cmap（format 12 か 4）がありません。");
            }

            var map = new Dictionary<int, int>();
            if (ReadU16(best) == 12)
            {
                ReadFormat12(best, map);
            }
            else
            {
                ReadFormat4(best, map);
            }
            return map;
        }

        private static int RankSubtable(int platform, int encoding, int format)
        {
            var unicode = platform == 0 || (platform == 3 && (encoding == 1 || encoding == 10));
            if (!unicode)
            {
                return int.MaxValue;
            }
            if (format == 12)
            {
                return 0;
            }
            if (format == 4)
            {
                return 1;
            }
            return int.MaxValue;
        }

        private void ReadFormat12(int sub, Dictionary<int, int> map)
        {
            var groups = ReadU32(sub + 12);
            for (long g = 0; g < groups; g++)
            {
                var group = sub + 16 + (int)(12 * g);
                var start = ReadU32(group);
                var end = ReadU32(group + 4);
                var glyph = ReadU32(group + 8);
                if (end < start || end > 0x10FFFF)
                {
                    throw new FormatException("cmap format 12 の範囲が壊れています。");
                }
                for (var cp = start; cp <= end; cp++)
                {
                    var gid = glyph + (cp - start);
                    if (gid != 0 && gid < (uint)GlyphCount)
                    {
                        map[(int)cp] = (int)gid;
                    }
                }
            }
        }

        private void ReadFormat4(int sub, Dictionary<int, int> map)
        {
            var segCount = ReadU16(sub + 6) / 2;
            var endCodes = sub + 14;
            var startCodes = endCodes + (2 * segCount) + 2;
            var idDeltas = startCodes + (2 * segCount);
            var idRangeOffsets = idDeltas + (2 * segCount);
            for (var s = 0; s < segCount; s++)
            {
                var end = ReadU16(endCodes + (2 * s));
                var start = ReadU16(startCodes + (2 * s));
                var delta = ReadI16(idDeltas + (2 * s));
                var rangeOffsetAt = idRangeOffsets + (2 * s);
                var rangeOffset = ReadU16(rangeOffsetAt);
                for (var cp = start; cp <= end && cp != 0xFFFF; cp++)
                {
                    int gid;
                    if (rangeOffset == 0)
                    {
                        gid = (cp + delta) & 0xFFFF;
                    }
                    else
                    {
                        var raw = ReadU16(rangeOffsetAt + rangeOffset + (2 * (cp - start)));
                        gid = raw == 0 ? 0 : (raw + delta) & 0xFFFF;
                    }
                    if (gid != 0 && gid < GlyphCount)
                    {
                        map[cp] = gid;
                    }
                }
            }
        }

        // ── glyf ─────────────────────────────────────────────────

        private void CheckGlyphIndex(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= GlyphCount)
            {
                throw new ArgumentOutOfRangeException(nameof(glyphIndex), $"字形番号 {glyphIndex} は範囲外です（字形数 {GlyphCount}）。");
            }
        }

        private (int Start, int End) GlyphRange(int glyphIndex)
        {
            int start;
            int end;
            if (_locaLong)
            {
                start = CheckedOffset(ReadU32(_locaOffset + (4 * glyphIndex)), 0);
                end = CheckedOffset(ReadU32(_locaOffset + (4 * (glyphIndex + 1))), 0);
            }
            else
            {
                start = 2 * ReadU16(_locaOffset + (2 * glyphIndex));
                end = 2 * ReadU16(_locaOffset + (2 * (glyphIndex + 1)));
            }
            if (end < start || end > _glyfLength)
            {
                throw new FormatException($"loca が字形 {glyphIndex} に不正な範囲を与えています。");
            }
            return (_glyfOffset + start, _glyfOffset + end);
        }

        private void AppendGlyph(int glyphIndex, Affine transform, List<OutlinePoint[]> contours, int depth)
        {
            if (depth > MaxCompositeDepth)
            {
                throw new FormatException($"複合字形の入れ子が深すぎます（字形 {glyphIndex}。循環参照の疑い）。");
            }

            var (start, end) = GlyphRange(glyphIndex);
            if (end == start)
            {
                return;  // 墨の無い字形
            }
            if (end - start < 10)
            {
                throw new FormatException($"字形 {glyphIndex} のヘッダが短すぎます。");
            }

            var numberOfContours = ReadI16(start);
            if (numberOfContours >= 0)
            {
                AppendSimpleGlyph(glyphIndex, start, end, numberOfContours, transform, contours);
            }
            else
            {
                AppendCompositeGlyph(glyphIndex, start, end, transform, contours, depth);
            }
        }

        private void AppendSimpleGlyph(
            int glyphIndex, int start, int end, int numberOfContours, Affine transform, List<OutlinePoint[]> contours)
        {
            if (numberOfContours == 0)
            {
                return;
            }

            var p = start + 10;
            var endPoints = new int[numberOfContours];
            for (var c = 0; c < numberOfContours; c++)
            {
                endPoints[c] = ReadU16(p);
                p += 2;
                if (c > 0 && endPoints[c] < endPoints[c - 1])
                {
                    throw new FormatException($"字形 {glyphIndex} の輪郭の終点が昇順ではありません。");
                }
            }
            var pointCount = endPoints[numberOfContours - 1] + 1;

            var instructionLength = ReadU16(p);
            p += 2 + instructionLength;

            var flags = new byte[pointCount];
            for (var i = 0; i < pointCount;)
            {
                RequireInGlyph(glyphIndex, p, 1, end);
                var flag = _data[p++];
                flags[i++] = flag;
                if ((flag & 0x08) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 1, end);
                    var repeat = _data[p++];
                    for (var r = 0; r < repeat && i < pointCount; r++)
                    {
                        flags[i++] = flag;
                    }
                }
            }

            var xs = new int[pointCount];
            var x = 0;
            for (var i = 0; i < pointCount; i++)
            {
                var flag = flags[i];
                if ((flag & 0x02) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 1, end);
                    var dx = _data[p++];
                    x += (flag & 0x10) != 0 ? dx : -dx;
                }
                else if ((flag & 0x10) == 0)
                {
                    RequireInGlyph(glyphIndex, p, 2, end);
                    x += ReadI16(p);
                    p += 2;
                }
                xs[i] = x;
            }

            var ys = new int[pointCount];
            var y = 0;
            for (var i = 0; i < pointCount; i++)
            {
                var flag = flags[i];
                if ((flag & 0x04) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 1, end);
                    var dy = _data[p++];
                    y += (flag & 0x20) != 0 ? dy : -dy;
                }
                else if ((flag & 0x20) == 0)
                {
                    RequireInGlyph(glyphIndex, p, 2, end);
                    y += ReadI16(p);
                    p += 2;
                }
                ys[i] = y;
            }

            var first = 0;
            for (var c = 0; c < numberOfContours; c++)
            {
                var last = endPoints[c];
                var length = last - first + 1;
                var contour = new OutlinePoint[length];
                for (var i = 0; i < length; i++)
                {
                    var k = first + i;
                    var (tx, ty) = transform.Apply(xs[k], ys[k]);
                    contour[i] = new OutlinePoint(tx, ty, (flags[k] & 0x01) != 0);
                }
                contours.Add(contour);
                first = last + 1;
            }
        }

        private void AppendCompositeGlyph(
            int glyphIndex, int start, int end, Affine transform, List<OutlinePoint[]> contours, int depth)
        {
            const int Arg1And2AreWords = 0x0001;
            const int ArgsAreXyValues = 0x0002;
            const int WeHaveAScale = 0x0008;
            const int MoreComponents = 0x0020;
            const int WeHaveAnXAndYScale = 0x0040;
            const int WeHaveATwoByTwo = 0x0080;
            const int ScaledComponentOffset = 0x0800;
            const int UnscaledComponentOffset = 0x1000;

            var p = start + 10;
            int flags;
            do
            {
                RequireInGlyph(glyphIndex, p, 4, end);
                flags = ReadU16(p);
                var component = ReadU16(p + 2);
                p += 4;

                int arg1;
                int arg2;
                if ((flags & Arg1And2AreWords) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 4, end);
                    arg1 = (flags & ArgsAreXyValues) != 0 ? ReadI16(p) : ReadU16(p);
                    arg2 = (flags & ArgsAreXyValues) != 0 ? ReadI16(p + 2) : ReadU16(p + 2);
                    p += 4;
                }
                else
                {
                    RequireInGlyph(glyphIndex, p, 2, end);
                    arg1 = (flags & ArgsAreXyValues) != 0 ? (sbyte)_data[p] : _data[p];
                    arg2 = (flags & ArgsAreXyValues) != 0 ? (sbyte)_data[p + 1] : _data[p + 1];
                    p += 2;
                }

                double a = 1, b = 0, c = 0, d = 1;
                if ((flags & WeHaveAScale) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 2, end);
                    a = d = ReadF2Dot14(p);
                    p += 2;
                }
                else if ((flags & WeHaveAnXAndYScale) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 4, end);
                    a = ReadF2Dot14(p);
                    d = ReadF2Dot14(p + 2);
                    p += 4;
                }
                else if ((flags & WeHaveATwoByTwo) != 0)
                {
                    RequireInGlyph(glyphIndex, p, 8, end);
                    a = ReadF2Dot14(p);
                    b = ReadF2Dot14(p + 2);
                    c = ReadF2Dot14(p + 4);
                    d = ReadF2Dot14(p + 6);
                    p += 8;
                }

                // 部品の線形変換（x' = a·x + c·y, y' = b·x + d·y）。平行移動は下で決める。
                var linear = new Affine(a, b, c, d, 0, 0);
                var componentContours = new List<OutlinePoint[]>();
                AppendGlyph(component, linear, componentContours, depth + 1);

                double dx;
                double dy;
                if ((flags & ArgsAreXyValues) != 0)
                {
                    dx = arg1;
                    dy = arg2;
                    // 部品の行列で平行移動も縮める指定。FreeType と Apple の手順と同じく、軸ごとに列の長さを掛ける
                    // （行列ごと掛けると、回転・斜めの部品で置き場所が他の実装と食い違う）。
                    if ((flags & ScaledComponentOffset) != 0 && (flags & UnscaledComponentOffset) == 0)
                    {
                        dx *= Math.Sqrt((a * a) + (b * b));
                        dy *= Math.Sqrt((c * c) + (d * d));
                    }
                }
                else
                {
                    // 点の照合: 親（ここまでに組んだ点）の arg1 番の点に、部品の arg2 番の点を重ねる。
                    var parent = FindPoint(contours, arg1, glyphIndex);
                    var child = FindPoint(componentContours, arg2, glyphIndex);
                    var parentLocal = transform.Inverse().Apply(parent.X, parent.Y);
                    dx = parentLocal.X - child.X;
                    dy = parentLocal.Y - child.Y;
                }

                var placement = new Affine(1, 0, 0, 1, dx, dy);
                foreach (var contour in componentContours)
                {
                    var placed = new OutlinePoint[contour.Length];
                    for (var i = 0; i < contour.Length; i++)
                    {
                        var local = placement.Apply(contour[i].X, contour[i].Y);
                        var (tx, ty) = transform.Apply(local.X, local.Y);
                        placed[i] = new OutlinePoint(tx, ty, contour[i].OnCurve);
                    }
                    contours.Add(placed);
                }
            }
            while ((flags & MoreComponents) != 0);
        }

        private static OutlinePoint FindPoint(List<OutlinePoint[]> contours, int index, int glyphIndex)
        {
            var remaining = index;
            foreach (var contour in contours)
            {
                if (remaining < contour.Length)
                {
                    return contour[remaining];
                }
                remaining -= contour.Length;
            }
            throw new FormatException($"複合字形 {glyphIndex} の点の照合が範囲外の点 {index} を指しています。");
        }

        private void RequireInGlyph(int glyphIndex, int position, int length, int end)
        {
            if (position + length > end)
            {
                throw new FormatException($"字形 {glyphIndex} のデータが途中で切れています。");
            }
        }

        // ── バイト列の読み取り（ビッグエンディアン） ─────────────────

        private int CheckedOffset(uint value, int minimum)
        {
            if (value < minimum || value > (uint)_data.Length)
            {
                throw new FormatException($"オフセット {value} がファイルの外を指しています。");
            }
            return (int)value;
        }

        private byte ReadU8(int offset)
        {
            Require(offset, 1);
            return _data[offset];
        }

        private int ReadU16(int offset)
        {
            Require(offset, 2);
            return (_data[offset] << 8) | _data[offset + 1];
        }

        private int ReadI16(int offset)
        {
            return (short)ReadU16(offset);
        }

        private uint ReadU32(int offset)
        {
            Require(offset, 4);
            return ((uint)_data[offset] << 24) | ((uint)_data[offset + 1] << 16) | ((uint)_data[offset + 2] << 8) | _data[offset + 3];
        }

        private double ReadF2Dot14(int offset)
        {
            return ReadI16(offset) / 16384.0;
        }

        private void Require(int offset, int length)
        {
            if (offset < 0 || offset + length > _data.Length)
            {
                throw new FormatException($"位置 {offset} から {length} バイトがファイルの外です。");
            }
        }
    }
}
