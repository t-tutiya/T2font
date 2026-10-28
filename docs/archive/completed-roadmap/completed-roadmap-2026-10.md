# 完了記録 — 2026-10

`docs/archive/completed-roadmap.md`（索引）から切り出した 2026-10 の完了記録。
**新しい項目は当月ファイルの先頭**（同日の既存エントリの上）へ追加し、索引にも 1 行追加する。

本書が持つのは**「いつ何が終わったか」**＝成果 1〜2 文・作業項目・移行に効く事実・正典リンク。
**裁定の経緯（なぜ・採らなかった案）は書かない**——決定 ID から `../decisions.md` 経由で月別本文へ引く（D-DOCDUP）。

---

## 2026-10-28 T2Font v0.1.0 — D-T2FONT

パッケージ `info.someiyoshino.t2font` の最初の公開版。字の表と差し替えられる字形の表現を分けた API に、同梱の SDF の表現・純 C# の
TrueType の読取と焼き器・`.t2font` の取込器・検証ビュー・適合検査を揃えた。契約の正典はパッケージの README、版の中身は CHANGELOG。

- 段 S1〜S7（骨格・Core・Bake・Runtime とシェーダ・取込器・検証・公開）。段取りは [`../completed-designs/t2font-v0.1-plan.md`](../completed-designs/t2font-v0.1-plan.md)（凍結）。
- SDF の焼きは T2Engine の生成器の移植で、同じ元フォント・同じ設定から画素・セル・送り幅まで同一の出力になる。
- 移行に効く事実:
  - 表現の種類の版数は種類ごとのカスタム依存（`info.someiyoshino.t2font/representation/<種類>`）で、上げるとその種類のフォントだけが取り込み直される（README §7.5）。
  - 取込器は表現のシェーダを読んで依存を張る。Library を消した後の取込でも資産ができることをローカルで確かめた（計画の R1）。
  - 適合検査は枠組みに依らない関数（`Someiyoshino.T2Font.Conformance`）で、呼ぶテストは利用側のプロジェクトに置く（`testables` に頼らない）。
