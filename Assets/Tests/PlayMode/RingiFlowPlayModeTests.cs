using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議の決裁フローを<b>PlayMode（実ランタイム）</b>で通す自動テスト：<see cref="RingiDirector"/>（MonoBehaviour）が
    /// 建白を起こし、<see cref="DecisionDeck"/> の決裁（<see cref="DecisionDeck.Resolved"/> 合流）で <see cref="RingiPipeline"/> が
    /// 執行し、世界（<see cref="FactionState.taxRate"/>）が動く／見送りでは動かないことを固定する。EditMode 純ロジックでは
    /// 検証できない「MonoBehaviour の購読→執行」配線を実機で担保する。
    /// </summary>
    public class RingiFlowPlayModeTests
    {
        private GameObject directorGo;
        private GameObject fleetDirectorGo;

        private static FactionState SetupCampaign()
        {
            DecisionDeck.Queue.items.Clear();   // 前テストの積み残しを掃く（id 衝突回避）
            RingiDirector.Ledger.Clear();
            FleetRingiDirector.Ledger.Clear();
            var camp = new CampaignState();
            var fs = new FactionState(Faction.同盟); // taxRate 0.3
            fs.credibility.globalDeference = 1f;
            CredibilityRules.Adjust(fs.credibility, BoxKind.政治家, 1f); // heed を最大化＝建白が浮上しやすい
            camp.states = new List<FactionState> { fs };
            StrategySession.Campaign = camp;
            if (GameSettings.Instance != null) GameSettings.Instance.playerFaction = Faction.同盟;
            return fs;
        }

        // 統治政策テストで差し替える static（TearDown で元へ戻す）
        private Dictionary<int, Province> savedProvinces;
        private GalaxyMap savedMap;
        private System.Func<PendingDecision, DecisionAuthorityResult> savedAuthorityCheck;
        private bool staticsSaved;

        [TearDown]
        public void TearDown()
        {
            // ★利用者の campaign_save.json を最初に戻す（以降の片付けが失敗しても残さない）。
            try { RestoreSaveFile(); }
            finally
            {
                if (directorGo != null) Object.Destroy(directorGo);
                if (fleetDirectorGo != null) Object.Destroy(fleetDirectorGo);
                RestoreFleetStatics();
                StrategySession.Campaign = null;
                DecisionDeck.Queue.items.Clear();
                RingiDirector.Ledger.Clear();
                FleetRingiDirector.Ledger.Clear();
                if (staticsSaved)
                {
                    StrategySession.Provinces = savedProvinces;
                    StrategySession.Map = savedMap;
                    DecisionDeck.AuthorityCheck = savedAuthorityCheck;
                    staticsSaved = false;
                }
                RestoreSession();
            }
        }

        // ===== 星系別統治政策の上申（#67/#109）＝SubmitGovernancePolicy → DecisionDeck.Resolve → OnResolved =====

        private const int GovSystemId = 7;

        /// <summary>
        /// 統治政策テストの前提：自勢力の1星系（民生）と、地方箱の信認を最大化した勢力。
        /// 権限判定は「誰でも裁可できる」前提に固定する（AuthorityCheck=null＝DecisionDeck の後方互換経路。
        /// 権限の上申経路は #67 側のテストが担う）。GalaxyView は置かない＝摩擦は既定値。
        /// 所有の確認は <see cref="StrategySession.Map"/> で行うので、対象星系を同盟所有で地図に置く。
        /// </summary>
        private Province SetupGovernance()
        {
            FactionState fs = SetupCampaign();
            CredibilityRules.Adjust(fs.credibility, BoxKind.地方, 1f, GovSystemId.ToString());

            savedProvinces = StrategySession.Provinces;
            savedMap = StrategySession.Map;
            savedAuthorityCheck = DecisionDeck.AuthorityCheck;
            staticsSaved = true;
            DecisionDeck.AuthorityCheck = null;

            var prov = new Province(GovSystemId, "") { governancePolicy = GovernancePolicy.民生 };
            StrategySession.Provinces = new Dictionary<int, Province> { { GovSystemId, prov } };

            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = GovSystemId, systemName = "テスト星系", owner = Faction.同盟 });
            StrategySession.Map = map;
            StrategySession.Campaign.map = map; // 保存（ToSaveData）は campaign.map を書く
            return prov;
        }

        /// <summary>統治政策の上申を浮上するまで繰り返す（伝播は確率的）。決裁id（&lt;0=失敗）。</summary>
        private static int SubmitGovernanceUntilSurfaced(RingiDirector dir, GovernancePolicy target, int maxTries = 400)
        {
            for (int i = 0; i < maxTries; i++)
            {
                int id = dir.SubmitGovernancePolicy(GovSystemId, "テスト星系", Faction.同盟, target);
                if (id >= 0) return id;
            }
            return -1;
        }

        private static PendingDecision FindDecision(int id)
        {
            for (int i = 0; i < DecisionDeck.Queue.items.Count; i++)
                if (DecisionDeck.Queue.items[i] != null && DecisionDeck.Queue.items[i].id == id)
                    return DecisionDeck.Queue.items[i];
            return null;
        }

        [UnityTest]
        public IEnumerator Governance_Approve_ChangesPolicyExactlyOnce()
        {
            Province prov = SetupGovernance();
            var dir = NewDirector();
            yield return null;

            int decisionId = SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.動員);
            Assert.GreaterOrEqual(decisionId, 0, "統治政策の上申が決裁デスクまで浮上しなかった");
            PendingDecision d = FindDecision(decisionId);
            Assert.IsNotNull(d);
            Assert.AreEqual(GovernanceRules.PolicyPetitionKey(GovSystemId, GovernancePolicy.動員), d.effectKey);
            Assert.AreEqual(GovernancePolicy.民生, prov.governancePolicy, "上申しただけでは変わらない");

            int resolvedCount = 0;
            System.Action<PendingDecision, int> counter = (pd, _) => { if (pd != null && pd.id == decisionId) resolvedCount++; };
            DecisionDeck.Resolved += counter;
            try
            {
                Assert.IsTrue(DecisionDeck.Resolve(decisionId, 0), "裁可できなかった");
                Assert.AreEqual(1, resolvedCount, "Resolved は1回だけ届く（未実装扱いで止まっていない）");
                Assert.AreEqual(GovernancePolicy.動員, prov.governancePolicy, "裁可で統治政策が切り替わる");
                Assert.IsTrue(d.applied);
                Assert.AreEqual(PetitionActionOutcome.実行, d.outcome);
                Petition pet = RingiDirector.Ledger.Get(d.petitionId);
                Assert.IsNotNull(pet);
                Assert.AreEqual(PetitionStatus.執行済, pet.status);

                // 2回目の裁可は弾かれ、効果も再適用されない
                prov.governancePolicy = GovernancePolicy.民生;
                Assert.IsFalse(DecisionDeck.Resolve(decisionId, 0), "決裁済みは再解決できない");
                Assert.AreEqual(1, resolvedCount, "二重に Resolved が届かない");
                Assert.AreEqual(GovernancePolicy.民生, prov.governancePolicy, "効果は1回だけ");
            }
            finally { DecisionDeck.Resolved -= counter; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Governance_Decline_LeavesPolicyAndAllowsResubmit()
        {
            Province prov = SetupGovernance();
            var dir = NewDirector();
            yield return null;

            int decisionId = SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.弾圧);
            Assert.GreaterOrEqual(decisionId, 0);
            PendingDecision d = FindDecision(decisionId);

            Assert.IsTrue(DecisionDeck.Resolve(decisionId, 1)); // 見送る
            Assert.AreEqual(GovernancePolicy.民生, prov.governancePolicy, "見送りは統治政策を変えない");
            Assert.IsTrue(d.applied, "見送りも処理済みとして記録される");
            Assert.AreEqual(PetitionActionOutcome.対象外, d.outcome);
            Assert.AreEqual(PetitionStatus.却下, RingiDirector.Ledger.Get(d.petitionId).status);

            // 決着後は同じ星系へ改めて上申できる（重複扱いが残らない）
            Assert.GreaterOrEqual(SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.弾圧), 0, "見送り後の再上申が止まった");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Governance_DuplicateForSameSystem_IsRejectedWhilePending()
        {
            Province prov = SetupGovernance();
            var dir = NewDirector();
            yield return null;

            int decisionId = SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.動員);
            Assert.GreaterOrEqual(decisionId, 0);
            int cards = DecisionDeck.Queue.items.Count;

            // 未決の間は同じ星系への上申を積まない（政策が違っても）
            Assert.Less(dir.SubmitGovernancePolicy(GovSystemId, "テスト星系", Faction.同盟, GovernancePolicy.動員), 0);
            Assert.Less(dir.SubmitGovernancePolicy(GovSystemId, "テスト星系", Faction.同盟, GovernancePolicy.解放), 0);
            Assert.AreEqual(cards, DecisionDeck.Queue.items.Count, "重複上申でカードが増えない");
            Assert.AreEqual(GovernancePolicy.民生, prov.governancePolicy);
            yield return null;
        }

        /// <summary>
        /// 決裁待ちの間に対象星系が他勢力のものになった／地図に無い場合、GalaxyView が無くても
        /// <see cref="StrategySession.Map"/> で弾き、統治政策を変えない（失敗として記録し、稟議は閉じる）。
        /// </summary>
        [UnityTest]
        public IEnumerator Governance_TargetNowForeignOrMissing_FailsWithoutChange()
        {
            Province prov = SetupGovernance();
            var dir = NewDirector();
            yield return null;

            // ①所有が変わった
            int id1 = SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.動員);
            Assert.GreaterOrEqual(id1, 0);
            StrategySession.Map.GetSystem(GovSystemId).owner = Faction.帝国;
            Assert.IsTrue(DecisionDeck.Resolve(id1, 0));
            PendingDecision d1 = FindDecision(id1);
            Assert.AreEqual(GovernancePolicy.民生, prov.governancePolicy, "管轄外の星系の政策を変えてはいけない");
            Assert.IsTrue(d1.applied);
            Assert.AreEqual(PetitionActionOutcome.対象なし, d1.outcome);
            Assert.AreEqual(PetitionStatus.執行済, RingiDirector.Ledger.Get(d1.petitionId).status, "稟議は閉じる（在庫を占有させない）");

            // ②地図に対象が無い（「確認できないので通す」にしない）
            StrategySession.Map.GetSystem(GovSystemId).owner = Faction.同盟;
            int id2 = SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.動員);
            Assert.GreaterOrEqual(id2, 0);
            StrategySession.Map = new GalaxyMap();
            Assert.IsTrue(DecisionDeck.Resolve(id2, 0));
            PendingDecision d2 = FindDecision(id2);
            Assert.AreEqual(GovernancePolicy.民生, prov.governancePolicy, "地図に無い星系へは執行しない");
            Assert.IsTrue(d2.applied);
            Assert.AreEqual(PetitionActionOutcome.対象なし, d2.outcome);
            yield return null;
        }

        // ===== 台帳の取り違え・旧セーブのカード =====

        private static PendingDecision MakeCard(int id, string effectKey, int petitionId)
        {
            var d = new PendingDecision(id, "テスト決裁", DecisionSeverity.通常, DecisionSource.建白結果,
                                        effectKey, defaultChoiceIndex: 1);
            d.choices.Add("裁可する");
            d.choices.Add("見送る（現状維持）");
            d.petitionId = petitionId;
            d.friction = 0.4f;
            return d;
        }

        /// <summary>
        /// 税と編制の台帳は id を別々に採番する＝同じ id を指す別種のカードで他方の稟議を執行しない。
        /// 台帳に無い稟議を指すカード（旧セーブ）は効果を出さず失敗として記録し、ロード時の予約で新しい稟議と id が重ならない。
        /// </summary>
        [UnityTest]
        public IEnumerator OrphanOrOtherLedgerPetitionId_IsNotExecuted()
        {
            SetupGovernance(); // AuthorityCheck=null の固定と static の退避を兼ねる
            FactionState fs = StrategySession.Campaign.states[0];
            NewDirector();
            NewFleetDirector();
            yield return null;
            float taxBefore = fs.taxRate;

            var taxPet = new Petition(0, "減税の建白", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            Assert.IsTrue(RingiPipeline.Submit(RingiDirector.Ledger, taxPet));
            RingiPipeline.SendToDecision(taxPet);

            // ①同じ id を指す編制カード（編制の台帳には無い）
            PendingDecision fleetCard = MakeCard(DecisionDeck.NextDecisionId(85000), "fleet.establish:12000", taxPet.id);
            DecisionDeck.Enqueue(fleetCard);
            Assert.IsTrue(DecisionDeck.Resolve(fleetCard.id, 0));
            Assert.AreEqual(taxBefore, fs.taxRate, 1e-4f, "編制カードで税の稟議が執行された");
            Assert.AreEqual(PetitionStatus.決裁待ち, taxPet.status, "他方の台帳の稟議の状態を動かしてはいけない");
            Assert.IsTrue(fleetCard.applied, "黙って終わらず処理済みとして記録される");
            Assert.AreEqual(PetitionActionOutcome.対象なし, fleetCard.outcome);

            // ②台帳に無い稟議を指す税カード（旧セーブ）
            int orphanPetitionId = taxPet.id + 50;
            PendingDecision orphan = MakeCard(DecisionDeck.NextDecisionId(80000), "tax.cut", orphanPetitionId);
            DecisionDeck.Enqueue(orphan);
            Assert.IsTrue(DecisionDeck.Resolve(orphan.id, 0));
            Assert.AreEqual(taxBefore, fs.taxRate, 1e-4f, "稟議の無いカードで効果が出た");
            Assert.IsTrue(orphan.applied);
            Assert.AreEqual(PetitionActionOutcome.対象なし, orphan.outcome);

            // ③ロード時と同じ予約で、新しい稟議は古いカードの id を使わない
            CampaignSerializer.ReservePetitionIds(DecisionDeck.Queue, RingiDirector.Ledger);
            var fresh = new Petition(0, "新しい建白", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            Assert.IsTrue(RingiPipeline.Submit(RingiDirector.Ledger, fresh));
            Assert.Greater(fresh.id, orphanPetitionId);
            yield return null;
        }

        // ===== セーブ→新しい static→ロード→裁可（#稟議完成②の保存が実際に効くか） =====

        // 利用者のセーブの退避（バイト列と有無をそのまま戻す）
        private bool saveBackedUp;
        private bool saveExisted;
        private byte[] saveBytes;

        private void BackupSaveFile()
        {
            if (saveBackedUp) return;
            string path = CampaignSaveManager.SaveFilePath;
            saveExisted = File.Exists(path);
            saveBytes = saveExisted ? File.ReadAllBytes(path) : null;
            saveBackedUp = true;
        }

        private void RestoreSaveFile()
        {
            if (!saveBackedUp) return;
            string path = CampaignSaveManager.SaveFilePath;
            if (saveExisted) File.WriteAllBytes(path, saveBytes);
            else if (File.Exists(path)) File.Delete(path);
            saveBackedUp = false;
            saveBytes = null;
        }

        // StrategySession の退避（ロードが丸ごと差し替えるため）
        private bool sessionSaved;
        private GalaxyMap sMap;
        private StrategicFleetRegistry sReg;
        private Dictionary<int, Province> sProvinces;
        private GameClock sClock;
        private WarpReinforcementLedger sReinforcements;
        private PetitionLedger sPetitions, sFleetPetitions;
        private DecisionQueue sDecisions;
        private List<Person> sPendingPeople;
        private ProtagonistCareerSave sCareer;
        private CourtAuthority sCourt;

        private void SnapshotSession()
        {
            sMap = StrategySession.Map; sReg = StrategySession.Reg; sProvinces = StrategySession.Provinces;
            sClock = StrategySession.Clock; sReinforcements = StrategySession.Reinforcements;
            sPetitions = StrategySession.Petitions; sFleetPetitions = StrategySession.FleetPetitions;
            sDecisions = StrategySession.Decisions; sPendingPeople = StrategySession.PendingPeople;
            sCareer = StrategySession.PendingProtagonistCareer; sCourt = StrategySession.CourtAuthority;
            sessionSaved = true;
        }

        private void RestoreSession()
        {
            if (!sessionSaved) return;
            StrategySession.Map = sMap; StrategySession.Reg = sReg; StrategySession.Provinces = sProvinces;
            StrategySession.Clock = sClock; StrategySession.Reinforcements = sReinforcements;
            StrategySession.Petitions = sPetitions; StrategySession.FleetPetitions = sFleetPetitions;
            StrategySession.Decisions = sDecisions; StrategySession.PendingPeople = sPendingPeople;
            StrategySession.PendingProtagonistCareer = sCareer; StrategySession.CourtAuthority = sCourt;
            StrategySession.Campaign = null;
            sessionSaved = false;
        }

        // 艦隊プール・台帳の退避（編制の執行が動かす）
        private bool fleetStaticsSaved;
        private int savedPool;
        private readonly HashSet<int> fleetNumbersBefore = new HashSet<int>();

        private void PrepareFleetPool(int available)
        {
            savedPool = FleetPool.Get(Faction.同盟);
            fleetNumbersBefore.Clear();
            foreach (FleetUnitData u in FleetRoster.AllFleets(Faction.同盟))
                if (u != null) fleetNumbersBefore.Add(u.fleetNumber);
            fleetStaticsSaved = true;
            FleetPool.Set(Faction.同盟, FleetPoolRules.Allocated(Faction.同盟) + available);
        }

        private void RestoreFleetStatics()
        {
            if (!fleetStaticsSaved) return;
            foreach (FleetUnitData u in FleetRoster.AllFleets(Faction.同盟))
                if (u != null && u.IsActive && !fleetNumbersBefore.Contains(u.fleetNumber))
                    FleetRoster.Disband(Faction.同盟, u.fleetNumber); // テストで設立した艦隊を畳む
            FleetPool.Set(Faction.同盟, savedPool);
            fleetStaticsSaved = false;
        }

        private static int CountActiveFleets(Faction f)
        {
            int n = 0;
            foreach (FleetUnitData u in FleetRoster.AllFleets(f))
                if (u != null && u.IsActive) n++;
            return n;
        }

        /// <summary>編制の建議を決裁デスクへ浮上するまで繰り返す。決裁id（&lt;0=失敗）。</summary>
        private static int RaiseFleetUntilSurfaced(FleetRingiDirector dir, int maxTries = 400)
        {
            for (int i = 0; i < maxTries; i++)
            {
                int before = DecisionDeck.Queue.items.Count;
                dir.ForcePlayerReview();
                for (int k = before; k < DecisionDeck.Queue.items.Count; k++)
                {
                    PendingDecision d = DecisionDeck.Queue.items[k];
                    if (d != null && d.effectKey != null &&
                        d.effectKey.StartsWith(FleetEstablishmentRules.EffectEstablish, System.StringComparison.Ordinal))
                        return d.id;
                }
            }
            return -1;
        }

        /// <summary>
        /// ★実ファイルの <see cref="CampaignSaveManager.SaveSession"/> → 新しい static の世界 →
        /// タイトル「再開」と同じ <see cref="GalaxyView.ContinueCampaignFromSave"/>（リセット→ロード）→ Director を作り直す
        /// → 復元したカード（統治政策＝未決／編制＝保留中）を裁可すると、効果がちょうど1回出る。
        /// </summary>
        [UnityTest]
        public IEnumerator SaveLoad_RestoredGovernanceAndFleetCards_ResolveExactlyOnce()
        {
            SnapshotSession();
            Province prov = SetupGovernance();
            FactionState fs = StrategySession.Campaign.states[0];
            CredibilityRules.Adjust(fs.credibility, BoxKind.国王, 1f); // 編制の建議（国王箱）を浮上しやすく
            PrepareFleetPool(20000);
            RingiDirector dir = NewDirector();
            FleetRingiDirector fleetDir = NewFleetDirector();
            yield return null;

            Assert.AreSame(StrategySession.Petitions, RingiDirector.Ledger, "税・統治の台帳は保存される入れ物そのもの");
            Assert.AreSame(StrategySession.FleetPetitions, FleetRingiDirector.Ledger, "編制の台帳は保存される入れ物そのもの");

            int govCardId = SubmitGovernanceUntilSurfaced(dir, GovernancePolicy.動員);
            Assert.GreaterOrEqual(govCardId, 0, "統治政策の上申が浮上しなかった");
            int fleetCardId = RaiseFleetUntilSurfaced(fleetDir);
            Assert.GreaterOrEqual(fleetCardId, 0, "編制の建議が浮上しなかった");

            PendingDecision govCard = FindDecision(govCardId);
            PendingDecision fleetCard = FindDecision(fleetCardId);
            DecisionDeck.Queue.Minimize(fleetCard); // 保留（最小化）のまま保存する
            int govPetId = govCard.petitionId;
            int fleetPetId = fleetCard.petitionId;
            string govKey = govCard.effectKey;
            string fleetKey = fleetCard.effectKey;
            Assert.IsNotNull(StrategySession.Petitions.Get(govPetId));
            Assert.IsNotNull(StrategySession.FleetPetitions.Get(fleetPetId));

            // --- 実ファイルへ保存（利用者のセーブは退避。ロード直後と TearDown の両方で戻す） ---
            BackupSaveFile();
            CampaignSaveManager.SaveSession(StrategySession.Campaign, null, null, StrategySession.Clock, StrategySession.Provinces);

            // --- Director を捨て、static を別の戦役の残骸で汚してから、再開と同じ順でリセット→ロード ---
            Object.Destroy(directorGo); directorGo = null;
            Object.Destroy(fleetDirectorGo); fleetDirectorGo = null;
            yield return null; // OnDestroy（購読解除）を確定
            StrategySession.Clear();
            StrategySession.Petitions.Add(new Petition(0, "前の戦役の残り", Faction.同盟, BoxKind.政治家,
                                                       PetitionOrigin.建白, "stale.leftover"));
            const int StaleCardId = 70001;
            DecisionDeck.Enqueue(MakeCard(StaleCardId, "tax.cut", 1));

            bool loaded;
            try { loaded = GalaxyView.ContinueCampaignFromSave(); }
            finally { RestoreSaveFile(); }
            Assert.IsTrue(loaded, "セーブを読めなかった");

            dir = NewDirector();
            fleetDir = NewFleetDirector();
            yield return null;

            // --- 復元の確認 ---
            Assert.AreSame(StrategySession.Petitions, RingiDirector.Ledger);
            Assert.AreSame(StrategySession.FleetPetitions, FleetRingiDirector.Ledger);
            Assert.IsNull(FindDecision(StaleCardId), "前の戦役のカードが残っている");
            foreach (Petition p in StrategySession.Petitions.items)
                Assert.AreNotEqual("stale.leftover", p != null ? p.effectKey : null, "前の戦役の稟議が残っている");

            Petition govPet = StrategySession.Petitions.Get(govPetId);
            Petition fleetPet = StrategySession.FleetPetitions.Get(fleetPetId);
            Assert.IsNotNull(govPet, "統治政策の稟議が復元されていない（リセットで消えた/保存されていない）");
            Assert.IsNotNull(fleetPet, "編制の稟議が復元されていない");
            Assert.AreEqual(PetitionStatus.決裁待ち, govPet.status);
            Assert.AreEqual(PetitionStatus.決裁待ち, fleetPet.status);
            Assert.AreEqual(govKey, govPet.effectKey);
            Assert.AreEqual(fleetKey, fleetPet.effectKey);

            govCard = FindDecision(govCardId);
            fleetCard = FindDecision(fleetCardId);
            Assert.IsNotNull(govCard, "統治政策のカードが復元されていない");
            Assert.IsNotNull(fleetCard, "編制のカードが復元されていない");
            Assert.IsFalse(govCard.applied);
            Assert.IsFalse(DecisionResolutionRules.IsSettled(govCard));
            Assert.AreEqual(DecisionStatus.最小化, fleetCard.status, "保留（最小化）が保たれる");
            Assert.IsFalse(fleetCard.applied);

            Province restored = StrategySession.Provinces[GovSystemId];
            Assert.AreNotSame(prov, restored, "ロードした Province で確認する");
            Assert.AreEqual(GovernancePolicy.民生, restored.governancePolicy);
            Assert.IsNotNull(StrategySession.Map.GetSystem(GovSystemId));

            // 採番の続き（ロード後の新しい稟議・カードが既存と重ならない）
            Assert.Greater(StrategySession.Petitions.NextId(), govPetId);
            Assert.Greater(StrategySession.FleetPetitions.NextId(), fleetPetId);
            Assert.Greater(DecisionDeck.NextDecisionId(80000), Mathf.Max(govCardId, fleetCardId));

            // --- 裁可：効果はそれぞれちょうど1回 ---
            int activeBefore = CountActiveFleets(Faction.同盟);
            int resolvedGov = 0, resolvedFleet = 0;
            System.Action<PendingDecision, int> counter = (pd, _) =>
            {
                if (pd == null) return;
                if (pd.id == govCardId) resolvedGov++;
                else if (pd.id == fleetCardId) resolvedFleet++;
            };
            DecisionDeck.Resolved += counter;
            try
            {
                Assert.IsTrue(DecisionDeck.Resolve(govCardId, 0), "復元した統治政策カードを裁可できない");
                Assert.AreEqual(1, resolvedGov);
                Assert.AreEqual(GovernancePolicy.動員, restored.governancePolicy, "ロード後の裁可で統治政策が変わらない");
                Assert.IsTrue(govCard.applied);
                Assert.AreEqual(PetitionActionOutcome.実行, govCard.outcome);
                Assert.AreEqual(PetitionStatus.執行済, govPet.status);

                Assert.IsTrue(DecisionDeck.Resolve(fleetCardId, 0), "復元した保留中の編制カードを裁可できない");
                Assert.AreEqual(1, resolvedFleet);
                Assert.AreEqual(activeBefore + 1, CountActiveFleets(Faction.同盟), "ロード後の裁可で艦隊が設立されない");
                Assert.IsTrue(fleetCard.applied);
                Assert.AreEqual(PetitionActionOutcome.実行, fleetCard.outcome);
                Assert.AreEqual(PetitionStatus.執行済, fleetPet.status);

                // 2回目は弾かれ、効果も出ない
                restored.governancePolicy = GovernancePolicy.民生;
                Assert.IsFalse(DecisionDeck.Resolve(govCardId, 0));
                Assert.IsFalse(DecisionDeck.Resolve(fleetCardId, 0));
                Assert.AreEqual(1, resolvedGov);
                Assert.AreEqual(1, resolvedFleet);
                Assert.AreEqual(GovernancePolicy.民生, restored.governancePolicy, "統治政策の効果が2回出た");
                Assert.AreEqual(activeBefore + 1, CountActiveFleets(Faction.同盟), "艦隊が2回設立された");
            }
            finally { DecisionDeck.Resolved -= counter; }
            yield return null;
        }

        private RingiDirector NewDirector()
        {
            directorGo = new GameObject("RingiDirector_Test");
            return directorGo.AddComponent<RingiDirector>(); // Awake が DecisionDeck.Resolved を購読
        }

        private FleetRingiDirector NewFleetDirector()
        {
            fleetDirectorGo = new GameObject("FleetRingiDirector_Test");
            return fleetDirectorGo.AddComponent<FleetRingiDirector>();
        }

        /// <summary>サンプルを浮上するまで起案し続ける（生存ロールは確率的なのでリトライ）。決裁id（&lt;0=失敗）。</summary>
        private static int RaiseUntilSurfaced(RingiDirector dir, int sampleIndex, int maxTries = 400)
        {
            for (int i = 0; i < maxTries; i++)
            {
                int id = dir.ForceRaise(sampleIndex);
                if (id >= 0) return id;
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator Approve_TaxCut_MovesWorld()
        {
            var fs = SetupCampaign();
            var dir = NewDirector();
            yield return null; // Awake/購読を確定

            float before = fs.taxRate; // 0.3
            int decisionId = RaiseUntilSurfaced(dir, sampleIndex: 0); // 減税
            Assert.GreaterOrEqual(decisionId, 0, "建白が決裁デスクまで浮上しなかった");

            Assert.IsTrue(DecisionDeck.Resolve(decisionId, 0), "決裁できなかった"); // 裁可
            Assert.Less(fs.taxRate, before, "裁可で税率が下がるはず（官僚に骨抜きされつつも世界が動く）");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Decline_TaxCut_LeavesWorldUnchanged()
        {
            var fs = SetupCampaign();
            var dir = NewDirector();
            yield return null;

            float before = fs.taxRate;
            int decisionId = RaiseUntilSurfaced(dir, sampleIndex: 0);
            Assert.GreaterOrEqual(decisionId, 0);

            Assert.IsTrue(DecisionDeck.Resolve(decisionId, 1)); // 見送る（現状維持）
            Assert.AreEqual(before, fs.taxRate, 1e-4f, "見送りは世界を動かさない");
            yield return null;
        }
    }
}
