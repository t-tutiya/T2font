Shader "Rinkaku/Glyph" {

// Rinkaku のグリフ表現のシェーダ（README §3.5）。T2Font のシェーダの契約（S1〜S9）を満たす。
// 画素の em 座標から索引格子のセルを引き、セルに書かれた区間（たいてい 1〜4 本）だけへ厳密な距離を解き、最近傍の区間の向きで符号を決める。
// 得た符号付き距離（em・インク領域の内側が正）は T2Font の SDF のシェーダの s と同じ意味なので、エッジスタイルの式はそれと同じ。
Properties {
	[NoScaleOffset]_MainTex	("Data Page", 2D) = "black" {}

	// 索引格子の 1em のセルの数。RinkakuRepresentation.ConfigureMaterial がフォントの値を入れる。
	_IndexEm			("Index Cells per em", Float) = 16

	_FaceDilate			("Face Dilate (em)", Float) = 0
	_FaceSoftness		("Face Softness (em)", Float) = 0
	// 縁取りの色は C# が linear に直して渡す（Vector は色空間の変換を受けない）。
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
	// rgb は straight α の合成、α だけ One OneMinusSrcAlpha の over 合成（S6）。
	Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

	Pass {
		CGPROGRAM
		// texel を整数の番地で引く（Load）ので 3.5（OpenGL ES 3.0・D3D10 相当）。ページの高さを知らずに番地を 2 次元へ直せ、UV の丸めの誤差も無い。
		#pragma target 3.5
		#pragma vertex vert
		#pragma fragment frag
		// 矩形クリップ。マテリアルは実行時に作るので shader_feature にすると変種がビルドで落ちる（S7）。
		#pragma multi_compile __ UNITY_UI_CLIP_RECT

		#include "UnityCG.cginc"
		#include "UnityUI.cginc"
		// _MainTex・_IndexEm と距離の求め方（計測のシェーダと共有する）。
		#include "RinkakuDistance.hlsl"

		uniform float		_FaceDilate;
		uniform float		_FaceSoftness;
		uniform float4		_OutlineColor;
		uniform float		_OutlineWidth;
		uniform float		_OutlineSoftness;
		uniform float4		_ClipRect;
		// Canvas の Vertex Color Always in Gamma Color Space が有効なら 1（Properties に置かない・S9）。
		uniform int			_UIVertexColorAlwaysGammaSpace;

		// 入力は POSITION・COLOR・TEXCOORD0 だけ（S1）。
		struct Attributes {
			float4	position	: POSITION;
			float4	color		: COLOR;
			// xy＝索引のずれと区間の表の番地（RinkakuRepresentation・README §3.4）、zw＝その頂点の em 座標。
			float4	uv0			: TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Varyings {
			float4	position	: SV_POSITION;
			float4	color		: COLOR;
			float2	em			: TEXCOORD0;
			// 番地は 2^24 未満の整数で quad の 4 頂点で同じ。補間すると丸めで 1 ずれうるので補間しない。
			nointerpolation float2 data : TEXCOORD1;
			float4	canvasPos	: TEXCOORD2;
			UNITY_VERTEX_OUTPUT_STEREO
		};

		Varyings vert(Attributes input)
		{
			Varyings output;
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			output.position = UnityObjectToClipPos(input.position);
			// 頂点色（rgb＝面の色・a＝フェード・S2）。gamma のまま来たら組み込みの UI シェーダと同じ関数で linear に直す（S9）。α は直さない。
			output.color	= input.color;
			if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
			{
				output.color.rgb = UIGammaToLinear(output.color.rgb);
			}
			output.em		= input.uv0.zw;
			output.data		= input.uv0.xy;
			output.canvasPos = input.position;
			return output;
		}

		float4 frag(Varyings input) : SV_Target
		{
			// 画面 1px が何 em か（S3）。ループの前で取る（微分は一様な制御の流れの中で取る）。
			float emPerPx = 0.5 * (length(float2(ddx(input.em.x), ddy(input.em.x)))
								 + length(float2(ddx(input.em.y), ddy(input.em.y))));
			emPerPx = max(emPerPx, 1e-6);

			RinkakuTrace trace;
			float s = RinkakuSignedDistance(input.em, input.data, trace);

			// ここから T2Font の SDF のシェーダと同じ（S4）: 2 つの境界に位置とぼかしを 1 つずつ。ランプは境界の中央に置く。
			float faceEdge  = -_FaceDilate;
			float outerEdge = -(_FaceDilate + _OutlineWidth);
			float ao = saturate((s - outerEdge) / (emPerPx + _OutlineSoftness) + 0.5);
			float af = min(saturate((s - faceEdge) / (emPerPx + _FaceSoftness) + 0.5), ao);

			// 入れ子の面積として合成する。premultiplied で足してから straight に戻す（Blend の rgb が straight の SrcAlpha のため）。
			float aOutline = _OutlineColor.a * (ao - af);
			float a = af + aOutline;
			float3 premul = input.color.rgb * af + _OutlineColor.rgb * aOutline;

			// フェード（頂点色の α）は最後に 1 回だけ掛ける（S2）。
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
