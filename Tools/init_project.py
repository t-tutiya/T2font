"""テンプレートから作ったリポジトリを、新しいプロジェクトとして初期化する（D-TEMPLATE）。

使い方（リポジトリのルートで、Unity で開く前に 1 回だけ実行する）:

    python Tools/init_project.py MyGame

行うこと:
  1. テキストファイル中の綴り TemplateProject をプロジェクト名へ置き換える。
  2. 名前に TemplateProject を含むファイル・フォルダ（.meta を含む）を改名する。
  3. Assets/ 配下の .meta の GUID と ProjectSettings の productGUID を作り直す
     （同じテンプレートから作ったプロジェクトどうしで GUID が衝突しないようにする）。
  4. README.md のテンプレート説明の節を消す。

置き換えない場所:
  - docs/archive/ … 歴史記録。地の文は当時の綴りのまま残す（D-DOCTREE）。
  - docs/guides/template-sync.md … テンプレートの綴りを説明する文書。
  - 本スクリプト自身。
"""
import pathlib
import re
import sys
import uuid

TOKEN = "TemplateProject"
ROOT = pathlib.Path(__file__).resolve().parent.parent

SKIP_DIRS = {".git", "Library", "Temp", "Logs", "obj", "bin", "UserSettings", "__pycache__"}
KEEP_PATHS = {"docs/guides/template-sync.md", "Tools/init_project.py"}
KEEP_PREFIXES = ("docs/archive/",)

GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)
PRODUCT_GUID = re.compile(r"(  productGUID: )[0-9a-f]{32}")
TEMPLATE_SECTION = re.compile(r"\n<!-- template:begin.*?<!-- template:end -->\n\n", re.DOTALL)


def rel(path):
    return path.relative_to(ROOT).as_posix()


def walk(base):
    """base 配下のファイルとフォルダを、深い順（子が親より先）に返す。"""
    found = []
    for path in base.iterdir():
        if path.is_dir():
            if path.name in SKIP_DIRS:
                continue
            found.extend(walk(path))
        found.append(path)
    return found


def read_text(path):
    data = path.read_bytes()
    if b"\0" in data:
        return None
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:
        return None


def write_text(path, text):
    # 改行を変えない（newline="" で LF のまま書く・D-GENEOL）。
    path.write_text(text, encoding="utf-8", newline="")


def is_kept(path):
    r = rel(path)
    return r in KEEP_PATHS or r.startswith(KEEP_PREFIXES)


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    name = sys.argv[1]
    if not re.fullmatch(r"[A-Z][A-Za-z0-9]*", name) or name == TOKEN:
        print(f"プロジェクト名は英大文字で始まる英数字にしてください（C# の名前空間と asmdef 名に使うため）: {name!r}")
        return 2
    if (ROOT / "Library").exists():
        print("Library/ があります。Unity で開く前に実行してください（開いた後に改名すると GUID の対応が崩れます）。")
        print("やり直すなら Library/ を消してから実行してください。")
        return 1
    if not (ROOT / "Assets" / f"{TOKEN}.Core").exists():
        print(f"Assets/{TOKEN}.Core が見つかりません。初期化済みではありませんか。")
        return 1

    files = [p for p in walk(ROOT) if p.is_file()]

    # 1. 綴りの置き換え
    replaced = 0
    for path in files:
        if is_kept(path):
            continue
        text = read_text(path)
        if text is None or TOKEN not in text:
            continue
        write_text(path, text.replace(TOKEN, name))
        replaced += 1

    # 2. 改名（深い順なので、子を改名してから親を改名できる）
    renamed = 0
    for path in walk(ROOT):
        if TOKEN in path.name and not is_kept(path):
            path.rename(path.with_name(path.name.replace(TOKEN, name)))
            renamed += 1

    # 3. GUID の作り直し（参照は Assets/ と ProjectSettings/ のテキストにある）
    metas = [p for p in walk(ROOT / "Assets") if p.is_file() and p.suffix == ".meta"]
    mapping = {}
    for meta in metas:
        m = GUID_LINE.search(read_text(meta) or "")
        if m:
            mapping[m.group(1)] = uuid.uuid4().hex
    targets = [p for base in ("Assets", "ProjectSettings") for p in walk(ROOT / base) if p.is_file()]
    for path in targets:
        text = read_text(path)
        if text is None:
            continue
        new = text
        for old_guid, new_guid in mapping.items():
            new = new.replace(old_guid, new_guid)
        if path.name == "ProjectSettings.asset":
            new = PRODUCT_GUID.sub(lambda m: m.group(1) + uuid.uuid4().hex, new)
        if new != text:
            write_text(path, new)

    # 4. README のテンプレート説明を消す
    readme = ROOT / "README.md"
    text = read_text(readme)
    if text is not None:
        write_text(readme, TEMPLATE_SECTION.sub("\n", text))

    print(f"初期化しました: {TOKEN} → {name}")
    print(f"  置換したファイル {replaced} 個 ／ 改名 {renamed} 個 ／ 作り直した GUID {len(mapping)} 個")
    print("次の手順: 変更をコミットして push し、Unity Hub で開く（README.md「新しいプロジェクトを始める手順」）。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
