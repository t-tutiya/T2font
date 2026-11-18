# T2Font and Rinkaku user guide

[日本語](README.md)

This guide is for programmers who use T2Font (a text rendering package for Unity uGUI) and Rinkaku (a glyph representation built on it) in
their own Unity projects.

- **The package READMEs are the contract** ([T2Font](../../Packages/info.someiyoshino.t2font/README.md),
  [Rinkaku](../../Packages/info.someiyoshino.rinkaku/README.md); written in Japanese). They define the API guarantees, the data formats and the
  shader rules. This guide covers procedures and code examples, and links to README sections for the contract. "README §n" means a section
  of the T2Font README unless stated otherwise.
- Prerequisites: Unity 6, uGUI (Canvas and `RectTransform`), C#.
- Only this page and the quickstart are available in English. The other pages of the guide are in Japanese.

## Pages

| Page | Read it when you want to |
|---|---|
| [`01-quickstart.en.md`](01-quickstart.en.md) | Install the packages and draw your first characters on a Canvas, then switch the glyph representation from SDF to Rinkaku |

Japanese pages (日本語の文書), listed in [`README.md`](README.md):

- [`02-host-guide.md`](02-host-guide.md): writing your own host (loading a font, placing glyphs, geometry, materials, fades, cleanup)
- [`03-choosing-representation.md`](03-choosing-representation.md): choosing between SDF and Rinkaku, and tuning their settings
- [`05-troubleshooting.md`](05-troubleshooting.md): import errors, missing glyphs, appearance, performance and platform issues by symptom

## API reference

Signatures and descriptions of the public types are in the [API reference](../generated/api/README.md), generated from the package
sources (the descriptions are in Japanese). Types are split into **inside the contract** (types meant for users, grouped by role) and
**outside the contract** (baker internals and types for tests and measurements, which may change without notice between versions). Use
only the types inside the contract from your code.

## How it works

You do not need these pages to use the packages. They help you understand the behavior and narrow down problems. They are in Japanese.

| Page | Contents |
|---|---|
| [From TrueType to the atlas](../architecture/02-truetype-to-atlas.html) | TrueType structure, building the typography table, baking the SDF atlas |
| [From the atlas to the screen](../architecture/03-atlas-to-screen.html) | Glyph placement, materials, CanvasRenderer and batching, the shader drawing to the render target |
| [Writing a bake spec](../architecture/04-bake-spec.html) | `.t2font` keys, creating a font asset, import errors and fixes, character set contents |

These pages are HTML with figures. GitHub shows their source, so clone the repository (or download it as ZIP) and open them in a browser.

## Versions and changes

- The install URLs and the latest release tag are in the [`README.md`](../../README.md) at the repository root (Install).
- Changes per version, including breaking changes, are in each package's CHANGELOG
  ([T2Font](../../Packages/info.someiyoshino.t2font/CHANGELOG.md), [Rinkaku](../../Packages/info.someiyoshino.rinkaku/CHANGELOG.md)).
  While the version is 0.x, even a minor release can break compatibility.

## Terms

| Term | Meaning |
|---|---|
| Host | The code that places glyphs on screen: the viewer `T2FontText`, or your own text renderer |
| Typography table | Advances, ink bounds and vertical metrics. Identical for every glyph representation |
| Glyph representation | How the ink of a glyph is drawn (SDF, Rinkaku, ...). A sub-asset of the font asset |
| Bake spec | A `.t2font` file (JSON). The importer bakes a font asset from the source font according to it |
| Edge style | Face dilation and softness, outline width and color, outline softness. Values are in em |
