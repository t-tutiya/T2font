# T2Font

**Glyph representation API for Unity uGUI text.** A font asset is split into a *typography table* (cmap, advances, ink
bounds, vertical metrics — identical for every rendering technique) and a pluggable *glyph representation* (how the ink
is drawn: SDF atlas, curves, bitmaps …). Hosts place glyphs without knowing the representation. The package ships an SDF
representation, a pure C# TrueType reader and baker, a `.t2font` importer, a viewer and a conformance suite.
License: MIT (`LICENSE.md`).

以下、日本語で契約を定める。**本書は T2Font の契約の正典**で、パッケージのコードのコメントは本書の節を「（README §n）」で参照する。

---

## 1. 構成（3 層）

```
T2FontAsset（ScriptableObject・ロードの単位）
├─ タイポグラフィテーブル  face（縦メトリクス）・codepoints[]（昇順）・glyphs[]（GlyphMetrics＝送り幅とインク境界）・notdef
├─ 表現                    representation → GlyphRepresentation（抽象 ScriptableObject・サブアセット）
│                          ├─ SdfRepresentation（本パッケージ）
│                          └─ ほかの表現（別のパッケージが足す）
└─ sourceHash
ホスト（表現を知らない）: 字を画面へ置く側。本パッケージの検証ビュー T2FontText、または利用側のテキスト描画
```

| アセンブリ | フォルダ | UnityEngine | 役割 |
|---|---|---|---|
| `Someiyoshino.T2Font.Core` | `Core/` | 使わない | タイポグラフィテーブルの型・グリフインデックスの検索・エッジスタイルの契約・字の走査 |
| `Someiyoshino.T2Font` | `Runtime/` | 使う | `T2FontAsset`・`GlyphRepresentation`・`GlyphGeometry`・`GlyphMaterialCache`・SDF の表現 |
| `Someiyoshino.T2Font.Bake` | `Bake/` | 使わない（Editor 専用） | TrueType の読み取り・文字セット・ベイク仕様・タイポグラフィテーブルの生成・SDF のベイク・表現の種類の記述子 |
| `Someiyoshino.T2Font.Editor` | `Editor/` | 使う（Editor 専用） | `.t2font` のインポーター（表現の種類でベイカーへ振り分ける）・SDF のベイカー |
| `Someiyoshino.T2Font.Viewer` | `Viewer/` | 使う | 検証ビュー `T2FontText`（自動参照しない） |
| `Someiyoshino.T2Font.Conformance` | `Conformance/` | 使う（Editor 専用） | 表現の適合検査 `GlyphConformance`（自動参照しない） |

Core と Bake は UnityEngine を参照しない純 C# なので、Unity を起動せずに外部の NUnit で検査できる。

## 2. 原則

### 2.1 明示

**未知のもの・描けないもの・見つからないものは、インポートかロードで止める。** 既定値で黙って読み替えない・効果を黙って落とさない・
無い字を黙って消さない。止めるときは何が・どこで違うかを文面で名指す。

### 2.2 依存の向き

パッケージは表現の具体型を知らない。表現（別のパッケージ）がパッケージの API に依存する。表現の種類を足しても、ホストのコードは変わらない。

### 2.3 組版は表現から独立する

タイポグラフィテーブル（送り幅・インク境界・縦メトリクス）は、表現によらず**同じリーダーと同じ手順**で作る（§7.3）。同じ元フォントなら、どの表現でベイクしても
送り幅は同一で、表現を替えても組版が動かない。

### 2.4 生成物はリポジトリに置かない

アトラスやタイポグラフィテーブルは、ベイク仕様（`.t2font`）と元フォントからインポーターがベイクする生成物で、Unity の Library に作られる。リポジトリに置くのは
ベイク仕様と元フォントだけ。

## 3. アセット

### 3.1 T2FontAsset

`T2FontAsset`（sealed の ScriptableObject）が持つもの:

