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

        // 朝廷の権威（律令の形骸化・官僚制基盤）。封建の世＝既に低め（武家政権相当）＝官職は名誉職化方向。
        // 文官ネームドの考課・叙位（五位の壁）はこの権威で効く（BureaucracyCareerRules / RitsuryoFormalizationRules）。
        private CourtAuthority courtAuthority = new CourtAuthority(0.35f);

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
            if (SystemDetailPanel.IsOpen || StrategyEventPanel.IsOpen || FleetOrganizationPanel.IsOpen || DecisionBoardPanel.IsOpen || CampaignEndOverlay.IsOpen) return;

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

            AnnounceCampaignObjective(); // 遊べる縦スライス：目標と初手をプレイヤーに提示（オンボーディング）
        }

        // --- オンボーディング（目標提示＋初手ガイド） ---
        private static bool objectiveAnnounced;

        /// <summary>キャンペーン開始時に勝利目標と最初の操作を通知で提示する（セッション一度きり）。勝敗は <see cref="CampaignVictoryRules"/>。</summary>
        private void AnnounceCampaignObjective()
        {
            if (objectiveAnnounced) return;
            objectiveAnnounced = true;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            int pct = Mathf.RoundToInt(CampaignVictoryRules.CampaignVictoryParams.Default.dominationFraction * 100f);
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意,
                $"【目標】{player} で銀河の {pct}% を支配せよ（敵を全制圧でも勝利／全星系を失えば敗北）");
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報,
                "操作：星系を右クリックで進軍 → 前線で接触 → 交戦中の回廊をダブルクリックで潜行（会戦へ）。Space/1-3=速度、H=ヘルプ。");
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
                // 文官行政（総督＝地方＋宰相＝中央）が安定度目標を押し上げる＝名実の乖離で朝廷の権威ぶん減衰（権威0なら効かない）。
                GovernanceRules.Tick(prov, owner, supplyOk: true, atWar: HasHostileFleetAt(s),
                    deltaTime: dt, policy: GovernancePolicy.民生, adminBonus: SystemAdminBonus(s));
            }
        }

        /// <summary>所有勢力の在任宰相による安定度寄与（名実の乖離＝朝廷の権威で減衰・<see cref="AdministrationRules"/>）。空席/非デモ勢力は0。</summary>
        private float PremierAdminBonus(Faction owner)
        {
            if (civilOffices == null) return 0f;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                if (DemoFactions[f] != owner) continue;
                Office office = civilOffices[f];
                if (office == null) return 0f;
                var premier = GovernmentRegistry.GetHolder(office) as Person;
                float authority = courtAuthority != null ? courtAuthority.authority : 0f;
                return AdministrationRules.StabilityContribution(premier, authority, AdministrationRules.AdminParams.Default);
            }
            return 0f;
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

            // 開始は帝国3:同盟3＝50:50（勝利/敗北しきい値70%＝開始時はどちらも未達＝開幕で決着しない）。
            // 帝国＝右クラスタ{0,2,3}／同盟＝左クラスタ{1,4,5}。中央ドラコ(3)が唯一の前線ハブ。
            map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "アスタ", new Vector2(0f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(1, "ベガ", new Vector2(-5f, -3f), Faction.同盟));
            map.AddSystem(new StarSystem(2, "ケレス", new Vector2(5f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(3, "ドラコ", new Vector2(0f, -0.5f), Faction.帝国));
            map.AddSystem(new StarSystem(4, "エリス", new Vector2(-2.5f, 1f), Faction.同盟));
            map.AddSystem(new StarSystem(5, "フェニクス", new Vector2(-3.5f, -2.5f), Faction.同盟));

            map.AddCorridor(new Corridor(2, 0, 4f, CorridorType.要衝));
            map.AddCorridor(new Corridor(0, 3, 5f));
            map.AddCorridor(new Corridor(3, 1, 4f));  // 前線：帝国ドラコ ⟷ 同盟ベガ
            map.AddCorridor(new Corridor(3, 4, 3f));  // 前線：帝国ドラコ ⟷ 同盟エリス
            map.AddCorridor(new Corridor(4, 1, 2f));
            map.AddCorridor(new Corridor(1, 5, 2f));
            map.AddCorridor(new Corridor(4, 5, 3f));

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
                factor *= ShipbuildingFundingFactor(yard.faction);   // G3：建艦予算の出資度が建艦速度に効く（#163→#884）
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
            civilians = new List<Person>();
            if (StrategySession.PendingPeople != null)
            {
                // ロード復元：保存済みロスターを採用（軍人=提督名簿／文民=文官名簿に振り分け）。
                var loaded = StrategySession.PendingPeople;
                int maxId = 0;
                for (int i = 0; i < loaded.Count; i++)
                {
                    Person p = loaded[i];
                    if (p == null) continue;
                    if (p.role == PersonRole.軍人) commanders.Add(p); else civilians.Add(p);
                    if (p.id > maxId) maxId = p.id;
                }
                nextPersonId = maxId + 1;
                StrategySession.PendingPeople = null; // 消費（再構築は一度きり）
            }
            else
            {
                int y = campaignYear;
                int id = 1;
                // 各勢力：壮年（当面は死ににくい）＋老齢（老衰しうる）
                commanders.Add(new Person(id++, "ミッターマイアー", Faction.帝国, PersonRole.軍人) { birthYear = y - 39, rankTier = 8 });
                commanders.Add(new Person(id++, "メックリンガー", Faction.帝国, PersonRole.軍人) { birthYear = y - 79, rankTier = 8 });
                commanders.Add(new Person(id++, "アッテンボロー", Faction.同盟, PersonRole.軍人) { birthYear = y - 41, rankTier = 7 });
                commanders.Add(new Person(id++, "ビュコック", Faction.同盟, PersonRole.軍人) { birthYear = y - 88, rankTier = 9 });
                id = SeedFoundingYouth(id, y); // 世代交代ループの種＝結婚適齢の若者（男女）
                nextPersonId = id; // 卒業生はこの続き番号で採番
            }

            // 特殊作戦部隊（#SOF・SEAL型選抜）：勢力ごとに候補を多段の苛烈な選抜で篩い、認定者を SOF 出身にする。
            RunSofSelection();

            // 士官学校（#155 LIFE-5）：各勢力に1校。質に差を付ける（名門は良将を出す）。
            academies = new List<Academy>
            {
                new Academy(schoolId: 1, faction: Faction.帝国, name: "帝国士官学校", capacity: 6, quality: 0.6f),
                new Academy(schoolId: 2, faction: Faction.同盟, name: "同盟士官学校", capacity: 6, quality: 0.55f),
            };

            // 大学（#156/#157 LIFE-6/7）：各勢力に文官大学＋帝国に工科大学（テクノクラート）。文民/技術者を輩出。
            // civilians は上で初期化済（ロード復元 or 空）。ここでは再生成しない。
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
        /// 暦の年境界ごとに人物を1年ぶん老衰判定する（TIME-6 #952・LIFE-2 #152）。死亡した提督はHUDで告知する。
        /// 純ロジックは <see cref="AnnualLifecycleRules"/> に委譲（乱数は決定論のため roll を渡す）。継承は後段。
        /// </summary>
        // 勢力の教育シグナル（高校の普及率×質）＝POP労働技能の上限（#2034 SKILL-3）に使う。未設定は既定。
        private void EducationSignalOf(Faction f, out float enrollment, out float quality)
        {
            enrollment = 0.7f; quality = 0.55f; // 既定
            if (highSchools != null)
                for (int i = 0; i < highSchools.Count; i++)
                    if (highSchools[i] != null && highSchools[i].faction == f)
                    { enrollment = highSchools[i].enrollmentRate; quality = highSchools[i].quality; return; }
        }

        /// <summary>
        /// 特殊作戦部隊（#SOF・SEAL型選抜）：勢力ごとに軍人候補を選抜スコアで多段に篩い（基礎→地獄週→卒業）、
        /// 認定者を SOF 出身にする（提督として能力上昇＝戦闘で常時+5%・側背/包囲で+20%）。開始時に一度。
        /// </summary>
        private void RunSofSelection()
        {
            if (commanders == null) return;
            var byFaction = new Dictionary<Faction, List<SofCandidate>>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.role != PersonRole.軍人) continue;
                float score = SpecialForcesRules.SelectionScore(c.leadership, c.mobility, c.attack);
                if (!byFaction.TryGetValue(c.faction, out var list)) { list = new List<SofCandidate>(); byFaction[c.faction] = list; }
                list.Add(new SofCandidate(c.id, score));
            }
            foreach (var kv in byFaction)
            {
                List<int> passed = SpecialForcesRules.Funnel(kv.Value);
                for (int j = 0; j < passed.Count; j++)
                {
                    Person p = ResolveCommander(passed[j]);
                    if (p == null) continue;
                    p.isSpecialForces = true;
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{p.faction} {p.name} が特殊作戦部隊の選抜を突破（SOF認定）");
                }
            }
        }

        // 叙勲の配線パラメータ（#2263・デモ既定）
        private const int MaxMedalsPerCommander = 5;  // 1人あたり叙勲数の上限（乱発防止）
        private const int CommissionAge = 22;          // 任官年齢（在役年数の起点）

        /// <summary>
        /// 戦略の年次叙勲（#2263）。武勲ある将官（中将以上）へ階級に応じて叙勲し、
        /// 恩給見込み（`RetirementRules.PensionFactor` × 勲章の `MedalRegistry.PensionFactor`）と名誉を通知する。
        /// 史実：勲章は恩給（年金）と名誉を増す。乱発防止に1人あたり上限＋階級依存の決定論抽選。
        /// </summary>
        private void RunAnnualMedalTick()
        {
            if (commanders == null) return;
            var rp = RetirementRules.RetireParams.Default;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.IsDeceased) continue;
                if (c.rankTier < 7) continue;                                  // 中将以上の将官が対象
                if (MedalRegistry.Count(c.id) >= MaxMedalsPerCommander) continue; // 上限

                // 階級が高いほど叙勲されやすい（決定論抽選）。
                float roll = Mathf.Abs(Mathf.Sin(c.id * 12.9898f + campaignYear * 78.233f));
                roll -= Mathf.Floor(roll);
                if (roll > c.rankTier / 20f) continue;

                MedalKind kind = c.rankTier >= 9 ? MedalKind.勲功章 : MedalKind.武功章;
                float merit = Mathf.Clamp(c.rankTier * 10f, 0f, 100f);
                Decoration d = MedalRegistry.Award(c.id, kind, merit, campaignYear, $"{c.name} の武勲");

                int age = LifecycleRules.Age(c, campaignYear);
                int years = Mathf.Max(0, age - CommissionAge);
                float pension = RetirementRules.PensionFactor(years, rp) * MedalRegistry.PensionFactor(c.id);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{c.faction} {c.name} に{kind}{d.grade}を叙勲。恩給見込み {pension:0.00}（勲章×{MedalRegistry.PensionFactor(c.id):0.00}）・名誉{MedalRegistry.Prestige(c.id):0}");
            }
        }

        // 宗教/文化の配線パラメータ（#172-175/#194・デモ既定）
        private const float RulerFaithDevotion = 0.6f;      // 支配勢力の信仰の強さ（デモ既定）
        private const float ReligionStabilityScale = 10f;   // 信仰の社会効果→安定度への反映スケール
        private const float SeparatismStabilityScale = 5f;  // 分離主義→安定度低下スケール

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

                    // POP の労働技能を1年ぶん形成（教育→OJT・#2034 配線）。教育の普及/質で上限が決まり、年々熟練が積み上がる。
                    float enroll, qual;
                    EducationSignalOf(sys != null ? sys.owner : Faction.帝国, out enroll, out qual);
                    PopLaborTickRules.TickYear(kv.Value, enroll, qual, EducationLevel.高等, PopLaborTickRules.DefaultLearnRate);

                    // 労働市場を1年ぶん（POPLAB-2/3/6 + SKILL-5 配線）：安定度#109 連動の需要へ職業配分が収束＝不安定で失業↑。
                    // 戦時（前線星系）は生産労働→軍属（総力戦#96）。技能が高い大衆ほど速く再配置（リスキリング#2034）。
                    float overall = PopLaborTickRules.OverallSkill(kv.Value);
                    float flow = LaborMarketTickRules.ReskillingFlowRate(LaborMarketTickRules.DefaultFlowRate, overall);
                    float mob = (sys != null && HasHostileFleetAt(sys)) ? LaborMarketTickRules.WarMobilizationRate : 0f;
                    LaborMarketTickRules.TickYear(kv.Value, mob, flow);
                    // 賃金を1年ぶん（POPLAB-4 配線）：労働逼迫（就業率）×技能で賃金指数が動く。
                    LaborWageTickRules.TickYear(kv.Value, LaborWageTickRules.DefaultAdjustRate);

                    // POP の消費需要を1年ぶん（#2042 配線）：購買力(賃金#1969)×人口で需要、生産力(安定度#109)で供給→充足→生活水準#181・飢餓。
                    // 不安定/占領/補給切れで生産力が落ちると必需が不足し飢餓に。富裕(高賃金)ほど上位財の需要が増える。
                    float outFactor = GovernanceRules.OutputFactor(kv.Value);
                    float popC = kv.Value.population;
                    PopConsumptionTickRules.TickYear(kv.Value, kv.Value.wageIndex,
                        popC * outFactor, popC * outFactor * 0.4f, popC * outFactor * 0.15f);

                    // 宗教(#172-175 配線)：住民の信仰を1年ぶん進め、信仰の社会効果を安定度へ緩やかに反映。
                    // 統合が進んだ惑星は支配勢力の信仰と親和（affinityMatch）。基準値はTick側で非破壊。
                    bool affinity = kv.Value.integration > 0.5f;
                    ReligionTickRules.TickYear(kv.Value, RulerFaithDevotion, affinity);
                    kv.Value.stability = Mathf.Clamp(
                        kv.Value.stability + (ReligionTickRules.SocialFactor(kv.Value) - 1f) * ReligionStabilityScale,
                        0f, 100f);

                    // 文化・民族(#194 配線)：同化/分離を1年ぶん進め、分離主義が安定度を蝕む。
                    // 戦時(前線)・低統合は分離を促す。亡命#194 の移住と相補的。
                    bool atWarHere = sys != null && HasHostileFleetAt(sys);
                    CultureTickRules.TickYear(kv.Value, kv.Value.integration > 0.5f, atWarHere);
                    float separatism = CultureTickRules.SeparatismRisk(kv.Value);
                    if (separatism > 0f)
                        kv.Value.stability = Mathf.Clamp(kv.Value.stability - separatism * SeparatismStabilityScale, 0f, 100f);
                    if (separatism > 0.6f && sys != null)
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                            $"{sys.systemName}：分離主義が高まっている（{separatism:0.0}）");
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

            // 代表生産チェーン（VCHAIN-6・#2091）：森林→木材→建材→住宅 を惑星ごとに年次で流し、住宅充足で生活水準を補正。
            RunSupplyChainTick();

            // 汎用BOM（BOM-6・#2098）：消費財（食品/衣類）をレシピで生産し、需要充足で生活水準を補正。
            RunBomConsumerTick();

            // SCM計画（SCM-6・#2105）：消費財需要をMRP展開し、原材料の逼迫（ボトルネック）を勢力ごとに通知（read-only）。
            RunScmPlanTick();

            // 外交（DIPLO-6・#2119）：勢力ペアの関係をドリフトし、AIが宣戦/講和/同盟を決める。
            RunDiplomacyTick();

            // 法の支配と法と秩序（LAW-6・#2126）：勢力の法の支配＋惑星の治安を解き、安定へ反映・抑圧を通知。
            RunLawTick();

            // 叙勲（#2263）：武勲ある将官へ年次で叙勲し、恩給見込み（勲章で増）・名誉を通知。
            RunAnnualMedalTick();

            // 財政の年（#161-163 配線）：予算編成→形式財政（債務/利払い）で予算と執行の1年を閉じる。
            RunFiscalYearTick();

            // 政体進化（#117 配線）：首長制→民主(立憲君主制/共和制)or独裁(共産主義/指導者独裁)へ社会シグナルで分岐進化。
            RunRegimeEvolutionTick();

            // 政党政治（#159 配線）：民主政治の勢力で政党制が成熟度に応じ二大政党へ収束し、衆参の選挙が回り、分断危機を通知。
            RunPoliticsTick();

            // キャンペーンの勝敗（遊べる縦スライスの核）：制覇/全制圧で勝利・滅亡/敵制覇で敗北。決着で時計を止めて終了画面。
            RunCampaignVictoryCheck();

            // 年境界の自動保存（決着後は保存しない＝終了状態で上書きしない）。閉じても進行が消えない。
            if (!campaignDecided) AutoSaveCampaign();

            // 世代交代（#159 配線）：成年が結婚し夫婦が子をなして名簿に加わる＝血統が年々更新される（死は下の老衰で）。
            RunGenerationTick();

            if (commanders == null) return;
            var deceased = AnnualLifecycleRules.ProcessMortality(
                commanders, campaignYear, 1, _ => UnityEngine.Random.value);
            for (int i = 0; i < deceased.Count; i++)
            {
                Person d = deceased[i];
                int age = LifecycleRules.Age(d, campaignYear);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{d.faction} {d.name} 提督 死去（享年 {age}）");

                // 配偶者を死別（生存配偶者の婚姻を解除）。
                if (d.spouseId >= 0)
                {
                    Person spouse = commanders.Find(x => x != null && x.id == d.spouseId);
                    PersonMarriageRules.Widow(spouse);
                }

                // ネームド資産の相続/没収（NASSET-4/6・#2063）：故人の固有資産を同勢力の最高位の存命司令へ相続、不在なら国家へ没収。
                var estate = NamedAssetRegistry.OwnedByPerson(d.id);
                if (estate.Count > 0)
                {
                    Person heir = FindHeir(d);
                    for (int e = 0; e < estate.Count; e++)
                    {
                        var asset = estate[e];
                        if (!AssetTransferRules.CanTransfer(asset)) continue; // 称号など不可は本人と消える
                        if (heir != null) AssetTransferRules.Inherit(asset, heir.id);
                        else AssetTransferRules.Confiscate(asset, d.faction);
                    }
                    string dest = heir != null ? $"{heir.name} が相続" : "国家へ没収";
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{d.name} の資産（{estate.Count}件）→ {dest}");
                }

                // 金融資産の相続（NFIN-6・#2070）：故人の保有持分を最高位の相続人へ、不在なら国家へ。
                var fin = FinancialHoldingRegistry.OwnedByPerson(d.id);
                if (fin.Count > 0)
                {
                    Person heir = FindHeir(d);
                    for (int e = 0; e < fin.Count; e++)
                    {
                        if (heir != null) { fin[e].ownerKind = AssetOwnerKind.人物; fin[e].ownerPersonId = heir.id; }
                        else { fin[e].ownerKind = AssetOwnerKind.国家; fin[e].ownerFaction = d.faction; }
                    }
                }

                // 不動産の細分化（NFIN-5/6・#2070＝分地相続）：故人の権利証を複数の相続人へ等分＝惑星の持分が細かく分かれる。
                var deeds = PropertyDeedRegistry.OwnedByPerson(d.id);
                if (deeds.Count > 0)
                {
                    var heirs = FindHeirs(d, 3); // 上位3名で分割（細分化傾向）
                    for (int e = 0; e < deeds.Count; e++)
                    {
                        if (heirs.Count > 0) PropertyFragmentationRules.FragmentOnInheritance(deeds[e], heirs);
                        else { deeds[e].ownerKind = AssetOwnerKind.国家; deeds[e].ownerFaction = d.faction; } // 相続人不在は国家へ（細分化せず）
                    }
                    if (heirs.Count > 1)
                        NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{d.name} の所領が {heirs.Count} 人へ分割相続（細分化）");
                }
            }

            // 人物の財産を1年ぶん（PFIN-6・#2056 配線）：俸給#1969 から特性で貯金/投資/浪費し財産が増減。
            // デモ：id で財産行動特性を割り振る。投資型は変動（暴落リスク#185）・浪費型は貯まらない・貯金型は堅実。
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.deathYear != 0) continue;
                c.financialTrait = (FinancialTrait)(System.Math.Abs(c.id) % 3); // 0貯金/1投資/2浪費
                float salary = 50f + c.rankTier * 50f; // 俸給 proxy（階級#14 比例・WAGE#1969）
                float ret = 0.05f + (c.financialTrait == FinancialTrait.投資 ? (UnityEngine.Random.value - 0.5f) * 0.6f : 0f); // 投資は±変動
                PersonFinanceTickRules.TickYear(c, salary, ret);
            }

            // ネームド資産（NASSET-6・#2063 配線）：人物/国家が固有名の資産（旗艦・宮殿等）を持ち、収益→財産・値上がり・相続。
            SeedNamedAssets();                                  // デモ資産シード（冪等）
            NamedAssetTickRules.TickYear(ResolveCommander);     // 純収益→所有者 wealth#2056・時価値上がり
            for (int f = 0; f < DemoFactions.Length; f++)       // 国家資産の純収益は国庫#163 相当へ（デモはログのみ）
            {
                float fInc = NamedAssetEffectRules.FactionAnnualIncome(DemoFactions[f]);
                if (fInc != 0f)
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{DemoFactions[f]} 国有資産収益 {fInc:0}");
            }

            // ネームド金融資産・不動産（NFIN-6・#2070 配線）：株式/債券/投資信託の配当・惑星所有権の地代→財産、紙くず化。
            SeedFinancialAssets();                                  // デモ金融/不動産シード（冪等）
            MaybeCrashAStock();                                     // 紙くず化デモ（暴落#185）
            NamedFinancialTickRules.TickYear(ResolveCommander);    // 配当/地代→所有者 wealth#2056
            for (int f = 0; f < DemoFactions.Length; f++)          // 国家の金融/不動産収益（デモはログ）
            {
                float fInc = NamedFinancialTickRules.FactionAnnualIncome(DemoFactions[f]);
                if (fInc != 0f)
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{DemoFactions[f]} 金融/地代収益 {fInc:0}");
            }

            // 国家・惑星の行政物資消費（STATEDEM-6・#2077 配線）：産出を行政・インフラが消費し、不足で統治が逼迫＝安定度低下。
            RunStateConsumptionTick();

            // 士官学校（#155 LIFE-5 細分化）：各校が幼年学校→士官学校→大学校 の多段で篩い、任官者をロスターへ供給。
            if (academies != null && commanders.Count < OfficerRosterCap)
                for (int i = 0; i < academies.Count; i++)
                    if (academies[i] != null) RunMilitaryAcademy(academies[i]);

            // 退役（#530-536 配線）：階級別の停年に達した現役将校を退役へ（元帥は終身）。退役者は昇進・入校の対象外＝以後は老衰で退場。
            RunRetirementTick();

            // 陸軍大学校のエリート街道（#SCHOOL-AGE 配線）：現役将校を大学校へ入校（学校配属＝艦隊配属不可）→卒業で参謀＝恩賜の軍刀組→昇進優遇。
            RunWarCollegeCareerTick();

            // 人事の空席補充（#152）と捕虜の処遇（#154）：死亡/退役/捕虜で空いた要職を後任補充、捕虜は解放/登用/処断で処遇。
            RunPersonnelTurnoverTick();

            // 大学（文民/技術者の輩出・LIFE-6/7）も年境界で回す。
            RunUniversityTick();

            // 朝廷の権威の動態（官僚制基盤）：戦乱で武家台頭＝権威↓（戦国化）／平時は律令が回復。以後の考課・銓衡・内政に効く。
            RunCourtAuthorityTick();

            // 文官の官歴（官僚制基盤）：文民ネームドに位階を叙し、考課で叙位・五位の壁を回す（朝廷の権威で効く）。
            RunBureaucracyTick();

            // 文官の銓衡配属（官僚制基盤）：叙位された文官を官位相当＋考課で宰相（文官要職）へ任命する（式部省の選叙）。
            RunCivilAppointmentTick();

            // 総督（地方官）の銓衡配属（官僚制基盤）：所有星系ごとに官位相当の文官を配属＝受領/国司。
            RunGovernorAppointmentTick();
        }

        /// <summary>
        /// 退役（#530-536 配線）：現役将校が階級別の停年に達したら退役へ編入（元帥 tier は終身＝対象外）。
        /// 退役者はロスターに残り（資産・老衰死の対象）、昇進・大学校入校からは外れる＝現役→退役→死亡 の一方向。
        /// </summary>
        private void RunRetirementTick()
        {
            if (commanders == null) return;
            var prm = RetirementRules.RetireParams.Default;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.deathYear != 0) continue;
                if (c.serviceStatus != ServiceStatus.現役) continue;
                int age = LifecycleRules.Age(c, campaignYear);
                if (RetirementRules.ShouldRetireByAge(age, c.rankTier, prm))
                {
                    c.serviceStatus = ServiceStatus.退役;
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{c.faction} {c.name} 退役（停年・享年 {age}）");
                }
            }
        }

        // --- 陸軍大学校のエリート街道（#SCHOOL-AGE 配線） ---

        /// <summary>勢力の昇進ドクトリンを現在の政体形態から導く（民主＝実力主義／専制・君主・共産＝学閥主義＝政体が軍人事に効く）。</summary>
        private static PromotionDoctrine WarCollegeDoctrine(Faction f)
        {
            var camp = StrategySession.Campaign;
            FactionState s = camp != null ? CampaignRules.GetState(camp, f) : null;
            if (s != null) return GovernmentFormRules.PromotionDoctrineOf(s.governmentForm);
            return f == Faction.帝国 ? PromotionDoctrine.学閥主義 : PromotionDoctrine.実力主義; // フォールバック
        }

        /// <summary>
        /// 大学校入学→学校配属（艦隊配属不可）→卒業で大学校卒=参謀＝恩賜の軍刀組→昇進優遇 を年次で回す（#SCHOOL-AGE）。
        /// 数式・状態遷移は <see cref="WarCollegeCareerRules"/>（Core）へ委譲し、ここは起きた事象を通知へ流すだけ。
        /// </summary>
        private void RunWarCollegeCareerTick()
        {
            if (commanders == null) return;
            var events = new List<CareerEvent>();
            WarCollegeCareerRules.TickYear(commanders, campaignYear, WarCollegeDoctrine, events);
            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent e = events[i];
                string rank = RankSystem.ResolveRankNameOrDefault(null, e.rankTier);
                switch (e.kind)
                {
                    case CareerEventKind.入校:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{e.faction} {e.personName} 陸軍大学校へ入校（学校配属＝艦隊配属を離れる）");
                        break;
                    case CareerEventKind.卒業:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{e.faction} {e.personName} 陸軍大学校を卒業（参謀＝星）");
                        break;
                    case CareerEventKind.恩賜の軍刀:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{e.faction} {e.personName} 恩賜の軍刀組（大学校卒首席級）＝エリート街道へ");
                        break;
                    case CareerEventKind.昇進:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{e.faction} {e.personName} {rank}へ昇進");
                        break;
                }
            }
        }

        // --- 財政の年（#161-163 配線）：予算編成→執行→債務で1年を閉じる ---

        /// <summary>
        /// 年次の財政：①予算編成（歳入レート×支出性向を分野重みで配分）②形式財政（債務/利払い）③債務スパイラル通知。
        /// 現金の執行は日次 <see cref="CampaignRules.TickBudgetDay"/> が予算総額を国庫から引いて行う（予算が満ちて初めて執行が動く）。
        /// 数式は <see cref="BudgetRules"/>/<see cref="FiscalRules"/>/<see cref="CampaignRules"/> へ委譲。
        /// </summary>
        private void RunFiscalYearTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;

            // ① 予算編成（帝国＝軍拡で赤字気味／同盟＝均衡・内政厚め）。重みは 軍事/建艦/内政/社会保障/研究/外交。
            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null || s.budget == null) continue;
                float revenueRate = FiscalRules.TaxRevenue(CampaignRules.EconomyBase(s), s.taxRate);
                float propensity = s.faction == Faction.帝国 ? 1.1f : 1.0f;
                float[] weights = s.faction == Faction.帝国
                    ? new float[] { 3, 2, 1, 1, 1, 1 }
                    : new float[] { 1, 1, 2, 2, 1, 1 };
                BudgetRules.AllocateByWeights(s.budget, revenueRate * propensity, weights);
            }

            // ② 形式財政：赤字→国債→利払い→翌年（債務繰り越し）。
            CampaignRules.TickFiscalYear(camp, 1f);

            // ③ 帰結（出資度→実効・G3/G5）：社会保障→希望／財政健全度→希望／内政→安定度／債務スパイラル通知。
            var p = FiscalRules.FiscalParams.Default;
            var adminBonusByFaction = new System.Collections.Generic.Dictionary<Faction, float>();
            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null || s.budget == null || s.fiscal == null) continue;
                float economy = CampaignRules.EconomyBase(s);
                float revenueRate = FiscalRules.TaxRevenue(economy, s.taxRate);

                // 社会保障の希望加点（＋）と財政難の希望毀損（−）＝民心へ
                if (s.community != null)
                {
                    float welfareBonus = BudgetRules.WelfareHopeBonus(s.budget, revenueRate * 0.15f); // ±0.3
                    float health = economy > 0f ? FiscalRules.FiscalHealthFactor(s.fiscal, economy, p) : 1f;
                    float hopeDelta = welfareBonus * 0.1f - (1f - health) * 0.05f;
                    s.community.hope = Mathf.Clamp01(s.community.hope + hopeDelta);
                }

                // 内政の安定度加点（所有 Province へ後段で反映）
                adminBonusByFaction[s.faction] = BudgetRules.AdministrationStabilityBonus(s.budget, revenueRate * 0.2f); // ±10

                if (FiscalRules.IsDebtSpiral(s.fiscal, economy, p))
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告, $"{s.faction} 債務スパイラル（債務 {s.fiscal.debt:0}）");
            }

            // 内政予算の出資度を所有星系の Province 安定度へ年次反映（過剰で+・不足で−・0..100）。
            if (map != null)
                foreach (var sys in map.systems)
                {
                    if (sys == null || !provinces.TryGetValue(sys.id, out var prov) || prov == null) continue;
                    if (adminBonusByFaction.TryGetValue(sys.owner, out float ab))
                        prov.stability = Mathf.Clamp(prov.stability + ab, 0f, 100f);
                }
        }

        // --- 政体進化（#117 配線）：首長制→民主/独裁→下位形態 ---
        private bool regimeFormsSeeded;

        // --- キャンペーン勝敗（遊べる縦スライスの核） ---
        private bool campaignDecided;

        /// <summary>
        /// プレイヤー勢力の戦略的決着を年次で判定し、勝利/敗北したら時計を止めて終了画面を出す（一度きり）。
        /// 判定は <see cref="CampaignVictoryRules"/>（制覇=支配率/全制圧/滅亡）。終了画面は <see cref="CampaignEndOverlay"/>。
        /// </summary>
        private void RunCampaignVictoryCheck()
        {
            if (campaignDecided || map == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            CampaignOutcome outcome = CampaignVictoryRules.Evaluate(map, player);
            if (outcome == CampaignOutcome.継続) return;

            campaignDecided = true;
            if (StrategySession.Clock != null) StrategySession.Clock.Pause(); // 進行を止める
            int frac = Mathf.RoundToInt(CampaignVictoryRules.OwnedFraction(map, player) * 100f);
            bool win = outcome == CampaignOutcome.勝利;
            string msg = win
                ? $"【勝利】{player} が銀河を制覇（支配 {frac}%）"
                : $"【敗北】{player} は星系をすべて失った";
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.警告, msg);
            CampaignEndOverlay.Show(win, player, CampaignVictoryRules.OwnedFraction(map, player)); // 終了画面（遊べる縦スライスの締め）
        }

        /// <summary>
        /// 戦役を跨いで残る static 状態をリセットする（終了画面「タイトルへ戻る」/新規キャンペーン開始時）。
        /// 同一アプリ実行内で2周目を始めても目標提示が再び出るよう、オンボーディングのフラグを戻す。
        /// </summary>
        public static void ResetCampaignStatics()
        {
            objectiveAnnounced = false;
        }

        /// <summary>
        /// タイトルから新規キャンペーンを始める前処理（戦略の世界状態を破棄＝Strategy シーンで一から構築される）。
        /// `TitleManager` が呼んでから "Strategy" シーンへ遷移する。
        /// </summary>
        public static void BeginNewCampaign()
        {
            StrategySession.Clear();
            BattleHandoff.Clear();
            ResetCampaignStatics();
        }

        /// <summary>
        /// 政体進化を年次で回す（#117）：初期形態をシード（帝国=君主制/同盟=共和制/他=首長制）し、社会シグナル
        /// （正統性/腐敗/合意/希望/包摂）から `GovernmentFormRules.NextForm` で年1回1遷移を進めて通知する。数式は Core へ委譲。
        /// </summary>
        private void RunRegimeEvolutionTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;

            if (!regimeFormsSeeded)
            {
                for (int i = 0; i < camp.states.Count; i++)
                {
                    FactionState s = camp.states[i];
                    if (s == null || s.governmentForm != GovernmentForm.首長制) continue;
                    s.governmentForm = s.faction == Faction.帝国 ? GovernmentForm.君主制
                                     : s.faction == Faction.同盟 ? GovernmentForm.共和制
                                     : GovernmentForm.首長制; // 他勢力は首長制スタート
                }
                regimeFormsSeeded = true;
            }

            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null) continue;

                // (1) 政変（C1 Tier A）：統制が弱いとクーデター/革命が発火し、成功で政体が転換する。
                CoupContext ctx = PoliticalUpheavalRules.ContextOf(s);
                UpheavalResult up = PoliticalUpheavalRules.ResolveUpheaval(s.governmentForm, ctx, UnityEngine.Random.value);
                if (up.attempted)
                {
                    if (s.regime != null) s.regime.legitimacy = up.newLegitimacy; // 事後正統性（成功/粛清/内戦）
                    if (up.formChanged)
                    {
                        GovernmentForm from = s.governmentForm;
                        GovernmentFormRules.Apply(s, up.newForm);
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告, $"{s.faction} {up.type}クーデター成功＝政体が {from} → {up.newForm} へ");
                    }
                    else
                    {
                        string note = up.outcome == CoupOutcome.内戦 ? "内戦化" : "未遂（鎮圧）";
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意, $"{s.faction} {up.type}クーデター {note}");
                    }
                    continue; // 政変があった年は緩やかな進化はスキップ
                }

                // (2) 緩やかな進化：社会シグナルで合法な遷移を1段進める。
                RegimeSignals signals = GovernmentFormRules.SignalsOf(s);
                GovernmentForm next = GovernmentFormRules.NextForm(s.governmentForm, signals);
                if (next != s.governmentForm)
                {
                    GovernmentForm prev = s.governmentForm;
                    GovernmentFormRules.Apply(s, next);
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意, $"{s.faction} 政体が {prev} → {next} へ移行");
                }
            }
        }

        /// <summary>
        /// 政党政治の年次 Tick（#159 配線）：民主政治の勢力ごとに、成熟度に応じて政党制を二大政党へ収束させ、
        /// 衆参の選挙日程を回し、分断危機の立ち上がりを通知する。数値は <see cref="PoliticsTickRules"/>（→PartySystemRules/ElectionScheduleRules）へ委譲。
        /// </summary>
        private void RunPoliticsTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;

            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null) continue;
                if (!ElectoralSystemRules.IsElectoral(s.governmentForm)) continue; // 民主政治のみ（寡頭/君主/独裁は選挙なし）

                if (s.politics == null || s.politics.parties.Count == 0) SeedDemoParties(s);

                var r = PoliticsTickRules.TickYear(s, campaignYear);

                if (r.lowerHouseElection)
                {
                    Party ruling = PartyRules.RulingParty(s.politics.parties);
                    string rn = ruling != null ? ruling.partyName : "—";
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                        $"{s.faction} 下院総選挙（衆議院相当・任期4年）＝第一党 {rn}");
                }
                if (r.upperHouseElection)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                        $"{s.faction} 上院通常選挙（参議院相当・半数改選）");
                if (r.dividedCrisisOnset)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                        $"{s.faction} 二大政党化で社会の分断が深刻化（有効政党数 {r.effectiveParties:0.0}）");
            }
        }

        /// <summary>
        /// 世代交代の年次 Tick（#159 配線）：名簿（commanders）の成年が結婚し、夫婦が子をなして名簿に加わる。
        /// 数値は <see cref="GenerationTickRules"/>（→PersonMarriageRules/ChildbirthRules/HeredityRules）へ委譲。死（老衰）は下流の ProcessMortality が担う。
        /// </summary>
        private void RunGenerationTick()
        {
            if (commanders == null) return;
            var res = GenerationTickRules.TickYear(
                commanders, campaignYear,
                () => nextPersonId++,
                () => UnityEngine.Random.value,
                new GenerationTickRules.GenerationParams(0.4f, OfficerRosterCap),
                ChildbirthRules.FertilityParams.Default,
                HeredityRules.HeredityParams.Default);

            if (res.marriages > 0 || res.births > 0)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"世代交代：{res.marriages} 組が結婚・{res.births} 人誕生（名簿 {commanders.Count} 名）");
        }

        /// <summary>世代交代ループの種＝結婚適齢の若者（男女）を勢力ごとに数名置く。これが結婚→出産→加齢→死で世代が回る。</summary>
        private int SeedFoundingYouth(int startId, int year)
        {
            int id = startId;
            Faction[] facs = { Faction.帝国, Faction.同盟 };
            for (int fi = 0; fi < facs.Length; fi++)
            {
                Faction fac = facs[fi];
                for (int k = 0; k < 4; k++) // 男2・女2
                {
                    Sex sex = (k % 2 == 0) ? Sex.男性 : Sex.女性;
                    commanders.Add(new Person(id++, $"{fac}の士{k + 1}", fac, PersonRole.軍人)
                    {
                        sex = sex,
                        birthYear = year - (20 + k), // 成年（20〜23歳）
                        rankTier = 0,
                        leadership = 45, attack = 45, defense = 45, mobility = 45, operation = 45, intelligence = 45,
                    });
                }
            }
            return id;
        }

        /// <summary>デモ用の政党シード：多党乱立から出発させる（成熟が上がると二大政党へ収束する＝#159）。</summary>
        private void SeedDemoParties(FactionState s)
        {
            if (s == null) return;
            if (s.politics == null) s.politics = new PoliticsState();
            if (s.politics.parties.Count > 0) return;

            string[] names = s.faction == Faction.帝国
                ? new[] { "立憲党", "自由党", "国民党", "革新党" }
                : new[] { "民政党", "進歩党", "中道党", "急進党" };
            int baseId = (int)s.faction * 100;
            float share = 1f / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Party p = PartyOrganizationRules.Create(baseId + i + 1, names[i], s.faction, founderId: -1);
                p.support = share;
                s.politics.parties.Add(p);
            }
        }

        /// <summary>建艦の出資度（G3）＝建艦予算/必要額。歳入の2割を満額基準とする（不足で建艦が遅れる）。</summary>
        private float ShipbuildingFundingFactor(Faction f)
        {
            var camp = StrategySession.Campaign;
            if (camp == null) return 1f;
            FactionState s = CampaignRules.GetState(camp, f);
            if (s == null || s.budget == null) return 1f;
            float need = FiscalRules.TaxRevenue(CampaignRules.EconomyBase(s), s.taxRate) * 0.2f;
            if (need <= 0f) return 1f;
            return BudgetRules.ShipbuildingFactor(s.budget, need);
        }

        // --- 人事の空席補充（#152）と捕虜の処遇（#154）の配線 ---
        private Office[] commandOffices; // 勢力ごとの要職（DemoFactions と並行・null=未設定）
        private Office[] civilOffices;   // 勢力ごとの文官要職＝宰相（銓衡で配属・DemoFactions と並行）
        private Office[] governorOffices; // 勢力ごとの総督職（OfficeScope.星系・scopeKey=星系id で星系別に配属）
        private const CourtRank PremierRequiredRank = CourtRank.従五位下; // 宰相の官位相当＝五位以上（貴族）
        private const CourtRank GovernorRequiredRank = CourtRank.正六位上; // 総督（受領/国司）の官位相当＝六位以上
        private const int MaxGovernedSystems = 16;   // 総督を置く星系の上限（PERF＝無制限配属を防ぐ）
        private const float CentralOversightShare = 0.3f; // 中央（宰相）が地方へ及ぼす監督の効き（薄く全土へ）

        /// <summary>文官要職（観測用・人物名鑑が在任を表示）。</summary>
        public IReadOnlyList<Office> CivilOffices => civilOffices;

        /// <summary>DemoFactions 内の番号（非デモ勢力は −1）。</summary>
        private int FactionIndex(Faction f)
        {
            for (int i = 0; i < DemoFactions.Length; i++) if (DemoFactions[i] == f) return i;
            return -1;
        }

        /// <summary>その文官が就いている文官官職名（宰相＝中央 or ◯◯総督＝地方）。無ければ空（観測用・人物名鑑が読む）。</summary>
        public string CivilPostOf(Person p)
        {
            if (p == null) return "";
            if (civilOffices != null)
                for (int f = 0; f < civilOffices.Length; f++)
                    if (civilOffices[f] != null && GovernmentRegistry.GetHolder(civilOffices[f]) is Person h && h.id == p.id)
                        return civilOffices[f].officeName;
            if (governorOffices != null && map != null)
                for (int i = 0; i < map.systems.Count; i++)
                {
                    StarSystem s = map.systems[i];
                    if (s == null) continue;
                    int fIdx = FactionIndex(s.owner);
                    if (fIdx < 0 || governorOffices[fIdx] == null) continue;
                    if (GovernmentRegistry.GetHolder(governorOffices[fIdx], s.id) is Person g && g.id == p.id)
                        return $"{s.systemName}総督";
                }
            return "";
        }

        /// <summary>勢力の現役（生存・自由・現役）司令を後任候補として集める。</summary>
        private System.Collections.Generic.List<ICharacter> ActiveCommanders(Faction f)
        {
            var list = new System.Collections.Generic.List<ICharacter>();
            if (commanders == null) return list;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c != null && c.faction == f && c.IsAvailable && c.serviceStatus == ServiceStatus.現役)
                    list.Add(c);
            }
            return list;
        }

        /// <summary>勢力の軍政型を現在の政体形態から導く（捕虜処遇 DefaultDisposition 等が政体に追従＝共産化で処断的に等）。</summary>
        private static CivilianControlType FactionControl(Faction f)
        {
            var camp = StrategySession.Campaign;
            FactionState s = camp != null ? CampaignRules.GetState(camp, f) : null;
            if (s != null) return GovernmentFormRules.ControlTypeOf(s.governmentForm);
            return f == Faction.帝国 ? CivilianControlType.君主統帥 : CivilianControlType.文民統制; // フォールバック
        }

        private static Faction EnemyOf(Faction f) => f == Faction.帝国 ? Faction.同盟 : Faction.帝国;

        /// <summary>要職をシード（冪等）：勢力ごとに「宇宙艦隊司令長官」を1つ作り、最先任の現役へ任命。</summary>
        private void SeedCommandOffices()
        {
            if (commandOffices != null) return;
            GovernmentRegistry.Clear();
            commandOffices = new Office[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                var office = new Office(900 + f, $"{fac}宇宙艦隊司令長官", OfficeScope.国家, OfficeDomain.軍事)
                { militaryOnly = true, requiredTier = 8 };
                commandOffices[f] = office;
                VacancyRules.FillVacancy(fac, office, ActiveCommanders(fac)); // 初任命
            }
            // 文官要職＝宰相（内政・文民専用）。位階の要求は官位相当（PremierRequiredRank）で別途効かせる＝requiredTier=0。
            // 初任は空席のまま（文民は年を追って卒業・叙位される）。年次の RunCivilAppointmentTick が銓衡で埋める。
            civilOffices = new Office[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                civilOffices[f] = new Office(910 + f, $"{fac}宰相", OfficeScope.国家, OfficeDomain.内政)
                { civilianOnly = true, requiredTier = 0 };
            }
            // 文官の地方官＝総督（受領/国司・OfficeScope.星系）。同一 Office を scopeKey=星系id で星系別に使う。
            governorOffices = new Office[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                governorOffices[f] = new Office(920 + f, $"{fac}総督", OfficeScope.星系, OfficeDomain.内政)
                { civilianOnly = true, requiredTier = 0 };
            }
        }

        /// <summary>勢力の文民ネームドを集める（銓衡候補）。</summary>
        private List<Person> CiviliansOf(Faction f)
        {
            var list = new List<Person>();
            if (civilians == null) return list;
            for (int i = 0; i < civilians.Count; i++)
                if (civilians[i] != null && civilians[i].faction == f) list.Add(civilians[i]);
            return list;
        }

        /// <summary>
        /// 文官の銓衡配属（官僚制基盤＝<see cref="CivilAppointmentRules"/> へ委譲）。死亡/捕虜・官位相当を割った在任者を解任し、
        /// 叙位された文官から考課＋位階で最適者を宰相へ任命する（式部省の選叙）。就任は人事通知へ。
        /// </summary>
        private void RunCivilAppointmentTick()
        {
            SeedCommandOffices(); // 冪等＝文官要職もここで用意される
            if (civilOffices == null || civilians == null) return;
            // 名実の乖離を選抜にも効かせる＝権威が低いほど門閥人事（位階＝家柄）が実績を上書きする。
            var prm = CivilServiceRules.ParamsForAuthority(
                courtAuthority != null ? courtAuthority.authority : 0f, CivilServiceRules.AppointmentParams.Default);
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Office office = civilOffices[f];
                if (office == null) continue;
                Faction fac = DemoFactions[f];
                var holder = GovernmentRegistry.GetHolder(office) as Person;
                if (holder != null && (!holder.IsAvailable
                    || JapaneseCourtRankRules.Compare(holder.courtRank, PremierRequiredRank) < 0))
                    GovernmentRegistry.Dismiss(office, holder); // 官位相当を割った（位階喪失）／死亡・捕虜
                ICharacter before = GovernmentRegistry.GetHolder(office);
                Person appointed = CivilAppointmentRules.FillVacancy(
                    fac, office, PremierRequiredRank, CiviliansOf(fac), prm);
                if (appointed != null && appointed != before)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{fac} {office.officeName} に {appointed.name}（{JapaneseCourtRankRules.Name(appointed.courtRank)}）が就任");
            }
        }

        /// <summary>勢力の文民から、既に他の官職に就いている者（<paramref name="assigned"/>）を除いた銓衡候補。一人一職を保つ。</summary>
        private List<Person> CiviliansOfExcluding(Faction f, HashSet<int> assigned)
        {
            var list = new List<Person>();
            if (civilians == null) return list;
            for (int i = 0; i < civilians.Count; i++)
            {
                Person c = civilians[i];
                if (c != null && c.faction == f && (assigned == null || !assigned.Contains(c.id))) list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 総督（地方官）の銓衡配属（官僚制基盤）。所有星系ごとに、官位相当（六位以上）の文官を考課＋位階で配属する
        /// ＝受領/国司。中央の宰相とは別人（一人一職）。PERF＝<see cref="MaxGovernedSystems"/> 件で打ち止め。
        /// </summary>
        private void RunGovernorAppointmentTick()
        {
            SeedCommandOffices();
            if (governorOffices == null || civilians == null || map == null) return;

            // 名実の乖離を選抜にも効かせる＝権威が低いほど門閥人事（位階＝家柄）が実績を上書きする。
            var prm = CivilServiceRules.ParamsForAuthority(
                courtAuthority != null ? courtAuthority.authority : 0f, CivilServiceRules.AppointmentParams.Default);
            var assigned = new HashSet<int>();
            if (civilOffices != null) // 宰相（中央）は総督に重ねない
                for (int f = 0; f < civilOffices.Length; f++)
                    if (civilOffices[f] != null && GovernmentRegistry.GetHolder(civilOffices[f]) is Person pm) assigned.Add(pm.id);

            int governed = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                if (governed >= MaxGovernedSystems) break;
                StarSystem s = map.systems[i];
                if (s == null) continue;
                int fIdx = FactionIndex(s.owner);
                if (fIdx < 0) continue; // デモ勢力の領のみ
                Office office = governorOffices[fIdx];
                if (office == null) continue;

                var holder = GovernmentRegistry.GetHolder(office, s.id) as Person;
                if (holder != null && (!holder.IsAvailable
                    || JapaneseCourtRankRules.Compare(holder.courtRank, GovernorRequiredRank) < 0))
                {
                    GovernmentRegistry.Dismiss(office, holder, s.id);
                    holder = null;
                }
                ICharacter before = holder;
                Person gov = CivilAppointmentRules.FillVacancy(
                    s.owner, office, GovernorRequiredRank, CiviliansOfExcluding(s.owner, assigned),
                    prm, scopeKey: s.id);
                if (gov == null) continue;

                assigned.Add(gov.id);
                governed++;
                if (gov != before)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{s.owner} {s.systemName}総督 に {gov.name}（{JapaneseCourtRankRules.Name(gov.courtRank)}）が就任");
            }
        }

        /// <summary>
        /// 星系の内政に効く文官行政寄与＝<b>総督（地方・その星系）＋宰相（中央・薄く監督）</b>。いずれも名実の乖離で
        /// 朝廷の権威ぶん減衰（<see cref="AdministrationRules"/>）。総督が空席なら中央の監督のみが薄く届く。
        /// </summary>
        private float SystemAdminBonus(StarSystem s)
        {
            if (s == null) return 0f;
            float authority = courtAuthority != null ? courtAuthority.authority : 0f;
            float gov = 0f;
            int fIdx = FactionIndex(s.owner);
            if (fIdx >= 0 && governorOffices != null && governorOffices[fIdx] != null)
            {
                var governor = GovernmentRegistry.GetHolder(governorOffices[fIdx], s.id) as Person;
                gov = AdministrationRules.StabilityContribution(governor, authority, AdministrationRules.AdminParams.Default);
            }
            return gov + PremierAdminBonus(s.owner) * CentralOversightShare;
        }

        /// <summary>
        /// 後任補充（VacancyRules・#152）＋捕虜の処遇（CaptivityRules・#154）を年次で回す。数式/状態遷移は Core 窓口へ委譲。
        /// </summary>
        private void RunPersonnelTurnoverTick()
        {
            if (commanders == null) return;
            ResolveCaptives();   // 既存捕虜を処遇（解放/登用/処断）
            MaybeCapture();      // 敵対勢力により低確率で捕虜化
            FillCommandVacancies(); // 要職の空席を後任補充
        }

        /// <summary>捕虜を捕獲側の政体に従って処遇：登用（寝返り・稀）→さもなくば解放/処断。</summary>
        private void ResolveCaptives()
        {
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.captiveStatus != CaptiveStatus.捕虜) continue;
                Faction captor = c.heldBy;

                // まず登用（寝返り＝調略）を試みる（思想差・処遇で決まる稀な成立）。
                float recruitChance = CaptivityRules.RecruitChance(0.5f, 0.5f);
                if (UnityEngine.Random.value < recruitChance && CaptivityRules.Recruit(c, captor))
                {
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{c.name} {captor} へ登用（寝返り）");
                    continue;
                }

                // さもなくば捕獲側の政体の既定処遇（処断 or 解放）。
                CaptiveDisposition dispo = CaptivityRules.DefaultDisposition(FactionControl(captor));
                if (dispo == CaptiveDisposition.処断 && CaptivityRules.Execute(c, campaignYear))
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.警告, $"{c.name} 処断（捕虜）");
                else if (CaptivityRules.Release(c))
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{c.name} 解放され帰還");
            }
        }

        /// <summary>敵対勢力により低確率で中堅以下の現役将校を捕虜化（前線での捕獲のデモ）。</summary>
        private void MaybeCapture()
        {
            if (UnityEngine.Random.value > 0.15f) return; // 年あたりの捕獲生起（控えめ）
            var pool = new System.Collections.Generic.List<Person>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c != null && c.IsAvailable && c.serviceStatus == ServiceStatus.現役 && c.rankTier < 8)
                    pool.Add(c); // 最高位は捕らえにくい＝中堅以下
            }
            if (pool.Count == 0) return;
            Person target = pool[UnityEngine.Random.Range(0, pool.Count)];
            Faction captor = EnemyOf(target.faction);
            if (CaptivityRules.Capture(target, captor, campaignYear))
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{target.faction} {target.name} {captor} の捕虜に");
        }

        /// <summary>要職の保持者が死亡/捕虜/退役なら解任し、現役の有資格者で後任補充（VacancyRules・#152）。</summary>
        private void FillCommandVacancies()
        {
            SeedCommandOffices();
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Office office = commandOffices[f];
                if (office == null) continue;
                Faction fac = DemoFactions[f];
                var holder = GovernmentRegistry.GetHolder(office) as Person;
                if (holder != null && (!holder.IsAvailable || holder.serviceStatus == ServiceStatus.退役))
                    GovernmentRegistry.Dismiss(office, holder);
                ICharacter before = GovernmentRegistry.GetHolder(office);
                VacancyRules.FillVacancy(fac, office, ActiveCommanders(fac));
                ICharacter after = GovernmentRegistry.GetHolder(office);
                if (after != null && after != before)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{fac} {office.officeName} に {after.CharacterName} が就任");
            }
        }

        // --- ネームド資産（NASSET・#2063 デモ配線） ---
        private static readonly Faction[] DemoFactions = { Faction.帝国, Faction.同盟 };
        private bool namedAssetsSeeded;

        /// <summary>id から司令を解決（資産収益を所有者 wealth へ流す・<see cref="NamedAssetTickRules"/> 用）。</summary>
        private Person ResolveCommander(int id)
        {
            if (commanders == null) return null;
            for (int i = 0; i < commanders.Count; i++)
                if (commanders[i] != null && commanders[i].id == id) return commanders[i];
            return null;
        }

        /// <summary>相続人＝故人と同勢力の最高位の存命司令（本人除く・同位は先頭）。不在なら null（=没収）。</summary>
        private Person FindHeir(Person d)
        {
            if (commanders == null || d == null) return null;
            Person best = null;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.id == d.id || c.deathYear != 0) continue;
                if (c.faction != d.faction) continue;
                if (best == null || c.rankTier > best.rankTier) best = c;
            }
            return best;
        }

        /// <summary>デモ資産シード（冪等）：各司令に固有名の旗艦、各勢力に宮殿を1つ持たせる（NASSET-6）。</summary>
        private void SeedNamedAssets()
        {
            if (namedAssetsSeeded || NamedAssetRegistry.All.Count > 0) { namedAssetsSeeded = true; return; }
            // 各勢力の宮殿（国家所有＝維持費は重いが威信・正統性を生む）。
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                var palace = new NamedAsset(NamedAssetRegistry.NextId(), $"{DemoFactions[f]}宮殿", NamedAssetCategory.宮殿)
                {
                    ownerKind = AssetOwnerKind.国家, ownerFaction = DemoFactions[f],
                    value = 5000f, yieldRate = 0.02f, upkeepRate = 0.05f, prestige = 30f
                };
                NamedAssetRegistry.Register(palace);
            }
            // 各司令の旗艦（人物所有＝維持費はかかるが威信。固有名は提督名から）。
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                {
                    Person c = commanders[i];
                    if (c == null) continue;
                    var flagship = new NamedAsset(NamedAssetRegistry.NextId(), $"{c.name}旗艦", NamedAssetCategory.旗艦)
                    {
                        ownerKind = AssetOwnerKind.人物, ownerPersonId = c.id,
                        value = 800f, upkeepRate = 0.03f, prestige = 8f
                    };
                    NamedAssetRegistry.Register(flagship);
                }
            namedAssetsSeeded = true;
        }

        // --- ネームド金融資産・不動産（NFIN・#2070 デモ配線） ---
        private bool financialAssetsSeeded;

        /// <summary>相続人を最大 max 名（同勢力の存命司令を階級降順・本人除く）。細分化相続の分割先。</summary>
        private System.Collections.Generic.List<int> FindHeirs(Person d, int max)
        {
            var result = new System.Collections.Generic.List<int>();
            if (commanders == null || d == null) return result;
            var pool = new System.Collections.Generic.List<Person>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.id == d.id || c.deathYear != 0 || c.faction != d.faction) continue;
                pool.Add(c);
            }
            pool.Sort((a, b) => b.rankTier.CompareTo(a.rankTier)); // 階級降順
            for (int i = 0; i < pool.Count && i < max; i++) result.Add(pool[i].id);
            return result;
        }

        /// <summary>デモ金融/不動産シード（冪等）：各勢力に国有株式・首都惑星の deed、各司令に少数の株式・地所（NFIN-6）。</summary>
        private void SeedFinancialAssets()
        {
            if (financialAssetsSeeded || FinancialHoldingRegistry.All.Count > 0 || PropertyDeedRegistry.All.Count > 0)
            { financialAssetsSeeded = true; return; }

            int underlying = 1;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                // 国有の株式（配当）と債券（クーポン）。
                FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, $"{DemoFactions[f]}重工")
                { ownerKind = AssetOwnerKind.国家, ownerFaction = DemoFactions[f], underlyingId = underlying++, units = 1000f, unitPrice = 10f, incomePerUnit = 0.5f, bookCost = 10000f });
                FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.債券, $"{DemoFactions[f]}国債")
                { ownerKind = AssetOwnerKind.国家, ownerFaction = DemoFactions[f], underlyingId = underlying++, units = 500f, unitPrice = 100f, incomePerUnit = 3f, bookCost = 50000f });
            }
            // 各司令に少数の株式（配当）と、首都星系（id=0 を仮の本拠）に地所（地代）。
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                {
                    Person c = commanders[i];
                    if (c == null) continue;
                    FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.投資信託, "銀河ファンド")
                    { ownerKind = AssetOwnerKind.人物, ownerPersonId = c.id, underlyingId = underlying, units = 50f, unitPrice = 12f, incomePerUnit = 0.4f, bookCost = 600f });
                    var deed = new PropertyDeed(0, c.faction == Faction.同盟 ? 1 : 0, 0.2f, 3000f)
                    { ownerKind = AssetOwnerKind.人物, ownerPersonId = c.id, rentRate = 0.04f };
                    PropertyDeedRegistry.Register(deed);
                }
            financialAssetsSeeded = true;
        }

        /// <summary>紙くず化デモ（NFIN-6・暴落#185）：低確率で1銘柄を時価0へ（同銘柄の全保有が紙くずに）。</summary>
        private void MaybeCrashAStock()
        {
            var stocks = FinancialHoldingRegistry.HoldingsOfInstrument(FinancialInstrument.株式);
            if (stocks.Count == 0 || UnityEngine.Random.value > 0.05f) return;
            int victim = stocks[UnityEngine.Random.Range(0, stocks.Count)].underlyingId;
            var affected = FinancialHoldingRegistry.HoldingsOfUnderlying(victim);
            string banner = null;
            for (int i = 0; i < affected.Count; i++)
            {
                FinancialAssetRules.MarkToMarket(affected[i], 0f, 0f); // 紙くず化
                banner = affected[i].underlyingName;
            }
            if (banner != null)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告, $"{banner} 株が暴落＝紙くずに（保有 {affected.Count} 件が無価値化）");
        }

        // --- 国家・惑星の行政物資消費（STATEDEM・#2077 デモ配線） ---
        private readonly System.Collections.Generic.Dictionary<Faction, ResourceStockpile> stateStockpiles
            = new System.Collections.Generic.Dictionary<Faction, ResourceStockpile>();

        /// <summary>国家ごとに所有惑星から産出→行政・インフラが消費→不足で統治逼迫＝安定度低下（STATEDEM-6）。</summary>
        private void RunStateConsumptionTick()
        {
            if (map == null || provinces == null) return;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                var owned = new System.Collections.Generic.List<Province>();
                int systemCount = 0;
                foreach (var s in map.systems)
                {
                    if (s == null || s.owner != fac) continue;
                    systemCount++;
                    if (provinces.TryGetValue(s.id, out var prov) && prov != null) owned.Add(prov);
                }
                if (systemCount == 0) continue;

                // 国庫（資源備蓄）を冪等生成。
                if (!stateStockpiles.TryGetValue(fac, out var stock) || stock == null)
                {
                    stock = new ResourceStockpile(200f, 0f, 100f);
                    stateStockpiles[fac] = stock;
                }
                // 年次産出（所有惑星の類型×統治で物資/燃料を産む）。
                for (int i = 0; i < owned.Count; i++)
                    ResourceProductionRules.ProduceFromProvince(stock, owned[i], 1f);

                // 行政・インフラ・公共サービスの物資消費＝総需要を国庫から引く。
                var result = StateConsumptionTickRules.TickState(owned, systemCount, stock);
                if (result.overall < 0.999f)
                {
                    // 行政物資不足＝統治が回らず安定度低下（緩やかに削る＝GovernanceRules 収束と競合させない）。
                    float penalty = StateConsumptionEffectRules.StabilityPenalty(result.overall) * 0.1f;
                    for (int i = 0; i < owned.Count; i++)
                        owned[i].stability = UnityEngine.Mathf.Max(0f, owned[i].stability - penalty);
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告,
                        $"{fac} 行政物資が不足（充足 {(int)(result.overall * 100)}%）＝統治逼迫で安定度低下");
                }

                // 企業の投入制約つき生産（FIRMPROD-6・#2084）：工員#110 から計画産出を見積り、国庫を投入に実産出を解く。
                // 原材料（物資）/エネルギー（燃料）が足りないと工場が遊休＝減産。実産出ぶんの投入を消費する。
                float industryWorkers = 0f;
                for (int i = 0; i < owned.Count; i++) industryWorkers += OccupationRules.Workers(owned[i], Occupation.工員);
                if (industryWorkers > 0f)
                {
                    float planned = industryWorkers; // 計画産出 proxy（労働×生産性=1）
                    var pr = EnterpriseProductionTickRules.Produce(planned, stock.Get(ResourceType.物資), stock.Get(ResourceType.燃料), float.MaxValue);
                    EnterpriseProductionTickRules.Consume(stock, pr.realizedOutput);
                    if (pr.inputConstrained && pr.utilization < 0.999f)
                        NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意,
                            $"{fac} 工業が{pr.binding}不足で減産（稼働 {(int)(pr.utilization * 100)}%）");
                }
            }
        }

        // --- 代表生産チェーン（森林→木材→建材→住宅・VCHAIN・#2091 デモ配線） ---
        private readonly System.Collections.Generic.Dictionary<int, ChainStock> chainStocks
            = new System.Collections.Generic.Dictionary<int, ChainStock>();

        /// <summary>類型ごとの森林初期量（居住/農業は森が多く、工業/鉱業は少ない）。</summary>
        private static float SeedForest(SystemType t)
        {
            switch (t)
            {
                case SystemType.農業: return 1000f;
                case SystemType.居住: return 800f;
                case SystemType.鉱業: return 200f;
                default: return 300f; // 工業
            }
        }

        /// <summary>惑星ごとに森林→木材→建材→住宅 を年次で流し、住宅充足で生活水準を補正（VCHAIN-6）。</summary>
        private void RunSupplyChainTick()
        {
            if (provinces == null) return;
            var p = SupplyChainParams.Default;
            int shortageCount = 0, depletionCount = 0;
            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                if (!chainStocks.TryGetValue(kv.Key, out var cs) || cs == null)
                {
                    // 初期住宅は需要の8割（最初から住んでいる）。
                    cs = new ChainStock(SeedForest(prov.systemType), 0f, 0f, prov.population * p.perCapitaHousing * 0.8f);
                    chainStocks[kv.Key] = cs;
                }
                var r = SupplyChainTickRules.TickYear(cs, prov.population, p);
                // 住宅充足で生活水準#181 を補正（不足は頭打ち＝#2042 がその年に設定した値へ乗算）。
                prov.livingStandard *= HousingDemandRules.LivingStandardFactor(r.occupancy, 0.7f);
                if (r.occupancy < 0.8f) shortageCount++;
                if (r.overharvest) depletionCount++;
            }
            if (shortageCount > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意, $"住宅不足の星系 {shortageCount}（木材・建材の供給不足）");
            if (depletionCount > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意, $"森林の過伐採 {depletionCount} 星系（再生が追いつかない）");
        }

        // --- 汎用BOM消費財（食品/衣類・BOM・#2098 デモ配線） ---
        private readonly System.Collections.Generic.Dictionary<int, CommodityStock> bomStocks
            = new System.Collections.Generic.Dictionary<int, CommodityStock>();
        private bool bomSeeded;
        private int grainId, fiberId, clothId, foodId, clothingId;
        private Recipe foodRecipe, clothRecipe, clothingRecipe;

        /// <summary>品目カタログとレシピを冪等 seed（食品←穀物、布←繊維、衣類←布）。</summary>
        private void EnsureBomContent()
        {
            if (bomSeeded) return;
            grainId = CommodityCatalog.Register("穀物", CommodityCategory.原材料).id;
            fiberId = CommodityCatalog.Register("繊維", CommodityCategory.原材料).id;
            clothId = CommodityCatalog.Register("布", CommodityCategory.中間財).id;
            foodId = CommodityCatalog.Register("食品", CommodityCategory.消費財).id;
            clothingId = CommodityCatalog.Register("衣類", CommodityCategory.消費財).id;
            foodRecipe = RecipeBook.Register(new Recipe(foodId).AddInput(grainId, 1f));        // 食品←穀物×1
            clothRecipe = RecipeBook.Register(new Recipe(clothId).AddInput(fiberId, 2f));       // 布←繊維×2
            clothingRecipe = RecipeBook.Register(new Recipe(clothingId).AddInput(clothId, 2f)); // 衣類←布×2
            bomSeeded = true;
        }

        /// <summary>惑星ごとに原材料を供給→食品/衣類をレシピ生産→消費財需要を消費し、不足で生活水準を補正（BOM-6）。</summary>
        private void RunBomConsumerTick()
        {
            if (provinces == null) return;
            EnsureBomContent();
            // Phase 1: 原材料供給（人口×安定度比例＝荒れた惑星は産まない）。
            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                if (!bomStocks.TryGetValue(kv.Key, out var cs) || cs == null) { cs = new CommodityStock(); bomStocks[kv.Key] = cs; }
                float outFactor = GovernanceRules.OutputFactor(prov);
                cs.Add(grainId, prov.population * 1.5f * outFactor);
                cs.Add(fiberId, prov.population * 0.6f * outFactor);
            }
            // Phase 2: 域内物流（DIST-6・#2112）＝余剰の穀物を不足惑星へ回廊で配送（通商破壊で分断）。生産の前に回す。
            RunRegionalDistributionTick();
            // Phase 3: レシピ生産＋消費財需要の充足。
            int foodShort = 0, clothingShort = 0;
            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                if (!bomStocks.TryGetValue(kv.Key, out var cs) || cs == null) continue;
                float pop = prov.population;
                // レシピ生産（上流→下流）：食品←穀物、布←繊維、衣類←布。
                BomTickRules.Produce(cs, foodRecipe, pop * 1.0f);
                BomTickRules.Produce(cs, clothRecipe, pop * 0.4f);
                BomTickRules.Produce(cs, clothingRecipe, pop * 0.2f);
                // 消費財需要の充足（食品は全員・衣類は控えめ）。
                float foodDemand = ConsumerDemandRules.Demand(pop, 1.0f);
                float clothingDemand = ConsumerDemandRules.Demand(pop, 0.2f);
                float foodFulfill = ConsumerDemandRules.Fulfillment(cs.Get(foodId), foodDemand);
                float clothingFulfill = ConsumerDemandRules.Fulfillment(cs.Get(clothingId), clothingDemand);
                ConsumerDemandRules.Consume(cs, foodId, foodDemand);
                ConsumerDemandRules.Consume(cs, clothingId, clothingDemand);
                float consumerFactor = ConsumerDemandRules.LivingStandardFactor(UnityEngine.Mathf.Min(foodFulfill, clothingFulfill), 0.6f);
                prov.livingStandard *= consumerFactor;
                if (foodFulfill < 0.8f) foodShort++;
                if (clothingFulfill < 0.8f) clothingShort++;
            }
            if (foodShort > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告, $"食料不足の星系 {foodShort}（穀物・食品の供給不足）");
            if (clothingShort > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"衣類不足の星系 {clothingShort}（繊維・布の供給不足）");
        }

        // --- SCM計画（MRP所要量展開・SCM・#2105 read-only 配線） ---
        /// <summary>勢力ごとに消費財需要をMRP展開し、原材料供給見込みと突き合わせて逼迫品目を通知（状態は変えない）。</summary>
        private void RunScmPlanTick()
        {
            if (map == null || provinces == null) return;
            EnsureBomContent();
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                float totalPop = 0f, grainSupply = 0f, fiberSupply = 0f;
                foreach (var s in map.systems)
                {
                    if (s == null || s.owner != fac) continue;
                    if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;
                    float pop = prov.population;
                    float outFactor = GovernanceRules.OutputFactor(prov);
                    totalPop += pop;
                    grainSupply += pop * 1.5f * outFactor; // RunBomConsumerTick と同じ供給見込み
                    fiberSupply += pop * 0.6f * outFactor;
                }
                if (totalPop <= 0f) continue;

                var demands = new System.Collections.Generic.Dictionary<int, float>
                {
                    { foodId, totalPop * 1.0f },     // 食品＝全員
                    { clothingId, totalPop * 0.2f }, // 衣類＝控えめ
                };
                var onHand = new CommodityStock();
                onHand.Add(grainId, grainSupply);
                onHand.Add(fiberId, fiberSupply);

                var plan = ScmTickRules.Plan(demands, onHand);
                if (plan.serviceLevel < 0.7f && plan.criticalCommodity >= 0)
                {
                    var crit = CommodityCatalog.Get(plan.criticalCommodity);
                    string name = crit != null ? crit.name : "原材料";
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意,
                        $"{fac} SCM計画：{name}が逼迫（消費財の充足見込み {(int)(plan.serviceLevel * 100)}%）");
                }
            }
        }

        // --- 勢力内供給配分（域内物流・DIST・#2112 配線） ---
        private const float DistributionLoss = 0.05f; // 回廊輸送ロス

        /// <summary>勢力ごとに連結領域内で穀物を再配分＝余剰の穀倉惑星が不足惑星を養う（通商破壊で分断・封鎖惑星は孤立）。</summary>
        private void RunRegionalDistributionTick()
        {
            if (map == null || provinces == null) return;
            // 通商破壊#95：敵艦が在席する星系は中継不能＝領域を分断する。
            var blocked = new System.Collections.Generic.HashSet<int>();
            foreach (var s in map.systems)
                if (s != null && HasHostileFleetAt(s)) blocked.Add(s.id);

            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                var components = RegionReachabilityRules.Components(map, fac, blocked);
                for (int ci = 0; ci < components.Count; ci++)
                {
                    var ids = new System.Collections.Generic.List<int>();
                    foreach (var id in components[ci])
                        if (provinces.TryGetValue(id, out var pv) && pv != null && bomStocks.TryGetValue(id, out var st) && st != null)
                            ids.Add(id);
                    if (ids.Count < 2) continue; // 2惑星以上ないと配分の意味がない

                    var stocks = new CommodityStock[ids.Count];
                    var grainDemand = new float[ids.Count];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        stocks[i] = bomStocks[ids[i]];
                        grainDemand[i] = provinces[ids[i]].population * 1.0f; // 食品の素＝穀物の地元需要
                    }
                    RegionalDistributionTickRules.Distribute(stocks, grainId, grainDemand, float.MaxValue, DistributionLoss);
                }
            }
        }

        // --- 外交（DIPLO・#2119 配線） ---
        /// <summary>勢力ペアの外交を年次で回す＝関係ドリフト→AIが宣戦/講和/同盟を決定し通知。</summary>
        private void RunDiplomacyTick()
        {
            if (map == null) return;
            // セッション初期化＋FactionRelations.ActiveDiplomacy 配線（冪等）。
            var names = new System.Collections.Generic.List<string>();
            for (int f = 0; f < DemoFactions.Length; f++) names.Add(DemoFactions[f].ToString());
            var state = DiplomacySession.Ensure(names);

            var dp = DiplomacyRules.DiplomacyParams.Default;
            var ai = DiplomacyAiRules.DiploAiParams.Default;
            var wp = WarGoalRules.WarGoalParams.Default;
            // プレイヤー勢力の外交はプレイヤーが操作する（AIに乗っ取らせない・#2119 操作化）。
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            for (int i = 0; i < DemoFactions.Length; i++)
                for (int j = i + 1; j < DemoFactions.Length; j++)
                {
                    Faction fa = DemoFactions[i], fb = DemoFactions[j];
                    if (fa == player || fb == player) continue; // プレイヤー絡みのペアはAI判断しない
                    string a = fa.ToString(), b = fb.ToString();
                    // 国力＝所有惑星の人口合計、思想親和＝デモは異勢力で険悪、国境接触ありとみなす。
                    float strA = FactionPopulation(fa), strB = FactionPopulation(fb);
                    var factors = new DiplomacyRules.OpinionFactors(-0.5f, 0.2f, true, 0f, false);
                    var ev = DiplomacyTickRules.TickPair(state, a, b, factors, strA, strB, campaignYear, dp, ai, wp);
                    switch (ev)
                    {
                        case DiplomacyEvent.宣戦布告:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.警告, $"{a} が {b} に宣戦布告");
                            break;
                        case DiplomacyEvent.講和:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} と {b} が講和");
                            break;
                        case DiplomacyEvent.同盟締結:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} と {b} が同盟締結");
                            break;
                    }
                }

            // 失効した条約を整理（status系は平時へ）。
            TreatyManagementRules.ExpireDue(state, campaignYear);
        }

        /// <summary>
        /// プレイヤー勢力の外交コマンドを発令（UI/キーから呼ぶ・#2119 操作化の入口）。
        /// 検証/適用は <see cref="DiplomacyCommandRules"/> へ委譲。成功で外交カテゴリへ通知し true。
        /// </summary>
        public bool IssuePlayerDiplomacy(Faction target, DiplomaticAction action)
        {
            var state = DiplomacySession.State;
            if (state == null) return false;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            if (target == player) return false;
            string a = player.ToString(), b = target.ToString();
            bool ok = DiplomacyCommandRules.Issue(state, a, b, action, DiplomacyRules.DiplomacyParams.Default);
            if (ok)
                NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} → {b}：{action} を発令");
            return ok;
        }

        /// <summary>勢力の国力 proxy＝所有星系の人口合計。</summary>
        private float FactionPopulation(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float pop = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                    pop += prov.population;
            return pop;
        }

        // --- 法の支配と法と秩序（LAW・#2126 配線） ---
        /// <summary>勢力の法の支配（デモ法体系）＋惑星の治安（犯罪→秩序）を年次で解き、安定へ反映・抑圧を通知。</summary>
        private void RunLawTick()
        {
            if (map == null || provinces == null) return;
            var cp = CrimeRules.CrimeParams.Default;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                // デモ法体系：同盟＝法の支配（権力も法に従う）／帝国＝法治どまり（権力制約が低い）。
                LegalSystem legal = fac == Faction.同盟
                    ? new LegalSystem(0.7f, 0.7f, 0.7f, 0.7f)
                    : new LegalSystem(0.7f, 0.4f, 0.25f, 0.6f);
                float rol = RuleOfLawRules.RuleOfLawIndex(legal);
                const float enforcement = 0.6f; // デモ警察力
                int repressed = 0;
                foreach (var s in map.systems)
                {
                    if (s == null || s.owner != fac) continue;
                    if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;
                    float unemployment = UnityEngine.Mathf.Clamp01(OccupationRules.UnemploymentPressure(prov));
                    float poverty = UnityEngine.Mathf.Clamp01(1f - prov.livingStandard);
                    var r = LawTickRules.TickProvince(rol, unemployment, poverty, 0.3f, enforcement, cp);
                    // 秩序で安定度を緩やかに補正（GovernanceRules 収束と競合させない）。
                    prov.stability = UnityEngine.Mathf.Clamp(prov.stability + r.stabilityDelta * 0.1f, 0f, 100f);
                    if (r.repression > 0.4f) repressed++;
                }
                if (RuleOfLawRules.IsRuleByLawOnly(legal) && repressed > 0)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                        $"{fac} 法治体制で取締りが抑圧化（{repressed} 星系）＝正統性を蝕む");
            }
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

        /// <summary>
        /// 文官の官歴を1年ぶん回す（官僚制基盤＝<see cref="BureaucracyCareerRules"/> へ委譲）。文民ネームドに位階を叙し、
        /// 考課（能×徳×績）で叙位／貶位する。<b>五位の壁</b>は朝廷の権威が高いとき（律令が機能）だけ越えられる
        /// ＝封建の世（権威低）では門閥以外は貴族へ上がれない。叙位の節目（五位突破）は通知へ。
        /// </summary>
        private void RunBureaucracyTick()
        {
            if (civilians == null || civilians.Count == 0) return;
            var changes = new List<BureaucracyCareerRules.CareerChange>();
            BureaucracyCareerRules.TickYear(
                civilians, courtAuthority != null ? courtAuthority.authority : 0f,
                campaignYear, BureaucracyCareerRules.CareerParams.Default, changes);

            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i].kind != BureaucracyCareerRules.CareerEventKind.五位突破) continue;
                Person p = FindCivilian(changes[i].personId);
                if (p == null) continue;
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{p.faction} {p.name} 叙従五位下＝貴族に列す（{JapaneseCourtRankRules.Name(changes[i].from)}→{JapaneseCourtRankRules.Name(changes[i].to)}）");
            }

            // 清廉度の動態（汚職）：監督（朝廷の権威）が弱いほど汚職が育つ＝名誉職化が腐敗を生む（考課の徳・内政に跳ね返る）。
            float authority = courtAuthority != null ? courtAuthority.authority : 0f;
            for (int i = 0; i < civilians.Count; i++)
            {
                Person c = civilians[i];
                if (c == null || c.role != PersonRole.文民 || c.merit == null) continue;
                c.merit.integrity = OfficialIntegrityRules.Tick(
                    c.merit.integrity, authority, OfficialIntegrityRules.IntegrityParams.Default);
            }
        }

        /// <summary>戦乱度＝前線/交戦の広がり（敵対艦隊が停泊する星系の割合）。朝廷の権威の動態の入力。</summary>
        private float WarIntensity()
        {
            if (map == null || map.systems.Count == 0) return 0f;
            int war = 0, total = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                total++;
                if (HasHostileFleetAt(s)) war++;
            }
            return total > 0 ? (float)war / total : 0f;
        }

        /// <summary>朝廷の権威を1年ぶん動かす（戦乱で武家台頭↓／平時は律令回復↑）。形骸化の段階が変われば通知。</summary>
        private void RunCourtAuthorityTick()
        {
            if (courtAuthority == null) return;
            RitsuryoPhase before = RitsuryoFormalizationRules.PhaseOf(courtAuthority.authority);
            CourtAuthorityRules.TickYear(courtAuthority, WarIntensity(), CourtAuthorityRules.AuthorityParams.Default);
            RitsuryoPhase after = RitsuryoFormalizationRules.PhaseOf(courtAuthority.authority);
            if (after != before)
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                    $"朝廷の権威が変動：{before}→{after}（官職の名実が{((int)after > (int)before ? "乖離" : "一致")}へ）");
        }

        private Person FindCivilian(int id)
        {
            if (civilians == null) return null;
            for (int i = 0; i < civilians.Count; i++)
                if (civilians[i] != null && civilians[i].id == id) return civilians[i];
            return null;
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

            // 外交コマンド（#2119 操作化）：対立勢力へ 7=宣戦 / 8=講和 / 9=同盟。自勢力の外交はプレイヤーが握る。
            if (kb.digit7Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.宣戦布告);
            if (kb.digit8Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.講和);
            if (kb.digit9Key.wasPressedThisFrame) IssueDiplomacyToRival(DiplomaticAction.同盟);

            // ミッションコマンド（任務戦術）：C＝マウス直下の敵対星系へ攻略任務／V＝対立勢力を攻略（参謀本部が目標選定・必要兵力を見積もり自動動員）。
            if (kb.cKey.wasPressedThisFrame) IssueMissionAtMouse();
            if (kb.vKey.wasPressedThisFrame) IssueCampaignAgainstRival();

            // セーブ/ロード（continue・全永続化）：F5=保存／F9=読込（読込後 Strategy を再ロードして再構築）。
            if (kb.f5Key.wasPressedThisFrame) SaveCampaign();
            if (kb.f9Key.wasPressedThisFrame) LoadCampaign();
        }

        /// <summary>対立勢力（プレイヤー以外の最初のデモ勢力）へ外交コマンドを発令。発令不可なら通知。</summary>
        private void IssueDiplomacyToRival(DiplomaticAction action)
        {
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            Faction rival = player;
            for (int i = 0; i < DemoFactions.Length; i++)
                if (DemoFactions[i] != player) { rival = DemoFactions[i]; break; }
            if (rival == player) return; // 対立勢力なし
            if (!IssuePlayerDiplomacy(rival, action))
                NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{action} は今は発令できません（{rival} との現状態）");
        }

        /// <summary>
        /// ミッションコマンド（任務戦術）：マウス直下の敵対星系へ「攻略せよ」と任務を下す。
        /// 参謀本部（自勢力の最有能指揮官の文才）が必要兵力を見積もり、遊休艦隊から必要十分を自動動員して進軍させる。
        /// 必要規模は参謀本部の実力で可変＝有能なら無駄なく軍団/軍集団を、無能なら過小動員のまま発動する。
        /// </summary>
        private void IssueMissionAtMouse()
        {
            if (cam == null || map == null || reg == null) return;
            Vector2 w = WorldMouse();
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > 1.2f) return;
            ExecuteMission(map.GetSystem(sysId));
        }

        /// <summary>
        /// ミッションコマンド（任務戦術）：「◯◯勢力を攻略せよ」＝対立勢力を相手に攻撃目標を参謀本部が選定し任務を下す。
        /// 避実撃虚で到達可能な最も攻めやすい敵星系を選び（兵力を分散させず一点に集中）、`ExecuteMission` で自動動員・進軍させる。
        /// </summary>
        private void IssueCampaignAgainstRival()
        {
            if (map == null || reg == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            Faction rival = player;
            for (int i = 0; i < DemoFactions.Length; i++)
                if (DemoFactions[i] != player) { rival = DemoFactions[i]; break; }
            if (rival == player) return; // 対立勢力なし

            // 敵勢力の星系を攻撃目標候補に。守備兵力＝在席敵対艦隊／到達可否＝自勢力星系から経路あり。
            var targets = new List<CampaignTarget>();
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                if (!FactionRelations.IsHostile(null, player, s.ownerData, s.owner)) continue; // 敵対星系のみ
                float garrison = 0f;
                var here = reg.FleetsAt(s.id);
                if (here != null)
                    for (int k = 0; k < here.Count; k++)
                        if (here[k] != null && FactionRelations.IsHostile(null, player, null, here[k].faction)) garrison += here[k].strength;
                bool defended = s.planet != null && !s.planet.Captured;
                targets.Add(new CampaignTarget(s.id, garrison, defended, ReachableByFaction(player, s.id)));
            }

            int targetId = MissionCommandRules.SelectCampaignTarget(targets);
            if (targetId < 0)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.情報,
                    $"{rival} 攻略：到達可能な攻撃目標がありません");
                return;
            }
            ExecuteMission(map.GetSystem(targetId));
        }

        /// <summary>その勢力のいずれかの所有星系から目標星系へ回廊経路で到達可能か（避実撃虚の到達可否）。</summary>
        private bool ReachableByFaction(Faction faction, int goalId)
        {
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.owner != faction) continue;
                if (s.id == goalId) return true;
                if (GalaxyPathfinder.FindPath(map, s.id, goalId).Count > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 任務の実行：参謀本部が必要兵力を見積もり、遊休艦隊から必要十分を自動動員して進軍させる。
        /// 必要規模は参謀本部の実力で可変。<b>戦力の集中が満たせなければ逐次投入せず「集中待機」する（孫子）</b>。
        /// </summary>
        private void ExecuteMission(StarSystem s)
        {
            if (s == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            // 自国/友軍星系には攻略任務を出さない（敵対星系のみ）。
            if (!FactionRelations.IsHostile(null, player, s.ownerData, s.owner))
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.情報,
                    $"{s.systemName} は攻略対象外（自国/友軍）");
                return;
            }

            // 敵戦力＝目標星系に在席する敵対艦隊の合計。防衛惑星があれば攻者三倍の対象（defended）。
            float enemyStrength = 0f;
            var here = reg.FleetsAt(s.id);
            if (here != null)
                for (int i = 0; i < here.Count; i++)
                {
                    StrategicFleet g = here[i];
                    if (g != null && FactionRelations.IsHostile(null, player, null, g.faction)) enemyStrength += g.strength;
                }
            bool defended = s.planet != null && !s.planet.Captured;

            // 参謀本部の実力（0..1）＝自勢力の最有能指揮官の文才（運営/情報）。
            float staff = StaffCompetence(player);

            // 動員候補＝自勢力の遊休（停泊中・非交戦）艦隊。
            var avail = new List<MissionForce>();
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != player) continue;
                if (f.IsOnCorridor || f.engaged) continue;          // 移動中/交戦中は動員しない
                if (f.currentSystemId == s.id) continue;             // 既に目標星系に居る艦は除く
                avail.Add(new MissionForce(f.id, f.strength));
            }

            MissionPlan plan = MissionCommandRules.PlanMission(
                s.id, MissionType.星系攻略, player, enemyStrength, defended, staff, avail);

            if (plan.fleetIds.Count == 0)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                    $"{s.systemName} 攻略任務：動員可能な遊休艦隊がありません");
                return;
            }

            // 兵力の集中（孫子＝戦力の逐次投入をしない）：集中が満たせない有能な参謀本部は発動せず待機する。
            if (!plan.launched)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                    $"任務：{s.systemName} 攻略は戦力集中まで待機（逐次投入を避ける）。動員可能{plan.committedStrength:0}/必要{plan.requiredStrength:0}");
                return;
            }

            // 動員した艦隊を目標星系へ進軍させる（どう動くかは各艦の経路探索に委ねる＝任務戦術）。
            for (int i = 0; i < plan.fleetIds.Count; i++)
            {
                StrategicFleet f = reg.GetFleet(plan.fleetIds[i]);
                if (f != null) f.WarpTo(map, s.id);
            }

            string scale = plan.echelon.ToString();
            string note = plan.piecemeal ? "（逐次投入＝兵力不足のまま発動）" : "";
            NotificationCenter.Push(NotificationCategory.占領,
                plan.piecemeal ? NotificationSeverity.注意 : NotificationSeverity.情報,
                $"任務：{s.systemName} 攻略。{scale}を集中動員（{plan.fleetIds.Count}隊・兵力{plan.committedStrength:0}/{plan.requiredStrength:0}）{note}");
        }

        /// <summary>参謀本部の実力（0..1）＝その勢力の最有能指揮官の文才（運営/情報の平均）を正規化。指揮官不在は中庸0.5。</summary>
        private float StaffCompetence(Faction faction)
        {
            if (commanders == null) return 0.5f;
            float best = -1f;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.faction != faction || c.IsDeceased) continue;
                if (c.CivilAptitude > best) best = c.CivilAptitude;
            }
            return best < 0f ? 0.5f : Mathf.Clamp01(best / 100f);
        }

        /// <summary>戦役の全状態（銀河/勢力/財政/人物/艦隊/時間/内政）をファイルへ書き出す共通処理。</summary>
        private void WriteCampaignSave()
        {
            var people = new System.Collections.Generic.List<Person>();
            if (commanders != null) people.AddRange(commanders);
            if (civilians != null) people.AddRange(civilians);
            CampaignSaveManager.SaveSession(StrategySession.Campaign, people, reg, StrategySession.Clock, StrategySession.Provinces);
        }

        /// <summary>戦役の全状態をファイルへ保存する（F5・手動）。</summary>
        private void SaveCampaign()
        {
            WriteCampaignSave();
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報, "セーブしました（F9 で再開）");
        }

        /// <summary>年境界ごとの自動保存（閉じても進行が消えないように）。F9/タイトルの「戦役を再開」で復帰できる。</summary>
        private void AutoSaveCampaign()
        {
            WriteCampaignSave();
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報, "オートセーブ");
        }

        /// <summary>セーブから全状態を StrategySession へ復元し、Strategy シーンを再ロードして盤面を再構築する（F9）。</summary>
        private void LoadCampaign()
        {
            if (!CampaignSaveManager.HasSave()) { NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意, "セーブがありません"); return; }
            if (CampaignSaveManager.LoadSession())
                SceneManager.LoadScene("Strategy");
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

            // 軍の質（C4）：降下する艦隊の補給（弾薬即応）を戦闘力倍率へ＝干上がった艦隊は会戦で弱い。
            // 下士官団/新兵練度はユニット未attribute（#210）ゆえ既定（null/0.5中立）。
            BattleHandoff.qualityA = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(a.supply));
            BattleHandoff.qualityB = ForceQualityRules.CombatMultiplier(null, 0.5f, MilitaryReadinessRules.FirepowerFactor(b.supply));

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
