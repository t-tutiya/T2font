# 自分のホストの書き方

字を画面へ置く側のコードを**ホスト**と呼ぶ。パッケージの検証ビュー `T2FontText` は検証のための最小のホストで、折り返し・カーニング・
演出を持たない。製品のテキスト描画は、同じ API で自分のホストを書く。本書はその手順を、検証のプロジェクトでコンパイルしている
コード例 [`Assets/Samples/SampleTextHost.cs`](../../Assets/Samples/SampleTextHost.cs) で示す。

- 契約（ホストとグリフ表現の分担・頂点・テクスチャ・マテリアル・シェーダの規約）の正典は README §4・§5。
- 字が画面に出るまでに何が起きるかの図解は [アトラスから画面へ](../architecture/03-atlas-to-screen.html)（HTML）。
- **ホストはグリフ表現の具体型を知らない**。`GlyphRepresentation` の抽象だけで描くので、同じホストが SDF でも Rinkaku でも、利用側が
  足したグリフ表現でも動く（README §2.2）。

## 1. 流れ

| 段 | 何をするか | 使う API |
|---|---|---|
| フォントを読む | エッジスタイルを描けるか確かめ、マテリアルを先に作る | `GlyphMaterialCache.Check`・`Prewarm` |
| 字を並べる | 字 → グリフインデックス、送り幅でペンを進める | `TextCodepoints.Read`・`T2FontAsset.Resolve`・`Metrics`・`Face` |
| 幾何を書く | グリフ表現に em の幾何を書かせ、表示サイズを掛けて Mesh へ詰める | `RepresentationOf`・`WriteGeometry`・`GlyphEdgeStyle.QuadExtentEm`・`GlyphGeometry.CopyTo` |
| レンダラへ渡す | マテリアル・テクスチャ・色・Mesh を `CanvasRenderer` へ渡す | `GlyphMaterialCache.Get`・`GetTexture` |
| 変える | フェードと面の色は幾何を作り直さずに変える | `CanvasRenderer.SetAlpha`・`SetColor` |
| 片付ける | Mesh とマテリアルを捨てる | `GlyphMaterialCache.Release`・`Dispose` |

各 API の引数と返り値は API リファレンス（`docs/generated/api/`）を見る。

## 2. アセンブリの参照

ホストのアセンブリ（asmdef）は `Someiyoshino.T2Font.Core`（`GlyphEdgeStyle`・`GlyphMetrics`・`TextCodepoints` など、UnityEngine を使わない型）と
`Someiyoshino.T2Font`（`T2FontAsset`・`GlyphRepresentation`・`GlyphGeometry`・`GlyphMaterialCache`）を参照する。名前空間はどちらも
`info.someiyoshino.T2Font`。

- asmdef を使わないコード（Assembly-CSharp）からは、この 2 つが自動で参照される。
- 検証ビュー（`Someiyoshino.T2Font.Viewer`）と適合検査（`Someiyoshino.T2Font.Conformance`）は自動で参照されない。使うなら asmdef で明示する。
- ベイク（`Someiyoshino.T2Font.Bake`）とインポーター（`Someiyoshino.T2Font.Editor`）は Editor 専用。実行時のホストからは参照しない。

## 3. フォントを読む

`T2FontAsset` はインポーターが作る生成物で、ほかのアセットを指すのはグリフ表現のシェーダの参照だけ（README §3.1）。`[SerializeField]` の欄・
Addressables・AssetBundle のどれで読んでもよく、フォントアセットを読めばグリフ表現・テクスチャ・シェーダが一緒に来る。

フォントを使い始める時点（ロードの処理の中）で、描くエッジスタイルを検査してマテリアルを作っておく。

<!-- gen:host-load -->
```csharp
// Assets/Samples/SampleTextHost.cs
/// <summary>
/// フォントを使い始める時点で 1 回呼ぶ。エッジスタイルをフォントのグリフ表現で描けるかを確かめ、マテリアルを先に作る
/// （描画の途中で作るとヒッチになる・README §4.5）。描けなければ誤りを出して false を返す。
/// </summary>
private bool LoadFont(T2FontAsset loaded, GlyphEdgeStyle style)
{
    var representation = loaded.Representation;
    var problem = GlyphMaterialCache.Check(representation, style);
    if (problem != null)
    {
        // 描けないエッジスタイルを黙って近似しない（README §6.3）。
        Debug.LogError($"[SampleTextHost] '{loaded.name}': {problem}", this);
        return false;
    }
    _materials ??= new GlyphMaterialCache();
    _materials.Prewarm(representation, new[] { style });
    return true;
}
```
<!-- /gen -->

