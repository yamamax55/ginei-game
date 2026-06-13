using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// SupplyModeRules（CRV-3 #1366）の純ロジック検証。作戦様式別補給消費の非対称＝
    /// 侵攻×2.0・保持×1.0・撤退×0.5＝攻める側は守る側の何倍も兵站を要する。既定Paramsで期待値固定。
    /// </summary>
    public class SupplyModeRulesTests
    {
        /// <summary>様式別の消費倍率＝侵攻×2.0・保持×1.0・撤退×0.5（攻勢/防御の非対称）。</summary>
        [Test]
        public void ConsumptionMultiplier_様式別の倍率が非対称()
        {
            Assert.AreEqual(2.0f, SupplyModeRules.ConsumptionMultiplier(SupplyMode.侵攻), 1e-4f);
            Assert.AreEqual(1.0f, SupplyModeRules.ConsumptionMultiplier(SupplyMode.保持), 1e-4f);
            Assert.AreEqual(0.5f, SupplyModeRules.ConsumptionMultiplier(SupplyMode.撤退), 1e-4f);
            // 侵攻は撤退の4倍・保持の2倍
            Assert.Greater(SupplyModeRules.ConsumptionMultiplier(SupplyMode.侵攻),
                SupplyModeRules.ConsumptionMultiplier(SupplyMode.保持));
        }

        /// <summary>実際の補給消費＝基礎消費×様式倍率×強度。同じ部隊でも侵攻は撤退の4倍を食う。</summary>
        [Test]
        public void SupplyConsumption_様式と強度で実消費が決まる()
        {
            // 基礎0.2・強度1.0：侵攻=0.2×2.0=0.4／保持=0.2／撤退=0.1
            Assert.AreEqual(0.4f, SupplyModeRules.SupplyConsumption(0.2f, SupplyMode.侵攻, 1.0f), 1e-4f);
            Assert.AreEqual(0.2f, SupplyModeRules.SupplyConsumption(0.2f, SupplyMode.保持, 1.0f), 1e-4f);
            Assert.AreEqual(0.1f, SupplyModeRules.SupplyConsumption(0.2f, SupplyMode.撤退, 1.0f), 1e-4f);
            // 強度0なら静止＝消費0
            Assert.AreEqual(0f, SupplyModeRules.SupplyConsumption(0.2f, SupplyMode.侵攻, 0f), 1e-4f);
        }

        /// <summary>攻勢の補給負担＝前進が深いほど増す。深い前進は浅い前進より重い。</summary>
        [Test]
        public void OffensiveBurden_前進が深いほど補給負担が増す()
        {
            float shallow = SupplyModeRules.OffensiveBurden(0.5f, 0.2f);
            float deep = SupplyModeRules.OffensiveBurden(0.5f, 0.9f);
            Assert.Greater(deep, shallow);
            // 深度0：force0.5×(1)×offensiveWeight2.0×0.5 = 0.5
            Assert.AreEqual(0.5f, SupplyModeRules.OffensiveBurden(0.5f, 0f), 1e-4f);
        }

        /// <summary>防御の節約＝塹壕化で保持倍率1.0を最大40%節約＝守る側の兵站優位。</summary>
        [Test]
        public void DefensiveEconomy_塹壕で補給を節約()
        {
            // 塹壕化0：保持倍率そのまま1.0
            Assert.AreEqual(1.0f, SupplyModeRules.DefensiveEconomy(SupplyMode.保持, 0f), 1e-4f);
            // 塹壕化1.0：1.0×(1−1.0×0.4)=0.6
            Assert.AreEqual(0.6f, SupplyModeRules.DefensiveEconomy(SupplyMode.保持, 1.0f), 1e-4f);
            // 保持以外は節約が効かない＝素の倍率
            Assert.AreEqual(2.0f, SupplyModeRules.DefensiveEconomy(SupplyMode.侵攻, 1.0f), 1e-4f);
        }

        /// <summary>撤退の節約＝整然とした撤退は撤退倍率0.5を最大50%節約＝物資放棄が少ない。</summary>
        [Test]
        public void RetreatSavings_整然とした撤退は消費を抑える()
        {
            // 秩序0：撤退倍率そのまま0.5
            Assert.AreEqual(0.5f, SupplyModeRules.RetreatSavings(SupplyMode.撤退, 0f), 1e-4f);
            // 秩序1.0：0.5×(1−1.0×0.5)=0.25
            Assert.AreEqual(0.25f, SupplyModeRules.RetreatSavings(SupplyMode.撤退, 1.0f), 1e-4f);
            // 撤退以外は効かない
            Assert.AreEqual(1.0f, SupplyModeRules.RetreatSavings(SupplyMode.保持, 1.0f), 1e-4f);
        }

        /// <summary>攻防の補給比＝侵攻(攻撃) vs 保持(防御)で2.0＝攻める側は守る側の倍の兵站を要する。</summary>
        [Test]
        public void AttackerDefenderRatio_攻める側が何倍要するか()
        {
            // 侵攻×2.0 ÷ 保持×1.0 = 2.0
            Assert.AreEqual(2.0f, SupplyModeRules.AttackerDefenderRatio(SupplyMode.侵攻, SupplyMode.保持), 1e-4f);
            // 侵攻×2.0 ÷ 撤退×0.5 = 4.0（追撃側は撤退側の4倍）
            Assert.AreEqual(4.0f, SupplyModeRules.AttackerDefenderRatio(SupplyMode.侵攻, SupplyMode.撤退), 1e-4f);
            // 同じ様式同士は1.0
            Assert.AreEqual(1.0f, SupplyModeRules.AttackerDefenderRatio(SupplyMode.保持, SupplyMode.保持), 1e-4f);
        }

        /// <summary>様式別の継続性＝同じ備蓄でも攻勢は早く尽き撤退は長く保つ。</summary>
        [Test]
        public void SustainabilityByMode_攻勢は早く尽きる()
        {
            // 備蓄1.0：侵攻=1.0/2.0=0.5／保持=1.0／撤退=1.0/0.5→clamp1.0
            Assert.AreEqual(0.5f, SupplyModeRules.SustainabilityByMode(SupplyMode.侵攻, 1.0f), 1e-4f);
            Assert.AreEqual(1.0f, SupplyModeRules.SustainabilityByMode(SupplyMode.保持, 1.0f), 1e-4f);
            // 攻勢は防御より継続が短い
            Assert.Less(SupplyModeRules.SustainabilityByMode(SupplyMode.侵攻, 0.6f),
                SupplyModeRules.SustainabilityByMode(SupplyMode.保持, 0.6f));
        }

        /// <summary>兵站破綻判定＝消費が供給能力×閾値を超えたら true（攻勢終末点）。</summary>
        [Test]
        public void IsLogisticallyOverextended_供給超過で破綻()
        {
            // 消費0.8 > 供給0.5×1.0 ＝ 破綻
            Assert.IsTrue(SupplyModeRules.IsLogisticallyOverextended(0.8f, 0.5f));
            // 消費0.4 < 供給0.5 ＝ 健全
            Assert.IsFalse(SupplyModeRules.IsLogisticallyOverextended(0.4f, 0.5f));
            // 閾値0.5なら供給0.5×0.5=0.25を超えた0.3で破綻
            Assert.IsTrue(SupplyModeRules.IsLogisticallyOverextended(0.3f, 0.5f, 0.5f));
        }
    }
}
