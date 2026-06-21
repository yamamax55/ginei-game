using UnityEngine;
using UnityEngine.UI;

namespace Ginei
{
    /// <summary>
    /// 階級ピラミッドの1帯を<b>台形</b>として描く uGUI Graphic（#階級章ピラミッド・コード生成ベクター）。
    /// 上辺幅(<see cref="topWidth"/>)＜下辺幅(<see cref="bottomWidth"/>)で、隣り合う帯どうしの辺が一致するよう
    /// 値を与えると、積み上げ全体が<b>滑らかな三角形（ピラミッド）</b>のシルエットになる（階段状にならない）。
    /// 単色塗り（色は <see cref="Graphic.color"/>）。クリック判定は矩形（Graphic 既定）で十分。
    /// 静的SVG画像と違い、人数・現在地ハイライトを動的に変えられる runtime ベクター。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PyramidBand : MaskableGraphic
    {
        [Tooltip("帯の上辺の幅(px)")]
        public float topWidth = 100f;
        [Tooltip("帯の下辺の幅(px)")]
        public float bottomWidth = 120f;

        /// <summary>上辺・下辺の幅をまとめて設定して再描画。</summary>
        public void SetTrapezoid(float top, float bottom)
        {
            topWidth = top;
            bottomWidth = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float cx = (r.xMin + r.xMax) * 0.5f;
            float yT = r.yMax, yB = r.yMin;
            float tw = topWidth * 0.5f;
            float bw = bottomWidth * 0.5f;
            Color32 c = color;

            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = new Vector3(cx - bw, yB); vh.AddVert(v); // 0 下-左
            v.position = new Vector3(cx + bw, yB); vh.AddVert(v); // 1 下-右
            v.position = new Vector3(cx + tw, yT); vh.AddVert(v); // 2 上-右
            v.position = new Vector3(cx - tw, yT); vh.AddVert(v); // 3 上-左
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
