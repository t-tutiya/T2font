# 03 章の実データの図（SVG の文字列と PNG）。数値は Dump の出力（パッケージの Core/Bake を走らせた実データ）とシェーダの式の CPU 版から。
import numpy as np, os
from PIL import Image
from sdfemu import *

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..','..'))
OUT=os.path.join(ROOT,'docs','architecture','03-atlas-to-screen')
os.makedirs(OUT,exist_ok=True)
F=48.0
UPM=D['unitsPerEm']
FACE=D['face']
GL=D['glyphs']
ASC=FACE['ascender']; DESC=FACE['descender']; GAP=FACE['lineGap']
YB=-ASC*F                      # ベースライン（rect の上端を 0・y 上向き）
LINESTEP=(ASC-DESC+GAP)*F
PENX=[]; x=0.0
for g in GL:
    PENX.append(x); x+=g['advance']*F
TOTAL=x
STYLE_OUT=dict(dilate=0,faceSoft=0,outlineWidth=0.05,outlineSoft=0,outlineColor=(0,0,0,1))
BG_SRGB=(0.32,0.36,0.42)
BG=srgb2lin(BG_SRGB)

def f(v,n=2):
    s=f"{v:.{n}f}"
    if s.startswith('-'): s='−'+s[1:]
    return s
def r(v): return f"{v:.2f}".rstrip('0').rstrip('.') if abs(v-round(v))>1e-9 else str(int(round(v)))

def arrow_h(x1,x2,y,cls='ah-dim'):
    # 両端に矢じり
    return (f'<line class="dim" x1="{r(x1)}" y1="{r(y)}" x2="{r(x2)}" y2="{r(y)}"/>'
            f'<polygon class="{cls}" points="{r(x2)},{r(y)} {r(x2-6)},{r(y+2.7)} {r(x2-6)},{r(y-2.7)}"/>'
            f'<polygon class="{cls}" points="{r(x1)},{r(y)} {r(x1+6)},{r(y-2.7)} {r(x1+6)},{r(y+2.7)}"/>')
def arrow_v(x,y1,y2,cls='ah-dim'):
    return (f'<line class="dim" x1="{r(x)}" y1="{r(y1)}" x2="{r(x)}" y2="{r(y2)}"/>'
            f'<polygon class="{cls}" points="{r(x)},{r(y1)} {r(x+2.7)},{r(y1+6)} {r(x-2.7)},{r(y1+6)}"/>'
            f'<polygon class="{cls}" points="{r(x)},{r(y2)} {r(x-2.7)},{r(y2-6)} {r(x+2.7)},{r(y2-6)}"/>')
def down(x,y1,y2):
    return f'<line class="flow" x1="{r(x)}" y1="{r(y1)}" x2="{r(x)}" y2="{r(y2-2)}"/><polygon class="ah-flow" points="{r(x)},{r(y2)} {r(x-3.6)},{r(y2-8)} {r(x+3.6)},{r(y2-8)}"/>'
def right(x1,x2,y):
    return f'<line class="flow" x1="{r(x1)}" y1="{r(y)}" x2="{r(x2-2)}" y2="{r(y)}"/><polygon class="ah-flow" points="{r(x2)},{r(y)} {r(x2-8)},{r(y-3.6)} {r(x2-8)},{r(y+3.6)}"/>'
def text(x,y,s,cls='lbl',anchor='start'):
    return f'<text class="{cls}" x="{r(x)}" y="{r(y)}" text-anchor="{anchor}">{s}</text>'
def svg(w,h,body,label,extra=''):
    return (f'<div class="scroll"><svg class="dia{extra}" viewBox="0 0 {r(w)} {r(h)}" width="{r(w)}" height="{r(h)}" role="img" aria-label="{label}" xmlns="http://www.w3.org/2000/svg">\n'
            + body + '\n</svg></div>')

