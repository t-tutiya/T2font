# 完了済みロードマップ（索引）

完了した Phase / Step / 着手候補の記録。`docs/roadmap.md` を「現在地と今後」に集中させるため、
完了した項目をここへ退避する。

本文は**月別ファイル**（`docs/archive/completed-roadmap/completed-roadmap-YYYY-MM.md`）に分割する（D-CRMONTH。`decisions.md` と同じ形）。
**新しい項目は当月ファイルの先頭に追加し、本索引にも 1 行追加する**（当月ファイルが無ければ新規作成し、索引に月見出しを足す）。

**索引行の形式**: `完了日 ｜ 完了項目（roadmap にあった見出し） ｜ 決定 ID`。索引は「1 行で見つけて本文へ飛ぶ」
ためのもので、**成果・作業項目・移行に効く事実は月別本文が正典**、**裁定の経緯は `decisions.md` が正典**である
（索引にも本文にも要約を複製しない＝D-DOCDUP）。

**完了日は roadmap に書かれていた表記のまま**載せる（`2026-06-08〜15` のような幅のある表記もそのまま）。
**どの月のファイルへ入るかは「最後の日付」で決める**。索引行と本文見出しは**同じ文字列**を持つ（下の不変条件 2 が比較できるように）。

守るべき不変条件は次の 2 本で、`decisions.md` と同型である（機械検査は `Tests/DocsIndexCoverageTests.cs`）:

1. **月見出しが宣言する件数＝その月の索引行数**（数え間違い・更新漏れを止める）。
2. **月別本文の `## 見出し` と索引行が、完了日ごとに同じ数だけある**（本文へ追記して索引行を足し忘れる、とその逆を止める）。

月見出しの書式（最初の項目を退避するときに、下の「---」の後へ足す。`YYYY-MM` 等は実際の値に置き換える）:

```markdown
## YYYY-MM — [`completed-roadmap/completed-roadmap-YYYY-MM.md`](completed-roadmap/completed-roadmap-YYYY-MM.md)（N 件）

| 完了日 | 完了項目 | 決定 ID |
|---|---|---|
| YYYY-MM-DD | 項目の見出し | D-XXX |
```

月別本文の書式（ファイル冒頭）:

```markdown
# 完了記録 — YYYY-MM

`docs/archive/completed-roadmap.md`（索引）から切り出した YYYY-MM の完了記録。
**新しい項目は当月ファイルの先頭**（同日の既存エントリの上）へ追加し、索引にも 1 行追加する。

本書が持つのは**「いつ何が終わったか」**＝成果 1〜2 文・作業項目・移行に効く事実・正典リンク。
**裁定の経緯（なぜ・採らなかった案）は書かない**——決定 ID から `../decisions.md` 経由で月別本文へ引く（D-DOCDUP）。

---

## YYYY-MM-DD 項目の見出し — D-XXX
```

---

## 2026-10 — [`completed-roadmap/completed-roadmap-2026-10.md`](completed-roadmap/completed-roadmap-2026-10.md)（3 件）

| 完了日 | 完了項目 | 決定 ID |
|---|---|---|
| 2026-10-29 | 頂点色を gamma のまま渡す Canvas への対応（着手候補 #3） | D-UIGAMMA |
| 2026-10-28 | プロジェクトの初期設定を終える | D-TEMPLATE |
| 2026-10-28 | T2Font v0.1.0 | D-T2FONT |
