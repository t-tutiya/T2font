# T2Font

> **Frozen (2026-10-31).** Development of this repository has ended at `v0.2.0`; issues and pull requests are not accepted. The `v0.2.0` tag stays
> installable via the git URL below. / **凍結（2026-10-31）**: 本リポジトリは `v0.2.0` で開発を終えた。Issue と PR は受け付けない。`v0.2.0` は下の
> git URL でそのまま導入できる（D-T2FFREEZE）。

**Glyph representation API for Unity uGUI text** — a font asset split into a typography table and a pluggable glyph
representation (SDF included), with a pure C# TrueType reader and baker, a `.t2font` importer, a viewer and a conformance suite.
The package lives in [`Packages/info.someiyoshino.t2font/`](Packages/info.someiyoshino.t2font/README.md). License: [MIT](LICENSE).

Unity uGUI の文字描画のためのパッケージ。フォントアセットを「タイポグラフィテーブル」と差し替えられる「グリフの表現」に分け、表現を足しても字を置く側の
コードが変わらない API を提供する。使い方と契約はパッケージの README、ライセンスは MIT（権利者 t-tutiya）。

## Install / 導入

Unity Package Manager →「+」→「Add package from git URL…」:

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.t2font#v0.2.0
```

`#` の後ろは版のタグ。パッケージのフォルダを利用側の `Packages/` へ写して使うこともできる（使い分けはパッケージの README §11）。

## 開発

- 開発の規約と構造の入口は [`CLAUDE.md`](CLAUDE.md)、全ドキュメントの索引は [`docs/README.md`](docs/README.md)。
- 現在地と今後は [`docs/roadmap.md`](docs/roadmap.md)。
- Pull requests are accepted from collaborators only. / プルリクエストは共同作業者からだけ受け付ける。