def contour_path(pts,T):
    n=len(pts)
    P=[(p[0],p[1],p[2]>0.5) for p in pts]
    s=next((i for i,p in enumerate(P) if p[2]),-1)
    if s>=0:
        start=P[s][:2]; seq=P[s+1:]+P[:s]
    else:
        start=((P[0][0]+P[1][0])/2,(P[0][1]+P[1][1])/2); seq=P[1:]+P[:1]
    def pt(p):
        X,Y=T(p[0],p[1]); return f"{X:.2f} {Y:.2f}"
    d=[f"M{pt(start)}"]; ctrl=None
    for p in seq:
        if p[2]:
            d.append(f"Q{pt(ctrl)} {pt(p)}" if ctrl else f"L{pt(p)}"); ctrl=None
        else:
            if ctrl:
                mid=((ctrl[0]+p[0])/2,(ctrl[1]+p[1])/2); d.append(f"Q{pt(ctrl)} {pt(mid)}")
            ctrl=p
    d.append(f"Q{pt(ctrl)} {pt(start)}" if ctrl else "")
    return ''.join(d)+'Z'
def glyph_path(g,T,cls='g-fill g-stroke'):
    if not g['contours']: return ''
    return f'<path class="{cls}" fill-rule="nonzero" d="'+' '.join(contour_path(c,T) for c in g['contours'])+'"/>'

# ── 図 1: 全体の流れ ─────────────────────────────────────────
def fig_pipeline():
    b=[]; x0=40; w=680; y=16
    def node(y,h,title,subs,asm,cls='step'):
        s=f'<rect class="node {cls}" x="{x0}" y="{y}" width="{w}" height="{h}" rx="6"/>'
        s+=text(x0+14,y+24,title,'node-t')
        for i,t in enumerate(subs): s+=text(x0+14,y+44+17*i,t,'node-s')
        s+=text(x0+w-12,y+24,asm,'asm','end')
        return s
    rows=[
     (62,'T2FontAsset（.t2font をインポートしたアセット）',['face・codepoints・glyphs・notdef ＋ 表現 SdfRepresentation（emPx・cells・pages・shader）'],'Runtime の型','in'),
     (44,'ホスト T2FontText.Rebuild（LateUpdate・値が変わったときだけ）',[],'Viewer','host'),
     (61,'① 字をグリフインデックスにする',['TextCodepoints.Read → T2FontAsset.Resolve（二分探索・無い字は notdef）'],'Core＋Runtime','step'),
     (78,'② 字を置き、幾何を書く',['Metrics → ペンを送り幅で進める','RepresentationOf → SdfRepresentation.WriteGeometry → GlyphGeometry（em・UV0）'],'Runtime','step'),
     (78,'③ マテリアルとテクスチャを用意する',['GlyphMaterialCache.Get（_RangeEm・エッジスタイルの uniform）','SdfRepresentation.GetTexture → ページの Texture2D'],'Runtime','step'),
     (61,'④ CanvasRenderer へ渡す',['GlyphGeometry.CopyTo(mesh, fontSize)・SetMaterial・SetTexture・SetColor・SetMesh'],'Viewer','step'),
     (61,'⑤ Canvas がバッチにまとめる',['同じマテリアル × テクスチャの字を 1 回の描画にする・頂点をルートの Canvas の空間へ'],'uGUI','step'),
     (78,'⑥ GPU で描く（シェーダ T2Font/SDF）',['頂点シェーダ → ラスタライズ → フラグメントシェーダ（距離 → 被覆率 → 色）','→ ブレンドしてレンダーターゲットへ書く'],'GPU','sdf'),
     (61,'⑦ 画面へ出す',['レンダーターゲット（バックバッファ・カメラのカラーテクスチャ・RenderTexture）→ Present'],'URP＋Unity','step'),
    ]
    for i,(h,t,s,a,c) in enumerate(rows):
        b.append(node(y,h,t,s,a,c)); y+=h
        if i<len(rows)-1: b.append(down(380,y,y+20)); y+=22
    return svg(760,y+16,'\n'.join(b),'グリフを描く 7 段の流れ図')

# ── 図 2: 字 → グリフインデックス（HTML の strip）────────────────
def strip_html():
    s=['<div class="strip" role="list" aria-label="字とグリフインデックス">']
    for g in GL:
        ch=g['ch'] if g['ch']!=' ' else '␣'
        s.append(f'<div class="g" role="listitem"><b>{ch}</b><span>U+{g["cp"]:04X}</span><span>→ {g["glyph"]}</span></div>')
    s.append('</div>')
    return '\n'.join(s)

