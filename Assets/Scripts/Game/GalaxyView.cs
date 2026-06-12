using System.Collections.Generic;
using UnityEngine;
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
    public class GalaxyView : MonoBehaviour
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

        private readonly List<StrategicFleet> selectedFleets = new List<StrategicFleet>();
        private readonly Dictionary<int, SpriteRenderer> systemDots = new Dictionary<int, SpriteRenderer>();
        private readonly List<LineRenderer> corridorLines = new List<LineRenderer>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetMarks = new Dictionary<StrategicFleet, SpriteRenderer>();
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
        [Tooltip("戦略会戦の戦力喪失をプール損耗へ換算する倍率（1戦略戦力≒艦艇。BattleHandoff.StrengthScale 相当）")]
        public int attritionPoolScale = 40;

        // TIME-7（#959）：暦の自動スロー（Paradox 風）。平時は暦を圧縮して速く流し、会戦の生起など「観るべき瞬間」は実時間へ減速。
        [Tooltip("平時に暦を実時間の何倍で流すか（自動スロー時は1倍＝実時間へ減速）。TIME-7 #959")]
        public float idleCalendarCompression = 30f;
        [Tooltip("暦流速の減速/再加速のなめらかさ（1秒あたりの倍率変化）")]
        public float calendarEaseRate = 8f;
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
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);

            disc = MakeDiscSprite(64);
            lineMat = new Material(Shader.Find("Sprites/Default"));

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

        /// <summary>戦術マップでの攻城進捗（割合）を戦略の惑星へ反映する（#131）。占領なら所有フリップ＋再建。</summary>
        private void ApplySiegeResult()
        {
            StarSystem s = map.GetSystem(BattleHandoff.planetSystemId);
            if (s != null && s.planet != null)
            {
                Planet p = s.planet;
                if (BattleHandoff.siegeResultCaptured)
                {
                    p.owner = BattleHandoff.besiegerFaction;
                    s.owner = BattleHandoff.besiegerFaction;
                    p.orbitalDefense = p.maxOrbitalDefense; // 新所有者が制空権を再建
                    p.invasionProgress = 0f;
                    NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意, $"{s.systemName} を占領しました");
                }
                else
                {
                    p.orbitalDefense = Mathf.Clamp01(BattleHandoff.siegeResultDefense) * p.maxOrbitalDefense;
                    p.invasionProgress = Mathf.Clamp01(BattleHandoff.siegeResultInvasion) * p.invasionThreshold;
                    NotificationCenter.Push(NotificationCategory.占領, $"{s.systemName} の攻城を進めました");
                }
            }
            BattleHandoff.Clear();
        }

        private void Update()
        {
            // 星系情報パネル／イベント提示モーダル／艦隊編成画面 表示中は戦略マップの入力・進行を止める（各パネルがポーズ＆入力を処理）。
            if (SystemDetailPanel.IsOpen || StrategyEventPanel.IsOpen || FleetOrganizationPanel.IsOpen || DecisionBoardPanel.IsOpen) return;

            HandleKeys();

            // TIME-1（#947）：統一クロックが速度/ポーズの権威。+/-（TimeDisplay）・1/2/3/Space（HandleKeys）が
            // クロックを駆動し、galaxySpeed/paused はそれをミラーする（日付HUD・時間連続性・自動解決の出所）。
            GameClock clock = StrategySession.Clock;
            if (clock != null) { galaxySpeed = (float)clock.speed; paused = clock.paused; }
            float dt = paused ? 0f : Time.deltaTime * Mathf.Max(0f, galaxySpeed);

            // TIME-7（#959）：暦は自動スロー。平時は暦を圧縮して速く流し（年が分単位）、会戦の生起など「観るべき瞬間」は
            // 実時間へ減速する。実時間アクション（艦隊移動・自動解決・攻城）は dt のまま＝暦だけ伸縮する（観られる速さは不変）。
            var flow = new TimeFlowRules.TimeFlowParams(idleCalendarCompression, 1f, calendarEaseRate);
            calendarCompression = TimeFlowRules.Ease(
                calendarCompression, TimeFlowRules.TargetCompression(IsActionSalient(), flow), flow, Time.deltaTime);
            if (clock != null) clock.Advance(Time.deltaTime * calendarCompression);
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
                    ResolveEncountersWithAttrition(); // #884 損耗：戦力喪失を勢力プールへ反映
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

        /// <summary>
        /// 星系ごとの統治状態(Province)を用意する。Battle 往復時は StrategySession から復元し安定度/統合を引き継ぐ。
        /// デモ用に勢力へ思想を持たせ、住民の思想＝（初回は）開始所有勢力とする。
        /// </summary>
        private void SetupGovernance()
        {
            // デモ用の勢力データ（思想を持たせて内政の手応えを出す。実運用は Resources/Factions の FactionData）。
            demoFactions[Faction.帝国] = MakeDemoFaction("帝国", "専制", Faction.帝国);
            demoFactions[Faction.同盟] = MakeDemoFaction("同盟", "民主", Faction.同盟);

            provinces.Clear();
            // 永続化済みの内政状態があれば引き継ぐ（Battle 往復で安定度/統合を失わない）。
            if (StrategySession.Provinces != null)
                foreach (var kv in StrategySession.Provinces)
                    if (kv.Value != null) provinces[kv.Key] = kv.Value;

            foreach (var s in map.systems)
            {
                if (s == null) continue;
                // 復元に無い星系（初回・新規）だけ作る。住民の思想＝開始所有勢力（占領されても変わらない＝燻りの源）。
                if (!provinces.ContainsKey(s.id))
                    provinces[s.id] = new Province(s.id, IdeologyOf(s.owner), 100f);
                // 復帰時点の所有を基準に（往復直後に誤って OnOccupied しないため）。
                prevOwners[s.id] = s.owner;
            }

            StrategySession.Provinces = provinces; // 以後この参照を永続化（static が生き続ける間）

            // 国家状態（#817 旗幟の基準忠誠の出所）：Battle 往復で失わないよう StrategySession に持たせる。
            if (StrategySession.Campaign == null) StrategySession.Campaign = new CampaignState(map);
            CampaignRules.EnsureStates(StrategySession.Campaign);
        }

        private FactionData MakeDemoFaction(string name, string ideology, Faction legacy)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.factionName = name; f.ideology = ideology; f.legacyFaction = legacy;
            return f;
        }

        private string IdeologyOf(Faction f) => demoFactions.TryGetValue(f, out var fd) && fd != null ? fd.ideology : "";

        /// <summary>各星系の内政を1tick進める：所有変化で OnOccupied（不安定化）、以降は目標安定度へ収束。</summary>
        private void TickGovernance(float dt)
        {
            if (dt <= 0f || map == null) return;
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;

                // 所有が変わった＝占領 → 統合リセットで不安定化
                if (prevOwners.TryGetValue(s.id, out var prev) && prev != s.owner)
                {
                    GovernanceRules.OnOccupied(prov);
                    prevOwners[s.id] = s.owner;
                }

                FactionData owner = demoFactions.TryGetValue(s.owner, out var fd) ? fd : null;
                GovernanceRules.Tick(prov, owner, supplyOk: true, atWar: HasHostileFleetAt(s), deltaTime: dt);
            }
        }

        /// <summary>その星系に所有勢力と敵対する戦略艦隊が停泊しているか（戦時ペナルティ判定）。</summary>
        private bool HasHostileFleetAt(StarSystem s)
        {
            var here = reg.FleetsAt(s.id);
            for (int i = 0; i < here.Count; i++)
            {
                StrategicFleet f = here[i];
                if (f != null && FactionRelations.IsHostile(null, f.faction, s.ownerData, s.owner)) return true;
            }
            return false;
        }

        /// <summary>マウス直下の星系の情報パネルを開く（I キー・#759）。表示中は戦略マップがポーズ。</summary>
        private void OpenSystemInfoAtMouse()
        {
            if (cam == null) return;
            Vector2 w = WorldMouse();
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > 1.2f) return;
            StarSystem s = map.GetSystem(sysId);
            if (s == null) return;
            provinces.TryGetValue(sysId, out var prov);
            SystemDetailPanel.Show(s, prov, map.Neighbors(sysId).Count, FleetSummaryAt(sysId));
        }

        /// <summary>星系に停泊中の戦略艦隊を勢力ごとに「N隊・兵力M」で要約する。</summary>
        private string FleetSummaryAt(int sysId)
        {
            var here = reg.FleetsAt(sysId);
            if (here == null || here.Count == 0) return "";
            var strengthByF = new Dictionary<Faction, int>();
            var countByF = new Dictionary<Faction, int>();
            for (int i = 0; i < here.Count; i++)
            {
                StrategicFleet f = here[i];
                if (f == null) continue;
                strengthByF.TryGetValue(f.faction, out int st); strengthByF[f.faction] = st + f.strength;
                countByF.TryGetValue(f.faction, out int c); countByF[f.faction] = c + 1;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in strengthByF)
                sb.AppendLine($"{kv.Key}：{countByF[kv.Key]}隊・兵力 {kv.Value}");
            return sb.ToString().TrimEnd();
        }

        // ===== デモ銀河 =====

        private void BuildDemoGalaxy()
        {
            // 戦略↔実会戦の往復で世界状態を保持（あれば再利用）
            if (StrategySession.HasState) { map = StrategySession.Map; reg = StrategySession.Reg; return; }

            map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "アスタ", new Vector2(0f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(1, "ベガ", new Vector2(-5f, -3f), Faction.同盟));
            map.AddSystem(new StarSystem(2, "ケレス", new Vector2(5f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(3, "ドラコ", new Vector2(0f, -0.5f), Faction.帝国));
            map.AddSystem(new StarSystem(4, "エリス", new Vector2(-2.5f, 1f), Faction.同盟));
            map.AddSystem(new StarSystem(5, "フェニクス", new Vector2(3.5f, -2.5f), Faction.帝国));

            map.AddCorridor(new Corridor(2, 0, 4f, CorridorType.要衝));
            map.AddCorridor(new Corridor(0, 3, 5f));
            map.AddCorridor(new Corridor(3, 1, 4f));
            map.AddCorridor(new Corridor(3, 4, 3f));
            map.AddCorridor(new Corridor(4, 1, 2f));
            map.AddCorridor(new Corridor(0, 5, 3f));
            map.AddCorridor(new Corridor(5, 3, 2f));

            // 帝国星系は惑星（制空権持ち）で防衛＝同盟は停泊だけでは占領できず攻城が要る（#131）。
            // 同盟星系は無防備（planet 無し）＝従来どおり停泊で占領（両方の挙動をデモ）。
            // PB-6 デモ：帝国星系の最初の2つを要塞・コロニーにして「同枠攻略」を見せる。残りは従来の惑星。
            int siegeVariety = 0;
            foreach (var s in map.systems)
                if (s != null && s.owner == Faction.帝国)
                {
                    if (siegeVariety == 0)
                        s.planet = PlanetSiegeRules.CreateTarget(s.id, Faction.帝国, Planet.SiegeTargetKind.要塞);
                    else if (siegeVariety == 1)
                        s.planet = PlanetSiegeRules.CreateTarget(s.id, Faction.帝国, Planet.SiegeTargetKind.コロニー);
                    else
                        s.planet = new Planet(s.id, Faction.帝国, demoPlanetDefense, demoPlanetDefense);
                    siegeVariety++;
                }

            reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(1, 2, Faction.帝国, 1.5f) { strength = 250 });
            reg.Add(new StrategicFleet(2, 1, Faction.同盟, 1.5f) { strength = 300 });
            reg.Add(new StrategicFleet(3, 4, Faction.同盟, 1.2f) { strength = 150 });
            reg.Add(new StrategicFleet(4, 3, Faction.帝国, 1.3f) { strength = 200 }); // ドラコ防衛・前線で衝突用

            StrategySession.Set(map, reg);
        }

        // ===== 描画 =====

        private void BuildVisuals()
        {
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                StarSystem a = map.GetSystem(c.aId);
                StarSystem b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;

                var lr = NewLine($"Corridor_{c.aId}_{c.bId}", 0);
                lr.positionCount = 2;
                lr.SetPosition(0, a.position);
                lr.SetPosition(1, b.position);
                bool choke = c.type == CorridorType.要衝;
                lr.startWidth = lr.endWidth = choke ? 0.16f : 0.08f;
                lr.startColor = lr.endColor = choke ? chokeColor : corridorColor;
                corridorLines.Add(lr);
            }

            foreach (var s in map.systems)
            {
                if (s == null) continue;
                var go = new GameObject($"System_{s.id}_{s.systemName}");
                go.transform.SetParent(transform, false);
                go.transform.position = s.position;
                go.transform.localScale = Vector3.one * systemScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc; sr.color = OwnerColor(s.owner); sr.sortingOrder = 2;
                systemDots[s.id] = sr;
                MakeLabel(go.transform, s.systemName, new Vector3(0f, systemScale * 0.9f, 0f), 0.9f);

                // 防衛惑星は攻城状態（制空権/侵略値）を星系の下にコンパクト表示
                if (s.planet != null)
                {
                    var sl = MakeLabel(go.transform, "", new Vector3(0f, -systemScale * 0.95f, 0f), 0.7f).GetComponent<TextMesh>();
                    siegeLabels[s.id] = sl;
                }
            }

            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                var go = new GameObject($"Fleet_{f.id}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * fleetScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = disc; sr.color = FactionColor(f.faction); sr.sortingOrder = 4;
                fleetMarks[f] = sr;

                // 選択リング（子・既定オフ）
                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(go.transform, false);
                ringGo.transform.localScale = Vector3.one * 1.8f;
                var ring = ringGo.AddComponent<SpriteRenderer>();
                ring.sprite = disc;
                ring.color = new Color(selectColor.r, selectColor.g, selectColor.b, 0.35f);
                ring.sortingOrder = 3;
                ring.enabled = false;
                fleetRings[f] = ring;

                // ETA ラベル（移動中のみ表示）
                var eta = MakeLabel(go.transform, "", new Vector3(0f, 0.9f, 0f), 0.7f).GetComponent<TextMesh>();
                eta.color = selectColor;
                fleetEta[f] = eta;
            }

            banner = MakeLabel(transform, "", new Vector3(0f, 7.3f, 0f), 1.0f).GetComponent<TextMesh>();
            // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示（バナー直下）
            policyLine = MakeLabel(transform, "", new Vector3(0f, 6.6f, 0f), 0.7f).GetComponent<TextMesh>();
            policyLine.color = new Color(0.85f, 0.9f, 0.7f);
            helpLine = MakeLabel(transform, "左ク:選択(Shift追加) / 回廊ダブルクリック:潜行 / 星系ダブルクリック:システムビュー / 右ク:進軍 / I:星系情報 / [ ]:税率 / +/-・1・2・3:速度 / Space:停止",
                new Vector3(0f, -7.4f, 0f), 0.7f).GetComponent<TextMesh>();
            helpLine.color = new Color(0.7f, 0.7f, 0.8f);
        }

        // ===== S5/S6：財政スライス（税率レバー・国庫・支持低下イベント）=====

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
        /// 星系ごとの造船所を用意する（#884→#148）。各勢力の初期艦隊プールをシードし、所有星系に造船所を置いて連続建艦を積む。
        /// 完成は暦の日次（<see cref="RunDailyCampaignTick"/>）で所有勢力の <see cref="FleetPool"/> へ就役＝編成画面の総艦艇が増える。
        /// </summary>
        private void SetupShipyard()
        {
            shipyards = new List<Shipyard>();
            if (map == null) return;
            var seeded = new HashSet<Faction>();
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                if (seeded.Add(s.owner) && FleetPool.Get(s.owner) <= 0) FleetPool.Set(s.owner, Mathf.Max(0, initialFleetPool));
                var yard = new Shipyard(s.id, s.owner, 1, Mathf.Max(0f, shipyardBuildPower));
                ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦);
                shipyards.Add(yard);
            }
        }

        /// <summary>
        /// 暦の1日ぶん全造船所の建艦を進め、完成艦を所有勢力プールへ就役させる（#884→#148）。生産力は内政（Province 安定度＝BUILD-2）連動。
        /// プレイヤー勢力の完成のみ HUD 告知（AI 建艦は静かに進む）。
        /// </summary>
        private void TickShipyard(float secondsPerDay)
        {
            if (shipyards == null) return;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            int playerBuilt = 0;
            for (int i = 0; i < shipyards.Count; i++)
            {
                Shipyard yard = shipyards[i];
                if (yard == null) continue;
                provinces.TryGetValue(yard.systemId, out var prov);
                float factor = ShipyardRules.ProductionFactor(prov); // BUILD-2：安定度比例＝支配≠即建艦
                var done = ShipyardRules.Tick(yard, secondsPerDay, factor);
                for (int j = 0; j < done.Count; j++)
                {
                    int built = ShipyardRules.CommissionToPool(done[j]);
                    if (yard.faction == pf) playerBuilt += built;
                }
                if (yard.queue.Count == 0) ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦);
            }
            if (playerBuilt > 0)
            {
                NotificationCenter.Push(NotificationCategory.建艦, $"造船完成：艦艇 +{playerBuilt}（プールへ／編成画面 B で配分）");
            }
        }

        /// <summary>戦略会戦の戦力喪失を勢力プールの損耗へ反映して解決する（#884 損耗）。解決前後の戦力差を debit。</summary>
        private void ResolveEncountersWithAttrition()
        {
            var before = TotalStrengthByFaction();
            StrategyRules.ResolveEncounters(reg);
            var after = TotalStrengthByFaction();
            foreach (var kv in before)
            {
                after.TryGetValue(kv.Key, out int now);
                int lost = kv.Value - now;
                if (lost > 0) FleetPoolRules.ApplyAttrition(kv.Key, lost * Mathf.Max(0, attritionPoolScale));
            }
        }

        /// <summary>現在の戦略艦隊の勢力別合計戦力。</summary>
        private Dictionary<Faction, int> TotalStrengthByFaction()
        {
            var d = new Dictionary<Faction, int>();
            if (reg != null && reg.fleets != null)
                for (int i = 0; i < reg.fleets.Count; i++)
                {
                    StrategicFleet f = reg.fleets[i];
                    if (f == null) continue;
                    d.TryGetValue(f.faction, out int s);
                    d[f.faction] = s + f.strength;
                }
            return d;
        }

        /// <summary>
        /// 加齢/老衰デモ用の提督ロスターを用意する（TIME-6 #952・LIFE-2 #152）。各勢力に若年・老齢を混ぜ、
        /// 暦の年境界で <see cref="AnnualLifecycleRules.ProcessMortality"/> により老衰死しうる。配下の継承は後段。
        /// </summary>
        private void SetupPersonnel()
        {
            campaignYear = TimeDisplay.StartYear; // 開始暦（宇宙暦SE796）と揃える
            commanders = new List<Person>();
            int y = campaignYear;
            int id = 1;
            // 各勢力：壮年（当面は死ににくい）＋老齢（老衰しうる）
            commanders.Add(new Person(id++, "ミッターマイアー", Faction.帝国, PersonRole.軍人) { birthYear = y - 39, rankTier = 8 });
            commanders.Add(new Person(id++, "メックリンガー", Faction.帝国, PersonRole.軍人) { birthYear = y - 79, rankTier = 8 });
            commanders.Add(new Person(id++, "アッテンボロー", Faction.同盟, PersonRole.軍人) { birthYear = y - 41, rankTier = 7 });
            commanders.Add(new Person(id++, "ビュコック", Faction.同盟, PersonRole.軍人) { birthYear = y - 88, rankTier = 9 });
            nextPersonId = id; // 卒業生はこの続き番号で採番

            // 士官学校（#155 LIFE-5）：各勢力に1校。質に差を付ける（名門は良将を出す）。
            academies = new List<Academy>
            {
                new Academy(schoolId: 1, faction: Faction.帝国, name: "帝国士官学校", capacity: 6, quality: 0.6f),
                new Academy(schoolId: 2, faction: Faction.同盟, name: "同盟士官学校", capacity: 6, quality: 0.55f),
            };

            // 大学（#156/#157 LIFE-6/7）：各勢力に文官大学＋帝国に工科大学（テクノクラート）。文民/技術者を輩出。
            civilians = new List<Person>();
            universities = new List<University>
            {
                new University(schoolId: 3, faction: Faction.帝国, name: "帝国大学", track: CareerTrack.科挙, capacity: 6, quality: 0.6f),
                new University(schoolId: 4, faction: Faction.同盟, name: "自由惑星同盟大学", track: CareerTrack.科挙, capacity: 6, quality: 0.6f),
                new University(schoolId: 5, faction: Faction.帝国, name: "帝国工科大学", track: CareerTrack.テクノクラート, capacity: 4, quality: 0.6f),
            };

            // 高校（中等教育の土台）：帝国は選別的（進学率低・質高）、同盟は大衆教育（進学率高）。
            highSchools = new List<HighSchool>
            {
                new HighSchool(schoolId: 10, faction: Faction.帝国, name: "帝国高等学校", enrollmentRate: 0.5f, quality: 0.6f),
                new HighSchool(schoolId: 11, faction: Faction.同盟, name: "同盟公立高校", enrollmentRate: 0.75f, quality: 0.5f),
            };
            // 中学校（前期中等教育）：高校より進学率高め（裾野）。中学校→高校→上級学校で進学率が複利。
            middleSchools = new List<MiddleSchool>
            {
                new MiddleSchool(schoolId: 12, faction: Faction.帝国, name: "帝国中等学校", enrollmentRate: 0.8f, quality: 0.55f),
                new MiddleSchool(schoolId: 13, faction: Faction.同盟, name: "同盟公立中学校", enrollmentRate: 0.95f, quality: 0.5f),
            };
            // 小学校（初等教育の根）：ほぼ全員（義務教育）。就学率が教育チェーンの根を成す。
            elementarySchools = new List<ElementarySchool>
            {
                new ElementarySchool(schoolId: 20, faction: Faction.帝国, name: "帝国国民学校", enrollmentRate: 0.9f, quality: 0.55f),
                new ElementarySchool(schoolId: 21, faction: Faction.同盟, name: "同盟公立小学校", enrollmentRate: 0.99f, quality: 0.5f),
            };
            // 幼稚園（就学前教育＝教育チェーンの最下根）。
            kindergartens = new List<Kindergarten>
            {
                new Kindergarten(schoolId: 22, faction: Faction.帝国, name: "帝国幼稚園", enrollmentRate: 0.6f, quality: 0.55f),
                new Kindergarten(schoolId: 23, faction: Faction.同盟, name: "同盟幼稚園", enrollmentRate: 0.8f, quality: 0.5f),
            };
            // 保育園（保育＝労働参加↑/出生率↑・教育とは別軸）。同盟は福祉手厚く整備率高め。
            nurseries = new List<Nursery>
            {
                new Nursery(schoolId: 24, faction: Faction.帝国, name: "帝国保育所", coverage: 0.4f),
                new Nursery(schoolId: 25, faction: Faction.同盟, name: "同盟公立保育園", coverage: 0.7f),
            };
            // 高専（中学校→高専の実務技術者路・高校を経ない別ルート・#157）。
            colleges = new List<TechnicalCollege>
            {
                new TechnicalCollege(schoolId: 14, faction: Faction.帝国, name: "帝国高等専門学校", capacity: 5, quality: 0.6f),
                new TechnicalCollege(schoolId: 15, faction: Faction.同盟, name: "同盟工業高専", capacity: 5, quality: 0.55f),
            };
            // 短大／専門学校（高校卒後2年制・中堅人材＝官界/現場の裾野・#156/#157）。
            juniorColleges = new List<JuniorCollege>
            {
                new JuniorCollege(schoolId: 16, faction: Faction.帝国, name: "帝国短期大学", capacity: 6, quality: 0.5f),
                new JuniorCollege(schoolId: 17, faction: Faction.同盟, name: "同盟短期大学", capacity: 6, quality: 0.5f),
            };
            vocationalSchools = new List<VocationalSchool>
            {
                new VocationalSchool(schoolId: 18, faction: Faction.帝国, name: "帝国専門学校", capacity: 6, quality: 0.5f),
                new VocationalSchool(schoolId: 19, faction: Faction.同盟, name: "同盟専門学校", capacity: 6, quality: 0.5f),
            };
        }

        /// <summary>その勢力の高校（中等教育）を返す（無ければ null＝教育の制約なし）。</summary>
        private HighSchool HighSchoolOf(Faction faction)
        {
            if (highSchools == null) return null;
            for (int i = 0; i < highSchools.Count; i++)
                if (highSchools[i] != null && highSchools[i].faction == faction) return highSchools[i];
            return null;
        }

        /// <summary>その勢力の中学校（前期中等教育）を返す（無ければ null）。</summary>
        private MiddleSchool MiddleSchoolOf(Faction faction)
        {
            if (middleSchools == null) return null;
            for (int i = 0; i < middleSchools.Count; i++)
                if (middleSchools[i] != null && middleSchools[i].faction == faction) return middleSchools[i];
            return null;
        }

        /// <summary>その勢力の小学校（初等教育）を返す（無ければ null）。</summary>
        private ElementarySchool ElementarySchoolOf(Faction faction)
        {
            if (elementarySchools == null) return null;
            for (int i = 0; i < elementarySchools.Count; i++)
                if (elementarySchools[i] != null && elementarySchools[i].faction == faction) return elementarySchools[i];
            return null;
        }

        /// <summary>その勢力の幼稚園（就学前教育）を返す（無ければ null）。</summary>
        private Kindergarten KindergartenOf(Faction faction)
        {
            if (kindergartens == null) return null;
            for (int i = 0; i < kindergartens.Count; i++)
                if (kindergartens[i] != null && kindergartens[i].faction == faction) return kindergartens[i];
            return null;
        }

        /// <summary>その勢力の保育園の出生率倍率（無ければ1.0）。</summary>
        private float NurseryFertilityOf(Faction faction)
        {
            if (nurseries == null) return 1f;
            for (int i = 0; i < nurseries.Count; i++)
                if (nurseries[i] != null && nurseries[i].faction == faction)
                    return NurseryRules.FertilityFactor(nurseries[i].coverage);
            return 1f;
        }

        /// <summary>その勢力の保育園の労働参加倍率（無ければ1.0）＝候補/徴募プールに掛ける。</summary>
        private float NurseryLaborOf(Faction faction)
        {
            if (nurseries == null) return 1f;
            for (int i = 0; i < nurseries.Count; i++)
                if (nurseries[i] != null && nurseries[i].faction == faction)
                    return NurseryRules.LaborParticipationFactor(nurseries[i].coverage);
            return 1f;
        }

        /// <summary>
        /// 教育チェーン（中学校→高校）を解決し、上級学校の候補母数倍率（進学率の複利）と実効教育質（質の段階的上乗せ）を返す。
        /// 学校が無い段は素通り（倍率1・据え置き＝後方互換）。
        /// </summary>
        private void ResolveEducation(Faction faction, float baseQuality, out float enrollFactor, out float effectiveQuality)
            => ResolveEducation(faction, baseQuality, true, out enrollFactor, out effectiveQuality);

        /// <summary>
        /// 教育チェーンを解決。<paramref name="includeHighSchool"/>=false は高校を経ない路（高専＝中学校→高専）＝高校段を素通り。
        /// </summary>
        private void ResolveEducation(Faction faction, float baseQuality, bool includeHighSchool,
            out float enrollFactor, out float effectiveQuality)
        {
            enrollFactor = 1f;
            effectiveQuality = baseQuality;
            if (includeHighSchool)
            {
                HighSchool hs = HighSchoolOf(faction);
                if (hs != null)
                {
                    enrollFactor *= HighSchoolRules.EducationFactor(hs.enrollmentRate);
                    effectiveQuality = HighSchoolRules.EffectiveIntakeQuality(effectiveQuality, hs.quality);
                }
            }
            MiddleSchool ms = MiddleSchoolOf(faction);
            if (ms != null)
            {
                enrollFactor *= MiddleSchoolRules.EducationFactor(ms.enrollmentRate);
                effectiveQuality = MiddleSchoolRules.EffectiveIntakeQuality(effectiveQuality, ms.quality);
            }
            // 小学校（初等教育の根）は学術路/実務路を問わず常にチェーンに入る。
            ElementarySchool es = ElementarySchoolOf(faction);
            if (es != null)
            {
                enrollFactor *= ElementarySchoolRules.EducationFactor(es.enrollmentRate);
                effectiveQuality = ElementarySchoolRules.EffectiveIntakeQuality(effectiveQuality, es.quality);
            }
            // 幼稚園（就学前教育の最下根）も常にチェーンに入る。
            Kindergarten kg = KindergartenOf(faction);
            if (kg != null)
            {
                enrollFactor *= KindergartenRules.EducationFactor(kg.enrollmentRate);
                effectiveQuality = KindergartenRules.EffectiveIntakeQuality(effectiveQuality, kg.quality);
            }
        }

        /// <summary>
        /// 暦の日境界ごとに走る盤面の日次処理（TIME-6 #952）：財政を1日ぶん進め（S5）、続いて支持低下イベント判定（S6）。
        /// 連続ドリフト系（艦隊移動・内政・社会連鎖 CampaignRules.Tick）は従来どおり dt で回る（後方互換・段階移行）。
        /// </summary>
        private void RunDailyCampaignTick()
        {
            float secondsPerDay = (float)policyCalendar.Params.secondsPerDay;
            CampaignRules.TickEconomyDay(StrategySession.Campaign, secondsPerDay);
            TickShipyard(secondsPerDay); // 建艦を1日進め、完成を勢力プールへ（#884→#148）
            RunDailyPolicyTick();
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
        /// 暦の年境界ごとに人物を1年ぶん老衰判定する（TIME-6 #952・LIFE-2 #152）。死亡した提督はHUDで告知する。
        /// 純ロジックは <see cref="AnnualLifecycleRules"/> に委譲（乱数は決定論のため roll を渡す）。継承は後段。
        /// </summary>
        private void RunAnnualLifecycleTick()
        {
            campaignYear++;

            // 惑星の人口を1年ぶん動かす（出生・死亡・加齢・LIFE-3 #153）。安定度で出生/死亡が増減＝荒れた星系は人口が減る。
            // Province は StrategySession で永続＝年を跨いで人口が積み上がる。
            if (provinces != null)
                foreach (var kv in provinces)
                {
                    if (kv.Value == null) continue;
                    // 保育園（保育）で出生率↑、POP男女比の偏りで出生率↓（番が組みにくい）＝所有勢力/惑星の状態で出生を増減。
                    StarSystem sys = map != null ? map.GetSystem(kv.Key) : null;
                    float fert = (sys != null ? NurseryFertilityOf(sys.owner) : 1f)
                               * SexRules.BalanceFactor(FemaleShareOf(kv.Value));
                    var baseRates = DemographicsRules.VitalRates.Default;
                    var rates = new DemographicsRules.VitalRates(
                        baseRates.birthRate * fert, baseRates.youthAging, baseRates.workAging, baseRates.elderMortality);
                    PopulationDynamicsRules.TickYear(kv.Value, rates);
                }

            // POP の引っ越し（移住・#194）：隣接星系間で住みよい星系（安定/統合が高い）へ住民が流れる＝荒れた星系は流出で痩せる。
            // 勢力をまたぐ流れ＝亡命（難民）。総量保存・StrategySession 永続で年を跨いで効く。
            if (map != null && provinces != null)
            {
                var migParams = PopulationMigrationRules.MigrationParams.Default;
                foreach (var s in map.systems)
                {
                    if (s == null || !provinces.TryGetValue(s.id, out var from) || from == null) continue;
                    System.Collections.Generic.List<int> neighbors = map.Neighbors(s.id);
                    for (int i = 0; i < neighbors.Count; i++)
                        if (provinces.TryGetValue(neighbors[i], out var to) && to != null)
                            PopulationMigrationRules.TickPair(from, to, migParams, 1f);
                }
            }

            if (commanders == null) return;
            var deceased = AnnualLifecycleRules.ProcessMortality(
                commanders, campaignYear, 1, _ => UnityEngine.Random.value);
            for (int i = 0; i < deceased.Count; i++)
            {
                Person d = deceased[i];
                int age = LifecycleRules.Age(d, campaignYear);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{d.faction} {d.name} 提督 死去（享年 {age}）");
            }

            // 士官学校（#155 LIFE-5 細分化）：各校が幼年学校→士官学校→大学校 の多段で篩い、任官者をロスターへ供給。
            if (academies != null && commanders.Count < OfficerRosterCap)
                for (int i = 0; i < academies.Count; i++)
                    if (academies[i] != null) RunMilitaryAcademy(academies[i]);

            // 大学（文民/技術者の輩出・LIFE-6/7）も年境界で回す。
            RunUniversityTick();
        }

        // 人材の男女比（デモ政策＝銀英伝風：帝国は家父長的で女性が少なく、同盟は平等で多め）。
        private const float ImperialFemaleShare = 0.08f;
        private const float AllianceFemaleShare = 0.35f;

        /// <summary>新任人材に性別を割り当てる（所有勢力の政策男女比・決定論 roll）。性的指向は別軸の検討項目（未実装）。</summary>
        private void AssignSexes(System.Collections.Generic.List<Person> people, Faction faction)
        {
            if (people == null) return;
            float fshare = faction == Faction.同盟 ? AllianceFemaleShare : ImperialFemaleShare;
            for (int i = 0; i < people.Count; i++)
                if (people[i] != null)
                    people[i].sex = UnityEngine.Random.value < fshare ? Sex.女性 : Sex.男性;
        }

        /// <summary>軍学校＝多段の選抜（幼年学校→士官学校→大学校・#155 細分化）。軍属層から入校し、任官者だけを士官名簿へ。</summary>
        private void RunMilitaryAcademy(Academy a)
        {
            // 中学校→高校 の教育チェーンが候補の母数（進学率の複利）と素質（質の上乗せ）を左右する。
            ResolveEducation(a.faction, a.quality, out float enroll, out float eq);
            int sitters = Mathf.Clamp(Mathf.FloorToInt(RecruitablePoolOf(a.faction) * enroll), 0, 20);
            if (sitters <= 0) return;
            var eff = new Academy(a.schoolId, a.faction, a.name, a.capacity, eq);
            var results = MilitaryAcademyRules.RunMilitarySession(eff, campaignYear, sitters, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += results.Count;
            AssignSexes(results, a.faction);

            int 退校 = 0, 幼 = 0, 士 = 0, 参 = 0;
            Person 首席 = null;
            for (int k = 0; k < results.Count; k++)
            {
                Person p = results[k];
                switch (p.militaryDegree)
                {
                    case MilitaryDegree.大学校卒: 参++; break;
                    case MilitaryDegree.士官学校卒: 士++; break;
                    case MilitaryDegree.幼年学校卒: 幼++; break;
                    default: 退校++; break;
                }
                if (MilitaryAcademyRules.IsCommissioned(p.militaryDegree))
                {
                    commanders.Add(p); // 任官（士官学校卒以上）のみ士官名簿へ
                    if (p.hammockNumber == 1) 首席 = p;
                }
            }
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{a.faction} {a.name} 入校{sitters}：参謀{参}/士官{士}/幼年{幼}/退校{退校}"
                + (首席 != null ? $"（首席 tier{首席.rankTier}）" : ""));
        }

        /// <summary>その勢力の徴募源（軍属 #96）＝所有星系の Province を合算（士官学校の輩出数の素・#155）。</summary>
        private float RecruitablePoolOf(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float part = FemaleMilitaryParticipationOf(faction); // 女性の軍参加政策（帝国は低い＝家父長制）
            float pool = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                {
                    // POP の性別構成で徴募源をゲート＝男性＋女性参加ぶんだけ軍に就ける。
                    float elig = SexRules.EligibleMilitaryFraction(FemaleShareOf(prov), part);
                    pool += OccupationRules.RecruitablePool(prov) * elig;
                }
            return pool * NurseryLaborOf(faction); // 保育園＝働く親が増える（労働参加）
        }

        // 女性の軍参加政策（デモ＝銀英伝風：帝国は家父長的で女性の軍参加が低く徴募源が細る／同盟は平等で全員）。
        private const float ImperialFemaleMilitaryParticipation = 0.1f;
        private const float AllianceFemaleMilitaryParticipation = 1f;
        private float FemaleMilitaryParticipationOf(Faction faction)
            => faction == Faction.同盟 ? AllianceFemaleMilitaryParticipation : ImperialFemaleMilitaryParticipation;

        /// <summary>惑星の女性割合（POP の男女比・コホート未設定なら均衡0.5）。</summary>
        private static float FemaleShareOf(Province prov)
            => prov != null && prov.demographics != null ? prov.demographics.femaleShare : SexRules.BalancedFemaleShare;

        /// <summary>その勢力の文民候補（官吏層 #110）＝所有星系の Province を合算（大学の輩出数の素・#156/#157）。</summary>
        private float CivilCandidatePoolOf(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float pool = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                    pool += OccupationRules.Workers(prov, Occupation.官吏);
            return pool * NurseryLaborOf(faction); // 保育園＝働く親が増える（労働参加）
        }

        /// <summary>
        /// 暦の年境界で大学が新任文民（文官/技術者）を輩出し文民ロスターへ供給する（#156/#157 LIFE-6/7）。
        /// 文民も老衰し（LIFE-2）、大学が補充する＝官界の世代交代。<see cref="OfficerAcademyRules"/> の文民版。
        /// </summary>
        private void RunUniversityTick()
        {
            if (civilians == null) return;
            // 文民の老衰（人事の世代交代）
            var deceased = AnnualLifecycleRules.ProcessMortality(civilians, campaignYear, 1, _ => UnityEngine.Random.value);
            for (int i = 0; i < deceased.Count; i++)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{deceased[i].faction} {deceased[i].name} 文官 死去（享年 {LifecycleRules.Age(deceased[i], campaignYear)}）");

            // 上級教育の卒業（官吏/工員層が支える・PERF上限で打ち止め）
            if (civilians.Count >= CivilRosterCap) return;
            if (universities != null)
                for (int i = 0; i < universities.Count; i++)
                {
                    University u = universities[i];
                    if (u == null) continue;
                    if (u.track == CareerTrack.科挙) RunImperialExam(u);
                    else RunTechnocratGraduation(u);
                }
            // 高専（中学校→高専の実務技術者路・高校を経ない別ルート）も年境界で輩出。
            if (colleges != null)
                for (int i = 0; i < colleges.Count; i++)
                    if (colleges[i] != null) RunTechnicalCollege(colleges[i]);
            // 短大／専門学校（高校卒後2年・中堅人材＝官界/現場の裾野）も輩出。
            if (juniorColleges != null)
                for (int i = 0; i < juniorColleges.Count; i++)
                    if (juniorColleges[i] != null) RunJuniorCollege(juniorColleges[i]);
            if (vocationalSchools != null)
                for (int i = 0; i < vocationalSchools.Count; i++)
                    if (vocationalSchools[i] != null) RunVocationalSchool(vocationalSchools[i]);
        }

        /// <summary>科挙＝多段の選抜（童試→郷試→会試→殿試・#156 細分化）。官吏層から受験し、進士だけを高官として登用する。</summary>
        private void RunImperialExam(University u)
        {
            ResolveEducation(u.faction, u.quality, out float enroll, out float eq);
            int sitters = Mathf.Clamp(Mathf.FloorToInt(CivilCandidatePoolOf(u.faction) * enroll), 0, 40);
            if (sitters <= 0) return;
            var eff = new University(u.schoolId, u.faction, u.name, u.track, u.capacity, eq);
            var results = ImperialExamRules.RunExamSession(eff, campaignYear, sitters, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += results.Count;
            AssignSexes(results, u.faction);

            int 生員 = 0, 挙人 = 0, 貢士 = 0, 進士 = 0;
            Person 状元 = null;
            for (int k = 0; k < results.Count; k++)
            {
                Person p = results[k];
                switch (p.examDegree)
                {
                    case ExamDegree.生員: 生員++; break;
                    case ExamDegree.挙人: 挙人++; break;
                    case ExamDegree.貢士: 貢士++; break;
                    case ExamDegree.進士:
                        進士++;
                        if (p.examRank == 1) 状元 = p;
                        civilians.Add(p); // 進士のみ高官として登用（科挙の狭き門）
                        break;
                }
            }
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{u.faction} {u.name} 科挙 受験{sitters}：進士{進士}/貢士{貢士}/挙人{挙人}/生員{生員}"
                + (状元 != null ? $"（状元 tier{状元.rankTier}）" : ""));
        }

        /// <summary>その勢力の技術者候補（工員層 #110）＝所有星系の Province を合算（高専の輩出数の素・#157）。</summary>
        private float TechnicalCandidatePoolOf(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float pool = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                    pool += OccupationRules.Workers(prov, Occupation.工員);
            return pool * NurseryLaborOf(faction); // 保育園＝働く親が増える（労働参加）
        }

        /// <summary>高専＝中学校から直接入る実務技術者路（高校を経ない・#157）。工員層から入学し技術者を文民ロスターへ。</summary>
        private void RunTechnicalCollege(TechnicalCollege c)
        {
            // 高専は高校を経ない＝中学校のみの教育チェーン（includeHighSchool:false）。
            ResolveEducation(c.faction, c.quality, false, out float enroll, out float eq);
            int intake = TechnicalCollegeRules.Intake(c, TechnicalCandidatePoolOf(c.faction) * enroll);
            if (intake <= 0) return;
            var eff = new TechnicalCollege(c.schoolId, c.faction, c.name, c.capacity, eq);
            var grads = TechnicalCollegeRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, c.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{c.faction} {c.name} {grads.Count}名 卒業（技術者）");
        }

        /// <summary>短大の卒業（高校卒後2年・行政中堅文民を官吏層から・#156）。</summary>
        private void RunJuniorCollege(JuniorCollege c)
        {
            ResolveEducation(c.faction, c.quality, out float enroll, out float eq); // 高校卒後＝高校チェーン込み
            int intake = JuniorCollegeRules.Intake(c, CivilCandidatePoolOf(c.faction) * enroll);
            if (intake <= 0) return;
            var eff = new JuniorCollege(c.schoolId, c.faction, c.name, c.capacity, eq);
            var grads = JuniorCollegeRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, c.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{c.faction} {c.name} {grads.Count}名 卒業（行政中堅）");
        }

        /// <summary>専門学校の卒業（高校卒後2年・実務specialist を工員層から・#157）。</summary>
        private void RunVocationalSchool(VocationalSchool s)
        {
            ResolveEducation(s.faction, s.quality, out float enroll, out float eq);
            int intake = VocationalSchoolRules.Intake(s, TechnicalCandidatePoolOf(s.faction) * enroll);
            if (intake <= 0) return;
            var eff = new VocationalSchool(s.schoolId, s.faction, s.name, s.capacity, eq);
            var grads = VocationalSchoolRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, s.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{s.faction} {s.name} {grads.Count}名 卒業（実務）");
        }

        /// <summary>テクノクラート大学の卒業（技術者を文民ロスターへ・#157）。</summary>
        private void RunTechnocratGraduation(University u)
        {
            ResolveEducation(u.faction, u.quality, out float enroll, out float eq);
            int intake = UniversityRules.Intake(u, CivilCandidatePoolOf(u.faction) * enroll);
            if (intake <= 0) return;
            var eff = new University(u.schoolId, u.faction, u.name, u.track, u.capacity, eq);
            var grads = UniversityRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, u.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{u.faction} {u.name} {grads.Count}名 卒業（{u.track}）");
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

        /// <summary>
        /// 浮きHUD（税率行・操作ヒント）を抑制するか。<see cref="StrategyMapWindow"/> が上メニューへ集約する間 true。
        /// banner（戦況/速度/選択）は動的なため抑制しない。
        /// </summary>
        public static bool HideWorldHud = false;

        /// <summary>プレイヤー勢力の税率/国庫/民心/安定度を読み取り表示する（S5・毎フレーム）。</summary>
        private void UpdatePolicyLine()
        {
            if (policyLine == null) return;
            // 上メニューへ集約中は浮き表示を消す（税率行・操作ヒントとも）
            if (HideWorldHud)
            {
                policyLine.text = "";
                if (helpLine != null) helpLine.text = "";
                return;
            }
            FactionState s = PlayerState();
            if (s == null) { policyLine.text = ""; return; }
            float hope = s.community != null ? s.community.hope : 0f;
            float stab = CampaignRules.EffectiveStability(StrategySession.Campaign,
                GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国);
            policyLine.text = $"税率 {s.taxRate * 100f:0}%　国庫 {s.treasury:0}　民心 {hope * 100f:0}%　安定度 {stab * 100f:0}%　[=/- で税率]";
            // 民心が閾値割れで警告色
            policyLine.color = hope < hopeEventThreshold ? new Color(1f, 0.5f, 0.4f) : new Color(0.85f, 0.9f, 0.7f);
        }

        private void Refresh()
        {
            UpdatePolicyLine(); // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示
            // 回廊色：交戦中は戦闘色で点滅、前線（両端が敵対所有＝FTL不可）は赤、要衝は金、その他は通常
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < corridorLines.Count && i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                Color col;
                if (IsEngagedCorridor(c)) col = Color.Lerp(combatColor, Color.white, pulse * 0.6f);
                else col = StrategyRules.IsFtlBlocked(map, c) ? frontlineColor
                    : (c.type == CorridorType.要衝 ? chokeColor : corridorColor);
                corridorLines[i].startColor = corridorLines[i].endColor = col;
            }

            // 除去された艦隊（戦闘で消滅）のマーカーを片付ける
            List<StrategicFleet> gone = null;
            foreach (var kv in fleetMarks)
                if (!reg.fleets.Contains(kv.Key)) (gone ??= new List<StrategicFleet>()).Add(kv.Key);
            if (gone != null)
                foreach (var f in gone)
                {
                    if (fleetMarks[f] != null) Destroy(fleetMarks[f].gameObject);
                    fleetMarks.Remove(f); fleetRings.Remove(f); fleetEta.Remove(f); selectedFleets.Remove(f);
                }

            foreach (var kv in systemDots)
            {
                StarSystem s = map.GetSystem(kv.Key);
                if (s != null && kv.Value != null) kv.Value.color = OwnerColor(s.owner);
            }

            // 攻城状態：制空権健在は ⛨残量%（橙）、ドメイン・ダウン中は 侵略%（赤）
            foreach (var kv in siegeLabels)
            {
                StarSystem s = map.GetSystem(kv.Key);
                TextMesh sl = kv.Value;
                if (s == null || s.planet == null || sl == null) continue;
                Planet p = s.planet;
                if (!p.DomainDown)
                {
                    sl.text = $"制空{Mathf.CeilToInt(100f * p.orbitalDefense / Mathf.Max(1f, p.maxOrbitalDefense))}%";
                    sl.color = defenseColor;
                }
                else
                {
                    sl.text = $"侵攻{Mathf.FloorToInt(100f * p.invasionProgress / Mathf.Max(1f, p.invasionThreshold))}%";
                    sl.color = invadeColor;
                }
            }

            foreach (var kv in fleetMarks)
            {
                StrategicFleet f = kv.Key;
                if (f == null || kv.Value == null) continue;
                kv.Value.transform.position = FleetWorldPos(f);
                kv.Value.color = FactionColor(f.faction);

                if (fleetRings.TryGetValue(f, out var ring)) ring.enabled = selectedFleets.Contains(f);
                if (fleetEta.TryGetValue(f, out var eta))
                    eta.text = f.engaged ? "◆交戦" : (f.IsMoving ? $"ETA {f.Eta:F1}" : (f.IsOnCorridor ? "保持" : $"{f.strength}"));
            }

            DrawSelectedRoutes();
            UpdateBanner();
        }

        /// <summary>選択中の移動艦隊について、現在位置→残り経路の終点までをハイライト表示。</summary>
        private void DrawSelectedRoutes()
        {
            int li = 0;
            for (int s = 0; s < selectedFleets.Count; s++)
            {
                StrategicFleet f = selectedFleets[s];
                if (f == null || !f.IsMoving) continue;

                var pts = new List<Vector3>();
                pts.Add(FleetWorldPos(f));
                var path = GalaxyPathfinder.FindPath(map, f.destinationSystemId, f.FinalDestinationId);
                if (path.Count == 0)
                {
                    StarSystem dst = map.GetSystem(f.destinationSystemId);
                    if (dst != null) pts.Add(dst.position);
                }
                else
                {
                    foreach (int sid in path)
                    {
                        StarSystem sys = map.GetSystem(sid);
                        if (sys != null) pts.Add(sys.position);
                    }
                }
                if (pts.Count < 2) continue;

                LineRenderer lr = GetRouteLine(li++);
                lr.positionCount = pts.Count;
                lr.SetPositions(pts.ToArray());
                lr.enabled = true;
            }
            for (; li < routeLines.Count; li++) routeLines[li].enabled = false;
        }

        private LineRenderer GetRouteLine(int i)
        {
            while (routeLines.Count <= i)
            {
                var lr = NewLine("Route", 1);
                lr.startWidth = lr.endWidth = 0.06f;
                lr.startColor = lr.endColor = new Color(selectColor.r, selectColor.g, selectColor.b, 0.85f);
                routeLines.Add(lr);
            }
            return routeLines[i];
        }

        private void UpdateBanner()
        {
            // イベント通知は左下フィード（NotificationFeed・#964）へ集約。バナーは現在状態のみ表示。
            if (AnyEngaged())
            {
                double total = currentAutoResolveSeconds > 0.0 ? currentAutoResolveSeconds : autoResolveDelay;
                float remain = Mathf.Max(0f, (float)total - engagedElapsed);
                banner.text = $"◆ 回廊で交戦中：ダブルクリックで潜行（手動指揮）／放置で自動解決（残り{remain:0.0}）";
                banner.color = combatColor;
                return;
            }
            if (TryBesiegeStatus(out string bt, out Color bc)) { banner.text = bt; banner.color = bc; return; }
            string speed = paused ? "停止" : $"x{galaxySpeed:0.#}";
            banner.text = $"速度 {speed}　選択 {selectedFleets.Count}隻";
            banner.color = Color.white;
        }

        /// <summary>
        /// 選択中の艦隊が敵の防衛惑星に停泊していれば、攻城の状況（制空権制圧/侵攻/係争中）を返す。
        /// 「敵惑星に入ったのに何も起きない」を防ぐ説明用フィードバック（#131）。
        /// </summary>
        private bool TryBesiegeStatus(out string text, out Color col)
        {
            text = ""; col = Color.white;
            for (int i = 0; i < selectedFleets.Count; i++)
            {
                StrategicFleet f = selectedFleets[i];
                if (f == null || f.IsOnCorridor) continue;
                StarSystem s = map.GetSystem(f.currentSystemId);
                if (s == null || s.planet == null) continue;
                Planet p = s.planet;
                if (!FactionRelations.IsHostile(null, f.faction, null, p.owner)) continue; // 自国/友軍の惑星

                bool contested = false;
                var present = reg.FleetsAt(s.id);
                for (int k = 0; k < present.Count; k++)
                {
                    StrategicFleet g = present[k];
                    if (g != null && !FactionRelations.IsHostile(null, g.faction, null, p.owner)) { contested = true; break; }
                }

                if (contested)
                {
                    text = $"{s.systemName}：係争中（敵守備隊あり）＝攻城停止。守備隊を排除せよ";
                    col = combatColor;
                }
                else if (!p.DomainDown)
                {
                    text = $"{s.systemName} を攻城中：制空権 {Mathf.CeilToInt(100f * p.orbitalDefense / Mathf.Max(1f, p.maxOrbitalDefense))}%（S-AVが制圧）／ダブルクリックで突入";
                    col = defenseColor;
                }
                else if (!p.Captured)
                {
                    text = $"{s.systemName} へ侵攻中：侵略 {Mathf.FloorToInt(100f * p.invasionProgress / Mathf.Max(1f, p.invasionThreshold))}%／ダブルクリックで突入";
                    col = invadeColor;
                }
                else continue;
                return true;
            }
            return false;
        }

        // ===== 入力 =====

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            GameClock clock = StrategySession.Clock;
            // ポーズ/速度プリセットは統一クロックを駆動（TIME-1）。速度の +/- は TimeDisplay が全シーン共通で処理。
            if (kb.spaceKey.wasPressedThisFrame && clock != null) clock.TogglePause();
            // S5：税率レバー（] で増税 / [ で減税。+/- は時間速度に割当のためブラケットへ）。
            FactionState ps = PlayerState();
            if (ps != null)
            {
                if (kb.rightBracketKey.wasPressedThisFrame) ps.taxRate = Mathf.Clamp01(ps.taxRate + taxStep);
                if (kb.leftBracketKey.wasPressedThisFrame) ps.taxRate = Mathf.Clamp01(ps.taxRate - taxStep);
            }
            if (clock != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { clock.SetSpeed(0.5f); clock.Resume(); }
                if (kb.digit2Key.wasPressedThisFrame) { clock.SetSpeed(1f); clock.Resume(); }
                if (kb.digit3Key.wasPressedThisFrame) { clock.SetSpeed(2f); clock.Resume(); }
            }
            if (kb.iKey.wasPressedThisFrame) OpenSystemInfoAtMouse(); // 星系情報パネル(#759)
        }

        private void HandleMouse()
        {
            if (Mouse.current == null || cam == null) return;

            // 左クリック：ダブルクリックで交戦中の回廊へ潜行（実会戦）、単クリックは選択
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 w = WorldMouse();

                // ダブルクリック判定（実時間・近接）→ 交戦中の回廊なら潜行
                float now = Time.realtimeSinceStartup;
                bool dbl = (now - lastClickTime <= doubleClickWindow) && Vector2.Distance(w, lastClickWorld) <= 0.6f;
                lastClickTime = now; lastClickWorld = w;
                // 交戦回廊への潜行＞攻城突入＞（どちらも無ければ）平時の星系をシステムビューで閲覧
                if (dbl && (TryDescend(w) || TryDescendPlanet(w) || TryEnterSystem(w))) return;

                bool additive = ShiftHeld();
                StrategicFleet nf = NearestFleet(w, 0.7f);
                if (nf != null)
                {
                    if (additive) { if (!selectedFleets.Remove(nf)) selectedFleets.Add(nf); }
                    else { selectedFleets.Clear(); selectedFleets.Add(nf); }
                }
                else if (!additive) selectedFleets.Clear();
            }
            // 右クリック：クリックに近い方を採用。星系の点が近ければ進軍、回廊の線が近ければ
            // その位置で停止保持（端点に居る選択艦のみ）。
            else if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (selectedFleets.Count == 0) return;
                Vector2 w = WorldMouse();

                int sysId = NearestSystemDist(w, out float sysD);
                bool hasCorr = NearestCorridor(w, out Corridor c, out float fracFromA, out float corrD);

                if (sysId >= 0 && sysD <= systemClickRadius)
                {
                    // 星系の点の上＝最優先で進軍。ハブ星系は放射状の回廊が中心を通るため
                    // 近さ比較だと回廊が勝って惑星へ入れない → 星系の判定半径を優先する。
                    foreach (var f in selectedFleets) if (f != null) f.WarpTo(map, sysId);
                }
                else if (hasCorr && corrD <= 0.6f)
                {
                    // 回廊の線上（星系から離れた位置）＝その位置で停止保持
                    foreach (var f in selectedFleets)
                    {
                        if (f == null) continue;
                        if (f.currentSystemId == c.aId) f.HoldOnCorridor(map, c.bId, fracFromA);
                        else if (f.currentSystemId == c.bId) f.HoldOnCorridor(map, c.aId, 1f - fracFromA);
                    }
                }
                else if (sysId >= 0 && sysD <= 1.6f)
                {
                    // 星系の近く（フォールバック）＝進軍
                    foreach (var f in selectedFleets) if (f != null) f.WarpTo(map, sysId);
                }
            }
        }

        /// <summary>
        /// クリック位置に交戦中の回廊があれば、その会戦へ潜行（実会戦・Battleシーン）する（#586 ①）。
        /// 潜行＝手動指揮。戻ると結果が反映され、観ていなかった他戦線は自動解決される。
        /// </summary>
        private bool TryDescend(Vector2 w)
        {
            if (!NearestCorridor(w, out Corridor c, out _, out float d) || d > 0.6f) return false;
            if (!StrategyRules.TryGetEngagementOnCorridor(reg, c.aId, c.bId, out var a, out var b)) return false;
            BattleHandoff.Queue(a, b, "Strategy");

            // 旗幟（#817）：国家状態から基準忠誠/調略の付け入りやすさを積む＝腐った国の艦隊は会戦中に寝返りうる。
            var campaign = StrategySession.Campaign;
            if (campaign != null)
            {
                FactionState sa = CampaignRules.GetState(campaign, a.faction);
                FactionState sb = CampaignRules.GetState(campaign, b.faction);
                if (sa != null) { BattleHandoff.loyaltyA = FactionLoyaltyRules.BaselineLoyalty(sa); BattleHandoff.intrigueA = FactionLoyaltyRules.BribeSusceptibility(sa); }
                if (sb != null) { BattleHandoff.loyaltyB = FactionLoyaltyRules.BaselineLoyalty(sb); BattleHandoff.intrigueB = FactionLoyaltyRules.BribeSusceptibility(sb); }
            }

            SceneManager.LoadScene("Battle");
            return true;
        }

        /// <summary>
        /// クリック位置の星系が敵の防衛惑星で、自軍が攻城中なら、惑星攻城の戦術マップ（Battleシーン）へ突入する（#131）。
        /// 中心に惑星・攻城艦隊が包囲・首飾り射程の外までの状態で開始する。
        /// </summary>
        private bool TryDescendPlanet(Vector2 w)
        {
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > systemClickRadius) return false;
            StarSystem s = map.GetSystem(sysId);
            if (s == null || s.planet == null) return false;

            StrategicFleet besieger = FindBesieger(sysId, s.planet.owner);
            if (besieger == null) return false;

            float defRatio = s.planet.maxOrbitalDefense > 0f ? s.planet.orbitalDefense / s.planet.maxOrbitalDefense : 0f;
            float invRatio = s.planet.invasionThreshold > 0f ? s.planet.invasionProgress / s.planet.invasionThreshold : 0f;
            BattleHandoff.QueuePlanetSiege(s.id, s.systemName, s.planet.owner, defRatio, invRatio,
                besieger.faction, besieger.strength, "Strategy", s.planet.kind);
            SceneManager.LoadScene("Battle");
            return true;
        }

        /// <summary>
        /// クリック位置に星系があれば、戦闘中でなくてもその星系の戦術マップ（システムビュー＝恒星系の閲覧）へ入る。
        /// 交戦回廊(TryDescend)・攻城突入(TryDescendPlanet)が優先で、どれにも該当しない平時の星系がここに来る。
        /// </summary>
        private bool TryEnterSystem(Vector2 w)
        {
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > systemClickRadius) return false;
            StarSystem s = map.GetSystem(sysId);
            if (s == null) return false;
            BattleHandoff.QueueSystemView(s.id, s.systemName, s.owner, "Strategy");
            SceneManager.LoadScene("Battle");
            return true;
        }

        /// <summary>指定星系に停泊し惑星所有者と敵対する艦隊（攻城側）を返す。選択中を優先、無ければ任意。</summary>
        private StrategicFleet FindBesieger(int sysId, Faction planetOwner)
        {
            for (int i = 0; i < selectedFleets.Count; i++)
            {
                StrategicFleet f = selectedFleets[i];
                if (f != null && !f.IsOnCorridor && f.currentSystemId == sysId &&
                    FactionRelations.IsHostile(null, f.faction, null, planetOwner)) return f;
            }
            foreach (var f in reg.FleetsAt(sysId))
                if (f != null && FactionRelations.IsHostile(null, f.faction, null, planetOwner)) return f;
            return null;
        }

        /// <summary>交戦中（engaged）の艦隊が1隻でも居るか。</summary>
        private bool AnyEngaged()
        {
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

        private Vector2 WorldMouse()
        {
            Vector3 sp = Mouse.current.position.ReadValue();
            return cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
        }

        private static bool ShiftHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private StrategicFleet NearestFleet(Vector2 w, float radius)
        {
            StrategicFleet best = null; float bestD = radius;
            foreach (var f in reg.fleets)
            {
                if (f == null) continue;
                float d = Vector2.Distance(FleetWorldPos(f), w);
                if (d <= bestD) { bestD = d; best = f; }
            }
            return best;
        }

        /// <summary>最も近い星系IDとその距離を返す（無ければ -1）。</summary>
        private int NearestSystemDist(Vector2 w, out float dist)
        {
            int best = -1; dist = float.MaxValue;
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                float d = Vector2.Distance(s.position, w);
                if (d < dist) { dist = d; best = s.id; }
            }
            return best;
        }

        /// <summary>クリック点に最も近い回廊（線分）と、その上の位置 fracFromA（aId→bId で0..1）と距離を返す。</summary>
        private bool NearestCorridor(Vector2 w, out Corridor best, out float fracFromA, out float dist)
        {
            best = null; fracFromA = 0f; dist = float.MaxValue;
            foreach (var c in map.corridors)
            {
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                Vector2 pa = a.position, ab = b.position - a.position;
                float len2 = ab.sqrMagnitude;
                float t = (len2 > 0f) ? Mathf.Clamp01(Vector2.Dot(w - pa, ab) / len2) : 0f;
                float d = Vector2.Distance(w, pa + ab * t);
                if (d < dist) { dist = d; best = c; fracFromA = t; }
            }
            return best != null;
        }

        // ===== ヘルパ =====

        private Vector2 FleetWorldPos(StrategicFleet f)
        {
            StarSystem cur = map.GetSystem(f.currentSystemId);
            if (cur == null) return Vector2.zero;
            if (!f.IsOnCorridor) return cur.position; // 停泊中は星系。回廊上（前進・保持）は補間
            StarSystem dst = map.GetSystem(f.destinationSystemId);
            if (dst == null) return cur.position;
            return Vector2.Lerp(cur.position, dst.position, f.Progress);
        }

        private Color OwnerColor(Faction f) => (f == Faction.帝国) ? empireColor : allianceColor;
        private Color FactionColor(Faction f) => Color.Lerp((f == Faction.帝国) ? empireColor : allianceColor, Color.white, 0.35f);

        private LineRenderer NewLine(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = lineMat;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.sortingOrder = order;
            return lr;
        }

        private GameObject MakeLabel(Transform parent, string text, Vector3 localOffset, float charSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = FontProvider.JapaneseFont;
            tm.fontSize = 48;
            tm.characterSize = charSize * 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            if (tm.font != null) mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = 6;
            return go;
        }

        private static Sprite MakeDiscSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    cols[y * size + x] = (d <= r - 1f) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(cols);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void OnDestroy()
        {
            if (lineMat != null) Destroy(lineMat);
            if (disc != null && disc.texture != null) Destroy(disc.texture);
        }
    }
}
