#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace info.someiyoshino.T2Font
{
    /// <summary>
    /// 表現が字形の幾何を書き込む器（README §4.2）。ホストが 1 つを持ち回し、字形ごとに <see cref="Clear"/> して使う（割り当て無し）。
    ///
    /// <para>
    /// 位置は em で受け取り、<see cref="CopyTo"/> で表示サイズを掛けて <c>Mesh</c> へ詰める。UV0 は <c>(u, v, emX, emY)</c>——zw は
    /// <b>この器が書く</b>その頂点の em 座標で、シェーダはその微分から 1 表示 px が何 em かを得る（README §5 S3）。表現は zw を
    /// 書けないので、この契約は破れない。
    /// </para>
    /// </summary>
    public sealed class GlyphGeometry
    {
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector3> _scaled = new List<Vector3>();
        private readonly List<Vector4> _uv0 = new List<Vector4>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>書いた頂点の数。</summary>
        public int VertexCount => _positions.Count;

        /// <summary>書いた三角形の頂点番号の数（三角形の数の 3 倍）。</summary>
        public int IndexCount => _triangles.Count;

        /// <summary>書いた頂点の位置（em）。検査用。</summary>
        public IReadOnlyList<Vector3> Positions => _positions;

        /// <summary>書いた頂点の UV0（xy＝表現・zw＝em）。検査用。</summary>
        public IReadOnlyList<Vector4> Uv0 => _uv0;

        /// <summary>空にする。</summary>
        public void Clear()
        {
            _positions.Clear();
            _uv0.Clear();
            _triangles.Clear();
        }

        /// <summary>頂点を 1 つ書き、その番号を返す。</summary>
        public int AddVertex(float emX, float emY, float u, float v)
        {
            _positions.Add(new Vector3(emX, emY, 0f));
            _uv0.Add(new Vector4(u, v, emX, emY));
            return _positions.Count - 1;
        }

        /// <summary>三角形を 1 つ書く（頂点番号は <see cref="AddVertex"/> の返り値）。</summary>
        public void AddTriangle(int a, int b, int c)
        {
            var count = _positions.Count;
            if (a < 0 || a >= count || b < 0 || b >= count || c < 0 || c >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(a), $"三角形の頂点番号（{a}, {b}, {c}）が書いた頂点（{count} 個）の外です。");
            }
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        /// <summary>
        /// 軸に沿った矩形を 1 つ書く（頂点 4 つ・三角形 2 つ）。位置は em、UV は表現の値（左下と右上）。
        /// </summary>
        public void AddQuad(float emLeft, float emBottom, float emRight, float emTop, float uLeft, float vBottom, float uRight, float vTop)
        {
            var leftBottom = AddVertex(emLeft, emBottom, uLeft, vBottom);
            var rightBottom = AddVertex(emRight, emBottom, uRight, vBottom);
            var rightTop = AddVertex(emRight, emTop, uRight, vTop);
            var leftTop = AddVertex(emLeft, emTop, uLeft, vTop);
            AddTriangle(leftBottom, rightTop, rightBottom);
            AddTriangle(leftBottom, leftTop, rightTop);
        }

        /// <summary>
        /// 書いた幾何を <paramref name="mesh"/> へ詰める（位置＝em × <paramref name="fontSize"/>）。<paramref name="mesh"/> は先に
        /// <c>Clear</c> する。uGUI の <c>CanvasRenderer.SetMesh</c> は中身を写すので、詰めた後に呼ぶのはホストの仕事。
        /// </summary>
        public void CopyTo(Mesh mesh, float fontSize)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            _scaled.Clear();
            foreach (var p in _positions)
            {
                _scaled.Add(p * fontSize);
            }
            mesh.Clear();
            // SetUVs と SetTriangles の前に SetVertices が要る（頂点数が決まる）。uGUI のバッチは UV0 の 4 成分を保つ。
            mesh.SetVertices(_scaled);
            mesh.SetUVs(0, _uv0);
            mesh.SetTriangles(_triangles, 0);
        }
    }
}
