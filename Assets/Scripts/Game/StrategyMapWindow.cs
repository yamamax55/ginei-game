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

        // マップ窓の正規化矩形（画面全体を 0〜1 とした位置/大きさ）。camera.rect と窓UIの両方に使う＝必ず一致。
        private Rect mapRect = new Rect(0.02f, 0.03f, 0.96f, 0.83f);

        private Camera cam;
        private Camera bgCam;
        private Rect originalRect;
        private bool rectApplied;

        private RectTransform titleBarRT;
        private RectTransform contentRT;
        private RectTransform edgeLeft, edgeRight, edgeBottom;
        private TextMeshProUGUI clockLabel;
        private TextMeshProUGUI statsLabel;

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
            if (bgCam != null) Destroy(bgCam.gameObject);
            GalaxyView.HideWorldHud = false;
        }

        private void Update()
        {
            TimeDisplay.StepSpeedInput();
            if (clockLabel != null && TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockLabel.text = text;
                clockLabel.color = color;
            }
            UpdateStatsLabel();

            // 目標（勝利進捗＋次の一手）は間引いて更新（毎フレーム再計算しない＝終盤ラグ規律）。
            objectiveTimer += Time.unscaledDeltaTime;
            if (objectiveTimer >= ObjectiveInterval) { objectiveTimer = 0f; UpdateObjective(); }
        }

        private void LateUpdate() => ApplyLayout();

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

        /// <summary>mapRect を camera.rect と窓UIのアンカーへ反映（両者とも画面全体基準＝一致）。</summary>
        private void ApplyLayout()
        {
            if (cam == null) return;
            mapRect.width = Mathf.Clamp(mapRect.width, 0.15f, 1f);
            mapRect.height = Mathf.Clamp(mapRect.height, 0.15f, 1f);
            mapRect.x = Mathf.Clamp(mapRect.x, 0f, 1f - mapRect.width);
            mapRect.y = Mathf.Clamp(mapRect.y, 0f, 1f - mapRect.height);
            cam.rect = mapRect;

            float x0 = mapRect.xMin, x1 = mapRect.xMax, y0 = mapRect.yMin, y1 = mapRect.yMax;

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

            statsLabel = AddText(top, "", 16f, new Color(0.85f, 0.9f, 0.7f), TextAlignmentOptions.Center);
            SetAnchors(statsLabel.rectTransform, new Vector2(0.18f, 0f), new Vector2(0.74f, 1f), Vector2.zero, Vector2.zero);

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

            AddCommand(cmd.transform, "勢力", () => UnityEngine.Object.FindAnyObjectByType<CampaignObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "財政", () => UnityEngine.Object.FindAnyObjectByType<EconomyObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "軍事", () => UnityEngine.Object.FindAnyObjectByType<MilitaryObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "人事", () => UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "解決", () => UnityEngine.Object.FindAnyObjectByType<DecisionBoardPanel>()?.Toggle());
            AddCommand(cmd.transform, "情報", () => UnityEngine.Object.FindAnyObjectByType<CoreStateInspector>()?.Toggle());
            AddCommand(cmd.transform, "通知", () => UnityEngine.Object.FindAnyObjectByType<NotificationLogOverlay>()?.Toggle());
            AddCommand(cmd.transform, "ヘルプ", () => UnityEngine.Object.FindAnyObjectByType<HelpOverlay>()?.Toggle());

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
            SetAnchors(cap.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));

            // 中身領域（透明＝マップを見せる・クリックを塞がない）。アンカーは ApplyLayout で mapRect に合わせる。
            contentRT = new GameObject("MapContent").AddComponent<RectTransform>();
            contentRT.transform.SetParent(root, false);

            // 縁取り（細い金色バー・raycast しない）
            Color edge = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
            edgeLeft = MakeEdge(root, edge);
            edgeRight = MakeEdge(root, edge);
            edgeBottom = MakeEdge(root, edge);
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

        private void UpdateStatsLabel()
        {
            if (statsLabel == null) return;
            CampaignState campaign = StrategySession.Campaign;
            if (campaign == null) { statsLabel.text = ""; return; }
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            FactionState s = CampaignRules.GetState(campaign, pf);
            if (s == null) { statsLabel.text = ""; return; }
            float hope = s.community != null ? s.community.hope : 0f;
            float stab = CampaignRules.EffectiveStability(campaign, pf);
            statsLabel.text = $"税率 {s.taxRate * 100f:0}%　国庫 {s.treasury:0}　民心 {hope * 100f:0}%　安定度 {stab * 100f:0}%";
            statsLabel.color = hope < 0.2f ? new Color(1f, 0.55f, 0.45f) : new Color(0.85f, 0.9f, 0.7f);
        }

        // ===== 目標（勝利進捗＋次の一手）＝B：目的可視化 =====

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

            // 勝利しきい値（既定60%）の縦マーカー。
            float winMark = CampaignVictoryRules.CampaignVictoryParams.Default.dominationFraction;
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
            float winFrac = CampaignVictoryRules.CampaignVictoryParams.Default.dominationFraction;
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
                case CampaignHint.任務を発令: return "C: 攻略任務を発令 ／ B: 艦隊を編成";
                case CampaignHint.領土を広げよ: return "艦隊を選んで右クリックで敵星系へ進軍";
                default: return "好機を待つ";
            }
        }

        // ===== 部品 =====

        private void AddCommand(Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject("Cmd_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = buttonColor;
            cb.highlightedColor = new Color(0.27f, 0.45f, 0.68f, 1f);
            cb.pressedColor = new Color(0.20f, 0.36f, 0.58f, 1f);
            cb.selectedColor = buttonColor;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 92f; le.preferredWidth = 92f; le.flexibleWidth = 0f;

            var t = AddText(go.transform, label, 16f, new Color(0.92f, 0.95f, 1f), TextAlignmentOptions.Center);
            SetAnchors(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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
