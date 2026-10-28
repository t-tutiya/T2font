<!-- PR テンプレート。該当しない項目は削除して構いません。 -->

## 概要

<!-- 何を・なぜ変更したか。関連する roadmap の項目 / decisions ID / plans 正典を記載。 -->

## 変更点

-

## 未実施検証（環境制約で本 PR 内で実行できなかったもの）

> 検証ステータスの置き場は **PR 本文のみ**（docs には書かない・D-ENV3）。実行できたものは「実施済み」に、リモート環境等で実行できなかったものはチェックを外し理由を添える。
>
> **上 2 つは CI でも実施できる**（検証経路③・D-ENV4）。リモートセッションで dotnet/Unity が無くても、ブランチを push して `quality.yml` を回せば NUnit（Job A）は満たせ、ヘッドレスコンパイル相当（Job B）はこの PR の `pull_request` トリガで回る（Job B は PR でだけ回る・D-JOBBPR）。CI で通したものは「実施済み」にし、実行した run の URL を添える。**未チェックのまま残してよいのは、どの経路でも回せなかったものだけ**。

- [ ] NUnit（`dotnet test Tests/` または CI Job A）
- [ ] ヘッドレスコンパイル（`Tools/verify-unity-compile.ps1` または CI Job B）
- [ ] Play Mode テスト
- [ ] 実機目視（該当する演出・座標・表示）

## ドキュメント還元チェックリスト

> 実装完了時ではなく **この PR で** 済ませる（退避の後回しを防ぐ）。

- [ ] **完了項目の退避**: 本 PR で roadmap の項目が完了した場合、`docs/roadmap.md` の該当行を完了記録へ退避した——本文は**当月ファイル** `docs/archive/completed-roadmap/completed-roadmap-YYYY-MM.md` の先頭へ、索引 `docs/archive/completed-roadmap.md` にも **1 行**（`完了日 ｜ 完了項目 ｜ 決定 ID`）を追加し、**月見出しの「（N 件）」も合わせた**（D-CRMONTH）。roadmap には未着手・進行中のみ残す。
- [ ] **roadmap 更新**: 「now / next / later」と「着手候補」表を現況へ更新した（順序の正典はこの 2 つのみ）。
- [ ] **decisions 追記**: 重要な設計判断があれば当月ファイル `docs/archive/decisions/decisions-YYYY-MM.md` の先頭へ追記し、索引 `docs/archive/decisions.md` にも **1 行**（`日付 ｜ **太字タイトル（決定 ID）** — 30 字以内の補足`）を追加した。
- [ ] **正典の同期**: 変更で構造・仕様が動いた場合、`docs/architecture/`・`docs/reference/`・該当 `docs/plans/` を更新した。
- [ ] **設計文書のライフサイクル（二択）**: 本 PR で実装が完了した `plans/` の設計文書について、**どちらか**を満たした——①**凍結退避した**（要点を decisions／完了記録へ集約し、本体を `docs/archive/completed-designs/` へ移して冒頭に凍結バナー。削除しない・`docs/archive/README.md` の退避ログに 1 行）／②**凍結しないので、冒頭バナーに「本書が現役なのは〜を所有するため」を明記した**（D-DOCLIFE・規則の正典は `docs/README.md`「ドキュメント運用ルール」）。
- [ ] **テンプレートへの還元**: 汎用の規約・仕組み（`docs/guides/template-sync.md` の表にあるファイル）を変えた場合、テンプレートへ戻す PR を作った、または作る必要がないと判断した。

## コード品質チェックリスト

- [ ] **省略可能引数を新設していない**（D-ARGOPT）。足した場合は、**なぜ既定値でなければならないか**をコメントで残した。既定値が要るのがテストだけなら、既定値は共有テスト支援層（`Assets/Tests/CoreSupport/`）へ置く。
- [ ] **`= null` を「空」の意味で使っていない**。空は空の値（`Array.Empty<T>()` / `Empty` 静的）で表し、nullable は「無いことに意味がある」ときだけ使う。
- [ ] **Unity 側の公開 API・インターフェースを変えた場合**、`Assets/Tests/` のインライン Fake・Stub も同じ PR で直した（Job A ではコンパイルされない）。
