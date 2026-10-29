# docs/architecture/04-bake-spec.html の表・付録・誤りの文面を作り直す（使い方は同じフォルダの README.md）。
#
# 1. Dump（パッケージの Core と Bake をリンクしたコンソールプログラム）で、文字セット・誤りの例の文面・見本のインポートの値を書き出す。
# 2. 元データから、表の行・付録の文字リスト・文面の HTML を作る。
# 3. 本書の <!-- gen:名前 --> と <!-- /gen --> の間だけを書き換える。地の文と CSS は本書のまま残す。
import html, json, os, re, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
DOC = os.path.join(ROOT, 'docs', 'architecture', '04-bake-spec.html')
MARKER = re.compile(r'<!-- gen:(?P<name>[A-Z0-9_]+) -->(?P<body>.*?)<!-- /gen -->', re.S)

# CommonSymbols のブロックの名前（FontCharsets.SymbolBlocks の先頭のコードポイントで引く。Unicode のブロック名とその訳）。
BLOCK_NAMES = {
    0x00A0: ('Latin-1 補助', 'Latin-1 Supplement'),
    0x2000: ('一般句読点', 'General Punctuation'),
    0x2100: ('文字様記号', 'Letterlike Symbols'),
    0x2150: ('数字の形', 'Number Forms'),
    0x2190: ('矢印', 'Arrows'),
    0x2200: ('数学記号', 'Mathematical Operators'),
    0x2300: ('その他の技術記号', 'Miscellaneous Technical'),
    0x2460: ('囲み英数字', 'Enclosed Alphanumerics'),
    0x2500: ('罫線', 'Box Drawing'),
    0x2580: ('ブロック要素', 'Block Elements'),
    0x25A0: ('幾何学模様', 'Geometric Shapes'),
    0x2600: ('その他の記号', 'Miscellaneous Symbols'),
    0x2700: ('装飾記号', 'Dingbats'),
    0x3000: ('CJK の記号と句読点', 'CJK Symbols and Punctuation'),
    0x3200: ('囲み CJK 文字・月', 'Enclosed CJK Letters and Months'),
    0x3300: ('CJK 互換', 'CJK Compatibility'),
    0xFF00: ('半角／全角形', 'Halfwidth and Fullwidth Forms'),
}

# JIS X 0208 の非漢字の区の中身（1〜8 区）。
NONKANJI_ROWS = {1: '記号', 2: '記号', 3: '数字・ラテン文字', 4: 'ひらがな', 5: 'カタカナ', 6: 'ギリシャ文字', 7: 'キリル文字', 8: '罫線'}

SINGLE = ['Ascii', 'JisX0208', 'WindowsVariants', 'NecRow13', 'IbmExtensions', 'CommonSymbols']


def dump(work):
    subprocess.run(
        ['dotnet', 'run', '-c', 'Release', '--project', os.path.join(HERE, 'Dump'), '--', ROOT, work],
        check=True)


def esc(text):
    return html.escape(text, quote=False)


def n(value):
    return f'{value:,}'


def ku_of(ch):
    # JIS X 0208 の区（EUC-JP の 1 バイト目 − 0xA0）。Python の euc_jp は JIS の標準の対応で写す。
    b = ch.encode('euc_jp')
    if len(b) != 2 or b[0] < 0xA1:
        raise ValueError(f'U+{ord(ch):04X} は JIS X 0208 の字ではありません。')
    return b[0] - 0xA0


