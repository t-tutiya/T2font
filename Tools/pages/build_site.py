#!/usr/bin/env python3
"""GitHub Pages のサイトを組み立てる（D-PAGES）。

docs/architecture/ の図解の HTML（と同じ名前のフォルダの画像）を出力先の architecture/ へ写し、
各 HTML の <title> と eyebrow から目次の index.html を作る。Markdown の文書は写さない
（GitHub の画面で読める。サイトは GitHub が表示できない HTML の図解のためのもの）。

使い方（リポジトリのルートで）: python3 Tools/pages/build_site.py _site
.github/workflows/pages.yml が main への push で走らせ、出力を Pages へ配備する。
"""

import html
import pathlib
import re
import shutil
import sys

REPOSITORY = "https://github.com/t-tutiya/T2font"

# 目次の区画。(見出し, docs/architecture/ からの相対のフォルダ)。フォルダの直下の *.html を名前の順に並べる。
SECTIONS = [
    ("T2Font", "."),
    ("Rinkaku", "rinkaku"),
]


def read_meta(path: pathlib.Path) -> tuple[str, str]:
    """HTML の <title> と eyebrow（章の番号とパッケージ）の文字列。"""
    text = path.read_text(encoding="utf-8")
    title = re.search(r"<title>(.*?)</title>", text, re.S)
    eyebrow = re.search(r'<p class="eyebrow">(.*?)</p>', text, re.S)
    if title is None:
        sys.exit(f"{path}: <title> がありません。")
    return (html.unescape(title.group(1)).strip(),
            html.unescape(re.sub(r"<[^>]+>", "", eyebrow.group(1))).strip() if eyebrow else "")


def build(source: pathlib.Path, output: pathlib.Path) -> None:
    if output.exists():
        shutil.rmtree(output)
    target = output / "architecture"
    target.mkdir(parents=True)

    sections_html = []
    for heading, folder in SECTIONS:
        src_dir = source / folder
        dst_dir = target / folder
        dst_dir.mkdir(parents=True, exist_ok=True)
        items = []
        for page in sorted(src_dir.glob("*.html")):
            shutil.copy2(page, dst_dir / page.name)
            assets = src_dir / page.stem
            if assets.is_dir():
                shutil.copytree(assets, dst_dir / page.stem)
            title, eyebrow = read_meta(page)
            href = f"architecture/{page.name}" if folder == "." else f"architecture/{folder}/{page.name}"
            items.append(
                f'<li><a href="{html.escape(href)}">{html.escape(title)}</a>'
                f'<span class="sub">{html.escape(eyebrow)}</span></li>')
        if not items:
            sys.exit(f"{src_dir}: 図解の HTML がありません。")
        sections_html.append(f"<h2>{html.escape(heading)}</h2>\n<ul>\n" + "\n".join(items) + "\n</ul>")

    index = INDEX_TEMPLATE.replace("{{SECTIONS}}", "\n".join(sections_html)).replace("{{REPOSITORY}}", REPOSITORY)
    (output / "index.html").write_text(index, encoding="utf-8")
    # Pages の Jekyll を通さない（HTML をそのまま配る）。
    (output / ".nojekyll").write_text("", encoding="utf-8")


INDEX_TEMPLATE = """<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>T2Font と Rinkaku の図解</title>
<style>
:root { --bg: #fbfaf7; --fg: #1d1d1f; --muted: #5f6368; --accent: #9b2c2c; --rule: #e3e0d8; }
@media (prefers-color-scheme: dark) {
  :root { --bg: #17171a; --fg: #ececec; --muted: #a8a8ad; --accent: #e8908f; --rule: #34343a; }
}
body { margin: 0; background: var(--bg); color: var(--fg);
  font-family: "BIZ UDPGothic", "Hiragino Sans", "Yu Gothic", system-ui, sans-serif; line-height: 1.7; }
main { max-width: 760px; margin: 0 auto; padding: 48px 16px 64px; }
h1 { font-size: 1.6rem; margin: 0 0 8px; }
h2 { font-size: 1.15rem; margin: 36px 0 8px; padding-bottom: 4px; border-bottom: 1px solid var(--rule); }
p { color: var(--muted); }
ul { list-style: none; padding: 0; margin: 0; }
li { padding: 10px 0; border-bottom: 1px solid var(--rule); }
a { color: var(--accent); text-decoration: none; font-weight: 700; }
a:hover { text-decoration: underline; }
.sub { display: block; font-size: 0.85rem; color: var(--muted); }
</style>
</head>
<body>
<main>
<h1>T2Font と Rinkaku の図解</h1>
<p>Unity uGUI の文字描画のパッケージ T2Font と、そのグリフ表現 Rinkaku の動作を図で解説する（日本語）。
使い方の手引き・API リファレンス・契約の正典（パッケージの README）は <a href="{{REPOSITORY}}">GitHub のリポジトリ</a>にある
（<a href="{{REPOSITORY}}/blob/main/docs/manual/README.md">利用の手引き</a>・
<a href="{{REPOSITORY}}/blob/main/docs/manual/README.en.md">User guide (English)</a>）。</p>
{{SECTIONS}}
</main>
</body>
</html>
"""


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit("使い方: python3 Tools/pages/build_site.py <出力先>")
    root = pathlib.Path(__file__).resolve().parents[2]
    build(root / "docs" / "architecture", pathlib.Path(sys.argv[1]))


if __name__ == "__main__":
    main()
