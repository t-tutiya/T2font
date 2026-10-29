# シェーダ T2Font/SDF の frag と同じ式の CPU 版と、図の元データ（Dump の出力）の読み込み。
# 元データの置き場は環境変数 T2FONT_FIGURES_WORK（build.py が入れる）。
import json, os, numpy as np
WORK=os.environ['T2FONT_FIGURES_WORK']
D=json.load(open(os.path.join(WORK,'dump.json'),encoding='utf-8'))
W=D['pages'][0]['Width']; H=D['pages'][0]['Height']
PAGE=np.frombuffer(open(os.path.join(WORK,'page0.bin'),'rb').read(),dtype=np.uint8).reshape(H,W).astype(np.float64)/255.0  # row 0 = bottom
RANGE=D['rangeEm']; EMPX=D['emPx']
G={g['ch']:g for g in D['glyphs']}

def tex2d(u,v):
    # bilinear・clamp。texel の中心は (i + 0.5) / W
    x=np.asarray(u)*W-0.5; y=np.asarray(v)*H-0.5
    x0=np.floor(x); y0=np.floor(y); fx=x-x0; fy=y-y0
    def at(ix,iy):
        ix=np.clip(ix,0,W-1).astype(int); iy=np.clip(iy,0,H-1).astype(int); return PAGE[iy,ix]
    return (at(x0,y0)*(1-fx)*(1-fy)+at(x0+1,y0)*fx*(1-fy)+at(x0,y0+1)*(1-fx)*fy+at(x0+1,y0+1)*fx*fy)

def uv_of(g,ex,ey):
    u=(g['atlasLeft']+(ex-g['planeLeft'])*(g['atlasRight']-g['atlasLeft'])/(g['planeRight']-g['planeLeft']))/W
    v=(g['atlasBottom']+(ey-g['planeBottom'])*(g['atlasTop']-g['atlasBottom'])/(g['planeTop']-g['planeBottom']))/H
    return u,v

# シェーダの frag と同じ順に計算する（README §5 S2〜S4）。ex・ey は em の座標（UV0.zw）。
def frag(g,ex,ey,emPerPx,style,face_rgb,face_a):
    dil,fs,ow,osf,oc=style['dilate'],style['faceSoft'],style['outlineWidth'],style['outlineSoft'],style['outlineColor']
    u,v=uv_of(g,ex,ey)
    r=tex2d(u,v)
    s=(r-0.5)*(2*RANGE)
    faceEdge=-dil; outerEdge=-(dil+ow)
    sat=lambda x: np.clip(x,0,1)
    ao=sat((s-outerEdge)/(emPerPx+osf)+0.5)
    af=np.minimum(sat((s-faceEdge)/(emPerPx+fs)+0.5),ao)
    aO=oc[3]*(ao-af)
    a=af+aO
    pr=np.stack([face_rgb[i]*af+oc[i]*aO for i in range(3)],-1)
    rgb=pr/np.maximum(a,1e-5)[...,None]
    return dict(r=r,s=s,ao=ao,af=af,aO=aO,a=a*face_a,rgb=rgb,u=u,v=v)

def srgb2lin(c):
    c=np.asarray(c,dtype=np.float64); return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def lin2srgb(c):
    c=np.clip(np.asarray(c,dtype=np.float64),0,1); return np.where(c<=0.0031308,c*12.92,1.055*c**(1/2.4)-0.055)
