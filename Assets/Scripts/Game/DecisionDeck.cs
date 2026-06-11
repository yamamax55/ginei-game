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
    /// <see cref="GameClock"/> を止める（アクティブポーズ＝本当にやばいやつ）。
    /// Strategy/Battle へ自動生成（NotificationFeed/TimeDisplay と同型）。効果の実適用（effectKey→世界）は DESK-6 で合流。
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
        private TMP_FontAsset jpFont;
        private bool deskPausedClock;
        private string lastSignature = "";

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

            // 表示は状態が変わった時だけ作り直す（毎フレームのボタン再生成を避ける）
            string sig = Signature();
            if (sig != lastSignature)
            {
                Rebuild();
                lastSignature = sig;
            }
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
            // 既存カードを破棄
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            // 最小化バッジ（下端＝最後に追加されるものが最下段）
            int minimized = Queue.MinimizedCount();
            if (minimized > 0)
                BuildMinimizedBadge(minimized);

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
                // 新着は提示中へ（提示開始）
                if (visible[i].status == DecisionStatus.新着) visible[i].status = DecisionStatus.提示中;
                BuildCard(visible[i]);
            }
        }

        private void BuildCard(PendingDecision d)
        {
            var card = new GameObject("Card_" + d.id);
            card.transform.SetParent(container, false);
            var bg = card.AddComponent<Image>();
            bg.color = CardColor(d.severity);
            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 4f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = cardWidth;

            // ヘッダ（重要度＋タイトル）
            AddLabel(card.transform, $"<b>[{d.severity}]</b> {d.title}", 20f,
                d.severity == DecisionSeverity.重大 ? new Color(1f, 0.85f, 0.85f) : Color.white);

            // 選択肢ボタン
            for (int i = 0; i < d.choices.Count; i++)
            {
                int idx = i;                 // クロージャ用にコピー
                PendingDecision dd = d;
                AddButton(card.transform, d.choices[i], () => ResolveDecision(dd, idx));
            }

            // 重大以外は最小化ボタン（重大は手を止めさせる＝最小化させない）
            if (d.severity != DecisionSeverity.重大)
            {
                PendingDecision dd = d;
                AddButton(card.transform, "― 後で（最小化）", () => { Queue.Minimize(dd); }, small: true);
            }
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
        }

        private void AddLabel(Transform parent, string text, float size, Color color)
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
        }

        private void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action, bool small = false)
        {
            var btnObj = new GameObject("Button");
            btnObj.transform.SetParent(parent, false);
            var img = btnObj.AddComponent<Image>();
            img.color = small ? new Color(0.22f, 0.26f, 0.32f, 0.9f) : new Color(0.26f, 0.40f, 0.56f, 1f);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(action);
            var le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = small ? 30f : 38f;
            le.preferredHeight = small ? 30f : 38f;

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
            if (jpFont != null) tmp.font = jpFont;
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
                DecisionSource.建白結果, "tax.cut", defaultChoiceIndex: 1);
            tax.choices.Add("裁可する"); tax.choices.Add("見送る（現状維持）");

            var treaty = new PendingDecision(9002, "辺境星系との通商条約", DecisionSeverity.重要,
                DecisionSource.イベント, "treaty.sign", defaultChoiceIndex: 1);
            treaty.choices.Add("締結する"); treaty.choices.Add("保留する");

            var coup = new PendingDecision(9003, "重大：地方総督のクーデターの兆候", DecisionSeverity.重大,
                DecisionSource.イベント, "", defaultChoiceIndex: 0);
            coup.choices.Add("鎮圧を命じる"); coup.choices.Add("懐柔する");

            Queue.Enqueue(tax);
            Queue.Enqueue(treaty);
            Queue.Enqueue(coup);
        }
    }
}
