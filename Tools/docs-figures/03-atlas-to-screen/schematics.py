# 03 章の模式図。
from figures import *

def box(x,y,w,h,title,subs=(),cls='',tcls='node-t',scls='node-s',asm=None,rx=6):
    s=f'<rect class="node {cls}" x="{r(x)}" y="{r(y)}" width="{r(w)}" height="{r(h)}" rx="{rx}"/>'
    s+=text(x+12,y+22,title,tcls)
    for i,t in enumerate(subs): s+=text(x+12,y+41+16*i,t,scls)
    if asm: s+=text(x+w-10,y+22,asm,'asm','end')
    return s

# ── 図 5: CanvasRenderer の口 ─────────────────────────────
def fig_slots():
    b=[]; y0=40; rh=58
    b.append(text(16,24,'T2Font が用意する物','lbl-b'))
    b.append(text(300,24,'CanvasRenderer','lbl-b'))
    b.append(text(520,24,'シェーダが受け取る物','lbl-b'))
    rows=[
     ('GlyphGeometry.CopyTo(mesh, 48)','位置は em × 48・UV0 は em のまま','SetMesh(mesh)','POSITION・TEXCOORD0'),
     ('GlyphMaterialCache.Get(表現, スタイル)','T2Font/SDF・_RangeEm・エッジスタイル','SetMaterial(material, 0)','_RangeEm・_FaceDilate …'),
     ('SdfRepresentation.GetTexture(glyph)','pages[cell.page]','SetTexture(page)','_MainTex'),
     ('faceColor.rgb・alpha','面の色とフェード','SetColor(color)','COLOR（頂点色）'),
     ('RectTransform の矩形','ルートの Canvas の空間','EnableRectClipping(rect)','UNITY_UI_CLIP_RECT・_ClipRect'),
    ]
    b.append(f'<rect class="node host" x="290" y="{y0-6}" width="196" height="{len(rows)*rh+4}" rx="6"/>')
    for i,(a1,a2,m,s) in enumerate(rows):
        y=y0+i*rh
        b.append(f'<rect class="node" x="16" y="{y}" width="246" height="{rh-10}" rx="5"/>')
        b.append(text(26,y+20,a1,'lbl-s mono')); b.append(text(26,y+37,a2,'lbl-s'))
        b.append(f'<rect class="node in" x="300" y="{y+6}" width="176" height="{rh-22}" rx="4"/>')
        b.append(text(310,y+29,m,'lbl-s mono'))
        b.append(f'<rect class="node sdf" x="520" y="{y}" width="224" height="{rh-10}" rx="5"/>')
        b.append(text(530,y+29,s,'lbl-s mono'))
        b.append(right(262,298,y+24)); b.append(right(478,518,y+24))
    yb=y0+len(rows)*rh+10
    b.append(text(300,yb+12,'materialCount = 1 を先に','lbl-s'))
    b.append(text(300,yb+28,'SetMesh は最後に呼ぶ','lbl-s'))
    b.append(text(520,yb+12,'テクスチャとクリップは','lbl-s'))
    b.append(text(520,yb+28,'Canvas がバッチへ渡す','lbl-s'))
    return svg(760,yb+40,'\n'.join(b),'CanvasRenderer の 5 つの口と、T2Font が渡す物・シェーダが受け取る物')

# ── 図 6: バッチ ────────────────────────────────────────
def fig_batch():
    b=[]
    chars=[g['ch'] for g in GL if not g['empty']]
    def row(y,chs,mat,label):
        s=[]; x=16; w=(728-8*(len(chars)-1))/len(chars)
        for i,c in enumerate(chs):
            s.append(f'<rect class="node" x="{r(x)}" y="{y}" width="{r(w)}" height="52" rx="4"/>')
            s.append(text(x+w/2,y+24,c,'node-t','middle'))
            s.append(text(x+w/2,y+42,f'{mat} × p0','lbl-s mono','middle'))
            x+=w+8
        return s,x-8
    b.append(text(16,18,'T2FontText 1（エッジスタイル None）— 字ごとの CanvasRenderer（空白は幾何が無いので作らない）','lbl-s'))
    s,xe=row(26,chars,'A','');  b+=s
    b.append(down(380,80,104))
    b.append(box(16,106,728,46,'バッチ 1: マテリアル A × ページ 0 → 描画 1 回',(),'sdf'))
    b.append(text(732,128,f'{len(chars)} quad・{len(chars)*4} 頂点・{len(chars)*2} 三角形','lbl-s mono','end'))
    y2=182
    b.append(text(16,y2-8,'T2FontText 2（縁取り 0.05em）— エッジスタイルが違うのでマテリアル B','lbl-s'))
    sub=['永','あ','ア']
    s,xe=row(y2,sub,'B',''); b+=s
    b.append(down(16+xe/2-8,y2+54,y2+78))
    b.append(box(16,y2+80,xe-16,46,'バッチ 2: マテリアル B × ページ 0',(),'sdf'))
    b.append(text(xe+16,y2+108,'マテリアルかテクスチャが変わる所でバッチが分かれる','lbl-s'))
    return svg(760,y2+140,'\n'.join(b),'Canvas が同じマテリアルとテクスチャの字を 1 回の描画にまとめる図')

