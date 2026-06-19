using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 植民地開発（新天地の開拓と発展段階）を固定する：投資は環境の過酷さで目減りし、定着は投資・人・
    /// 暮らしの積、インフラは投資×人口×時間で逓増、自給はインフラ×資源で飽和、開発段階は両輪で進み、
    /// 本国依存は自給の補数、見返りは段階×資源で生じ、成立は自給と依存の閾値で決まる。クランプを担保。
    /// 既定＝過酷重み1/入植係数0.01/インフラ率0.001/時間冪0.5/自給スケール0.005/段階インフラ重み0.5/飽和インフラ200/見返り率0.1。
    /// </summary>
    public class ColonyDevelopmentRulesTests
    {
        private static readonly ColonyDevelopmentParams P = ColonyDevelopmentParams.Default;

        [Test]
        public void InitialInvestment_DiminishedByHostility()
        {
            // 過酷さ0なら満額、過酷さ1なら半減（命がけの開拓）
            Assert.AreEqual(1000f, ColonyDevelopmentRules.InitialInvestment(1000f, 0f, P), 1e-4f);
            Assert.AreEqual(500f, ColonyDevelopmentRules.InitialInvestment(1000f, 1f, P), 1e-4f);
            // 過酷なほど目減り（単調減）
            Assert.Less(ColonyDevelopmentRules.InitialInvestment(1000f, 3f, P),
                        ColonyDevelopmentRules.InitialInvestment(1000f, 1f, P));
            // 負入力はクランプ
            Assert.AreEqual(0f, ColonyDevelopmentRules.InitialInvestment(-1000f, -1f, P), 1e-4f);
        }

        [Test]
        public void PopulationSettlement_RequiresInvestmentSettlersAndAmenities()
        {
            // 投資500×入植者寄与clamp01(100×0.01=1.0)×生活基盤0.5 = 250
            Assert.AreEqual(250f, ColonyDevelopmentRules.PopulationSettlement(500f, 100f, 0.5f, P), 1e-4f);
            // 入植者半分でも生活基盤満点なら同値（積のトレードオフ）
            Assert.AreEqual(250f, ColonyDevelopmentRules.PopulationSettlement(500f, 50f, 1f, P), 1e-4f);
            // 生活基盤0なら誰も根付かない（積）
            Assert.AreEqual(0f, ColonyDevelopmentRules.PopulationSettlement(500f, 100f, 0f, P), 1e-4f);
            // 入植者0なら定着なし／過大入力はクランプ（入植者寄与・生活基盤とも1.0で頭打ち＝500×1×1）
            Assert.AreEqual(0f, ColonyDevelopmentRules.PopulationSettlement(500f, 0f, 0.5f, P), 1e-4f);
            Assert.AreEqual(500f, ColonyDevelopmentRules.PopulationSettlement(500f, 9999f, 1.5f, P), 1e-4f);
        }

        [Test]
        public void InfrastructureGrowth_GrowsWithTimeButDecelerates()
        {
            // 0.001×250×250×√4 = 125
            Assert.AreEqual(125f, ColonyDevelopmentRules.InfrastructureGrowth(250f, 250f, 4f, P), 1e-3f);
            // √1 = 1 → 62.5
            Assert.AreEqual(62.5f, ColonyDevelopmentRules.InfrastructureGrowth(250f, 250f, 1f, P), 1e-3f);
            // 時間で逓増だが伸びは鈍る（冪<1）：1→4 の伸び < 4→16 でない＝整えるほど一手が小さい
            float d1 = ColonyDevelopmentRules.InfrastructureGrowth(250f, 250f, 2f, P)
                     - ColonyDevelopmentRules.InfrastructureGrowth(250f, 250f, 1f, P);
            float d2 = ColonyDevelopmentRules.InfrastructureGrowth(250f, 250f, 3f, P)
                     - ColonyDevelopmentRules.InfrastructureGrowth(250f, 250f, 2f, P);
            Assert.Greater(d1, d2); // 逓増（後半ほど伸びが小さい）
            // 人口0なら整わない／負入力クランプ
            Assert.AreEqual(0f, ColonyDevelopmentRules.InfrastructureGrowth(250f, 0f, 4f, P), 1e-4f);
            Assert.AreEqual(0f, ColonyDevelopmentRules.InfrastructureGrowth(-1f, -1f, -1f, P), 1e-4f);
        }

        [Test]
        public void SelfSufficiency_NeedsInfraAndResources()
        {
            // 0.005×√(125×100=12500≈111.803) ≈ 0.559017
            Assert.AreEqual(0.559017f, ColonyDevelopmentRules.SelfSufficiency(125f, 100f, P), 1e-3f);
            // インフラ・資源とも十分なら1.0で飽和（0.005×√40000=1.0）
            Assert.AreEqual(1f, ColonyDevelopmentRules.SelfSufficiency(200f, 200f, P), 1e-3f);
            // どちらか欠ければ自給できない（積）
            Assert.AreEqual(0f, ColonyDevelopmentRules.SelfSufficiency(0f, 100f, P), 1e-4f);
            Assert.AreEqual(0f, ColonyDevelopmentRules.SelfSufficiency(125f, 0f, P), 1e-4f);
        }

        [Test]
        public void DevelopmentStage_AdvancesWithBothInfraAndSufficiency()
        {
            // 0.5×clamp01(125/200=0.625) + 0.5×0.559017 = 0.3125+0.2795085 = 0.5920085
            Assert.AreEqual(0.5920085f, ColonyDevelopmentRules.DevelopmentStage(125f, 0.559017f, P), 1e-3f);
            // 前哨（インフラ・自給とも0）＝段階0
            Assert.AreEqual(0f, ColonyDevelopmentRules.DevelopmentStage(0f, 0f, P), 1e-4f);
            // 属州（インフラ飽和・自給満点）＝段階1
            Assert.AreEqual(1f, ColonyDevelopmentRules.DevelopmentStage(200f, 1f, P), 1e-3f);
            // インフラだけでは満ちない（自給0なら最大0.5止まり＝両輪）
            Assert.AreEqual(0.5f, ColonyDevelopmentRules.DevelopmentStage(400f, 0f, P), 1e-3f);
        }

        [Test]
        public void HomelandDependency_IsComplementOfSufficiency()
        {
            // 自給0.559017 → 依存0.440983
            Assert.AreEqual(0.440983f, ColonyDevelopmentRules.HomelandDependency(0.559017f), 1e-3f);
            // 完全自立＝依存0
            Assert.AreEqual(0f, ColonyDevelopmentRules.HomelandDependency(1f), 1e-4f);
            // 全くの未開＝依存1（本国頼り）
            Assert.AreEqual(1f, ColonyDevelopmentRules.HomelandDependency(0f), 1e-4f);
            // 過入力クランプ
            Assert.AreEqual(0f, ColonyDevelopmentRules.HomelandDependency(1.5f), 1e-4f);
        }

        [Test]
        public void EconomicReturn_DevelopedColoniesEnrichHomeland()
        {
            // 段階0.5920085×資源100×見返り率0.1 = 5.920085
            Assert.AreEqual(5.920085f, ColonyDevelopmentRules.EconomicReturn(0.5920085f, 100f, P), 1e-4f);
            // 前哨地（段階0）は見返りなし
            Assert.AreEqual(0f, ColonyDevelopmentRules.EconomicReturn(0f, 100f, P), 1e-4f);
            // 発展した植民地ほど本国を富ませる（単調増）
            Assert.Greater(ColonyDevelopmentRules.EconomicReturn(1f, 100f, P),
                           ColonyDevelopmentRules.EconomicReturn(0.5f, 100f, P));
            // 負・過大入力クランプ
            Assert.AreEqual(0f, ColonyDevelopmentRules.EconomicReturn(-1f, -1f, P), 1e-4f);
        }

        [Test]
        public void IsColonyViable_NeedsSufficiencyAndLowDependency()
        {
            // 自給0.559≥0.5 かつ 依存0.441<0.5 → 成立
            Assert.IsTrue(ColonyDevelopmentRules.IsColonyViable(0.559f, 0.441f));
            // 自給が閾値未満＝成り立たない（本国の支援が要る）
            Assert.IsFalse(ColonyDevelopmentRules.IsColonyViable(0.3f, 0.7f));
            // 完全自立＝成立
            Assert.IsTrue(ColonyDevelopmentRules.IsColonyViable(1f, 0f));
            // 閾値を厳しくすると同じ植民地でも不成立（撤退/支援継続の判断）
            Assert.IsFalse(ColonyDevelopmentRules.IsColonyViable(0.559f, 0.441f, 0.8f));
        }

        [Test]
        public void Params_CtorClamps()
        {
            var p = new ColonyDevelopmentParams(-1f, -0.01f, -0.001f, 1.5f, -0.005f, 2f, -10f, -0.1f);
            Assert.AreEqual(0f, p.hostilityWeight, 1e-5f);     // 非負
            Assert.AreEqual(0f, p.settlerWeight, 1e-5f);       // 非負
            Assert.AreEqual(0f, p.infraRate, 1e-5f);           // 非負
            Assert.AreEqual(1f, p.timeExponent, 1e-5f);        // 0..1
            Assert.AreEqual(0f, p.sufficiencyScale, 1e-5f);    // 非負
            Assert.AreEqual(1f, p.stageInfraWeight, 1e-5f);    // 0..1
            Assert.AreEqual(0.0001f, p.stageInfraNorm, 1e-5f); // 下限で0除算回避
            Assert.AreEqual(0f, p.returnRate, 1e-5f);          // 非負
        }

        [Test]
        public void LongRunStory_FrontierGrowsFromOutpostToSelfSufficientProvince()
        {
            // 過酷な辺境星を20年かけて開拓する（決定論シミュレート）。
            float capital = 2000f;
            float invest = ColonyDevelopmentRules.InitialInvestment(capital, 1f, P); // 過酷さ1で半減=1000
            float settlement = ColonyDevelopmentRules.PopulationSettlement(invest, 12f, 0.6f, P); // 1000×0.12×0.6=72
            float localResources = 50f;

            float earlyStage = -1f;
            float lateStage = -1f;
            bool earlyViable = true;
            bool lateViable = false;
            for (int year = 1; year <= 20; year++)
            {
                float infra = ColonyDevelopmentRules.InfrastructureGrowth(invest, settlement, year, P);
                float suff = ColonyDevelopmentRules.SelfSufficiency(infra, localResources, P);
                float stage = ColonyDevelopmentRules.DevelopmentStage(infra, suff, P);
                float dep = ColonyDevelopmentRules.HomelandDependency(suff);
                bool viable = ColonyDevelopmentRules.IsColonyViable(suff, dep);
                if (year == 1) { earlyStage = stage; earlyViable = viable; }
                if (year == 20) { lateStage = stage; lateViable = viable; }
            }
            // 開拓は進む＝前哨地から属州へ（段階が上がる）
            Assert.Greater(lateStage, earlyStage);
            // 初年は本国頼りで成り立たないが、年を経て自立する（成立へ転じる）
            Assert.IsFalse(earlyViable);
            Assert.IsTrue(lateViable);
            // 自立した植民地は本国へ見返りを返す（属州化＝富の源泉）
            float finalInfra = ColonyDevelopmentRules.InfrastructureGrowth(invest, settlement, 20f, P);
            float finalSuff = ColonyDevelopmentRules.SelfSufficiency(finalInfra, localResources, P);
            float finalStage = ColonyDevelopmentRules.DevelopmentStage(finalInfra, finalSuff, P);
            Assert.Greater(ColonyDevelopmentRules.EconomicReturn(finalStage, localResources, P), 0f);
        }
    }
}
