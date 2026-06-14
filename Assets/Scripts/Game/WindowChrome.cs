using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 観測オーバーレイ等の「画面」を Windows 風ウィンドウへ統一する共通クローム（#上メニューのウィンドウ化）。
    /// タイトルバー（つかんでドラッグ移動＝<see cref="UIDragMove"/>）＋×閉じるボタンを枠の先頭に差し込み、
    /// 背景ディマーを非モーダル化（クリック透過）して盤面を塞がない＝<see cref="SystemMapWindow"/>/
    /// <see cref="SystemDetailPanel"/> と同じ意匠。各オーバーレイはこの窓口を呼ぶだけ（タイトルバーを二重実装しない）。
    /// </summary>
    public static class WindowChrome
    {
        /// <summary>タイトルバーの高さ（px）。</summary>
        public const float TitleBarHeight = 32f;

        private static readonly Color BarColor = new Color(0.13f, 0.18f, 0.26f, 1f);
        private static readonly Color CaptionColor = new Color(1f, 0.84f, 0.36f);

        /// <summary>
        /// <see cref="VerticalLayoutGroup"/> を持つ枠（frame）の先頭にタイトルバーを差し込む（VLG が上段へ並べる）。
        /// 残りの本文は既存の伸縮子（flexibleHeight=1 のスクロール等）がそのまま埋める。
        /// </summary>
        public static void AddTitleBarLayout(RectTransform frameRT, string caption, UnityAction onClose)
            => AddTitleBarLayout(frameRT, caption, onClose, null);

        /// <summary>
        /// 最小化ボタン付きのタイトルバーを差し込む。<paramref name="onMinimize"/> は折り畳み状態（true=最小化中）を受け取る。
        /// ボタンの字形（－/□）は本クロームが自動でトグルする＝呼び出し側は本文の表示/サイズ調整だけ行う。
        /// </summary>
        public static void AddTitleBarLayout(RectTransform frameRT, string caption, UnityAction onClose, UnityAction<bool> onMinimize)
        {
            if (frameRT == null) return;
            GameObject bar = BuildBar(frameRT, caption, onClose, out _, onMinimize);
            bar.transform.SetSiblingIndex(0); // VLG の先頭＝最上段
            LayoutElement le = bar.AddComponent<LayoutElement>();
            le.minHeight = TitleBarHeight; le.preferredHeight = TitleBarHeight; le.flexibleHeight = 0f;
        }

        /// <summary>
        /// VLG を持たない枠（panel）の上端へタイトルバーをアンカー配置する（呼び出し側で本文を <see cref="TitleBarHeight"/>
        /// ぶん下げてかぶりを避ける）。ドラッグ対象は <paramref name="windowRT"/>。
        /// </summary>
        public static void AddTitleBarAnchored(RectTransform windowRT, string caption, UnityAction onClose)
        {
            if (windowRT == null) return;
            GameObject bar = BuildBar(windowRT, caption, onClose, out RectTransform barRT);
            barRT.anchorMin = new Vector2(0f, 1f);
            barRT.anchorMax = new Vector2(1f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.sizeDelta = new Vector2(0f, TitleBarHeight);
            barRT.anchoredPosition = Vector2.zero;
        }

        /// <summary>背景ディマーを非モーダル化（完全透明＋クリック透過）＝窓の後ろの盤面を塞がない。</summary>
        public static void MakeNonModal(Image dim)
        {
            if (dim == null) return;
            dim.color = new Color(0f, 0f, 0f, 0f);
            dim.raycastTarget = false;
        }

        /// <summary>既存のヘッダー帯（GameObject）をドラッグハンドル化する（HelpOverlay 等・既にヘッダーがある窓向け）。</summary>
        public static void MakeDraggable(GameObject handle, RectTransform windowRT)
        {
            if (handle == null || windowRT == null) return;
            Image img = handle.GetComponent<Image>();
            if (img != null) img.raycastTarget = true; // ドラッグを受けるため
            UIDragMove drag = handle.GetComponent<UIDragMove>();
            if (drag == null) drag = handle.AddComponent<UIDragMove>();
            drag.target = windowRT;
        }

        // ===== 内部 =====

        private static GameObject BuildBar(RectTransform parent, string caption, UnityAction onClose, out RectTransform barRT, UnityAction<bool> onMinimize = null)
        {
            GameObject bar = new GameObject("TitleBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            barRT = bar.GetComponent<RectTransform>();
            Image img = bar.AddComponent<Image>();
            img.color = BarColor;
            UIDragMove drag = bar.AddComponent<UIDragMove>();
            drag.target = parent;

            // 右端のボタン群（×閉じる＋任意で最小化）ぶんだけキャプションを空ける
            float rightReserve = onMinimize != null ? 78f : 42f;
            TextMeshProUGUI cap = CreateText(bar.transform, "≡ " + caption + "　（ドラッグで移動）", 15f, CaptionColor, TextAlignmentOptions.Left);
            RectTransform crt = cap.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(12f, 0f); crt.offsetMax = new Vector2(-rightReserve, 0f);

            // 最小化ボタン（×の左隣・字形は本クロームがトグル）
            if (onMinimize != null)
            {
                GameObject mb = new GameObject("Minimize", typeof(RectTransform));
                mb.transform.SetParent(bar.transform, false);
                RectTransform mrt = mb.GetComponent<RectTransform>();
                mrt.anchorMin = new Vector2(1f, 0f); mrt.anchorMax = new Vector2(1f, 1f);
                mrt.pivot = new Vector2(1f, 0.5f); mrt.sizeDelta = new Vector2(34f, 0f);
                mrt.anchoredPosition = new Vector2(-40f, 0f);
                Image mimg = mb.AddComponent<Image>();
                mimg.color = BarColor;
                Button mbtn = mb.AddComponent<Button>();
                mbtn.transition = UnityEngine.UI.Selectable.Transition.None;
                TextMeshProUGUI mglyph = CreateText(mb.transform, "－", 18f, Color.white, TextAlignmentOptions.Center);
                StretchFull(mglyph.rectTransform);
                bool[] collapsed = { false };
                mbtn.onClick.AddListener(() =>
                {
                    collapsed[0] = !collapsed[0];
                    mglyph.text = collapsed[0] ? "□" : "－";
                    onMinimize(collapsed[0]);
                });
            }

            GameObject cb = new GameObject("Close", typeof(RectTransform));
            cb.transform.SetParent(bar.transform, false);
            RectTransform cbrt = cb.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1f, 0f); cbrt.anchorMax = new Vector2(1f, 1f);
            cbrt.pivot = new Vector2(1f, 0.5f); cbrt.sizeDelta = new Vector2(34f, 0f);
            cbrt.anchoredPosition = new Vector2(-3f, 0f);
            Image cimg = cb.AddComponent<Image>();
            cimg.color = BarColor;
            Button cbtn = cb.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            if (onClose != null) cbtn.onClick.AddListener(onClose);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, Color.white, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
            return bar;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) t.font = ja;
            return t;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