| 欄 | 意味 |
|---|---|
| `face` | `FontFaceMetrics`（ascender・descender・lineGap。単位は em） |
| `codepoints` | 字（Unicode スカラー値）の昇順の配列。`i < codepoints.Length` のグリフインデックス `i` は字 `codepoints[i]` のグリフ |
| `glyphs` | `GlyphMetrics` のグリフインデックス順の配列。cmap を持つグリフの**後ろに** cmap を持たないグリフ（.notdef ほか）を置く |
| `notdef` | 代替グリフ（元フォントの glyph 0）のグリフインデックス |
| `representation` | グリフの表現（§3.4） |
| `sourceHash` | 生成元（生成器のバージョン・ベイク仕様・元フォント）のハッシュ |

- **`Resolve(codepoint, out isSubstitute)`** はグリフインデックスを返す。収録していない字は `notdef` を返し、コードポイントごとに 1 回 `LogError` を出す
  ——送り幅 0 で消すと、出荷物で字が黙って消える（§2.1）。報告済みの記録は再生の開始ごとに捨てる。
- **`Metrics(glyph)`** はタイポグラフィテーブルの記録、**`RepresentationOf(glyph)`** はそのグリフを描く表現（今の版は常に `representation`。1 フォントに複数の
  表現を持たせる拡張の口）。

### 3.2 グリフインデックス

字は二分探索 1 回でグリフインデックスになる（約 7,000 字で比較 13 回・割り当て無し）。グリフインデックスはタイポグラフィテーブルの配列の添字そのもので、間接の表は無い。
表現のデータもグリフインデックスで引く。cmap を持たないグリフ（.notdef・将来の縦書きの代替グリフや合字）も普通のグリフインデックスを持つ。

### 3.3 単位

タイポグラフィテーブルの値は **em**（字の大きさに対する比）で、ペン原点とベースラインを原点とし、y は上向き。`GlyphMetrics` の ink* は**インク境界
そのもの**で、表現が描くための余白（距離レンジなど）を含まない。インクの無い字（空白）は ink の幅が 0。

### 3.4 表現のサブアセット

`GlyphRepresentation` は抽象 ScriptableObject で、インポーターが `T2FontAsset` のサブアセットとして作る。表現を足すパッケージは派生クラスを
**クラス名とファイル名を一致させて**置く（Unity が MonoScript を解決するため）。

## 4. 描画の契約（頂点・テクスチャ・マテリアル）

### 4.1 ホストと表現の分担

| 仕事 | ホスト | 表現 |
|---|---|---|
| 字 → グリフインデックス・未収録の報告 | `T2FontAsset.Resolve` | — |
| 送り幅・行 | タイポグラフィテーブル | — |
| 頂点の位置と UV0 | `GlyphGeometry` を渡し、em × 表示サイズへ変換して `Mesh` へ詰める | em の座標と UV0.xy を書く（`WriteGeometry`） |
| テクスチャ | `CanvasRenderer.SetTexture` | どのテクスチャか（`GetTexture`） |
| マテリアル | `GlyphMaterialCache` から引く | シェーダ・表現の定数・共有してよい範囲 |
| 色・フェード・クリップ | `CanvasRenderer` の色と α・矩形クリップ | シェーダが §5 のとおり解釈する |

### 4.2 頂点

- **位置**: 表現が em で書き、ホストが表示サイズを掛ける。
- **UV0.zw＝その頂点の em 座標**（必須・`GlyphGeometry` が書く）。シェーダはその微分から「1 表示 px が何 em か」を得る（§5 S3）。
- **UV0.xy＝表現が決める**（SDF はページの UV）。整数の番地を入れるなら float は 2^24 まで正確に持つ。補間で丸めがずれないよう、
  シェーダは `nointerpolation` か丸めで受ける。
- **追加の頂点チャネル（`Canvas.additionalShaderChannels`）は使わない**。Canvas 全体の設定で、ホストの他の描画まで変える。
- **幾何は quad に限らない**。三角形を何枚書いてもよい。

### 4.3 quad の外延

表現は、描けると宣言した効果（§6.3）の最大の外延（`GlyphEdgeStyle.MaxOuterExtentEm`）＋AA の半分（0.5 表示 px）ぶん、インク境界から広げた範囲を覆う
幾何を書く。足りないと、幾何の縁で縁取りが平らに断たれる。幾何は em で書くので、AA の半分の em は表現が支える最小の表示サイズで決まる
（同梱の SDF は 14px 以上・§9）。