# ── 図 3: 字を置く（実データ）─────────────────────────────────
def fig_layout():
    S=2.0; L=104; T0=36
    X=lambda x: L+x*S
    Y=lambda y: T0-y*S
    b=[]
    top=Y(0); base=Y(YB); desc=Y(YB+DESC*F); nxt=Y(YB-LINESTEP)
    right_edge=X(TOTAL)
    b.append(f'<line class="vguide" x1="{r(L)}" y1="{r(top)}" x2="{r(right_edge+12)}" y2="{r(top)}"/>')
    b.append(f'<line class="vguide faint" x1="{r(L)}" y1="{r(desc)}" x2="{r(right_edge+12)}" y2="{r(desc)}"/>')
    b.append(f'<line class="base" x1="{r(L)}" y1="{r(base)}" x2="{r(right_edge+12)}" y2="{r(base)}"/>')
    b.append(f'<line class="base" x1="{r(L)}" y1="{r(nxt)}" x2="{r(right_edge+12)}" y2="{r(nxt)}" opacity="0.45"/>')
    b.append(f'<line class="vguide faint" x1="{r(L)}" y1="{r(top-10)}" x2="{r(L)}" y2="{r(nxt+14)}"/>')
    b.append(text(L-8,top+4,'rect.yMax','lbl-s mono','end'))
    b.append(text(L-12,base+4,'ベースライン','lbl-s','end'))
    b.append(text(L-8,desc+4,'ディセンダ','lbl-s','end'))
    b.append(text(L-8,nxt+4,'次の行','lbl-s','end'))
    b.append(text(L,top-14,'rect.xMin','lbl-s mono','middle'))
    for g,px in zip(GL,PENX):
        if not g['empty']:
            b.append(f'<rect class="glyphrect" x="{r(X(px+g["planeLeft"]*F))}" y="{r(Y(YB+g["planeTop"]*F))}" width="{r((g["planeRight"]-g["planeLeft"])*F*S)}" height="{r((g["planeTop"]-g["planeBottom"])*F*S)}" stroke-dasharray="4 3" opacity="0.8"/>')
        T=lambda fx,fy,px=px: (X(px+fx/UPM*F),Y(YB+fy/UPM*F))
        b.append(glyph_path(g,T))
    ay=desc+22
    for i,(g,px) in enumerate(zip(GL,PENX)):
        b.append(f'<circle class="origin" cx="{r(X(px))}" cy="{r(base)}" r="3.6"/>')
        x1=X(px); x2=X(px+g['advance']*F)
        b.append(f'<line class="tick" x1="{r(x1)}" y1="{r(base)}" x2="{r(x1)}" y2="{r(ay+4)}"/>')
        b.append(arrow_h(x1+1,x2-1,ay))
        b.append(text((x1+x2)/2,ay+16,f"{g['advance']*F:.2f}",'lbl-s mono','middle'))
        if g['empty']: b.append(text((x1+x2)/2,top-6,'幾何無し','lbl-s','middle'))
    b.append(f'<line class="tick" x1="{r(right_edge)}" y1="{r(base)}" x2="{r(right_edge)}" y2="{r(ay+4)}"/>')
    b.append(text(L,ay+34,'送り幅 × fontSize（px）・● ペン原点・破線 quad（セルの plane × 48）','lbl-s'))
    xa=right_edge+30
    b.append(arrow_v(xa,base,nxt))
    b.append(text(xa-8,(base+nxt)/2+40,f'行送り {LINESTEP:.2f} px','lbl-s mono','end'))
    b.append(text(xa-8,(base+nxt)/2+56,'＝ (ascender − descender + lineGap) × 48','lbl-s mono','end'))
    return svg(xa+30,nxt+20,'\n'.join(b),'文字列 T2Font 永あア を 48px で置いた図。ペン原点と送り幅と quad')

