# 出荷用に文字セットを絞る

和文の本文フォントは、開発の間は JIS X 0208 の全部などの広い文字セット（`DevelopmentDefault`）でベイクしておくと、本文を書き足すたびに
字を気にせずに済む。ただし、出荷物に入るアトラスはその分だけ大きい。**出荷のときは、本文で使う字だけでベイクし直したフォントアセットを
入れる**。本書はその手順を、検証のプロジェクトでコンパイルしているコード例
[`Assets/Samples/Editor/ShippingFontSample.cs`](../../Assets/Samples/Editor/ShippingFontSample.cs) で示す。

パッケージが持つのは部品（ベイク仕様の読み書き・字の集合・元フォントのリーダー・インポーター）で、**どの文字列を本文とするか・どこで
フォントアセットを差し替えるかはプロジェクトが決める**。ベイク仕様のキーの正典は README §7。

## 1. 流れ

1. 本文（出荷するテキスト・シナリオ・UI の文言のデータ）から字を集め、常に入れる字とベイク仕様の指定文字を足す。
2. 元フォントに無い字を分ける（報告する）。
3. 開発用のベイク仕様の文字セットを、集めた字の明示（`characters`）に置き換えた**出荷用のベイク仕様**を作る（`FontBakeSpec.WithCharacters`）。
4. 出荷用のベイク仕様を `Assets/` の下の一時の置き場へ書き、インポートする。要る字を全部持つことを確かめる。
5. ビルド（AssetBundle・Addressables・プレイヤー）に、開発用ではなく出荷用のフォントアセットを入れる。

1〜3 は純 C# の部品（アセンブリ `Someiyoshino.T2Font.Bake`）だけで書けるので、Unity を起動しない外部の NUnit でも回せる。4 は Editor
（`AssetDatabase` とインポーター）が要る。

## 2. 字を集める

<!-- gen:ship-collect -->
```csharp
// Assets/Samples/Editor/ShippingFontSample.cs
/// <summary>出荷する字を集める: 本文の全部の字 ∪ 常に入れる字 ∪ ベイク仕様の指定文字。</summary>
public static SortedSet<int> CollectCodepoints(FontBakeSpec spec, IEnumerable<string> texts)
{
    var codepoints = new SortedSet<int>();
    foreach (var text in texts)
    {
        // 制御文字と BOM は描く字ではないので足されない。本文の構文は解釈せず、全部の字を集める
        // （集めすぎても字が増えるだけだが、取りこぼすと出荷物で字が欠ける）。
        TextCodepoints.AddDrawable(codepoints, text);
    }
    // 実行時に組み立てる字（数字・記号）は、本文に現れないことがあるので常に入れる。
    codepoints.UnionWith(FontCharsets.Build(FontCharsetKind.Ascii));
    // ベイク仕様の指定文字（characters）は、開発用と同じく出荷用にも入れる。
    codepoints.UnionWith(spec.NamedCodepoints());
    return codepoints;
}
```
<!-- /gen -->

- **本文の構文を解釈せずに、全部の字を集める**。タグやコメントの字まで入っても字が少し増えるだけだが、取りこぼした字は出荷物で欠ける。
- **実行時に組み立てる字**（数字・記号・時刻の書式など）は、本文に現れないことがあるので常に入れる。足りない分は、ベイク仕様の `characters` か
  `FontCharsets.Build` の文字セットで足す。`JisX0208Table` の部分（たとえば非漢字 `NonKanji`）は `FontCharsets.AddString` で足せる。
- **C# のコードに書いた文字列の字は、本文のデータに入らない**。画面へ出す文字列はデータから読むか、その字を `characters` に足す。
- プレイヤーが任意の字を入力できる（名前の入力など）フォントは絞れない。そのフォントは開発用の文字セットのまま入れる。

## 3. 出荷用のベイク仕様を作る

<!-- gen:ship-plan -->
```csharp
// Assets/Samples/Editor/ShippingFontSample.cs
/// <summary>
/// 開発用のベイク仕様（<paramref name="specPath"/>）から、本文の字だけを持つ出荷用のベイク仕様を作る。元フォントに無い字は
/// <paramref name="missing"/> に返し、出荷用には入れない（入れるとインポートが失敗する）。
/// </summary>
public static FontBakeSpec PlanShippingSpec(string specPath, IEnumerable<string> texts, out List<int> missing)
{
    // インポーターと同じグリフ表現の種類の一覧で読む（インポートできるベイク仕様を、ここだけが拒むことの無いように）。
    GlyphRepresentationKinds kinds = T2FontImporter.RepresentationKinds();
    FontBakeSpec spec = FontBakeSpec.Parse(File.ReadAllText(specPath), specPath, kinds);
    var font = new TrueTypeFont(File.ReadAllBytes(spec.Source));

    SortedSet<int> wanted = CollectCodepoints(spec, texts);
    missing = wanted.Where(cp => !font.Contains(cp)).ToList();
    // 文字セットを字の明示（characters）に置き換えた、自己完結のベイク仕様。元フォントとグリフ表現の設定は写される。
    return spec.WithCharacters(wanted.Where(font.Contains));
}
```
<!-- /gen -->

