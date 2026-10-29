# 頂点色を gamma のまま渡す Canvas への対応（設計スタディ）

作成日: 2026-10-29

roadmap の着手候補 #3 の方針を決めるための比較。現状の動作は [`../architecture/03-atlas-to-screen.html`](../architecture/03-atlas-to-screen.html) §9.2、
契約はパッケージの README §5 S2。決めた方針と理由は decisions に記録する。

---

## 1. 問題

uGUI の Canvas は、Linear のプロジェクトで頂点色をシェーダへ渡すときに 2 つの形を持つ。

| Canvas の設定 | Canvas がすること | シェーダがすること |
|---|---|---|
| Vertex Color Always in Gamma Color Space が無効（既定） | メッシュを作るときに頂点色を gamma から linear へ直す | 受けた値をそのまま使う |
| 有効（`Canvas.vertexColorAlwaysGammaSpace`） | 頂点色を gamma のまま渡す | 自分で linear へ直す |

`T2Font/SDF` は `vert` で頂点色をそのまま渡すので、有効にした Canvas では gamma の値を linear として扱い、**面の色が黙って明るくずれる**
（README §2.1 に反する）。

- **ずれ方**: sRGB で 128 の灰色は 188 に、64 は 137 に出る。0 と 1 は変換で動かないので、白・黒・純色（適合検査の S2 が使う (1, 0, 0)）ではずれない
  ——今の適合検査はこの不具合を捕まえない。
- **ずれないもの**: フェード（頂点色の α。どちらの設定でも変換しない）・縁取りの色（マテリアルの `Vector` で、`GlyphMaterialCache` が linear に直して
  入れる）。Gamma のプロジェクトでは設定が効かないので、何も起きない。
- **有効にする理由**: 既定では Canvas が linear の値を 8bit の頂点色に詰めるので、暗い色の階調が潰れる（グラデーションに縞が出る）。有効にすると
  変換が精度の高いシェーダの中で行われる（Unity の文書の説明）。Canvas 全体の設定なので、同じ Canvas の他の UI のためにも有効にされうる。

## 2. Unity の仕組み（調べたこと）

- **文書**（ScriptReference `Canvas.vertexColorAlwaysGammaSpace`）: 「組み込みの UI シェーダは gamma から linear への変換を持つ。独自の UI シェーダでは、
  利用者が変換を用意する必要がある」。どう用意するか（uniform の名前・関数）は書いていない。
- **TMP のシェーダ**（Unity 製の `com.unity.textmeshpro` 3.2.0-pre.10 の TMP Essential Resources。`TMP_SDF.shader` を含む 11 本）: すべて同じ形で変換する。

  ```hlsl
  #include "UnityUI.cginc"
  int _UIVertexColorAlwaysGammaSpace;   // Properties には置かない
  ...
  // 頂点シェーダ
  if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
  {
      input.color.rgb = UIGammaToLinear(input.color.rgb);
  }
  ```

  `UIGammaToLinear` は TMP のファイルには無く、組み込みの `UnityUI.cginc` にある。TMP の C# はこの uniform に値を入れていないので、
  **Canvas がバッチごとに入れている**と読める。
- **TMP の修正（UUM-36113「TextMeshPro が VertexColorAlwaysGammaSpace のときに linear へ直さない」）**: 再現手順は「Canvas の設定を有効にしても
  TMP の字の色が変わらない」ことを期待し、2022.2・2023.1・2023.2 で再現した。TMP 3.2.0-pre.6 で直り、修正の版にはエンジンの 2022.3.0f1・
  2023.2.0b10・2023.3.0a8 も載っている。TMP の側の修正はシェーダの変更で、TMP の C# は uniform に触れない。エンジンの側も変えたかは分からないが、
  どちらにしても 6000.3 はそれより後の版。なお、この設定は 2020.3・2021.3 には無かった（同じ課題の記録）。
- **ほかの独自のシェーダ**: オープンソースの UIEffect v5（`mob-sakai/UIEffect` 5.11.7）の `UIEffect.shader` も同じ uniform で直す
  （関数は `UnityCG.cginc` の `GammaToLinearSpace`）。頂点シェーダの形は組み込みの UI シェーダの写しなので、TMP とは独立の根拠としては弱い。
