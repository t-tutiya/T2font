# Third Party Notices

本パッケージは第三者のコードを含まない。手法は次の公開された文献の考え方に基づく（README §9）。

- **2 次ベジエへの距離の閉形式**: 点から 2 次ベジエ曲線への最短距離を、接線との内積が 0 になる三次方程式を閉形式で解いて求める。
  Inigo Quilez, "2D distance functions"（https://iquilezles.org/articles/distfunctions2d/）の 2 次ベジエの節の考え方。
- **擬似法線による符号**: 最近傍が頂点のとき、隣り合う 2 つの辺の法線の和で内外を決める。
  J. Andreas Bærentzen and Henrik Aanæs, "Signed Distance Computation Using the Angle Weighted Pseudonormal",
  IEEE Transactions on Visualization and Computer Graphics 11(3), 2005。
- **曲線をそのまま GPU で描くフォントの描画の先行例**: Eric Lengyel, "GPU-Centered Font Rendering Directly from Glyph Outlines",
  Journal of Computer Graphics Techniques 6(2), 2017。本パッケージは被覆の帯の索引ではなく、最近傍の区間の索引と符号付き距離を使う。
- **最近傍の種の写像**: Guodong Rong and Tiow-Seng Tan, "Jump Flooding in GPU with Applications to Voronoi Diagram and Distance Transform",
  ACM Symposium on Interactive 3D Graphics and Games, 2006。本パッケージは画素ごとの 1 点ではなく、texel の面積に対する集合をベイクで求める。

同じ系譜の先行例（セルごとに最近傍になりうる輪郭の特徴だけを持ち、厳密な距離から効果を描く）。本パッケージはこれらと独立に設計したが、手法の中核は
これらが先に示している。

- **線分のボロノイ解析による一様格子と厳密な符号付き距離（グリフ）**: Zheng Qin, Michael D. McCool, and Craig S. Kaplan,
  "Real-Time Texture-Mapped Vector Glyphs", ACM Symposium on Interactive 3D Graphics and Games, pp. 125–132, 2006。
- **特徴を 1 本ずつ置きセルごとの番号の列で引く格子（一般のベクタテクスチャ）**: Zheng Qin, Michael D. McCool, and Craig S. Kaplan,
  "Precise Vector Textures for Real-Time 3D Rendering", ACM Symposium on Interactive 3D Graphics and Games, pp. 199–206, 2008。
- **セルごとの近い円弧の一覧と符号付き距離（オープンソースの文字の描画）**: Behdad Esfahbod, GLyphy（https://github.com/behdad/glyphy・2012）。
  本パッケージは円弧で近似せず 2 次ベジエのまま解き、セルの集合を上位集合からサンプリングで最近傍の集合まで絞る。
- **セルに特化した可変長のプリミティブの列**: Diego Nehab and Hugues Hoppe, "Random-Access Rendering of General Vector Graphics",
  ACM Transactions on Graphics 27(5), 2008。
