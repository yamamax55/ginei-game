using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 会戦イベントの配線（#2179 / 戦術決裁デスク）。既存の `EventEngine`/`GameEventDef`/`EventChoice`（#116）を会戦に駆動する。
    /// 一定間隔で会戦用イベントを抽選発火し、<b>右下の「戦術決裁デスク」へ時間を止めずに積む</b>（戦略の
    /// <see cref="DecisionDeck"/> の会戦版）。提示は非ポーズ＝戦闘は流れ続ける。締切・最小化・自動解決は Core の
    /// <see cref="DecisionQueue"/>/<see cref="DecisionTriageRules"/> を会戦 game-時間（timeScale 追従）で回す＝
    /// 放置すれば既定選択が機械的に採択される（放置の代償）。効果は選択肢のデリゲート（<see cref="EventChoice.Apply"/>）で適用。
    /// Battle シーンに自動生成。戦略の決裁デスクとはキューを分離（会戦専用キュー＝混線しない）。
    /// </summary>
    public class BattleEventManager : MonoBehaviour
    {
        [Tooltip("イベント抽選の間隔（秒・game-time）")]
        public float tickInterval = 25f;

        [Header("戦術決裁デスク（右下・非ポーズ）")]
        [Tooltip("デスクの最大同時表示カード数")]
        public int maxVisibleCards = 4;
        [Tooltip("カード幅")]
        public float cardWidth = 360f;
        [Tooltip("キャンバスのソート順（通知880より前・モーダル900+より後ろ）")]
        public int canvasSortingOrder = 884;

        // 右下のミニマップ（220x220＋inset(-12,12)＝約232px四方）を避けて上に逃がす余白。
        private const float MinimapClearance = 248f;
        // ドラッグハンドル（タイトルバー）の高さ。
        private const float DragHandleHeight = 26f;

        private readonly EventEngine engine = new EventEngine();
        private readonly DecisionQueue deck = new DecisionQueue();
        // 決裁id → 発火したイベント定義とコンテキスト（効果適用に使う）。
        private readonly Dictionary<int, EventEntry> eventById = new Dictionary<int, EventEntry>();
        private int nextDecisionId = 1;
        private float nextTick;

        // 固定会戦QA（SPEED-08）が自分の使い捨てシーンに限って会戦イベントを動かすための許可。
        // 既定＝無し＝従来どおり Battle シーンだけで動く（自動生成 TryCreate はシーン名だけを見るので対象外）。
        private static Scene qaHostScene;

        /// <summary>このシーンに置いた個体だけ、名前が Battle でなくても動かしてよい（QA専用・終了時に <see cref="ClearQaHostScene"/>）。</summary>
        public static void AllowQaHostScene(Scene scene) => qaHostScene = scene;

        /// <summary>QAの許可を解く（以後は従来どおり Battle シーン以外で自壊）。</summary>
        public static void ClearQaHostScene() => qaHostScene = default;

        /// <summary>QAの許可が残っているか（試験・復元確認用）。</summary>
        public static bool HasQaHostScene => qaHostScene.IsValid();

        private static bool IsQaHost(Scene scene) => qaHostScene.IsValid() && scene == qaHostScene && scene.isLoaded;

        /// <summary>通知の送り先。null＝従来どおり <see cref="NotificationCenter"/>（QAはローカルログへ差し替える）。</summary>
        public System.Action<NotificationCategory, NotificationSeverity, string> NotificationSink { get; set; }

        // 効果の対象勢力の明示指定（QAが自分の味方艦隊に合わせる）。未指定＝従来どおり GameSettings.playerFaction。
        private bool hasTargetFactionOverride;
        private Faction targetFactionOverride;

        /// <summary>この個体の効果の対象勢力を明示する（GameSettings は書き換えない）。</summary>
        public void SetTargetFaction(Faction faction)
        {
            targetFactionOverride = faction;
            hasTargetFactionOverride = true;
        }

        /// <summary>対象勢力を明示しているか。</summary>
        public bool HasTargetFactionOverride => hasTargetFactionOverride;

        /// <summary>いま効果を掛ける勢力（明示があればそれ、無ければ GameSettings.playerFaction）。</summary>
        public Faction TargetFaction => Player;

        /// <summary>抽選で発火した件数（観測用）。</summary>
        public int FiredCount { get; private set; }
        /// <summary>抽選を行った回数（発火しなかった回も数える・観測用）。</summary>
        public int TickCount { get; private set; }
        /// <summary>次の抽選時刻（Time.time 基準。Start 前は0）。</summary>
        public float NextTickAt => nextTick;

        /// <summary>デスクに残っている未解決の決裁数（観測用）。</summary>
        public int PendingDecisionCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < deck.items.Count; i++)
                {
                    var d = deck.items[i];
                    if (d != null && d.status != DecisionStatus.決裁済 && d.status != DecisionStatus.自動解決) n++;
                }
                return n;
            }
        }

        // UI
        private RectTransform container;
        private GameObject deckRoot;
        private GameObject dragHandle;
        private TMP_FontAsset jpFont;
        private bool windowAttachDone;
        private string lastSignature = "";
        private readonly List<CardView> cardViews = new List<CardView>();

        private class EventEntry
        {
            public GameEventDef def;
            public EventContext ctx;
            public int defaultChoice;
        }

        private class CardView
        {
            public PendingDecision decision;
            public TextMeshProUGUI deadlineLabel;
        }

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
            if (!BattleOverlayScopeRules.ShouldHost(scene.name)) return;
            // 攻城/システムビューなど特殊モードでは出さない。
            if (BattleHandoff.IsPlanetSiege || BattleHandoff.IsSystemView) return;
            // 会戦シーンごとに1つ（WIN-4）：ウィンドウ化会戦は複数の Battle シーンが同時にロードされるので、
            // グローバル重複ガードでなく「このシーンに既に在るか」で判定する（Minimap.TryCreate と同じ作法）。
            if (FindInScene(scene) != null) return;
            var go = new GameObject("BattleEventManager");
            // ★ additive ロードされた会戦シーンへ帰属させる（WIN-1 残骸バグ）。
            //   ウィンドウ化会戦ではアクティブシーンが Strategy のままなので、素の new GameObject は
            //   戦略側に生まれ、Battle シーンをアンロードしても生き残って決裁カードを生み続けてしまう。
            //   フルスクリーン会戦（Battle＝アクティブ）では移動不要＝従来動作（後方互換）。
            if (BattleOverlayScopeRules.NeedsSceneMove(scene.IsValid(), scene == SceneManager.GetActiveScene()))
                SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BattleEventManager>();
        }

        /// <summary>指定シーンに属する BattleEventManager を返す（無ければ null）。</summary>
        private static BattleEventManager FindInScene(Scene scene)
        {
            BattleEventManager[] all = FindObjectsByType<BattleEventManager>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene == scene) return all[i];
            return null;
        }

        private void Awake()
        {
            // 保険：万一 Strategy 等の非会戦シーンに生まれた／取り残された個体は即座に自壊する。
            // （`BattleSetup.Awake` のシーン名ガードと同じ趣旨。決裁カードを生み続ける経路を塞ぐ）
            if (!BattleOverlayScopeRules.ShouldHost(gameObject.scene.name) && !IsQaHost(gameObject.scene))
            {
                Debug.LogWarning("BattleEventManager: 会戦シーン以外に生成されたため自壊します（scene: "
                    + gameObject.scene.name + "）");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            RegisterEvents();
            BuildUI();
            nextTick = Time.time + tickInterval;
        }

        /// <summary>
        /// 会戦終了（シーンのアンロード／フルスクリーンのシーン遷移）で自分が作った UI を確実に片付ける。
        /// Canvas は自分の子なので通常は連鎖破棄されるが、親替え（<see cref="BattleWindowUI.TryAttach"/> で
        /// 窓内 UI 親矩形へ移したコンテナ）は<b>別シーンの下に居る</b>ため連鎖破棄されない＝明示的に破棄する。
        /// </summary>
        private void OnDestroy()
        {
            // 窓へ親替えしたコンテナだけ明示的に破棄する（自分の子でなくなっているため連鎖破棄されない）。
            // 親替えしていない場合は deckRoot の子＝下の破棄で一緒に片付くので二重に触らない。
            if (Application.isPlaying && container != null && deckRoot != null
                && !container.IsChildOf(deckRoot.transform))
                Destroy(container.gameObject);
            container = null;

            if (Application.isPlaying && deckRoot != null) Destroy(deckRoot);
            deckRoot = null;
            dragHandle = null;
            cardViews.Clear();
            eventById.Clear();
            deck.items.Clear();
        }

        private Faction Player => hasTargetFactionOverride ? targetFactionOverride
            : (GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟);

        private bool eventsRegistered;
        // イベントid → 定義（QAの決定論の効果確認で引く）。
        private readonly Dictionary<string, GameEventDef> defsById = new Dictionary<string, GameEventDef>();

        private void Register(GameEventDef def)
        {
            engine.Register(def);
            defsById[def.id] = def;
        }

        private void Notify(NotificationCategory category, NotificationSeverity severity, string message)
        {
            if (NotificationSink != null) NotificationSink(category, severity, message);
            else NotificationCenter.Push(category, severity, message);
        }

        /// <summary>
        /// 固定会戦QA専用の<b>決定論の効果確認</b>：登録済みイベントの選択肢の効果をそのまま1回適用する（抽選・デスク・発火数には触れない）。
        /// ★自然発生の確認ではない。QA許可のシーンの個体だけ受け付ける（通常の Battle 個体では false）。
        /// </summary>
        public bool ApplyChoiceForQaVerification(string eventId, int choiceIndex)
        {
            if (!IsQaHost(gameObject.scene) || string.IsNullOrEmpty(eventId)) return false;
            RegisterEvents();
            if (!defsById.TryGetValue(eventId, out GameEventDef def)) return false;
            if (choiceIndex < 0 || choiceIndex >= def.choices.Count) return false;
            def.choices[choiceIndex].Apply(new EventContext(Player));
            return true;
        }

        private void RegisterEvents()
        {
            if (eventsRegistered) return;
            eventsRegistered = true;

            // 義勇兵の志願（好機）：受ければ士気↑。放置の既定＝断る（無効果）。
            Register(new GameEventDef("battle_volunteers", "義勇兵の志願",
                    "近隣の義勇兵が前線への参加を志願している。")
                .AddChoice("受け入れる（士気↑）", ctx => AdjustPlayerMorale(8f, "battle_volunteers"))
                .AddChoice("断る", null));

            // 補給線の不安（ジレンマ）：放置の既定＝慎重（安全だが士気↓）。
            Register(new GameEventDef("battle_supply", "補給線に不安",
                    "弾薬の補給に遅れが出ている。どう戦う？")
                .AddChoice("慎重に戦う（士気↓・安全）", ctx => AdjustPlayerMorale(-5f, "battle_supply"))
                .AddChoice("強攻して勢いをつける（士気↑）", ctx => AdjustPlayerMorale(7f, "battle_supply")));

            // 英雄的奮戦（通知＝確認のみ）：自動で士気↑。
            Register(new GameEventDef("battle_heroics", "英雄的奮戦",
                    "一隊の奮戦が全軍を奮い立たせた！")
                .AddChoice("士気高まる", ctx => AdjustPlayerMorale(6f, "battle_heroics")));
        }

        /// <summary>放置時に機械的に採択する既定選択（イベントごと）。</summary>
        private static int DefaultChoiceFor(string eventId)
        {
            switch (eventId)
            {
                case "battle_volunteers": return 1; // 断る（無効果）
                case "battle_supply": return 0;     // 慎重（安全）
                default: return 0;                  // 通知は唯一の選択
            }
        }

        private static DecisionSeverity SeverityFor(GameEventDef def)
            => def.IsNotification ? DecisionSeverity.情報 : DecisionSeverity.通常;

        private void Update()
        {
            // 0) 自分の会戦シーンが畳まれていたら何もしない（戦略側に残って決裁を生み続けない）。
            //    ウィンドウ化会戦で窓を閉じるとシーンがアンロードされる＝そのフレーム以降は沈黙する。
            if (!BattleOverlayScopeRules.ShouldRun(gameObject.scene.name, gameObject.scene.isLoaded) && !IsQaHost(gameObject.scene)) return;

            // ウィンドウ化会戦（WIN-4）ではデスクを自分の窓内 UI 親へ親替えする（全画面に広げない）。
            TryWindowAttach();

            // 1) 一定間隔で会戦イベントを抽選発火し、デスクへ積む（ポーズしない）。
            if (Time.time >= nextTick)
            {
                nextTick = Time.time + Mathf.Max(5f, tickInterval);
                var ctx = new EventContext(Player);
                GameEventDef fired = engine.Tick(ctx, Time.time, Random.value);
                TickCount++;
                if (fired != null)
                {
                    FiredCount++;
                    EnqueueEvent(fired, ctx);
                    engine.ClearPending(); // デスク側で管理＝エンジンの保留キューは持ち越さない
                }
            }

            // 2) 締切を会戦 game-時間で進める（timeScale 追従＝ポーズ中は dt=0 で凍結）。
            //    締切超で最小化、さらに猶予超で既定選択を自動採択（放置の代償）。
            float gdt = Time.deltaTime;
            var resolved = DecisionTriageRules.Tick(deck, gdt);
            for (int i = 0; i < resolved.Count; i++)
                AutoResolve(resolved[i]);

            // 3) 表示は集合が変わった時だけ作り直す（毎フレームのボタン再生成を避ける）。
            string sig = Signature();
            if (sig != lastSignature) { Rebuild(); lastSignature = sig; }

            UpdateDeadlines();
        }

        private void EnqueueEvent(GameEventDef def, EventContext ctx)
        {
            // 通知（選択肢0〜1＝決断を伴わない）はデスクに載せず、即時に効果を適用して通知システムへ流す。
            // 実決断（選択肢2以上）のみデスクのカードにする。左下の NotificationFeed が下記 Push を自動表示する。
            if (def.IsNotification)
            {
                if (def.choices.Count > 0) def.choices[0].Apply(ctx);
                Notify(NotificationCategory.戦闘, NotificationSeverity.情報,
                    string.IsNullOrEmpty(def.body) ? def.title : $"{def.title}：{def.body}");
                return;
            }

            int id = nextDecisionId++;
            int defChoice = DefaultChoiceFor(def.id);
            var pd = new PendingDecision(id, def.title, SeverityFor(def),
                DecisionSource.イベント, "", defChoice, def.body);
            for (int i = 0; i < def.choices.Count; i++) pd.choices.Add(def.choices[i].label);
            deck.Enqueue(pd);
            eventById[id] = new EventEntry { def = def, ctx = ctx, defaultChoice = defChoice };
        }

        /// <summary>放置で自動採択された決裁の効果を適用して片付ける。</summary>
        private void AutoResolve(PendingDecision d)
        {
            if (d == null) return;
            if (eventById.TryGetValue(d.id, out EventEntry e))
            {
                ApplyChoice(e, d.chosenIndex);
                Notify(NotificationCategory.戦闘, NotificationSeverity.注意,
                    $"［放置〕{d.title} → {ChoiceLabel(d, d.chosenIndex)}（現場判断で処理）");
                eventById.Remove(d.id);
            }
            deck.items.Remove(d);
        }

        /// <summary>プレイヤーが決裁＝選択肢を採択して効果を適用し片付ける（非ポーズ）。</summary>
        private void ResolveByPlayer(PendingDecision d, int choiceIndex)
        {
            if (d == null) return;
            if (eventById.TryGetValue(d.id, out EventEntry e))
            {
                ApplyChoice(e, choiceIndex);
                eventById.Remove(d.id);
            }
            deck.items.Remove(d);
            // 即座に作り直す（残ったカードを詰める）。
            lastSignature = "";
        }

        private static void ApplyChoice(EventEntry e, int choiceIndex)
        {
            if (e == null || e.def == null) return;
            if (choiceIndex < 0 || choiceIndex >= e.def.choices.Count) return;
            e.def.choices[choiceIndex].Apply(e.ctx);
        }

        private static string ChoiceLabel(PendingDecision d, int idx)
        {
            if (d == null || idx < 0 || idx >= d.choices.Count) return "（既定）";
            return d.choices[idx];
        }

        /// <summary>
        /// プレイヤー勢力の生存旗艦の士気を一律に増減する。
        /// <paramref name="eventId"/> は観測台帳（<see cref="MoraleAuditLog"/>）へ残す原因の内訳で、
        /// 増減の計算には関与しない（士気が上がったのが自然回復かこのイベントかを後から言い分けるため）。
        /// </summary>
        private void AdjustPlayerMorale(float delta, string eventId)
        {
            Faction player = Player;
            IReadOnlyList<FleetStrength> flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength f = flags[i];
                if (f == null || !f.IsAlive || f.faction != player) continue;
                FleetMorale mo = f.GetComponent<FleetMorale>();
                if (mo != null) mo.ApplyMoraleDelta(delta, MoraleChangeSource.戦況イベント, eventId);
            }
            Notify(NotificationCategory.戦闘, NotificationSeverity.情報,
                delta >= 0 ? $"会戦イベント：味方の士気が上がった（+{delta:0}）" : $"会戦イベント：味方の士気が下がった（{delta:0}）");
        }

        // ===== 表示（右下スタック・非ポーズ） =====

        private string Signature()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < deck.items.Count; i++)
            {
                var d = deck.items[i];
                if (d == null) continue;
                sb.Append(d.id).Append(':').Append((int)d.status).Append('|');
            }
            return sb.ToString();
        }

        private void Rebuild()
        {
            cardViews.Clear();
            // ドラッグハンドル以外を破棄（ハンドルは位置保持のため残す）。
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var c = container.GetChild(i);
                if (c.gameObject == dragHandle) continue;
                Destroy(c.gameObject);
            }

            // 未解決のみ・新しい順（後から来たものを上に）に最大 maxVisibleCards 枚。
            int shown = 0;
            for (int i = deck.items.Count - 1; i >= 0 && shown < maxVisibleCards; i--)
            {
                var d = deck.items[i];
                if (d == null) continue;
                if (d.status == DecisionStatus.決裁済 || d.status == DecisionStatus.自動解決) continue;
                if (d.status == DecisionStatus.新着) d.status = DecisionStatus.提示中;
                BuildCard(d);
                shown++;
            }

            // ハンドルは常に先頭（最下段）に保つ。カードが無い間は隠す（DecisionDeck と同様）。
            if (dragHandle != null)
            {
                dragHandle.transform.SetAsFirstSibling();
                dragHandle.SetActive(shown > 0);
            }
        }

        private void BuildCard(PendingDecision d)
        {
            var card = new GameObject("Card_" + d.id);
            card.transform.SetParent(container, false);
            var bg = card.AddComponent<Image>();
            bg.color = d.severity == DecisionSeverity.通常
                ? new Color(0.10f, 0.20f, 0.32f, 0.94f)
                : new Color(0.14f, 0.22f, 0.18f, 0.94f); // 情報＝緑寄り
            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 8);
            vlg.spacing = 4f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = cardWidth;

            // タイトル
            // 記号「⚔」は日本語フォントに無く豆腐になる（FontCoverageChecker で実測）＝短い日本語で示す。
            AddLabel(card.transform, $"<b>【戦況】{d.title}</b>", 18f, new Color(1f, 0.92f, 0.6f));
            // 本文
            if (!string.IsNullOrEmpty(d.body))
                AddLabel(card.transform, d.body, 15f, new Color(0.86f, 0.9f, 0.95f));
            // 締切（毎フレーム更新）
            var dl = AddLabel(card.transform, "", 13f, new Color(0.7f, 0.78f, 0.86f));
            dl.alignment = TextAlignmentOptions.Right;
            cardViews.Add(new CardView { decision = d, deadlineLabel = dl });

            // 選択肢ボタン（横並び）
            var row = new GameObject("Choices");
            row.transform.SetParent(card.transform, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            var rle = row.AddComponent<LayoutElement>();
            rle.minHeight = 34f;

            for (int i = 0; i < d.choices.Count; i++)
            {
                int idx = i;
                PendingDecision dd = d;
                AddButton(row.transform, d.choices[i], () => ResolveByPlayer(dd, idx));
            }
        }

        private void UpdateDeadlines()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * (Mathf.PI * 2f));
            var p = DecisionTriageParams.Default;
            for (int i = 0; i < cardViews.Count; i++)
            {
                var v = cardViews[i];
                if (v == null || v.decision == null || v.deadlineLabel == null) continue;
                var d = v.decision;
                float total = DecisionTriageRules.DeadlineFor(d.severity, p) + p.autoResolveGrace;
                float remaining = Mathf.Max(0f, total - d.elapsed);
                v.deadlineLabel.text = $"放置で自動処理まで 残り {Mathf.CeilToInt(remaining)} 秒";
                v.deadlineLabel.color = remaining <= 8f
                    ? Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.3f), pulse)
                    : new Color(0.7f, 0.78f, 0.86f);
            }
        }

        // ===== UI 部品 =====

        private void BuildUI()
        {
            var canvasGo = new GameObject("BattleEventDeckCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            deckRoot = canvasGo;

            var cont = new GameObject("DeckContainer");
            cont.transform.SetParent(canvasGo.transform, false);
            container = cont.AddComponent<RectTransform>();
            container.anchorMin = new Vector2(1f, 0f); // 右下
            container.anchorMax = new Vector2(1f, 0f);
            container.pivot = new Vector2(1f, 0f);
            // 右下はミニマップ（約232px四方）が占めるので、その上へ逃がして重ねない。
            container.anchoredPosition = new Vector2(-16f, MinimapClearance);
            container.sizeDelta = new Vector2(cardWidth + 8f, 0f);

            var vlg = cont.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.LowerRight;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            var fitter = cont.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; // 下端から上へ積む

            // ドラッグハンドル（デスク全体を自由移動）＝常にコンテナ先頭（最下段＝コーナー寄り）に置く。
            // DecisionDeck と同様、対象はコンテナ RectTransform。カードが無い間は隠す。
            dragHandle = new GameObject("DragHandle");
            dragHandle.transform.SetParent(container, false);
            var hImg = dragHandle.AddComponent<Image>();
            hImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            var hle = dragHandle.AddComponent<LayoutElement>();
            hle.preferredWidth = cardWidth; hle.minHeight = DragHandleHeight;
            var drag = dragHandle.AddComponent<UIDragMove>();
            drag.target = container;
            var hLabel = AddLabel(dragHandle.transform, "≡ 戦術決裁デスク（ドラッグで移動）", 15f, new Color(0.7f, 0.78f, 0.86f));
            hLabel.alignment = TextAlignmentOptions.Center;
            var hlrt = hLabel.rectTransform; // ラベルをハンドル枠いっぱいに伸ばす（中央表示）
            hlrt.anchorMin = Vector2.zero; hlrt.anchorMax = Vector2.one;
            hlrt.offsetMin = Vector2.zero; hlrt.offsetMax = Vector2.zero;
            dragHandle.transform.SetAsFirstSibling(); // VLG の先頭＝LowerRight 積みの最下段
            dragHandle.SetActive(false);
        }

        /// <summary>
        /// ウィンドウ化会戦（WIN-4）では戦術決裁デスクを自分の窓内 UI 親へ親替えする
        /// （全画面に広げず、対応する会戦窓の中に収める）。フルスクリーン会戦（会戦シーン＝アクティブシーン）
        /// では何もしない＝従来どおり画面右下に出す（後方互換）。<see cref="Minimap"/> と同じ作法。
        /// </summary>
        private void TryWindowAttach()
        {
            if (windowAttachDone || container == null) return;
            if (gameObject.scene == SceneManager.GetActiveScene()) { windowAttachDone = true; return; }
            if (BattleWindowUI.TryAttach(gameObject.scene, container)) windowAttachDone = true;
        }

        private TextMeshProUGUI AddLabel(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.raycastTarget = false;
            if (jpFont != null) label.font = jpFont;
            return label;
        }

        private GameObject AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var btnObj = new GameObject("Button");
            btnObj.transform.SetParent(parent, false);
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.26f, 0.40f, 0.56f, 1f);
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            if (action != null) btn.onClick.AddListener(action);
            var le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 34f; le.preferredHeight = 34f;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero; txtRT.anchoredPosition = Vector2.zero;
            var tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (jpFont != null) tmp.font = jpFont;
            return btnObj;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            // 自分の会戦シーンへ帰属させる（戦略側に EventSystem を置き去りにしない／
            // ウィンドウ化会戦では BattleWindow の EventSystem ガードが正しく畳める）。
            Scene mine = gameObject.scene;
            if (BattleOverlayScopeRules.NeedsSceneMove(mine.IsValid(), mine == SceneManager.GetActiveScene()))
                SceneManager.MoveGameObjectToScene(es, mine);
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