- **反対の主張**: Unity のフォーラムのスレッド「Published "Default UI Shader" differs from actual default UI shader」に、「この設定の実装は文書に無く、
  独自の UI シェーダでは再現できない。親の Canvas が設定を有効にしているかを知る方法が無いので、どちらかを前提にするしかない」という書き込みが
  ある。同じスレッドには uniform で直すコードも載っている。本文はサーバがコマンドラインからの取得を拒むので、検索結果の抜粋でしか読めていない
  ——どの書き込みが先か、誰が書いたかは分からない。TMP の修正と食い違うので、下の①は描いて確かめる（§3.1 のテスト・§7）。
- **キーワードではなく uniform**なので、`multi_compile` も変種も増えない（README §5 S7 は関わらない）。
- **文書で確かめられないこと**: ①Canvas が組み込みでないシェーダ（パッケージのシェーダ）にも uniform を渡すこと——TMP のシェーダがパッケージの
  アセットであること、TMP の修正がその前提で確かめられていることが根拠。反対の主張がある。②入れ子の Canvas で、どの Canvas の設定が効くか。
  どちらも描いて確かめる（§7）。

## 3. 案 A: シェーダで直す

TMP・組み込みの UI シェーダと同じ仕組みを `T2Font/SDF` に足し、**シェーダの契約に足して全表現の義務にする**。

### 3.1 変える物

| 物 | 変更 |
|---|---|
| `Shaders/T2Font-SDF.shader` | 下のコード。ほかは変えない |
| README §5 | S9 を足す（下の表） |
| 適合検査 `GlyphConformance.CheckRendering` | S9 の検査を足す（下のコード）。`Render` に「gamma のまま渡すか」の引数を足す |
| Runtime・検証ビューの C# | 変えない（Canvas が uniform を入れるので、ホストは何もしない） |
| テスト | EditMode の `GlyphConformanceTests` が `CheckRendering` を空と比べているので、新しい検査もそこで回る（描画装置が無い環境では Ignore になるので、確実に回るのはローカル） |
| テスト（Canvas を通す） | 適合検査は Canvas を通らないので、シェーダが uniform を読むことしか確かめない。Canvas が uniform を入れること（§2 の①）は、EditMode に Canvas を通して描くテストを足して確かめる: Screen Space - Camera の Canvas に、検証ビューと同じ灰色の `Image` を並べ、カメラで RenderTexture へ描き、設定の有効・無効の両方で検証ビューの面の色が `Image` と同じことを確かめる（描画装置が要るのでローカル。URP でカメラを 1 回だけ描く口は実装時に選ぶ） |

```hlsl
// Canvas が Vertex Color Always in Gamma Color Space のバッチに入れる。TMP と組み込みの UI シェーダに合わせ、Properties に置かない（README §5 S9）。
int _UIVertexColorAlwaysGammaSpace;
...
output.color = input.color;
// 面の色は linear で使う。gamma のまま来たら、組み込みの UI シェーダと同じ関数で直す（README §5 S9）。
if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
{
    output.color.rgb = UIGammaToLinear(output.color.rgb);
}
```

| # | 規約 | 破ると何が壊れるか |
|---|---|---|
| S9 | 面の色は linear で使う。`_UIVertexColorAlwaysGammaSpace`（Canvas が渡す int。Properties に置かない）が真で Linear のプロジェクトなら、頂点色の rgb を `UIGammaToLinear`（`UnityUI.cginc`）で直す。Canvas を通らない描画では uniform が 0 のままなので直さない | Canvas の Vertex Color Always in Gamma Color Space を有効にすると、面の色が明るくずれる |