### 4.4 テクスチャ

- 字ごとのテクスチャは `_MainTex`（`CanvasRenderer.SetTexture`）の 1 枚。**推奨はフォントのデータを 1 枚（か数枚のページ）に収め、
  `_MainTex` で渡す形**——マテリアルがフォントに依らず、エッジスタイルごとに 1 マテリアルで全フォントを描ける。uGUI のバッチの鍵は
  マテリアル×テクスチャなので、割れ方も SDF と同じ（フォントの境で割れる）。
- データのテクスチャ（距離以外の値を持つもの）は非圧縮・point 読み（または `Load`）・sRGB 無し・ミップ無しにする。インポートで黙って
  圧縮されると値が壊れるので、ベイカーは作ったテクスチャの形式と寸法を検査し、違えばインポートを失敗させる。
- `ComputeBuffer`／`GraphicsBuffer` はアセットとして直列化できず、フラグメントで読むには SM 4.5 が要るので使わない。

### 4.5 マテリアル

- 表現は `Shader`（インポーターが参照を入れる）と、マテリアルを共有してよい範囲 `MaterialScope`（既定はシェーダ＝全フォントで共有）を持つ。
  フォント全体のテクスチャをマテリアルに載せる表現は `MaterialScope` を自分にし、`ConfigureMaterial` で載せる。
- `GlyphMaterialCache` は（`MaterialScope`・エッジスタイル）ごとに 1 つのマテリアルを作る。作る時点は**フォントを読んだ時点**（ロードの処理の中）
  にするのがよい——描画中に作るとヒッチになる。フォントを外すときは `Release(scope)` で、そのフォントに属するマテリアルを捨てる。
- マテリアルは実行時に作り、アセットにはしない。

## 5. シェーダの契約（表現の種類によらない）

| # | 規約 | 破ると何が壊れるか |
|---|---|---|
| S1 | 入力は POSITION・COLOR・TEXCOORD0（float4）だけ | 追加チャネルは Canvas 全体の設定（§4.2） |
| S2 | 面の色＝頂点色の rgb（rgb にだけ掛ける）。フェード＝頂点色の a を**最後に 1 回** | α を二重に掛けると、面と縁取りの減衰が食い違う |
| S3 | AA は**常に画面の 1px**。UV0.zw の微分から得る（回転・拡縮でも保つ） | 小さい字はギザつき、大きい字はぼける |
| S4 | エッジスタイルの uniform（§6.1）を em で解釈し、面と縁取りを**入れ子の面積**で合成する | エッジスタイルの値の意味が表現で変わる |
| S5 | 描けない効果の uniform は読まない（値はホストの検査で 0 に限られる・§6.3） | 黙って近似した見た目が出る |
| S6 | `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`（rgb は straight α・α は over） | 透明にクリアした RenderTexture へ描くと α が二乗されて下層が漏れる |
| S7 | 実行時に切り替えるキーワードは `multi_compile`（`#pragma multi_compile __ UNITY_UI_CLIP_RECT`） | マテリアルが実行時生成なので、`shader_feature` の変種はビルドで落ちる |
| S8 | 被覆は**非ゼロ巻き数**で決める | 和文フォントは輪郭が重なる。偶奇規則では重なりが抜ける |
| S9 | 面の色は **linear** で使う。uniform `_UIVertexColorAlwaysGammaSpace`（int・Properties に置かない）が真で、Linear のプロジェクトなら、頂点色の rgb を `UIGammaToLinear`（`UnityUI.cginc`）で linear に直す（α は直さない） | Canvas の Vertex Color Always in Gamma Color Space（`Canvas.vertexColorAlwaysGammaSpace`）を有効にすると、Canvas は頂点色を gamma のまま渡し、この uniform を立てる。直さないと面の色が明るくずれる（sRGB の 128 が 188 に出る） |

