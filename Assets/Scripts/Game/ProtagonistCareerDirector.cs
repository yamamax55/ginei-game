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

        private List<CascadeLevel> cascade;
        private Faction pf = Faction.同盟;
        private int lastCouncilMonth;
        private bool ready;

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

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

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
            if (Protagonist == null) return;
            int beforeTier = Protagonist.rankTier;

            // 尉官/佐官時代は日々の勤務で武勲が少しずつ積む（昇進モンタージュ＝准将で実艦隊指揮へ）。
            if (Protagonist.rankTier < FlagRankTier)
                MeritRecordRules.Record(Merit, ExploitKind.任務達成, juniorServiceMerit, MeritP);

            // 主命の遂行（会戦/任務での達成を簡略にロール＝将来は会戦結果で駆動）。
            if (SovereignMandateRules.IsOpen(ActiveMandate) && Random.value < mandateSuccessChance)
            {
                float favor = SovereignMandateRules.Complete(ActiveMandate, Merit, MeritP, MandateP);
                SovereignMandateRules.ApplyOutcomeFavor(Relations, ActiveMandate, favor);
                ProtagonistChronicleRules.Record(Chronicle, month, ChronicleEventKind.主命達成, $"{ActiveMandate.kind}を完遂");
                Push(NotificationSeverity.情報, $"［主命達成］{ActiveMandate.kind}を完遂（武勲を得た）");
                ActiveMandate = null;
            }

            // 評定：期限超過の主命を失敗確定＋保留武勲昇進を1段確定（発令はしない）。
            var outcome = MonthlyCouncilRules.Hold(Protagonist, Merit, FactionRanks, ActiveMandate, Sovereign,
                month, nextMandateId, () => Random.value, MeritP, MandateP, CouncilNoIssue, Relations);

            if (ActiveMandate != null && ActiveMandate.status == MandateStatus.失敗)
            {
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
        public bool SubmitPetition()
        {
            if (Protagonist == null) return false;
            Person superior = DirectSuperior();
            if (superior == null) { Push(NotificationSeverity.注意, "具申できる上官がいません"); return false; }
            int id = RingiDirector.Ledger.NextId();
            Petition pet = PersonRingiRules.RaiseTo(id, $"{Protagonist.name}の建白", Protagonist, superior, "career.petition");
            if (pet == null) { Push(NotificationSeverity.注意, "具申の資格がありません（上官でない）"); return false; }
            RingiDirector.Ledger.Add(pet);
            Push(NotificationSeverity.情報, $"［具申］{superior.CharacterName} へ建白を提出（稟議 Alt+I で確認）");
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

        // ===== セットアップ =====

        private void Setup()
        {
            var gs = GameSettings.Instance;
            pf = gs != null ? gs.playerFaction : Faction.同盟;
            // 立身出世は尉官〜元帥の完全ラダーで段階昇進させる（playerFactionData は将官のみのことが多く段が飛ぶため）。
            FactionRanks = BuildCareerLadder();

            AdmiralData pa = gs != null ? ContentDatabase.AdmiralByName(gs.selectedAdmiral) : null;
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
            Merit = new MeritRecord(ProtagonistId);
            Relations = new PersonRelationGraph();
            Chronicle = new ProtagonistChronicle();

            // 士官学校から任官（TKO-1）。
            var academy = new Academy(7, pf, "士官学校", 200, 0.55f);
            var outcome = ProtagonistCareerRules.EnrollWithClass(Protagonist, academy, 60, EnrollYear, 910000, i => Random.value);
            ProtagonistChronicleRules.Record(Chronicle, 0, ChronicleEventKind.入校, "士官学校へ入校");
            // 任官は少尉から（大学校卒＝大尉 fast-track）。准将までは月次評定のモンタージュで駆け上がる（TKO-12）。
            int commission = MilitaryAcademyRules.CommissionTier(outcome.degree, outcome.hammockNumber);
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
