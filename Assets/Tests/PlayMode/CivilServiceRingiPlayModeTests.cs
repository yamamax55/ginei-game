using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 省内職位の人事（#141）を<b>稟議・決裁へ接続した経路</b>を固定条件で通す PlayMode 試験：
    /// 権限のある操作者（所管大臣）は決裁カードを作らず直接台帳へ反映する／権限外の操作者は<b>官僚機構で握り潰されず</b>
    /// 正規の上申先（<b>効果キーから復号した省の所管大臣</b>＝一般の分野推定〔内政〕ではない）へ決裁カードとして届く／
    /// 同じ人事は二重に起票しない／裁可で台帳が<b>一度だけ</b>動く（二度目の裁可は効かない）／見送りは何も変えずカードを閉じる／
    /// 承認までに状態が変わっていれば（対象者の死亡など）台帳を変えず理由を結果へ残す／保存往復で効果キー・対象・決裁者が保たれる。
    /// <para>GalaxyView は無効な GameObject に載せて Start を走らせない（盤面は <c>BindElectionQaWorld</c> で差し込む）。
    /// 内閣は選挙の乱数に依らせず Core の共通入口（<see cref="CabinetAppointmentRules"/>）で決め打ちに組む。
    /// セーブファイルは読み書きしない（JSON は文字列の往復だけ）。</para>
    /// </summary>
    public class CivilServiceRingiPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;
        private const int PremierId = 11;             // 政治家 11..20（11=首相・12〜=各省の大臣）
        private const int PoliticianCount = 10;
        private const int BureaucratId = 31;          // 既存の職業官僚 31..36
        private const int BureaucratCount = 6;
        private const int Newcomer = 40;              // 未配属の入省候補（人事の対象）
        private static readonly CabinetParams CabPrm = CabinetParams.Default;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameClock savedClock;
        private DecisionQueue savedDecisions;
        private PetitionLedger savedPetitions;
        private System.Func<PendingDecision, DecisionAuthorityResult> savedAuthority;
        private List<GovernmentRegistry.Appointment> savedAppointments;
        private GalaxyView savedActive;

        [SetUp]
        public void SetUp()
        {
            savedActive = GalaxyView.Active;
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
            savedClock = StrategySession.Clock;
            savedDecisions = StrategySession.Decisions;
            savedPetitions = StrategySession.Petitions;
            savedAuthority = DecisionDeck.AuthorityCheck;
            savedAppointments = new List<GovernmentRegistry.Appointment>(GovernmentRegistry.Appointments);

            StrategySession.Decisions = new DecisionQueue();
            StrategySession.Petitions = new PetitionLedger();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            GalaxyView.SwapActiveForQa(savedActive);
            DecisionDeck.AuthorityCheck = savedAuthority;
            StrategySession.Decisions = savedDecisions;
            StrategySession.Petitions = savedPetitions;
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
            StrategySession.Clock = savedClock;
            GovernmentRegistry.Clear();
            for (int i = 0; i < savedAppointments.Count; i++)
            {
                GovernmentRegistry.Appointment a = savedAppointments[i];
                GovernmentRegistry.TryAppoint(a.faction, a.office, a.holder, a.scopeKey);
            }
        }

        // ===== 盤面 =====

        private static GalaxyMap NewMap()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(CapitalId, "試験首都星", Vector2.zero, Faction.同盟));
            map.AddSystem(new StarSystem(FrontierId, "試験辺境星", new Vector2(1f, 0f), Faction.同盟));
            return map;
        }

        private static Dictionary<int, Province> NewProvinces()
            => new Dictionary<int, Province>
            {
                { CapitalId, new Province(CapitalId, "", 1000f) },
                { FrontierId, new Province(FrontierId, "", 600f) },
            };

        /// <summary>職業官僚（政治家でない文民）：入省・課長級の資格（正七位上・考課6.0）を満たす。</summary>
        private static Person NewBureaucrat(int id, int aptitude)
        {
            var p = new Person(id, "試験官僚" + id, Faction.同盟, PersonRole.文民)
            { birthYear = 750, courtRank = CourtRank.正七位上, operation = aptitude, intelligence = aptitude };
            p.merit = new OfficialMerit(id) { evaluations = 4, cumulativeScore = 24f }; // 平均6.0
            return p;
        }

        private static List<Person> NewCivilians()
        {
            var list = new List<Person>();
            for (int i = 0; i < PoliticianCount; i++)
                list.Add(new Person(PremierId + i, "試験政治家" + (PremierId + i), Faction.同盟, PersonRole.文民)
                { isPolitician = true, birthYear = 760, charisma = 60, operation = 90 - i, intelligence = 90 });
            for (int i = 0; i < BureaucratCount; i++) list.Add(NewBureaucrat(BureaucratId + i, 30 - i));
            return list;
        }

        private GalaxyView NewView(GalaxyMap map, Dictionary<int, Province> provinces, List<Person> civilians)
        {
            var go = new GameObject("GalaxyView_CivilServiceRingiQa");
            go.SetActive(false); // Start を走らせない（盤面は QA 入口で差し込む）
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        /// <summary>稟議の執行（決裁の購読）と決裁の権限判定を本番と同じ Director で配線する。</summary>
        private void SpawnDirectors()
        {
            var ringiGo = new GameObject("RingiDirector_Qa");
            spawned.Add(ringiGo);
            RingiDirector ringi = ringiGo.AddComponent<RingiDirector>();
            ringi.raiseInterval = 1e9f;      // 試験中に自発的な建白を起こさせない
            ringi.agendaCooldownSeconds = 1e9f;

            var authGo = new GameObject("DecisionAuthorityDirector_Qa");
            spawned.Add(authGo);
            authGo.AddComponent<DecisionAuthorityDirector>(); // Awake で DecisionDeck.AuthorityCheck を差し込む
        }

        /// <summary>同盟（共和制）＋政府シード＋内閣＋人事台帳（年次人事で初期化＝既存配属を移行）まで整えた盤面。</summary>
        private GalaxyView StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians)
        {
            GalaxyMap map = NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            civilians = NewCivilians();
            campaign = new CampaignState(map);
            alliance = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            GalaxyView view = NewView(map, provinces, civilians);
            view.SeedGovernmentForQa();
            FormCabinet(alliance, view, civilians, FirstYear);
            GalaxyView.SwapActiveForQa(view); // 権限判定・執行が読む盤面
            SpawnDirectors();
            view.RunCivilServiceAnnualTickForQa(); // 台帳を作り既存の配属を移行する（以後の人事の土台）
            return view;
        }

        private static void FormCabinet(FactionState alliance, GalaxyView view, List<Person> roster, int year)
        {
            PoliticsState pol = alliance.politics;
            var ruling = new Party(1, "試験民政党", Faction.同盟) { leaderId = PremierId, support = 1f };
            for (int i = 0; i < PoliticianCount; i++) ruling.memberIds.Add(PremierId + i);
            pol.parties.Add(ruling);
            pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = PremierId, partyId = 1, formedYear = year, sourceElectionId = "qa",
            };

            List<Ministry> tree = Tree(view);
            int top = view.TopMinistryIdOf(Faction.同盟);
            CabinetAppointmentRules.Reconcile(pol, Faction.同盟, tree, top, roster, year, CabPrm);
            int next = PremierId + 1;
            for (int i = 0; i < tree.Count; i++)
            {
                if (tree[i].id == top) continue;
                AppointmentResult r = CabinetAppointmentRules.TryAppoint(pol, Faction.同盟, PremierId, tree, top,
                    tree[i].id, CabinetPostKind.大臣, next, roster, year, "試験：承認権者を置く", CabPrm);
                Assert.IsTrue(r.ok, tree[i].ministryName + " に大臣を置けない：" + r.reason);
                next++;
            }
        }

        // ===== 参照 =====

        private static List<Ministry> Tree(GalaxyView view) => new List<Ministry>(view.MinistriesOf(Faction.同盟));

        private static Ministry MinistryNamed(GalaxyView view, string suffix)
        {
            List<Ministry> tree = Tree(view);
            for (int i = 0; i < tree.Count; i++)
                if (tree[i] != null && tree[i].ministryName.EndsWith(suffix)) return tree[i];
            return null;
        }

        private static int MinisterOf(FactionState alliance, int ministryId)
        {
            CabinetPost post = CabinetAppointmentRules.FindPost(alliance.politics.cabinet, ministryId, CabinetPostKind.大臣);
            return post != null ? post.holderId : -1;
        }

        private static Person Find(List<Person> roster, int id) => roster.Find(p => p.id == id);

        private static PendingDecision Card(int id)
        {
            DecisionQueue q = DecisionDeck.Queue;
            for (int i = 0; i < q.items.Count; i++)
                if (q.items[i] != null && q.items[i].id == id) return q.items[i];
            return null;
        }

        /// <summary>未解決の決裁カードの件数（起票していないことの確認に使う）。</summary>
        private static int ActiveCards() => DecisionDeck.Queue != null ? DecisionDeck.Queue.ActiveCount() : 0;

        // ===== 1. 権限があれば直接実行（カードを作らない） =====

        [UnityTest]
        public IEnumerator DirectExecute_WhenActorIsMinister_UpdatesLedgerWithoutRaisingCard()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            yield return null;

            Ministry hyobu = MinistryNamed(view, "兵部省");
            Assert.IsNotNull(hyobu);
            int minister = MinisterOf(alliance, hyobu.id);
            Assert.Greater(minister, 0, "前提：兵部省に所管大臣がいる");

            hyobu.staffSlots++;                      // 入省の空きを1つ作る
            civilians.Add(NewBureaucrat(Newcomer, 20));
            view.BindPlayerCharacterForQa(Find(civilians, minister));
            int cardsBefore = ActiveCards();

            AppointmentResult preview = view.PreviewPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚);
            Assert.IsTrue(preview.ok, preview.reason);
            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer), "見込みで台帳が動いた");

            CivilServiceRequestResult r = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚, "試験：直接の任用");
            Assert.AreEqual(CivilServiceRequestOutcome.実行, r.outcome, r.reason);
            Assert.IsTrue(r.DidExecute);
            Assert.AreEqual(-1, r.decisionId, "直接実行で決裁カードを作った");
            Assert.AreEqual(cardsBefore, ActiveCards(), "直接実行で決裁カードが増えた");

            CivilServiceRecord rec = CivilServicePostRules.FindServing(alliance.civilService, Newcomer);
            Assert.IsNotNull(rec, "台帳へ反映されない");
            Assert.AreEqual(hyobu.id, rec.ministryId);
            Assert.AreEqual(BureaucratGrade.一般官僚, rec.grade);
            Assert.AreEqual(minister, rec.appointedById, "承認したのは所管大臣でない");
            StringAssert.Contains("試験：直接の任用", rec.reason, "理由が人事履歴へ残らない");
            Assert.IsTrue(hyobu.staffIds.Contains(Newcomer), "省庁の配属と食い違う");
        }

        [UnityTest]
        public IEnumerator DirectExecute_UsesDefaultReason_WhenReasonIsBlank()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            yield return null;

            Ministry hyobu = MinistryNamed(view, "兵部省");
            int minister = MinisterOf(alliance, hyobu.id);
            hyobu.staffSlots++;
            civilians.Add(NewBureaucrat(Newcomer, 20));
            view.BindPlayerCharacterForQa(Find(civilians, minister));

            CivilServiceRequestResult r = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚, "   ");
            Assert.IsTrue(r.DidExecute, r.reason);
            CivilServiceRecord rec = CivilServicePostRules.FindServing(alliance.civilService, Newcomer);
            StringAssert.Contains(CivilServiceRingiRules.DefaultReason(CivilServiceAction.配属), rec.reason,
                "理由が空のとき既定文を残さない");
        }

        // ===== 2. 権限外は上申 → 裁可で一度だけ反映 =====

        [UnityTest]
        public IEnumerator Petition_WhenActorLacksAuthority_GoesToOwningMinister_AndAppliesOnceOnApproval()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            yield return null;

            Ministry hyobu = MinistryNamed(view, "兵部省");   // 軍事所管＝効果キーの一般推定（内政）とは違う省
            int minister = MinisterOf(alliance, hyobu.id);
            hyobu.staffSlots++;
            civilians.Add(NewBureaucrat(Newcomer, 20));

            Person clerk = Find(civilians, BureaucratId);      // 権限を持たない職業官僚が起案する
            view.BindPlayerCharacterForQa(clerk);

            CivilServiceRequestResult r = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚, "試験：上申の任用");
            Assert.AreEqual(CivilServiceRequestOutcome.上申, r.outcome, r.reason);
            Assert.AreEqual(minister, r.addresseeId, "上申先が所管（兵部）大臣でない＝効果キーの分野推定で代用している");
            Assert.GreaterOrEqual(r.decisionId, GalaxyView.CivilServiceDecisionIdBand);
            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer), "上申の時点で台帳が動いた");

            PendingDecision card = Card(r.decisionId);
            Assert.IsNotNull(card, "決裁カードが積まれない");
            Assert.AreEqual(r.effectKey, card.effectKey);
            Assert.IsTrue(CivilServiceRingiRules.TryDecode(card.effectKey, out CivilServicePostRequest req));
            Assert.AreEqual(hyobu.id, req.ministryId);
            Assert.AreEqual(Newcomer, req.personId);
            Assert.AreEqual(CivilServiceAction.配属, req.action);
            Assert.AreEqual(clerk.id, card.proposerId);
            Assert.AreEqual(minister, card.deciderId);
            Assert.AreEqual(2, card.choices.Count);
            Assert.AreEqual("裁可する", card.choices[0]);
            Assert.AreEqual("見送る（現状維持）", card.choices[1]);
            Assert.AreEqual(1, card.defaultChoiceIndex, "既定は現状維持でない");
            StringAssert.Contains("試験：上申の任用", card.body, "理由がカード本文に載らない");
            StringAssert.Contains("対象：", card.body);
            Assert.Greater(card.petitionId, 0, "稟議と紐づかない");
            Petition pet = RingiDirector.Ledger.Get(card.petitionId);
            Assert.IsNotNull(pet, "稟議台帳に載らない（官僚機構で握り潰された）");
            Assert.AreEqual(PetitionStatus.決裁待ち, pet.status);
            Assert.AreEqual(minister, pet.addresseeId);

            // 同じ人事は二重に起票しない
            CivilServiceRequestResult dup = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚, "試験：二重の上申");
            Assert.AreEqual(CivilServiceRequestOutcome.却下, dup.outcome);
            StringAssert.Contains("決裁待ち", dup.reason);
            Assert.AreEqual(1, RingiDirector.Ledger.ActiveCount(), "稟議が二重に起票された");

            // 起案者本人は裁可できない＝上申として扱われ、台帳は動かない
            Assert.IsFalse(DecisionDeck.Resolve(card.id, 0), "権限のない起案者が裁可できてしまう");
            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer), "権限外の裁可で台帳が動いた");
            Assert.IsFalse(card.applied);

            // 所管大臣が裁可＝ここで初めて台帳が動く
            view.BindPlayerCharacterForQa(Find(civilians, minister));
            card.escalated = false; // 上申中の札を、決裁権者自身の操作として確定させる
            Assert.IsTrue(DecisionDeck.Resolve(card.id, 0), "所管大臣が裁可できない");

            CivilServiceRecord rec = CivilServicePostRules.FindServing(alliance.civilService, Newcomer);
            Assert.IsNotNull(rec, "裁可しても台帳が動かない");
            Assert.AreEqual(hyobu.id, rec.ministryId);
            Assert.AreEqual(minister, rec.appointedById);
            StringAssert.Contains("試験：上申の任用", rec.reason, "カード本文の理由が人事履歴へ渡らない");
            Assert.IsTrue(card.applied);
            Assert.AreEqual(PetitionActionOutcome.実行, card.outcome, card.resultDetail);
            Assert.IsNotEmpty(card.resultDetail);
            Assert.AreEqual(PetitionStatus.執行済, RingiDirector.Ledger.Get(card.petitionId).status);

            // 二度目の裁可は効かない（効果は一度だけ）
            int records = alliance.civilService.records.Count;
            Assert.IsFalse(DecisionDeck.Resolve(card.id, 0), "決裁済みの札を再び確定できる");
            Assert.AreEqual(records, alliance.civilService.records.Count, "同じ人事が二度反映された");
            Assert.AreEqual(1, CountServingIn(alliance.civilService, hyobu.id, Newcomer));
        }

        private static int CountServingIn(CivilServiceState st, int ministryId, int personId)
        {
            int n = 0;
            for (int i = 0; i < st.records.Count; i++)
                if (st.records[i].IsServing && st.records[i].ministryId == ministryId && st.records[i].personId == personId) n++;
            return n;
        }

        // ===== 3. 見送り／承認前に状態が変わった場合 =====

        [UnityTest]
        public IEnumerator Petition_Deferred_ChangesNothing_AndStaleApproval_FailsWithReason()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            yield return null;

            Ministry hyobu = MinistryNamed(view, "兵部省");
            int minister = MinisterOf(alliance, hyobu.id);
            hyobu.staffSlots += 2;
            civilians.Add(NewBureaucrat(Newcomer, 20));
            civilians.Add(NewBureaucrat(Newcomer + 1, 20));
            Person clerk = Find(civilians, BureaucratId);

            // --- 見送り：人事は変わらず、稟議は却下で閉じる ---
            view.BindPlayerCharacterForQa(clerk);
            CivilServiceRequestResult a = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚, "試験：見送られる任用");
            Assert.IsTrue(a.DidPetition, a.reason);
            PendingDecision cardA = Card(a.decisionId);
            int recordsBefore = alliance.civilService.records.Count;

            view.BindPlayerCharacterForQa(Find(civilians, minister));
            cardA.escalated = false;
            Assert.IsTrue(DecisionDeck.Resolve(cardA.id, 1), "見送りを確定できない");
            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer), "見送りで人事が動いた");
            Assert.AreEqual(recordsBefore, alliance.civilService.records.Count);
            Assert.AreEqual(PetitionStatus.却下, RingiDirector.Ledger.Get(cardA.petitionId).status);
            Assert.IsTrue(cardA.applied, "見送りの札が閉じていない（もう一度押せてしまう）");
            Assert.AreNotEqual(PetitionActionOutcome.実行, cardA.outcome);
            Assert.IsFalse(DecisionDeck.Resolve(cardA.id, 0), "見送った札を裁可し直せる");
            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer));

            // --- 承認前に対象者が死亡：台帳を変えず理由を残す ---
            view.BindPlayerCharacterForQa(clerk);
            CivilServiceRequestResult b = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer + 1, BureaucratGrade.一般官僚, "試験：裁可前に失われる任用");
            Assert.IsTrue(b.DidPetition, b.reason);
            PendingDecision cardB = Card(b.decisionId);

            Find(civilians, Newcomer + 1).deathYear = FirstYear; // 決裁までに対象者が死ぬ

            view.BindPlayerCharacterForQa(Find(civilians, minister));
            cardB.escalated = false;
            Assert.IsTrue(DecisionDeck.Resolve(cardB.id, 0), "裁可そのものは通る（承認と執行成功は別）");
            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer + 1), "失われた対象へ人事が通った");
            Assert.AreEqual(recordsBefore, alliance.civilService.records.Count, "台帳が動いた");
            Assert.IsTrue(cardB.applied);
            Assert.AreNotEqual(PetitionActionOutcome.実行, cardB.outcome);
            StringAssert.Contains("死亡", cardB.resultDetail, "失敗の理由が結果に残らない");
            Assert.AreNotEqual(PetitionStatus.承認, RingiDirector.Ledger.Get(cardB.petitionId).status,
                "執行できなかった稟議を承認のまま残している");
        }

        // ===== 4. 保存往復 =====

        [UnityTest]
        public IEnumerator Petition_SurvivesSaveLoad_WithEffectKeyTargetAndDecider()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            yield return null;

            Ministry hyobu = MinistryNamed(view, "兵部省");
            int minister = MinisterOf(alliance, hyobu.id);
            civilians.Add(NewBureaucrat(Newcomer, 20));
            Person clerk = Find(civilians, BureaucratId);
            view.BindPlayerCharacterForQa(clerk);

            CivilServiceRequestResult r = view.SubmitPlayerCivilServicePost(hyobu.id, CivilServiceAction.配属,
                Newcomer, BureaucratGrade.一般官僚, "試験：保存される任用");
            Assert.IsTrue(r.DidPetition, r.reason);
            PendingDecision card = Card(r.decisionId);

            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignSerializer.WriteDecisions(save, DecisionDeck.Queue);
            CampaignSerializer.WritePetitions(save.petitions, RingiDirector.Ledger);
            CampaignSaveData back = CampaignSerializer.Parse(JsonUtility.ToJson(save));
            Assert.IsNotNull(back);

            DecisionQueue restoredCards = CampaignSerializer.ReadDecisions(back);
            var restoredLedger = new PetitionLedger();
            CampaignSerializer.ReadPetitions(back.petitions, restoredLedger);

            PendingDecision loaded = null;
            for (int i = 0; i < restoredCards.items.Count; i++)
                if (restoredCards.items[i] != null && restoredCards.items[i].id == card.id) loaded = restoredCards.items[i];
            Assert.IsNotNull(loaded, "人事の決裁カードが保存されない");
            Assert.AreEqual(card.effectKey, loaded.effectKey, "効果キーが保たれない");
            Assert.IsTrue(CivilServiceRingiRules.TryDecode(loaded.effectKey, out CivilServicePostRequest req),
                "読み戻した効果キーから人事を復元できない");
            Assert.AreEqual(hyobu.id, req.ministryId, "対象の省が保たれない");
            Assert.AreEqual(Newcomer, req.personId, "対象の人物が保たれない");
            Assert.AreEqual(CivilServiceAction.配属, req.action);
            Assert.AreEqual(BureaucratGrade.一般官僚, req.targetGrade);
            Assert.AreEqual(minister, loaded.deciderId, "決裁者が保たれない");
            Assert.AreEqual(clerk.id, loaded.proposerId, "提案者が保たれない");
            Assert.AreEqual(card.petitionId, loaded.petitionId);
            Assert.IsFalse(loaded.applied, "未執行の札が適用済みで戻った");
            Assert.AreEqual("試験：保存される任用", CivilServiceRingiRules.ExtractReason(loaded.body),
                "理由がカード本文として保存されない");

            Petition loadedPet = restoredLedger.Get(loaded.petitionId);
            Assert.IsNotNull(loadedPet, "稟議が保存されない");
            Assert.AreEqual(card.effectKey, loadedPet.effectKey);
            Assert.AreEqual(PetitionStatus.決裁待ち, loadedPet.status);
            Assert.AreEqual(minister, loadedPet.addresseeId);
        }
    }
}