# ── 図 7: GPU の段 ──────────────────────────────────────
def fig_gpu():
    rows=[
     (61,'バッチの頂点',['POSITION（ルートの Canvas の空間）・COLOR・TEXCOORD0（u, v, emX, emY）'],'Canvas','in'),
     (61,'頂点シェーダ vert',['UnityObjectToClipPos(position) → クリップ空間・色と UV0 と canvasPos をそのまま渡す'],'シェーダ','step'),
     (78,'ラスタライズ',['三角形が覆う画素の中心ごとにフラグメントを作り、varying を補間する','2×2 の画素を組にして回すので ddx・ddy が隣との差として取れる'],'GPU の固定機能','step'),
     (78,'フラグメントシェーダ frag',['tex2D(_MainTex, uv).r → 距離 s（em）・UV0.zw の微分 → 1 表示 px の em','→ 被覆率 ao・af → 面と縁取りの色 → フェード → クリップ'],'シェーダ','sdf'),
     (61,'ブレンド',['Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha・ZWrite Off・Cull Off'],'GPU の固定機能','step'),
     (44,'レンダーターゲットの画素',[],'§9','host'),
    ]
    b=[]; y=16
    for i,(h,t,s,a,c) in enumerate(rows):
        b.append(box(40,y,680,h,t,s,c,asm=a)); y+=h
        if i<len(rows)-1: b.append(down(380,y,y+20)); y+=22
    return svg(760,y+16,'\n'.join(b),'GPU の上の段の流れ図')

# ── 図 10: Canvas の描画モードとレンダーターゲット ─────────────────
def fig_modes():
    b=[]
    b.append(box(16,16,220,62,'Screen Space - Overlay',['カメラを持たない'],'in'))
    b.append(box(270,16,220,62,'Screen Space - Camera',['指定のカメラの前の平面'],'in'))
    b.append(box(524,16,220,62,'World Space',['シーンの中の平面'],'in'))
    b.append(down(380,78,104)); b.append(down(634,78,104))
    b.append(box(270,106,474,62,'カメラが描く',['不透明物の後、透明物の並び（Queue Transparent）の中で描く'],'step'))
    b.append(down(507,168,194))
    b.append(box(270,196,474,78,'カメラのカラーターゲット',['URP の中間のカラーテクスチャ（使うかどうかは URP が決める）','か、バックバッファ（中間を使わないとき）'],'step'))
    b.append(down(507,274,300))
    b.append(box(270,302,474,44,'中間を使ったときは、バックバッファへ写す（ブリット）',(),'step'))
    b.append(down(507,346,372))
    # Overlay
    b.append(f'<line class="flow" x1="126" y1="78" x2="126" y2="372"/><polygon class="ah-flow" points="126,374 122.4,366 129.6,366"/>')
    b.append(text(136,188,'URP の UI のパスが','lbl-s')); b.append(text(136,204,'シーンの画像の上へ描く','lbl-s'))
    b.append(text(136,220,'（rendersUIOverlay）','lbl-s mono'))
    b.append(box(16,374,728,46,'バックバッファ（スワップチェーンの今のフレームの画像）',(),'sdf'))
    b.append(down(380,420,446))
    b.append(box(16,448,728,46,'Present → ディスプレイ',(),'host'))
    return svg(760,510,'\n'.join(b),'Canvas の描画モードごとのレンダーターゲットと画面までの流れ')

# ── 図 11: 1 フレームの順序 ────────────────────────────────
def fig_frame():
    rows=[
     ('Update','PlayerLoop の Update','ホストが文字列・色などを変える（T2FontText のプロパティは _dirty を立てるだけ）','step'),
     ('LateUpdate','PreLateUpdate.ScriptRunBehaviourLateUpdate','T2FontText.LateUpdate: _dirty なら Rebuild（§3〜§6）','host'),
     ('Canvas の更新','PostLateUpdate.PlayerUpdateCanvases','Canvas.willRenderCanvases（uGUI のレイアウトと Graphic の再構築）','step'),
     ('ジオメトリをまとめる','PostLateUpdate.PlayerEmitCanvasGeometry','変わった Canvas のバッチを作り直す（§7）','step'),
     ('描く','PostLateUpdate.FinishFrameRendering','URP がカメラを描く。Camera・World の Canvas はカメラの中、Overlay の Canvas は UI のパス（§8・§9）','sdf'),
     ('画面へ出す','PostLateUpdate.PresentAfterDraw','バックバッファを Present する','step'),
    ]
    b=[]; y=16; h=58
    for i,(t,pl,s,c) in enumerate(rows):
        b.append(f'<rect class="node {c}" x="40" y="{y}" width="680" height="{h}" rx="6"/>')
        b.append(text(54,y+23,t,'node-t')); b.append(text(706,y+23,pl,'asm','end'))
        b.append(text(54,y+44,s,'lbl-s'))
        y+=h
        if i<len(rows)-1: b.append(down(380,y,y+18)); y+=20
    return svg(760,y+16,'\n'.join(b),'1 フレームの中で T2Font の描画が進む順序')
