using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 精鋭部隊（少数精鋭・質的優位）の純ロジック検証。既定 Params で期待値固定。
    /// </summary>
    public class EliteUnitRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow（代替不能性）箇所のみ緩める

        [Test]
        public void CombatMultiplier_ScalesWithTier_RespectsFloor()
        {
            // 最精鋭＝基準+combatBonusScale(1.0)。
            Assert.AreEqual(2.0f, EliteUnitRules.CombatMultiplier(1.0f), Eps);
            // 中位。
            Assert.AreEqual(1.5f, EliteUnitRules.CombatMultiplier(0.5f), Eps);
            // 精鋭度0でも下限 minCombatMultiplier(1.1)＝それでも基準以上。
            Assert.AreEqual(1.1f, EliteUnitRules.CombatMultiplier(0f), Eps);
        }

        [Test]
        public void ShockValue_AmplifiedBySurprise()
        {
            // 奇襲なし＝戦闘力倍率そのまま。
            Assert.AreEqual(2.0f, EliteUnitRules.ShockValue(2.0f, 0f), Eps);
            // 完全奇襲＝surpriseShockScale(2.0)倍。
            Assert.AreEqual(4.0f, EliteUnitRules.ShockValue(2.0f, 1.0f), Eps);
            // 半奇襲＝中間（1.5倍）。
            Assert.AreEqual(3.0f, EliteUnitRules.ShockValue(2.0f, 0.5f), Eps);
        }

        [Test]
        public void BreakthroughPower_DividesByEnemyLine()
        {
            // 兵力10×倍率2.0=20、防御線4で割る＝5.0。
            Assert.AreEqual(5.0f, EliteUnitRules.BreakthroughPower(10f, 2.0f, 4f), Eps);
            // 防御線なし＝実効打撃力そのまま。
            Assert.AreEqual(20f, EliteUnitRules.BreakthroughPower(10f, 2.0f, 0f), Eps);
            // 薄い線ほど突破しやすい（線2 > 線4）。
            Assert.Greater(EliteUnitRules.BreakthroughPower(10f, 2.0f, 2f),
                           EliteUnitRules.BreakthroughPower(10f, 2.0f, 4f));
        }

        [Test]
        public void SpecialOpsSuccess_HighTierLowDifficulty()
        {
            // 最精鋭・無難度＝満点。
            Assert.AreEqual(1.0f, EliteUnitRules.SpecialOpsSuccess(1.0f, 0f), Eps);
            // 中位精鋭・中難度＝(0.2+0.4)*0.5=0.3。
            Assert.AreEqual(0.3f, EliteUnitRules.SpecialOpsSuccess(0.5f, 0.5f), Eps);
            // 練度0・最高難度＝0。
            Assert.AreEqual(0f, EliteUnitRules.SpecialOpsSuccess(0f, 1.0f), Eps);
        }

        [Test]
        public void IrreplaceabilityCost_NonlinearInLoss()
        {
            // 全損＝irreplaceabilityScale(3.0)。
            Assert.AreEqual(3.0f, EliteUnitRules.IrreplaceabilityCost(1.0f), PowEps);
            // 半損は指数2で 0.25*3.0=0.75（線形なら1.5＝非線形を確認）。
            Assert.AreEqual(0.75f, EliteUnitRules.IrreplaceabilityCost(0.5f), PowEps);
            // 無損＝0。
            Assert.AreEqual(0f, EliteUnitRules.IrreplaceabilityCost(0f), Eps);
        }

        [Test]
        public void MoraleAnchor_LiftsSurroundingMorale()
        {
            // 精鋭在席で士気0.5を底上げ（0.5+0.4*0.5=0.7）。
            Assert.AreEqual(0.7f, EliteUnitRules.MoraleAnchor(1.0f, 0.5f), Eps);
            // 精鋭不在＝周囲士気のまま。
            Assert.AreEqual(0.5f, EliteUnitRules.MoraleAnchor(0f, 0.5f), Eps);
            // 既に満士気は底上げ余地なし（1.0でクランプ）。
            Assert.AreEqual(1.0f, EliteUnitRules.MoraleAnchor(1.0f, 1.0f), Eps);
        }

        [Test]
        public void OverrelianceRisk_HighWhenWeakRegulars()
        {
            // 全面依存・通常部隊質0＝最大リスク。
            Assert.AreEqual(1.0f, EliteUnitRules.OverrelianceRisk(1.0f, 0f), Eps);
            // 通常部隊が精強なら依存しても無害。
            Assert.AreEqual(0f, EliteUnitRules.OverrelianceRisk(1.0f, 1.0f), Eps);
            // 中庸＝0.25。
            Assert.AreEqual(0.25f, EliteUnitRules.OverrelianceRisk(0.5f, 0.5f), Eps);
        }

        [Test]
        public void Narrative_ElitePiercesAndAnchors_ButLossIsIrreplaceable()
        {
            // ローゼンリッター型：少数精鋭が決定点へ奇襲で投入される。
            float mult = EliteUnitRules.CombatMultiplier(0.9f); // 高練度＝1.9倍
            float shock = EliteUnitRules.ShockValue(mult, 0.8f); // 奇襲で衝撃増幅
            Assert.Greater(shock, mult); // 不意打ちは正面攻撃より重い

            // 薄い一点（防御線3）に投入し突破口を開く。
            float power = EliteUnitRules.BreakthroughPower(8f, mult, 3f);
            Assert.IsTrue(EliteUnitRules.IsEliteDecisive(power, 4f)); // 局面を決した
            Assert.IsFalse(EliteUnitRules.IsEliteDecisive(power, 100f)); // 過大な要求には届かない

            // 要塞内部奪取（高難度の特殊作戦）も高い練度なら成算がある。
            float ops = EliteUnitRules.SpecialOpsSuccess(0.9f, 0.6f);
            Assert.Greater(ops, 0.2f);

            // 戦線では精鋭が周囲の動揺した士気（0.3）を支える＝戦意の核。
            float anchored = EliteUnitRules.MoraleAnchor(1.0f, 0.3f);
            Assert.Greater(anchored, 0.3f);

            // だが精鋭を半分失うと代替が利かず、損失感は線形比例より重い。
            float halfLoss = EliteUnitRules.IrreplaceabilityCost(0.5f);
            float linearHalf = EliteUnitRules.IrreplaceabilityCost(1.0f) * 0.5f;
            Assert.Less(halfLoss, linearHalf); // 0.75 < 1.5 ＝非線形で序盤の損失が軽く見えても全損は跳ね上がる

            // 精鋭頼みで通常部隊（質0.2）が育たぬリスクも残る。
            float risk = EliteUnitRules.OverrelianceRisk(0.9f, 0.2f);
            Assert.Greater(risk, 0.5f);
        }
    }
}
