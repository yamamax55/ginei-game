using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 決裁駆動キャンペーンの司令塔（決裁ゲーム MVP・ハイブリッド）。<b>プレイヤーの唯一のレバー＝決裁デスク</b>で
    /// マクロメーター（<see cref="CampaignMeters"/>＝軍事/民心/国庫/統制＋覇権）を動かし、いずれかの柱が尽きれば敗北・
    /// 覇権が満ちれば勝利（<see cref="CampaignMeterRules"/>）。裏の戦略シム（支配率）は <see cref="CampaignMeters.control"/>/
    /// <see cref="CampaignMeters.hegemony"/> を緩やかに引く（観てなくても戦況が効く）。何もしないと国庫/民心が痩せる
    /// 受動圧（＝決裁し続けないと負ける）。決着で <see cref="CampaignEndOverlay"/> を出す。Strategy シーンへ自動生成（手配線不要）。
    /// 数式・状態は Core 窓口へ委譲（並行新設しない）。メーターHUDも本コンポーネントが生成・更新（フィードバックの要）。
    /// </summary>
    public class DecisionCampaignDirector : MonoBehaviour
    {
        /// <summary>現在のマクロメーター（観測/UI から読めるよう公開・シーン跨ぎで保持）。</summary>
        public static readonly CampaignMeters Meters = new CampaignMeters();

        [Header("ハイブリッド（裏の戦略シム→メーター）")]
        [Tooltip("この game-秒ごとにドリフト/受動圧を適用する")]
        public float driftInterval = 2f;
        [Tooltip("統制を実際の支配率へ近づける速さ（0..1・interval ごと）")]
        [Range(0f, 1f)] public float controlToTerritory = 0.04f;
        [Tooltip("覇権を支配率へ引き上げる速さ（領土も勝利に寄与・決裁が主・領土は従）")]
        [Range(0f, 1f)] public float hegemonyFromTerritory = 0.01f;
        [Header("実シミュのミラー（軍事/民心/国庫＝観測層と連動）")]
        [Tooltip("メーターを実シミュ値へ寄せる速さ（0..1・interval ごと）")]
        [Range(0f, 1f)] public float mirrorRate = 0.25f;
        [Tooltip("国庫メーターの正規化基準額。残高0=中立50%・+この額で100%/−この額で0%")]
        public float treasuryFullReference = 2000f;

        [Header("決裁→メーター")]
        [Tooltip("裁可（先頭の選択肢）したときメーター増減に掛ける倍率")]
        [Range(0f, 1f)] public float approveScale = 1f;

        private float accum;
        private bool ended;
        private CampaignState lastCampaign; // 新キャンペーン検出＝メーターをリセットする

        // HUD 参照
        private readonly List<Bar> bars = new List<Bar>();
        private TMP_FontAsset jpFont;
        private Transform hudRoot;         // メーターHUDのCanvas（詳細ウィンドウの親）
        private RectTransform windowRoot; // メーターウィンドウ枠（表示トグル対象）
        private GameObject meterPanel;     // 本文（最小化で畳む対象）

        // 関連数値ウィンドウ（バーのダブルクリックで開く・1枚を使い回し）
        private GameObject detailWindow;
        private TextMeshProUGUI detailBody;
        private string detailPillar;

        private class Bar
        {
            public System.Func<float> value;
            public RectTransform fill;
            public RectTransform root; // 列全体の矩形（ダブルクリック当たり判定用）
            public TextMeshProUGUI label;
            public string name;
            public float width;
        }

        // メーター直読みダブルクリック（uGUI イベントに依存しない確実な経路）
        [Tooltip("バーの連続クリックをダブルクリックと見なす最大間隔（秒・実時間）")]
        public float meterDoubleClickInterval = 0.35f;
        private float lastMeterClickTime = -999f;
        private Vector2 lastMeterClickPos;

        // ===== 自動生成 =====

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
            if (scene.name != "Strategy") return; // 戦役でのみ
            if (FindAnyObjectByType<DecisionCampaignDirector>() != null) return;
            new GameObject("DecisionCampaignDirector").AddComponent<DecisionCampaignDirector>();
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            DecisionDeck.Resolved += OnResolved;
            BuildHud();
        }

        private void OnDestroy() => DecisionDeck.Resolved -= OnResolved;

        private void Update()
        {
            var camp = StrategySession.Campaign;
            if (camp == null) return;

            // 新しいキャンペーンを検出したらメーターを初期化（Battle 往復では同一インスタンス＝保持）
            if (!ReferenceEquals(camp, lastCampaign))
            {
                ResetMeters();
                lastCampaign = camp;
            }

            // game-時間でドリフト/受動圧（ポーズ中は進まない・倍速で速まる）
            GameClock clock = StrategySession.Clock;
            float gdt = clock != null ? (float)clock.EffectiveDt(Time.unscaledDeltaTime) : Time.deltaTime;
            accum += gdt;
            if (accum >= driftInterval)
            {
                accum = 0f;
                ApplyDrift();
            }

            HandleMeterDoubleClick();
            UpdateHud();
            EvaluateOutcome();
        }

        /// <summary>
        /// メーターのバーのダブルクリックを<b>マウス直読み</b>で検出する（uGUI の EventSystem/レイキャストに依存しない）。
        /// マップ（<see cref="GalaxyView"/>）と同方式＝この経路は確実に動く。バー矩形に入っていれば該当の柱の詳細を開く。
        /// </summary>
        private void HandleMeterDoubleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (windowRoot == null || !windowRoot.gameObject.activeSelf) return; // 非表示中は無視
            if (meterPanel == null || !meterPanel.activeSelf) return;            // 最小化中は無視

            Vector2 pos = mouse.position.ReadValue();
            float now = Time.unscaledTime;
            bool isDouble = (now - lastMeterClickTime) <= meterDoubleClickInterval
                            && (pos - lastMeterClickPos).sqrMagnitude <= 12f * 12f;

            if (!isDouble)
            {
                lastMeterClickTime = now;
                lastMeterClickPos = pos;
                return;
            }

            lastMeterClickTime = -999f; // 次の単発から数え直す
            for (int i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                if (b.root != null && RectTransformUtility.RectangleContainsScreenPoint(b.root, pos, null))
                {
                    ShowDetail(b.name);
                    break;
                }
            }
        }

        // ===== 決裁→メーター =====

        private void OnResolved(PendingDecision d, int choiceIndex)
        {
            if (d == null) return;
            // 先頭の選択肢（裁可/締結など能動側）でのみメーターを動かす。見送り（既定=現状維持）は受動圧に任せる。
            if (choiceIndex != 0) return;
            if (!DecisionMeterEffects.TryGet(d.effectKey, out var delta)) return;

            CampaignMeterRules.Apply(Meters, delta, approveScale);
        }

        // ===== ハイブリッド（裏のシム→メーター） =====

        private void ApplyDrift()
        {
            // 統制は実際の支配率へ寄る／覇権は支配率ぶんを従として積む（領土＝裏の戦略シム）
            float owned = OwnedFraction();
            Meters.control += (owned - Meters.control) * controlToTerritory;
            if (owned > Meters.hegemony)
                Meters.hegemony += (owned - Meters.hegemony) * hegemonyFromTerritory;

            // 軍事/民心/国庫は実シミュ値（観測層が映すもの）へ収束＝メーターと観測層を連動（ミラー化）。
            // 受動ドリフトは廃止し、実データの鏡にする（決裁の効果は実シミュへ波及したぶんがここに映る）。
            if (TryGetMirrorTargets(out float mil, out float sup, out float tre))
            {
                Meters.military += (mil - Meters.military) * mirrorRate;
                Meters.support += (sup - Meters.support) * mirrorRate;
                Meters.treasury += (tre - Meters.treasury) * mirrorRate;
            }

            CampaignMeterRules.Clamp(Meters);
        }

        private float OwnedFraction()
        {
            var map = StrategySession.Map;
            if (map == null) return Meters.control; // 盤面が無ければ現状維持
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            return CampaignVictoryRules.OwnedFraction(map, pf);
        }

        /// <summary>
        /// 軍事/民心/国庫メーターの目標値を実シミュ（観測層が映す値）から導く＝<b>ミラー化</b>の単一窓口。
        /// 民心←共同体の希望(0..1)／国庫←国庫残高を基準額で正規化(負債=0)／軍事←自勢力の保有艦艇シェア。
        /// 値が取れなければ false（現状維持）。
        /// </summary>
        private bool TryGetMirrorTargets(out float military, out float support, out float treasury)
        {
            military = support = treasury = 0f;
            CampaignState camp = StrategySession.Campaign;
            if (camp == null) return false;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            FactionState s = CampaignRules.GetState(camp, pf);
            if (s == null) return false;

            support = s.community != null ? Mathf.Clamp01(s.community.hope) : Meters.support;
            // 国庫＝残高を中立0.5基準で正規化（黒字で↑/赤字で↓・±基準額で1/0）。残高0=中立50%＝開始直後の低残高で誤って枯渇敗北しない。
            treasury = treasuryFullReference > 0f ? Mathf.Clamp01(0.5f + s.treasury / (2f * treasuryFullReference)) : Meters.treasury;
            military = MilitaryShare(pf);
            return true;
        }

        /// <summary>自勢力の保有艦艇シェア（自 ÷ 全勢力合計＝0..1・拮抗で0.5）。データ無しは現状維持。</summary>
        private float MilitaryShare(Faction pf)
        {
            int own = FleetPool.Get(pf);
            int total = 0;
            foreach (Faction f in System.Enum.GetValues(typeof(Faction)))
                total += FleetPool.Get(f);
            if (total <= 0) return Meters.military; // 盤面に艦艇プールが無ければ現状維持
            return Mathf.Clamp01((float)own / total);
        }

        // ===== 勝敗 =====

        private void EvaluateOutcome()
        {
            if (ended || CampaignEndOverlay.IsOpen) return;
            var outcome = CampaignMeterRules.Evaluate(Meters, out string reason);
            if (outcome == CampaignOutcome.継続) return;

            ended = true;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            bool win = outcome == CampaignOutcome.勝利;
            NotificationCenter.Push(NotificationCategory.システム,
                win ? NotificationSeverity.情報 : NotificationSeverity.警告,
                win ? $"勝利：{reason}" : $"敗北：{reason}");

            var clock = StrategySession.Clock;
            clock?.Pause();
            CampaignEndOverlay.Show(win, pf, Meters.hegemony, reason); // 決着理由（柱の崩壊/覇権）を正しく表示
        }

        /// <summary>メーターを初期値へ戻す（新キャンペーン開始時）。</summary>
        public static void ResetMeters()
        {
            Meters.military = 0.5f;
            Meters.support = 0.5f;
            Meters.treasury = 0.5f;
            Meters.control = 0.5f;
            Meters.hegemony = 0f;
        }

        // ===== HUD =====

        private void BuildHud()
        {
            var canvasObj = new GameObject("DecisionMeterHudCanvas");
            canvasObj.transform.SetParent(transform);
            hudRoot = canvasObj.transform;
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 870; // NotificationFeed(880)/DecisionDeck(885) より僅か後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // ウィンドウ枠（既定＝右上・上メニュー直下／タイトルバーでドラッグ移動・最小化・×で隠す）。
            // 右下は決裁デスク(DecisionDeck)の定位置と重なるため右上へ（× で隠したら上メニュー「メーター」で復帰）。
            var window = new GameObject("MeterWindow");
            window.transform.SetParent(canvasObj.transform, false);
            windowRoot = window.AddComponent<RectTransform>();
            windowRoot.anchorMin = new Vector2(1f, 1f);
            windowRoot.anchorMax = new Vector2(1f, 1f);
            windowRoot.pivot = new Vector2(1f, 1f);
            windowRoot.anchoredPosition = new Vector2(-16f, -116f); // 右上＝上メニュー(0.10×1080≈108)の直下
            var wbg = window.AddComponent<Image>();
            wbg.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);
            var wvlg = window.AddComponent<VerticalLayoutGroup>();
            wvlg.spacing = 0f;
            wvlg.childControlWidth = true; wvlg.childControlHeight = true;
            wvlg.childForceExpandWidth = true; wvlg.childForceExpandHeight = false;
            var wfitter = window.AddComponent<ContentSizeFitter>();
            wfitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            wfitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // メーター本体（横並びバー）＝ウィンドウ本文。サイズは LayoutElement で明示（入れ子 CSF を避ける）。
            meterPanel = new GameObject("MeterPanel");
            meterPanel.transform.SetParent(window.transform, false);
            meterPanel.AddComponent<RectTransform>();
            var hlg = meterPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 6, 6);
            hlg.spacing = 10f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            var ple = meterPanel.AddComponent<LayoutElement>();
            ple.preferredWidth = 660f;  // 5バー×120 + 間隔4×10 + 余白20
            ple.preferredHeight = 50f;

            AddBar(meterPanel.transform, "軍事", new Color(0.85f, 0.35f, 0.30f), () => Meters.military);
            AddBar(meterPanel.transform, "民心", new Color(0.35f, 0.78f, 0.45f), () => Meters.support);
            AddBar(meterPanel.transform, "国庫", new Color(0.90f, 0.78f, 0.30f), () => Meters.treasury);
            AddBar(meterPanel.transform, "統制", new Color(0.45f, 0.62f, 0.95f), () => Meters.control);
            AddBar(meterPanel.transform, "覇権", new Color(0.80f, 0.55f, 0.95f), () => Meters.hegemony);

            // タイトルバー（VLG 先頭へ差し込み）：× は隠す／－は本文を畳む。
            WindowChrome.AddTitleBarLayout(windowRoot, "勝敗メーター",
                () => SetVisible(false),
                collapsed => { if (meterPanel != null) meterPanel.SetActive(!collapsed); });
        }

        /// <summary>メーターウィンドウの表示/非表示。上メニュー「メーター」から呼ぶ。</summary>
        public void SetVisible(bool visible)
        {
            if (windowRoot != null) windowRoot.gameObject.SetActive(visible);
        }

        /// <summary>メーターウィンドウの表示トグル（上メニュー「メーター」用）。</summary>
        public void Toggle()
        {
            if (windowRoot != null) windowRoot.gameObject.SetActive(!windowRoot.gameObject.activeSelf);
        }

        private void AddBar(Transform parent, string name, Color color, System.Func<float> value)
        {
            const float barWidth = 120f;

            var col = new GameObject("Meter_" + name);
            col.transform.SetParent(parent, false);
            var cvlg = col.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 2f;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            cvlg.childAlignment = TextAnchor.UpperCenter;
            var cle = col.AddComponent<LayoutElement>();
            cle.preferredWidth = barWidth;

            // ほぼ透明な背景＝レイキャスト面（ラベルの隙間も拾う）＋ダブルクリックで関連数値ウィンドウを開く。
            // 完全な alpha=0 はカリングで拾われない環境があるため極僅かに不透明にする。
            var colBg = col.AddComponent<Image>();
            colBg.color = new Color(0f, 0f, 0f, 1f / 255f);
            colBg.raycastTarget = true;
            string pillarName = name;
            col.AddComponent<MeterBarClick>().onDoubleClick = () => ShowDetail(pillarName);

            var label = AddLabel(col.transform, name, 15f, new Color(0.85f, 0.9f, 0.95f));
            label.alignment = TextAlignmentOptions.Center;

            // バー（背景＋フィル）
            var track = new GameObject("Track");
            track.transform.SetParent(col.transform, false);
            var timg = track.AddComponent<Image>();
            timg.color = new Color(0f, 0f, 0f, 0.5f);
            var tle = track.AddComponent<LayoutElement>();
            tle.preferredWidth = barWidth; tle.minHeight = 14f; tle.preferredHeight = 14f;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(track.transform, false);
            var fimg = fillGo.AddComponent<Image>();
            fimg.color = color;
            var fill = fillGo.GetComponent<RectTransform>();
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.sizeDelta = new Vector2(barWidth, 0f);

            bars.Add(new Bar { value = value, fill = fill, root = (RectTransform)col.transform, label = label, name = name, width = barWidth });
        }

        private void UpdateHud()
        {
            for (int i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                float v = Mathf.Clamp01(b.value());
                b.fill.sizeDelta = new Vector2(b.width * v, 0f);
                // 覇権は％、柱は枯渇が近いと赤で警告
                bool isHegemony = b.name == "覇権";
                b.label.text = $"{b.name} {Mathf.RoundToInt(v * 100f)}";
                if (!isHegemony && v <= 0.15f)
                    b.label.color = Color.Lerp(new Color(1f, 0.3f, 0.3f), Color.white,
                        0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
                else
                    b.label.color = new Color(0.85f, 0.9f, 0.95f);
            }

            if (detailWindow != null && detailWindow.activeSelf)
                detailBody.text = BuildDetailText(detailPillar);
        }

        // ===== 関連数値ウィンドウ（バーのダブルクリック） =====

        /// <summary>指定の柱の関連数値ウィンドウを開く（無ければ生成）。</summary>
        private void ShowDetail(string pillar)
        {
            detailPillar = pillar;
            if (detailWindow == null) BuildDetailWindow();
            detailWindow.SetActive(true);
            detailWindow.transform.SetAsLastSibling(); // 最前面へ
            detailBody.text = BuildDetailText(detailPillar);
        }

        private void BuildDetailWindow()
        {
            const float winW = 320f, winH = 210f, titleInset = 30f;

            var win = new GameObject("MeterDetailWindow");
            // メーター本体(windowRoot)は確実にレンダリングできている＝その親が生きた Canvas。
            // hudRoot が何らかの理由で無効でも Canvas 外に作られないよう、実証済みの親を使う。
            Transform parent = (windowRoot != null && windowRoot.parent != null) ? windowRoot.parent : hudRoot;
            win.transform.SetParent(parent, false);
            var rt = win.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(winW, winH);            // 固定サイズ＝レイアウト依存をなくし確実に表示
            rt.anchoredPosition = new Vector2(-16f, -206f);    // メーターウィンドウの直下（右上）
            var bg = win.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.97f);

            // 本文＝枠いっぱい（タイトルバーぶん上を空ける）。VLG/ContentSizeFitter を使わず固定矩形で配置。
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(win.transform, false);
            detailBody = bodyGo.AddComponent<TextMeshProUGUI>();
            detailBody.fontSize = 15f;
            detailBody.color = new Color(0.85f, 0.9f, 0.95f);
            detailBody.richText = true;
            detailBody.overflowMode = TextOverflowModes.Overflow;
            detailBody.alignment = TextAlignmentOptions.TopLeft;
            if (jpFont != null) detailBody.font = jpFont;
            var bodyRT = detailBody.rectTransform;
            bodyRT.anchorMin = new Vector2(0f, 0f);
            bodyRT.anchorMax = new Vector2(1f, 1f);
            bodyRT.offsetMin = new Vector2(12f, 10f);          // 左・下の余白
            bodyRT.offsetMax = new Vector2(-12f, -titleInset); // 右・上（タイトルバーぶん）

            // タイトルバー（ドラッグ移動・×で隠す）。固定サイズ枠なのでアンカー版を使う。
            WindowChrome.AddTitleBarAnchored(rt, "メーター詳細", () => detailWindow.SetActive(false));

            detailWindow = win;
        }

        /// <summary>柱ごとの関連数値テキストを実シミュ値から組む（ライブ更新）。</summary>
        private string BuildDetailText(string pillar)
        {
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            CampaignState camp = StrategySession.Campaign;
            FactionState s = camp != null ? CampaignRules.GetState(camp, pf) : null;
            GalaxyMap map = StrategySession.Map;
            var sb = new System.Text.StringBuilder();

            switch (pillar)
            {
                case "軍事":
                    sb.Append("<b>【軍事】艦艇シェア</b>\n");
                    sb.Append($"メーター：{Mathf.RoundToInt(Meters.military * 100f)}\n");
                    {
                        int own = FleetPool.Get(pf);
                        int total = 0;
                        foreach (Faction f in System.Enum.GetValues(typeof(Faction))) total += FleetPool.Get(f);
                        sb.Append($"自勢力 {pf}：{own:#,0} 隻\n");
                        foreach (Faction f in System.Enum.GetValues(typeof(Faction)))
                            if (f != pf) sb.Append($"　{f}：{FleetPool.Get(f):#,0} 隻\n");
                        sb.Append($"シェア：{(total > 0 ? 100f * own / total : 0f):0} ％（自÷全勢力）");
                    }
                    break;

                case "民心":
                    sb.Append("<b>【民心】希望</b>\n");
                    sb.Append($"メーター：{Mathf.RoundToInt(Meters.support * 100f)}\n");
                    if (s != null)
                    {
                        sb.Append($"希望 hope：{(s.community != null ? s.community.hope : 0f):0.00}\n");
                        sb.Append($"高税の不満：{FiscalRules.TaxBurdenPenalty(s.taxRate):0.00}\n");
                        sb.Append($"包摂度：{s.inclusiveness:0.00}（収奪0↔包摂1）");
                    }
                    else sb.Append("（キャンペーン未開始）");
                    break;

                case "国庫":
                    sb.Append("<b>【国庫】財政</b>\n");
                    sb.Append($"メーター：{Mathf.RoundToInt(Meters.treasury * 100f)}\n");
                    if (s != null)
                    {
                        float eco = CampaignRules.EconomyBase(s);
                        sb.Append($"国庫残高：{s.treasury:#,0}\n");
                        sb.Append($"税率：{Mathf.RoundToInt(s.taxRate * 100f)} ％\n");
                        sb.Append($"課税ベース：{eco:0.00}\n");
                        sb.Append($"税収/秒：{FiscalRules.TaxRevenue(eco, s.taxRate):0.00}\n");
                        sb.Append($"正規化基準：±{treasuryFullReference:#,0}（残高0=50％）");
                    }
                    else sb.Append("（キャンペーン未開始）");
                    break;

                case "統制":
                    sb.Append("<b>【統制】支配率</b>\n");
                    sb.Append($"メーター：{Mathf.RoundToInt(Meters.control * 100f)}\n");
                    if (map != null)
                    {
                        sb.Append($"所有星系：{CampaignVictoryRules.OwnedCount(map, pf)} / {CampaignVictoryRules.TotalSystems(map)}\n");
                        sb.Append($"支配率：{CampaignVictoryRules.OwnedFraction(map, pf) * 100f:0} ％\n");
                        sb.Append($"最大敵支配率：{CampaignVictoryRules.MaxRivalFraction(map, pf) * 100f:0} ％");
                    }
                    else sb.Append("（盤面なし）");
                    break;

                case "覇権":
                    sb.Append("<b>【覇権】勝利進捗</b>\n");
                    sb.Append($"覇権：{Mathf.RoundToInt(Meters.hegemony * 100f)} ％\n");
                    if (map != null)
                        sb.Append($"支配率：{CampaignVictoryRules.OwnedFraction(map, pf) * 100f:0} ％\n");
                    sb.Append($"勝利しきい値：{ActiveDominationFraction() * 100f:0} ％");
                    break;
            }
            return sb.ToString();
        }

        /// <summary>現在の難易度に応じた制覇しきい値（覇権ウィンドウ表示用・<see cref="StrategyMapWindow"/> と同じ窓口）。</summary>
        private static float ActiveDominationFraction()
            => CampaignDifficultyRules.VictoryParams(
                GameSettings.Instance != null ? GameSettings.Instance.campaignDifficulty : CampaignDifficulty.普通).dominationFraction;

        private TextMeshProUGUI AddLabel(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            if (jpFont != null) label.font = jpFont;
            return label;
        }
    }
}
