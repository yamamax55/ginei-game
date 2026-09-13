using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 集約された艦隊マーカーをクリックしたときに開く「短い艦隊一覧」（#戦略MAPの艦艇表示）。
    /// 同じ場所に複数の艦隊が居るとマーカーは1つに畳まれるため、<b>個別に選ぶ手段</b>がここになる。
    /// 行をクリックするとその艦隊が選択され、以後は従来どおり<b>盤面を右クリックで進軍</b>できる。
    ///
    /// 設計の要点：
    /// ・MAP のビューポート内に収める。<b>最大高さ＋スクロール</b>で 20〜50 隊が集中しても最終行まで選べる。
    /// ・文字と行の高さは実ピクセルで下限を確保（縦長/小窓で半分に縮まないように）。
    /// ・名前が長くて省略されても、<b>兵力と状態は別の欄</b>に置くので識別できる。
    /// ・窓の移動/リサイズ・解像度変更にも追従して位置と大きさを取り直す。
    /// ・記号を使わず日本語と数字だけで組む（フォントに無い字＝豆腐を出さない）。
    /// </summary>
    public class FleetClusterListPanel : MonoBehaviour
    {
        /// <summary>いま開いているか（盤面が入力を譲るために参照）。</summary>
        public static bool IsOpen { get; private set; }

        private static FleetClusterListPanel instance;

        private Canvas canvas;
        private RectTransform panel;
        private RectTransform content;
        private ScrollRect scroll;
        private TextMeshProUGUI header;
        private TMP_FontAsset jpFont;
        private readonly List<GameObject> rows = new List<GameObject>();
        private System.Action<int> onPick;

        // 開いたときの状態（追従のために覚えておく）。
        private Vector2 anchorScreen;
        private Rect viewport;
        private Vector2Int lastScreen;

        // 実ピクセルでの下限（低解像度・縦長でも読める/押せる大きさ）。
        private const float MinFontPx = 14f;
        private const float MinRowPx = 30f;
        private const float BaseFont = 17f;
        private const float BaseRow = 34f;
        /// <summary>一覧の最大高さ（ビューポート高に対する割合）。これを超えたらスクロールする。</summary>
        private const float MaxHeightFrac = 0.62f;

        public static void Show(string title, IList<string> names, IList<string> details, IList<int> fleetIds,
            Vector2 screenPos, Rect viewport, System.Action<int> onPickFleet)
        {
            if (instance == null)
            {
                var go = new GameObject("FleetClusterListPanel");
                instance = go.AddComponent<FleetClusterListPanel>();
                instance.Build();
            }
            instance.Open(title, names, details, fleetIds, screenPos, viewport, onPickFleet);
        }

        public static void Hide()
        {
            if (instance != null) instance.Close();
        }

        private void Build()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");

            var canvasObj = new GameObject("FleetListCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 895; // 通知(880)/決裁(885)より前・モーダル(900+)より後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            var p = new GameObject("Panel", typeof(RectTransform));
            p.transform.SetParent(canvasObj.transform, false);
            panel = p.GetComponent<RectTransform>();
            panel.pivot = new Vector2(0f, 1f);
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            var bg = p.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);
            var outline = p.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.84f, 0.36f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 見出し行（左＝まとまりの要約／右＝閉じる）。
            var head = new GameObject("Header", typeof(RectTransform));
            head.transform.SetParent(p.transform, false);
            var headRT = head.GetComponent<RectTransform>();
            headRT.anchorMin = new Vector2(0f, 1f); headRT.anchorMax = new Vector2(1f, 1f);
            headRT.pivot = new Vector2(0.5f, 1f);

            header = MakeText(headRT, "", BaseFont + 1f, new Color(1f, 0.88f, 0.55f));
            header.fontStyle = FontStyles.Bold;
            header.margin = new Vector4(10f, 0f, 44f, 0f);
            var hrt = header.rectTransform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

            MakeCloseButton(headRT);

            // 本文＝スクロール領域（行数が多くても最終行まで到達できる）。
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(p.transform, false);
            var scrollRT = scrollGo.GetComponent<RectTransform>();
            scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 26f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vrt = viewportGo.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewportGo.AddComponent<RectMask2D>();
            scroll.viewport = vrt;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            // ★横に張ったら sizeDelta.x を 0 にする（実機QA：名前の左端が切れて「第900031」が「艦隊」だけになった）。
            // new GameObject(RectTransform) の既定 sizeDelta は (100,100)。横 stretch のまま放置すると
            // 中身がビューポートより 100px 広くなり、pivot 0.5 で左右へ 50px ずつはみ出して RectMask2D に切られる。
            content.sizeDelta = new Vector2(0f, 0f);
            content.anchoredPosition = Vector2.zero;
            var cvlg = contentGo.AddComponent<VerticalLayoutGroup>();
            cvlg.padding = new RectOffset(8, 8, 6, 6);
            cvlg.spacing = 3f;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            var cfit = contentGo.AddComponent<ContentSizeFitter>();
            cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            UiScrollbars.Attach(scroll);   // #H スクロールできることを画面で示す（見えて掴めるバー）

            // 見出し／本文の縦位置は Layout() で毎回入れ直す（サイズが変わるため）。
            headerRT = headRT;
            scrollAreaRT = scrollRT;

            canvasObj.SetActive(false);
        }

        private RectTransform headerRT;
        private RectTransform scrollAreaRT;

        private void MakeCloseButton(RectTransform parent)
        {
            var go = new GameObject("Close", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(40f, -6f);
            rt.anchoredPosition = new Vector2(-4f, 0f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.24f, 0.16f, 0.18f, 1f);
            var btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(Close);
            // 記号（×）はフォント依存なので日本語で書く（#記号欠落）。
            var cap = MakeText(rt, "閉", BaseFont, new Color(1f, 0.86f, 0.86f));
            cap.alignment = TextAlignmentOptions.Center;
            cap.raycastTarget = false;
            var crt = cap.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        }

        /// <summary>実ピクセルで下限を割らない文字サイズ（設計ピクセル）。</summary>
        private static float FontSize()
            => StrategyScreenLayoutRules.MinDesignForActual(BaseFont, MinFontPx, Screen.width);

        /// <summary>実ピクセルで下限を割らない行の高さ（＝クリック領域）。</summary>
        private static float RowHeight()
            => StrategyScreenLayoutRules.MinDesignForActual(BaseRow, MinRowPx, Screen.width);

        private void Open(string title, IList<string> names, IList<string> details, IList<int> fleetIds,
            Vector2 screenPos, Rect vp, System.Action<int> onPickFleet)
        {
            onPick = onPickFleet;
            anchorScreen = screenPos;
            viewport = vp;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            ClearRows();

            float font = FontSize();
            float row = RowHeight();

            header.text = title;
            header.fontSize = font + 1f;

            int n = names != null ? names.Count : 0;
            for (int i = 0; i < n; i++)
            {
                int fleetId = (fleetIds != null && i < fleetIds.Count) ? fleetIds[i] : -1;
                string detail = (details != null && i < details.Count) ? details[i] : "";
                rows.Add(MakeRow(names[i], detail, fleetId, font, row));
            }

            canvas.gameObject.SetActive(true);
            IsOpen = true;
            Layout();
        }

        /// <summary>大きさと位置を取り直す（開いた直後・窓移動/リサイズ・解像度変更のたび）。</summary>
        private void Layout()
        {
            if (panel == null) return;

            float uiScale = Screen.width > 0 ? Screen.width / 1920f : 1f;
            float headerH = RowHeight();

            float width = StrategyScreenLayoutRules.MinDesignForActual(300f, 220f, Screen.width);

            // 本文の必要高さ（行数ぶん）と、ビューポートから決まる上限の小さい方を採る＝はみ出さない。
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float wantBody = content.rect.height;
            float viewportPx = Mathf.Max(1f, viewport.height * Screen.height);
            float maxBody = (viewportPx * MaxHeightFrac) / uiScale - headerH;
            float bodyH = Mathf.Clamp(wantBody, RowHeight(), Mathf.Max(RowHeight(), maxBody));

            panel.sizeDelta = new Vector2(width, headerH + bodyH);

            if (headerRT != null)
            {
                headerRT.sizeDelta = new Vector2(0f, headerH);
                headerRT.anchoredPosition = Vector2.zero;
            }
            if (scrollAreaRT != null)
            {
                scrollAreaRT.anchorMin = new Vector2(0f, 0f);
                scrollAreaRT.anchorMax = new Vector2(1f, 1f);
                scrollAreaRT.offsetMin = new Vector2(0f, 0f);
                scrollAreaRT.offsetMax = new Vector2(0f, -headerH);
            }

            PlaceInsideViewport(width, headerH + bodyH, uiScale);
        }

        /// <summary>MAP のビューポート内へ収める（右/下へはみ出したら内側へ折り返し、最後にクランプ）。</summary>
        private void PlaceInsideViewport(float designW, float designH, float uiScale)
        {
            float wPx = designW * uiScale;
            float hPx = designH * uiScale;

            float vx0 = viewport.xMin * Screen.width, vx1 = viewport.xMax * Screen.width;
            float vy0 = viewport.yMin * Screen.height, vy1 = viewport.yMax * Screen.height;

            float x = anchorScreen.x + 14f;
            float y = anchorScreen.y - 14f;
            if (x + wPx > vx1) x = anchorScreen.x - 14f - wPx;
            if (y - hPx < vy0) y = anchorScreen.y + 14f + hPx;

            x = Mathf.Clamp(x, vx0 + 4f, Mathf.Max(vx0 + 4f, vx1 - wPx - 4f));
            y = Mathf.Clamp(y, Mathf.Min(vy1 - 4f, vy0 + hPx + 4f), vy1 - 4f);

            panel.anchoredPosition = new Vector2(x / uiScale, y / uiScale);
        }

        private void Update()
        {
            if (!IsOpen) return;

            // 解像度やマップ窓が変わったら位置と大きさを取り直す（はみ出したまま残さない）。
            var now = new Vector2Int(Screen.width, Screen.height);
            Camera mapCam = Camera.main;
            Rect cur = mapCam != null ? mapCam.rect : viewport;
            if (now != lastScreen || cur != viewport)
            {
                lastScreen = now;
                viewport = cur;
                Layout();
            }
        }

        private GameObject MakeRow(string name, string detail, int fleetId, float font, float rowHeight)
        {
            var go = new GameObject("Row_" + fleetId, typeof(RectTransform));
            go.transform.SetParent(content, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = rowHeight; le.preferredHeight = rowHeight;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.27f, 1f);
            var btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            int captured = fleetId;
            btn.onClick.AddListener(() => { onPick?.Invoke(captured); Close(); });

            // 名前は長いと省略されうるので、兵力/状態は<b>別の欄（右寄せ）</b>に置いて必ず読めるようにする。
            var nameLabel = MakeText((RectTransform)go.transform, name, font, new Color(0.92f, 0.95f, 1f));
            nameLabel.alignment = TextAlignmentOptions.Left;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.raycastTarget = false;
            // 実機QA：名前の左端が切れて「第900031」が「0031」に見えた。
            // MakeText の既定 pivot/anchor のまま offset を効かせると基準がずれるので、
            // アンカーを張ったうえで pivot も明示し、左の余白は margin で確保する（矩形は切らない）。
            var nrt = nameLabel.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0.56f, 1f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.offsetMin = Vector2.zero; nrt.offsetMax = Vector2.zero;
            nameLabel.margin = new Vector4(10f, 0f, 4f, 0f);

            var detailLabel = MakeText((RectTransform)go.transform, detail, font, new Color(1f, 0.9f, 0.66f));
            detailLabel.alignment = TextAlignmentOptions.Right;
            detailLabel.overflowMode = TextOverflowModes.Truncate;
            detailLabel.raycastTarget = false;
            var drt = detailLabel.rectTransform;
            drt.anchorMin = new Vector2(0.56f, 0f); drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            detailLabel.margin = new Vector4(4f, 0f, 10f, 0f);

            return go;
        }

        private TextMeshProUGUI MakeText(RectTransform parent, string text, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (jpFont != null) t.font = jpFont;
            return t;
        }

        private void ClearRows()
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i] != null) Destroy(rows[i]);
            rows.Clear();
        }

        private void Close()
        {
            ClearRows();
            if (canvas != null) canvas.gameObject.SetActive(false);
            IsOpen = false;
        }

        private void OnDestroy()
        {
            if (instance == this) { instance = null; IsOpen = false; }
        }
    }
}