# ── 図 4: quad と UV（実データ）─────────────────────────────
def page_crop():
    x0,x1,y0,y1=680,884,360,576
    img=(PAGE[y0:y1,x0:x1][::-1]*255+0.5).astype(np.uint8)
    Image.fromarray(img,'L').save(OUT+'/page-crop.png',optimize=True)
    return x0,x1,y0,y1
def fig_quad():
    g=[x for x in GL if x['ch']=='あ'][0]
    P=190.0; ox=72; oy=236
    X=lambda e: ox+e*P
    Y=lambda e: oy-e*P
    b=[]
    l,bt,rt,tp=g['planeLeft'],g['planeBottom'],g['planeRight'],g['planeTop']
    b.append(f'<rect class="cellbox hl" x="{r(X(l))}" y="{r(Y(tp))}" width="{r((rt-l)*P)}" height="{r((tp-bt)*P)}"/>')
    b.append(f'<line class="leader-c" x1="{r(X(l))}" y1="{r(Y(bt))}" x2="{r(X(rt))}" y2="{r(Y(tp))}" stroke-dasharray="3 3"/>')
    b.append(f'<line class="base" x1="{r(X(l)-14)}" y1="{r(oy)}" x2="{r(X(rt)+14)}" y2="{r(oy)}"/>')
    b.append(f'<line class="vguide" x1="{r(ox)}" y1="{r(Y(tp)-12)}" x2="{r(ox)}" y2="{r(Y(bt)+12)}"/>')
    b.append(f'<rect class="inkbox" x="{r(X(g["inkLeft"]))}" y="{r(Y(g["inkTop"]))}" width="{r((g["inkRight"]-g["inkLeft"])*P)}" height="{r((g["inkTop"]-g["inkBottom"])*P)}"/>')
    b.append(glyph_path(g,lambda fx,fy:(X(fx/UPM),Y(fy/UPM))))
    b.append(f'<circle class="origin" cx="{r(ox)}" cy="{r(oy)}" r="4.5"/>')
    corners=[(0,l,bt,'start',18),(1,rt,bt,'end',34),(2,rt,tp,'end',-24),(3,l,tp,'start',-8)]
    for i,ex,ey,anc,dy in corners:
        b.append(f'<circle class="pt-seg" cx="{r(X(ex))}" cy="{r(Y(ey))}" r="3.4"/>')
        b.append(text(X(ex),Y(ey)+dy,f'{i}','wnum',anc))
        b.append(text(X(ex)+(14 if anc=='start' else -14),Y(ey)+dy,f'({f(ex,4)}, {f(ey,4)})','lbl-s mono',anc))
    b.append(text(X(rt)+18,oy-6,'ペン原点 (0, 0)','lbl-s'))
    b.append(text(X(l),Y(bt)+60,'em の座標（plane）＝ 位置 と UV0.zw','lbl-b'))
    # 右: ページの一部
    x0,x1,y0,y1=page_crop()
    S=1.5; ix=440; iy=34
    b.append(f'<image href="03-atlas-to-screen/page-crop.png" x="{ix}" y="{iy}" width="{r((x1-x0)*S)}" height="{r((y1-y0)*S)}" style="image-rendering:pixelated" preserveAspectRatio="none"/>')
    for k in range(1,3):
        b.append(f'<line stroke="var(--atl-q)" stroke-width="1" stroke-dasharray="4 3" x1="{r(ix+k*68*S)}" y1="{iy}" x2="{r(ix+k*68*S)}" y2="{r(iy+(y1-y0)*S)}"/>')
        b.append(f'<line stroke="var(--atl-q)" stroke-width="1" stroke-dasharray="4 3" x1="{ix}" y1="{r(iy+k*72*S)}" x2="{r(ix+(x1-x0)*S)}" y2="{r(iy+k*72*S)}"/>')
    ax=lambda a: ix+(a-x0)*S
    ay=lambda a: iy+(y1-a)*S
    al,ab,ar,at=g['atlasLeft'],g['atlasBottom'],g['atlasRight'],g['atlasTop']
    b.append(f'<rect class="slot c2" x="{r(ax(al))}" y="{r(ay(at))}" width="{r((ar-al)*S)}" height="{r((at-ab)*S)}"/>')
    b.append(text(ax(al),ay(ab)+15,f'({f(al/W,4)}, {f(ab/H,4)})','lbl-s mono on-plate','start'))
    b.append(text(ax(ar),ay(at)-6,f'({f(ar/W,4)}, {f(at/H,4)})','lbl-s mono on-plate','end'))
    b.append(text(ix,iy+(y1-y0)*S+20,f'ページ 0（{W}×{H}）の一部','lbl-s'))
    b.append(text(ix,iy+(y1-y0)*S+36,f'x {x0}〜{x1}・y {y0}〜{y1}（セル 3×3）','lbl-s mono'))
    b.append(text(ix,iy+(y1-y0)*S+60,'ページの UV ＝ UV0.xy','lbl-b'))
    b.append(right(X(rt)+22,ix-10,Y(0.4)))
    b.append(text((X(rt)+ix)/2+6,Y(0.4)-24,'atlas ÷','lbl-s mono','middle'))
    b.append(text((X(rt)+ix)/2+6,Y(0.4)-10,'ページの寸法','lbl-s','middle'))
    return svg(760,420,'\n'.join(b),'あ の quad の em 座標と、ページの上の同じ矩形の UV')

