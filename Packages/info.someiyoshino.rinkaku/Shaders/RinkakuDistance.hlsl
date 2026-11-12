#ifndef RINKAKU_DISTANCE_INCLUDED
#define RINKAKU_DISTANCE_INCLUDED

// Rinkaku の画素の符号付き距離（README §3.5）。グリフ表現のシェーダ Rinkaku/Glyph と、GPU の途中の値を読む計測のシェーダが同じ手順を使うように、
// 距離の求め方だけをここに置く。RinkakuTrace は計測が読む途中の値で、グリフ表現のシェーダは読まないのでコンパイラが消す。

// ページの幅（texel・2^12）と、セル 1 つのセグメントの数の上限（RinkakuEncoding と同じ値）。
#define RINKAKU_PAGE_WIDTH_BITS 12
#define RINKAKU_MAX_ITEMS 32

// 端と途中を「同じ近さ」とみなす距離の幅（em）。途中の点は係数から B(t) を組み立てるので、端点から直接測る端の距離と float の丸めで
// 座標の数 ulp（1e−7 em ほど）ずれうる。この幅の中なら端を選ぶ（頂点の近くでは擬似法線の符号も正しく、距離の差は幅を超えない）。
#define RINKAKU_ENDPOINT_SLACK		1e-6

Texture2D<float4>	_MainTex;
// グリッドの 1em のセルの数（RinkakuRepresentation.ConfigureMaterial が入れる）。
uniform float		_IndexEm;

// 最も近いセグメントについて、距離を解く途中の値。
struct RinkakuTrace
{
	uint	item;		// 最も近いセグメントの番号（1 始まり・0 はセグメントが無い）
	uint	count;		// セルのセグメントの数
	uint	flags;		// 1: 実根 1 つの枝 ／ 2: Newton 法を試した ／ 4: Newton 法の t を採った ／ 8: 始点を採った ／ 16: 終点を採った ／ 32: 直線の正射影
	float	tClosed;	// 閉形式の t
	float	tNewton;	// Newton 法の後の t
	float	t;			// 両端と比べた後の t
	float	best;		// 返した距離の二乗
	float	start;		// 始点への距離の二乗
	float	end;		// 終点への距離の二乗
	float2	normal;		// 符号を決めた法線
	float	side;		// dot(pos − foot, normal)
	int2	cellTexel;	// 引いたセルの texel
	float4	itemD2;		// セルの先頭 4 本の距離の二乗（セルに無い本は −1）
	float4	itemT;		// セルの先頭 4 本の t
};

// texel のバイト（RGBA8 を 0〜255 の整数へ）。
uint4 RinkakuBytes(uint address)
{
	int2 texel = int2(address & ((1u << RINKAKU_PAGE_WIDTH_BITS) - 1u), address >> RINKAKU_PAGE_WIDTH_BITS);
	return (uint4)round(_MainTex.Load(int3(texel, 0)) * 255.0);
}

uint4 RinkakuBytesAt(int2 texel)
{
	return (uint4)round(_MainTex.Load(int3(texel, 0)) * 255.0);
}

// セグメントテーブルの項目（texel 2 つ・README §3.2）の 1 つ目の texel: 始点（em）。
float2 RinkakuStart(uint4 a)
{
	return float2(a.x | (a.y << 8), a.z | (a.w << 8)) / 16384.0 - 1.0;
}

// 項目の 2 つ目の texel: 制御点の始点からの差（em）。
float2 RinkakuControlDelta(uint4 b)
{
	int dx = int(b.x | ((b.y & 15u) << 8));
	int dy = int((b.y >> 4) | (b.z << 4));
	dx = dx >= 2048 ? dx - 4096 : dx;
	dy = dy >= 2048 ? dy - 4096 : dy;
	return float2(dx, dy) / 4096.0;
}

// 項目の 2 つ目の texel の w: 擬似法線（単位）。最も近いセグメントの端点にだけ要るので、セグメントを評価する間は読まない。
float2 RinkakuPseudoNormal(uint angle)
{
	float turn = angle * (6.28318530718 / 256.0);
	return float2(cos(turn), sin(turn));
}

float RinkakuDist2(float2 d, float2 c, float2 b, float t)
{
	float2 e = d + (c + b * t) * t;
	return dot(e, e);
}

