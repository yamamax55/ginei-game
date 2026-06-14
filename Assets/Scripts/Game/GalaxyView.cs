using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（C-1 #34）の最小ビジュアライズ。C-1 の純ロジック
    /// （GalaxyMap / StrategicFleet / GalaxyPathfinder / StrategicFleetRegistry / StrategyRules）を
    /// 画面につなぐデモ：星系・回廊・艦隊マーカーを描画し、クリックで艦隊選択→星系クリックでワープ指示。
    /// 銀河時間を進め、到着で占領（色変化）、同一回廊の敵対遭遇を会戦トリガーとして表示。
    ///
    /// 操作：左クリック=艦隊選択（Shiftで追加）／星系クリック=選択艦隊をワープ／
    ///       Space=停止・再開／1・2・3=速度（0.5x/1x/2x）。
    /// 実機確認用。`Ginei/戦略マップ デモを開く` でデモシーンに配置 → 再生。
    /// </summary>
    public partial class GalaxyView : MonoBehaviour
    {
        [Header("見た目")]
        public float systemScale = 0.8f;
        public float fleetScale = 0.4f;
        public Color empireColor = new Color(0.85f, 0.3f, 0.25f);
        public Color allianceColor = new Color(0.3f, 0.5f, 0.9f);
        public Color corridorColor = new Color(0.5f, 0.55f, 0.7f, 0.9f);
        public Color chokeColor = new Color(0.9f, 0.8f, 0.3f, 0.95f);
        public Color frontlineColor = new Color(0.9f, 0.25f, 0.2f, 0.95f); // 前線（FTL不可）
        public Color selectColor = new Color(1f, 0.95f, 0.4f);

        [Header("時間")]
        public float galaxySpeed = 1f;

        [Header("二層遷移（戦略↔戦術・#586）")]
        [Tooltip("交戦中の回廊（赤点滅）の色")]
        public Color combatColor = new Color(1f, 0.35f, 0.15f, 1f);
        [Tooltip("交戦を放置したとき自動解決するまでの猶予（銀河時間・秒）。この間にダブルクリックで潜行できる")]
        public float autoResolveDelay = 2.5f;
        [Tooltip("ダブルクリック判定の猶予（実時間・秒）")]
        public float doubleClickWindow = 0.35f;
        [Tooltip("星系の点をクリックしたと見なす半径（ハブ星系で回廊より星系を優先＝惑星へ入れる）")]
        public float systemClickRadius = 0.65f;

        [Header("マップ操作（#2384 戦略マップUX）")]
        [Tooltip("既定のカメラズーム（orthographicSize）。F キーでこの値へリセット")]
        public float defaultZoom = 8f;
        [Tooltip("ホイール1ノッチあたりのズーム率（0.3=30%。回す量に比例して指数加速＝他ゲーム準拠で速め）")]
        [Range(0.05f, 0.6f)] public float zoomPerNotch = 0.3f;
        [Tooltip("ズーム追従の滑らかさ（小さいほどゆっくり滑らかに・大きいほど即時。unscaled 駆動）")]
        public float zoomLerpSpeed = 11f;
        [Tooltip("ズーム下限（近づける限界）")]
        public float minZoom = 3f;
        [Tooltip("ズーム上限（引きの限界）")]
        public float maxZoom = 16f;
        [Tooltip("左ドラッグをパンと見なすしきい値（ピクセル）。これ未満で離せばクリック（選択/ダブルクリック）")]
        public float dragThresholdPixels = 8f;
        [Tooltip("カメラ中心の移動可能範囲（±このワールド距離でクランプ＝迷子防止）")]
        public float panLimit = 22f;
        [Header("ナビ（キーパン）")]
        [Tooltip("キーボード（WASD/矢印）パンの速度")]
        public float keyPanSpeed = 26f;
        [Tooltip("パン（ドラッグ/端/キー）の追従の滑らかさ（小さいほど即時・大きいほどヌルッと慣性的）")]
        public float panSmoothTime = 0.10f;
        [Tooltip("背景星雲（galaxy_backdrop）の不透明度（0=出さない）。視野に追従して常に覆う")]
        [Range(0f, 1f)] public float backdropAlpha = 0.55f;
        [Tooltip("背景星雲の明るさ（0=黒〜1=原画。盤面の星系/回廊を読みやすくするため暗めに落とす）")]
        [Range(0f, 1f)] public float backdropBrightness = 0.4f;
        [Tooltip("背景星雲が視野からはみ出す余裕倍率（端の隙間防止）")]
        public float backdropCover = 1.15f;

        [Header("惑星攻城（#131）")]
        [Tooltip("S-AV戦力あたりの制空権抑制速度")]
        public float siegeSuppressRate = 0.05f;
        [Tooltip("ドメイン・ダウン後のS-AV戦力あたり侵略値蓄積速度")]
        public float siegeInvadeRate = 0.05f;
        [Tooltip("非交戦時の制空権再建速度")]
        public float siegeDefenseRegen = 0f;
        [Tooltip("デモ：帝国星系に置く惑星の制空権/侵略閾値")]
        public float demoPlanetDefense = 100f;
        public Color defenseColor = new Color(0.9f, 0.55f, 0.25f);
        public Color invadeColor = new Color(0.95f, 0.3f, 0.3f);

        private GalaxyMap map;
        private StrategicFleetRegistry reg;
        private Camera cam;
        private Sprite disc;
        private Material lineMat;
        private bool paused;
        private float occupyTimer;
        private float engagedElapsed;      // 交戦中が継続している時間（自動解決の猶予計測）
        private double currentAutoResolveSeconds; // TIME-4：現交戦の自動解決所要時間（AutoBattleSim 算出・game-seconds）
        private float lastClickTime = -1f; // ダブルクリック判定用（実時間）
        private Vector2 lastClickWorld;
        private SpriteRenderer backdrop;    // 背景星雲（galaxy_backdrop・視野追従）
        private bool leftDragging;          // 左ドラッグ中（クリック判定は離した時＝誤選択防止。星系は動かさない）
        private Vector2 leftPressScreen;    // 左押下時のスクリーン座標（ドラッグ判定の起点）
        private bool midPanning;            // 中ボタンドラッグでスクロール中
        private bool leftPressOverUI;       // 左押下が UI 上で始まったか（その間マップを動かさない）
        private bool midPressOverUI;        // 中押下が UI 上で始まったか
        private float zoomTarget;           // ホイールズームの目標 orthographicSize（滑らかに追従）
        private bool zoomInit;              // zoomTarget 初期化済みか
        private Vector2 zoomAnchorScreen;   // ズーム中心に保つスクリーン点（最後のホイール時のカーソル）
        private Vector3 panTarget;          // カメラ中心の目標位置（パン入力はここを動かし、cam は滑らかに追従）
        private Vector3 panVelocity;        // SmoothDamp 用の速度キャッシュ
        private bool panInit;               // panTarget 初期化済みか

        private readonly List<StrategicFleet> selectedFleets = new List<StrategicFleet>();
        private readonly Dictionary<int, SpriteRenderer> systemDots = new Dictionary<int, SpriteRenderer>();
        private readonly List<LineRenderer> corridorLines = new List<LineRenderer>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetMarks = new Dictionary<StrategicFleet, SpriteRenderer>();
        // 勢力別の艦隊スプライト（帝国/同盟）。未登録の勢力はマル（disc）のまま。Start で Resources から読み込む。
        private readonly Dictionary<Faction, Sprite> fleetSprites = new Dictionary<Faction, Sprite>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetRings = new Dictionary<StrategicFleet, SpriteRenderer>();
        private readonly Dictionary<StrategicFleet, TextMesh> fleetEta = new Dictionary<StrategicFleet, TextMesh>();
        private readonly List<LineRenderer> routeLines = new List<LineRenderer>();
        private TextMesh banner;
        private TextMesh helpLine;
        private TextMesh policyLine;                 // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示
        private readonly Dictionary<int, TextMesh> siegeLabels = new Dictionary<int, TextMesh>();

        // S5/S6（縦スライス）：税率レバー・財政・支持低下イベント
        [Header("内政スライス（S5/S6）")]
        [Tooltip("税率の1操作あたりの増減")]
        public float taxStep = 0.05f;
        [Tooltip("デバッグモード（` キーで切替）。税率レバー [ ] 等のデバッグ専用操作を有効化する。通常プレイでは税率は内政/AIに委ね手動レバーは出さない（タイクン化回避＝高位の決断＋創発的帰結）。")]
        public bool debugMode = false;
        [Tooltip("民心(希望)がこの値を下回ると不満イベントを提示")]
        public float hopeEventThreshold = 0.35f;
        private EventEngine policyEngine;
        private EventContext policyCtx;
        // TIME-6（#952）：暦の日境界でイベント判定を駆動するディスパッチャ（毎フレームでなく per-day＝倍速で暦比一定・ポーズで停止）。
        private CalendarDispatcher policyCalendar;

        // TIME-6（#952・LIFE-2 #152）：人物の加齢/老衰を暦の年境界で回すデモロスター。提督が老いて死に、HUDで告知する。
        private List<Person> commanders;
        private int campaignYear;

        // 士官学校（#155 LIFE-5）：勢力ごとの学校。暦の年境界で新任士官を輩出しロスターへ供給する。
        private List<Academy> academies;
        private int nextPersonId = 1;       // 卒業生のID採番（手置き提督の次から）
        private const int OfficerRosterCap = 80; // 士官名簿の上限（PERF＝無制限増加を防ぐ）

        // 大学（#156/#157 LIFE-6/7）：文官/技術者を輩出する文民版の学校。文民ロスターへ供給する。
        private List<University> universities;
        private List<Person> civilians;
        private const int CivilRosterCap = 80; // 文民名簿の上限（PERF）

        // 朝廷の権威（律令の形骸化・官僚制基盤）。封建の世＝既に低め（武家政権相当）＝官職は名誉職化方向。
        // 文官ネームドの考課・叙位（五位の壁）はこの権威で効く（BureaucracyCareerRules / RitsuryoFormalizationRules）。
        private CourtAuthority courtAuthority; // 実体は StrategySession.CourtAuthority（SetupPersonnel で共有＝Battle往復/セーブで永続）

        /// <summary>朝廷の権威（観測用・read-only 参照）。</summary>
        public CourtAuthority Court => courtAuthority;
        /// <summary>文民ネームドのロスター（観測用・人物名鑑が読む）。</summary>
        public IReadOnlyList<Person> CivilianRoster => civilians;
        /// <summary>武官ネームドのロスター（観測用・人物名鑑が読む）。</summary>
        public IReadOnlyList<Person> CommanderRoster => commanders;

        // 幼稚園/小学校/中学校/高校（#155-157 の土台）：勢力ごとの就学前〜中等教育。進学率＝候補の母数、質＝候補の素質を左右する（複利）。
        private List<Kindergarten> kindergartens;
        private List<ElementarySchool> elementarySchools;
        private List<HighSchool> highSchools;
        private List<MiddleSchool> middleSchools;
        // 保育園（#153/#110）：教育でなく保育＝労働参加↑・出生率↑（POP の出生/労働に効く）。
        private List<Nursery> nurseries;
        private List<TechnicalCollege> colleges; // 高専（中学校→高専の実務技術者路・#157）
        private List<JuniorCollege> juniorColleges; // 短大（高校卒後2年・行政中堅・#156）
        private List<VocationalSchool> vocationalSchools; // 専門学校（高校卒後2年・実務specialist・#157）

        // #884 造船 → #148 艦隊プール供給：星系ごとの造船所（全勢力＝AIも建艦）。暦の日次で建艦し、完成を所有勢力の FleetPool へ就役。
        // 生産力は内政（Province 安定度比例＝BUILD-2）に連動＝支配が不安定な系は建艦が遅い。損耗（戦略会戦の戦力喪失）でプール減。
        private List<Shipyard> shipyards;
        [Tooltip("各勢力の初期艦隊プール（FleetPool 未設定時にシード）")]
        public int initialFleetPool = 12000;
        [Tooltip("星系造船所の建艦速度（ポイント/戦略秒。生産力係数 BUILD-2 を掛ける）")]
        public float shipyardBuildPower = 1f;

        // TIME-7（#959）：暦の自動スロー（Paradox 風）。平時は暦を圧縮して速く流し、会戦の生起など「観るべき瞬間」は実時間へ減速。
        [Tooltip("平時に暦を実時間の何倍で流すか（自動スロー時は1倍＝実時間へ減速）。TIME-7 #959")]
        public float idleCalendarCompression = 30f;
        [Tooltip("暦流速の減速/再加速のなめらかさ（1秒あたりの倍率変化）")]
        public float calendarEaseRate = 8f;
        [Tooltip("PERF（死のスパイラル防止）：1フレームで進める実時間の上限(秒)。ヒッチ/alt-tab/GCストールで巨大化した deltaTime が暦圧縮(最大idleCalendarCompression倍)で増幅され、年/日境界が一気に大量発火→重い社会シミュが1フレームで数百回走り管理ヒープが数GBへ膨張するのを防ぐ。スケーラビリティ規律#2。")]
        public float maxFrameDt = 0.1f;
        private float calendarCompression = 1f; // 現在の暦流速倍率（1=実時間。起動時は実時間から立ち上げる）

        // 内政（#109・#759）：星系ごとの統治状態。デモは所有勢力の思想で安定度が動く。
        private readonly Dictionary<int, Province> provinces = new Dictionary<int, Province>();
        private readonly Dictionary<int, Faction> prevOwners = new Dictionary<int, Faction>();
        private readonly Dictionary<Faction, FactionData> demoFactions = new Dictionary<Faction, FactionData>();

        private void Start()
        {
            // 戦略マップシーンのコンテキストを設定（#107：会戦から戻った後も正しく絞られるよう再セット）
            GameInput.SetContext(InputContext.戦略);

            cam = Camera.main;
            if (cam == null) cam = new GameObject("GalaxyCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = defaultZoom;
            zoomTarget = defaultZoom; zoomInit = true; // ホイールズームの追従目標を初期化
            cam.transform.position = new Vector3(0f, 0f, -10f);
            panTarget = cam.transform.position; panVelocity = Vector3.zero; panInit = true; // パン追従目標を初期化
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);

            disc = MakeDiscSprite(64);
            lineMat = new Material(Shader.Find("Sprites/Default"));
            LoadFleetSprites();

            SetupBackdrop(); // 背景星雲（galaxy_backdrop）を視野追従で敷く（#2384）

            BuildDemoGalaxy();
            SetupGovernance();
            SetupEvents(); // S6：支持低下イベント（#116 エンジン）を用意
            BuildVisuals();

            // 実会戦（Battleシーン）から戻ってきた結果を戦略へ反映。
            // さらに、潜行中に銀河の時計は止まらない＝観ていなかった他戦線は自動侵攻で決着（#586 ④⑤）。
            if (BattleHandoff.Resolved && StrategyRules.ApplyHandoffResult(reg))
            {
                int others = StrategyRules.ResolveEncounters(reg);
                NotificationCenter.Push(NotificationCategory.戦闘,
                    others > 0 ? $"実会戦の結果を反映（観ていない{others}戦線は自動解決）" : "実会戦の結果を反映しました");
            }

            // 惑星攻城の戦術マップでの進捗を惑星へ書き戻す（#131）
            if (BattleHandoff.siegeResolved) ApplySiegeResult();
        }

        private void Update()
        {
            // ESC（#ウィンドウESC）：重ねたウィンドウを最前面から1枚ずつ閉じ、無くなったらシステムメニュー。
            // モーダル窓が盤面入力を塞ぐ前（下の early-return より前）に評価し、閉じた窓自身もここで処理する。
            if (GameInput.WasPressed(GameAction.キャンセル)) HandleStrategyEscape();

            // イベント提示モーダル／艦隊編成画面／決裁／終了画面／システムメニュー 表示中は戦略マップの入力・進行を止める。
            // SystemDetailPanel は非モーダル窓化したので塞がない（開いたままマップ操作・進行が続く）。
            if (StrategyEventPanel.IsOpen || FleetOrganizationPanel.IsOpen || DecisionBoardPanel.IsOpen
                || CampaignEndOverlay.IsOpen || StrategySystemMenu.IsOpen) return;

            // 盤面が未構築（Start 前・リロード途中）なら何もしない＝reg/map の null 参照を全面ガード。
            if (map == null || reg == null) return;

            HandleKeys();

            // TIME-1（#947）：統一クロックが速度/ポーズの権威。+/-（TimeDisplay）・1/2/3/Space（HandleKeys）が
            // クロックを駆動し、galaxySpeed/paused はそれをミラーする（日付HUD・時間連続性・自動解決の出所）。
            GameClock clock = StrategySession.Clock;
            if (clock != null) { galaxySpeed = (float)clock.speed; paused = clock.paused; }
            // PERF（死のスパイラル防止）：ヒッチ/alt-tab/GCストールで巨大化した Time.deltaTime を上限クランプ。
            // 暦圧縮(calendarCompression・最大 idleCalendarCompression 倍)で増幅されるため、生のまま使うと
            // 1フレームで年/日境界が大量発火→重い社会シミュが数百回走り管理ヒープが数GBへ膨張する（スケーラビリティ規律#2）。
            float frameDt = Mathf.Min(Time.deltaTime, maxFrameDt);
            float dt = paused ? 0f : frameDt * Mathf.Max(0f, galaxySpeed);

            // TIME-7（#959）：暦は自動スロー。平時は暦を圧縮して速く流し（年が分単位）、会戦の生起など「観るべき瞬間」は
            // 実時間へ減速する。実時間アクション（艦隊移動・自動解決・攻城）は dt のまま＝暦だけ伸縮する（観られる速さは不変）。
            var flow = new TimeFlowRules.TimeFlowParams(idleCalendarCompression, 1f, calendarEaseRate);
            calendarCompression = TimeFlowRules.Ease(
                calendarCompression, TimeFlowRules.TargetCompression(IsActionSalient(), flow), flow, frameDt);
            if (clock != null) clock.Advance(frameDt * calendarCompression);
            reg.Tick(dt);

            // 回廊で接触した敵対艦隊は「交戦中」として固着（旧：即・実会戦へ強制遷移＝廃止）。
            // プレイヤーはダブルクリックで潜行＝手動指揮へ。放置すれば猶予後に自動解決（#586 ①④）。
            StrategyRules.BeginEngagements(reg);
            if (AnyEngaged())
            {
                // TIME-4（#950）：自動解決の所要時間を AutoBattleSim（裏の簡易戦術シミュ）で算出＝
                // 観戦会戦と同じ game-time を消費する（固定 autoResolveDelay でなく交戦戦力から決まる）。
                if (currentAutoResolveSeconds <= 0.0) currentAutoResolveSeconds = ComputeEngagementDuration();
                engagedElapsed += dt;
                if (engagedElapsed >= currentAutoResolveSeconds)
                {
                    StrategyRules.ResolveEncounters(reg); // 放置の自動解決。プールは減らさない（手動会戦と統一）
                    engagedElapsed = 0f;
                    currentAutoResolveSeconds = 0.0;
                }
            }
            else { engagedElapsed = 0f; currentAutoResolveSeconds = 0.0; }

            // 防衛惑星の攻城（停泊した敵対艦隊が S-AV で制空権制圧→侵略→占領）。銀河時間で進む。
            StrategyRules.TickSieges(map, reg, dt, new SiegeParams(siegeSuppressRate, siegeInvadeRate, siegeDefenseRegen));

            occupyTimer += dt;
            if (occupyTimer >= 0.4f) { StrategyRules.ResolveAllOccupations(map, reg); occupyTimer = 0f; }

            // 内政（#109）：所有変化で不安定化→時間で統合・安定。情報パネル(#759)が読む。
            TickGovernance(dt);

            // 国家状態（#817 旗幟の出所）：各勢力の腐敗→合意→希望を銀河時間で進める。
            CampaignRules.Tick(StrategySession.Campaign, dt);
            // 財政（S5）＋支持低下イベント（S6）は日境界、人物の加齢/老衰（LIFE-2）は年境界で進める（TIME-6 #952）。
            // いずれも暦駆動＝倍速で暦比一定・ポーズで停止。日次→年次の順に独立発火（CalendarDispatcher）。
            if (clock != null && policyCalendar != null)
                policyCalendar.Advance(clock.ElapsedSeconds, onDay: RunDailyCampaignTick, onYear: RunAnnualLifecycleTick);


            HandleMouse();
            Refresh();
        }

        // ===== 内政（#109）＋星系情報パネル（#759） =====

        // --- オンボーディング（目標提示＋初手ガイド） ---
        private static bool objectiveAnnounced;

        /// <summary>現在の難易度（GameSettings）に応じた勝敗しきい値。盤面/勝敗/目標表示の単一窓口。</summary>
        private static CampaignVictoryRules.CampaignVictoryParams ActiveVictoryParams()
            => CampaignDifficultyRules.VictoryParams(
                GameSettings.Instance != null ? GameSettings.Instance.campaignDifficulty : CampaignDifficulty.普通);

        /// <summary>キャンペーン開始時に勝利目標と最初の操作を通知で提示する（セッション一度きり）。勝敗は <see cref="CampaignVictoryRules"/>。</summary>
        private void AnnounceCampaignObjective()
        {
            if (objectiveAnnounced) return;
            objectiveAnnounced = true;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            int pct = Mathf.RoundToInt(ActiveVictoryParams().dominationFraction * 100f);
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意,
                $"【目標】{player} で銀河の {pct}% を支配せよ（敵を全制圧でも勝利／全星系を失えば敗北）");
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報,
                "操作：星系を右クリックで進軍 → 前線で接触 → 交戦中の回廊をダブルクリックで潜行（会戦へ）。Space/1-3=速度、H=ヘルプ。");
        }

        /// <summary>プレイヤー勢力の国家状態（無ければ null）。</summary>
        private FactionState PlayerState()
        {
            var campaign = StrategySession.Campaign;
            if (campaign == null) return null;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            return CampaignRules.GetState(campaign, pf);
        }

        /// <summary>支持低下イベント（#116 エンジン経由）を用意する。Start で SetupGovernance の後に呼ぶ。</summary>
        private void SetupEvents()
        {
            policyEngine = new EventEngine();
            policyCtx = new EventContext(
                GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国);

            var unrest = new GameEventDef
            {
                id = "民衆の不満",
                title = "民衆の不満",
                body = "重税に民が苦しんでいる。民心が離れ始めた——どう応える？",
                repeatable = true,
                cooldown = 180f, // TIME-6：game-seconds 基準（=3 game-day・1日60s）。暦時間でのクールダウン。
            };
            // 条件：プレイヤー勢力の民心(希望)がしきい値を下回る
            unrest.condition = ctx =>
            {
                FactionState s = PlayerState();
                return s != null && s.community != null && s.community.hope < hopeEventThreshold;
            };
            // 選択肢＝政治的帰結（盤面の状態を直接動かす）
            unrest.AddChoice("減税して民を宥める（税率↓・民心↑）", ctx =>
            {
                FactionState s = PlayerState();
                if (s == null) return;
                s.taxRate = Mathf.Clamp01(s.taxRate - 0.15f);
                if (s.community != null) s.community.hope = Mathf.Clamp01(s.community.hope + 0.12f);
            });
            unrest.AddChoice("強硬に抑え込む（抑圧↑・短期しのぎ）", ctx =>
            {
                FactionState s = PlayerState();
                if (s == null || s.community == null) return;
                s.community.repression = Mathf.Clamp01(s.community.repression + 0.2f);
                s.community.hope = Mathf.Clamp01(s.community.hope + 0.05f); // 力で一時的に持ち直す
            });
            policyEngine.Register(unrest);

            // TIME-6（#952）：暦の日境界でイベント判定を回す。現在のクロック経過へ同期（初フレームで日跨ぎを一気に発火させない）。
            double startElapsed = StrategySession.Clock != null ? StrategySession.Clock.ElapsedSeconds : 0d;
            policyCalendar = new CalendarDispatcher(GameDate.DateParams.Default, startElapsed);

            SetupPersonnel();
            SetupShipyard();
        }

        /// <summary>
        /// 暦の日境界ごとに走る盤面の日次処理（TIME-6 #952）：財政を1日ぶん進め（S5）、続いて支持低下イベント判定（S6）。
        /// 連続ドリフト系（艦隊移動・内政・社会連鎖 CampaignRules.Tick）は従来どおり dt で回る（後方互換・段階移行）。
        /// </summary>
        private void RunDailyCampaignTick()
        {
            float secondsPerDay = (float)policyCalendar.Params.secondsPerDay;
            CampaignRules.TickEconomyDay(StrategySession.Campaign, secondsPerDay); // 歳入＝税収を国庫へ
            CampaignRules.TickBudgetDay(StrategySession.Campaign, secondsPerDay); // 歳出＝予算総額を国庫から（国家予算の基盤）
            TickShipyard(secondsPerDay); // 建艦を1日進め、完成を勢力プールへ（#884→#148）
            RunDailyPolicyTick();
            RunMilitarySupplyTick(); // 軍要求物資（#2049）：補給切れの前線艦隊が干上がる
        }

        // 軍の補給を1日ぶん（MILSUP-6・#2049 配線）：補給源（自勢力領）から切れた前線艦隊は補給が枯れて損耗する。
        // 現在/出発星系が自勢力領なら補給線が通る＝補給。敵に後背を取られる/前線で孤立すると干上がる（兵糧攻め）。
        private void RunMilitarySupplyTick()
        {
            if (reg == null || reg.fleets == null || map == null) return;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.strength <= 0) continue;
                StarSystem sys = map.GetSystem(f.currentSystemId);
                bool supplied = sys != null && sys.owner == f.faction; // 後背が自勢力領＝補給線が通る
                int lost = MilitarySupplyTickRules.TickFleet(f, supplied);
                if (lost > 0)
                    NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                        $"{f.faction} 第{f.id}艦隊 補給途絶で損耗（-{lost}・補給{Mathf.RoundToInt(f.supply * 100f)}%）");
            }
        }

        /// <summary>
        /// 「観るべき瞬間」か（TIME-7 #959 自動スロー）：会戦の生起・前線への亜光速侵入など。true の間は暦を実時間へ減速し、
        /// 早送りで会戦や接触を見逃さない・暦が一気に飛ばないようにする。実時間アクションの速さ自体は変えない。
        /// </summary>
        private bool IsActionSalient()
        {
            if (AnyEngaged()) return true; // 会戦が起きている＝観て介入できるよう減速
            if (reg != null && reg.fleets != null)
            {
                for (int i = 0; i < reg.fleets.Count; i++)
                {
                    StrategicFleet f = reg.fleets[i];
                    if (f != null && f.IsSublight) return true; // 前線へ亜光速侵入中＝接触直前の緊張
                }
            }
            return false;
        }

        /// <summary>
        /// 暦の日境界ごとに支持低下イベントの条件を判定し、発火したらモーダル提示する（S6・TIME-6 #952）。
        /// EventEngine の cooldown 判定は <b>game-time（クロック経過秒）</b>を渡す＝倍速で暦比一定・ポーズで停止。
        /// </summary>
        private void RunDailyPolicyTick()
        {
            if (policyEngine == null || StrategyEventPanel.IsOpen) return;
            float nowGameSeconds = StrategySession.Clock != null ? (float)StrategySession.Clock.ElapsedSeconds : 0f;
            GameEventDef fired = policyEngine.Tick(policyCtx, nowGameSeconds, 0.5f);
            if (fired != null) ShowPolicyEvent(fired);
        }

        /// <summary>発火したイベント定義を選択肢付きモーダルで提示し、選択で <see cref="EventEngine.Resolve"/> する。</summary>
        private void ShowPolicyEvent(GameEventDef def)
        {
            var choices = new System.Collections.Generic.List<(string, System.Action)>();
            for (int i = 0; i < def.choices.Count; i++)
            {
                int idx = i; // クロージャ用に確定
                choices.Add((def.choices[i].label, () => policyEngine.Resolve(idx, policyCtx)));
            }
            StrategyEventPanel.Show(def.title, def.body, choices);
        }

        /// <summary>背景星雲をカメラ視野に追従させ常に覆う（ズーム/パンに連動）。</summary>
        private void LateUpdate()
        {
            SmoothPan(); // パン目標へカメラを滑らかに追従（ドラッグ/端/キー共通の慣性的な動き）

            if (backdrop == null || cam == null) return;
            Vector3 cp = cam.transform.position;
            backdrop.transform.position = new Vector3(cp.x, cp.y, 0f);
            float worldH = cam.orthographicSize * 2f;
            float worldW = worldH * Mathf.Max(0.01f, cam.aspect);
            Vector3 sprSize = backdrop.sprite.bounds.size; // ワールド単位（pixelsPerUnit=100）
            float sx = sprSize.x > 0f ? (worldW / sprSize.x) * backdropCover : 1f;
            float sy = sprSize.y > 0f ? (worldH / sprSize.y) * backdropCover : 1f;
            backdrop.transform.localScale = new Vector3(sx, sy, 1f);
        }

        /// <summary>交戦中（engaged）の艦隊が1隻でも居るか。</summary>
        private bool AnyEngaged()
        {
            if (reg == null || reg.fleets == null) return false; // リロード途中など未初期化時の保険
            foreach (var f in reg.fleets) if (f != null && f.engaged) return true;
            return false;
        }

        /// <summary>
        /// 交戦中の最初の敵対ペアから自動解決の所要時間を <see cref="AutoBattleSim"/>（裏の簡易戦術シミュ）で見積もる
        /// （TIME-4 #950・時間統一）。ペアが取れなければ従来の固定値にフォールバック。返り値は game-seconds。
        /// </summary>
        private double ComputeEngagementDuration()
        {
            StrategicFleet a = null;
            foreach (var f in reg.fleets)
            {
                if (f == null || !f.engaged) continue;
                if (a == null) { a = f; continue; }
                if (FactionRelations.IsHostile(null, a.faction, null, f.faction))
                {
                    var r = AutoBattleSim.Resolve(a.strength, f.strength);
                    return r.durationSeconds > 0.0 ? r.durationSeconds : autoResolveDelay;
                }
            }
            return autoResolveDelay; // フォールバック（従来値）
        }

        /// <summary>回廊 c の上に交戦中の艦隊が居るか（描画の点滅判定用）。</summary>
        private bool IsEngagedCorridor(Corridor c)
        {
            int min = Mathf.Min(c.aId, c.bId), max = Mathf.Max(c.aId, c.bId);
            foreach (var f in reg.fleets)
            {
                if (f == null || !f.engaged) continue;
                int fMin = Mathf.Min(f.currentSystemId, f.destinationSystemId);
                int fMax = Mathf.Max(f.currentSystemId, f.destinationSystemId);
                if (fMin == min && fMax == max) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (lineMat != null) Destroy(lineMat);
            if (disc != null && disc.texture != null) Destroy(disc.texture);
        }
    }
}
