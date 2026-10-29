using info.someiyoshino.T2Font;
using UnityEngine;

namespace T2font.EditModeTests
{
    /// <summary>
    /// 描ける効果を選べる、何も描かない表現（マテリアルの置き場とアセットの検査を、表現の具体型に依らずに試すため）。
    /// ScriptableObject なのでクラス名とファイル名を一致させてある。
    /// </summary>
    public sealed class FakeGlyphRepresentation : GlyphRepresentation
    {
        public override string Kind => "fake";

        public static FakeGlyphRepresentation Create(Shader shader, GlyphEffects effects)
        {
            var representation = CreateInstance<FakeGlyphRepresentation>();
            if (shader != null)
            {
                representation.InitializeRepresentation(shader, effects);
            }
            return representation;
        }

        public override Texture GetTexture(int glyph)
        {
            return null;
        }

        public override void WriteGeometry(int glyph, in GlyphMetrics metrics, GlyphGeometry geometry)
        {
        }
    }
}
