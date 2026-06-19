using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時経済／軍需転換（<see cref="WarEconomyRules"/>）の純ロジックテスト。既定
    /// <see cref="WarEconomyParams.Default"/> で期待値を固定（転換効率0.8・士気重み0.6・年疲弊0.1・
    /// 持続基準0.5・支持上積み0.3）。乗算の累積箇所のみ許容差を緩める。
    /// </summary>
    public class WarEconomyRulesTests
    {
        const float Eps = 1e-4f;
        const float MulEps = 1e-3f;

        [Test]
        public void WarConversion_ScalesCapacityByRateAndEfficiency()
        {
            // 100*0.5*0.8 = 40
            Assert.AreEqual(40f, WarEconomyRules.WarConversion(100f, 0.5f), MulEps);
        }

        [Test]
        public void CivilianHardship_EqualsConversionRate()
        {
            Assert.AreEqual(0.7f, WarEconomyRules.CivilianHardship(0.7f), Eps);
            // 範囲外はクランプ
            Assert.AreEqual(1f, WarEconomyRules.CivilianHardship(1.5f), Eps);
        }

        [Test]
        public void MilitaryOutput_ScalesConversionByIndustrialEfficiency()
        {
            // 40*0.9 = 36
            Assert.AreEqual(36f, WarEconomyRules.MilitaryOutput(40f, 0.9f), MulEps);
        }

        [Test]
        public void EconomicStrain_AccumulatesWithRateAndDuration()
        {
            // clamp01(0.5*4*0.1) = 0.2
            Assert.AreEqual(0.2f, WarEconomyRules.EconomicStrain(0.5f, 4f), MulEps);
            // 長期高転換は上限1に張り付く clamp01(1*30*0.1)=clamp01(3)=1
            Assert.AreEqual(1f, WarEconomyRules.EconomicStrain(1f, 30f), Eps);
        }

        [Test]
        public void MoraleCostFromHardship_IsNegativeAndScaled()
        {
            // -clamp01(0.5*0.6) = -0.3（士気喪失＝負値）
            Assert.AreEqual(-0.3f, WarEconomyRules.MoraleCostFromHardship(0.5f), MulEps);
            // 苦境ゼロならコストなし
            Assert.AreEqual(0f, WarEconomyRules.MoraleCostFromHardship(0f), Eps);
        }

        [Test]
        public void GunsVsButter_MapsRateToSignedTradeoff()
        {
            Assert.AreEqual(-1f, WarEconomyRules.GunsVsButter(0f), Eps);   // 全て民需
            Assert.AreEqual(0f, WarEconomyRules.GunsVsButter(0.5f), Eps);  // 拮抗
            Assert.AreEqual(1f, WarEconomyRules.GunsVsButter(1f), Eps);    // 全て軍需
        }

        [Test]
        public void SustainableConversion_RaisedBySupportLoweredByStrain()
        {
            // clamp01(0.5 + 0.5*0.3 - 0.2) = clamp01(0.45) = 0.45
            Assert.AreEqual(0.45f, WarEconomyRules.SustainableConversion(0.2f, 0.5f), MulEps);
            // 高疲弊は持続可能率を押し下げる clamp01(0.5 + 0 - 0.9) = 0
            Assert.AreEqual(0f, WarEconomyRules.SustainableConversion(0.9f, 0f), Eps);
        }

        [Test]
        public void IsOvermobilized_TrueWhenRateExceedsSustainablePlusThreshold()
        {
            // 0.7 > 0.45 + 0.1 = 0.55 → true（過剰動員）
            Assert.IsTrue(WarEconomyRules.IsOvermobilized(0.7f, 0.45f));
            // 0.5 > 0.55 は false（持続限界の許容内）
            Assert.IsFalse(WarEconomyRules.IsOvermobilized(0.5f, 0.45f));
        }

        [Test]
        public void Narrative_TotalWarBoostsArmsButErodesHomeFrontAndOvermobilizes()
        {
            // 高転換率0.7で総力戦＝軍需は増える（民需能力100→軍需能力56→兵器50.4）
            float conv = WarEconomyRules.WarConversion(100f, 0.7f);
            Assert.AreEqual(56f, conv, MulEps);
            float output = WarEconomyRules.MilitaryOutput(conv, 0.9f);
            Assert.AreEqual(50.4f, output, MulEps);

            // だが民生は圧迫され生活苦→戦争支持（士気）が削られる
            float hardship = WarEconomyRules.CivilianHardship(0.7f);
            Assert.AreEqual(0.7f, hardship, Eps);
            float moraleCost = WarEconomyRules.MoraleCostFromHardship(hardship);
            Assert.Less(moraleCost, 0f);                       // 士気は下がる
            Assert.AreEqual(-0.42f, moraleCost, MulEps);       // -clamp01(0.7*0.6)

            // 天秤は軍需側に傾く（+方向）
            Assert.Greater(WarEconomyRules.GunsVsButter(0.7f), 0f);

            // 5年の総力戦で経済が疲弊し、支持0.6でも持続可能率は0.7に届かない＝過剰動員で破綻寸前
            float strain = WarEconomyRules.EconomicStrain(0.7f, 5f);
            Assert.AreEqual(0.35f, strain, MulEps);            // clamp01(0.7*5*0.1)
            float sustainable = WarEconomyRules.SustainableConversion(strain, 0.6f);
            Assert.AreEqual(0.33f, sustainable, MulEps);       // clamp01(0.5+0.18-0.35)
            Assert.IsTrue(WarEconomyRules.IsOvermobilized(0.7f, sustainable));  // 0.7 > 0.43
        }
    }
}