- `Check` は、エッジスタイルの値が範囲の外か、グリフ表現が描けない効果を使うとき、理由の文面を返す。`Get` は同じ場合に例外で止める。
  黙って近似しないのは、グリフ表現によって見た目が変わらないようにするため（README §6.3）。
- **`GlyphMaterialCache` はホストの間で 1 つを共有する**のがよい。uGUI はマテリアルとテクスチャが同じ字を 1 回の描画にまとめる（バッチ）が、
  ホストごとにキャッシュを持つと、同じ設定でも別のマテリアルになってまとまらない。コード例は短くするためにホストごとに持っている。
- マテリアルは（グリフ表現の `MaterialScope`・エッジスタイル）ごとに 1 つ。SDF の `MaterialScope` はシェーダなので、エッジスタイルが同じなら
  全 SDF フォントが 1 つのマテリアルを共有する。Rinkaku はフォントごと（README §4.5・Rinkaku の README §3.5）。面の色とフェードは頂点色で
  持つので、色違いのテキストもマテリアルを共有する。

## 4. 字を並べる

<!-- gen:host-layout -->
```csharp
// Assets/Samples/SampleTextHost.cs
/// <summary>
/// 字を並べる（描画から独立した計算）。ペンは 1 行目の左端・ベースラインから始め、字ごとに送り幅だけ進める。
/// タイポグラフィテーブルの値は em なので、表示サイズ（1em の px）を掛ける。
/// </summary>
private static void Layout(T2FontAsset font, string text, float fontSize, List<PlacedGlyph> placed)
{
    placed.Clear();
    var face = font.Face;
    var lineStep = (face.ascender - face.descender + face.lineGap) * fontSize;
    var pen = new Vector2(0f, -face.ascender * fontSize);
    for (var i = 0; i < text.Length;)
    {
        // サロゲートペアは 1 字として読む。
        var codepoint = TextCodepoints.Read(text, i, out var length);
        i += length;
        if (codepoint == '\n')
        {
            pen = new Vector2(0f, pen.y - lineStep);
            continue;
        }
        // 収録していない字は代替グリフ（.notdef）になり、字ごとに 1 回 LogError が出る（README §3.1）。
        var glyph = font.Resolve(codepoint, out _);
        placed.Add(new PlacedGlyph(codepoint, glyph, pen));
        pen.x += font.Metrics(glyph).advance * fontSize;
    }
}
```
<!-- /gen -->

- 1 字はコードポイント（`TextCodepoints.Read` はサロゲートペアを 1 字として読む）。
- 値の単位は em で、ペン原点とベースラインを原点とし y は上向き（README §3.3）。行の高さは `Face` の ascender − descender ＋ lineGap
  （descender は負）。
- **収録していない字**: `Resolve` は代替グリフ（.notdef）を返し、コードポイントごとに 1 回 `LogError` を出す。字を黙って消すと出荷物で
  気付けないため（README §2.1）。ホストが別のフォントへ振り分けたい（フォールバック）なら、先に `T2FontAsset.Contains` で確かめる
  ——パッケージはフォールバックを持たない。
- タイポグラフィテーブルが持つのは送り幅・インク境界・縦メトリクスだけで、カーニング・合字・縦書きの情報は無い。折り返し・禁則・
  ルビなどの組版はホストの仕事。
- 並べる計算は描画から分けておくと、Unity を起動しないテストや、表示の前の行数の見積もりに使える。

## 5. 幾何を書く

<!-- gen:host-geometry -->
```csharp
// Assets/Samples/SampleTextHost.cs
/// <summary>字 1 つの幾何を書いて Mesh へ詰める。インクの無い字（空白）には何も書かれないので false を返す。</summary>
private bool WriteMesh(T2FontAsset font, in PlacedGlyph placed, in GlyphEdgeStyle style, Mesh mesh)
{
    var representation = font.RepresentationOf(placed.Glyph);
    _geometry.Clear();
    // 幾何はインク境界をこの幅だけ広げた範囲を覆う。エッジスタイルか表示サイズを変えたら書き直す（README §4.3）。
    representation.WriteGeometry(placed.Glyph, font.Metrics(placed.Glyph), style.QuadExtentEm(fontSize), _geometry);
    if (_geometry.VertexCount == 0)
    {
        return false;
    }
    // 位置は em × 表示サイズ。UV0.zw（頂点の em 座標）は器が書く（README §4.2）。
    _geometry.CopyTo(mesh, fontSize);
    return true;
}
```
<!-- /gen -->