def values(d):
    sets = {c['name']: c['codepoints'] for c in d['charsets']}
    if [c['name'] for c in d['charsets'] if c['single']] != SINGLE:
        sys.exit('文字セットの名前が SINGLE と合いません: ' + ', '.join(c['name'] for c in d['charsets']))
    cat = {int(k): v for k, v in d['categories'].items()}
    t = d['tables']

    def glyph(cp):
        c = cat.get(cp, '')
        if cp == 0x20:
            return '<b class="sp">␠</b>'
        if c in ('SpaceSeparator', 'Format', 'OtherNotAssigned'):
            return '<b></b>'
        if c in ('NonSpacingMark', 'SpacingCombiningMark', 'EnclosingMark'):
            return '<b>◌' + esc(chr(cp)) + '</b>'
        return '<b>' + esc(chr(cp)) + '</b>'

    def cell(cp, state=None):
        c = cat.get(cp, '')
        # U+0020 は ␠ で見せるので、見えない字の枠を付けない。
        cls = state or (None if cp == 0x20 else {'SpaceSeparator': 'ws', 'Format': 'ws', 'OtherNotAssigned': 'na'}.get(c))
        body = '<b></b>' if state else glyph(cp)
        attr = f' class="{cls}"' if cls else ''
        return f'<li{attr}>{body}<i>{cp:04X}</i></li>'

    def cells(items):
        return '<ol class="cells">' + ''.join(items) + '</ol>'

    v = {}
    for name in SINGLE + ['DevelopmentDefault']:
        v['N_' + name.upper()] = n(len(sets[name]))
    v['N_JIS_NONKANJI'] = n(len(t['NonKanji']))
    v['N_JIS_LEVEL1'] = n(len(t['Level1']))
    v['N_JIS_LEVEL2'] = n(len(t['Level2']))
    total = sum(len(sets[s]) for s in SINGLE)
    v['N_SUM'] = n(total)

    # 重なりの表（対角は字数）。
    rows = []
    head = ''.join(f'<th class="num">{s}</th>' for s in SINGLE)
    for a in SINGLE:
        tds = []
        for b in SINGLE:
            k = len(set(sets[a]) & set(sets[b]))
            cls = 'num diag' if a == b else ('num' if k else 'num zero')
            tds.append(f'<td class="{cls}">{n(k) if k else "—"}</td>')
        rows.append(f'<tr><th class="rowh">{a}</th>{"".join(tds)}</tr>')
    v['OVERLAP_TABLE'] = f'<thead><tr><th></th>{head}</tr></thead>\n<tbody>\n' + '\n'.join(rows) + '\n</tbody>'

    # 記号のブロック。
    blocks, excl = d['symbolBlocks'], d['symbolExclusions']
    excluded = {cp for first, last in excl for cp in range(first, last + 1)}
    symbols = set(sets['CommonSymbols'])
    unknown = [f'U+{first:04X}' for first, _ in blocks if first not in BLOCK_NAMES]
    if unknown:
        sys.exit('BLOCK_NAMES に無いブロックがあります: ' + ', '.join(unknown))
    v['N_SYM_BLOCKS'] = n(len(blocks))
    v['N_SYM_EXCLUDED'] = n(len(excluded))
    v['N_SYM_UNASSIGNED'] = n(sum(1 for cp in symbols if cat[cp] == 'OtherNotAssigned'))
    v['SYM_EXCLUSIONS'] = '・'.join(f'U+{a:04X}〜U+{b:04X}' for a, b in excl)
    block_rows, appx = [], []
    for first, last in blocks:
        ja, en = BLOCK_NAMES[first]
        inside = [cp for cp in range(first, last + 1) if cp in symbols]
        na = sum(1 for cp in inside if cat[cp] == 'OtherNotAssigned')
        rng = f'U+{first:04X}〜U+{last:04X}'
        block_rows.append(f'<tr><td><a href="#sym-{first:04x}">{ja}</a></td><td class="mono">{en}</td><td class="mono nw">{rng}</td>'
                          f'<td class="num">{n(len(inside))}</td><td class="num">{n(na) if na else "—"}</td></tr>')
        items = [cell(cp, 'ex' if cp in excluded else None) for cp in range(first, last + 1)]
        appx.append(f'<h4 id="sym-{first:04x}">{ja}<span class="sub">{en}・<span class="mono">{rng}</span>・{n(len(inside))} 字</span></h4>\n'
                    + cells(items))
    v['SYM_BLOCK_ROWS'] = '\n'.join(block_rows)
    v['APPX_SYMBOLS'] = '\n'.join(appx)

    # Ascii（U+007F は含まないので空けて並べ、16 の列を揃える）。
    ascii = set(sets['Ascii'])
    v['APPX_ASCII'] = cells(cell(cp, None if cp in ascii else 'ex') for cp in range(0x20, 0x80))

    # JIS X 0208。区は表の並び（区点の順）から求め、区点の順になっていることも確かめる。
    def by_ku(cps):
        groups = {}
        last = (0, 0)
        for cp in cps:
            b = chr(cp).encode('euc_jp')
            key = (b[0], b[1])
            if key <= last:
                sys.exit(f'JisX0208Table が区点の順ではありません: U+{cp:04X}')
            last = key
            groups.setdefault(ku_of(chr(cp)), []).append(cp)
        return groups

    nonkanji = by_ku(t['NonKanji'])
    if sorted(nonkanji) != sorted(NONKANJI_ROWS):
        sys.exit('非漢字の区が NONKANJI_ROWS と合いません: ' + str(sorted(nonkanji)))
    v['APPX_JIS_NONKANJI'] = '\n'.join(
        f'<h4>{ku} 区<span class="sub">{NONKANJI_ROWS[ku]}・{n(len(cps))} 字</span></h4>\n' + cells(cell(cp) for cp in cps)
        for ku, cps in sorted(nonkanji.items()))

    def kanji(cps):
        groups = by_ku(cps)
        return '<div class="kanji">\n' + '\n'.join(
            f'<div class="kr"><span class="kn">{ku}</span><p lang="ja">{esc("".join(chr(cp) for cp in cps))}</p></div>'
            for ku, cps in sorted(groups.items())) + '\n</div>'

    l1, l2 = by_ku(t['Level1']), by_ku(t['Level2'])
    v['JIS_LEVEL1_ROWS'] = f'{min(l1)}〜{max(l1)}'
    v['JIS_LEVEL2_ROWS'] = f'{min(l2)}〜{max(l2)}'
    v['APPX_JIS_LEVEL1'] = kanji(t['Level1'])
    v['APPX_JIS_LEVEL2'] = kanji(t['Level2'])

    # WindowsVariants: CP932 のバイト列を JIS の標準の対応（shift_jis）で読み戻すと、JIS X 0208 の側の字になる。
    jis = set(sets['JisX0208'])
    wrows = []
    for cp in t['WindowsVariants']:
        std = chr(cp).encode('cp932').decode('shift_jis')
        if ord(std) not in jis or ord(std) == cp:
            sys.exit(f'U+{cp:04X} の JIS 標準の側が JisX0208 にありません。')
        wrows.append(f'<tr><td class="big">{esc(chr(cp))}</td><td class="mono">U+{cp:04X}</td>'
                     f'<td class="big">{esc(std)}</td><td class="mono">U+{ord(std):04X}</td></tr>')
    v['APPX_WINDOWS'] = '\n'.join(wrows)

    v['APPX_NEC'] = cells(cell(cp) for cp in t['NecRow13'])
    v['APPX_IBM'] = cells(cell(cp) for cp in t['IbmExtensions'])

    # 見本・基本の形・書き出しの例。
    s = d['sample']
    pages = '・'.join(f"{p['Width']}×{p['Height']}" for p in s['pages'])
    v['SAMPLE_SPEC'] = esc(s['spec'].rstrip('\n'))
    v['SAMPLE_LOG'] = esc(f"[T2Font] {s['path']}: {s['codepoints']} 字・グリフ {s['glyphs']}・{s['kind']}・{len(s['pages'])} ページ"
                          f"（{pages}・{s['format']}）・セル {s['cellW']}×{s['cellH']}（インク境界 {s['cellBox']}）・")
    v['SAMPLE_CHARS'] = n(s['codepoints'])
    v['SAMPLE_GLYPHS'] = n(s['glyphs'])
    v['SAMPLE_PAGES'] = f"{len(s['pages'])} ページ {pages} px"
    v['SAMPLE_CELL'] = f"{s['cellW']}×{s['cellH']} px"
    v['BASE_SPEC'] = esc(d['baseSpec'].rstrip('\n'))
    v['EXAMPLE_PATH'] = esc(d['examplePath'])
    v['EXPORTED_SPEC'] = esc(d['exported'].rstrip('\n'))

    dev = d['develop']
    v['DEV_CHARS'] = n(dev['codepoints'])
    v['DEV_GLYPHS'] = n(dev['glyphs'])
    v['DEV_MISSING'] = n(dev['missing'])
    size_rows = []
    for z in dev['sizes']:
        px = sum(p['Width'] * p['Height'] for p in z['pages'])
        dims = '・'.join('{}×{}'.format(p['Width'], p['Height']) for p in z['pages'])
        size_rows.append(
            f"<tr><td class=\"num\">{z['emPx']}</td><td class=\"num\">{z['cellW']}×{z['cellH']}</td>"
            f"<td class=\"num\">{len(z['pages'])}</td><td class=\"mono\">{dims}</td>"
            f"<td class=\"num\">{px / 2**20:.1f} MiB</td><td class=\"num\">{px / 2 / 2**20:.1f} MiB</td></tr>")
    v['SIZE_ROWS'] = '\n'.join(size_rows)
    v['SIZE_INKED'] = n(dev['sizes'][0]['InkedCount'])

    for name, e in d['errors'].items():
        v[f'ERR_{name}_IN'] = esc(e['shown'])
        v[f'ERR_{name}_MSG'] = esc(e['message'])
    for name, message in d['fontErrors'].items():
        v[f'FONT_ERR_{name}'] = esc(message)
    return v


def main():
    with tempfile.TemporaryDirectory() as work:
        dump(work)
        with open(os.path.join(work, 'dump.json'), encoding='utf-8') as f:
            v = values(json.load(f))

    text = open(DOC, encoding='utf-8').read()
    unknown = sorted({m['name'] for m in MARKER.finditer(text)} - v.keys())
    unused = sorted(v.keys() - {m['name'] for m in MARKER.finditer(text)})
    if unknown or unused:
        sys.exit(f'印と値が合いません: 本書にだけある {unknown}・スクリプトにだけある {unused}')

    def fill(m):
        body = m['body']
        value = v[m['name']]
        # 塊（表の行・付録）は印の行の間に置く（印と同じ行に詰めない）。
        if body.startswith('\n'):
            value = '\n' + value + '\n'
        return f"<!-- gen:{m['name']} -->{value}<!-- /gen -->"

    updated = MARKER.sub(fill, text)
    if updated != text:
        open(DOC, 'w', encoding='utf-8', newline='\n').write(updated)
        print(f'書き換えた: {os.path.relpath(DOC, ROOT)}')
    else:
        print(f'変わらない: {os.path.relpath(DOC, ROOT)}')


if __name__ == '__main__':
    main()
