#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace info.someiyoshino.T2font.Tests
{
    /// <summary>
    /// API リファレンス（<c>docs/generated/api/</c>）のジェネレーターと golden 検査（D-USERDOCS）。
    ///
    /// <para>
    /// パッケージのソースを Roslyn で<b>構文だけ</b>読む（コンパイルしない）ので、UnityEngine を使うアセンブリ（Runtime・Editor・Viewer）も
    /// Unity なしで読める。型と公開メンバーのシグネチャと XML コメントを Markdown にする。継承できるクラスの <c>protected</c> のメンバーも
    /// 載せる（グリフ表現の作者が派生で使う）。
    /// </para>
    ///
    /// <para>
    /// <b>契約の内と外</b>: 公開型を、利用者（ホスト・ベイク仕様を扱うツール・グリフ表現の作者）が使うことを README が想定する型（内・役割別）と、
    /// ベイカーの部品や検査と計測のための公開（外）に分ける。分類は下の表が持ち、表に無い公開型・パッケージに無い型が表にあると
    /// <see cref="分類表はパッケージの公開型と過不足なく一致する"/> が落ちる——新しい版を写したときに分類を強いる。公開型の中の公開の入れ子の型は、
    /// 外側の型の分類に従う。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ApiReferenceDocTests
    {
        private const string OutputDir = "docs/generated/api";

        private const string Header =
            "<!-- 自動生成（Tests/ApiReferenceDocTests.cs）。手で編集しない。パッケージを写し直したら UPDATE_GENERATED=1 の dotnet test か [regen] 付きの push で作り直す。 -->";

        private const string RoleHost = "ホスト（字を画面へ置く側）";
        private const string RoleViewer = "検証ビュー";
        private const string RoleTool = "ベイク仕様を扱うツール（Editor と外部 NUnit）";
        private const string RoleAuthor = "グリフ表現の作者";
        private const string RoleSdf = "同梱のグリフ表現: SDF";
        private const string RoleRinkaku = "グリフ表現 Rinkaku";

        private static readonly PackageSpec[] Packages =
        {
            new PackageSpec(
                "T2Font",
                "info.someiyoshino.t2font",
                "t2font",
                new[]
                {
                    (RoleHost, new[]
                    {
                        "T2FontAsset", "GlyphRepresentation", "GlyphGeometry", "GlyphMaterialCache", "GlyphEdgeStyle", "GlyphEffects",
                        "GlyphMetrics", "FontFaceMetrics", "TextCodepoints",
                    }),
                    (RoleViewer, new[] { "T2FontText" }),
                    (RoleTool, new[]
                    {
                        "FontBakeSpec", "FontCoverage", "FontCharsetKind", "FontCharsets", "JisX0208Table", "GlyphRepresentationKinds",
                        "T2FontImporter", "TrueTypeFont", "FontSpecBaker", "FontSpecFace", "FontSpecException",
                    }),
                    (RoleAuthor, new[]
                    {
                        "GlyphRepresentationKind", "GlyphRepresentationSettings", "SpecSection", "EmBox", "GlyphRepresentationBaker",
                        "GlyphBakeContext", "GlyphDataPage", "GlyphDataFormat", "FontFaceData", "FontFaceBuilder", "GlyphOutline",
                        "OutlineSegment", "OutlinePoint", "OutlineEdge", "FontBakeException", "GlyphConformance",
                    }),
                    (RoleSdf, new[]
                    {
                        "SdfRepresentation", "SdfRepresentationKind", "SdfSpecSettings", "SdfPageFormat", "SdfEncoding", "SdfGlyphCell",
                        "SdfGlyphBaker",
                    }),
                },
                new[]
                {
                    "GlyphTable", "DistanceTransform", "ScanlineRasterizer", "FontAtlasPage", "SdfAtlasBaker", "SdfAtlas", "SdfBakeSettings",
                }),
            new PackageSpec(
                "Rinkaku",
                "info.someiyoshino.rinkaku",
                "rinkaku",
                new[]
                {
                    (RoleRinkaku, new[]
                    {
                        "RinkakuRepresentation", "RinkakuRepresentationKind", "RinkakuSpecSettings", "RinkakuEncoding", "RinkakuGlyphCell",
                        "RinkakuGlyphBaker",
                    }),
                },
                new[]
                {
                    "RinkakuAtlasBaker", "RinkakuAtlas", "RinkakuBakeSettings", "RinkakuCellOrder", "RinkakuGlyphStats", "RinkakuDecoder",
                }),
        };

        private static readonly Lazy<List<ApiType>> ModelLazy = new(() => BuildModel(RepoRoot.Find()));

        private static IEnumerable<TestCaseData> OutputFiles()
        {
            yield return new TestCaseData("README.md").SetName("ApiReference_README");
            foreach (PackageSpec package in Packages)
            {
                yield return new TestCaseData(package.Slug + ".md").SetName($"ApiReference_{package.Slug}");
                yield return new TestCaseData(package.Slug + "-outside.md").SetName($"ApiReference_{package.Slug}-outside");
            }
            yield return new TestCaseData("undocumented.md").SetName("ApiReference_undocumented");
        }

        [TestCaseSource(nameof(OutputFiles))]
        public void APIリファレンスが生成物と一致する(string fileName)
        {
            GeneratedGolden.AssertUpToDate(Generate(ModelLazy.Value, fileName), $"{OutputDir}/{fileName}",
                "Packages/ のパッケージのソースと、ApiReferenceDocTests の分類表");
        }

        [Test]
        public void 分類表はパッケージの公開型と過不足なく一致する()
        {
            var problems = new List<string>();
            foreach (PackageSpec package in Packages)
            {
                var found = new HashSet<string>(ModelLazy.Value
                    .Where(t => t.Package == package && !t.Name.Contains('.'))
                    .Select(t => t.Name), StringComparer.Ordinal);
                var classified = new HashSet<string>(package.ContractTypes().Concat(package.Outside), StringComparer.Ordinal);
                problems.AddRange(found.Except(classified).OrderBy(n => n, StringComparer.Ordinal)
                    .Select(n => $"{package.Title}: 公開型 {n} が分類表に無い（契約の内なら役割の列へ、外なら外の列へ足す）。"));
                problems.AddRange(classified.Except(found).OrderBy(n => n, StringComparer.Ordinal)
                    .Select(n => $"{package.Title}: 分類表の {n} が、パッケージの公開型に無い（消えたか、公開でなくなった）。"));
                problems.AddRange(package.ContractTypes().Concat(package.Outside).GroupBy(n => n).Where(g => g.Count() > 1)
                    .Select(g => $"{package.Title}: 分類表に {g.Key} が 2 回ある。"));
            }
            Assert.That(problems, Is.Empty,
                "API リファレンスの分類表（Tests/ApiReferenceDocTests.cs）とパッケージの公開型が食い違っています（D-USERDOCS）:\n  " +
                string.Join("\n  ", problems));
        }

        // ── 生成 ─────────────────────────────────────────────────────────────

        private static string Generate(List<ApiType> model, string fileName)
        {
            if (fileName == "README.md")
            {
                return GenerateIndex(model);
            }
            if (fileName == "undocumented.md")
            {
                return GenerateUndocumented(model);
            }
            foreach (PackageSpec package in Packages)
            {
                if (fileName == package.Slug + ".md")
                {
                    return GenerateContract(model, package);
                }
                if (fileName == package.Slug + "-outside.md")
                {
                    return GenerateOutside(model, package);
                }
            }
            throw new ArgumentException($"未知の出力ファイル {fileName}", nameof(fileName));
        }

        private static string GenerateIndex(List<ApiType> model)
        {
            var sb = new StringBuilder();
            sb.AppendLineLf(Header);
            sb.AppendLineLf();
            sb.AppendLineLf("# API リファレンス");
            sb.AppendLineLf();
            sb.AppendLineLf("T2Font と Rinkaku の公開型の一覧。パッケージのソース（XML コメント）から生成している。使い方の手順は [利用の手引き](../../manual/README.md)、");
            sb.AppendLineLf("契約の正典は各パッケージの README（文中の「README §n」は、その型のパッケージの README の節）。");
            sb.AppendLineLf();
            sb.AppendLineLf("- **契約の内**: 利用者——ホスト・ベイク仕様を扱うツール・グリフ表現の作者——が使うことを README が想定している型。役割別に並べる。");
            sb.AppendLineLf("  互換を壊す変更は各パッケージの CHANGELOG に書かれる。");
            sb.AppendLineLf("- **契約の外**: 公開されているが、ベイカーの部品や、検査と計測のための型。版の間で予告なく変わりうるので、利用側のコードから使わない。");
            sb.AppendLineLf();
            sb.AppendLineLf("型ごとの分類は、ジェネレーター（`Tests/ApiReferenceDocTests.cs`）の表が持つ（D-USERDOCS）。");
            sb.AppendLineLf();
            sb.AppendLineLf("| ページ | 内容 |");
            sb.AppendLineLf("|---|---|");
            foreach (PackageSpec package in Packages)
            {
                sb.AppendLineLf($"| [`{package.Slug}.md`]({package.Slug}.md) | {package.Title} の契約の内（役割別） |");
                sb.AppendLineLf($"| [`{package.Slug}-outside.md`]({package.Slug}-outside.md) | {package.Title} の契約の外 |");
            }
            sb.AppendLineLf("| [`undocumented.md`](undocumented.md) | XML コメントの無い公開メンバー（パッケージの正本の開発環境へ返す一覧） |");

            foreach (PackageSpec package in Packages)
            {
                sb.AppendLineLf();
                sb.AppendLineLf($"## {package.Title} — 契約の内");
                foreach ((string role, string[] names) in package.Contract)
                {
                    sb.AppendLineLf();
                    sb.AppendLineLf($"### {role}");
                    sb.AppendLineLf();
                    sb.AppendLineLf("| 型 | 概要 |");
                    sb.AppendLineLf("|---|---|");
                    foreach (string name in names)
                    {
                        ApiType type = Find(model, package, name);
                        sb.AppendLineLf($"| [`{type.Name}`]({package.Slug}.md#{Slug(type.Name)}) | {TableCell(FirstSentence(type.Summary))} |");
                    }
                }
                sb.AppendLineLf();
                sb.AppendLineLf($"## {package.Title} — 契約の外");
                sb.AppendLineLf();
                sb.AppendLineLf("| 型 | 概要 |");
                sb.AppendLineLf("|---|---|");
                foreach (string name in package.Outside)
                {
                    ApiType type = Find(model, package, name);
                    sb.AppendLineLf($"| [`{type.Name}`]({package.Slug}-outside.md#{Slug(type.Name)}) | {TableCell(FirstSentence(type.Summary))} |");
                }
            }
            return sb.ToString();
        }

        private static string GenerateContract(List<ApiType> model, PackageSpec package)
        {
            var sb = new StringBuilder();
            sb.AppendLineLf(Header);
            sb.AppendLineLf();
            sb.AppendLineLf($"# {package.Title} の API — 契約の内");
            sb.AppendLineLf();
            sb.AppendLineLf($"利用者が使うことを README が想定している公開型（役割別）。索引と「契約の内と外」の意味は [`README.md`](README.md)。");
            sb.AppendLineLf($"文中の「README §n」は [{package.Title} の README](../../../Packages/{package.Folder}/README.md) の節。");
            foreach ((string role, string[] names) in package.Contract)
            {
                sb.AppendLineLf();
                sb.AppendLineLf($"## {role}");
                foreach (string name in names)
                {
                    AppendTypeWithNested(sb, model, package, Find(model, package, name));
                }
            }
            return sb.ToString();
        }

        private static string GenerateOutside(List<ApiType> model, PackageSpec package)
        {
            var sb = new StringBuilder();
            sb.AppendLineLf(Header);
            sb.AppendLineLf();
            sb.AppendLineLf($"# {package.Title} の API — 契約の外");
            sb.AppendLineLf();
            sb.AppendLineLf("公開されているが、ベイカーの部品や、検査と計測のための型。**版の間で予告なく変わりうるので、利用側のコードから使わない**。");
            sb.AppendLineLf($"索引は [`README.md`](README.md)。文中の「README §n」は [{package.Title} の README](../../../Packages/{package.Folder}/README.md) の節。");
            sb.AppendLineLf();
            sb.AppendLineLf("## 型");
            foreach (string name in package.Outside)
            {
                AppendTypeWithNested(sb, model, package, Find(model, package, name));
            }
            return sb.ToString();
        }

        private static string GenerateUndocumented(List<ApiType> model)
        {
            var sb = new StringBuilder();
            sb.AppendLineLf(Header);
            sb.AppendLineLf();
            sb.AppendLineLf("# XML コメントの無い公開メンバー");
            sb.AppendLineLf();
            sb.AppendLineLf("API リファレンスに説明の出ない型とメンバー（基底の上書きと演算子を除く）。パッケージの中は本リポジトリで直さない（D-T2FREPUB）ので、");
            sb.AppendLineLf("この一覧をパッケージの正本の開発環境へ返し、次の版で `<summary>` を足してもらう。");
            foreach (PackageSpec package in Packages)
            {
                sb.AppendLineLf();
                sb.AppendLineLf($"## {package.Title}");
                sb.AppendLineLf();
                var lines = new List<string>();
                foreach (ApiType type in OrderedTypes(model, package))
                {
                    if (type.Summary.Length == 0)
                    {
                        lines.Add($"- 型 `{type.Name}`（`{type.SourcePath}`）");
                    }
                    foreach (ApiMember member in type.Members.Where(m => m.Summary.Length == 0 && !m.Conventional))
                    {
                        lines.Add($"- `{type.Name}` の `{member.Title}`");
                    }
                }
                if (lines.Count == 0)
                {
                    sb.AppendLineLf("無し。");
                }
                foreach (string line in lines)
                {
                    sb.AppendLineLf(line);
                }
            }
            return sb.ToString();
        }

        /// <summary>分類表の順（内の役割順 → 外）に、入れ子の型を外側の直後に置いて並べる。</summary>
        private static IEnumerable<ApiType> OrderedTypes(List<ApiType> model, PackageSpec package)
        {
            foreach (string name in package.ContractTypes().Concat(package.Outside))
            {
                ApiType type = Find(model, package, name);
                yield return type;
                foreach (ApiType nested in NestedOf(model, type))
                {
                    yield return nested;
                }
            }
        }

        private static void AppendTypeWithNested(StringBuilder sb, List<ApiType> model, PackageSpec package, ApiType type)
        {
            AppendType(sb, package, type);
            foreach (ApiType nested in NestedOf(model, type))
            {
                AppendType(sb, package, nested);
            }
        }

        private static IEnumerable<ApiType> NestedOf(List<ApiType> model, ApiType outer)
        {
            return model.Where(t => t.Package == outer.Package && t.Name.StartsWith(outer.Name + ".", StringComparison.Ordinal));
        }

        private static void AppendType(StringBuilder sb, PackageSpec package, ApiType type)
        {
            sb.AppendLineLf();
            sb.AppendLineLf($"### {type.Name}");
            sb.AppendLineLf();
            sb.AppendLineLf($"名前空間 `{type.Namespace}`・アセンブリ `{type.Assembly}`・ソース [`{type.SourcePath}`](../../../{type.SourcePath})");
            sb.AppendLineLf();
            sb.AppendLineLf("```csharp");
            sb.AppendLineLf(type.Signature);
            sb.AppendLineLf("```");
            if (type.Summary.Length > 0)
            {
                sb.AppendLineLf();
                sb.AppendLineLf(type.Summary);
            }

            if (type.IsEnum)
            {
                if (type.Members.Count > 0)
                {
                    sb.AppendLineLf();
                    foreach (ApiMember member in type.Members)
                    {
                        string text = member.Summary.Length > 0 ? " — " + Inline(member.Summary) : string.Empty;
                        sb.AppendLineLf($"- `{member.Signature}`{text}");
                    }
                }
                return;
            }

            foreach (ApiMember member in type.Members)
            {
                sb.AppendLineLf();
                sb.AppendLineLf($"#### `{member.Title}`");
                sb.AppendLineLf();
                sb.AppendLineLf("```csharp");
                sb.AppendLineLf(member.Signature);
                sb.AppendLineLf("```");
                if (member.Summary.Length > 0)
                {
                    sb.AppendLineLf();
                    sb.AppendLineLf(member.Summary);
                }
                if (member.Details.Count > 0)
                {
                    sb.AppendLineLf();
                    foreach (string detail in member.Details)
                    {
                        sb.AppendLineLf("- " + detail);
                    }
                }
            }
        }

        private static ApiType Find(List<ApiType> model, PackageSpec package, string name)
        {
            ApiType? type = model.FirstOrDefault(t => t.Package == package && t.Name == name);
            if (type == null)
            {
                Assert.Fail($"{package.Title}: 分類表の型 {name} がパッケージの公開型に無い（分類表の検査を見る）。");
            }
            return type!;
        }

        // ── 読み取り ─────────────────────────────────────────────────────────

        private static List<ApiType> BuildModel(string root)
        {
            var options = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Regular, new[] { "UNITY_EDITOR" });
            var model = new List<ApiType>();
            foreach (PackageSpec package in Packages)
            {
                string packageRoot = Path.Combine(root, "Packages", package.Folder);
                IEnumerable<string> files = Directory.EnumerateFiles(packageRoot, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.Ordinal);
                foreach (string path in files)
                {
                    string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                    string assembly = AssemblyOf(path, packageRoot);
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path);
                    foreach (BaseTypeDeclarationSyntax declaration in tree.GetCompilationUnitRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                    {
                        if (!IsEffectivelyPublic(declaration))
                        {
                            continue;
                        }
                        model.Add(ReadType(package, assembly, relative, declaration));
                    }
                }
            }
            return model;
        }

        /// <summary>ファイルから親へ辿って、最初に見つかる asmdef の名前。</summary>
        private static string AssemblyOf(string path, string packageRoot)
        {
            string? directory = Path.GetDirectoryName(path);
            while (directory != null && directory.Length >= packageRoot.Length)
            {
                string? asmdef = Directory.GetFiles(directory, "*.asmdef").FirstOrDefault();
                if (asmdef != null)
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(asmdef));
                    return doc.RootElement.GetProperty("name").GetString()!;
                }
                directory = Path.GetDirectoryName(directory);
            }
            Assert.Fail($"{path} を含む asmdef が見つかりません。");
            return string.Empty;
        }

        private static bool IsEffectivelyPublic(BaseTypeDeclarationSyntax declaration)
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return false;
            }
            return declaration.Ancestors().OfType<BaseTypeDeclarationSyntax>().All(outer => outer.Modifiers.Any(SyntaxKind.PublicKeyword));
        }

        private static ApiType ReadType(PackageSpec package, string assembly, string relative, BaseTypeDeclarationSyntax declaration)
        {
            IEnumerable<string> outers = declaration.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.Text);
            string name = string.Join(".", outers.Append(declaration.Identifier.Text));
            string ns = string.Join(".", declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));

            var type = new ApiType(package, assembly, ns, name, relative, TypeSignature(declaration), declaration is EnumDeclarationSyntax);
            XElement? doc = DocOf(declaration);
            type.Summary = doc == null ? string.Empty : Block(doc.Element("summary"), doc.Element("remarks"));

            if (declaration is EnumDeclarationSyntax enumeration)
            {
                foreach (EnumMemberDeclarationSyntax member in enumeration.Members)
                {
                    string signature = member.EqualsValue == null ? member.Identifier.Text : Normalize($"{member.Identifier.Text} {member.EqualsValue}");
                    XElement? memberDoc = DocOf(member);
                    type.Members.Add(new ApiMember(member.Identifier.Text, signature,
                        memberDoc == null ? string.Empty : Block(memberDoc.Element("summary"), null), new List<string>(), false));
                }
                return type;
            }

            var owner = (TypeDeclarationSyntax)declaration;
            foreach (MemberDeclarationSyntax member in owner.Members)
            {
                if (member is BaseTypeDeclarationSyntax || !IsVisible(member, owner))
                {
                    continue;
                }
                (string Title, string Signature)? head = MemberHead(member);
                if (head == null)
                {
                    continue;
                }
                XElement? memberDoc = DocOf(member);
                string summary = memberDoc == null ? string.Empty : Block(memberDoc.Element("summary"), memberDoc.Element("remarks"));
                bool conventional = member.Modifiers.Any(SyntaxKind.OverrideKeyword)
                    || member is OperatorDeclarationSyntax || member is ConversionOperatorDeclarationSyntax;
                if (summary.Length == 0 && member.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    summary = "基底の型の同じメンバーを上書きする（説明は基底の型のメンバーを見る）。";
                }
                type.Members.Add(new ApiMember(head.Value.Title, head.Value.Signature, summary, Details(memberDoc), conventional && memberDoc == null));
            }
            return type;
        }

        private static bool IsVisible(MemberDeclarationSyntax member, TypeDeclarationSyntax owner)
        {
            if (owner is InterfaceDeclarationSyntax)
            {
                return true;
            }
            SyntaxTokenList modifiers = member.Modifiers;
            if (modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return true;
            }
            bool extensible = owner is ClassDeclarationSyntax
                && !owner.Modifiers.Any(SyntaxKind.SealedKeyword)
                && !owner.Modifiers.Any(SyntaxKind.StaticKeyword);
            return extensible && modifiers.Any(SyntaxKind.ProtectedKeyword) && !modifiers.Any(SyntaxKind.PrivateKeyword);
        }

        private static string TypeSignature(BaseTypeDeclarationSyntax declaration)
        {
            string modifiers = string.Join(" ", declaration.Modifiers.Select(m => m.Text).Where(m => m != "partial"));
            string keyword;
            string typeParameters = string.Empty;
            string constraints = string.Empty;
            switch (declaration)
            {
                case RecordDeclarationSyntax record:
                    keyword = record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
                    typeParameters = record.TypeParameterList?.ToString() ?? string.Empty;
                    constraints = string.Join(" ", record.ConstraintClauses.Select(c => c.ToString()));
                    break;
                case TypeDeclarationSyntax typeDeclaration:
                    keyword = typeDeclaration.Keyword.Text;
                    typeParameters = typeDeclaration.TypeParameterList?.ToString() ?? string.Empty;
                    constraints = string.Join(" ", typeDeclaration.ConstraintClauses.Select(c => c.ToString()));
                    break;
                default:
                    keyword = "enum";
                    break;
            }
            string bases = declaration.BaseList == null ? string.Empty : " " + declaration.BaseList;
            return Normalize($"{modifiers} {keyword} {declaration.Identifier.Text}{typeParameters}{bases} {constraints}");
        }

        /// <summary>メンバーの見出し（名前）とシグネチャ（本体を除いた宣言）。載せない種類は null。</summary>
        private static (string Title, string Signature)? MemberHead(MemberDeclarationSyntax member)
        {
            string modifiers = string.Join(" ", member.Modifiers.Select(m => m.Text));
            switch (member)
            {
                case ConstructorDeclarationSyntax constructor:
                    if (constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        return null;
                    }
                    return (constructor.Identifier.Text + "（構築子）", Normalize($"{modifiers} {constructor.Identifier.Text}{constructor.ParameterList}"));
                case MethodDeclarationSyntax method:
                    if (method.ExplicitInterfaceSpecifier != null)
                    {
                        return null;
                    }
                    return (method.Identifier.Text + "()", Normalize(
                        $"{modifiers} {method.ReturnType} {method.Identifier.Text}{method.TypeParameterList}{method.ParameterList} {string.Join(" ", method.ConstraintClauses)}"));
                case PropertyDeclarationSyntax property:
                    if (property.ExplicitInterfaceSpecifier != null)
                    {
                        return null;
                    }
                    return (property.Identifier.Text, Normalize($"{modifiers} {property.Type} {property.Identifier.Text} {Accessors(property.AccessorList)}"));
                case IndexerDeclarationSyntax indexer:
                    return ("this[]", Normalize($"{modifiers} {indexer.Type} this{indexer.ParameterList} {Accessors(indexer.AccessorList)}"));
                case FieldDeclarationSyntax field:
                    {
                        bool isConst = field.Modifiers.Any(SyntaxKind.ConstKeyword);
                        IEnumerable<string> declarators = field.Declaration.Variables
                            .Select(v => isConst && v.Initializer != null ? $"{v.Identifier.Text} {v.Initializer}" : v.Identifier.Text);
                        string title = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.Text));
                        return (title, Normalize($"{modifiers} {field.Declaration.Type} {string.Join(", ", declarators)}"));
                    }
                case EventFieldDeclarationSyntax eventField:
                    {
                        string names = string.Join(", ", eventField.Declaration.Variables.Select(v => v.Identifier.Text));
                        return (names, Normalize($"{modifiers} event {eventField.Declaration.Type} {names}"));
                    }
                case EventDeclarationSyntax eventDeclaration:
                    return (eventDeclaration.Identifier.Text, Normalize($"{modifiers} event {eventDeclaration.Type} {eventDeclaration.Identifier.Text}"));
                case OperatorDeclarationSyntax op:
                    return ("operator " + op.OperatorToken.Text, Normalize($"{modifiers} {op.ReturnType} operator {op.OperatorToken.Text}{op.ParameterList}"));
                case ConversionOperatorDeclarationSyntax conversion:
                    return ($"{conversion.ImplicitOrExplicitKeyword.Text} operator {conversion.Type}", Normalize(
                        $"{modifiers} {conversion.ImplicitOrExplicitKeyword.Text} operator {conversion.Type}{conversion.ParameterList}"));
                default:
                    return null;
            }
        }

        private static string Accessors(AccessorListSyntax? list)
        {
            if (list == null)
            {
                return "{ get; }";
            }
            IEnumerable<string> visible = list.Accessors
                .Where(a => !a.Modifiers.Any(SyntaxKind.PrivateKeyword)
                    && !(a.Modifiers.Any(SyntaxKind.InternalKeyword) && !a.Modifiers.Any(SyntaxKind.ProtectedKeyword)))
                .Select(a => (a.Modifiers.Count > 0 ? string.Join(" ", a.Modifiers.Select(m => m.Text)) + " " : string.Empty) + a.Keyword.Text + ";");
            return "{ " + string.Join(" ", visible) + " }";
        }

        // ── XML コメント → Markdown ─────────────────────────────────────────

        private const char ParagraphMark = '\u0002';
        private const char ItemMark = '\u0003';
        private const char CodeLine = '\u0004';

        private static readonly Regex SourceBreak = new(@"[ \t]*\n[ \t]*", RegexOptions.Compiled);

        /// <summary>宣言の直前の <c>///</c> のコメントを XML として読む。無いか、XML として読めなければ null。</summary>
        private static XElement? DocOf(SyntaxNode node)
        {
            List<SyntaxTrivia> comments = node.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                .ToList();
            if (comments.Count == 0)
            {
                return null;
            }
            IEnumerable<string> lines = string.Concat(comments.Select(c => c.ToFullString()))
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line =>
                {
                    string trimmed = line.TrimStart();
                    return trimmed.StartsWith("///", StringComparison.Ordinal) ? trimmed.Substring(3) : trimmed;
                });
            try
            {
                return XElement.Parse("<doc>" + string.Join("\n", lines) + "</doc>", LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        /// <summary>段落を空行で分けた Markdown（型とメンバーの説明）。</summary>
        private static string Block(XElement? summary, XElement? remarks)
        {
            var sb = new StringBuilder();
            if (summary != null)
            {
                AppendChildren(sb, summary);
            }
            if (remarks != null)
            {
                sb.Append(ParagraphMark);
                AppendChildren(sb, remarks);
            }
            return Finish(sb.ToString());
        }

        /// <summary>1 行に収めた Markdown（表のセル・箇条の中）。</summary>
        private static string Inline(string block)
        {
            return Regex.Replace(block, @"\n+(- )?", m => m.Groups[1].Success ? " ・" : " ").Trim();
        }

        private static List<string> Details(XElement? doc)
        {
            var details = new List<string>();
            if (doc == null)
            {
                return details;
            }
            foreach (XElement param in doc.Elements("typeparam").Concat(doc.Elements("param")))
            {
                details.Add($"引数 `{(string?)param.Attribute("name")}`: {Inline(Block(param, null))}");
            }
            XElement? value = doc.Element("value");
            if (value != null)
            {
                details.Add($"値: {Inline(Block(value, null))}");
            }
            XElement? returns = doc.Element("returns");
            if (returns != null)
            {
                details.Add($"返り値: {Inline(Block(returns, null))}");
            }
            foreach (XElement exception in doc.Elements("exception"))
            {
                details.Add($"例外 `{Cref((string?)exception.Attribute("cref") ?? string.Empty)}`: {Inline(Block(exception, null))}");
            }
            return details;
        }

        private static void AppendChildren(StringBuilder sb, XElement element)
        {
            foreach (XNode node in element.Nodes())
            {
                AppendNode(sb, node);
            }
        }

        private static void AppendNode(StringBuilder sb, XNode node)
        {
            if (node is XText text)
            {
                sb.Append(EscapeText(text.Value));
                return;
            }
            if (node is not XElement element)
            {
                return;
            }
            switch (element.Name.LocalName)
            {
                case "c":
                    sb.Append(CodeSpan(element.Value));
                    break;
                case "code":
                    sb.Append(ParagraphMark).Append("```csharp").Append(CodeLine)
                        .Append(element.Value.Trim('\n').Replace('\n', CodeLine)).Append(CodeLine).Append("```").Append(ParagraphMark);
                    break;
                case "see":
                case "seealso":
                    {
                        string? cref = (string?)element.Attribute("cref");
                        string? langword = (string?)element.Attribute("langword");
                        if (cref != null)
                        {
                            sb.Append(CodeSpan(Cref(cref)));
                        }
                        else if (langword != null)
                        {
                            sb.Append(CodeSpan(langword));
                        }
                        else
                        {
                            AppendChildren(sb, element);
                        }
                        break;
                    }
                case "paramref":
                case "typeparamref":
                    sb.Append(CodeSpan((string?)element.Attribute("name") ?? string.Empty));
                    break;
                case "b":
                case "strong":
                    sb.Append("**");
                    AppendChildren(sb, element);
                    sb.Append("**");
                    break;
                case "i":
                case "em":
                    sb.Append('*');
                    AppendChildren(sb, element);
                    sb.Append('*');
                    break;
                case "para":
                case "br":
                    sb.Append(ParagraphMark);
                    AppendChildren(sb, element);
                    sb.Append(ParagraphMark);
                    break;
                case "list":
                    sb.Append(ParagraphMark);
                    foreach (XElement item in element.Elements("item"))
                    {
                        sb.Append(ItemMark);
                        AppendChildren(sb, item);
                    }
                    sb.Append(ParagraphMark);
                    break;
                default:
                    AppendChildren(sb, element);
                    break;
            }
        }

        /// <summary>
        /// ソースの改行を詰め（片側が ASCII なら空白 1 つ・両側が和文か、和文の句読点と括弧に接するなら何も入れない）、段落と箇条の印を Markdown に直す。
        /// コードの区画（<c>&lt;code&gt;</c>）の改行と字下げは保つ。
        /// </summary>
        private static string Finish(string text)
        {
            string joined = SourceBreak.Replace(text, m =>
            {
                char before = m.Index > 0 ? text[m.Index - 1] : ' ';
                int afterIndex = m.Index + m.Length;
                char after = afterIndex < text.Length ? text[afterIndex] : ' ';
                bool asciiSide = before < 0x80 || after < 0x80;
                return asciiSide && !IsJapanesePunctuation(before) && !IsJapanesePunctuation(after) ? " " : string.Empty;
            });

            var paragraphs = new List<string>();
            foreach (string raw in joined.Split(ParagraphMark))
            {
                if (raw.Contains(CodeLine))
                {
                    paragraphs.Add(raw.Trim().Replace(CodeLine, '\n'));
                    continue;
                }
                string[] pieces = Regex.Replace(raw, @"[ ]{2,}", " ").Split(ItemMark);
                var lines = new List<string>();
                string lead = pieces[0].Trim();
                if (lead.Length > 0)
                {
                    lines.Add(lead);
                }
                foreach (string piece in pieces.Skip(1))
                {
                    string item = piece.Trim();
                    if (item.Length > 0)
                    {
                        lines.Add("- " + item);
                    }
                }
                if (lines.Count > 0)
                {
                    paragraphs.Add(string.Join("\n", lines));
                }
            }
            return string.Join("\n\n", paragraphs);
        }

        /// <summary>和文の句読点と括弧（隣に空白を入れない字）。</summary>
        private static bool IsJapanesePunctuation(char c)
        {
            return (c >= '\u3000' && c <= '\u303F') || (c >= '\uFF01' && c <= '\uFF0F') || (c >= '\uFF1A' && c <= '\uFF20')
                || c == '・' || c == '—' || c == '…' || c == '＝';
        }

        private static string EscapeText(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("*", "\\*");
        }

        private static string CodeSpan(string code)
        {
            string flat = Regex.Replace(code, @"\s+", " ").Trim();
            return flat.Contains('`') ? $"`` {flat} ``" : $"`{flat}`";
        }

        private static string Cref(string cref)
        {
            return cref.Length > 2 && cref[1] == ':' ? cref.Substring(2) : cref;
        }

        private static string FirstSentence(string summary)
        {
            string inline = Inline(summary);
            int end = inline.IndexOf('。');
            return end < 0 ? inline : inline.Substring(0, end + 1);
        }

        private static string TableCell(string text)
        {
            return text.Length == 0 ? "—" : text.Replace("|", "\\|");
        }

        private static string Normalize(string text)
        {
            string flat = Regex.Replace(text, @"\s+", " ").Trim();
            return flat.Replace("( ", "(").Replace(" )", ")").Replace("[ ", "[").Replace(" ]", "]");
        }

        /// <summary>GitHub の見出しのアンカー（英数字の型名だけを使う）。</summary>
        private static string Slug(string heading)
        {
            return Regex.Replace(heading.ToLowerInvariant(), @"[^a-z0-9_-]", string.Empty);
        }

        // ── 型 ───────────────────────────────────────────────────────────────

        private sealed class PackageSpec
        {
            public PackageSpec(string title, string folder, string slug, (string Role, string[] Names)[] contract, string[] outside)
            {
                Title = title;
                Folder = folder;
                Slug = slug;
                Contract = contract;
                Outside = outside;
            }

            public string Title { get; }

            public string Folder { get; }

            public string Slug { get; }

            public (string Role, string[] Names)[] Contract { get; }

            public string[] Outside { get; }

            public IEnumerable<string> ContractTypes()
            {
                return Contract.SelectMany(c => c.Names);
            }
        }

        private sealed class ApiType
        {
            public ApiType(PackageSpec package, string assembly, string ns, string name, string sourcePath, string signature, bool isEnum)
            {
                Package = package;
                Assembly = assembly;
                Namespace = ns;
                Name = name;
                SourcePath = sourcePath;
                Signature = signature;
                IsEnum = isEnum;
            }

            public PackageSpec Package { get; }

            public string Assembly { get; }

            public string Namespace { get; }

            public string Name { get; }

            public string SourcePath { get; }

            public string Signature { get; }

            public bool IsEnum { get; }

            public string Summary { get; set; } = string.Empty;

            public List<ApiMember> Members { get; } = new List<ApiMember>();
        }

        private sealed class ApiMember
        {
            public ApiMember(string title, string signature, string summary, List<string> details, bool conventional)
            {
                Title = title;
                Signature = signature;
                Summary = summary;
                Details = details;
                Conventional = conventional;
            }

            /// <summary>説明が無くても意味の決まっているメンバー（基底の上書き・演算子）。XML コメントの無い一覧に載せない。</summary>
            public bool Conventional { get; }

            public string Title { get; }

            public string Signature { get; }

            public string Summary { get; }

            public List<string> Details { get; }
        }
    }
}