def vertex_rows():
    g=[x for x in GL if x['ch']=='あ'][0]
    l,bt,rt,tp=g['planeLeft'],g['planeBottom'],g['planeRight'],g['planeTop']
    ul,vb,ur,vt=g['atlasLeft']/W,g['atlasBottom']/H,g['atlasRight']/W,g['atlasTop']/H
    rows=[]
    for i,(ex,ey,u,v,name) in enumerate([(l,bt,ul,vb,'左下'),(rt,bt,ur,vb,'右下'),(rt,tp,ur,vt,'右上'),(l,tp,ul,vt,'左上')]):
        rows.append(f'<tr><td class="num">{i}</td><td>{name}</td><td class="mono">({f(ex,4)}, {f(ey,4)})</td><td class="mono">({f(ex*F,2)}, {f(ey*F,2)})</td><td class="mono">({f(u,4)}, {f(v,4)}, {f(ex,4)}, {f(ey,4)})</td></tr>')
    return '\n'.join(rows)

def layout_rows():
    rows=[]
    for g,px in zip(GL,PENX):
        ch=g['ch'] if g['ch']!=' ' else '␣（空白）'
        q='—' if g['empty'] else f"[{f(px+g['planeLeft']*F)}, {f(px+g['planeRight']*F)}]"
        rows.append(f'<tr><td>{ch}</td><td class="num">{g["glyph"]}</td><td class="num mono">{f(px)}</td><td class="num mono">{g["advance"]:.3f}</td><td class="num mono">{f(g["advance"]*F)}</td><td class="mono">{q}</td></tr>')
    return '\n'.join(rows)

# ── 図 8: フラグメントの段（実データ・CPU で同じ式）───────────────
G_A=[x for x in GL if x['ch']=='あ'][0]
PEN_A=PENX[GL.index(G_A)]
PX0,PX1=220,281        # 画素の列（含む・含まない）
PK0,PK1=2,66           # 画素の行（上から k・中心は y = −(k + 0.5)）
ROW_K=36
def raster(style=STYLE_OUT,face=(1,1,1),alpha=1.0):
    xs=np.arange(PX0,PX1)+0.5; ks=np.arange(PK0,PK1); ys=-(ks+0.5)
    XX,YY=np.meshgrid(xs,ys)
    ex=(XX-PEN_A)/F; ey=(YY-YB)/F
    g=G_A
    inside=(ex>=g['planeLeft'])&(ex<=g['planeRight'])&(ey>=g['planeBottom'])&(ey<=g['planeTop'])
    o=frag(g,ex,ey,1/F,style,face,alpha)
    o['inside']=inside
    return o
