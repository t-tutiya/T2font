# docs/archive — アーカイブ索引

**完了・凍結・歴史専用**。`decisions.md` 以外は通常の開発作業では参照不要（「なぜそうしたか」を追う時のみ読む）。**生きた設計計画は `docs/plans/`** にある（本フォルダではない）。

## 構成

| 場所 | 役割 |
|---|---|
| [`decisions.md`](decisions.md) | **設計判断ログ（索引）**。本文は `decisions/decisions-YYYY-MM.md` に月別分割。「なぜそうしたか」の**生きた正典**（決定 ID で追跡・追記のみ） |
| [`completed-roadmap.md`](completed-roadmap.md) | **完了済みロードマップ（索引）**。本文は `completed-roadmap/completed-roadmap-YYYY-MM.md` に月別分割（`docs/roadmap.md` から完了項目を退避していく） |
| `completed-designs/` | **実装完了で凍結した設計文書**（`docs/plans/` の完了分）。冒頭に凍結バナー・以後更新しない。最初の 1 本を退避するときにフォルダを作る |
| `evaluations/` | 客観評価レポート・コードレビュー。最初の 1 本を置くときにフォルダを作る |
| `sources/` | 過去の設計の入力資料（評価 PDF・調査レポート等）。置くときにフォルダを作る |

> **設計文書のライフサイクル（D-DOCLIFE）**: 生きた設計計画は `docs/plans/`。**実装が完了したら二択**——①削除せず `completed-designs/` へ凍結退避し、要点を `decisions.md` / 完了記録に集約する／②`plans/` に残すなら**冒頭バナーに「本書が現役なのは〜を所有するため」を明記する**。規則の正典は `../README.md`「ドキュメント運用ルール」。削除するのは**不採用・陳腐化・上位文書へ改稿された文書のみ**（下記「退避・復元・削除ログ」）。

## completed-designs/（実装完了で凍結した設計文書）

**下表は 1 行の対象説明と決定 ID だけを持つ**。現行の正典・凍結後も参照する節・申し送りは各文書の冒頭バナーが正典で、下表にも `decisions.md` にも複製しない（D-DOCDUP）。フォルダの `*.md` と下表の行は 1 対 1（`Tests/DocsIndexCoverageTests.cs` が検査する）。

| ファイル | 対象 | 決定 ID |
|---|---|---|
| [`completed-designs/vertex-color-gamma-study.md`](completed-designs/vertex-color-gamma-study.md) | 頂点色を gamma のまま渡す Canvas への対応（A: シェーダで直す・B: ホストで止める）の比較 | D-UIGAMMA |
| [`completed-designs/t2font-v0.1-plan.md`](completed-designs/t2font-v0.1-plan.md) | T2Font v0.1.0 の段取り（S1〜S7・各段の中身と検証） | D-T2FONT |

## evaluations/（客観評価・コードレビュー）

フォルダの `*.md` と下表の行は 1 対 1（`Tests/DocsIndexCoverageTests.cs` が検査する）。

| ファイル | 内容 |
|---|---|

## 退避・復元・削除ログ

文書を `completed-designs/` へ退避した・復元した・削除したときに 1 行足す（新しいものを上に）。

| 日付 | 操作 | 文書 | 理由・決定 ID |
|---|---|---|---|
| 2026-10-29 | 退避 | `vertex-color-gamma-study.md` | 案 A を採って 0.2.0 で実装した（D-UIGAMMA・D-DOCLIFE） |
| 2026-10-28 | 退避 | `t2font-v0.1-plan.md` | v0.1.0 の段 S1〜S7 を終えた（D-T2FONT・D-DOCLIFE） |