- `WriteGeometry` に渡す幅（`quadExtentEm`）は、描くエッジスタイルと表示サイズから `GlyphEdgeStyle.QuadExtentEm(表示 px)` で求める。
  幾何はインク境界をこの幅だけ広げた範囲を覆う（README §4.3）。足りないと縁取りが幾何の縁で平らに断たれ、広すぎると何も描かれない画素まで
  フラグメントシェーダが走る。
- **エッジスタイルか表示サイズを変えたら幾何を書き直す**。表示を 0.5 倍より小さく縮める演出をするホストは、縮めた後の表示サイズで求める。
  `GlyphRepresentation.WholeQuad` を渡すと、グリフ表現が覆える範囲の全部を書く（エッジスタイルを決める前の幾何・検査）。
- `GlyphGeometry` は 1 つを持ち回して字ごとに `Clear` する（割り当てが無い）。`CopyTo` は渡した Mesh を先に `Clear` してから詰めるので、
  Mesh も字ごとに使い回せる。
- インクの無い字（空白）には何も書かれない。送り幅だけを進める。

## 6. レンダラへ渡す

<!-- gen:host-bind -->
```csharp
// Assets/Samples/SampleTextHost.cs
/// <summary>字のレンダラへ、マテリアル・テクスチャ・色・幾何を渡す。</summary>
private void Bind(CanvasRenderer renderer, T2FontAsset font, in PlacedGlyph placed, in GlyphEdgeStyle style, Mesh mesh)
{
    var representation = font.RepresentationOf(placed.Glyph);
    // materialCount を先に入れないと SetMaterial が効かず、何も描かれない。
    renderer.materialCount = 1;
    renderer.SetMaterial(_materials!.Get(representation, style), 0);
    // テクスチャは字ごとに引く（ページが複数あるフォントでは、字によって違う）。
    renderer.SetTexture(representation.GetTexture(placed.Glyph));
    // 面の色は頂点色の rgb（README §5 S2）。CanvasRenderer の色はバッチのキーに入らない。
    renderer.SetColor(faceColor);
    // SetMesh は中身を Canvas 側へ写す。詰め終えてから呼び、後で Mesh を書き換えたら SetMesh し直す。
    renderer.SetMesh(mesh);
}
```
<!-- /gen -->

- **バッチ**: uGUI のバッチのキーはマテリアルとテクスチャで、`CanvasRenderer` の色は入らない。字ごとにレンダラを分けても、同じマテリアルと
  テクスチャの字は Canvas が 1 回の描画にまとめる。まとまりが割れるのは、フォント（ページ）・エッジスタイル・間に挟まる別の描画の境
  （README §4.4）。
- **テクスチャは字ごとに引く**。ページが複数あるフォントでは、字によってテクスチャが違う。
- **色**: 面の色は頂点色の rgb、フェードは頂点色の α で、シェーダが最後に 1 回だけ α を掛ける（README §5 S2）。色空間の扱い（Linear の
  プロジェクトと、Canvas の Vertex Color Always in Gamma Color Space）はシェーダが受け持つので、ホストは `Color` をそのまま渡す（README §5 S9）。
- **クリップ**: `RectMask2D` と `Mask` は `Graphic` に働く部品で、ホストが直接持つ `CanvasRenderer` には効かない。矩形でクリップするなら、
  ホストが `CanvasRenderer.EnableRectClipping` に最上位の Canvas の空間の矩形を渡す（見本は `T2FontText` の `CanvasRect`）。シェーダは矩形の
  クリップ（`UNITY_UI_CLIP_RECT`）を持ち、ステンシルのマスクは持たない。

## 7. フェードと色を変える

<!-- gen:host-fade -->
```csharp
// Assets/Samples/SampleTextHost.cs
/// <summary>フェード（0〜1）。α は幾何と独立に変えられるので、Mesh を作り直さない（README §5 S2）。</summary>
public void SetFade(float alpha)
{
    foreach (var view in _views)
    {
        view.Renderer.SetAlpha(alpha);
    }
}
```
<!-- /gen -->