```csharp
// S9: 灰色の頂点色は、_UIVertexColorAlwaysGammaSpace が 0 なら値のまま、1 なら（Linear のとき）linear へ直して面に塗られる。
// 0 と 1 は変換で動かないので、S2 の (1, 0, 0) では確かめられない。
const float gray = 0.5f;
var linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.GammaToLinearSpace(gray) : gray;
var asIs = Straight(Render(representation, glyph, metrics, plain, 128f, new Color(gray, gray, gray, 1f), null, false).Peak());
var converted = Straight(Render(representation, glyph, metrics, plain, 128f, new Color(gray, gray, gray, 1f), null, true).Peak());
if (!Near(asIs, new Color(gray, gray, gray), 0.02f)) { /* S9: uniform が 0 なのに面の色を変えた */ }
if (!Near(converted, new Color(linear, linear, linear), 0.02f)) { /* S9: gamma のまま来た頂点色を linear へ直していない */ }

// Render の中: properties.SetInteger(VertexColorAlwaysGammaSpaceId, vertexColorInGamma ? 1 : 0);
```

許容差 0.02 は、`UIGammaToLinear` が近似の関数であることと、読み戻す RenderTexture が 8bit であることを吸収する（期待値は 0.5 と約 0.214 で、
取り違えれば 0.29 離れる）。

### 3.2 利点・欠点

- **利点**: 有効にした Canvas でも面の色が既定と同じになり、暗い色はむしろ精度が上がる。同じ Canvas の `Image` などと同じ関数で直すので、
  同じ色を指定した UI と色が揃う。**どのホストでも**（検証ビューでも利用側のテキスト描画でも）ホストのコードなしに正しく描ける。実行時に設定を
  切り替えても、Canvas がバッチごとに uniform を入れるので追従する。変種は増えず、コストは頂点ごとの分岐 1 つ。
- **欠点**: 表現を足す側（MTSDF・ビットマップなど）のシェーダに義務が 1 つ増える。見落としは適合検査が捕まえる。
- **危険**: §2 の①が外れる（Canvas が独自のシェーダに uniform を渡さない）と、有効にした Canvas では今と同じく明るくずれたままになる。
  悪くはならないが直らない。フォーラムに反対の主張があるので、§3.1 の Canvas を通すテストとローカルの目視（§7）で確かめてからマージする。

## 4. 案 B: ホストで止める

シェーダは変えず、**Linear のプロジェクトで設定を有効にした Canvas の下では描かない**ことをホストの契約にする。

### 4.1 変える物

| 物 | 変更 |
|---|---|
| Runtime | 判定の口を足す（下のコード）。`GlyphMaterialCache` は Canvas を知らないので、ホストが Canvas を渡して呼ぶ |
| 検証ビュー `T2FontText` | `Rebuild` で判定し、描けなければ `Debug.LogError` を出して描かない（エッジスタイルが描けないときと同じ形）。設定の切り替えには通知が無いので、`LateUpdate` で親の Canvas の値を見張り、変わったら作り直す |
| README §4 | 「ホストは、判定の口が誤りを返す Canvas の下では描かない」を足す |
| シェーダ・適合検査 | 変えない（表現の側の問題ではないので、適合検査では確かめられない） |
| テスト | EditMode の `T2FontTextTests` に「設定を有効にした Canvas の下では描かずに LogError を出す」を足す（Linear のプロジェクトでだけ意味を持つ） |

```csharp
// Runtime（置き場は GlyphMaterialCache の静的メソッドか、新しい静的クラス）
/// <summary>
/// Linear のプロジェクトで頂点色を gamma のまま渡す Canvas の下では、面の色を正しく描けない（README §4.x）。描けなければ誤りの文面、描けるなら null。
/// </summary>
public static string? CheckCanvas(Canvas canvas)
{
    var root = canvas.rootCanvas;
    return QualitySettings.activeColorSpace == ColorSpace.Linear && root.vertexColorAlwaysGammaSpace
        ? $"Canvas '{root.name}' の Vertex Color Always in Gamma Color Space が有効です。T2Font は頂点色を linear で受けるので、面の色がずれます。"
        : null;
}

// T2FontText.Rebuild（エッジスタイルの検査の後）
var canvas = GetComponentInParent<Canvas>();
var canvasProblem = canvas == null ? null : GlyphMaterialCache.CheckCanvas(canvas);
if (canvasProblem != null)
{
    Debug.LogError($"[T2FontText] {canvasProblem}", this);
    return;
}
```

### 4.2 利点・欠点