S9 は組み込みの UI シェーダと同じ関数で直すので、同じ色を指定した `Image` などと面の色が揃う。既定（設定が無効）では Canvas が頂点色を linear に直して
渡し、uniform は立たない。Canvas を通らない描画（§10 の適合検査など）でも立たないので、ホストは頂点色を linear で渡す。

## 6. エッジスタイルの契約

### 6.1 値

`GlyphEdgeStyle`（Core）が持つ。値はすべて **em**。

| 値 | uniform | 範囲 | 意味 |
|---|---|---|---|
| 面の縁のずらし | `_FaceDilate` | −0.03〜0.04 | 面の縁と縁取りの外縁を一緒に動かす（＋で太く）。縁取りの幅は変わらない |
| 面の縁のぼかし | `_FaceSoftness` | 0〜0.06 | 面と縁取りの色の遷移を AA に足す幅。50% の位置は面の縁のまま |
| 縁取りの幅 | `_OutlineWidth` | 0〜0.08 | 面の縁から外への帯の幅 |
| 外縁のぼかし | `_OutlineSoftness` | 0〜0.06 | 外縁の α のランプを AA に足す幅。50% の位置は外縁のまま |
| 縁取りの色 | `_OutlineColor` | rgba | 値は sRGB で持ち、ホストが色空間に合わせて linear に直して `Vector` で渡す |

面の色とフェードは頂点色（§5 S2）で、マテリアルには持たせない。

### 6.2 外延

全キーを上限にした組の外延は `MaxOuterExtentEm`＝0.04＋0.08＋0.06÷2＝**0.15em**。表現はこの外延を描ける範囲を持つ（SDF は距離レンジ
0.1875em）。

### 6.3 効果の能力

- 表現は描ける効果を `GlyphEffects`（`FaceDilate`・`FaceSoftness`・`Outline`・`OutlineSoftness`）で宣言する。**宣言した効果は §6.1 の
  範囲の全部で正しく描く**（一部の値だけ描ける、は宣言しない）。
- エッジスタイルが使う効果（値が 0 でないキー）と表現の能力が合わなければ、ホストはそのエッジスタイルをそのフォントに使う前に止める（`GlyphEdgeStyle.Uses`
  と `GlyphRepresentation.Effects` を照合する。`GlyphMaterialCache.Check` が照合し、`Get` は合わなければ例外で止める）。
- 宣言の正典は表現の種類の記述子（§7.4）。ベイカーがそれを表現のアセットへ書く。

## 7. ベイク仕様（`.t2font`）とインポート

### 7.1 キー

JSON。`//` のコメントを書ける。**未知のキー・重複したキー・型の違う値はインポートの失敗**（§2.1）。

| キー | 必須 | 意味 |
|---|---|---|
| `source` | 必須 | 元フォントのプロジェクトパス（`Assets/` か `Packages/` で始まり、区切りは `/`）。TrueType 輪郭（`glyf`）の単体のフォント |
| `coverage` | 必須 | `corpus`（本文の全字を覆うべきフォント）か `fixed`（固定の文字セットだけの装飾書体）。検査の厳しさを決めるのは利用側 |
| `charsets` | どちらか | 固定の文字セットの名前の配列（§7.2）。元フォントに無い字は件数だけを報告する |
| `characters` | どちらか | 指定の追加文字。元フォントに無ければインポートの失敗 |
| `representation` | 必須 | `{ "kind": "<種類>", … }`。`kind` 以外のキーはその種類の記述子が検査する |

```jsonc
{
    "source": "Assets/Authoring/Fonts/mplus-1c-regular.ttf",
    "coverage": "corpus",
    "charsets": ["DevelopmentDefault"],
    "representation": { "kind": "sdf", "emPx": 48, "format": "R8" }
}
```

### 7.2 文字セット

`Ascii`・`JisX0208`・`WindowsVariants`・`NecRow13`・`IbmExtensions`・`CommonSymbols`、と和の `DevelopmentDefault`（全部）。

### 7.3 インポートの流れ