def fig_frag_pngs():
    o=raster()
    ins=o['inside']
    def rgba(gray,alpha):
        a=np.dstack([gray,gray,gray,alpha]); return Image.fromarray((np.clip(a,0,1)*255+0.5).astype(np.uint8),'RGBA')
    rgba(o['r'],ins*1.0).save(OUT+'/frag-r.png',optimize=True)
    rgba(1-o['ao'],ins*1.0).save(OUT+'/frag-ao.png',optimize=True)
    rgba(1-o['af'],ins*1.0).save(OUT+'/frag-af.png',optimize=True)
    a=np.where(ins,o['a'],0)[...,None]
    res=o['rgb']*a+BG*(1-a)
    Image.fromarray((lin2srgb(res)*255+0.5).astype(np.uint8),'RGB').save(OUT+'/frag-out.png',optimize=True)
def frag_panel(png,label,cls=''):
    w=PX1-PX0; h=PK1-PK0; k=ROW_K-PK0
    c=f' class="{cls}"' if cls else ''
    return (f'<svg class="dia narrow" viewBox="-3 0 {w+6} {h}" role="img" aria-label="{label}" xmlns="http://www.w3.org/2000/svg">'
            f'<image{c} href="03-atlas-to-screen/{png}" x="0" y="0" width="{w}" height="{h}" style="image-rendering:pixelated" preserveAspectRatio="none"/>'
            f'<polygon class="ah-ctrl" points="-0.2,{k+0.5} -3,{k-1.2} -3,{k+2.2}"/><polygon class="ah-ctrl" points="{w+0.2},{k+0.5} {w+3},{k-1.2} {w+3},{k+2.2}"/></svg>')

