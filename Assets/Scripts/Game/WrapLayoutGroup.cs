using UnityEngine;
using UnityEngine.UI;

namespace Ginei
{
    /// <summary>
    /// 横に並べ、幅に入らなくなったら<b>次の行へ折り返す</b>簡易レイアウト（#低解像度での可読性）。
    ///
    /// <see cref="HorizontalLayoutGroup"/> は折り返さないので、画面が狭いとボタンが潰れるか枠外へ出る。
    /// 縦長（900x1200）や 1024 幅では上段の主要操作が 6〜8px まで縮んで読めなくなっていた（実機QA）。
    /// ここでは各要素の希望幅（<see cref="LayoutElement.preferredWidth"/>）をそのまま尊重し、
    /// 収まらなくなったところで改行する＝文字を縮めずに全部を触れる状態で並べる。
    ///
    /// 行の高さは要素の希望高さ（無ければ親の高さ）を使う。行数に応じて自分の高さも申告するので、
    /// 親が <see cref="ContentSizeFitter"/> を持てばバーごと縦に伸びる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WrapLayoutGroup : LayoutGroup
    {
        [Tooltip("要素どうしの横の間隔")]
        public float spacing = 4f;
        [Tooltip("行どうしの縦の間隔")]
        public float lineSpacing = 4f;

        private float lastPreferredHeight;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            // 最小幅＝いちばん広い要素（それ以下には縮められない）。
            float widest = 0f;
            for (int i = 0; i < rectChildren.Count; i++)
                widest = Mathf.Max(widest, ChildWidth(rectChildren[i]));
            // 引数は (最小, 最大, 希望, 伸縮, 軸)。最大は無制限＝-1、伸縮は 0（横幅は親に合わせる）。
            float need = widest + padding.horizontal;
            SetLayoutInputForAxis(need, -1f, need, 0f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float h = Arrange(false);
            lastPreferredHeight = h;
            SetLayoutInputForAxis(h, -1f, h, 0f, 1);
        }

        public override void SetLayoutHorizontal() => Arrange(true);
        public override void SetLayoutVertical() => Arrange(true);

        private float ChildWidth(RectTransform c)
        {
            float w = LayoutUtility.GetPreferredWidth(c);
            if (w <= 0f) w = c.rect.width;
            return Mathf.Max(1f, w);
        }

        private float ChildHeight(RectTransform c)
        {
            float h = LayoutUtility.GetPreferredHeight(c);
            if (h <= 0f) h = rectTransform.rect.height - padding.vertical;
            return Mathf.Max(1f, h);
        }

        /// <summary>要素を折り返しながら並べる。<paramref name="apply"/>=false なら必要な高さを測るだけ。</summary>
        private float Arrange(bool apply)
        {
            float avail = rectTransform.rect.width - padding.horizontal;
            if (avail <= 0f) avail = 1f;

            float x = padding.left;
            float y = padding.top;
            float lineH = 0f;
            int lineCount = 1;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform c = rectChildren[i];
                float w = ChildWidth(c);
                float h = ChildHeight(c);

                // 行頭でなく、入らないなら改行。
                if (x > padding.left && (x - padding.left) + w > avail)
                {
                    x = padding.left;
                    y += lineH + lineSpacing;
                    lineH = 0f;
                    lineCount++;
                }

                if (apply)
                {
                    SetChildAlongAxis(c, 0, x, w);
                    SetChildAlongAxis(c, 1, y, h);
                }

                x += w + spacing;
                lineH = Mathf.Max(lineH, h);
            }

            return y + lineH + padding.bottom;
        }

        /// <summary>直近に算出した必要高さ（親が高さを合わせたいときに読む）。</summary>
        public float PreferredHeight => lastPreferredHeight;
    }
}
