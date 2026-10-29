# 完了記録 — 2026-10

`docs/archive/completed-roadmap.md`（索引）から切り出した 2026-10 の完了記録。
**新しい項目は当月ファイルの先頭**（同日の既存エントリの上）へ追加し、索引にも 1 行追加する。

本書が持つのは**「いつ何が終わったか」**＝成果 1〜2 文・作業項目・移行に効く事実・正典リンク。
**裁定の経緯（なぜ・採らなかった案）は書かない**——決定 ID から `../decisions.md` 経由で月別本文へ引く（D-DOCDUP）。

---

## 2026-10-29 頂点色を gamma のまま渡す Canvas への対応（着手候補 #3） — D-UIGAMMA

`T2Font/SDF` が、Canvas の Vertex Color Always in Gamma Color Space を有効にしたときに gamma のまま来る頂点色を linear に直すようにした。
シェーダの契約 S9 として全表現の義務にし、T2Font 0.2.0 として出す（未リリースだった用語の変更も同じ版に入る）。

- パッケージ: シェーダ `T2Font/SDF`・README §5 S9・適合検査 `CheckRendering` の S9 の検査・CHANGELOG と `package.json` の 0.2.0・導入の URL のタグ。
- テスト: EditMode の `T2FontTextColorSpaceTests` が Canvas を通して描き、設定の有効・無効で面の色を組み込みの UI シェーダの四角と比べる（描画装置が要る）。
- 文書: 03 章の §8.2・§9.2・図 7。方針の比較は [`../completed-designs/vertex-color-gamma-study.md`](../completed-designs/vertex-color-gamma-study.md)（凍結）。
- 移行に効く事実: 表現を足すパッケージのシェーダは S9 を満たす——`_UIVertexColorAlwaysGammaSpace` を Properties に置かずに int で宣言し、立てば
  （Linear のプロジェクトで）頂点色の rgb を `UIGammaToLinear` で直す。満たさないと適合検査の S9 で落ちる。

## 2026-10-28 プロジェクトの初期設定を終える — D-TEMPLATE

テンプレートから作ったリポジトリの初期設定（ルートの README にあったチェックリスト 1〜9）を終え、チェックリストの節を消した。

- 公開範囲は Public（パッケージを OSS として公開するため・D-T2FONT）。
- CI の Job B の secrets を登録し、main をルールセットで守る（PR を必須・必須のチェック）。
- 移行に効く事実: プルリクエストは共同作業者だけが作れる設定にした（リポジトリの Settings → General → Features）。Claude が作る PR は
  ユーザーの名義なので、この設定の影響を受けない。

## 2026-10-28 T2Font v0.1.0 — D-T2FONT

パッケージ `info.someiyoshino.t2font` の最初の公開版。字の表と差し替えられる字形の表現を分けた API に、同梱の SDF の表現・純 C# の
TrueType の読取と焼き器・`.t2font` の取込器・検証ビュー・適合検査を揃えた。契約の正典はパッケージの README、版の中身は CHANGELOG。

- 段 S1〜S7（骨格・Core・Bake・Runtime とシェーダ・取込器・検証・公開）。段取りは [`../completed-designs/t2font-v0.1-plan.md`](../completed-designs/t2font-v0.1-plan.md)（凍結）。
- SDF の焼きは T2Engine の生成器の移植で、同じ元フォント・同じ設定から画素・セル・送り幅まで同一の出力になる。
- 移行に効く事実:
  - 表現の種類の版数は種類ごとのカスタム依存（`info.someiyoshino.t2font/representation/<種類>`）で、上げるとその種類のフォントだけが取り込み直される（README §7.5）。
  - 取込器は表現のシェーダを読んで依存を張る。Library を消した後の取込でも資産ができることをローカルで確かめた（計画の R1）。
  - 適合検査は枠組みに依らない関数（`Someiyoshino.T2Font.Conformance`）で、呼ぶテストは利用側のプロジェクトに置く（`testables` に頼らない）。
