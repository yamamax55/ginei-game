using System.Collections;
using System.Collections.Generic;
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

        private static FactionState SetupCampaign()
        {
            DecisionDeck.Queue.items.Clear();   // 前テストの積み残しを掃く（id 衝突回避）
            RingiDirector.Ledger.Clear();
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
        private System.Func<PendingDecision, DecisionAuthorityResult> savedAuthorityCheck;
        private bool staticsSaved;

        [TearDown]
        public void TearDown()
        {
            if (directorGo != null) Object.Destroy(directorGo);
            StrategySession.Campaign = null;
            DecisionDeck.Queue.items.Clear();
            RingiDirector.Ledger.Clear();
            if (staticsSaved)
            {
                StrategySession.Provinces = savedProvinces;
                DecisionDeck.AuthorityCheck = savedAuthorityCheck;
                staticsSaved = false;
            }
        }

        // ===== 星系別統治政策の上申（#67/#109）＝SubmitGovernancePolicy → DecisionDeck.Resolve → OnResolved =====

        private const int GovSystemId = 7;

        /// <summary>
        /// 統治政策テストの前提：自勢力の1星系（民生）と、地方箱の信認を最大化した勢力。
        /// 権限判定は「誰でも裁可できる」前提に固定する（AuthorityCheck=null＝DecisionDeck の後方互換経路。
        /// 権限の上申経路は #67 側のテストが担う）。GalaxyView は置かない＝摩擦は既定値・所有チェックは省略される。
        /// </summary>
        private Province SetupGovernance()
        {
            FactionState fs = SetupCampaign();
            CredibilityRules.Adjust(fs.credibility, BoxKind.地方, 1f, GovSystemId.ToString());

            savedProvinces = StrategySession.Provinces;
            savedAuthorityCheck = DecisionDeck.AuthorityCheck;
            staticsSaved = true;
            DecisionDeck.AuthorityCheck = null;

            var prov = new Province(GovSystemId, "") { governancePolicy = GovernancePolicy.民生 };
            StrategySession.Provinces = new Dictionary<int, Province> { { GovSystemId, prov } };
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

        private RingiDirector NewDirector()
        {
            directorGo = new GameObject("RingiDirector_Test");
            return directorGo.AddComponent<RingiDirector>(); // Awake が DecisionDeck.Resolved を購読
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
