using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>損耗補充の流れ（消耗部隊への新兵/新造艦補充と練度希釈）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class ReplacementFlowRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 補充供給率は需要と効率で流れるがプールに頭打ち()
        {
            // 需要5×効率0.8=4、プール10で足りる→4。
            Assert.AreEqual(4f, ReplacementFlowRules.ReplacementRate(10f, 5f), Eps);
            // プール2しか無ければ需要4でも頭打ち→2。
            Assert.AreEqual(2f, ReplacementFlowRules.ReplacementRate(2f, 5f), Eps);
        }

        [Test]
        public void 頭数の回復は供給率と時間の積()
        {
            // 供給率4×経過3=12。
            Assert.AreEqual(12f, ReplacementFlowRules.StrengthRecovery(100f, 4f, 3f), Eps);
            // 負の dt は0。
            Assert.AreEqual(0f, ReplacementFlowRules.StrengthRecovery(100f, 4f, -1f), Eps);
        }

        [Test]
        public void 補充が混じると平均練度が薄まる_補充なしは不変()
        {
            // 古参60(練度0.8)に練度0の補充40 → 60*0.8/100 = 0.48。
            Assert.AreEqual(0.48f, ReplacementFlowRules.VeterancyDilution(0.8f, 60f, 40f), Eps);
            // 補充0なら希釈なし → 0.8。
            Assert.AreEqual(0.8f, ReplacementFlowRules.VeterancyDilution(0.8f, 60f, 0f), Eps);
            // 母数0なら0。
            Assert.AreEqual(0f, ReplacementFlowRules.VeterancyDilution(0.8f, 0f, 0f), Eps);
        }

        [Test]
        public void 統合時間は補充が多いほど長く結束が高いほど短い()
        {
            // 補充10×基準2×(1-0.5*1.0)=10（結束満点で半減）。
            Assert.AreEqual(10f, ReplacementFlowRules.IntegrationTime(10f, 1.0f), Eps);
            // 結束0なら満額 → 10*2*1 = 20。
            Assert.AreEqual(20f, ReplacementFlowRules.IntegrationTime(10f, 0.0f), Eps);
        }

        [Test]
        public void 補充不足は需要に対する不足比_供給充足なら0()
        {
            // (需要10-供給4)/10 = 0.6。
            Assert.AreEqual(0.6f, ReplacementFlowRules.ReplacementShortfall(10f, 4f), Eps);
            // 供給が需要以上なら不足なし。
            Assert.AreEqual(0f, ReplacementFlowRules.ReplacementShortfall(10f, 10f), Eps);
            // 需要0なら不足なし。
            Assert.AreEqual(0f, ReplacementFlowRules.ReplacementShortfall(0f, 0f), Eps);
        }

        [Test]
        public void 補充不足が続くと部隊が空洞化_不足なしは進まない()
        {
            // 不足0.5×速度0.5×期間2 = 0.5。
            Assert.AreEqual(0.5f, ReplacementFlowRules.UnitHollowing(0.5f, 2f), Eps);
            // 不足0なら空洞化しない。
            Assert.AreEqual(0f, ReplacementFlowRules.UnitHollowing(0f, 10f), Eps);
        }

        [Test]
        public void 幹部を残すと再建が速い_母数0は0()
        {
            // 古参30/総100=0.3 × 幹部の効き0.5 = 0.15。
            Assert.AreEqual(0.15f, ReplacementFlowRules.CadrePreservation(30f, 100f), Eps);
            // 母数0なら0。
            Assert.AreEqual(0f, ReplacementFlowRules.CadrePreservation(0f, 0f), Eps);
        }

        [Test]
        public void 戦力化は頭数回復と統合進捗のしきい値で判定()
        {
            // 経過6/統合10=0.6 ≥ しきい0.6 → 戦力化。
            Assert.IsTrue(ReplacementFlowRules.IsUnitCombatReady(5f, 10f, 6f));
            // 経過5/10=0.5 < 0.6 → 未だ。
            Assert.IsFalse(ReplacementFlowRules.IsUnitCombatReady(5f, 10f, 5f));
            // 頭数の回復が無ければ未戦力。
            Assert.IsFalse(ReplacementFlowRules.IsUnitCombatReady(0f, 10f, 100f));
        }

        [Test]
        public void 物語_消耗部隊へ補充を流すと頭数は戻るが練度が薄まり追いつかぬと枯れる()
        {
            // 会戦で消耗した古参部隊：古参40・現有兵力40（半分が溶けた）。需要は10/秒。
            // プールが薄い(供給6しか出ない)→ 供給率6。
            float rate = ReplacementFlowRules.ReplacementRate(6f, 10f); // min(10*0.8=8, 6)=6
            Assert.AreEqual(6f, rate, Eps);

            // 5秒流すと頭数は30回復（40→70へ）。
            float recovered = ReplacementFlowRules.StrengthRecovery(40f, rate, 5f);
            Assert.AreEqual(30f, recovered, Eps);

            // しかし補充30が混じり平均練度は薄まる：古参40(練度0.9) / (40+30) = 0.9*40/70。
            float dilutedVet = ReplacementFlowRules.VeterancyDilution(0.9f, 40f, 30f);
            Assert.AreEqual(0.9f * 40f / 70f, dilutedVet, Eps); // ≒0.5143
            Assert.Less(dilutedVet, 0.9f); // 確かに薄まった。

            // 補充が需要に追いつかない：(10-6)/10=0.4 の不足。
            float shortfall = ReplacementFlowRules.ReplacementShortfall(10f, rate);
            Assert.AreEqual(0.4f, shortfall, Eps);

            // 不足が続けば部隊は空洞化する：不足0.4×速度0.5×期間5 = 1.0（枯渇）。
            float hollow = ReplacementFlowRules.UnitHollowing(shortfall, 5f);
            Assert.AreEqual(1.0f, hollow, Eps);

            // だが古参40/総70の骨格があれば再建は速い：0.5714*0.5。
            float cadre = ReplacementFlowRules.CadrePreservation(40f, 70f);
            Assert.AreEqual((40f / 70f) * 0.5f, cadre, Eps);
            Assert.Greater(cadre, 0f);
        }
    }
}
