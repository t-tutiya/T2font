# T2Font

**Glyph representation API for Unity uGUI text** — a font asset split into a typography table and a pluggable glyph
representation (SDF included), with a pure C# TrueType reader and baker, a `.t2font` importer, a viewer and a conformance suite.
The package lives in [`Packages/info.someiyoshino.t2font/`](Packages/info.someiyoshino.t2font/README.md). License: [MIT](LICENSE).

Unity uGUI の文字描画のためのパッケージ。フォント資産を「字の表」と差し替えられる「字形の表現」に分け、表現を足しても字を置く側の
コードが変わらない API を提供する。使い方と契約はパッケージの README、ライセンスは MIT（権利者 t-tutiya）。

## Install / 導入

Unity Package Manager →「+」→「Add package from git URL…」:

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.t2font#v0.1.0
```

`#` の後ろは版のタグ。パッケージのフォルダを利用側の `Packages/` へ写して使うこともできる（使い分けはパッケージの README §11）。

## 開発

- 開発の規約と構造の入口は [`CLAUDE.md`](CLAUDE.md)、全ドキュメントの索引は [`docs/README.md`](docs/README.md)。
- 現在地と今後は [`docs/roadmap.md`](docs/roadmap.md)。
- Pull requests are accepted from collaborators only. / プルリクエストは共同作業者からだけ受け付ける。
