using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 貿易戦争を固定する：関税打撃＝関税×貿易量、報復は双方関税の幾何平均（片方0で無）、
    /// 関税は自国産業を保護しつつ消費者を負担させる、報復×統合で供給網が混乱し貿易量比例で双方が損失、
    /// 報復×代替先でデカップリング、正味＝保護−負担−損失。クランプを担保。
    /// </summary>
    public class TradeWarRulesTests
    {
        private static readonly TradeWarParams P = TradeWarParams.Default;
        // 打撃幅0.5/応酬1.0/保護0.6/負担0.7/混乱0.8/損失0.9/分断1.0

        [Test]
        public void TariffImpact_RateTimesVolume()
        {
            Assert.AreEqual(0.5f, TradeWarRules.TariffImpact(1f, 1f, P), 1e-4f);   // 全力×太い貿易＝最大
            Assert.AreEqual(0.2f, TradeWarRules.TariffImpact(0.5f, 0.8f, P), 1e-4f);
            Assert.AreEqual(0f, TradeWarRules.TariffImpact(0f, 1f, P), 1e-4f);     // 関税ゼロ＝打撃なし
            Assert.AreEqual(0f, TradeWarRules.TariffImpact(1f, 0f, P), 1e-4f);     // 貿易ゼロ＝打撃なし
        }

        [Test]
        public void RetaliationEscalation_GeometricMean_NeedsBothSides()
        {
            Assert.AreEqual(1f, TradeWarRules.RetaliationEscalation(1f, 1f, P), 1e-3f);   // 双方全力＝最大
            Assert.AreEqual(0.6f, TradeWarRules.RetaliationEscalation(0.4f, 0.9f, P), 1e-3f); // sqrt(0.36)
            Assert.AreEqual(0f, TradeWarRules.RetaliationEscalation(1f, 0f, P), 1e-3f);   // 片方だけ＝応酬せず
            Assert.AreEqual(0f, TradeWarRules.RetaliationEscalation(0f, 1f, P), 1e-3f);
        }

        [Test]
        public void DomesticProtection_TariffTimesImportCompetition()
        {
            Assert.AreEqual(0.6f, TradeWarRules.DomesticProtection(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.15f, TradeWarRules.DomesticProtection(0.5f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, TradeWarRules.DomesticProtection(1f, 0f, P), 1e-4f); // 競合産業なし＝恩恵なし
        }

        [Test]
        public void ConsumerBurden_TariffTimesImportDependency()
        {
            Assert.AreEqual(0.7f, TradeWarRules.ConsumerBurden(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.14f, TradeWarRules.ConsumerBurden(0.5f, 0.4f, P), 1e-4f);
            Assert.AreEqual(0f, TradeWarRules.ConsumerBurden(1f, 0f, P), 1e-4f); // 自給自足＝負担なし
        }

        [Test]
        public void SupplyChainDisruption_RetaliationTimesIntegration()
        {
            Assert.AreEqual(0.8f, TradeWarRules.SupplyChainDisruption(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.2f, TradeWarRules.SupplyChainDisruption(0.5f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, TradeWarRules.SupplyChainDisruption(1f, 0f, P), 1e-4f); // 非統合＝混乱せず
        }

        [Test]
        public void MutualLoss_DisruptionTimesVolume()
        {
            Assert.AreEqual(0.9f, TradeWarRules.MutualLoss(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.27f, TradeWarRules.MutualLoss(0.5f, 0.6f, P), 1e-4f);
            Assert.AreEqual(0f, TradeWarRules.MutualLoss(1f, 0f, P), 1e-4f); // 貿易なし＝損失なし
        }

        [Test]
        public void Decoupling_RetaliationTimesAlternatives()
        {
            Assert.AreEqual(1f, TradeWarRules.Decoupling(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.4f, TradeWarRules.Decoupling(0.8f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, TradeWarRules.Decoupling(1f, 0f, P), 1e-4f); // 代替先なし＝分断できず
        }

        [Test]
        public void NetTradeWarValue_ProtectionMinusBurdenMinusLoss()
        {
            // 保護0.6−負担0.14−損失0.27 = 0.19
            Assert.AreEqual(0.19f, TradeWarRules.NetTradeWarValue(0.6f, 0.14f, 0.27f, P), 1e-4f);
            // 痛みが恩恵を上回ると負（-1..1 にクランプ）
            Assert.AreEqual(-1f, TradeWarRules.NetTradeWarValue(0f, 1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.6f, TradeWarRules.NetTradeWarValue(0.6f, 0f, 0f, P), 1e-4f);
        }

        [Test]
        public void ClampsOutOfRangeInputs()
        {
            Assert.AreEqual(0.5f, TradeWarRules.TariffImpact(2f, 5f, P), 1e-4f);        // 上振れクランプ
            Assert.AreEqual(0f, TradeWarRules.TariffImpact(-1f, 1f, P), 1e-4f);          // 下振れクランプ
            Assert.AreEqual(0f, TradeWarRules.RetaliationEscalation(-1f, -1f, P), 1e-3f);
            // 各入力 0..1 へクランプ：保護 clamp01(2)=1、負担/損失 clamp01(-1)=0 → 1-0-0=1.0
            Assert.AreEqual(1.0f, TradeWarRules.NetTradeWarValue(2f, -1f, -1f, P), 1e-4f);
        }

        [Test]
        public void Story_EscalatingTariffsBackfireIntoMutualLoss()
        {
            // 深く統合した太い貿易相手と全面的な関税の応酬に入ると、保護の恩恵を負担と損失が呑み込み正味は負へ。
            float ownTariff = 1f, oppTariff = 1f;
            float volume = 1f, integration = 1f, importComp = 0.5f, importDep = 1f, alternatives = 0.7f;

            float esc = TradeWarRules.RetaliationEscalation(ownTariff, oppTariff, P); // 1.0
            float protection = TradeWarRules.DomesticProtection(ownTariff, importComp, P); // 0.3
            float burden = TradeWarRules.ConsumerBurden(ownTariff, importDep, P);         // 0.7
            float disruption = TradeWarRules.SupplyChainDisruption(esc, integration, P);  // 0.8
            float loss = TradeWarRules.MutualLoss(disruption, volume, P);                 // 0.72
            float decoupling = TradeWarRules.Decoupling(esc, alternatives, P);           // 0.7
            float net = TradeWarRules.NetTradeWarValue(protection, burden, loss, P);

            Assert.Greater(esc, 0.5f);          // 応酬は激化
            Assert.Greater(decoupling, 0.5f);   // 経済分断が進む
            Assert.Greater(loss, protection);   // 損失が保護の恩恵を上回る
            Assert.Less(net, 0f);               // 貿易戦争に勝者なし＝自滅
        }
    }
}