1. 元フォントを TrueType のリーダーで読む。
2. 文字セットを決める（固定の文字セット ∪ 指定文字）。
3. **タイポグラフィテーブルを作る**（cmap・送り幅・インク境界・.notdef・縦メトリクス）——全表現で共通（§2.3）。
4. 表現のベイカーに、グリフインデックス・輪郭・タイポグラフィテーブル・設定を渡し、表現のサブアセット（とテクスチャ）を作らせる。
5. タイポグラフィテーブルと表現から `T2FontAsset` を組む。

どの段の誤りでもアセットを作らない（ロードが null になり、参照が明確に失敗する）。ベイク仕様か元フォントが変われば再インポートする。

### 7.4 表現の種類

| 物 | 置き場 | 役割 |
|---|---|---|
| 種類の記述子 `GlyphRepresentationKind` | 各表現の Bake アセンブリ（純 C#） | 名前・描ける効果・設定の検査 |
| ベイカー `GlyphRepresentationBaker` | 各表現の Editor アセンブリ | 輪郭と設定から表現のサブアセットを作る |

インポーターはベイカーを Editor の `TypeCache` で集める（引数の無い公開の構築子で作る）。同じ種類のベイカーが 2 つある・ベイカーの無い種類はインポートの失敗。
ベイカーはインポートの文脈に直接触れず、`GlyphBakeContext` を通す: テクスチャなどのサブアセットを渡す（`AddObject`）・自分のパッケージのシェーダを読んで
依存を張る（`PackageAssetPath`・`LoadShader`）・報告の文を足す（`Report`）。インポートが途中で失敗したら、渡したサブアセットはインポーターが捨てる。
返す表現の種類の名前と描ける効果が記述子と違えばインポートの失敗。

### 7.5 バージョン

**同じ入力から出る出力が変わる変更をしたらバージョンを上げる**——上げないと、古いインポートの結果が使われ続ける。

| バージョン | 何が変わったら上げるか | 再インポートされるもの |
|---|---|---|
| `FontFaceBuilder.Version` | タイポグラフィテーブル（§7.3 の 3） | 全フォント（インポーターのバージョン＝これ × 100 ＋ インポーターの改訂） |
| 種類の記述子の `Version` | その表現の出力 | その種類のフォントだけ（種類ごとのカスタム依存 `info.someiyoshino.t2font/representation/<種類>`） |

種類のバージョンをインポーターのバージョンに混ぜないのは、ある表現のバージョンを上げたときに全フォントが再インポートされないようにするため（和文フォントは 1 本で
数十秒かかる）。カスタム依存はドメインの読み込みの時点で登録する（インポートの処理の中では登録できない）。ベイク仕様・元フォント・表現のシェーダが
変わったときも再インポートする。

## 8. 表現を足す手順

1. Bake アセンブリ（純 C#・UnityEngine 非参照）に種類の記述子（名前・`GlyphEffects`・設定の検査）と、タイポグラフィテーブルと輪郭からデータを作る処理を置く。
   外部の NUnit で検査できる。
2. Runtime アセンブリに `GlyphRepresentation` の派生（§3.4）を置き、`WriteGeometry`・`GetTexture`・`ConfigureMaterial` を実装する。
3. シェーダを §5 の契約で書く。
4. Editor アセンブリに `GlyphRepresentationBaker` の派生を置き、テクスチャを作って検査し、シェーダの参照を入れる（`GlyphBakeContext`・§7.4）。
5. 適合検査（§10）を通す。

## 9. 同梱の表現: SDF（`"kind": "sdf"`）

| 設定 | 既定 | 意味 |
|---|---|---|
| `emPx` | 48 | アトラス上の 1em の px（8〜256） |
| `format` | `R8` | ページの形式。`R8`（非圧縮・全ターゲット）か `BC4`（半分・デスクトップのみ） |
| `maxPageSize` | 8192 | 1 ページの最大の幅と高さ（64〜16384）。ページは上限を超えたときだけ分ける |
| `cellBox` | インク境界の和 | セルのインク境界 `[左, 下, 右, 上]`（em）。明示した箱からはみ出す字があれば失敗 |

