using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略（星系）マップの Windows 風UI（銀英伝の古典UI意匠・#UI統一）。
    /// ①画面上部の<b>固定コマンドメニューバー</b>（国家ステータス・二重暦/速度・各パネルを開くボタン列）と、
    /// ②<b>ドラッグで動かせる星系マップ窓</b>から成る。
    /// <b>整合の要</b>：マップ窓は正規化矩形 <c>mapRect</c>（0〜1・画面全体基準）を唯一の真実とし、
    /// <see cref="Camera.rect"/> と窓UIのアンカーの<b>両方に同じ mapRect を与える</b>＝両者とも画面全体基準なので
    /// 解像度・アスペクトに依らず<b>ピクセル一致</b>する（GetWorldCorners/Screen 依存の逆算をしない）。
    /// タイトルバーのドラッグは mapRect を正規化で平行移動する。<see cref="GalaxyView"/> は <see cref="Camera.ScreenToWorldPoint"/>
    /// でクリックを拾い、これはビューポート rect を尊重するため窓移動後も選択/進軍が正しく動く。
    /// 窓の外は<b>背景カメラ</b>が黒でクリアし残像を防ぐ。浮きHUDは <see cref="GalaxyView.HideWorldHud"/> で抑制し上メニューへ集約。Strategy 専用。
    /// </summary>
    public class StrategyMapWindow : MonoBehaviour
    {
        [Header("上部メニューバー")]
        public float menuBarFrac = 0.10f; // 目標(勝利進捗)行を足したぶん少し高く

        [Header("マップ窓")]
        [Tooltip("窓タイトルバーの高さ（ピクセル）")]
        public float mapTitleHeight = 30f;

        [Header("配色（ゲーム意匠）")]
        public Color menuBarColor = new Color(0.11f, 0.15f, 0.22f, 1f);
        public Color titleBarColor = new Color(0.13f, 0.18f, 0.26f, 1f);
        public Color buttonColor = new Color(0.16f, 0.21f, 0.30f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);
        public Color desktopColor = new Color(0.02f, 0.02f, 0.05f, 1f);

        [Header("リサイズ")]
        [Tooltip("右下のリサイズグリップの一辺（ピクセル）")]
        public float resizeGripSize = 22f;
        [Tooltip("マップ窓の最小幅/高さ（画面比 0〜1）")]
        public float minWindowFrac = 0.2f;

        // マップ窓の正規化矩形（画面全体を 0〜1 とした位置/大きさ）。camera.rect と窓UIの両方に使う＝必ず一致。
        // 初期は左寄せ・幅約63%・上メニュー直下から高さ約55%（右と下に通知/決裁の浮き窓ぶんの余白を残す）。
        private Rect mapRect = new Rect(0.01f, 0.31f, 0.63f, 0.55f);

        private Camera cam;
        private Camera bgCam;
        private Rect originalRect;
        private bool rectApplied;

        private RectTransform titleBarRT;
        private RectTransform contentRT;
        private RectTransform edgeLeft, edgeRight, edgeBottom;
        private RectTransform resizeGripRT;
        private TextMeshProUGUI clockLabel;

        // 最小化（タイトルバーだけ残してマップ表示を畳む）
        private bool minimized;
        private int savedCullingMask;
        private CameraClearFlags savedClearFlags;
        private TextMeshProUGUI minimizeLabel;

        // 目標（勝利進捗＋次の一手）— B：目的可視化（#遊べる縦スライス）
        private RectTransform objectiveFillRT;
        private TextMeshProUGUI objectiveLabel;
        private TextMeshProUGUI hintLabel;
        private float objectiveTimer;
        private const float ObjectiveInterval = 0.5f; // 毎フレーム再計算しない（終盤ラグ規律）

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy") return;
            if (UnityEngine.Object.FindAnyObjectByType<StrategyMapWindow>() != null) return;
            new GameObject("StrategyMapWindow").AddComponent<StrategyMapWindow>();
        }

        private void Awake()
        {
            cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            GalaxyView.HideWorldHud = true;
            SetupBackgroundCamera();
            BuildUI();
            ApplyLayout();
        }

        private void OnDestroy()
        {
            if (cam != null && rectApplied) cam.rect = originalRect;
            if (cam != null && minimized) { cam.cullingMask = savedCullingMask; cam.clearFlags = savedClearFlags; } // 最小化中の破棄でも復元
            if (bgCam != null) Destroy(bgCam.gameObject);
            GalaxyView.HideWorldHud = false;
            UIDragMove.TopReservedPx = 0f; // 戦略を離れたら確保帯を解除（他シーンに持ち越さない）
        }

        private void Update()
        {
            TimeDisplay.StepSpeedInput();
            if (clockLabel != null && TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockLabel.text = text;
                clockLabel.color = color;
            }

            // 目標（勝利進捗＋次の一手）は間引いて更新（毎フレーム再計算しない＝終盤ラグ規律）。
            objectiveTimer += Time.unscaledDeltaTime;
            if (objectiveTimer >= ObjectiveInterval) { objectiveTimer = 0f; UpdateObjective(); }
        }

        private void LateUpdate()
        {
            // ドラッグで動かせる各窓（観測オーバーレイ等）が上メニューより上へ行かないよう、確保帯＝上メニュー高を公開。
            UIDragMove.TopReservedPx = menuBarFrac * Screen.height;
            ApplyLayout();
        }

        // ===== カメラ =====

        private void SetupBackgroundCamera()
        {
            if (cam == null) return;
            originalRect = cam.rect;
            rectApplied = true;

            var go = new GameObject("StrategyDesktopCamera");
            bgCam = go.AddComponent<Camera>();
            bgCam.orthographic = true;
            bgCam.depth = cam.depth - 1f;
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = desktopColor;
            bgCam.cullingMask = 0;
            bgCam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>タイトルバーのドラッグで窓（mapRect）を正規化平行移動する。</summary>
        private void OnTitleDrag(Vector2 deltaPixels)
        {
            float sw = Screen.width > 0 ? Screen.width : 1920f;
            float sh = Screen.height > 0 ? Screen.height : 1080f;
            mapRect.x += deltaPixels.x / sw;
            mapRect.y += deltaPixels.y / sh;
            ApplyLayout();
        }

        /// <summary>右下グリップのドラッグで窓（mapRect）をリサイズする（上端＝top は固定し下/右辺を動かす）。</summary>
        private void OnResizeDrag(Vector2 deltaPixels)
        {
            float sw = Screen.width > 0 ? Screen.width : 1920f;
            float sh = Screen.height > 0 ? Screen.height : 1080f;
            float topY = mapRect.yMax;            // 上端を固定（上メニュー側を動かさない）
            mapRect.width = Mathf.Max(minWindowFrac, mapRect.width + deltaPixels.x / sw);
            mapRect.y += deltaPixels.y / sh;       // 下辺をカーソルに追従
            mapRect.height = Mathf.Max(minWindowFrac, topY - mapRect.y);
            mapRect.y = topY - mapRect.height;     // height をクランプしたぶん下辺を整合
            ApplyLayout();
        }

        /// <summary>mapRect を camera.rect と窓UIのアンカーへ反映（両者とも画面全体基準＝一致）。</summary>
        private void ApplyLayout()
        {
            if (cam == null) return;
            float sh = Screen.height > 0 ? Screen.height : 1080f;
            // 上メニューバー＋窓タイトルバーのぶんを差し引いた上限＝窓の上端はここを越えない（#4）。
            float titleFrac = mapTitleHeight / sh;
            float topLimit = Mathf.Clamp01(1f - menuBarFrac - titleFrac);

            mapRect.width = Mathf.Clamp(mapRect.width, minWindowFrac, 1f);
            mapRect.height = Mathf.Clamp(mapRect.height, minWindowFrac, Mathf.Max(minWindowFrac, topLimit));
            mapRect.x = Mathf.Clamp(mapRect.x, 0f, 1f - mapRect.width);
            mapRect.y = Mathf.Clamp(mapRect.y, 0f, Mathf.Max(0f, topLimit - mapRect.height));
            cam.rect = mapRect;

            float x0 = mapRect.xMin, x1 = mapRect.xMax, y0 = mapRect.yMin, y1 = mapRect.yMax;

            // 最小化中は本体（マップ枠・縁・グリップ）を隠してタイトルバーだけ残す。
            bool body = !minimized;
            if (contentRT != null) contentRT.gameObject.SetActive(body);
            if (edgeLeft != null) edgeLeft.gameObject.SetActive(body);
            if (edgeRight != null) edgeRight.gameObject.SetActive(body);
            if (edgeBottom != null) edgeBottom.gameObject.SetActive(body);
            if (resizeGripRT != null) resizeGripRT.gameObject.SetActive(body);

            if (contentRT != null) { Stretch(contentRT, x0, y0, x1, y1); }
            if (titleBarRT != null)
            {
                titleBarRT.anchorMin = new Vector2(x0, y1);
                titleBarRT.anchorMax = new Vector2(x1, y1);
                titleBarRT.pivot = new Vector2(0.5f, 0f);
                titleBarRT.sizeDelta = new Vector2(0f, mapTitleHeight);
                titleBarRT.anchoredPosition = Vector2.zero;
            }
            // 縁取り（細いバー）
            Edge(edgeLeft, x0, y0, x0, y1, new Vector2(0f, 0.5f), new Vector2(2f, 0f));
            Edge(edgeRight, x1, y0, x1, y1, new Vector2(1f, 0.5f), new Vector2(2f, 0f));
            Edge(edgeBottom, x0, y0, x1, y0, new Vector2(0.5f, 0f), new Vector2(0f, 2f));

            // 右下リサイズグリップ（窓の右下角）。
            if (resizeGripRT != null)
            {
                resizeGripRT.anchorMin = new Vector2(x1, y0);
                resizeGripRT.anchorMax = new Vector2(x1, y0);
                resizeGripRT.pivot = new Vector2(1f, 0f);
                resizeGripRT.sizeDelta = new Vector2(resizeGripSize, resizeGripSize);
                resizeGripRT.anchoredPosition = Vector2.zero;
            }
        }

        private static void Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Edge(RectTransform rt, float x0, float y0, float x1, float y1, Vector2 pivot, Vector2 sizeDelta)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("StrategyMapWindowCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();
            Transform root = canvasObj.transform;

            BuildMenuBar(root);
            BuildMapWindow(root);
            BuildObserverWindow(root);
        }

        private void BuildMenuBar(Transform root)
        {
            var bar = AddBar(root, "MenuBar", new Vector2(0f, 1f - menuBarFrac), new Vector2(1f, 1f), menuBarColor);

            var top = new GameObject("TopRow").AddComponent<RectTransform>();
            top.transform.SetParent(bar.transform, false);
            top.anchorMin = new Vector2(0f, 0.66f); top.anchorMax = new Vector2(1f, 1f);
            top.offsetMin = Vector2.zero; top.offsetMax = Vector2.zero;

            var title = AddText(top, "≡ 戦略", 20f, accentColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            SetAnchors(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.20f, 1f), new Vector2(20f, 0f), new Vector2(-8f, 0f));

            // 税率/国庫/民心/安定度の常時表示は廃止（じゃまなので削除）。出所は「勢力」(G)／「財政」(E) パネル。
            clockLabel = AddText(top, "", 16f, new Color(0.95f, 0.92f, 0.7f), TextAlignmentOptions.Right);
            SetAnchors(clockLabel.rectTransform, new Vector2(0.74f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-20f, 0f));

            // 目標行（勝利進捗バー＋次の一手）＝プレイ中の行動指針（B：目的可視化）。
            BuildObjectiveRow(bar.transform);

            var cmd = new GameObject("CommandRow").AddComponent<RectTransform>();
            cmd.transform.SetParent(bar.transform, false);
            cmd.anchorMin = new Vector2(0f, 0f); cmd.anchorMax = new Vector2(1f, 0.32f);
            cmd.offsetMin = new Vector2(16f, 4f); cmd.offsetMax = new Vector2(-16f, -2f);
            var hlg = cmd.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            // 執務机（なりきり提督の一人称UI）を上メニューへ格上げ＝観測ウィンドウのシステムタブ内項目でなく、
            // コマンドバーの専用ボタンで直接開く（プレイヤーの主画面ゆえ最優先・観測の左に置く）。Alt+J も従来どおり。
            MakeBarButton(cmd.transform, "執務机", 110f,
                () => UnityEngine.Object.FindAnyObjectByType<ProtagonistDeskOverlay>()?.Toggle());

            // 艦隊管理（プレイヤー勢力の自艦隊点検＝プール/編制サマリ/空席・過大指揮）を上メニューへ。執務机の隣に置く。
            MakeBarButton(cmd.transform, "艦隊管理", 110f,
                () => UnityEngine.Object.FindAnyObjectByType<PlayerFleetManagementOverlay>()?.Toggle());

            // 上メニューの集約：25個のボタンを「観測」1個に畳み、タブ化したウィンドウ（内政/経済/軍事/政治/
            // システムの5タブ）から各オブザーバを開く。既存ウィンドウ・単一文字ショートカット（G/J/M/…）は不変。
            MakeBarButton(cmd.transform, "観測 ▾", 132f, ToggleObserverWindow);

            var rule = AddBar(bar.transform, "Rule", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));
            var rrt = (RectTransform)rule.transform; rrt.pivot = new Vector2(0.5f, 0f); rrt.sizeDelta = new Vector2(0f, 2f);
        }

        private void BuildMapWindow(Transform root)
        {
            // タイトルバー（つかんで移動・mapRect を動かす）
            var bar = new GameObject("MapTitleBar").AddComponent<RectTransform>();
            bar.transform.SetParent(root, false);
            titleBarRT = bar;
            var tImg = bar.gameObject.AddComponent<Image>();
            tImg.color = titleBarColor;
            var drag = bar.gameObject.AddComponent<MapWindowDrag>();
            drag.onDragDelta = OnTitleDrag;
            var cap = AddText(bar, "≡ 星系マップ　（ドラッグで移動）", 15f, accentColor, TextAlignmentOptions.Left);
            SetAnchors(cap.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-44f, 0f));

            // 最小化／復元ボタン（右上の「—」）。タイトルバーだけ残してマップ表示を畳む。
            BuildMinimizeButton(bar);

            // 中身領域（透明＝マップを見せる・クリックを塞がない）。アンカーは ApplyLayout で mapRect に合わせる。
            contentRT = new GameObject("MapContent").AddComponent<RectTransform>();
            contentRT.transform.SetParent(root, false);

            // 縁取り（細い金色バー・raycast しない）
            Color edge = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
            edgeLeft = MakeEdge(root, edge);
            edgeRight = MakeEdge(root, edge);
            edgeBottom = MakeEdge(root, edge);

            // 右下リサイズグリップ（つかんで窓の大きさを変える）。raycast を受けてドラッグを拾う。
            var gripGo = new GameObject("MapResizeGrip");
            gripGo.transform.SetParent(root, false);
            resizeGripRT = gripGo.AddComponent<RectTransform>();
            var gripImg = gripGo.AddComponent<Image>();
            gripImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f);
            gripImg.raycastTarget = true;
            var gripDrag = gripGo.AddComponent<MapWindowDrag>();
            gripDrag.onDragDelta = OnResizeDrag;
        }

        /// <summary>タイトルバー右端に最小化／復元ボタン（—／＋）を作る。</summary>
        private void BuildMinimizeButton(Transform titleBar)
        {
            var go = new GameObject("MinimizeButton");
            go.transform.SetParent(titleBar, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(38f, 0f);
            rt.anchoredPosition = new Vector2(-4f, 0f);
            var img = go.AddComponent<Image>();
            img.color = buttonColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleMinimize);
            minimizeLabel = AddText(go.transform, "—", 18f, accentColor, TextAlignmentOptions.Center);
            minimizeLabel.fontStyle = FontStyles.Bold;
            SetAnchors(minimizeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>最小化／復元を切り替える。最小化中はマップ描画を畳み（カメラ非描画）、タイトルバーだけ残す。</summary>
        public void ToggleMinimize()
        {
            minimized = !minimized;
            if (cam != null)
            {
                if (minimized)
                {
                    // マップを描かず背景デスクトップ（黒）を見せる＝タイトルバーだけ残った最小化状態。
                    savedCullingMask = cam.cullingMask;
                    savedClearFlags = cam.clearFlags;
                    cam.cullingMask = 0;
                    cam.clearFlags = CameraClearFlags.Depth;
                }
                else
                {
                    cam.cullingMask = savedCullingMask;
                    cam.clearFlags = savedClearFlags;
                }
            }
            if (minimizeLabel != null) minimizeLabel.text = minimized ? "＋" : "—";
            ApplyLayout();
        }

        private static RectTransform MakeEdge(Transform parent, Color color)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return rt;
        }

        // ===== 目標（勝利進捗＋次の一手）＝B：目的可視化 =====

        /// <summary>現在の難易度（GameSettings）に応じた制覇しきい値（進捗バーのマーカー/着色に使う）。</summary>
        private static float ActiveDominationFraction()
            => CampaignDifficultyRules.VictoryParams(
                GameSettings.Instance != null ? GameSettings.Instance.campaignDifficulty : CampaignDifficulty.普通).dominationFraction;

        private void BuildObjectiveRow(Transform bar)
        {
            var row = new GameObject("ObjectiveRow").AddComponent<RectTransform>();
            row.transform.SetParent(bar, false);
            row.anchorMin = new Vector2(0f, 0.34f); row.anchorMax = new Vector2(1f, 0.64f);
            row.offsetMin = new Vector2(20f, 0f); row.offsetMax = new Vector2(-20f, 0f);

            // 勝利進捗バー（左）：背景＋フィル＋しきい値マーカー。上に進捗テキストを重ねる。
            var barBg = new GameObject("VictoryBarBg").AddComponent<RectTransform>();
            barBg.transform.SetParent(row, false);
            SetAnchors(barBg, new Vector2(0f, 0.12f), new Vector2(0.46f, 0.88f), Vector2.zero, Vector2.zero);
            var bgImg = barBg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f); bgImg.raycastTarget = false;

            objectiveFillRT = new GameObject("Fill").AddComponent<RectTransform>();
            objectiveFillRT.transform.SetParent(barBg, false);
            objectiveFillRT.anchorMin = new Vector2(0f, 0f);
            objectiveFillRT.anchorMax = new Vector2(0f, 1f); // 幅は UpdateObjective で支配率に
            objectiveFillRT.offsetMin = Vector2.zero; objectiveFillRT.offsetMax = Vector2.zero;
            var fillImg = objectiveFillRT.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.7f, 0.95f, 0.9f); fillImg.raycastTarget = false;

            // 勝利しきい値（難易度連動）の縦マーカー。
            float winMark = ActiveDominationFraction();
            var mark = new GameObject("WinMark").AddComponent<RectTransform>();
            mark.transform.SetParent(barBg, false);
            mark.anchorMin = new Vector2(winMark, 0f); mark.anchorMax = new Vector2(winMark, 1f);
            mark.pivot = new Vector2(0.5f, 0.5f); mark.sizeDelta = new Vector2(2f, 0f);
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.color = new Color(1f, 0.84f, 0.36f, 0.9f); markImg.raycastTarget = false;

            objectiveLabel = AddText(barBg, "", 14f, Color.white, TextAlignmentOptions.Center);
            SetAnchors(objectiveLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 次の一手ヒント（右）。
            hintLabel = AddText(row, "", 15f, new Color(0.95f, 0.9f, 0.6f), TextAlignmentOptions.Left);
            SetAnchors(hintLabel.rectTransform, new Vector2(0.48f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), Vector2.zero);
        }

        /// <summary>勝利進捗バー＋次の一手を更新する（盤面シグナルから・間引き）。</summary>
        private void UpdateObjective()
        {
            if (objectiveFillRT == null) return;
            GalaxyMap map = StrategySession.Map;
            if (map == null) { if (objectiveLabel != null) objectiveLabel.text = ""; if (hintLabel != null) hintLabel.text = ""; return; }

            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            int total = CampaignVictoryRules.TotalSystems(map);
            int owned = CampaignVictoryRules.OwnedCount(map, pf);
            float frac = CampaignVictoryRules.OwnedFraction(map, pf);
            float winFrac = ActiveDominationFraction();
            bool rivalsRemain = CampaignVictoryRules.RivalSystemsRemain(map, pf);

            objectiveFillRT.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            var fillImg = objectiveFillRT.GetComponent<Image>();
            if (fillImg != null)
            {
                // 勝利目前=金／守勢(支配≦15%)=赤／通常=青。
                fillImg.color = frac >= winFrac - 0.1f ? new Color(1f, 0.84f, 0.36f, 0.95f)
                    : frac <= 0.15f ? new Color(0.95f, 0.45f, 0.4f, 0.95f)
                    : new Color(0.35f, 0.7f, 0.95f, 0.9f);
            }
            if (objectiveLabel != null)
                objectiveLabel.text = $"制覇 {Mathf.RoundToInt(frac * 100f)}% / {Mathf.RoundToInt(winFrac * 100f)}%（{pf} {owned} / {total} 星系）";

            // 次の一手（Core が選び、Game が文言＋キーへ）。
            CountFleetSignals(pf, out bool hasEngagement, out int idleFleets);
            CampaignHint hint = CampaignGuidanceRules.NextAction(hasEngagement, idleFleets, rivalsRemain);
            if (hintLabel != null) hintLabel.text = "▶ " + HintText(hint);
        }

        /// <summary>プレイヤー艦隊の交戦中の有無・遊休数を数える（次の一手のシグナル）。</summary>
        private void CountFleetSignals(Faction pf, out bool hasEngagement, out int idleFleets)
        {
            hasEngagement = false; idleFleets = 0;
            StrategicFleetRegistry reg = StrategySession.Reg;
            if (reg == null || reg.fleets == null) return;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != pf) continue;
                if (f.engaged) hasEngagement = true;
                else if (!f.IsMoving && f.strength > 0) idleFleets++;
            }
        }

        private static string HintText(CampaignHint hint)
        {
            switch (hint)
            {
                case CampaignHint.前線へ潜行: return "交戦中の回廊をダブルクリックで潜行（手動指揮）";
                case CampaignHint.任務を発令: return "C: 攻略任務を発令 ／ B: 艦艇観測";
                case CampaignHint.領土を広げよ: return "艦隊を選んで右クリックで敵星系へ進軍";
                default: return "好機を待つ";
            }
        }

        // ===== 部品 =====

        // ===== 観測ウィンドウ（上メニュー集約・タブ化） =====

        private GameObject observerWindow;
        private readonly List<GameObject> tabPages = new List<GameObject>();
        private readonly List<Button> tabButtons = new List<Button>();
        private int activeTab;

        /// <summary>観測カテゴリ（5タブ）と各タブの項目（ラベル→該当オブザーバの Toggle）。集約の単一定義。</summary>
        private (string name, (string label, System.Action action)[] items)[] ObserverCategories()
            => new (string, (string, System.Action)[])[]
        {
            ("内政", new (string, System.Action)[]
            {
                ("勢力",   () => UnityEngine.Object.FindAnyObjectByType<CampaignObserverOverlay>()?.Toggle()),
                ("法令",   () => UnityEngine.Object.FindAnyObjectByType<LawObserverOverlay>()?.Toggle()),
                ("政府",   () => UnityEngine.Object.FindAnyObjectByType<GovernmentObserverOverlay>()?.Toggle()),
                ("官僚",   () => UnityEngine.Object.FindAnyObjectByType<BureaucracyObserverOverlay>()?.Toggle()),
                ("人口",   () => UnityEngine.Object.FindAnyObjectByType<DemographicsObserverOverlay>()?.Toggle()),
                ("労働",   () => UnityEngine.Object.FindAnyObjectByType<LaborObserverOverlay>()?.Toggle()),
                ("教育",   () => UnityEngine.Object.FindAnyObjectByType<EducationObserverOverlay>()?.Toggle()),
                ("事象",   () => UnityEngine.Object.FindAnyObjectByType<ChronicleObserverOverlay>()?.Toggle()),
            }),
            ("経済", new (string, System.Action)[]
            {
                ("財政",   () => UnityEngine.Object.FindAnyObjectByType<EconomyObserverOverlay>()?.Toggle()),
                ("財政詳", () => UnityEngine.Object.FindAnyObjectByType<FiscalObserverOverlay>()?.Toggle()),
                ("生産",   () => UnityEngine.Object.FindAnyObjectByType<ProductionObserverOverlay>()?.Toggle()),
                ("兵站",   () => UnityEngine.Object.FindAnyObjectByType<LogisticsObserverOverlay>()?.Toggle()),
                ("造船",   () => UnityEngine.Object.FindAnyObjectByType<ShipyardObserverOverlay>()?.Toggle()),
                ("研究",   () => UnityEngine.Object.FindAnyObjectByType<ResearchObserverOverlay>()?.Toggle()),
            }),
            ("軍事", new (string, System.Action)[]
            {
                ("軍事",   () => UnityEngine.Object.FindAnyObjectByType<MilitaryObserverOverlay>()?.Toggle()),
                ("艦艇",   () => UnityEngine.Object.FindAnyObjectByType<FleetObserverOverlay>()?.Toggle()),
                ("人物動", () => UnityEngine.Object.FindAnyObjectByType<PersonnelDynamicsObserverOverlay>()?.Toggle()),
            }),
            ("政治", new (string, System.Action)[]
            {
                ("政治",   () => UnityEngine.Object.FindAnyObjectByType<PoliticsObserverOverlay>()?.Toggle()),
                ("外交",   () => UnityEngine.Object.FindAnyObjectByType<DiplomacyObserverOverlay>()?.Toggle()),
                ("人事",   () => UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>()?.Toggle()),
            }),
            ("システム", new (string, System.Action)[]
            {
                ("決裁",     () => UnityEngine.Object.FindAnyObjectByType<DecisionBoardPanel>()?.Toggle()),
                ("稟議",     () => UnityEngine.Object.FindAnyObjectByType<RingiObserverOverlay>()?.Toggle()),
                // 「執務机」はコマンドバーの専用ボタンへ格上げ（BuildMenuBar）＝ここからは除去（重複回避）。
                ("メーター", () => UnityEngine.Object.FindAnyObjectByType<DecisionCampaignDirector>()?.Toggle()),
                ("情報",     () => UnityEngine.Object.FindAnyObjectByType<CoreStateInspector>()?.Toggle()),
                ("通知",     () => UnityEngine.Object.FindAnyObjectByType<NotificationLogOverlay>()?.Toggle()),
                ("ヘルプ",   () => UnityEngine.Object.FindAnyObjectByType<HelpOverlay>()?.Toggle()),
            }),
        };

        /// <summary>
        /// 観測ウィンドウを組む（上メニュー集約・タブ化）。1枚のドラッグ可能な窓に 5タブ（内政/経済/軍事/政治/
        /// システム）を並べ、タブで中身を切り替えて各オブザーバを開く。独立 Canvas（overrideSorting=872）で
        /// マップより前面・初期は閉。各項目は既存オブザーバの Toggle() を呼ぶだけ（オブザーバ窓は不変）。
        /// </summary>
        private void BuildObserverWindow(Transform root)
        {
            var cats = ObserverCategories();

            var winGo = new GameObject("ObserverWindow");
            winGo.transform.SetParent(root, false);
            var win = winGo.AddComponent<RectTransform>();
            win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
            win.pivot = new Vector2(0.5f, 0.5f);
            win.sizeDelta = new Vector2(584f, 380f);
            win.anchoredPosition = new Vector2(0f, 40f);
            var winCanvas = winGo.AddComponent<Canvas>();
            winCanvas.overrideSorting = true; winCanvas.sortingOrder = 872;
            winGo.AddComponent<GraphicRaycaster>();
            var winBg = winGo.AddComponent<Image>();
            winBg.color = new Color(menuBarColor.r, menuBarColor.g, menuBarColor.b, 0.99f);

            // タイトルバー（つかんで移動・×で閉じる）
            var titleGo = new GameObject("TitleBar");
            titleGo.transform.SetParent(win, false);
            var titleRT = titleGo.AddComponent<RectTransform>();
            SetAnchors(titleRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 0f));
            var titleImg = titleGo.AddComponent<Image>();
            titleImg.color = titleBarColor;
            var drag = titleGo.AddComponent<UIDragMove>();
            drag.target = win;
            var titleTxt = AddText(titleGo.transform, "≡ 観測ウィンドウ", 15f, accentColor, TextAlignmentOptions.Left);
            SetAnchors(titleTxt.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-40f, 0f));
            var closeBtn = MakeButton(titleGo.transform, "Close", "×", 18f, TextAlignmentOptions.Center,
                () => { if (winGo != null) winGo.SetActive(false); }, out _);
            SetAnchors((RectTransform)closeBtn.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-32f, 3f), new Vector2(-4f, -3f));

            // タブ行
            var tabRowGo = new GameObject("TabRow");
            tabRowGo.transform.SetParent(win, false);
            var tabRT = tabRowGo.AddComponent<RectTransform>();
            SetAnchors(tabRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -66f), new Vector2(-6f, -32f));
            var tabHlg = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 4f; tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.childControlWidth = true; tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true; tabHlg.childForceExpandHeight = true;

            // コンテンツ領域（各ページを重ね active のみ表示）
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(win, false);
            var contentRT2 = contentGo.AddComponent<RectTransform>();
            SetAnchors(contentRT2, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -70f));

            for (int c = 0; c < cats.Length; c++)
            {
                int idx = c;
                var tabBtn = MakeButton(tabRowGo.transform, "Tab_" + cats[c].name, cats[c].name, 15f,
                    TextAlignmentOptions.Center, () => SetActiveTab(idx), out _);
                tabButtons.Add(tabBtn);

                var pageGo = new GameObject("Page_" + cats[c].name);
                pageGo.transform.SetParent(contentGo.transform, false);
                var pageRT = pageGo.AddComponent<RectTransform>();
                SetAnchors(pageRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var grid = pageGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(130f, 34f);
                grid.spacing = new Vector2(8f, 8f);
                grid.padding = new RectOffset(6, 6, 6, 6);
                grid.childAlignment = TextAnchor.UpperLeft;

                var items = cats[c].items;
                for (int k = 0; k < items.Length; k++)
                {
                    System.Action act = items[k].action; // クロージャ捕捉
                    MakeButton(pageGo.transform, "It_" + items[k].label, items[k].label, 15f,
                        TextAlignmentOptions.Center, () => act?.Invoke(), out _);
                }
                tabPages.Add(pageGo);
            }

            observerWindow = winGo;
            observerWindow.SetActive(false);
            SetActiveTab(0);
        }

        /// <summary>観測ウィンドウの開閉（上メニューの「観測」ボタン）。開くとき最前面へ。</summary>
        private void ToggleObserverWindow()
        {
            if (observerWindow == null) return;
            bool open = !observerWindow.activeSelf;
            observerWindow.SetActive(open);
            if (open) observerWindow.transform.SetAsLastSibling();
        }

        /// <summary>アクティブタブを切り替える（該当ページのみ表示・タブの強調を更新）。</summary>
        private void SetActiveTab(int idx)
        {
            activeTab = idx;
            for (int i = 0; i < tabPages.Count; i++)
                if (tabPages[i] != null) tabPages[i].SetActive(i == idx);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] == null) continue;
                var cb = tabButtons[i].colors;
                cb.normalColor = (i == idx) ? new Color(0.27f, 0.45f, 0.68f, 1f) : buttonColor;
                cb.selectedColor = cb.normalColor;
                tabButtons[i].colors = cb;
            }
        }

        /// <summary>上メニューバーの固定幅ボタン（HLG 内・LayoutElement で幅を固定）。</summary>
        private void MakeBarButton(Transform parent, string label, float width, System.Action onClick)
        {
            var btn = MakeButton(parent, "Cmd_" + label, label, 16f, TextAlignmentOptions.Center, onClick, out _);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;
        }

        /// <summary>汎用ボタン（背景＋ラベル＋色遷移）。<paramref name="bg"/> で背景 Image を受け取る（タブ強調等）。</summary>
        private Button MakeButton(Transform parent, string name, string label, float fontSize,
            TextAlignmentOptions align, System.Action onClick, out Image bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            bg = go.AddComponent<Image>();
            bg.color = Color.white;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor = buttonColor;
            cb.highlightedColor = new Color(0.30f, 0.48f, 0.70f, 1f);
            cb.pressedColor = new Color(0.20f, 0.36f, 0.58f, 1f);
            cb.selectedColor = buttonColor;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            float padL = align == TextAlignmentOptions.Left ? 10f : 4f;
            var t = AddText(go.transform, label, fontSize, new Color(0.92f, 0.95f, 1f), align);
            SetAnchors(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(padL, 0f), new Vector2(-4f, 0f));
            return btn;
        }

        private static Image AddBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) tmp.font = ja;
            return tmp;
        }
    }
}
