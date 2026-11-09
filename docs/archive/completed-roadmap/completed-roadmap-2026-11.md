# 完了記録 — 2026-11

`docs/archive/completed-roadmap.md`（索引）から切り出した 2026-11 の完了記録。
**新しい項目は当月ファイルの先頭**（同日の既存エントリの上）へ追加し、索引にも 1 行追加する。

本書が持つのは**「いつ何が終わったか」**＝成果 1〜2 文・作業項目・移行に効く事実・正典リンク。
**裁定の経緯（なぜ・採らなかった案）は書かない**——決定 ID から `../decisions.md` 経由で月別本文へ引く（D-DOCDUP）。

---

## 2026-11-09 v0.3.0 の公開（T2Font 0.3.0・Rinkaku 0.1.0） — D-T2FREPUB

v0.2.0 は利用側へ写され、そこで開発が続いた。その版を T2Font 0.3.0 として写し戻し、グリフ表現 Rinkaku 0.1.0 を同梱した。

- パッケージ: `Packages/info.someiyoshino.t2font/`（変更点は CHANGELOG の 0.3.0）・`Packages/info.someiyoshino.rinkaku/`（新規・MIT）。
- 検証のプロジェクト: ベイク仕様の誤りの文面 1 件のテストを直し、アセンブリ一覧の生成物を作り直した。それ以外は無改修で通る。
- **移行に効く事実**: `SpecSection` の読み口が変わった（T2Font の CHANGELOG の 0.3.0「Changed」）。SDF のベイクの出力は変わらない。
- 正典: 各パッケージの README・CHANGELOG。
