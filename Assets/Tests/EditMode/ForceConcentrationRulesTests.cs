using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 兵力集中：局所優勢比・集結時間・他方面の手薄・各個撃破潜在・質的効果（二乗則）・集めすぎリスク・好機の窓・決定的集中判定。
    /// 既定 Params で期待値固定（1e-4f／Pow 箇所のみ緩める）。
    /// </summary>
    public class ForceConcentrationRulesTests
    {
        [Test]
        public void LocalSuperiority_RatioAndParity()
        {
            Assert.AreEqual(3.0f, ForceConcentrationRules.LocalSuperiority(300f, 100f), 1e-4f); // 集中3:1
            Assert.AreEqual(1.0f, ForceConcentrationRules.LocalSuperiority(50f, 50f), 1e-4f);   // 拮抗1.0
            Assert.AreEqual(0f, ForceConcentrationRules.LocalSuperiority(-10f, 100f), 1e-4f);   // 負入力は0
        }

        [Test]
        public void ConcentrationTime_ScalesWithForcesAndDistance()
        {
            Assert.AreEqual(600f, ForceConcentrationRules.ConcentrationTime(200f, 3f), 1e-4f); // 200×3×1.0
            Assert.AreEqual(0f, ForceConcentrationRules.ConcentrationTime(0f, 5f), 1e-4f);     // 集める戦力0は即時
        }

        [Test]
        public void OtherFrontExposure_RisesWithFractionAndFronts()
        {
            Assert.AreEqual(0.6f, ForceConcentrationRules.OtherFrontExposure(0.9f, 3), 1e-4f); // 0.9×(2/3)
            Assert.AreEqual(0f, ForceConcentrationRules.OtherFrontExposure(0.5f, 1), 1e-4f);   // 単一戦線=他方面なし
        }

        [Test]
        public void DefeatInDetailPotential_NeedsSuperiorityAndDispersion()
        {
            Assert.AreEqual(0.5f, ForceConcentrationRules.DefeatInDetailPotential(3.0f, 0.5f), 1e-4f); // (3-1)→1.0 ×0.5
            Assert.AreEqual(0f, ForceConcentrationRules.DefeatInDetailPotential(1.0f, 0.8f), 1e-4f);   // 拮抗=各個撃破不可
        }

        [Test]
        public void MassEffect_SquareLaw()
        {
            Assert.AreEqual(9.0f, ForceConcentrationRules.MassEffect(3.0f), 1e-3f); // 3^2＝二乗則
            Assert.AreEqual(1.0f, ForceConcentrationRules.MassEffect(1.0f), 1e-3f); // 拮抗1.0
        }

        [Test]
        public void OverconcentrationRisk_AboveThreshold()
        {
            Assert.AreEqual(0.4f, ForceConcentrationRules.OverconcentrationRisk(0.9f), 1e-4f); // (0.9-0.7)×2.0
            Assert.AreEqual(0f, ForceConcentrationRules.OverconcentrationRisk(0.7f), 1e-4f);   // 閾値ちょうどは0
        }

        [Test]
        public void TimingVsConcentration_WindowOverTime()
        {
            Assert.AreEqual(0.5f, ForceConcentrationRules.TimingVsConcentration(600f, 300f), 1e-4f); // 300/600
            Assert.AreEqual(1.0f, ForceConcentrationRules.TimingVsConcentration(600f, 600f), 1e-4f); // 間に合う
            Assert.AreEqual(1.0f, ForceConcentrationRules.TimingVsConcentration(0f, 50f), 1e-4f);    // 集結時間0は即時
        }

        [Test]
        public void IsDecisivelyConcentrated_Threshold()
        {
            Assert.IsTrue(ForceConcentrationRules.IsDecisivelyConcentrated(3.0f));  // 3≥2
            Assert.IsFalse(ForceConcentrationRules.IsDecisivelyConcentrated(1.5f)); // 1.5<2
        }

        /// <summary>
        /// 物語：決定的な一点に集中して局所優勢を取り（3:1）分散した敵を各個撃破できる潜在を得るが、
        /// 集中に時間がかかり（集結時間が長い）他方面が手薄になり（脆弱性が高い）、集めすぎれば一撃で大損害を負う。
        /// </summary>
        [Test]
        public void Story_ConcentrateAtDecisivePointButExposeOtherFronts()
        {
            // 全戦力の9割を一点へ集める（3方面のうち1点へ集中）。
            float concentratedFraction = 0.9f;
            float concentrated = 270f;     // 集中側の戦力
            float enemyLocal = 90f;        // その点の敵局所戦力（敵は分散）

            float superiority = ForceConcentrationRules.LocalSuperiority(concentrated, enemyLocal);
            Assert.AreEqual(3.0f, superiority, 1e-4f); // 局所3:1

            // 決定的に集中できた＝閾値2:1 を超える。
            Assert.IsTrue(ForceConcentrationRules.IsDecisivelyConcentrated(superiority));

            // 分散した敵を各個撃破できる潜在が高い。
            float detail = ForceConcentrationRules.DefeatInDetailPotential(superiority, 0.8f);
            Assert.Greater(detail, 0.5f);

            // 質的効果は数の二乗的に効く（局所優勢が二乗で効く）。
            Assert.AreEqual(9.0f, ForceConcentrationRules.MassEffect(superiority), 1e-3f);

            // だが集中には時間がかかる（集結時間 > 0）。
            float gatherTime = ForceConcentrationRules.ConcentrationTime(180f, 4f);
            Assert.Greater(gatherTime, 0f);

            // その間、他方面（残り2戦線）が手薄になる。
            float exposure = ForceConcentrationRules.OtherFrontExposure(concentratedFraction, 3);
            Assert.AreEqual(0.6f, exposure, 1e-4f);

            // 集めすぎ（9割）は一撃で大損害を負うリスクを伴う（卵を一つの籠に）。
            float risk = ForceConcentrationRules.OverconcentrationRisk(concentratedFraction);
            Assert.AreEqual(0.4f, risk, 1e-4f);
            Assert.Greater(risk, 0f);
        }
    }
}