// 点から直線のセグメント (p0, p1) への最短距離の二乗（正射影）と、最も近い点 foot・そこが端か（endpoint: 0＝途中・1＝始点・2＝終点）・右手の法線 normal。
// 直線は制御点の差が (0, 0) の項目（README §3.2）。端は正射影の t を丸めたかで決め、同じ枝の中で整数に書く（後から t と 0・1 を比べ直さない）。
// 帯の境（t がちょうど 0・1）で途中と端を取り違えても、そこでは接線の法線と擬似法線の符号が一致するので、符号は変わらない。
float RinkakuLineDistanceSquared(
	float2 pos, float2 p0, float2 p1, out float t, out uint endpoint, out float2 foot, out float2 normal, inout RinkakuTrace trace)
{
	float2 v = p1 - p0;
	float raw = dot(pos - p0, v) / dot(v, v);
	endpoint = raw <= 0.0 ? 1u : (raw >= 1.0 ? 2u : 0u);
	t = saturate(raw);
	foot = p0 + v * t;
	normal = float2(v.y, -v.x);
	float2 e = pos - foot;
	float2 toStart = p0 - pos;
	float2 toEnd = p1 - pos;
	trace.flags = 32u | (endpoint == 1u ? 8u : (endpoint == 2u ? 16u : 0u));
	trace.tClosed = t;
	trace.tNewton = t;
	trace.start = dot(toStart, toStart);
	trace.end = dot(toEnd, toEnd);
	return dot(e, e);
}

// 点から 2 次ベジエ (p0, q, p1) への最短距離の二乗（閉形式＋Newton 法 1 回）と、最も近い点 foot・そこが端か（endpoint: 0＝途中・1＝始点・
// 2＝終点）・途中なら接線の右手の法線 normal。直線は RinkakuLineDistanceSquared で測るので、ここに来るセグメントは曲線で、ほぼ直線の曲線はベイクで直線に
// してある（|p0 − 2q + p1| ≥ 1/1024 em）——b = p0 − 2q + p1 は 0 にならない（README §3.2）。
float RinkakuDistanceSquared(
	float2 pos, float2 p0, float2 q, float2 p1, out float t, out uint endpoint, out float2 foot, out float2 normal, inout RinkakuTrace trace)
{
	float2 a = q - p0;
	float2 b = p0 - 2.0 * q + p1;
	float2 c = a * 2.0;
	float2 d = p0 - pos;
	float kk = 1.0 / max(dot(b, b), 1e-12);
	float kx = kk * dot(a, b);
	float ky = kk * (2.0 * dot(a, a) + dot(d, b)) / 3.0;
	float kz = kk * dot(d, a);
	float p = ky - kx * kx;
	float q3 = kx * (2.0 * kx * kx - 3.0 * ky) + kz;
	float h = q3 * q3 + 4.0 * p * p * p;
	uint flags = 0u;
	if (h >= 0.0)
	{
		flags |= 1u;
		h = sqrt(h);
		float2 x = (float2(h, -h) - q3) / 2.0;
		float2 uv = sign(x) * pow(abs(x), 1.0 / 3.0);
		t = saturate(uv.x + uv.y - kx);
	}
	else
	{
		float z = sqrt(-p);
		float v = acos(clamp(q3 / (p * z * 2.0), -1.0, 1.0)) / 3.0;
		float m = cos(v);
		float n = sin(v) * 1.732050808;
		float t1 = saturate((m + m) * z - kx);
		float t2 = saturate((-n - m) * z - kx);
		t = RinkakuDist2(d, c, b, t1) <= RinkakuDist2(d, c, b, t2) ? t1 : t2;
	}
	float tClosed = t;
	// 閉形式の t を Newton 法の 1 反復でリファインする（ほぼ直線の長い曲線で係数が大きくなり、float の桁落ちが t に出るのを抑える）。
	float2 e = d + (c + b * t) * t;
	float2 tangent = c + 2.0 * b * t;
	float f = dot(e, tangent);
	float df = dot(tangent, tangent) + 2.0 * dot(e, b);
	if (df > 0.0)
	{
		flags |= 2u;
		float refined = saturate(t - f / df);
		if (RinkakuDist2(d, c, b, refined) < dot(e, e))
		{
			flags |= 4u;
			t = refined;
		}
	}
	float tNewton = t;
	float best = RinkakuDist2(d, c, b, t);
	// 塗る側は進行方向の右手。途中の点は接線 B'(t) の右手の法線で内外を決める。
	foot = p0 + (c + b * t) * t;
	float2 heading = c + 2.0 * b * t;
	normal = float2(heading.y, -heading.x);
	endpoint = 0u;

	// 両端も必ず比べ、同じ近さなら端を選ぶ（README §3.5）。頂点が最近傍になる楔の中では、端の擬似法線でなければ符号を決められない
	// ——接線の法線で決めると、鋭い角の楔の中で符号を誤る。閉形式の根は GPU の精度（acos・pow の近似、掛け算と足し算のまとめ方）で
	// 端からわずかにずれうるので、端は根に頼らずに比べる。端を選んだことは、端を選んだ枝の中で整数（endpoint）に書き、後から t と 0・1 を
	// 比べて決め直さない（D3D11 の GPU で、t がちょうど 1.0 なのに t >= 1.0 の枝が通らないことがあった）。
	// 距離の二乗どうしで比べるので、幅は (r + δ)² − r² = δ(2r + δ) に直す。
	float slack = RINKAKU_ENDPOINT_SLACK * (2.0 * sqrt(best) + RINKAKU_ENDPOINT_SLACK);
	float start = dot(d, d);
	if (start <= best + slack)
	{
		flags |= 8u;
		t = 0.0;
		best = start;
		foot = p0;
		endpoint = 1u;
	}
	float2 toEnd = p1 - pos;
	float end = dot(toEnd, toEnd);
	if (end <= best + slack)
	{
		flags |= 16u;
		t = 1.0;
		best = end;
		foot = p1;
		endpoint = 2u;
	}

	trace.flags = flags;
	trace.tClosed = tClosed;
	trace.tNewton = tNewton;
	trace.start = start;
	trace.end = end;
	return best;
}

