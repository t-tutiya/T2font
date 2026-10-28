Shader "T2Font/SDF" {

// 同梱の SDF の表現のシェーダ（README §9）。§5 のシェーダの契約（S1〜S8）を満たす。
// 面と縁取りの 2 つの境界それぞれに「位置」と「ぼかし」を 1 つずつ持ち、どの値も境界 1 本の位置かランプ 1 本の幅だけを
// 動かす。値はすべて em（字の大きさに対する比・README §6.1）。AA は値ではなく、常に画面の 1px で掛ける（ぼかしはその上に足す幅）。
// マテリアルは GlyphMaterialCache が作り、距離域を SdfRepresentation が、縁の値をキャッシュが入れる。
Properties {
	[NoScaleOffset]_MainTex	("Distance Page", 2D) = "white" {}

	// SDF の片側距離域（em）。SdfRepresentation.ConfigureMaterial が SdfEncoding.RangeEm を入れる。既定値はキャッシュを通らない
	// マテリアル（手で作ったもの）のためのもので、SdfEncoding.RangeEm と一致させる。
	_RangeEm			("SDF Range (em)", Float) = 0.1875

	_FaceDilate			("Face Dilate (em)", Float) = 0
	_FaceSoftness		("Face Softness (em)", Float) = 0
	// 縁取りの色は C# が linear に直して渡す（Vector は色空間の変換を受けない・README §6.1）。
	_OutlineColor		("Outline Color (linear)", Vector) = (0,0,0,1)
	_OutlineWidth		("Outline Width (em)", Float) = 0
	_OutlineSoftness	("Outline Softness (em)", Float) = 0

	_CullMode			("Cull Mode", Float) = 0
}

SubShader {

	Tags
	{
		"Queue"="Transparent"
		"RenderType"="Transparent"
		"IgnoreProjector"="True"
		"PreviewType"="Plane"
	}

	Cull [_CullMode]
	ZWrite Off
	ZTest [unity_GUIZTestMode]
	// rgb は straight α の合成、α だけ One OneMinusSrcAlpha の over 合成（a = α + a_dst × (1 − α)）にする（README §5 S6）。
	// α も SrcAlpha で合成すると、透明にクリアした RenderTexture へ描いたときに α が二乗され、その RT を重ねると下層が漏れる。
	Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

	Pass {
		CGPROGRAM
		#pragma target 3.0
		#pragma vertex vert
		#pragma fragment frag
		// 矩形クリップ（CanvasRenderer.EnableRectClipping・RectMask2D）。有効なバッチに Canvas が本キーワードと _ClipRect を渡す。
		// マテリアルは実行時に作るので shader_feature にすると変種がビルドで落ちる（README §5 S7）。
		#pragma multi_compile __ UNITY_UI_CLIP_RECT

		#include "UnityCG.cginc"
		#include "UnityUI.cginc"

		uniform sampler2D	_MainTex;
		uniform float		_RangeEm;
		uniform float		_FaceDilate;
		uniform float		_FaceSoftness;
		uniform float4		_OutlineColor;
		uniform float		_OutlineWidth;
		uniform float		_OutlineSoftness;
		uniform float4		_ClipRect;

		// 入力は POSITION・COLOR・TEXCOORD0 だけ（README §5 S1）。追加の頂点チャネルは Canvas 全体の設定なので使わない。
		struct Attributes {
			float4	position	: POSITION;
			float4	color		: COLOR;
			// xy＝ページの UV、zw＝その頂点の em 座標（GlyphGeometry が書く・README §4.2）。zw は AA の幅を画面の 1px に合わせるためだけに使う。
			float4	uv0			: TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings {
			float4	position	: SV_POSITION;
			float4	color		: COLOR;
			float4	uv0			: TEXCOORD0;
			// クリップ判定用の頂点位置。Canvas のバッチの頂点は Canvas のルート空間で来るので、_ClipRect と同じ空間になる
			// （uGUI の既定のシェーダの worldPosition と同じ扱い）。
			float4	canvasPos	: TEXCOORD1;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		Varyings vert(Attributes input)
		{
			Varyings output;
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			output.position = UnityObjectToClipPos(input.position);
			// 頂点色（rgb＝面の色・a＝フェード・README §5 S2）。色空間の扱いは uGUI の既定のまま。
			output.color	= input.color;
			output.uv0		= input.uv0;
			output.canvasPos = input.position;
			return output;
		}

		float4 frag(Varyings input) : SV_Target
		{
			// 輪郭からの距離（em・墨の内側が正）。ページは単チャネル（R8／BC4）で、値は .r に入る（README §9）。
			// 距離は焼くときに非ゼロ巻き数で塗った形から取ってあるので、輪郭が重なる字も抜けない（README §5 S8）。
			float s = (tex2D(_MainTex, input.uv0.xy).r - 0.5) * (2.0 * _RangeEm);

			// 画面 1px が何 em か（README §5 S3）。em 座標は quad の中で線形なので、その微分は正確で quad の中で一定
			// （回転・拡縮しても 1 表示 px が保たれる）。標本値 s の fwidth は bilinear で区分的になり、距離域の外で 0 になるので使わない。
			float emPerPx = 0.5 * (length(float2(ddx(input.uv0.z), ddy(input.uv0.z)))
								 + length(float2(ddx(input.uv0.w), ddy(input.uv0.w))));
			emPerPx = max(emPerPx, 1e-6);

			// 2 つの境界。ランプはどちらも境界の中央に置く（ぼかしを足しても 50% の位置は動かない・README §6.1）。
			float faceEdge  = -_FaceDilate;
			float outerEdge = -(_FaceDilate + _OutlineWidth);
			// 縁取り込みの形と面の被覆率。面は形の内側にあるので af ≤ ao に揃える。
			float ao = saturate((s - outerEdge) / (emPerPx + _OutlineSoftness) + 0.5);
			float af = min(saturate((s - faceEdge) / (emPerPx + _FaceSoftness) + 0.5), ao);

			// 入れ子の面積として合成する（README §5 S4）: 面が af、縁取りの帯が (ao − af)。縁取りが 1px より細くても、画素に占める
			// 縁の色の割合が面積と合う。premultiplied で足してから straight に戻す（Blend の rgb が straight の SrcAlpha のため）。
			float aOutline = _OutlineColor.a * (ao - af);
			float a = af + aOutline;
			float3 premul = input.color.rgb * af + _OutlineColor.rgb * aOutline;

			// フェード（頂点色の α）は最後に 1 回だけ掛ける（README §5 S2。面と縁の減衰を揃える）。
			float4 color = float4(premul / max(a, 1e-5), a * input.color.a);

			#ifdef UNITY_UI_CLIP_RECT
			color.a *= UnityGet2DClipping(input.canvasPos.xy, _ClipRect);
			#endif

			return color;
		}

		ENDCG
	}
}
}
