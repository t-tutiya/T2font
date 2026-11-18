# クイックスタート

[English](01-quickstart.en.md)

パッケージを導入し、元フォントからフォントアセットをベイクして、Canvas に最初の字を出すまで。最後にグリフ表現を SDF から Rinkaku へ替えて
比べる。コードは書かない。各段の詳しい説明は [ベイク仕様の書き方](../architecture/04-bake-spec.html) §2（HTML・手元のブラウザで開く）にある。

## 1. 要るもの

- **Unity 6**（パッケージの `package.json` の `unity` の版以上）。依存の uGUI（`com.unity.ugui`）と Newtonsoft JSON
  （`com.unity.nuget.newtonsoft-json`）は、パッケージを入れると一緒に入る。
- **元フォント**: TrueType 輪郭（`glyf`）の単体のフォント。拡張子が `.ttf` のものはたいていこれに当たる。フォントコレクション（`.ttc`）と
  CFF 輪郭のフォント（`.otf` の多く）は読めない。
- 元フォントのライセンスが、ゲームへの組み込みと、ベイクした派生物（アトラスのテクスチャ）の配布を許すこと。

## 2. パッケージを入れる

Unity の Package Manager →「+」→「Add package from git URL…」に、次の URL を入れる。`<タグ>` は版のタグで、最新のものはリポジトリのルートの
[`README.md`](../../README.md)（Install / 導入）にある。

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.t2font#<タグ>
```

Rinkaku も使うなら、T2Font を入れた後に同じタグで入れる（Rinkaku は T2Font に依存する）。8 の段まで試すなら、ここで入れておく。

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.rinkaku#<タグ>
```

パッケージのフォルダごと（`.meta` と LICENSE.md を含めて）利用側の `Packages/` へ写して使うこともできる。外部の NUnit などの Unity の外の
ビルドからパッケージのソースをリンクしたいときは写す（git URL で入れたパッケージは Library の中に置かれ、読み取り専用・README §11）。

## 3. 元フォントを置く

元フォントを `Assets/` の下に置く。ここでは `Assets/Fonts/MyFont.ttf` とする。Unity は `.ttf` を自分の Font アセットとしてもインポートするが、
T2Font はそれを使わない（インポーターがファイルのバイト列を直接読む）。

## 4. ベイク仕様を書く

`Assets/Fonts/MyFont.t2font` というテキストファイルを作り、UTF-8 で次のとおり書く。Unity の Create メニューには項目が無いので、
エクスプローラーやテキストエディタで作る（Windows のエクスプローラーで拡張子を隠していると `MyFont.t2font.txt` になりやすい）。

```jsonc
// 先頭が // の行はコメント。
{
    "source": "Assets/Fonts/MyFont.ttf",
    "coverage": "fixed",
    "charsets": ["Ascii"],
    "characters": "こんにちは、世界。",
    "representation": { "kind": "sdf" }
}
```

| キー | 意味 |
|---|---|
| `source` | 元フォントのプロジェクトパス（`Assets/` か `Packages/` で始まり、区切りは `/`） |
| `coverage` | `fixed`（決まった字だけを使う書体）か `corpus`（本文の全字を覆うべき書体）。ジェネレーターの振る舞いは変わらず、検査の厳しさを決める目印 |
| `charsets` | 固定の文字セットの名前。和文の本文なら `["DevelopmentDefault"]`（JIS X 0208 の全部ほか。インポートに数十秒かかる） |
| `characters` | 追加の字。元フォントに無い字を書くとインポートが失敗する |
| `representation` | グリフ表現の種類と設定。省いた設定は既定値になる |

キーの正典は README §7.1、文字セットの中身は [ベイク仕様の書き方](../architecture/04-bake-spec.html) §6。

## 5. インポートを確かめる