# ── 図 9: 断面（実データ）──────────────────────────────────
def fig_section():
    g=G_A; Yc=-(ROW_K+0.5); ey=(Yc-YB)/F
    xa=g['planeLeft']*F+PEN_A; xb=g['planeRight']*F+PEN_A
    xs=np.linspace(xa,xb,1400); ex=(xs-PEN_A)/F
    o=frag(g,ex,np.full_like(ex,ey),1/F,STYLE_OUT,(1,1,1),1)
    pc=np.arange(np.ceil(xa-0.5),np.floor(xb-0.5)+1)+0.5
    op=frag(g,(pc-PEN_A)/F,np.full_like(pc,ey),1/F,STYLE_OUT,(1,1,1),1)
    L=86; R=742; sx=(R-L)/(xb-xa)
    X=lambda x: L+(x-xa)*sx
    t1,h1=26,170; t2,h2=268,120
    rng=RANGE
    Ys=lambda s: t1+h1/2-(s/rng)*(h1/2)
    Yc_=lambda c: t2+h2-(c*h2)
    b=[]
    for px in np.arange(np.ceil(xa),np.floor(xb)+1):
        b.append(f'<line class="grid" x1="{r(X(px))}" y1="{t1}" x2="{r(X(px))}" y2="{t1+h1}"/>')
        b.append(f'<line class="grid" x1="{r(X(px))}" y1="{t2}" x2="{r(X(px))}" y2="{t2+h2}"/>')
    b.append(f'<rect fill="none" stroke="var(--rule-strong)" x="{L}" y="{t1}" width="{R-L}" height="{h1}"/>')
    b.append(f'<rect fill="none" stroke="var(--rule-strong)" x="{L}" y="{t2}" width="{R-L}" height="{h2}"/>')
    # 帯
    b.append(f'<rect class="band-ext" x="{L}" y="{r(Ys(0))}" width="{R-L}" height="{r(Ys(-0.05)-Ys(0))}"/>')
    b.append(f'<line class="dim-g" x1="{L}" y1="{r(Ys(0))}" x2="{R}" y2="{r(Ys(0))}"/>')
    b.append(f'<line class="leader-c" x1="{L}" y1="{r(Ys(-0.05))}" x2="{R}" y2="{r(Ys(-0.05))}"/>')
    for s,lab in [(rng,'+0.1875'),(0,'0'),(-0.05,'−0.05'),(-rng,'−0.1875')]:
        b.append(text(L-6,Ys(s)+4,lab,'lbl-s mono','end'))
    b.append(text(L-6,t1-8,'s（em）','lbl-s','end'))
    pts=' '.join(f'{X(x):.1f},{Ys(s):.1f}' for x,s in zip(xs,o['s']))
    b.append(f'<polyline class="curve" points="{pts}"/>')
    for c,lab in [(1,'1'),(0.5,'0.5'),(0,'0')]:
        b.append(text(L-6,Yc_(c)+4,lab,'lbl-s mono','end'))
    b.append(f'<line class="tick" x1="{L}" y1="{r(Yc_(0.5))}" x2="{R}" y2="{r(Yc_(0.5))}"/>')
    pa=' '.join(f'{X(x):.1f},{Yc_(c):.1f}' for x,c in zip(xs,o['ao']))
    pf=' '.join(f'{X(x):.1f},{Yc_(c):.1f}' for x,c in zip(xs,o['af']))
    b.append(f'<polyline fill="none" stroke="var(--ctrl)" stroke-width="1.6" points="{pa}"/>')
    b.append(f'<polyline fill="none" stroke="var(--guide)" stroke-width="2.2" points="{pf}"/>')
    for x,a1,a2 in zip(pc,op['ao'],op['af']):
        b.append(f'<circle cx="{r(X(x))}" cy="{r(Yc_(a1))}" r="2.6" fill="var(--surface)" stroke="var(--ctrl)" stroke-width="1.3"/>')
        b.append(f'<circle cx="{r(X(x))}" cy="{r(Yc_(a2))}" r="2.6" fill="var(--guide)"/>')
    for px in range(int(np.ceil(xa/5)*5),int(xb)+1,5):
        b.append(text(X(px),t2+h2+16,str(px),'lbl-s mono','middle'))
    b.append(text(R,t2+h2+34,'画面の x（px）。縦の細線は画素の境、点は画素の中心','lbl-s','end'))
    lg=t1+h1+26
    b.append(f'<line class="curve" x1="{L}" y1="{lg}" x2="{L+22}" y2="{lg}"/>'+text(L+28,lg+4,'距離 s','lbl-s'))
    b.append(f'<line class="dim-g" x1="{L+100}" y1="{lg}" x2="{L+122}" y2="{lg}"/>'+text(L+128,lg+4,'面の縁 s = −_FaceDilate','lbl-s'))
    b.append(f'<line class="leader-c" x1="{L+300}" y1="{lg}" x2="{L+322}" y2="{lg}"/>'+text(L+328,lg+4,'縁取りの外縁 s = −(_FaceDilate + _OutlineWidth)','lbl-s'))
    lg2=t2-10
    b.append(f'<line stroke="var(--ctrl)" stroke-width="1.6" x1="{L}" y1="{lg2}" x2="{L+22}" y2="{lg2}"/>'+text(L+28,lg2+4,'ao（縁取り込みの形）','lbl-s'))
    b.append(f'<line stroke="var(--guide)" stroke-width="2.2" x1="{L+200}" y1="{lg2}" x2="{L+222}" y2="{lg2}"/>'+text(L+228,lg2+4,'af（面）','lbl-s'))
    b.append(text(L-6,t2+h2/2-16,'被覆率','lbl-s','end'))
    return svg(760,t2+h2+44,'\n'.join(b),'あ を 48px で描いたときの 1 行の断面。距離と 2 つの被覆率')

def pixel_rows():
    g=G_A; Yc=-(ROW_K+0.5); ey=(Yc-YB)/F
    out=[]
    for X in [230.5,233.5]:
        ex=(X-PEN_A)/F
        o=frag(g,np.array([ex]),np.array([ey]),1/F,STYLE_OUT,(1,1,1),1)
        a=o['a'][0]; rgb=o['rgb'][0]
        res=rgb*a+BG*(1-a)
        out.append(dict(X=X,ex=ex,ey=ey,u=float(o['u'][0]),v=float(o['v'][0]),r=float(o['r'][0]),s=float(o['s'][0]),ao=float(o['ao'][0]),af=float(o['af'][0]),aO=float(o['aO'][0]),a=float(a),rgb=rgb,res=res,res8=(lin2srgb(res)*255+0.5).astype(int)))
    return out
