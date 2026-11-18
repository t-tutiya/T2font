# Quickstart

[日本語](01-quickstart.md)

This page takes you from installing the packages to baking a font asset from a source font and drawing your first characters on a Canvas.
At the end you switch the glyph representation from SDF to Rinkaku and compare them. No code is needed.

## 1. Requirements

- **Unity 6** (at least the version in the package's `package.json` field `unity`). The dependencies uGUI (`com.unity.ugui`) and
  Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) are installed together with the package.
- **A source font**: a single TrueType font with `glyf` outlines. Most `.ttf` files qualify. Font collections (`.ttc`) and fonts with CFF
  outlines (most `.otf` files) cannot be read.
- The font's license must allow embedding in your game and distributing baked derivatives (the atlas textures).

## 2. Install the packages

In Unity's Package Manager, click "+" and choose "Add package from git URL…", then enter the URL below. `<tag>` is a release tag; the latest
one is in the [`README.md`](../../README.md) at the repository root (Install).

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.t2font#<tag>
```

To use Rinkaku as well, add it with the same tag after T2Font (Rinkaku depends on T2Font). Add it now if you plan to follow step 8.

```
https://github.com/t-tutiya/T2font.git?path=/Packages/info.someiyoshino.rinkaku#<tag>
```

You can also copy a package folder (including the `.meta` files and LICENSE.md) into your project's `Packages/`. Copy it when a build
outside Unity, such as an external NUnit project, needs to link the package sources: a package installed from a git URL lives read-only in
the Library (README §11).

## 3. Add the source font

Put the source font under `Assets/`. This page uses `Assets/Fonts/MyFont.ttf`. Unity also imports a `.ttf` as its own Font asset, but T2Font
does not use it; the importer reads the bytes of the file directly.

## 4. Write a bake spec

Create a text file `Assets/Fonts/MyFont.t2font` and save it as UTF-8 with the content below. Unity's Create menu has no entry for it, so
create it with your file manager or a text editor (if Windows Explorer hides extensions, the file easily ends up as `MyFont.t2font.txt`).

```jsonc
// Lines starting with // are comments.
{
    "source": "Assets/Fonts/MyFont.ttf",
    "coverage": "fixed",
    "charsets": ["Ascii"],
    "characters": "Hello, world.",
    "representation": { "kind": "sdf" }
}
```

| Key | Meaning |
|---|---|
| `source` | Project path of the source font (starts with `Assets/` or `Packages/`, separated by `/`) |
| `coverage` | `fixed` (a typeface used only for known characters) or `corpus` (a typeface that must cover all body text). The baker behaves the same; the value tells your own checks how strict to be |
| `charsets` | Names of fixed character sets. For Japanese body text use `["DevelopmentDefault"]` (all of JIS X 0208 and more; the import takes tens of seconds) |
| `characters` | Additional characters. The import fails if the source font lacks any of them |
| `representation` | The kind of glyph representation and its settings. Omitted settings take their defaults |

The keys are defined in README §7.1.

## 5. Check the import

When you save the file and return to Unity, the import starts. On success the Console shows one line such as
`[T2Font] Assets/Fonts/MyFont.t2font: …字・グリフ…・sdf・1 ページ…` (the report is in Japanese). Expand `MyFont.t2font` (▶) in the Project
window: under the main asset `T2FontAsset` you find the glyph representation (`MyFont (sdf)`) and the page textures.

On failure the Console shows a red error and no asset is created. The message starts with the path of the bake spec and the line number.

The baked results (the atlas and the typography table) are generated into the Library. Commit only the bake spec and the source font
(with their `.meta` files) to your repository (README §2.4).

## 6. Draw characters

1. In the Hierarchy, create "GameObject > UI > Canvas".
2. Right-click the Canvas and choose "Create Empty" to add a child GameObject (it gets a `RectTransform`). Give it a generous width and
   height (for example 800×300).
3. In the Inspector, use "Add Component" to add `T2FontText`. It is the package's viewer and draws one string.
4. Drag `MyFont.t2font` (or the main asset under ▶) into the `Font` field.
5. Type "Hello, world." into `Text`. `Font Size` is the size of 1 em in pixels.

Characters start at the top-left corner of the `RectTransform`. Lines advance only at line breaks; there is no wrapping, kerning or
vertical writing. `Face Color` and `Alpha` set the face color and the fade, and `Clip To Rect` clips the characters to the component's own
rectangle.

`T2FontText` is a minimal host for checking fonts. For text rendering in your product, write your own host with the same API.

## 7. Try edge styles

The fields under the header "縁（em）" (edges, in em) of `T2FontText` change the edge style. Values are relative to the character size (em).

| Field | Value to try | What you see |
|---|---|---|
| `Outline Width` and `Outline Color` | 0.05, black | An outline |
| `Face Dilate` | 0.03 or -0.02 | A bolder or thinner face |
| `Face Softness`, `Outline Softness` | 0.04 | Soft face edge, soft outline edge |

README §6.1 defines the ranges and meanings. An out-of-range value or an effect the glyph representation cannot draw is not approximated: the
host logs an error to the Console and does not draw (README §6.3).

## 8. Switch the glyph representation to Rinkaku

1. If you did not install Rinkaku in step 2, install it with the same tag.
2. Duplicate `MyFont.t2font` as `MyFont-rinkaku.t2font` and change `representation` as follows.

   ```jsonc
   "representation": { "kind": "rinkaku" }
   ```

3. After the import, the glyph representation under ▶ is `MyFont-rinkaku (rinkaku)`.
4. Create a second `T2FontText` with `MyFont-rinkaku`, set `Font Size` to about 200 for both, and put them side by side. SDF rounds the
   corners; Rinkaku keeps them sharp at any display size. Edge style values keep the same meaning.

Switching the glyph representation does not change the advances or the ink bounds (the typography table), so the layout of the characters
does not move (README §2.3). Rinkaku costs more per pixel to draw than SDF (Rinkaku README §7).

## 9. What to read next

- Pages for other goals, such as writing your own host or trimming the character set for shipping, are listed in
  [`README.en.md`](README.en.md) (most of them are in Japanese).
- The contract (API guarantees, shader rules) is the [T2Font README](../../Packages/info.someiyoshino.t2font/README.md) (Japanese).
