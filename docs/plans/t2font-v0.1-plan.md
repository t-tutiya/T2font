# T2Font v0.1.0 の段取り

作成日: 2026-10-28
**本書が現役なのは、v0.1.0（着手候補 2）の段 S1〜S7 の中身と検証を所有するため**。契約（何を作るか）の正典はパッケージの README
（`Packages/info.someiyoshino.t2font/README.md`）で、本書は繰り返さない。置き場・配り方・ライセンスの理由は D-T2FONT。

**期限**: v0.1.0 のタグを 2026-11-01 までに打つ（ハッカソンの開発期間は 2026-11-02〜11-08。大会の成果はこの版の上に作るので、大会の前の
土台であることを日付で示す）。

---

## 1. 段

各段は前の段を待つ。1 段ごとにコミットし、Job A（外部 NUnit・整形・editorconfig-checker）をコンテナで回してから次へ進む。Unity 側の
コンパイルは PR の Job B が見る（本リポジトリは公開なので Actions の分を気にしなくてよい。Job B には `UNITY_LICENSE` ほかの secrets が要る）。

| 段 | 中身 | 検証 |
|---|---|---|
| **S1 骨格** | パッケージのフォルダ・`package.json`・アセンブリ（Core／Bake／Runtime／Editor／Viewer と `csc.rsp`）・LICENSE と第三者の告知・README（契約）・CHANGELOG。外部 NUnit のリンク・パッケージの検査（`Tests/PackageIntegrityTests.cs`）・アセンブリ一覧の生成器をパッケージまで広げる | Job A |
| **S2 Core** | `FontFaceMetrics`・`GlyphMetrics`・字形番号の検索・`GlyphEffects`・`GlyphEdgeStyle`（値域と外延・使う効果）・字の走査 | Job A（Core の単体） |
| **S3 Bake** | TrueType の読取・輪郭・ラスタ・距離変換・配置・字集合（JIS X 0208 ほか）・生成仕様（共通のキー＋`representation`）・**字の表の生成**（表現と独立）・**SDF のアトラスの焼き**・表現の種類の記述子 | Job A（M+ 1c の表の値・再構成した墨と輪郭の面積・生成仕様の誤り・出力のハッシュ） |
| **S4 Runtime** | `T2FontAsset`・`GlyphRepresentation`・`GlyphGeometry`・`GlyphMaterialCache`・`SdfRepresentation`・SDF のシェーダ | PR の Job B |
| **S5 Editor** | `.t2font` の取込器（焼き器を `TypeCache` で集めて種類で振り分ける）・SDF の焼き器（ページのテクスチャ化と検査・シェーダの参照） | PR の Job B・ローカルで取り込み |
| **S6 検証** | 検証ビュー `T2FontText`・適合検査（EditMode）・取込の EditMode テスト・検証用の元フォント（M+ 1c）と生成仕様・比較の場面を作るメニュー | PR の Job B・**ローカルで Play Mode と目視** |
| **S7 公開** | CHANGELOG の 0.1.0・ルートの README・タグ `v0.1.0` | — |

## 2. 範囲の外（v0.1.0 では作らない）

- 出荷用の字集合の抽出（本文から字を決めて焼き直す仕組み）——利用側のビルドの事情に属する。生成仕様の `characters` に字を明示すれば同じ取込器で焼ける。
- SDF 以外の表現（MTSDF・画像の字形・ビットマップ）と、1 フォントに複数の表現を持たせる拡張（API の口 `RepresentationOf` だけを用意する）。
- CFF 輪郭（OTF）の読取・カーニング・縦書き。

## 3. 確かめること（危険）

| # | 内容 | 確かめ方 |
|---|---|---|
| R1 | 取込器の中でシェーダを読んで表現へ参照を入れる手順が、Unity 6.3 の並列取込で安定するか（README §4.5） | S5 で取り込み直しと、Library を消した後の取り込みを繰り返す。不安定ならシェーダのパスを持ち、実行時に引く形へ倒す |
| R2 | パッケージの `.meta` はリポジトリで作らず、Unity で開いたときに生成されたものをコミットする（GUID は利用側の写しでも同じになる） | S6 のローカル確認で `.meta` をコミットする |
| R3 | 埋め込みパッケージの Unity 側のテストが Test Runner に出るか（`testables`） | 解消: 適合検査は枠組みに依らない関数としてパッケージの `Conformance` に置き、呼ぶテストはプロジェクトの `Assets/Tests/EditMode/` に置くので `testables` に頼らない（`architecture/01`「パッケージ T2Font」） |
