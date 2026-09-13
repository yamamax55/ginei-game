using UnityEngine;
using UnityEngine.UI;

namespace Ginei
{
    /// <summary>
    /// スクロール領域に<b>見えて掴めるスクロールバー</b>を付ける唯一の窓口（#H）。
    ///
    /// <b>なぜ要るか</b>：このプロジェクトの一覧はすべてコード生成で、<see cref="ScrollRect"/> だけを作って
    /// バーを付けていなかった。ホイールでは動くが、末尾が隠れていること自体が画面から分からない
    /// （実機報告：艦隊メニューの艦隊一覧・行き先一覧で下が切れていると気づけない）。
    ///
    /// <b>作法</b>
    /// <list type="bullet">
    ///   <item>バーは <see cref="ScrollRect"/> の子として作り、<b>ビューポートを内側へ詰めて</b>置く
    ///   ＝行やボタンの上に重ならない（掴めない・押せないを作らない）。</item>
    ///   <item><see cref="ScrollRect.ScrollbarVisibility.AutoHide"/>＝内容が収まっているときは出さない。
    ///   件数・絞り込み・並べ替え・リサイズで内容が変われば Unity 側が範囲とつまみを更新する。</item>
    ///   <item>つまみのドラッグ・トラックのクリック・ホイールは <see cref="ScrollRect"/> が同じ
    ///   <see cref="ScrollRect.normalizedPosition"/> を共有するので<b>自動で同期</b>する
    ///   （ここで位置を別管理しない＝ずれる余地を作らない）。</item>
    ///   <item>バーは自前の当たり判定を持つので、下の行の選択や窓のドラッグを誤発火させない。</item>
    /// </list>
    /// </summary>
    public static class UiScrollbars
    {
        /// <summary>バーの太さ（px）。細すぎると掴めないので、実ピクセルで十分な幅を取る。</summary>
        public const float Thickness = 12f;

        /// <summary>バーと中身のあいだの余白（px）。</summary>
        public const float Gap = 2f;

        private static readonly Color TrackColor = new Color(0.16f, 0.19f, 0.26f, 0.85f);
        private static readonly Color HandleColor = new Color(0.62f, 0.70f, 0.86f, 0.95f);

        /// <summary>
        /// その <see cref="ScrollRect"/> に必要な向きのバーを付ける（縦・横それぞれ、実際に動く方向だけ）。
        /// <b>ビューポートと中身が設定されたあと</b>に呼ぶこと（<c>scroll.content = ...</c> の直後）。
        /// 二重に呼んでも増えない（すでに付いていれば何もしない）。
        /// </summary>
        public static void Attach(ScrollRect scroll)
        {
            if (scroll == null) return;
            if (scroll.vertical && scroll.verticalScrollbar == null) AttachVertical(scroll);
            if (scroll.horizontal && scroll.horizontalScrollbar == null) AttachHorizontal(scroll);
        }

        /// <summary>縦のバーを右端に付け、ビューポートをその幅ぶん左へ詰める。</summary>
        public static void AttachVertical(ScrollRect scroll)
        {
            if (scroll == null || scroll.verticalScrollbar != null) return;

            Scrollbar sb = Build(scroll, "VerticalScrollbar", Scrollbar.Direction.BottomToTop);
            RectTransform rt = (RectTransform)sb.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(Thickness, 0f);
            rt.anchoredPosition = Vector2.zero;

            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            InsetViewport(scroll, right: Thickness + Gap, bottom: 0f);
        }

        /// <summary>横のバーを下端に付け、ビューポートをその高さぶん上へ詰める。</summary>
        public static void AttachHorizontal(ScrollRect scroll)
        {
            if (scroll == null || scroll.horizontalScrollbar != null) return;

            Scrollbar sb = Build(scroll, "HorizontalScrollbar", Scrollbar.Direction.LeftToRight);
            RectTransform rt = (RectTransform)sb.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, Thickness);
            rt.anchoredPosition = Vector2.zero;

            scroll.horizontalScrollbar = sb;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            InsetViewport(scroll, right: 0f, bottom: Thickness + Gap);
        }

        /// <summary>トラック＋つまみを持つ <see cref="Scrollbar"/> を1つ作る（背景から見分けられる濃さ）。</summary>
        private static Scrollbar Build(ScrollRect scroll, string name, Scrollbar.Direction direction)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(scroll.transform, false);

            Image track = go.AddComponent<Image>();
            track.color = TrackColor;
            track.raycastTarget = true;      // トラックのクリックで飛べるように

            Scrollbar sb = go.AddComponent<Scrollbar>();
            sb.direction = direction;
            sb.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            sb.targetGraphic = track;

            // つまみ（Scrollbar が sizeDelta/anchor を毎フレーム書くので、中身だけ用意する）。
            var area = new GameObject("SlidingArea", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            RectTransform areaRT = (RectTransform)area.transform;
            areaRT.anchorMin = Vector2.zero;
            areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(1f, 1f);
            areaRT.offsetMax = new Vector2(-1f, -1f);

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(area.transform, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = HandleColor;
            handleImg.raycastTarget = true;
            RectTransform handleRT = (RectTransform)handle.transform;
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;

            sb.handleRect = handleRT;
            sb.targetGraphic = handleImg;    // つまみを掴んだ手応え（色の変化）
            return sb;
        }

        /// <summary>
        /// ビューポートをバーのぶんだけ内側へ詰める（行の上にバーが重ならないように）。
        /// アンカーの張り方（0..1＋sizeDelta / offsetMin,Max）に依らず効くよう offset で調整する。
        /// </summary>
        private static void InsetViewport(ScrollRect scroll, float right, float bottom)
        {
            RectTransform vp = scroll.viewport;
            if (vp == null) return;
            if (right > 0f) vp.offsetMax = new Vector2(vp.offsetMax.x - right, vp.offsetMax.y);
            if (bottom > 0f) vp.offsetMin = new Vector2(vp.offsetMin.x, vp.offsetMin.y + bottom);
        }
    }
}