保存して Unity に戻ると、インポートが始まる。成功すると Console に `[T2Font] Assets/Fonts/MyFont.t2font: …字・グリフ…・sdf・1 ページ…` の
報告が 1 行出る。Project ウィンドウで `MyFont.t2font` の ▶ を開くと、メインアセットの `T2FontAsset` の下に、グリフ表現（`MyFont (sdf)`）と
ページのテクスチャが並ぶ。

失敗すると Console に赤い誤りが出て、アセットは作られない。文面の頭にベイク仕様のパスと行番号が付く。直し方は
[ベイク仕様の書き方](../architecture/04-bake-spec.html) §8。

ベイクした結果（アトラスとタイポグラフィテーブル）は Library に作られる生成物で、リポジトリに入れるのはベイク仕様と元フォント（とそれぞれの
`.meta`）だけ（README §2.4）。

## 6. 字を出す

1. Hierarchy で「GameObject > UI > Canvas」を作る。
2. Canvas を右クリックして「Create Empty」で子の GameObject を作る（`RectTransform` が付く）。幅と高さを広めに取る（例: 800×300）。
3. Inspector の「Add Component」で `T2FontText` を足す。パッケージの検証ビューで、文字列 1 本を描く。
4. `Font` の欄へ `MyFont.t2font` をドラッグする（▶ の中のメインアセットでもよい）。
5. `Text` に「こんにちは、世界。」と書く。`Font Size` は 1em の px。

字は `RectTransform` の左上から並ぶ。行は改行だけで送り、折り返し・カーニング・縦書きは持たない。`Face Color` と `Alpha` は面の色とフェード、
`Clip To Rect` は自分の矩形で字をクリップする。

`T2FontText` は検証のための最小のホストである。製品のテキスト描画は、同じ API で自分のホストを書く。

## 7. エッジスタイルを試す

`T2FontText` の「縁（em）」の欄で、エッジスタイルを替えられる。値は字の大きさに対する比（em）。

| 欄 | 試す値 | 見えるもの |
|---|---|---|
| `Outline Width` と `Outline Color` | 0.05・黒 | 縁取り |
| `Face Dilate` | 0.03 か -0.02 | 面を太らせる・細らせる |
| `Face Softness`・`Outline Softness` | 0.04 | 面の縁・外縁のぼかし |

値の範囲と意味の正典は README §6.1。範囲の外の値や、グリフ表現が描けない効果は、黙って近似せずに誤りを Console へ出して描かない（README §6.3）。

## 8. グリフ表現を Rinkaku に替える

1. 2 の段で Rinkaku を入れていなければ、同じタグで入れる。
2. `MyFont.t2font` を複製して `MyFont-rinkaku.t2font` にし、`representation` を次のとおり書き換える。

   ```jsonc
   "representation": { "kind": "rinkaku" }
   ```

3. インポートされると、▶ の中のグリフ表現が `MyFont-rinkaku (rinkaku)` になる。
4. `T2FontText` をもう 1 つ作って `MyFont-rinkaku` を入れ、両方の `Font Size` を 200 ほどにして並べる。SDF は角が丸くなり、Rinkaku は表示の
   大きさに依らず角が鋭い。エッジスタイルの値は同じ意味のまま使える。

ベイク仕様を替えても、送り幅とインク境界（タイポグラフィテーブル）は変わらない。グリフ表現を替えても、字の並びは動かない（README §2.3）。
Rinkaku は画素あたりの描画の費用が SDF より大きい（Rinkaku の README §7）。

## 9. 次に読む

- 自分のテキスト描画に組み込む: [`02-host-guide.md`](02-host-guide.md)。
- 出荷のために文字セットを絞るなど、目的別の文書は [`README.md`](README.md) の表から選ぶ。
- `.t2font` のキー・文字セット・インポートの失敗の全部は [ベイク仕様の書き方](../architecture/04-bake-spec.html)。
- 契約（API の約束・シェーダの規約）は [T2Font の README](../../Packages/info.someiyoshino.t2font/README.md)。