- **利点**: シェーダの契約が変わらず、表現を足す側に義務が増えない。判定は文書にある C# の値だけで行い、§2 の①に頼らない。
- **欠点**: 設定を有効にした Canvas では T2Font を使えない。Canvas 全体の設定なので、T2Font を置くために同じ Canvas の他の UI の精度まで諦めることになる。
- **欠点**: 判定は**ホストごとに**呼ぶ。`GlyphMaterialCache` もシェーダも Canvas を知らないので、パッケージが強制できない——利用側のホストが呼び忘れると、
  今と同じく黙って明るくずれる。
- **欠点**: どの Canvas の設定が効くか（§2 の②）が文書に無い。上のコードはルートの Canvas を見るが、入れ子の Canvas の設定が効くなら判定が外れる。
  実行時の切り替えも、ホストが見張らないと見落とす。

## 5. 比べる

| 観点 | A: シェーダで直す | B: ホストで止める |
|---|---|---|
| 有効にした Canvas での面の色 | 既定と同じ（暗い色は精度が上がる） | 描かない（LogError） |
| 利用側のホスト | 何もしなくてよい | 判定を自分で呼ぶ。呼び忘れると黙ってずれる |
| 表現を足す側 | シェーダに S9 の数行。見落としは適合検査が捕まえる | 何もしなくてよい |
| 変える物 | シェーダ・README §5・適合検査・Canvas を通す EditMode テスト | Runtime の API・検証ビュー・README §4・EditMode テスト |
| 実行時の切り替え | そのまま追従する | ホストが見張る |
| 文書で確かめられない前提 | Canvas が独自のシェーダにも uniform を渡す（TMP の修正が根拠。フォーラムに反対の主張がある。Canvas を通すテストで確かめる） | 入れ子の Canvas でどの設定が効くか |
| 前提が外れたとき | 今と同じく明るくずれる（悪くならない） | 判定が外れて、今と同じく明るくずれる |

## 6. 推奨: A

- 目的（どのホストでも面の色を正しく描く）を、ホストに頼らずパッケージの中で満たせるのは A だけ。B は「止める」約束をホストに預けるので、
  §2.1 の「黙って読み替えない」を利用側のコードの規律で守ることになる。
- Unity 自身（組み込みの UI シェーダ・TMP）がこの形で、文書も「独自のシェーダは変換を用意する」と求めている。B はこの設定を使うプロジェクトで
  T2Font を使えなくする。
- 表現を足す側の義務が 1 つ増えるが、数行で、適合検査が見落としを捕まえる。
- 前提①には反対の主張があるが、外れても今より悪くならず、Canvas を通すテストで外れたことが分かる。外れたと分かったら、その時点で B へ切り替える
  （シェーダの変更は害が無いので残してよい）。

## 7. どちらの案でも行うこと

- **版**: 0.2.0（契約が変わる。A はシェーダの契約に S9 が増え、ほかの表現のシェーダにも義務がかかる。B はホストの契約が増える）。`package.json`・
  CHANGELOG（未リリースの用語の変更と合わせて 0.2.0 にする）・README §11 の URL のタグ。タグ `v0.2.0` はマージの後に打つ。
- **文書**: 03 章の §9.2（面の色）。A はさらに §8.2（`vert` の説明）と図 7 の文面（`Tools/docs-figures/03-atlas-to-screen/schematics.py` を直して作り直す）。
  roadmap の #3 を完了記録へ移す。decisions に方針と採らなかった案を記録する。
- **確かめ方**: 外部 NUnit（Job A）・Unity のコンパイル（PR の Job B）に加えて、ローカルで次を行う。
  1. EditMode テストを回す（A は `GlyphConformanceTests` と Canvas を通すテスト、B は `T2FontTextTests`）。
  2. 「T2font/比較の場面を作る」で場面を作り、検証ビューの面の色を灰色（たとえば 128）にして、ルートの Canvas の Vertex Color Always in Gamma Color Space を
     切り替える。A は面の色が変わらない（同じ色の `Image` を横に置いて比べる）こと、B は字が消えて LogError が出ることを確かめる。§2 の②
     （入れ子の Canvas）は、入れ子の Canvas を足して同じことを確かめる。
