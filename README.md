# T2Font

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.24076087.svg)](https://doi.org/10.5281/zenodo.24076087)

**Glyph representation API for Unity uGUI text** — a font asset split into a typography table and a pluggable glyph
representation (SDF included), with a pure C# TrueType reader and baker, a `.t2font` importer, a viewer and a conformance suite.
The package lives in [`Packages/info.someiyoshino.t2font/`](Packages/info.someiyoshino.t2font/README.md). License: [MIT](LICENSE).

**Rinkaku** (since v0.3.0) is a glyph representation for T2Font that keeps TrueType outlines as quadratic Bézier segments and draws them by
exact signed distance through a per-texel index of the nearest segments, so SDF-style edge effects (dilation, softness, outlines) work while
edges stay sharp at any size. It lives in [`Packages/info.someiyoshino.rinkaku/`](Packages/info.someiyoshino.rinkaku/README.md) (MIT).

Unity uGUI の文字描画のためのパッケージ。フォントアセットを「タイポグラフィテーブル」と差し替えられる「グリフの表現」に分け、表現を足しても字を置く側の
コードが変わらない API を提供する。使い方と契約はパッケージの README、ライセンスは MIT（権利者 t-tutiya）。

v0.3.0 から、グリフ表現 **Rinkaku**（輪郭）を同梱する。TrueType の輪郭を 2 次ベジエのセグメントのまま持ち、セルごとの最近傍のセグメントのインデックスを引いて
厳密な符号付き距離で描く。縁取り・太らせ・ぼかしは SDF と同じ式のまま使え、縁は表示サイズに依らず鋭い（パッケージの README・MIT）。

## Install / 導入

Unity Package Manager →「+」→「Add package from git URL…」:

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.t2font#v0.4.0
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.rinkaku#v0.4.0
```

`#` の後ろは版のタグ。Rinkaku は T2Font に依存するので、T2Font を先に同じタグで入れる。パッケージのフォルダを利用側の `Packages/` へ写して
使うこともできる（使い分けは T2Font の README §11・Rinkaku の README §11）。

## Citation / 引用

Each release is archived on Zenodo with its own DOI. The DOI in the badge,
[10.5281/zenodo.24076087](https://doi.org/10.5281/zenodo.24076087), cites all versions and resolves to the latest one
(v0.4.0: T2Font 0.4.0 and Rinkaku 0.2.0); its page lists the DOI of each version. v0.4.0 is
[10.5281/zenodo.24152696](https://doi.org/10.5281/zenodo.24152696) and v0.3.0 (T2Font 0.3.0 and Rinkaku 0.1.0) is
[10.5281/zenodo.24076088](https://doi.org/10.5281/zenodo.24076088). /
リリースは版ごとに Zenodo に記録し、DOI が付く。バッジの DOI（10.5281/zenodo.24076087）は全版をまとめるもので、最新の版
（v0.4.0＝T2Font 0.4.0 と Rinkaku 0.2.0）へ解決し、そのページに版ごとの DOI が並ぶ。v0.4.0 の DOI は 10.5281/zenodo.24152696、v0.3.0（T2Font 0.3.0 と Rinkaku 0.1.0）の DOI は
10.5281/zenodo.24076088。

```
Tsuchiya, T. (2026). T2Font and Rinkaku: glyph representation packages for Unity uGUI text (v0.4.0) [Computer software].
Zenodo. https://doi.org/10.5281/zenodo.24152696
```

The paper on Rinkaku (preprint, English and Japanese) is archived separately. Version 1.1 describes v0.4.0;
version 1.0 (10.5281/zenodo.24095980) describes v0.3.0. /
Rinkaku の論文（Preprint・英語版と日本語版）は別の記録にある。版 1.1 が v0.4.0 に、版 1.0（10.5281/zenodo.24095980）が v0.3.0 に対応する。

```
Tsuchiya, T. (2026). Rinkaku: Exact Signed Distance Rendering of CJK Glyphs with a Nearest-Segment Index (Version 1.1) [Preprint].
Zenodo. https://doi.org/10.5281/zenodo.24152359
```

## 開発

- 開発の規約と構造の入口は [`CLAUDE.md`](CLAUDE.md)、全ドキュメントの索引は [`docs/README.md`](docs/README.md)。
- 現在地と今後は [`docs/roadmap.md`](docs/roadmap.md)。
- The packages are developed elsewhere and copied here for each release; issues and pull requests are not accepted. /
  パッケージは別の開発環境で作り、版ごとに本リポジトリへ写して公開する。Issue とプルリクエストは受け付けない（D-T2FREPUB）。
