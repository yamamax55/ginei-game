using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 決裁デスク（DESK-4/5 #1632/#1633）。イベント/決裁を<b>時間を止めずに右下へ積み上げる</b>能動フィード
    /// （左下 <see cref="NotificationFeed"/> の能動版）。<see cref="DecisionTriageRules"/> を game-時間で回し、
    /// 規定時間で最小化→さらに猶予超で AI が既定選択を機械的に採択（自動解決）。<b>重大決裁だけ</b>
    /// <see cref="GameClock"/> を止める（アクティブポーズ）。各カードは <b>本文の開閉（▾）・最小化（─）</b> を持ち、
    /// デッキ上部の<b>ドラッグハンドルで自由に移動</b>できる（<see cref="UIDragMove"/>）。
    /// 効果の実適用（effectKey→世界）は DESK-6（合流）で。Strategy/Battle へ自動生成。
    /// </summary>
    public class DecisionDeck : MonoBehaviour
    {
        [Header("デモ")]
        [Tooltip("起動時にサンプル決裁を積む（重大で時間停止／通常が締切で最小化→自動選択 を体感）")]
        public bool spawnDemoDecisions = true;

        [Header("旧イベント抑制（DESK 移行）")]
        [Tooltip("旧 StrategyEventPanel（中央モーダル）を抑制し決裁デスクへ集約する。DESK 移行中は既定 true。F9 で実行時トグル")]
        public bool suppressLegacyEventPanel = true;

        [Header("外観")]
        public int canvasSortingOrder = 885;       // NotificationFeed(880) より僅かに前・モーダル(900+)より後ろ
        public float cardWidth = 380f;
        public int maxVisibleCards = 5;

        /// <summary>共有の決裁キュー（他システムは <see cref="Enqueue"/> で積む）。</summary>
        public static DecisionQueue Queue { get; private set; } = new DecisionQueue();

        /// <summary>決裁を積む単一窓口（イベント/目安箱の諮問・裁可がここへ流す＝DESK-6）。</summary>
        public static void Enqueue(PendingDecision d)
        {
            if (Queue == null) Queue = new DecisionQueue();
            Queue.Enqueue(d);
        }

        private RectTransform container;
        private GameObject dragHandle;
        private TMP_FontAsset jpFont;
        private bool deskPausedClock;
        private string lastSignature = "";
        private readonly HashSet<int> collapsed = new HashSet<int>(); // 本文を畳んだ決裁id（要約のみ表示）

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
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<DecisionDeck>() != null) return;
            new GameObject("DecisionDeck").AddComponent<DecisionDeck>();
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            // 新システム（決裁デスク）が判断：移行中は旧イベントモーダルを抑制し右下スタックへ集約
            StrategyEventPanel.Enabled = !suppressLegacyEventPanel;
            if (spawnDemoDecisions) SpawnDemo();
        }

        private void Update()
        {
            // デバッグ：F9 で旧イベントモーダルの ON/OFF を実行時切替（決裁デスクへ移行中の確認用）
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                StrategyEventPanel.Enabled = !StrategyEventPanel.Enabled;
                NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意,
                    StrategyEventPanel.Enabled ? "旧イベントモーダル：ON" : "旧イベントモーダル：OFF（決裁デスクに集約）");
            }

            // game-時間で締切を進める（クロックがポーズ＝重大停止中は EffectiveDt が0＝凍結＝時間が止まる）
            GameClock clock = StrategySession.Clock;
            float gdt = clock != null ? (float)clock.EffectiveDt(Time.unscaledDeltaTime) : Time.deltaTime;

            var resolved = DecisionTriageRules.Tick(Queue, gdt);
            for (int i = 0; i < resolved.Count; i++)
            {
                var d = resolved[i];
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                    $"［自動処理］{d.title} → {ChoiceLabel(d, d.chosenIndex)}");
            }
            if (resolved.Count > 0) Queue.PruneResolved(); // 自動解決済みを掃く（溜め込まない）

            // DESK-4：活性な重大が居る間だけ時間を止める（自分が止めたぶんだけ戻す）
            if (clock != null)
            {
                bool shouldStop = DecisionTriageRules.ClockShouldStop(Queue);
                if (shouldStop && !clock.paused) { clock.Pause(); deskPausedClock = true; }
                else if (!shouldStop && deskPausedClock) { clock.Resume(); deskPausedClock = false; }
            }

            // 表示は状態が変わった時だけ作り直す（毎フレームのボタン再生成を避ける＝ドラッグ位置も保つ）
            string sig = Signature();
            if (sig != lastSignature)
            {
                Rebuild();
                lastSignature = sig;
            }

            // デッキに何も無ければハンドルを隠す
            if (dragHandle != null) dragHandle.SetActive(Queue.Count > 0);
        }

        // ===== 表示 =====

        private string Signature()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Queue.items.Count; i++)
            {
                var d = Queue.items[i];
                if (d == null) continue;
                sb.Append(d.id).Append(':').Append((int)d.status).Append('|');
            }
            return sb.ToString();
        }

        private void Rebuild()
        {
            // ドラッグハンドル以外を破棄（ハンドルは位置保持のため残す）
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var c = container.GetChild(i);
                if (c.gameObject == dragHandle) continue;
                Destroy(c.gameObject);
            }

            // 表示するカード（最小化・解決済を除く＝提示中/新着）。重大→経過順で優先。
            var visible = new List<PendingDecision>();
            for (int i = 0; i < Queue.items.Count; i++)
            {
                var d = Queue.items[i];
                if (d == null) continue;
                if (d.status == DecisionStatus.新着 || d.status == DecisionStatus.提示中)
                    visible.Add(d);
            }
            visible.Sort((a, b) =>
            {
                int s = ((int)b.severity).CompareTo((int)a.severity);
                return s != 0 ? s : b.elapsed.CompareTo(a.elapsed);
            });

            int shown = 0;
            for (int i = 0; i < visible.Count && shown < maxVisibleCards; i++, shown++)
            {
                if (visible[i].status == DecisionStatus.新着) visible[i].status = DecisionStatus.提示中;
                BuildCard(visible[i]);
            }

            // 最小化バッジ（最下段＝コーナー寄り）
            int minimized = Queue.MinimizedCount();
            if (minimized > 0) BuildMinimizedBadge(minimized);
        }

        private void BuildCard(PendingDecision d)
        {
            var card = new GameObject("Card_" + d.id);
            card.transform.SetParent(container, false);
            var bg = card.AddComponent<Image>();
            bg.color = CardColor(d.severity);
            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 8);
            vlg.spacing = 4f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = cardWidth;

            // --- ヘッダ行（要約＋本文開閉▾＋最小化─）＝Windowライクなタイトルバー ---
            var header = new GameObject("Header");
            header.transform.SetParent(card.transform, false);
            var hbg = header.AddComponent<Image>();
            hbg.color = new Color(0f, 0f, 0f, 0.25f);
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var hle = header.AddComponent<LayoutElement>();
            hle.minHeight = 30f;

            // 要約（タイトル）＝可変幅で残りを占める
            var summary = AddLabel(header.transform, $"<b>[{d.severity}]</b> {d.title}", 18f,
                d.severity == DecisionSeverity.重大 ? new Color(1f, 0.85f, 0.85f) : Color.white);
            var sle = summary.gameObject.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1f;

            bool hasBody = !string.IsNullOrEmpty(d.body);
            GameObject collapsible = null;

            // 本文の開閉トグル（本文がある時だけ）
            if (hasBody)
            {
                int id = d.id;
                var toggleGo = AddButton(header.transform, collapsed.Contains(id) ? "▾" : "▴",
                    null, small: true, fixedWidth: 34f);
                var toggleTmp = toggleGo.GetComponentInChildren<TextMeshProUGUI>();
                var btn = toggleGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (collapsible == null) return;
                    bool nowCollapsed = collapsible.activeSelf; // 表示中→畳む
                    collapsible.SetActive(!nowCollapsed);
                    if (nowCollapsed) collapsed.Add(id); else collapsed.Remove(id);
                    if (toggleTmp != null) toggleTmp.text = nowCollapsed ? "▾" : "▴";
                });
            }

            // 最小化（─）＝重大以外。重大は手を止めさせる＝最小化させない
            if (d.severity != DecisionSeverity.重大)
            {
                PendingDecision dd = d;
                AddButton(header.transform, "─", () => Queue.Minimize(dd), small: true, fixedWidth: 34f);
            }

            // --- 本文＋選択肢（畳める） ---
            collapsible = new GameObject("Body");
            collapsible.transform.SetParent(card.transform, false);
            var cvlg = collapsible.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 4f; cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;

            if (hasBody)
                AddLabel(collapsible.transform, d.body, 16f, new Color(0.86f, 0.9f, 0.95f));

            for (int i = 0; i < d.choices.Count; i++)
            {
                int idx = i;
                PendingDecision dd = d;
                AddButton(collapsible.transform, d.choices[i], () => ResolveDecision(dd, idx));
            }

            collapsible.SetActive(!collapsed.Contains(d.id)); // 畳み状態を復元（既定＝展開＝本文表示）
        }

        private void BuildMinimizedBadge(int count)
        {
            var badge = new GameObject("MinimizedBadge");
            badge.transform.SetParent(container, false);
            var le = badge.AddComponent<LayoutElement>();
            le.preferredWidth = cardWidth;
            AddButton(badge.transform, $"▾ 保留中の決裁 {count} 件（クリックで展開）", RestoreMinimized, small: true);
        }

        private void RestoreMinimized()
        {
            for (int i = 0; i < Queue.items.Count; i++)
            {
                var d = Queue.items[i];
                if (d != null && d.status == DecisionStatus.最小化) Queue.Restore(d);
            }
        }

        private void ResolveDecision(PendingDecision d, int choiceIndex)
        {
            if (d == null) return;
            Queue.Resolve(d, choiceIndex);
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                $"［裁可］{d.title} → {ChoiceLabel(d, choiceIndex)}");
            Queue.PruneResolved(); // 決裁済みを掃く
            // effectKey→世界 の実適用は DESK-6（イベント/目安箱の合流）で。
        }

        private static string ChoiceLabel(PendingDecision d, int idx)
        {
            if (d == null || d.choices == null || idx < 0 || idx >= d.choices.Count) return "（既定）";
            return d.choices[idx];
        }

        private static Color CardColor(DecisionSeverity s)
        {
            switch (s)
            {
                case DecisionSeverity.重大: return new Color(0.45f, 0.10f, 0.12f, 0.96f);
                case DecisionSeverity.重要: return new Color(0.42f, 0.30f, 0.08f, 0.94f);
                case DecisionSeverity.通常: return new Color(0.10f, 0.20f, 0.32f, 0.92f);
                default: return new Color(0.16f, 0.18f, 0.22f, 0.90f);
            }
        }

        // ===== UI 部品 =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("DecisionDeckCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            var cont = new GameObject("DeckContainer");
            cont.transform.SetParent(canvasObj.transform, false);
            container = cont.AddComponent<RectTransform>();
            container.anchorMin = new Vector2(1f, 0f); // 右下
            container.anchorMax = new Vector2(1f, 0f);
            container.pivot = new Vector2(1f, 0f);
            container.anchoredPosition = new Vector2(-16f, 16f);
            container.sizeDelta = new Vector2(cardWidth + 8f, 0f);

            var vlg = cont.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.LowerRight;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            var fitter = cont.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; // 下端から上へ積む

            // ドラッグハンドル（デッキ全体を自由移動）＝常に最上段に置く
            dragHandle = new GameObject("DragHandle");
            dragHandle.transform.SetParent(container, false);
            var hImg = dragHandle.AddComponent<Image>();
            hImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var hle = dragHandle.AddComponent<LayoutElement>();
            hle.preferredWidth = cardWidth; hle.minHeight = 26f;
            var drag = dragHandle.AddComponent<UIDragMove>();
            drag.target = container;
            var hLabel = AddLabel(dragHandle.transform, "≡ 決裁デスク（ドラッグで移動）", 15f, new Color(0.7f, 0.78f, 0.86f));
            hLabel.alignment = TextAlignmentOptions.Center;
            dragHandle.SetActive(false);
        }

        private TextMeshProUGUI AddLabel(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            if (jpFont != null) label.font = jpFont;
            return label;
        }

        private GameObject AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action,
            bool small = false, float fixedWidth = 0f)
        {
            var btnObj = new GameObject("Button");
            btnObj.transform.SetParent(parent, false);
            var img = btnObj.AddComponent<Image>();
            img.color = small ? new Color(0.22f, 0.26f, 0.32f, 0.9f) : new Color(0.26f, 0.40f, 0.56f, 1f);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            if (action != null) btn.onClick.AddListener(action);
            var le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = small ? 28f : 36f;
            le.preferredHeight = small ? 28f : 36f;
            if (fixedWidth > 0f) { le.preferredWidth = fixedWidth; le.flexibleWidth = 0f; }

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero; txtRT.anchoredPosition = Vector2.zero;
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = small ? 16f : 18f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (jpFont != null) tmp.font = jpFont;
            return btnObj;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ===== デモ =====

        private void SpawnDemo()
        {
            if (Queue.ActiveCount() > 0) return; // 既に何か積まれていれば何もしない

            var tax = new PendingDecision(9001, "減税の建白（政治家箱）", DecisionSeverity.通常,
                DecisionSource.建白結果, "tax.cut", defaultChoiceIndex: 1,
                body: "辺境の議員から減税の建白が上がっている。重税に民が苦しんでおり、民心の離反が懸念される。" +
                      "ただし国庫はすでに細っており、減税は歳入を削る。財務官僚は強く難色を示している。");
            tax.choices.Add("裁可する"); tax.choices.Add("見送る（現状維持）");

            var treaty = new PendingDecision(9002, "辺境星系との通商条約", DecisionSeverity.重要,
                DecisionSource.イベント, "treaty.sign", defaultChoiceIndex: 1,
                body: "辺境の独立星系群が通商条約の締結を打診してきた。締結すれば交易路が開け国庫が潤うが、" +
                      "隣接する大国を刺激し、外交関係が悪化する恐れがある。");
            treaty.choices.Add("締結する"); treaty.choices.Add("保留する");

            var coup = new PendingDecision(9003, "地方総督のクーデターの兆候", DecisionSeverity.重大,
                DecisionSource.イベント, "", defaultChoiceIndex: 0,
                body: "辺境を治める総督が中央への上納を渋り、独自に兵を集めているとの報告。放置すれば離反は確実。" +
                      "鎮圧は兵力を割き、懐柔は中央の威信を損なう。どちらにせよ時間の猶予はない。");
            coup.choices.Add("鎮圧を命じる"); coup.choices.Add("懐柔する");

            Queue.Enqueue(tax);
            Queue.Enqueue(treaty);
            Queue.Enqueue(coup);
        }
    }
}