// 画素の符号付き距離（em・インク領域の内側が正・README §3）。half は、ナローインデックスで texel のどちらの半分がこのセルか（0＝前半・1＝後半）。
float RinkakuSignedDistanceInCell(float2 pos, int2 cellTexel, uint half, uint dataBase, bool wide, out RinkakuTrace trace)
{
	trace = (RinkakuTrace)0;
	trace.cellTexel = cellTexel;
	trace.itemD2 = -1.0;
	trace.itemT = -1.0;

	// セルはセグメントの番号 2 つ (a, b)。ナローインデックスは texel の半分（8bit×2）、ワイドインデックスは texel 1 つ（16bit×2）（README §3.3）。
	uint4 first = RinkakuBytesAt(cellTexel);
	uint a;
	uint b;
	if (wide)
	{
		a = first.x | (first.y << 8);
		b = first.z | (first.w << 8);
	}
	else
	{
		a = half != 0u ? first.z : first.x;
		b = half != 0u ? first.w : first.y;
	}

	// 2 本以下のセルは番号そのもの。3 本以上のセルは (レコードの後ろ向きの差の下の桁, 印) で、レコードを 1 回読む: 短いレコードは番号 4 つ
	//（3 本か 4 本・空きは 0）、長いレコードは [セグメントの数, 番号, …]（5 本以上。先頭の 3 本はレコードの最初の読みに入っている）。
	uint slots[4];
	slots[0] = a;
	slots[1] = b;
	slots[2] = 0u;
	slots[3] = 0u;
	// 印は番号の最大の値から下へ 4 つ: 下の 1 ビットが長いレコードか、その上の 1 ビットがレコードの位置の上の桁。
	uint below = (wide ? 65535u : 255u) - b;
	bool list = false;
	uint listAddress = 0u;
	uint count;
	if (below < 4u)
	{
		listAddress = dataBase - (a + ((below >> 1) << (wide ? 16u : 8u)));
		uint4 record = RinkakuBytes(listAddress);
		if (wide)
		{
			uint4 second = RinkakuBytes(listAddress + 1u);
			record = uint4(record.x | (record.y << 8), record.z | (record.w << 8), second.x | (second.y << 8), second.z | (second.w << 8));
		}
		if ((below & 1u) == 0u)
		{
			slots[0] = record.x;
			slots[1] = record.y;
			slots[2] = record.z;
			slots[3] = record.w;
			count = record.w != 0u ? 4u : 3u;
		}
		else
		{
			list = true;
			count = record.x;
			slots[0] = record.y;
			slots[1] = record.z;
			slots[2] = record.w;
		}
	}
	else
	{
		count = (a != 0u ? 1u : 0u) + (b != 0u ? 1u : 0u);
	}
	trace.count = count;

	float best = 1e30;
	bool found = false;
	float2 bestFoot = pos;
	float2 bestNormal = float2(0.0, 0.0);
	uint bestEndpoint = 0u;
	uint bestEntry = 0u;
	[loop]
	for (uint k = 0u; k < count && k < RINKAKU_MAX_ITEMS; k++)
	{
		uint item;
		if (k < 3u || !list)
		{
			item = slots[k];
		}
		else
		{
			// 長いレコードはセグメントの数の後ろに番号が並ぶ（ナローインデックスは 1 バイトずつ、ワイドインデックスは 2 バイトずつ）。
			uint byteIndex = (1u + k) * (wide ? 2u : 1u);
			uint4 bytes = RinkakuBytes(listAddress + (byteIndex >> 2));
			uint component = byteIndex & 3u;
			item = bytes[component];
			if (wide)
			{
				item |= bytes[component + 1u] << 8;
			}
		}

		// セグメント item は項目 item − 1 の始点と制御点、項目 item の始点（＝終点）でできる。擬似法線（項目の 2 つ目の texel の w）は、最も近い
		// セグメントが端で決まったときにだけ、ループの後で読む。
		uint entry = dataBase + 2u * (item - 1u);
		float2 p0 = RinkakuStart(RinkakuBytes(entry));
		uint4 controlBytes = RinkakuBytes(entry + 1u);
		float2 q = p0 + RinkakuControlDelta(controlBytes);
		float2 p1 = RinkakuStart(RinkakuBytes(entry + 2u));
		// 制御点の差が (0, 0) の項目は直線（README §3.2）。バイトで判定する（差の 24bit が全部 0）。
		bool isLine = (controlBytes.x | controlBytes.y | controlBytes.z) == 0u;

		// セルのセグメントは全部解く。3 点の AABB までの距離（セグメントまでの距離の下限）が今の最短より遠いセグメントを飛ばす検査は置かない
		// ——曲線優先の並びでは近い直線から上限を得るのが遅れ、検査の分岐とループの制御の費用が、省ける解く手間を上回る（README §3.5）。
		float4 slot = float4(k == 0u, k == 1u, k == 2u, k == 3u);

		float t;
		uint endpoint;
		float2 foot;
		float2 normal;
		RinkakuTrace curve = (RinkakuTrace)0;
		float d2;
		// 直線と曲線で別の手順を通す。ウェーブの中で両方が混ざれば両方を進む（どちらかだけなら片方）。
		[branch]
		if (isLine)
		{
			d2 = RinkakuLineDistanceSquared(pos, p0, p1, t, endpoint, foot, normal, curve);
		}
		else
		{
			d2 = RinkakuDistanceSquared(pos, p0, q, p1, t, endpoint, foot, normal, curve);
		}
		trace.itemD2 = lerp(trace.itemD2, d2, slot);
		trace.itemT = lerp(trace.itemT, t, slot);
		if (d2 < best)
		{
			best = d2;
			found = true;
			bestFoot = foot;
			bestNormal = normal;
			bestEndpoint = endpoint;
			// 始点は項目 item − 1、終点は項目 item（セグメントテーブルの番地で 2 つ先）。
			bestEntry = endpoint == 2u ? entry + 2u : entry;

			trace.item = item;
			trace.flags = curve.flags;
			trace.tClosed = curve.tClosed;
			trace.tNewton = curve.tNewton;
			trace.t = t;
			trace.best = d2;
			trace.start = curve.start;
			trace.end = curve.end;
		}
	}

	// 最も近いセグメントの向きで符号を決める: 途中は接線の右手の法線、端は擬似法線（頂点を共有する 2 セグメントは同じ項目の擬似法線を読む）。
	float2 normal = bestNormal;
	if (bestEndpoint != 0u)
	{
		normal = RinkakuPseudoNormal(RinkakuBytes(bestEntry + 1u).w);
	}
	float side = dot(pos - bestFoot, normal);
	bool inside = found && side >= 0.0;
	trace.normal = normal;
	trace.side = side;

	float magnitude = sqrt(best);
	return inside ? magnitude : -magnitude;
}

// 補間された em 座標と UV0.xy（インデックスのずれ K とセグメントテーブルの番地・README §3.4）から、画素の符号付き距離を求める。
float RinkakuSignedDistance(float2 em, float2 data, out RinkakuTrace trace)
{
	// インデックスのずれ K（x 13bit・y 11bit・足し値 64）とセグメントテーブルの番地（ワイドインデックスは負）。
	uint packed = (uint)round(data.x);
	int2 offset = int2(int(packed & 8191u), int(packed >> 13)) - 64;
	bool wide = data.y < 0.0;
	uint dataBase = (uint)round(abs(data.y));
	// ナローインデックスは texel 1 つにセル 2 つ（偶数のセルが前半）。>> は符号を保つので、負のセルでも floor(cell.x / 2) になる。
	int2 cell = int2(floor(em * _IndexEm));
	int2 cellTexel = int2(wide ? cell.x : cell.x >> 1, cell.y) + offset;
	uint half = wide ? 0u : uint(cell.x & 1);
	return RinkakuSignedDistanceInCell(em, cellTexel, half, dataBase, wide, trace);
}

#endif
