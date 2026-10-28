# T2font

（プロジェクトの 1〜2 行の説明）

- 開発の規約と構造の入口は [`CLAUDE.md`](CLAUDE.md)、全ドキュメントの索引は [`docs/README.md`](docs/README.md)。
- 現在地と今後は [`docs/roadmap.md`](docs/roadmap.md)。

## 新しいプロジェクトを始める手順

終わった項目にチェックを付け、全部終わったらこの節を消して `docs/roadmap.md` の着手候補 1 を完了記録へ退避する。

- [ ] 1. **リポジトリを作る**: GitHub でテンプレートのページを開き、「Use this template」→「Create a new repository」。公開範囲は Private を推奨。
- [ ] 2. **Claude の GitHub App の対象に入れる**（App を「選んだリポジトリだけ」で入れている場合）: GitHub 右上のアイコン → Settings → Applications → Installed GitHub Apps → Claude の Configure で新しいリポジトリを追加する。
- [ ] 3. **クローンする**: GitHub Desktop の「Clone a repository」で手元に取得する。**まだ Unity で開かない**（開くとフォルダ名が変わる前の状態で Library が作られる）。
- [ ] 4. **初期化する**: リポジトリのルートで `python Tools/init_project.py <プロジェクト名>` を実行する（プロジェクト名は英字で始まる英数字。例 `MyGame`）。
      `T2font` の綴りを全部置き換え、該当するファイル・フォルダの名前を変え、Unity の GUID を作り直す。Claude に「初期化して」と頼んでもよい。
      終わったら GitHub Desktop でコミットして push する。
- [ ] 5. **Unity で開く**: Unity Hub の「Add」→「Add project from disk」で選んで開く。Unity の版を上げる場合はこの時点で行う（D-UNITYVER）。
      開いた後に増えた・変わったファイル（`Packages/packages-lock.json` 等）があればコミットする。
- [ ] 6. **CI の Unity コンパイルゲートを有効にする**: GitHub のリポジトリの Settings → Secrets and variables → Actions に `UNITY_LICENSE`・`UNITY_EMAIL`・`UNITY_PASSWORD`
      を登録する（手順は [`docs/guides/code-quality.md`](docs/guides/code-quality.md)「Job B」）。未登録の間は Job B が自動でスキップされる。
- [ ] 7. **ブランチを守る（任意）**: Settings → Branches で `main` に「PR を必須」「`lint-and-unit` の成功を必須」を設定する。
- [ ] 8. **claude.ai/code の環境を用意する**: 新しいリポジトリを選んでセッションを始める。ネットワークポリシーは既定のままでよい（検証は CI 経由で回る・D-ENV4）。
- [ ] 9. **書き始める**: `CLAUDE.md` の「プロジェクト概要」と、`docs/roadmap.md` の「いま」を書く。