`SetAlpha` と `SetColor` はメッシュと独立に変えられ、幾何を作り直さない。字ごとのフェードイン（タイプライター風の表示）は、字の
`CanvasRenderer` の α を毎フレーム変えるだけで済む。エッジスタイル（縁取りの色や幅）を変えるときは、マテリアルが替わり、幾何の範囲も
変わりうるので、`Get` で引き直して幾何を書き直す。

## 8. uGUI のリビルドの仕組みに乗せる

コード例は短くするために、値が変わったら次の `Update` で作り直している。製品のホストでは、uGUI のリビルドの仕組みに乗せると、
1 フレームに何度値を変えても作り直しは 1 回で済む。

1. 幾何が要るときに `CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this)` で登録する（同じフレームの重複は 1 回にまとまる）。
   `ICanvasElement` を実装し、`Graphic` は継承しなくてよい。
2. レンダリングの直前の Canvas の更新が `ICanvasElement.Rebuild(CanvasUpdate.PreRender)` を呼ぶ。ここで幾何を書き、`SetMesh`・`SetMaterial`・
   `SetTexture` する。
3. `SetMesh` は中身を Canvas のバッチへ写す。後で Mesh を書き換えても描画には出ないので、幾何を変えたら登録し直して `SetMesh` する。
4. α は `SetAlpha` でメッシュと独立に変えられるので、フェードの演出では作り直しが走らない。

`ICanvasElement` と `CanvasUpdateRegistry` は uGUI（アセンブリ `UnityEngine.UI`）の型なので、asmdef の参照に足す。

## 9. 片付ける

<!-- gen:host-release -->
```csharp
// Assets/Samples/SampleTextHost.cs
/// <summary>フォントをやめるときに、そのフォントのマテリアルを捨てる。</summary>
private void ReleaseFont()
{
    if (_loadedFont != null && _materials != null)
    {
        var scope = _loadedFont.Representation.MaterialScope;
        if (scope != null)
        {
            // スコープがシェーダのグリフ表現（SDF）では、同じシェーダのほかのフォントのマテリアルも捨てる（次の Get で作り直される）。
            _materials.Release(scope);
        }
    }
    _loadedFont = null;
}

private void OnDestroy()
{
    // Mesh とマテリアルはネイティブのオブジェクトで、参照を手放しても GC では回収されない。
    foreach (var view in _views)
    {
        Destroy(view.Mesh);
    }
    _views.Clear();
    _materials?.Dispose();
    _materials = null;
}
```
<!-- /gen -->

- Mesh とマテリアルは Unity のネイティブのオブジェクトで、参照を手放しても GC では回収されない。持ち主が明示的に破棄する。
- `Release(scope)` は、そのスコープのマテリアルを捨てる。SDF のようにスコープがシェーダのグリフ表現では、同じシェーダのほかのフォントの
  マテリアルも捨てる（次の `Get` で作り直されるので誤りにはならないが、作り直しの費用がかかる）。フォントを外すたびではなく、シーンの
  切り替えなどの区切りでまとめて捨ててもよい。

## 10. 確かめる

- **エッジスタイルとフォントの組を前もって照合する**: 実行時は `Check` と `Get` が止めるが、スタイルをデータ（表や設定ファイル）で持つ
  プロジェクトでは、出荷の前に全部の組を照合できる。ベイク仕様を `FontBakeSpec.Parse` で読み（グリフ表現の種類の一覧は Editor では
  `T2FontImporter.RepresentationKinds()`）、`spec.Representation.Kind.Effects` とスタイルの `GlyphEdgeStyle.Uses` を比べる。値の範囲は
  `GlyphEdgeStyle.FindOutOfRange` で見る。どれも Unity を起動しない外部の NUnit から呼べる（`Core` と `Bake` は UnityEngine を使わない）。
- **並べる計算のテスト**: フォントアセットは EditMode のテストで `AssetDatabase.LoadAssetAtPath<T2FontAsset>("…/名前.t2font")` で読める
  （インポートが失敗していれば null）。
- グリフ表現の適合検査（`GlyphConformance`）は、グリフ表現を作る側の検査で、ホストを書くだけなら要らない。
