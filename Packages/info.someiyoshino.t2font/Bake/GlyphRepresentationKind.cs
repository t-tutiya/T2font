#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace info.someiyoshino.T2Font.Bake
{
    /// <summary>
    /// 表現の種類の記述子（README §7.4）。ベイク仕様の <c>representation.kind</c> の名前・描ける効果・設定の検査を持つ。
    /// UnityEngine を参照しないので、外部 NUnit でベイク仕様とエッジスタイルの照合ができる。表現を足すパッケージは派生を 1 つ置く（README §8）。
    /// </summary>
    public abstract class GlyphRepresentationKind
    {
        /// <summary>ベイク仕様に書く名前（小文字の英数字。例 <c>sdf</c>）。</summary>
        public abstract string Name { get; }

        /// <summary>この種類の表現が描ける効果（README §6.3）。宣言の正典で、ベイカーが表現のアセットへ写す。</summary>
        public abstract GlyphEffects Effects { get; }

        /// <summary>
        /// この種類のベイカーの出力のバージョン。<b>同じ入力から出る出力が変わる変更をしたら上げる</b>（README §7.5）。インポーターはバージョンが変わった
        /// 種類のベイク仕様を再インポートする。
        /// </summary>
        public abstract int Version { get; }

        /// <summary>
        /// <c>representation</c> の節（<c>kind</c> を除くキー）を読んで設定にする。未知のキー・型の違う値・範囲の外の値は
        /// <see cref="FontSpecException"/>（<see cref="SpecSection.Fail"/>）で止める（README §2.1）。
        /// </summary>
        public abstract GlyphRepresentationSettings ParseSettings(SpecSection section);
    }

    /// <summary>表現の種類ごとの設定（<see cref="GlyphRepresentationKind.ParseSettings"/> の結果）。</summary>
    public abstract class GlyphRepresentationSettings
    {
        /// <summary>この設定の種類。</summary>
        public abstract GlyphRepresentationKind Kind { get; }

        /// <summary>
        /// ベイク仕様へ書き戻すときの <c>representation</c> の節のキー（<c>kind</c> を除く）と、JSON の値の綴り。省略できる値も全部書く
        /// （既定値が変わっても意味が変わらない）。<see cref="GlyphRepresentationKind.ParseSettings"/> で同じ値に読み戻せること。
        /// </summary>
        public abstract IEnumerable<KeyValuePair<string, string>> ToJsonMembers();
    }

    /// <summary>
    /// 使う表現の種類の一覧（README §7.4）。利用側のプロジェクトが 1 か所で明示して並べ、ベイク仕様の読み取りとエッジスタイルの照合に渡す。
    /// </summary>
    public sealed class GlyphRepresentationKinds
    {
        private readonly Dictionary<string, GlyphRepresentationKind> _kinds = new Dictionary<string, GlyphRepresentationKind>(StringComparer.Ordinal);

        /// <exception cref="ArgumentException">同じ名前の種類が 2 つある。</exception>
        public GlyphRepresentationKinds(IEnumerable<GlyphRepresentationKind> kinds)
        {
            if (kinds == null)
            {
                throw new ArgumentNullException(nameof(kinds));
            }
            foreach (var kind in kinds)
            {
                if (kind == null)
                {
                    throw new ArgumentException("種類の一覧に null があります。", nameof(kinds));
                }
                if (_kinds.ContainsKey(kind.Name))
                {
                    throw new ArgumentException($"表現の種類 \"{kind.Name}\" が 2 つあります。", nameof(kinds));
                }
                _kinds.Add(kind.Name, kind);
            }
        }

        /// <summary>並べた種類の名前（昇順）。</summary>
        public IEnumerable<string> Names => _kinds.Keys.OrderBy(n => n, StringComparer.Ordinal);

        /// <summary>名前で引く。無ければ null。</summary>
        public GlyphRepresentationKind? Find(string name)
        {
            return _kinds.TryGetValue(name, out var kind) ? kind : null;
        }
    }

    /// <summary>
    /// ベイク仕様の JSON のオブジェクト 1 つを読む補助（誤りの文面にファイル名と行を付ける）。ベイク仕様の最上位と、表現の種類の
    /// <c>representation</c> の節の両方がこれで読む——どちらも「未知のキー・型の違う値は失敗」の規則に揃う（README §7.1）。
    /// </summary>
    public sealed class SpecSection
    {
        /// <param name="reservedKeys">読み手の外で扱うキー（<c>representation</c> の節の <c>kind</c> など）。
        /// <see cref="RequireKnownKeys"/> は既知として扱う。</param>
        public SpecSection(JObject json, string sourceName, string path, IReadOnlyCollection<string> reservedKeys)
        {
            Json = json ?? throw new ArgumentNullException(nameof(json));
            SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            ReservedKeys = reservedKeys ?? throw new ArgumentNullException(nameof(reservedKeys));
        }

        /// <summary>読むオブジェクト。</summary>
        public JObject Json { get; }

        /// <summary>誤りの文面に出すファイル名。</summary>
        public string SourceName { get; }

        /// <summary>誤りの文面に出す節の名前（最上位は空）。</summary>
        public string Path { get; }

        /// <summary>読み手の外で扱うキー。</summary>
        public IReadOnlyCollection<string> ReservedKeys { get; }

        /// <summary>知らないキーがあれば失敗する。<paramref name="known"/> 以外のキーを受けない。</summary>
        public void RequireKnownKeys(params string[] known)
        {
            foreach (var property in Json.Properties())
            {
                if (Array.IndexOf(known, property.Name) < 0 && !ReservedKeys.Contains(property.Name))
                {
                    throw Fail(property, $"未知のキー \"{property.Name}\" です（キーは {string.Join("・", known)}）。");
                }
            }
        }

        /// <summary>文字列の必須のキー。</summary>
        public string RequireString(string key)
        {
            return OptionalString(key) ?? throw Fail(null, $"キー \"{key}\" がありません。");
        }

        /// <summary>文字列のキー。無ければ null。</summary>
        public string? OptionalString(string key)
        {
            if (!Json.TryGetValue(key, StringComparison.Ordinal, out var token))
            {
                return null;
            }
            if (token.Type != JTokenType.String)
            {
                throw Fail(token, $"{key} は文字列にしてください。");
            }
            return (string?)token ?? string.Empty;
        }

        /// <summary>数のキー。無ければ null。</summary>
        public double? OptionalNumber(string key)
        {
            if (!Json.TryGetValue(key, StringComparison.Ordinal, out var token))
            {
                return null;
            }
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                throw Fail(token, $"{key} は数にしてください。");
            }
            return Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// インク境界 <c>[左, 下, 右, 上]</c>（em）のキー。無ければ null。
        /// </summary>
        public EmBox? OptionalBox(string key)
        {
            if (!Json.TryGetValue(key, StringComparison.Ordinal, out var token))
            {
                return null;
            }
            if (!(token is JArray box) || box.Count != 4 || !box.All(t => t.Type == JTokenType.Integer || t.Type == JTokenType.Float))
            {
                throw Fail(token, $"{key} は [左, 下, 右, 上]（em）の 4 つの数にしてください。");
            }
            try
            {
                return new EmBox((double)box[0], (double)box[1], (double)box[2], (double)box[3]);
            }
            catch (ArgumentException e)
            {
                throw Fail(token, e.Message);
            }
        }

        /// <summary>
        /// 誤りを作る。<paramref name="at"/> があれば行番号を、節の名前があればそれを文面に付ける。
        /// </summary>
        public FontSpecException Fail(JToken? at, string message)
        {
            var line = string.Empty;
            if (at is IJsonLineInfo info && info.HasLineInfo())
            {
                line = FormattableString.Invariant($"{info.LineNumber} 行目: ");
            }
            var where = Path.Length > 0 ? Path + ": " : string.Empty;
            return new FontSpecException($"{SourceName}: {line}{where}{message}");
        }

        /// <summary>JSON の数の綴り（往復で同じ値に戻る形）。</summary>
        public static string JsonNumber(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>JSON の文字列の綴り。</summary>
        public static string JsonString(string value)
        {
            return JsonConvert.ToString(value);
        }
    }
}