- ベイク仕様は、インポーターと同じグリフ表現の種類の一覧（`T2FontImporter.RepresentationKinds()`）で読む。外部の NUnit では、使う種類の記述子を
  並べた `GlyphRepresentationKinds` を渡す（`new GlyphRepresentationKinds(new GlyphRepresentationKind[] { SdfRepresentationKind.Instance, RinkakuRepresentationKind.Instance })`）。
- `WithCharacters` が返すベイク仕様は、元フォントとグリフ表現の設定を写した自己完結のもので、固定の文字セットを持たない。字は `characters` に明示する
  ので、元フォントに無い字が混ざるとインポートが失敗する（黙って欠けない）。コード例は元フォントに無い字を先に分けて `missing` で返す。
  `missing` を空でないときにビルドを止めるか、警告に留めるかはプロジェクトが決める（本番の本文の字なら止めるのがよい）。
- `coverage` が `fixed` の装飾書体（タイトル用など）は、開発用の文字セットが既に小さいので、そのまま入れてよい。

## 4. インポートして確かめる

<!-- gen:ship-bake -->
```csharp
// Assets/Samples/Editor/ShippingFontSample.cs
/// <summary>出荷用のベイク仕様を <paramref name="outputPath"/>（<c>Assets/</c> の下）へ書いてインポートし、要る字を全部持つことを確かめる。</summary>
public static T2FontAsset BakeShippingFont(FontBakeSpec shipping, string sourceSpecPath, string outputPath)
{
    File.WriteAllText(outputPath, shipping.ToJson(new[] { $"出荷用。{sourceSpecPath} から本文の字で作った生成物で、手で直さない。" }));
    AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);

    // インポートが失敗していれば null（誤りは Console に出ている）。
    T2FontAsset? imported = AssetDatabase.LoadAssetAtPath<T2FontAsset>(outputPath);
    if (imported == null)
    {
        throw new InvalidOperationException($"{outputPath} のインポートに失敗しました。Console の誤りを見てください。");
    }
    T2FontAsset asset = imported;
    var lacking = shipping.NamedCodepoints().Where(cp => !asset.Contains(cp)).ToList();
    if (lacking.Count > 0)
    {
        throw new InvalidOperationException(
            $"{outputPath} に無い字があります: {string.Join(" ", lacking.Select(TextCodepoints.Display))}");
    }
    return asset;
}
```
<!-- /gen -->

- 置き場は `Assets/` の下の一時のフォルダ（たとえば `Assets/BuildTemp/Fonts/`）にし、`.gitignore` に足す。出荷用のアセットは生成物で、
  リポジトリに入れない（README §2.4）。
- ファイル名（拡張子を除く）がアセットの名前になる。開発用と同じファイル名にしておくと、名前でフォントを引くローダー（AssetBundle の中の名前・
  Addressables のアドレスなど）を変えずに差し替えられる。
- `T2FontAsset.SourceHash` は生成元（ジェネレーターのバージョン・ベイク仕様・元フォント）のハッシュで、どのフォントを出荷したかの記録に使える。

## 5. ビルドに入れる

開発用と出荷用のどちらをビルドに入れるかは、フォントアセットの参照のしかたで決まる。

| 参照のしかた | 差し替え方 |
|---|---|
| AssetBundle・Addressables（名前やアドレスで引く） | ビルドの前に、開発用のベイク仕様の代わりに出荷用のベイク仕様をバンドルへ割り当てる。名前が同じならローダーは変わらない |
| シーン・プレハブの `[SerializeField]` の欄 | 欄が指すのはアセットの GUID なので、出荷用のアセットを指す欄を別に持つか、ビルドの前処理で欄を差し替える |

どちらの場合も、ビルドの後に**開発用のフォントアセットと元フォント（`.ttf`）が出荷物の依存に入っていない**ことを確かめる
（AssetBundle なら `AssetDatabase.GetDependencies` で辿れる）。

## 6. 版をまたぐ注意

- **パッチで本文の字が減ると、古いセーブデータが持つ文字列の字が出荷物から消える**（セーブの一覧に出す抜粋・バックログなど）。出荷した字の一覧を
  版ごとに残し、出荷用の文字セットには全部の版の和を入れる。
- 出荷物で最後に守るのは `T2FontAsset.Resolve` で、収録していない字は代替グリフで描かれ、コードポイントごとに 1 回 `LogError` が出る
  （README §3.1）。開発中にこの文面が出たら、字の集め方を見直す。
