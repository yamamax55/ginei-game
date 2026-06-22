using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 軍人立志伝の Game 層エンジン（EPIC #2477 配線・Strategy 自動生成）。主人公（<see cref="GameSettings.selectedAdmiral"/>）を
    /// 士官学校から任官させ（TKO-1）、<b>月次評定</b>（TKO-10 <see cref="MonthlyCouncilRules"/>）で武勲昇進（TKO-2）と主命の決着を回し、
    /// 君主の主命を<b>指揮系統で噛み砕いて</b>（TKO-11 <see cref="MandateCascadeRules"/>）末端の主人公へ落とす。達成は武勲（<see cref="MeritRecordRules"/>）と
    /// 恩義（TKO-3 <see cref="PersonRelationRules"/>）を生み、一代記（TKO-6 <see cref="ProtagonistChronicle"/>）に刻まれ <see cref="NotificationCenter"/> へ通知する。
    /// 月境界は統一クロックの経過秒（<see cref="GameClock.ElapsedSeconds"/>÷月秒）で自前に検出＝`GalaxyView` を編集しない（additive）。
    /// 純ロジック・状態遷移は Core 窓口へ委譲し、本クラスは配線のみ。<see cref="ProtagonistDeskOverlay"/>（Alt+J）が状態を映す。
    /// </summary>
    public class ProtagonistCareerDirector : MonoBehaviour
    {
        [Header("立身出世ループ（調整値）")]
        [Tooltip("主命を遂行（会戦/任務で達成）する月あたりの確率")]
        [Range(0f, 1f)] public float mandateSuccessChance = 0.35f;
        [Tooltip("主命が無い月に新たな主命を拝命する確率")]
        [Range(0f, 1f)] public float issueChance = 0.6f;
        [Tooltip("准将未満（尉官/佐官）の月あたり勤務功績＝昇進モンタージュの速さ")]
        public float juniorServiceMerit = 14f;

        // 艦隊指揮の下限（准将＝tier5）。これ未満は尉官/佐官＝モンタージュで駆け上がる。
        private const int FlagRankTier = 5;

        // 会戦→武勲の換算（P1-a #2477）。与ダメをこの量で割って撃沈功績の magnitude に（上限 MaxBattleMerit）。
        private const float DamagePerSinkMerit = 50f;
        private const int MaxBattleMerit = 30;

        // 1月あたりの game-秒（GameDate.DateParams 既定＝60秒/日×30日）。
        private const float SecondsPerMonth = 60f * 30f;
        private const int ProtagonistId = 900001;
        private const int SovereignId = 900000;
        private const int EnrollYear = 796;
        private int nextMandateId = 870000;

        public static ProtagonistCareerDirector Instance { get; private set; }

        // 立身出世の状態（執務机 UI が読む）。
        public Person Protagonist { get; private set; }
        public Person Sovereign { get; private set; }
        public FactionData FactionRanks { get; private set; }
        public MeritRecord Merit { get; private set; }
        public SovereignMandate ActiveMandate { get; private set; }
        public PersonRelationGraph Relations { get; private set; }
        public ProtagonistChronicle Chronicle { get; private set; }
        public IReadOnlyList<CascadeLevel> Cascade => cascade;
        public PersonOrigin Origin { get; private set; } // 出自（採用「出自選択」・平民/貴族/王家）
        /// <summary>主人公の会戦成長（GrowthRegistry・無ければ null）。執務机が実効能力の表示に使う。</summary>
        public Growth HeroGrowth => heroAdmiralKey != 0 ? GrowthRegistry.Get(heroAdmiralKey) : null;
        /// <summary>不満（主命失敗で増・達成で減）＝岐路判定の入力。</summary>
        public float Grievance => grievance;
        /// <summary>武名（ADM-3 #2304・0..100・政界転身の政治資本）。</summary>
        public int Fame => fame;

        private List<CascadeLevel> cascade;
        private Faction pf = Faction.同盟;
        private int lastCouncilMonth;
        private int heroAdmiralKey; // 主人公の AdmiralData.GetInstanceID()＝会戦成長台帳 GrowthRegistry のキー（P1-b 永続）
        private float grievance;    // 不満（主命失敗で増・達成で減）＝岐路判定 CareerForkRules 用
        private int fame;           // 武名（ADM-3 #2304・戦功で上がり政界転身の資本に・RenownRules）
        private int pendingPetitions; // 未裁可の具申（TKO-4・月次評定で上官が裁可＝序列内・MEYASU の決裁デスクは通さない）
        private readonly List<string> pendingPetitionKeys = new List<string>(); // 未裁可の具申の effectKey（採用で TryDecode→効果適用）
        private bool retired;       // 下野（TKO-7 岐路）＝月次ループを止める
        private int ageMonths;      // 主人公の年齢（月）＝加齢/老衰死・継承の駆動（LifecycleRules）
        private bool ready;

        private const int StartAgeMonths = 18 * 12; // 任官時の年齢（士官学校卒～18歳）

        // 会戦の戦果インボックス（別シーンの BattleManager が積む・static で Battle→Strategy を跨いで保持）。
        // 次の月次評定でドレインして武勲化＝立身出世が「時間経過」でなく「会戦の結果」で進む（P1-a #2477）。
        private static float pendingBattleDamage;
        private static int pendingBattleVictories;
        private static int pendingBattleCount;

        /// <summary>会戦の戦果を主人公の武勲インボックスへ積む（<see cref="BattleManager"/> から・別シーン可）。
        /// 主人公の艦隊ぶんだけを渡すこと（判定は呼び出し側）。次の月次評定で武勲へ変換される。</summary>
        public static void ReportBattle(float damageDealt, bool isWinner)
        {
            if (damageDealt > 0f) pendingBattleDamage += damageDealt;
            if (isWinner) pendingBattleVictories++;
            pendingBattleCount++;
        }

        private static readonly MeritRecordRules.MeritRecordParams MeritP = MeritRecordRules.MeritRecordParams.Default;
        private static readonly SovereignMandateRules.MandateParams MandateP = SovereignMandateRules.MandateParams.Default;
        // 評定は発令しない（issueChance=0）＝発令はカスケード経由で本クラスが行う。
        private static readonly MonthlyCouncilRules.CouncilParams CouncilNoIssue = new MonthlyCouncilRules.CouncilParams(1, 0f);

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
            if (scene.name != "Strategy") return; // 立身出世ループは戦略マップで回す
            if (Object.FindAnyObjectByType<ProtagonistCareerDirector>() != null) return;
            new GameObject("ProtagonistCareerDirector").AddComponent<ProtagonistCareerDirector>();
        }

        private void Awake()
        {
            Instance = this;
            // 観測層規約：独立ルート（本ディレクタの static 状態）を J（CoreStateInspector）へ1行登録（重複ラベルは上書き）。
            CoreStateInspector.Register("立身出世 (ProtagonistCareer)", BuildInspectorSnapshot);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // J（汎用インスペクタ）用の状態スナップショット（観測専用・読むだけ）。未準備/不在は null。
        private static object BuildInspectorSnapshot()
        {
            ProtagonistCareerDirector d = Instance;
            if (d == null || d.Protagonist == null) return null;
            var snap = new Dictionary<string, object>
            {
                ["主人公"] = d.Protagonist.name,
                ["階級"] = $"{d.RankName(d.Protagonist.rankTier)} (tier{d.Protagonist.rankTier})",
                ["武勲点"] = d.Merit != null ? d.Merit.points : 0f,
                ["昇進確定段数"] = d.Merit != null ? d.Merit.meritPromotionsApplied : 0,
                ["主命"] = d.ActiveMandate != null
                    ? $"{d.ActiveMandate.kind}/{d.ActiveMandate.status}/期限{d.ActiveMandate.dueMonth}月" : "なし",
                ["君主"] = d.Sovereign != null ? d.Sovereign.name : "（不在）",
                ["戦果インボックス"] = $"与ダメ{pendingBattleDamage:0}/勝利{pendingBattleVictories}/会戦{pendingBattleCount}",
            };
            return snap;
        }

        private void Update()
        {
            if (StrategySession.Campaign == null) return;
            if (!ready) { Setup(); return; }

            GameClock clock = StrategySession.Clock;
            if (clock == null) return;
            int nowMonth = Mathf.FloorToInt((float)clock.ElapsedSeconds / SecondsPerMonth);
            int guard = 0;
            while (lastCouncilMonth < nowMonth && guard++ < 24) // 一度に高々24ヶ月だけ追いつく
            {
                lastCouncilMonth++;
                RunCouncil(lastCouncilMonth);
            }
        }

        // ===== 立身出世ループ本体 =====

        private void RunCouncil(int month)
        {
            if (Protagonist == null || retired) return; // 下野/一代記完結後はループ停止（舞台を降りた）
            ageMonths++;
            if (ageMonths % 12 == 0 && CheckDeathAndSuccession(month)) return; // 加齢→老衰死→継承/一代記完結
            int beforeTier = Protagonist.rankTier;

            // 尉官/佐官時代は日々の勤務で武勲が少しずつ積む（昇進モンタージュ＝准将で実艦隊指揮へ）。
            if (Protagonist.rankTier < FlagRankTier)
                MeritRecordRules.Record(Merit, ExploitKind.任務達成, juniorServiceMerit, MeritP);

            // 会戦の戦果を武勲へ取り込む（会戦結果で駆動・P1-a #2477）。勝利があれば主命達成の主因になる。
            bool wonBattle = DrainBattleMerit(month);

            // 主命の遂行：会戦で勝てば達成（戦果で駆動）。会戦が無い月は従来の簡易ロールでフォールバック。
            if (SovereignMandateRules.IsOpen(ActiveMandate) && (wonBattle || Random.value < mandateSuccessChance))
            {
                float favor = SovereignMandateRules.Complete(ActiveMandate, Merit, MeritP, MandateP);
                SovereignMandateRules.ApplyOutcomeFavor(Relations, ActiveMandate, favor);
                grievance = Mathf.Max(0f, grievance - 0.15f); // 達成で不満が和らぐ
                ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.主命達成, $"{ActiveMandate.kind}を完遂");
                Push(NotificationSeverity.情報, $"［主命達成］{ActiveMandate.kind}を完遂（武勲を得た）");
                ActiveMandate = null;
            }

            // 評定：期限超過の主命を失敗確定＋保留武勲昇進を1段確定（発令はしない）。
            // 昇進は階級ピラミッドの定員空きで律速（上ほど狭き門・TKO-13）。ピラミッド未配備なら律速しない。
            var pyr = RankPyramidDirector.Instance;
            System.Func<int, bool> gate = pyr != null ? (System.Func<int, bool>)(t => pyr.CanPromote(pf, t)) : null;
            var outcome = MonthlyCouncilRules.Hold(Protagonist, Merit, FactionRanks, ActiveMandate, Sovereign,
                month, nextMandateId, () => Random.value, MeritP, MandateP, CouncilNoIssue, Relations, gate);

            if (ActiveMandate != null && ActiveMandate.status == MandateStatus.失敗)
            {
                grievance = Mathf.Clamp01(grievance + 0.2f); // 失敗で不満が募る（岐路の引き金）
                ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.主命失敗, $"{ActiveMandate.kind}が期限切れ");
                Push(NotificationSeverity.注意, $"［主命失敗］{ActiveMandate.kind}（期限切れ）");
                ActiveMandate = null;
            }
            if (outcome.promoted)
            {
                ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.昇進, RankName(Protagonist.rankTier));
                Push(NotificationSeverity.情報, $"［昇進］{Protagonist.name} が {RankName(Protagonist.rankTier)} へ");
            }
            // 将官に列す節目（准将＝艦隊指揮の下限）。
            if (beforeTier < FlagRankTier && Protagonist.rankTier >= FlagRankTier)
            {
                ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.昇進,
                    $"将官に列す（{RankName(Protagonist.rankTier)}・実艦隊指揮）");
                Push(NotificationSeverity.情報,
                    $"［将官昇任］{Protagonist.name} が {RankName(Protagonist.rankTier)} に列し、実艦隊の指揮を委ねられる");
            }

            // 具申の結実（TKO-4・序列内）：上官が拾って裁可＝採用で建白採用の武勲。MEYASU の決裁デスク（god-view）は通さない。
            if (pendingPetitions > 0)
            {
                float pf2 = (Relations != null && Sovereign != null)
                    ? PersonRelationRules.NetAffinity(Relations, Protagonist.id, Sovereign.id) : 0f;
                float adoptChance = Mathf.Clamp01(0.35f + pf2 * 0.4f); // 上官との関係が良いほど拾われる
                int adopted = 0, budgetAdopted = 0, baseAdopted = 0;
                long budgetBefore = ProtagonistGrantStore.Grants.fleetBudgetGrant;
                for (int i = 0; i < pendingPetitions; i++)
                {
                    if (Random.value >= adoptChance) continue;
                    adopted++;
                    // 採用された具申を decode して実際の効果へ反映（記帳だけ→効く）。keys 不足分（旧セーブ）は汎用建白。
                    string key = i < pendingPetitionKeys.Count ? pendingPetitionKeys[i] : "";
                    PetitionEffect eff = PetitionEffectRules.Apply(ProtagonistGrantStore.Grants, key);
                    if (eff == PetitionEffect.予算増額) budgetAdopted++;
                    else if (eff == PetitionEffect.根拠地変更) baseAdopted++;
                }
                pendingPetitions = 0;
                pendingPetitionKeys.Clear();
                if (adopted > 0)
                {
                    MeritRecordRules.Record(Merit, ExploitKind.建白採用, adopted, MeritP);
                    ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.武勲, $"建白{adopted}件が容れられた");
                    Push(NotificationSeverity.情報, $"［建白採用］具申{adopted}件が上官に容れられ武勲を得た");
                    if (budgetAdopted > 0)
                    {
                        long delta = ProtagonistGrantStore.Grants.fleetBudgetGrant - budgetBefore;
                        Push(NotificationSeverity.情報, $"［予算拝領］運用予算枠 +{delta:#,0}（累計 {ProtagonistGrantStore.Grants.fleetBudgetGrant:#,0}）");
                    }
                    if (baseAdopted > 0)
                    {
                        int sys = ProtagonistGrantStore.Grants.fleetBaseSystemId;
                        Push(NotificationSeverity.情報, sys >= 0 ? $"［根拠地変更］根拠地が星系 {sys} に改められた" : "［根拠地変更］根拠地の変更が容れられた（司令部一任）");
                    }
                }
            }

            // 新たな主命をカスケードで拝命（君主の主命を指揮系統で噛み砕き末端へ）。
            if (ActiveMandate == null && Random.value < issueChance)
            {
                ActiveMandate = IssueViaCascade(month);
                if (ActiveMandate != null)
                {
                    ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.主命拝命,
                        $"{ResolveName(ActiveMandate.issuerId)} より「{ActiveMandate.kind}」");
                    Push(NotificationSeverity.注意, $"［主命］{ResolveName(ActiveMandate.issuerId)} より「{ActiveMandate.kind}」を拝命");
                }
            }
        }

        // 亡命（最小・キャリア継続型）：敵勢力へ降り、降格して士官として再出仕（艦隊/領土は持ち出さない）。
        // 一人称＝主人公の所属がプレイヤーの所属＝GameSettings.playerFaction も追随。
        private void ApplyDefection(int month)
        {
            Faction enemy = FindEnemyFaction();
            pf = enemy;
            Protagonist.faction = enemy;
            Protagonist.rankTier = Mathf.Max(1, Protagonist.rankTier - 2); // 亡命者は降格して再出仕
            Sovereign = FindSovereignFor(enemy)
                ?? new Person(SovereignId, "君主", enemy, PersonRole.軍人) { isSovereign = true, rankTier = 10 };
            Merit = new MeritRecord(ProtagonistId);
            fame = Mathf.RoundToInt(fame * 0.5f); SyncFameToRegistry(); // 旧友の信は失う
            grievance = 0f; pendingPetitions = 0; pendingPetitionKeys.Clear(); ProtagonistGrantStore.Clear(); ActiveMandate = null; retired = false;
            Relations = new PersonRelationGraph();
            PersonRelationRules.LinkCommand(Relations, Sovereign, Protagonist, 0.1f); // 新主君とは薄い縁から
            var gs = GameSettings.Instance;
            if (gs != null) gs.playerFaction = enemy;
            ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.岐路, $"亡命（{enemy} へ降る）");
            Push(NotificationSeverity.警告, $"［亡命］{Protagonist.name} は {enemy} へ降った（降格して再出仕）");
        }

        // 独立（最小）：新勢力(FactionData)を樹立し主人公を君主に。本拠1星系を割譲（ownerData のみ・enum は据え置き）。
        // 経済/政治/AI の本格統合は後段＝本実装は「自ら旗を立て自分が主君」までの最小。
        private void ApplyIndependence(int month)
        {
            var newFaction = ScriptableObject.CreateInstance<FactionData>();
            newFaction.factionName = $"{Protagonist.name}独立軍";
            newFaction.legacyFaction = pf; // 旧母体の系譜（敵対判定の後方互換）
            string home = "辺境";
            GalaxyMap map = StrategySession.Map;
            if (map != null && map.systems != null)
            {
                for (int i = 0; i < map.systems.Count; i++)
                {
                    StarSystem s = map.systems[i];
                    if (s != null && s.owner == pf) { s.ownerData = newFaction; home = s.systemName; break; }
                }
            }
            Protagonist.isSovereign = true; // 自ら主君に
            Sovereign = Protagonist;
            ActiveMandate = null; retired = false; grievance = 0f; pendingPetitions = 0; pendingPetitionKeys.Clear(); ProtagonistGrantStore.Clear();
            var gs = GameSettings.Instance;
            if (gs != null) gs.playerFactionData = newFaction; // プレイヤーは独立勢力を率いる
            ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.岐路, $"独立（{home} で旗揚げ・{newFaction.factionName}）");
            Push(NotificationSeverity.警告, $"［独立］{Protagonist.name} が {home} で旗を揚げた（{newFaction.factionName}・最小実装）");
        }

        private Faction FindEnemyFaction()
        {
            GalaxyMap map = StrategySession.Map;
            if (map != null && map.systems != null)
            {
                for (int i = 0; i < map.systems.Count; i++)
                {
                    StarSystem s = map.systems[i];
                    if (s != null && s.owner != pf && FactionRelations.IsHostile(null, pf, null, s.owner)) return s.owner;
                }
            }
            return pf == Faction.帝国 ? Faction.同盟 : Faction.帝国;
        }

        private Person FindSovereignFor(Faction f)
        {
            var gv = Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null || gv.CommanderRoster == null) return null;
            var roster = gv.CommanderRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p != null && p.faction == f && p.isSovereign && p.IsAvailable) return p;
            }
            return null;
        }

        // 政界転身：主人公を自勢力の与党（無ければ最大党）へ入党させる（政党#159・GOV-6）。best-effort（政党未整備なら何もしない）。
        private bool RegisterAsPolitician()
        {
            CampaignState c = StrategySession.Campaign;
            if (c == null || c.states == null) return false;
            FactionState fs = null;
            for (int i = 0; i < c.states.Count; i++)
                if (c.states[i] != null && c.states[i].faction == pf) { fs = c.states[i]; break; }
            if (fs == null || fs.politics == null || fs.politics.parties == null || fs.politics.parties.Count == 0) return false;
            Party party = PartyRules.RulingParty(fs.politics.parties) ?? fs.politics.parties[0];
            if (party == null) return false;
            if (!party.memberIds.Contains(Protagonist.id)) party.memberIds.Add(Protagonist.id);
            return true;
        }

        // 主人公の実行時武名を会戦の鼓舞（ShipCombat）へ反映＝AdmiralData の InstanceID キーで FameRegistry へ。
        private void SyncFameToRegistry()
        {
            if (heroAdmiralKey != 0) FameRegistry.Set(heroAdmiralKey, fame);
        }

        // インボックスの戦果を武勲へ変換し、勝利の有無を返す（主命達成の主因判定に使う）。
        private bool DrainBattleMerit(int month)
        {
            if (pendingBattleCount <= 0) return false;
            float dmg = pendingBattleDamage; int wins = pendingBattleVictories;
            pendingBattleDamage = 0f; pendingBattleVictories = 0; pendingBattleCount = 0;

            int sink = Mathf.Clamp(Mathf.RoundToInt(dmg / DamagePerSinkMerit), 0, MaxBattleMerit);
            if (sink > 0) MeritRecordRules.Record(Merit, ExploitKind.撃沈, sink, MeritP);   // 与ダメ→撃沈功績
            if (wins > 0) MeritRecordRules.Record(Merit, ExploitKind.旗艦撃破, wins, MeritP); // 勝利→殊勲
            fame = RenownRules.Gain(fame, sink + wins * 5); // 戦功で武名が上がる（ADM-3・大勝ほど＝政界転身の資本）
            SyncFameToRegistry(); // 稼いだ武名を会戦（ShipCombat の鼓舞）へ反映（ADM-3）
            if (sink > 0 || wins > 0)
                Push(NotificationSeverity.情報, "［戦功］会戦の働きで武勲を得た");
            return wins > 0;
        }

        // 年境界で老衰死を判定し、死んだら継承（貴族/王家）or 一代記完結（平民）を処理する（採用「死で詰まない」・#907 解答）。
        // 戻り値 true＝この月は死亡処理で終える。
        private bool CheckDeathAndSuccession(int month)
        {
            int age = ageMonths / 12;
            if (!LifecycleRules.ShouldDieOfAge(age, Random.value, 1, LifecycleRules.LifespanParams.Default)) return false;

            ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.死去, $"{Protagonist.name} 逝去（享年{age}）");

            bool noble = OriginRules.InheritsOnDeath(Origin);
            if (ProtagonistSuccessionRules.ContinuesAsHeir(Origin, hasHeir: noble))
            {
                // 貴族/王家＝世継ぎが家督を継ぎ操作座を引き継ぐ（家督は継ぐが武勲/武名/成長は自分で立て直す）。
                Person heir = ProtagonistHeirRules.CreateHeir(Protagonist, Origin, ProtagonistId, Protagonist.name + "の世継ぎ", EnrollYear + age);
                Protagonist = heir;
                Merit = new MeritRecord(ProtagonistId);
                fame = 0; grievance = 0f; pendingPetitions = 0; pendingPetitionKeys.Clear(); ProtagonistGrantStore.Clear(); retired = false;
                ActiveMandate = null;
                ageMonths = StartAgeMonths;
                SyncFameToRegistry(); // 世継ぎは武名も一から（会戦の鼓舞もリセット）
                if (heroAdmiralKey != 0 && GrowthRegistry.Has(heroAdmiralKey)) GrowthRegistry.Get(heroAdmiralKey).experience = 0f;
                if (Relations != null && Sovereign != null) PersonRelationRules.LinkCommand(Relations, Sovereign, Protagonist, 0.3f);
                ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.岐路,
                    $"{heir.name} が家督を継ぎ {RankName(heir.rankTier)} に出仕");
                Push(NotificationSeverity.警告, $"［継承］{heir.name} が家督を継いだ（{RankName(heir.rankTier)}）");
                return true;
            }

            // 平民＝一代記の完結。次の主人公は新規プレイで（自動では始めない＝肥大化防止）。
            retired = true;
            ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.死去, "一代記、ここに完結（平民の叩き上げは一代限り）");
            Push(NotificationSeverity.警告, $"［一代記完結］{Protagonist.name} の生涯が幕を閉じた（新規プレイで次の主人公を）");
            return true;
        }

        private SovereignMandate IssueViaCascade(int month)
        {
            MandateKind kind = (MandateKind)Random.Range(0, 6);
            List<Person> chain = BuildChain();
            if (chain.Count < 2)
            {
                cascade = null;
                return SovereignMandateRules.Issue(nextMandateId++, Sovereign, Protagonist, kind, "", month, MandateP);
            }
            float topScope = CommandCapacityRules.MaxStrengthForTier(Sovereign.rankTier);
            cascade = MandateCascadeRules.Build(topScope, chain);
            return MandateCascadeRules.ToLeafMandate(cascade, nextMandateId++, pf, kind, "", month, MandateP);
        }

        // 指揮系統チェーン（君主→中間指揮官→主人公）。中間は player 勢力で主人公と君主の間の階級から上位2名。
        private List<Person> BuildChain()
        {
            var chain = new List<Person> { Sovereign };
            var gv = Object.FindAnyObjectByType<GalaxyView>();
            if (gv != null && gv.CommanderRoster != null)
            {
                var mids = new List<Person>();
                var roster = gv.CommanderRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    Person p = roster[i];
                    if (p == null || p.faction != pf || !p.IsAvailable) continue;
                    if (p.id == Sovereign.id || p.id == Protagonist.id) continue;
                    if (p.rankTier > Protagonist.rankTier && p.rankTier < Sovereign.rankTier) mids.Add(p);
                }
                mids.Sort((a, b) => b.rankTier.CompareTo(a.rankTier));
                for (int i = 0; i < mids.Count && i < 2; i++) chain.Add(mids[i]);
            }
            chain.Add(Protagonist);
            return chain;
        }

        // ===== 一人称の動詞：上官への具申（TKO-4） =====

        /// <summary>主人公が直属の上官へ建白を起案・具申する（TKO-4）。稟議在庫へ載せ、稟議オブザーバ（Alt+I）に
        /// 起案者＝主人公・決裁者＝上官として現れる。資格（下位→上位・同勢力）は <see cref="PersonRingiRules"/> が判定。</summary>
        public bool SubmitPetition() => SubmitPetition($"{Protagonist?.name}の建白", "career.petition");

        /// <summary>
        /// 自艦隊への予算増額を上官へ具申する（TKO-4 の具体例）。増額は自分の階級の指揮可能規模を基準に算出し、
        /// effectKey に直列化（<see cref="FleetBudgetPetitionRules"/>）して稟議へ載せる＝採用時に decode して反映できる。
        /// </summary>
        public bool SubmitFleetBudgetPetition()
        {
            if (Protagonist == null) return false;
            if (!RankGateOk(PetitionCategory.予算)) return false;
            int budgetBase = CommandCapacityRules.MaxStrengthForTier(Protagonist.rankTier);
            int amount = FleetBudgetPetitionRules.RecommendedIncrease(budgetBase);
            string key = FleetBudgetPetitionRules.EncodeEffectKey(0, amount); // 番号0＝自艦隊
            return SubmitPetition($"自艦隊への予算増額（{amount:#,0}）の具申", key);
        }

        /// <summary>
        /// 自艦隊の根拠地（母港/展開拠点）の変更を上官へ具申する（TKO-4）。具体的な星系は司令部一任（前線寄りへ）として
        /// effectKey に直列化（<see cref="FleetBasePetitionRules"/>）し、採用時に decode して反映できる。
        /// </summary>
        public bool SubmitFleetBasePetition()
        {
            if (Protagonist == null) return false;
            if (!RankGateOk(PetitionCategory.作戦)) return false;
            string key = FleetBasePetitionRules.EncodeEffectKey(0, FleetBasePetitionRules.UnspecifiedSystem); // 番号0＝自艦隊・星系一任
            return SubmitPetition("自艦隊の根拠地変更（前線寄りへ）の具申", key);
        }

        /// <summary>その区分の具申を今の階級で出せるか（出せなければ通知）。UI も <see cref="PetitionRankRules"/> で同判定。</summary>
        public bool CanRaiseCategory(PetitionCategory category)
            => Protagonist != null && PetitionRankRules.CanRaise(category, Protagonist.rankTier);

        private bool RankGateOk(PetitionCategory category)
        {
            if (CanRaiseCategory(category)) return true;
            int need = PetitionRankRules.MinTier(category);
            Push(NotificationSeverity.注意, $"その具申はあなたの階級では出せません（要 {RankName(need)}）");
            return false;
        }

        /// <summary>
        /// 上官へ賄賂を送る（私財から支出）。成功すれば上官の親愛が上がり覚えがめでたくなる（昇進/具申に有利）。
        /// ただし露見（<see cref="BriberyRules.IsExposed"/>）すると逆に遺恨を買い不満が募る。数値は <see cref="BriberyRules"/> へ委譲。
        /// </summary>
        public bool BribeSuperior()
        {
            if (Protagonist == null) return false;
            Person superior = DirectSuperior();
            if (superior == null) { Push(NotificationSeverity.注意, "賄賂を送る上官がいません"); return false; }
            var bp = BribeParams.Default;
            int cost = BriberyRules.Cost(superior.rankTier, bp);
            if (Protagonist.wealth < cost)
            {
                Push(NotificationSeverity.注意, $"私財が足りません（賄賂 {cost:#,0}／所持 {Protagonist.wealth:#,0}）");
                return false;
            }
            Protagonist.wealth -= cost; // 私財から支出（実状態変更）

            if (BriberyRules.IsExposed(Random.value, bp))
            {
                // 露見＝逆効果：上官の遺恨が募り、不満も増える。
                if (Relations != null)
                {
                    float cur = RelationStrength(superior.id, Protagonist.id, RelationKind.遺恨);
                    Relations.Set(superior.id, Protagonist.id, RelationKind.遺恨, BriberyRules.NewStrength(cur, BriberyRules.ResentmentGain(cost, bp)));
                }
                grievance += 10f;
                Push(NotificationSeverity.警告, $"［賄賂露見］{superior.CharacterName} への贈賄が露見し心証を害した（私財 {cost:#,0} を失う）");
                return false;
            }

            if (Relations != null)
            {
                float cur = RelationStrength(superior.id, Protagonist.id, RelationKind.親愛);
                Relations.Set(superior.id, Protagonist.id, RelationKind.親愛, BriberyRules.NewStrength(cur, BriberyRules.FavorGain(cost, bp)));
            }
            Push(NotificationSeverity.情報, $"［賄賂］{superior.CharacterName} の覚えがめでたくなった（親愛↑・私財 {cost:#,0}・残 {Protagonist.wealth:#,0}）");
            return true;
        }

        /// <summary>関係グラフの強度（無ければ0）。</summary>
        private float RelationStrength(int fromId, int toId, RelationKind kind)
        {
            PersonRelation r = Relations != null ? Relations.Get(fromId, toId, kind) : null;
            return r != null ? r.strength : 0f;
        }

        /// <summary>建白を起案して上官（序列内）へ提出する共通処理。title/effectKey で具申内容を変える。</summary>
        public bool SubmitPetition(string title, string effectKey)
        {
            if (Protagonist == null) return false;
            Person superior = DirectSuperior();
            if (superior == null) { Push(NotificationSeverity.注意, "具申できる上官がいません"); return false; }
            int id = RingiDirector.Ledger.NextId();
            Petition pet = PersonRingiRules.RaiseTo(id, title, Protagonist, superior, effectKey);
            if (pet == null) { Push(NotificationSeverity.注意, "具申の資格がありません（上官でない）"); return false; }
            RingiDirector.Ledger.Add(pet);
            pendingPetitions++; // 月次評定で上官（序列内）が裁可する＝結実すれば建白採用の武勲（P1-d）
            pendingPetitionKeys.Add(effectKey ?? ""); // 採用時に decode して効果適用するため effectKey を控える
            Push(NotificationSeverity.情報, $"［具申］{superior.CharacterName} へ建白を提出（稟議 Alt+I で確認・月次評定で裁可）");
            return true;
        }

        private Person DirectSuperior()
        {
            if (cascade != null && cascade.Count >= 2)
            {
                int sid = cascade[cascade.Count - 2].holderId;
                Person s = FindPerson(sid);
                if (s != null) return s;
            }
            return Sovereign;
        }

        // ===== セーブ/復元（P1-c #2477） =====

        /// <summary>現在の立身出世状態をセーブ平データへ写す（GalaxyView の戦役セーブから呼ぶ）。未準備なら hasData=false。</summary>
        public ProtagonistCareerSave CaptureSave()
        {
            if (!ready || Protagonist == null) return new ProtagonistCareerSave { hasData = false };
            Growth heroGrowth = heroAdmiralKey != 0 ? GrowthRegistry.Get(heroAdmiralKey) : null;
            ProtagonistCareerSave save = ProtagonistCareerSerializer.Capture(Protagonist, Merit, ActiveMandate,
                lastCouncilMonth, nextMandateId, pendingBattleDamage, pendingBattleVictories, pendingBattleCount, heroGrowth, Chronicle);
            save.origin = (int)Origin;
            save.grievance = grievance;
            save.fame = fame;
            save.pendingPetitions = pendingPetitions;
            save.retired = retired;
            save.politician = Protagonist != null && Protagonist.isPolitician;
            save.ageMonths = ageMonths;
            return save;
        }

        /// <summary>主人公にいま開かれているキャリアの岐路（TKO-7・政界転身を含む）。執務机が表示する＝一人称の自由意志の可視化。
        /// 入力は既存状態から導出（忠誠≈君主との親密度・不満＝grievance・実力＝武勲・stature＝階級）。判定は <see cref="CareerForkRules"/>。</summary>
        public List<CareerFork> AvailableForks()
        {
            if (Protagonist == null) return new List<CareerFork> { CareerFork.忠勤 };
            float favor = (Relations != null && Sovereign != null)
                ? PersonRelationRules.NetAffinity(Relations, Protagonist.id, Sovereign.id) : 0f;
            float loyalty = Mathf.Clamp01(0.5f + favor * 0.5f); // 親密度→忠誠の近似
            float merit01 = MeritRecordRules.MeritScore01(Merit, MeritP);
            float politics01 = Mathf.Clamp01(fame / 100f); // 政界転身は武名（ADM-3）を資本に
            return CareerForkRules.AvailableForks(loyalty, grievance, merit01, politics01,
                Mathf.Clamp(favor, -1f, 1f), Protagonist.rankTier, CareerForkRules.ForkParams.Default);
        }

        /// <summary>
        /// 主人公が岐路を選んで実行する（TKO-7・一人称の自由意志）。開かれた進路か（<see cref="AvailableForks"/>）＋前提
        /// （<see cref="CareerForkExecutionRules.CanExecute"/>）を確認し、振り分け先（<see cref="CareerForkExecutionRules.ExecutorFor"/>）で
        /// 内部状態を変える＝下野=月次ループ停止／政界転身=政治家フラグ／亡命・独立=記録＋通知（戦略への反映は後段の Game 配線）。
        /// 実行できなければ通知して false。執務机のボタンから呼ぶ。
        /// </summary>
        public bool ExecuteFork(CareerFork f)
        {
            if (Protagonist == null) return false;
            if (!AvailableForks().Contains(f)) { Push(NotificationSeverity.注意, "その進路はまだ開かれていません"); return false; }
            bool hasEnemyHaven = true;                       // 多勢力＝迎える敵は居る（厳密な受入判定は後段）
            bool hasIndependenceBase = Protagonist.rankTier >= 8; // 大将以上の stature（独立の拠点）
            bool hasPoliticalOpening = true;                 // 政界の受け皿（政党#159 配線は後段）
            if (!CareerForkExecutionRules.CanExecute(f, hasEnemyHaven, hasIndependenceBase, hasPoliticalOpening))
            { Push(NotificationSeverity.注意, "その進路は今は実行できません（前提不足）"); return false; }

            int month = lastCouncilMonth;
            switch (CareerForkExecutionRules.ExecutorFor(f))
            {
                case ForkExecutor.なし:
                    Push(NotificationSeverity.情報, "忠勤を続ける");
                    break;
                case ForkExecutor.退役:
                    retired = true;
                    ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.岐路, "下野（軍を辞す）");
                    Push(NotificationSeverity.注意, $"［岐路］{Protagonist.name} は下野した（軍を辞す）");
                    break;
                case ForkExecutor.政界入り:
                    Protagonist.isPolitician = true;
                    bool joined = RegisterAsPolitician(); // 政党#159 へ入党（与党＝政府の長への道）
                    ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.岐路, "政界転身（政治家提督へ）");
                    Push(NotificationSeverity.情報, joined
                        ? $"［岐路］{Protagonist.name} は政界へ転身し与党に入った（政治家提督）"
                        : $"［岐路］{Protagonist.name} は政界へ転身した（政治家提督）");
                    break;
                case ForkExecutor.亡命受入:
                    ApplyDefection(month);   // 最小・キャリア継続型：敵勢力へ降って降格再出仕
                    break;
                case ForkExecutor.独立樹立:
                    ApplyIndependence(month); // 最小・キャリア継続型：新勢力を樹立し自ら君主に・本拠1星系割譲
                    break;
            }
            return true;
        }

        // ===== セットアップ =====

        private void Setup()
        {
            // 戦果インボックスを新戦役ぶんでリセット（前のプレイの残留を持ち込まない）。
            pendingBattleDamage = 0f; pendingBattleVictories = 0; pendingBattleCount = 0;
            grievance = 0f; fame = 0; pendingPetitions = 0; pendingPetitionKeys.Clear(); ProtagonistGrantStore.Clear(); retired = false; ageMonths = StartAgeMonths;

            var gs = GameSettings.Instance;
            pf = gs != null ? gs.playerFaction : Faction.同盟;
            Origin = gs != null ? gs.selectedOrigin : OriginRules.Default; // 出自（復元時は下で上書き）
            // 立身出世は尉官〜元帥の完全ラダーで段階昇進させる（playerFactionData は将官のみのことが多く段が飛ぶため）。
            FactionRanks = BuildCareerLadder();

            AdmiralData pa = gs != null ? ContentDatabase.AdmiralByName(gs.selectedAdmiral) : null;
            heroAdmiralKey = pa != null ? pa.GetInstanceID() : 0; // 会戦成長台帳のキー（BattleManager と同じ GetInstanceID）
            Protagonist = new Person(ProtagonistId, pa != null ? pa.FullName : "主人公", pf, PersonRole.軍人);
            if (pa != null)
            {
                Protagonist.leadership = pa.leadership; Protagonist.attack = pa.attack;
                Protagonist.defense = pa.defense; Protagonist.mobility = pa.mobility;
                Protagonist.operation = pa.operation; Protagonist.intelligence = pa.intelligence;
            }
            else
            {
                Protagonist.leadership = Protagonist.attack = Protagonist.defense = Protagonist.mobility = 72;
            }

            Sovereign = FindSovereign() ?? new Person(SovereignId, "君主", pf, PersonRole.軍人) { isSovereign = true, rankTier = 10 };
            Relations = new PersonRelationGraph();
            Chronicle = new ProtagonistChronicle();

            // 「続きから」＝立身出世の進捗を復元（P1-c #2477）。あれば士官学校からの再任官をせず復帰する。
            ProtagonistCareerSave loaded = StrategySession.PendingProtagonistCareer;
            StrategySession.PendingProtagonistCareer = null; // 一度だけ消費
            if (loaded != null && loaded.hasData)
            {
                ProtagonistCareerSerializer.ApplyPerson(loaded, Protagonist);
                Merit = ProtagonistCareerSerializer.ToMerit(loaded);
                ActiveMandate = ProtagonistCareerSerializer.ToMandate(loaded);
                if (loaded.nextMandateId > 0) nextMandateId = loaded.nextMandateId;
                pendingBattleDamage = loaded.pendingBattleDamage;
                pendingBattleVictories = loaded.pendingBattleVictories;
                pendingBattleCount = loaded.pendingBattleCount;
                // 会戦で得た提督の成長（XP）を復元（P1-b 永続＝捨てない）。会戦時の GrowthRegistry キーと一致。
                if (loaded.hasGrowth && heroAdmiralKey != 0)
                    GrowthRegistry.GetOrCreate(heroAdmiralKey, (GrowthArchetype)loaded.growthArchetype).experience = loaded.growthExperience;
                ProtagonistCareerSerializer.ApplyChronicle(loaded, Chronicle); // 一代記（TKO-6）を復元
                Origin = (PersonOrigin)loaded.origin; // 出自を復元
                grievance = loaded.grievance;
                fame = loaded.fame;
                SyncFameToRegistry(); // 続きからでも武名が会戦に効く
                pendingPetitions = loaded.pendingPetitions;
                retired = loaded.retired;
                Protagonist.isPolitician = loaded.politician;
                if (loaded.politician) RegisterAsPolitician(); // 続きからでも与党在籍を復元（政党は再シードされるため best-effort）
                ageMonths = loaded.ageMonths > 0 ? loaded.ageMonths : StartAgeMonths;
                PersonRelationRules.LinkCommand(Relations, Sovereign, Protagonist, 0.3f);
                lastCouncilMonth = loaded.lastCouncilMonth;
                Push(NotificationSeverity.情報, $"［復帰］{Protagonist.name}＝{RankName(Protagonist.rankTier)}（武勲{Merit.points:0}）");
                ready = true;
                return;
            }

            Merit = new MeritRecord(ProtagonistId);

            // 士官学校から任官（TKO-1）。出自で入口の学校が変わる（平民=士官学校/貴族=幼年学校/王家=王室教育）。
            // 実体の選抜は既存の多段選抜（MilitaryAcademyRules）へ接続＝ここは入口の名称ぶんだけ反映（数値は不変・後方互換）。
            string schoolName = OriginRules.SchoolName(OriginRules.PathFor(Origin));
            var academy = new Academy(7, pf, schoolName, 200, 0.55f);
            var outcome = ProtagonistCareerRules.EnrollWithClass(Protagonist, academy, 60, EnrollYear, 910000, i => Random.value);
            ProtagonistChronicleRules.Record(Chronicle, 0, ChronicleEventKind.入校, $"{OriginRules.Title(Origin)}・{schoolName}へ入校");
            // 任官は少尉から（大学校卒＝大尉 fast-track）。准将までは月次評定のモンタージュで駆け上がる（TKO-12）。
            int commission = MilitaryAcademyRules.CommissionTier(outcome.degree, outcome.hammockNumber);
            commission = Mathf.Max(commission, OriginRules.CommissionFloor(Origin)); // 出自の“スタートの段”（貴族/王家は先行）
            if (commission <= 0) commission = 1; // 主人公は最低 少尉で任官
            Protagonist.rankTier = commission;
            ProtagonistChronicleRules.Record(Chronicle, 0, ChronicleEventKind.卒業任官,
                $"{MilitaryAcademyRules.DegreeTitle(outcome.degree)}・席次{outcome.hammockNumber}・{RankName(commission)}に任官");
            Push(NotificationSeverity.情報, $"［任官］{Protagonist.name}＝{RankName(commission)}（席次{outcome.hammockNumber}）");

            // 君主との初期関係（恩義の芽）。
            PersonRelationRules.LinkCommand(Relations, Sovereign, Protagonist, 0.3f);

            GameClock clock = StrategySession.Clock;
            lastCouncilMonth = clock != null ? Mathf.FloorToInt((float)clock.ElapsedSeconds / SecondsPerMonth) : 0;
            ready = true;
        }

        private Person FindSovereign()
        {
            var gv = Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null || gv.CommanderRoster == null) return null;
            var roster = gv.CommanderRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p != null && p.faction == pf && p.isSovereign && p.IsAvailable) return p;
            }
            return null;
        }

        private Person FindPerson(int id)
        {
            if (Sovereign != null && Sovereign.id == id) return Sovereign;
            if (Protagonist != null && Protagonist.id == id) return Protagonist;
            var gv = Object.FindAnyObjectByType<GalaxyView>();
            if (gv != null && gv.CommanderRoster != null)
            {
                var roster = gv.CommanderRoster;
                for (int i = 0; i < roster.Count; i++)
                    if (roster[i] != null && roster[i].id == id) return roster[i];
            }
            return null;
        }

        /// <summary>id を人物名へ（不明は「上官」）。</summary>
        public string ResolveName(int id)
        {
            Person p = FindPerson(id);
            return p != null ? p.CharacterName : "上官";
        }

        /// <summary>tier を階級名へ（勢力の階級表→尉官/佐官を含む完全ラダー＝立身出世版）。</summary>
        public string RankName(int tier) => RankSystem.CareerRankName(FactionRanks, tier);

        // 尉官〜元帥の完全ラダー（少尉1〜元帥10）。月次評定が NextRankTier で1段ずつ辿れるよう全段を持つ（TKO-12）。
        private FactionData BuildCareerLadder()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(1, "少尉"), new FactionData.RankEntry(2, "大尉"),
                new FactionData.RankEntry(3, "少佐"), new FactionData.RankEntry(4, "大佐"),
                new FactionData.RankEntry(5, "准将"), new FactionData.RankEntry(6, "少将"),
                new FactionData.RankEntry(7, "中将"), new FactionData.RankEntry(8, "大将"),
                new FactionData.RankEntry(9, "上級大将"), new FactionData.RankEntry(10, "元帥"),
            };
            return f;
        }

        private static void Push(NotificationSeverity sev, string msg)
            => NotificationCenter.Push(NotificationCategory.人事, sev, msg);
    }
}