- 距離レンジは全フォント共通の **0.1875em**（`SdfEncoding.RangeEm`）。符号化は `d = 0.5 + 距離 / (2 × 距離レンジ)`（インク領域の内側が正）で、
  距離レンジの外は 0 か 1 に飽和する。§6.2 の外延（0.15em）＋14px の AA の半分を覆う。
- 生成: スーパーサンプリング（8 倍）の格子へ非ゼロ巻き数で塗る → 厳密距離変換 → texel の中心でサンプル → 8bit へ符号化。全字を同じ大きさのセルに入れ、
  優先クラス（漢字以外・かな・JIS 第 1 水準・第 2 水準・それ以外）の順にページへ並べる。セルの余白は「距離レンジの切り上げ＋1 texel」、
  寸法は 4 の倍数（BC4 のブロックが 2 字にまたがらない）。
- ページは単チャネル・linear・ミップ無し・clamp・bilinear・非 Readable。シェーダ `T2Font/SDF`（`Shaders/T2Font-SDF.shader`）は距離を `.r` から読む。

## 10. 検証ビューと適合検査

- **検証ビュー** `T2FontText`（Viewer）: 文字列 1 本を、字ごとに 1 つの `CanvasRenderer` で描く。エッジスタイル（§6.1）とフォントを Inspector で替えられる。
  1 字 1 レンダラにしてあるのは、テクスチャとマテリアルの束縛・クリップ・α の扱いを、利用側の一般的なテキスト描画と同じ条件で試すため。
- **適合検査** `GlyphConformance`（Conformance）: 表現でベイクしたフォントを渡すと、誤りの文面の一覧を返す（空なら適合）。テストの枠組みに
  依らないので、表現を足すパッケージのテストから呼べる。表現を足したら通す。
  - `CheckGeometry(font)`: 全グリフの幾何——インクの無いグリフは何も書かない・UV0.zw＝頂点の em 座標（§4.2）・頂点の境界ボックスがインク境界を描ける効果の
    外延（`OuterExtentEm`）だけ広げた範囲を覆う（§4.3）。描画装置は要らない。
  - `CheckRendering(font, codepoint)`: 実際に描いた画素で §5 の S2（面の色とフェード）・S3（1px の AA を 24px と 200px で）・S4（縁取りの幅と色）・
    S6（透明の上で α が 1 回だけ掛かる）・S9（uniform が立てば灰色の頂点色を linear に直し、立たなければ直さない）と矩形クリップを確かめる。
    字には「■」のような、インク部が中央の列を上下の水平な縁で横切る塗りつぶしの字を渡す。
    描画装置が要る（`CanRender`）。Canvas もカメラも使わず RenderTexture へ直接描くので、描画パイプラインに依らない。

## 11. 導入と写しの運用

正本はリポジトリ `t-tutiya/T2font`。導入の方法は 2 つで、どちらも同じパッケージが `Packages/info.someiyoshino.t2font` として入る。

| 方法 | 向く場合 | 手順 |
|---|---|---|
| **git URL** | Unity の中だけで使う | Package Manager の「+」→「Add package from git URL…」に下の URL を入れる（Git が要る） |
| **写す** | パッケージのソースを Unity の外（外部の NUnit など）からも読む | フォルダごと（`.meta` と `LICENSE.md` を含めて）利用側の `Packages/` へ写す |

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.t2font#v0.2.0
```

- `#` の後ろは版のタグ（CHANGELOG の版に `v` を付けたもの）。付けないと既定のブランチの先端が入り、どの版を使っているかが決まらない。
  版を上げるときは URL のタグを書き換える。
- git URL で入れたパッケージは Library の中（読み取り専用）に置かれ、Unity の外のビルドからソースをリンクできない。その用途では写す。
- 写しは手で直さない。直すときは正本で直し、版を上げて写し直す（どの版を写したかは `package.json` の版で分かる）。
- パッケージはテストを持たない。表現の適合検査は、利用側のテストから `GlyphConformance` を呼んで回す（§10。`testables` は要らない）。

## 12. ライセンス

MIT（`LICENSE.md`）。権利者は t-tutiya。第三者の告知と手法の出典は `Third Party Notices.md`。
