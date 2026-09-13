using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略画面の割り付け（#戦略MAP刷新・パネルの重なり解消）。MAP・右カラム（勝敗メーター/決裁デスク）・
    /// 下の通知帯の位置を<b>画面に対する割合</b>で定める唯一の基準。各パネルが個別に数値を持つと解像度ごとに
    /// ずれるため、Game 層はここを読んで初期位置を決める。
    ///
    /// 割合なので、同じアスペクト（1920x1080 と 2560x1440＝どちらも 16:9）では完全に一致する。
    /// UI Canvas は参照解像度 1920x1080・match=幅 のため、水平方向の割合は画面幅に依らず設計値どおりになる。
    /// 純データ（Unity シーン非依存）＝TestHarness で不変条件を検証できる。
    /// </summary>
    public readonly struct StrategyScreenLayout
    {
        /// <summary>MAP の左端（画面幅に対する割合）。</summary>
        public readonly float mapLeft;
        /// <summary>MAP の幅（画面幅に対する割合）。</summary>
        public readonly float mapWidth;
        /// <summary>MAP の下端（画面高に対する割合・下から）。</summary>
        public readonly float mapBottom;
        /// <summary>MAP の上端（画面高に対する割合・下から）。上メニューの直下。</summary>
        public readonly float mapTop;
        /// <summary>MAP の右端と右カラムのあいだに空ける間隔（画面幅に対する割合）。</summary>
        public readonly float rightGap;
        /// <summary>画面右端に残す余白（画面幅に対する割合）。右カラムの各パネルはこの位置で右端を揃える。</summary>
        public readonly float rightMargin;

        public StrategyScreenLayout(float mapLeft, float mapWidth, float mapBottom, float mapTop, float rightGap, float rightMargin)
        {
            this.mapLeft = mapLeft;
            this.mapWidth = mapWidth;
            this.mapBottom = mapBottom;
            this.mapTop = mapTop;
            this.rightGap = rightGap;
            this.rightMargin = rightMargin;
        }

        /// <summary>
        /// 既定＝MAP を画面の約74%幅・上メニュー直下から通知帯の上まで。右に約23%のカラム、下に通知帯。
        /// </summary>
        public static StrategyScreenLayout Default => new StrategyScreenLayout(0.012f, 0.740f, 0.245f, 0.885f, 0.008f, 0.006f);

        /// <summary>MAP の右端。</summary>
        public float MapRight => mapLeft + mapWidth;
        /// <summary>MAP の高さ。</summary>
        public float MapHeight => mapTop - mapBottom;
        /// <summary>右カラムの左端。</summary>
        public float RightColumnLeft => MapRight + rightGap;
        /// <summary>右カラムの幅。</summary>
        public float RightColumnWidth => 1f - RightColumnLeft - rightMargin;
        /// <summary>通知帯の高さ（画面下端から MAP 下端まで）。</summary>
        public float BottomBandHeight => mapBottom;
    }

    /// <summary>
    /// <see cref="StrategyScreenLayout"/> の不変条件と、割合→設計ピクセル換算。
    /// 「右カラムが MAP に被らない」「通知が MAP の下に収まる」をここで判定できるようにし、
    /// 実機で重なってから気づく事故（勝敗メーターが星名を隠す／通知が凡例を隠す）を防ぐ。
    /// </summary>
    public static class StrategyScreenLayoutRules
    {
        /// <summary>UI Canvas の参照解像度（幅）。割合→設計ピクセルの換算に使う。</summary>
        public const float ReferenceWidth = 1920f;
        /// <summary>UI Canvas の参照解像度（高さ）。</summary>
        public const float ReferenceHeight = 1080f;

        /// <summary>右カラムが MAP に重ならないか（間隔が正で、幅が実用的に残っているか）。</summary>
        public static bool RightColumnClearsMap(StrategyScreenLayout l)
            => l.RightColumnLeft >= l.MapRight && l.RightColumnWidth > 0.15f;

        /// <summary>
        /// 高さ <paramref name="panelHeightFrac"/>（画面高に対する割合）のパネルを画面下端から
        /// <paramref name="bottomMarginFrac"/> の位置に置いたとき、MAP の下端に収まるか。
        /// </summary>
        public static bool BottomPanelClearsMap(StrategyScreenLayout l, float panelHeightFrac, float bottomMarginFrac)
            => bottomMarginFrac + panelHeightFrac <= l.mapBottom;

        /// <summary>MAP が上メニュー（<paramref name="menuBarFrac"/> ぶん）に食い込まないか。</summary>
        public static bool MapClearsMenuBar(StrategyScreenLayout l, float menuBarFrac)
            => l.mapTop <= 1f - menuBarFrac;

        /// <summary>通知パネルの設計高さ（タイトル30＋タブ28＋5行×26＋余白14）。実装と一致させること。</summary>
        public const float NotificationDesignHeight = 202f;
        /// <summary>通知パネルの下余白（設計ピクセル）。</summary>
        public const float NotificationDesignMargin = 40f;
        /// <summary>
        /// MAP 下端と通知の間に空ける最小の隙間（画面高に対する割合）。
        /// 実機QA（フルHD）で MAP 下辺が通知上辺に 16px 被ったため、隙間を 16px 相当（1080 比で 0.015）ぶん厚くした。
        /// </summary>
        public const float BottomGapFrac = 0.038f;

        /// <summary>
        /// 実際の画面サイズに合わせた割り付けを作る（#画面比率への適応）。
        /// UI Canvas は<b>幅基準</b>でスケールするため、設計ピクセルで組んだ通知パネルの高さは
        /// 画面が横長になるほど「画面高に対して」大きくなる。21:9（2560x1080）では固定の
        /// 下端 0.245 を超えて通知が画面外へはみ出し、縦長（900x1200）では逆に無駄な余白になる。
        /// ここで通知帯の実寸から MAP の下端を出し直し、どの比率でも重ならず余白も残さないようにする。
        /// </summary>
        public static StrategyScreenLayout ForScreen(float screenWidth, float screenHeight)
        {
            StrategyScreenLayout b = StrategyScreenLayout.Default;
            if (screenWidth <= 1f || screenHeight <= 1f) return b;

            // Canvas の matchWidthOrHeight=0（幅基準）と同じ換算＝設計ピクセル→実ピクセル。
            float uiScale = screenWidth / ReferenceWidth;
            float bandPx = (NotificationDesignHeight + NotificationDesignMargin) * uiScale;
            float bandFrac = bandPx / screenHeight;

            // 下端＝通知帯＋隙間。極端な比率でも MAP が潰れない/画面外へ出ない範囲へ収める。
            float mapBottom = Mathf.Clamp(bandFrac + BottomGapFrac, 0.10f, 0.45f);

            // 上端は据え置き（上メニューは画面高の割合で組んであり比率に依らない）。
            return new StrategyScreenLayout(b.mapLeft, b.mapWidth, mapBottom, b.mapTop, b.rightGap, b.rightMargin);
        }

        /// <summary>その画面サイズで、通知パネルが MAP の下に収まり画面内にも収まるか。</summary>
        public static bool BottomPanelFitsOnScreen(StrategyScreenLayout l, float screenWidth, float screenHeight)
        {
            if (screenWidth <= 1f || screenHeight <= 1f) return true;
            float uiScale = screenWidth / ReferenceWidth;
            float bandPx = (NotificationDesignHeight + NotificationDesignMargin) * uiScale;
            float availablePx = l.mapBottom * screenHeight;
            return bandPx <= availablePx + 0.5f;
        }

        /// <summary>
        /// 実ピクセルで最低 <paramref name="minActualPx"/> になる「設計ピクセル」値を返す（#低解像度での可読性）。
        /// Canvas が幅基準でスケールするため、縦長や小窓（例 900x1200）では設計値がそのまま半分近くまで
        /// 縮む。文字サイズやクリック領域はこれを通して、どの画面でも読める/押せる大きさを確保する。
        /// </summary>
        public static float MinDesignForActual(float designPx, float minActualPx, float screenWidth)
        {
            if (screenWidth <= 1f) return designPx;
            float uiScale = screenWidth / ReferenceWidth;
            if (uiScale <= 0.0001f) return designPx;
            float needed = minActualPx / uiScale;   // これだけ設計値を積めば実ピクセルで minActualPx になる
            return Mathf.Max(designPx, needed);
        }

        /// <summary>割合を設計ピクセル（参照解像度 1920 基準）へ換算する。</summary>
        public static float ToDesignWidth(float frac) => frac * ReferenceWidth;

        /// <summary>割合を設計ピクセル（参照解像度 1080 基準）へ換算する。</summary>
        public static float ToDesignHeight(float frac) => frac * ReferenceHeight;

        /// <summary>設計ピクセルを画面高に対する割合へ換算する（パネル実寸から収まりを判定するとき用）。</summary>
        public static float HeightFracOfDesign(float designPx) => designPx / ReferenceHeight;
    }
}
