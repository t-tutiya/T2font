# docs/architecture/03-atlas-to-screen.html の図と数値を作り直す（使い方は同じフォルダの README.md）。
#
# 1. Dump（パッケージの Core と Bake をリンクしたコンソールプログラム）で見本をベイクし、元データを一時フォルダへ書き出す。
# 2. 元データとシェーダの式の CPU 版から、SVG の図・表の行・数値と PNG を作る。
# 3. 本書の <!-- gen:名前 --> と <!-- /gen --> の間だけを書き換える。地の文と CSS は本書のまま残す。
import os, re, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
DOC = os.path.join(ROOT, 'docs', 'architecture', '03-atlas-to-screen.html')
MARKER = re.compile(r'<!-- gen:(?P<name>[A-Z0-9_]+) -->(?P<body>.*?)<!-- /gen -->', re.S)


def dump(work):
    subprocess.run(
        ['dotnet', 'run', '-c', 'Release', '--project', os.path.join(HERE, 'Dump'), '--', ROOT, work],
        check=True)


def values():
    import numpy as np
    from schematics import (
        BG, D, F, G_A, PEN_A, ROW_K, TOTAL, YB, fig_batch, fig_frag_pngs, fig_frame, fig_gpu, fig_layout, fig_modes,
        fig_pipeline, fig_quad, fig_section, fig_slots, frag, frag_panel, layout_rows, lin2srgb, pixel_rows, strip_html,
        vertex_rows, f)

    fig_frag_pngs()

    def vec(v, n=3):
        return '(' + ', '.join(f(x, n) if abs(x) >= 5e-4 or x < 0 else '0' for x in v) + ')'

    def srgb8(v):
        return '(' + ', '.join(str(int(x)) for x in (lin2srgb(v) * 255 + 0.5).astype(int)) + ')'

    pixels = pixel_rows()
    ey = (-(ROW_K + 0.5) - YB) / F
    # 縁取り無しで同じ画素を描いた例（透明の RenderTexture の上での α を見せる）。
    none = dict(dilate=0, faceSoft=0, outlineWidth=0, outlineSoft=0, outlineColor=(0, 0, 0, 1))
    o = frag(G_A, np.array([(pixels[1]['X'] - PEN_A) / F]), np.array([ey]), 1 / F, none, (1, 1, 1), 1)
    a2 = float(o['a'][0])
    rgb2 = o['rgb'][0]
    res2 = rgb2 * a2 + BG * (1 - a2)

    v = {
        'FIG_PIPELINE': fig_pipeline(), 'STRIP': strip_html(), 'FIG_LAYOUT': fig_layout(), 'LAYOUT_ROWS': layout_rows(),
        'FIG_QUAD': fig_quad(), 'VERTEX_ROWS': vertex_rows(), 'FIG_SLOTS': fig_slots(), 'FIG_BATCH': fig_batch(),
        'FIG_GPU': fig_gpu(), 'FIG_SECTION': fig_section(), 'FIG_MODES': fig_modes(), 'FIG_FRAME': fig_frame(),
        'PANEL_R': frag_panel('frag-r.png', 'あ の tex.r'),
        'PANEL_AO': frag_panel('frag-ao.png', 'あ の ao', 'inv'),
        'PANEL_AF': frag_panel('frag-af.png', 'あ の af', 'inv'),
        'PANEL_OUT': frag_panel('frag-out.png', 'あ をブレンドした結果'),
        'TOTAL': f'{TOTAL:.2f}', 'ROW_EY': f'{ey:.4f}', 'HASH': D['hash'], 'BG_LIN': vec(BG, 4),
        'P2_OUT': vec(list(rgb2) + [a2]), 'P2_RES': vec(list(res2) + [1], 4), 'P2_RES8': srgb8(res2),
        'P2_PREMUL': vec(list(rgb2 * a2) + [a2]), 'P2_A': f'{a2:.3f}', 'P2_A2': f'{a2 * a2:.3f}',
    }
    for i, p in enumerate(pixels):
        k = f'P{i}_'
        v[k + 'X'] = f"{p['X']}"
        v[k + 'EM'] = f"({f(p['ex'], 4)}, {f(p['ey'], 4)})"
        v[k + 'UV'] = f"({p['u']:.5f}, {p['v']:.5f})"
        v[k + 'R'] = f"{p['r']:.4f}"
        v[k + 'S'] = f(p['s'], 5)
        v[k + 'AO'] = f"{p['ao']:.4f}"
        v[k + 'AF'] = f"{p['af']:.4f}"
        v[k + 'AOL'] = f"{p['aO']:.4f}"
        v[k + 'OUT'] = vec(list(p['rgb']) + [p['a']])
        v[k + 'RES'] = vec(list(p['res']) + [1], 4)
        v[k + 'RES8'] = '(' + ', '.join(str(int(x)) for x in p['res8']) + ')'
    return v


def main():
    with tempfile.TemporaryDirectory() as work:
        dump(work)
        os.environ['T2FONT_FIGURES_WORK'] = work
        sys.path.insert(0, HERE)
        v = values()

    html = open(DOC, encoding='utf-8').read()
    unknown = sorted({m['name'] for m in MARKER.finditer(html)} - v.keys())
    unused = sorted(v.keys() - {m['name'] for m in MARKER.finditer(html)})
    if unknown or unused:
        sys.exit(f'印と値が合いません: 本書にだけある {unknown}・スクリプトにだけある {unused}')

    def fill(m):
        body = m['body']
        value = v[m['name']]
        # 図の塊は印の行の間に置く（印と同じ行に詰めない）。
        if body.startswith('\n'):
            value = '\n' + value + '\n'
        return f"<!-- gen:{m['name']} -->{value}<!-- /gen -->"

    updated = MARKER.sub(fill, html)
    if updated != html:
        open(DOC, 'w', encoding='utf-8', newline='\n').write(updated)
        print(f'書き換えた: {os.path.relpath(DOC, ROOT)}')
    else:
        print(f'変わらない: {os.path.relpath(DOC, ROOT)}')


if __name__ == '__main__':
    main()
