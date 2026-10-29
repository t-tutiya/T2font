#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// フォントが本文をどこまで覆うべきか（README §7.1）。検査の厳しさを決めるのは利用側で、生成器はこの値で振る舞いを変えない。
    /// </summary>
    public enum FontCoverage
    {
        /// <summary>本文フォント。利用側は本文の全字を覆うことを検査する。</summary>
        Corpus,

        /// <summary>装飾書体。固定の文字セットだけをベイクし、欠けは実行時の代替グリフで見せる。</summary>
        Fixed,
    }

    /// <summary>
    /// フォントのベイク仕様（<c>*.t2font</c>・JSON・README §7）。共通のキー（元フォント・網羅の種類・文字セット）と、表現の種類ごとの
    /// <c>representation</c> の節を持つ。
    ///
    /// <para>
    /// <b>未知のキー・重複したキー・型の違う値は失敗</b>にする——綴りの誤りを既定値で黙って読むと、作者の意図と違うフォントが
    /// 黙ってできる（README §2.1）。表現の種類は必須で、既定の表現を黙って選ばない。
    /// </para>
    /// </summary>
    public sealed class FontBakeSpec
    {
        /// <summary>ベイク仕様ファイルの拡張子（インポーターの主拡張子）。</summary>
        public const string Extension = ".t2font";

        private static readonly string[] KnownKeys = { "source", "coverage", "charsets", "characters", "representation" };
        private static readonly string[] RepresentationReserved = { "kind" };

        public FontBakeSpec(
            string source, FontCoverage coverage, FontCharsetKind charsets, string characters, GlyphRepresentationSettings representation)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Coverage = coverage;
            Charsets = charsets;
            Characters = characters ?? throw new ArgumentNullException(nameof(characters));
            Representation = representation ?? throw new ArgumentNullException(nameof(representation));
        }

        /// <summary>元フォントのプロジェクトパス（<c>Assets/</c> か <c>Packages/</c> で始まる・区切りは <c>/</c>）。</summary>
        public string Source { get; }

        /// <summary>網羅の種類。</summary>
        public FontCoverage Coverage { get; }

        /// <summary>固定の文字セット（元フォントに無い字は上限集合の欠けとして件数だけを報告する）。</summary>
        public FontCharsetKind Charsets { get; }

        /// <summary>指定の追加文字（元フォントに無ければ失敗する）。</summary>
        public string Characters { get; }

        /// <summary>表現の種類と、その種類の設定。</summary>
        public GlyphRepresentationSettings Representation { get; }

        /// <summary>
        /// ベイク仕様を読む。<paramref name="sourceName"/> は誤りの文面に出す名前（ファイルのパス）、<paramref name="kinds"/> は
        /// 使える表現の種類の一覧（README §7.4）。
        /// </summary>
        /// <exception cref="FontSpecException">JSON の構文・未知のキー・型・値の範囲・未知の表現の種類の誤り。</exception>
        public static FontBakeSpec Parse(string text, string sourceName, GlyphRepresentationKinds kinds)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (sourceName == null)
            {
                throw new ArgumentNullException(nameof(sourceName));
            }
            if (kinds == null)
            {
                throw new ArgumentNullException(nameof(kinds));
            }

            var root = new SpecSection(ReadObject(text, sourceName), sourceName, string.Empty, Array.Empty<string>());
            root.RequireKnownKeys(KnownKeys);

            var source = root.RequireString("source");
            if (!(source.StartsWith("Assets/", StringComparison.Ordinal) || source.StartsWith("Packages/", StringComparison.Ordinal))
                || source.IndexOf('\\') >= 0)
            {
                throw root.Fail(root.Json["source"], $"source はプロジェクトパス（Assets/ か Packages/ で始まり、区切りは /）で書いてください: \"{source}\"");
            }

            var coverageText = root.RequireString("coverage");
            FontCoverage coverage;
            switch (coverageText)
            {
                case "corpus":
                    coverage = FontCoverage.Corpus;
                    break;
                case "fixed":
                    coverage = FontCoverage.Fixed;
                    break;
                default:
                    throw root.Fail(root.Json["coverage"], $"coverage は \"corpus\" か \"fixed\" です: \"{coverageText}\"");
            }

            var charsets = FontCharsetKind.None;
            if (root.Json.TryGetValue("charsets", StringComparison.Ordinal, out var charsetsToken))
            {
                if (!(charsetsToken is JArray array))
                {
                    throw root.Fail(charsetsToken, "charsets は文字セットの名前の配列にしてください。");
                }
                foreach (var item in array)
                {
                    var name = item.Type == JTokenType.String ? (string?)item : null;
                    var kind = name == null ? (FontCharsetKind?)null : CharsetByName(name);
                    if (kind == null)
                    {
                        throw root.Fail(item, $"未知の文字セット {item.ToString(Formatting.None)} です（名前は {string.Join("・", KnownCharsets())}）。");
                    }
                    charsets |= kind.Value;
                }
            }

            var characters = root.OptionalString("characters") ?? string.Empty;
            try
            {
                FontCharsets.AddString(new HashSet<int>(), characters);
            }
            catch (ArgumentException e)
            {
                throw root.Fail(root.Json["characters"], "characters に不正な字があります: " + e.Message);
            }

            if (charsets == FontCharsetKind.None && characters.Length == 0)
            {
                throw root.Fail(null, "charsets か characters のどちらかで字を指定してください（字の無いフォントは作れません）。");
            }

            return new FontBakeSpec(source, coverage, charsets, characters, ParseRepresentation(root, kinds));
        }

        /// <summary>固定の文字セットのコードポイント。</summary>
        public SortedSet<int> FixedCodepoints()
        {
            return FontCharsets.Build(Charsets);
        }

        /// <summary>指定の追加文字のコードポイント。</summary>
        public SortedSet<int> NamedCodepoints()
        {
            var set = new SortedSet<int>();
            FontCharsets.AddString(set, Characters);
            return set;
        }

        /// <summary>
        /// 文字セットだけを <paramref name="codepoints"/> に替えたベイク仕様。ほかの値（元フォント・網羅の種類・表現）は写して自己完結にする。
        /// 字は指定の追加文字（<c>characters</c>）として明示し、固定の文字セットは持たない——ベイクする字がベイク仕様の本文だけで決まり、指定文字は
        /// 元フォントに無ければインポートが失敗するので、黙って欠けない。本文から字を決めて再ベイクする利用側の道具が使う。
        /// </summary>
        /// <exception cref="ArgumentException">字が無い・描く字でない（制御文字・BOM・サロゲート）値がある。</exception>
        public FontBakeSpec WithCharacters(IEnumerable<int> codepoints)
        {
            if (codepoints == null)
            {
                throw new ArgumentNullException(nameof(codepoints));
            }

            var text = new StringBuilder();
            foreach (var cp in new SortedSet<int>(codepoints))
            {
                if (cp < 0x20 || cp == 0x7F || cp == 0xFEFF || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
                {
                    throw new ArgumentException(FormattableString.Invariant($"U+{cp:X4} は描く字ではありません。"), nameof(codepoints));
                }
                text.Append(char.ConvertFromUtf32(cp));
            }
            if (text.Length == 0)
            {
                throw new ArgumentException("字が 1 つもありません（字の無いフォントは作れません）。", nameof(codepoints));
            }
            return new FontBakeSpec(Source, Coverage, FontCharsetKind.None, text.ToString(), Representation);
        }

        /// <summary>
        /// ベイク仕様の本文（JSON）。<see cref="Parse"/> で同じ値に読み戻せる。<paramref name="headerLines"/> は先頭に <c>//</c> の
        /// コメントとして書く（無ければ空の列）。キーは省略できる値も含めて全部書く（既定値が変わっても意味が変わらない）。
        /// </summary>
        public string ToJson(IEnumerable<string> headerLines)
        {
            if (headerLines == null)
            {
                throw new ArgumentNullException(nameof(headerLines));
            }

            var text = new StringBuilder();
            foreach (var line in headerLines)
            {
                text.Append("// ").Append(line).Append('\n');
            }

            var items = new List<string>
            {
                "\"source\": " + SpecSection.JsonString(Source),
                "\"coverage\": " + SpecSection.JsonString(Coverage == FontCoverage.Corpus ? "corpus" : "fixed"),
            };
            if (Charsets != FontCharsetKind.None)
            {
                var names = new List<string>();
                foreach (FontCharsetKind kind in Enum.GetValues(typeof(FontCharsetKind)))
                {
                    var bits = (int)kind;
                    if (bits != 0 && (bits & (bits - 1)) == 0 && (Charsets & kind) != 0)
                    {
                        names.Add(SpecSection.JsonString(kind.ToString()));
                    }
                }
                items.Add("\"charsets\": [" + string.Join(", ", names) + "]");
            }
            if (Characters.Length > 0)
            {
                items.Add("\"characters\": " + SpecSection.JsonString(Characters));
            }

            var representation = new List<string> { "\"kind\": " + SpecSection.JsonString(Representation.Kind.Name) };
            foreach (var member in Representation.ToJsonMembers())
            {
                representation.Add(SpecSection.JsonString(member.Key) + ": " + member.Value);
            }
            items.Add("\"representation\": { " + string.Join(", ", representation) + " }");

            text.Append("{\n");
            for (var i = 0; i < items.Count; i++)
            {
                text.Append("    ").Append(items[i]).Append(i + 1 < items.Count ? ",\n" : "\n");
            }
            text.Append("}\n");
            return text.ToString();
        }

        private static GlyphRepresentationSettings ParseRepresentation(SpecSection root, GlyphRepresentationKinds kinds)
        {
            if (!root.Json.TryGetValue("representation", StringComparison.Ordinal, out var token))
            {
                throw root.Fail(null, $"キー \"representation\" がありません（表現の種類を {{ \"kind\": … }} で選んでください。種類は {string.Join("・", kinds.Names)}）。");
            }
            if (!(token is JObject section))
            {
                throw root.Fail(token, "representation は { \"kind\": …, … } のオブジェクトにしてください。");
            }

            var reader = new SpecSection(section, root.SourceName, "representation", RepresentationReserved);
            var kindName = reader.RequireString("kind");
            var kind = kinds.Find(kindName)
                ?? throw reader.Fail(section["kind"], $"未知の表現の種類 \"{kindName}\" です（種類は {string.Join("・", kinds.Names)}）。");
            var settings = kind.ParseSettings(reader);
            if (settings.Kind != kind)
            {
                throw new InvalidOperationException($"表現の種類 \"{kind.Name}\" の ParseSettings が別の種類の設定を返しました。");
            }
            return settings;
        }

        private static JObject ReadObject(string text, string sourceName)
        {
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Double;
                    var settings = new JsonLoadSettings
                    {
                        CommentHandling = CommentHandling.Ignore,
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load,
                    };
                    var token = JToken.ReadFrom(reader, settings);
                    var root = token as JObject ?? throw new FontSpecException($"{sourceName}: 最上位は {{ … }} のオブジェクトにしてください。");
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.Comment)
                        {
                            throw new FontSpecException(FormattableString.Invariant($"{sourceName}: {reader.LineNumber} 行目: オブジェクトの後ろに余計な内容があります。"));
                        }
                    }
                    return root;
                }
            }
            catch (JsonException e)
            {
                throw new FontSpecException($"{sourceName}: JSON として読めません: " + e.Message);
            }
        }

        // 名前は列挙子の綴りそのもの（大文字小文字も区別する。数字や「A, B」の組み合わせの綴りは受けない）。
        private static FontCharsetKind? CharsetByName(string name)
        {
            foreach (FontCharsetKind kind in Enum.GetValues(typeof(FontCharsetKind)))
            {
                if (kind != FontCharsetKind.None && string.Equals(kind.ToString(), name, StringComparison.Ordinal))
                {
                    return kind;
                }
            }
            return null;
        }

        private static IEnumerable<string> KnownCharsets()
        {
            foreach (FontCharsetKind kind in Enum.GetValues(typeof(FontCharsetKind)))
            {
                if (kind != FontCharsetKind.None)
                {
                    yield return kind.ToString();
                }
            }
        }
    }
}
