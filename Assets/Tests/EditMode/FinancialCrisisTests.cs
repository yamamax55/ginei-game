using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// リーマンショック（#1939 LEHM・<see cref="FinancialCrisisRules"/>）を固定する：評価損(L1)、レバレッジと破綻(L2)、
    /// 信用収縮(L3)、連鎖伝染でシステム損失が直接損を上回る/cascade(L4)、救済コストとtoo-big-to-fail(L5)。
    /// </summary>
    public class FinancialCrisisTests
    {
        [Test]
        public void MarkToMarketLoss_AndLeverage()
        {
            var fi = new FinancialInstitution("行", capital: 50f, assets: 1000f, mbsExposure: 500f);
            Assert.AreEqual(50f, FinancialCrisisRules.MarkToMarketLoss(fi, 0.1f), 1e-3f); // 500*0.1
            Assert.AreEqual(20f, FinancialCrisisRules.Leverage(fi), 1e-3f);               // 1000/50
        }

        [Test]
        public void Bankrupt_WhenLossExceedsCapital()
        {
            var fi = new FinancialInstitution("薄資本", capital: 30f, assets: 1000f, mbsExposure: 500f);
            float loss = FinancialCrisisRules.MarkToMarketLoss(fi, 0.1f); // 50 > 資本30
            Assert.IsTrue(FinancialCrisisRules.IsBankrupt(fi, loss));     // 債務超過＝破綻
            Assert.AreEqual(-20f, FinancialCrisisRules.EquityAfterLoss(fi, loss), 1e-3f);
            // 高レバレッジ（薄い自己資本）ほど小さな損失で破綻
            Assert.IsFalse(FinancialCrisisRules.IsBankrupt(new FinancialInstitution("厚資本", 100f, 1000f, 500f), loss));
        }

        [Test]
        public void CreditCrunch_ScalesWithStress()
        {
            var fi = new FinancialInstitution("行", capital: 100f, assets: 1000f);
            Assert.AreEqual(0.5f, FinancialCrisisRules.CreditCrunchFactor(fi, 50f), 1e-3f); // 損失50/資本100
            Assert.AreEqual(1f, FinancialCrisisRules.CreditCrunchFactor(fi, 200f), 1e-3f);  // 自己資本超＝完全収縮
        }

        [Test]
        public void ResolveCrisis_ContagionAmplifiesLoss()
        {
            // 1社が証券化過多で直接破綻→伝染で生存行も毀損＝システム損失>直接損
            var market = new List<FinancialInstitution>
            {
                new FinancialInstitution("震源", capital: 50f, assets: 1000f, mbsExposure: 1000f, interbankLinkage: 0.3f),
                new FinancialInstitution("行B", capital: 100f, assets: 1000f, mbsExposure: 0f, interbankLinkage: 0.3f),
                new FinancialInstitution("行C", capital: 100f, assets: 1000f, mbsExposure: 0f, interbankLinkage: 0.3f),
                new FinancialInstitution("行D", capital: 100f, assets: 1000f, mbsExposure: 0f, interbankLinkage: 0.3f),
            };
            float directLoss = 1000f * 0.1f; // 震源の直接損=100
            var r = FinancialCrisisRules.ResolveCrisis(market, 0.1f, FinancialCrisisRules.DefaultLGD);
            Assert.GreaterOrEqual(r.failedCount, 1);          // 少なくとも震源が破綻
            Assert.Greater(r.totalLoss, directLoss);          // 伝染でシステム損失が直接損を上回る
        }

        [Test]
        public void ResolveCrisis_Cascade_WhenSurvivorsThin()
        {
            // 生存行の自己資本が薄いと伝染で連鎖破綻（cascade）
            var market = new List<FinancialInstitution>
            {
                new FinancialInstitution("震源", capital: 50f, assets: 1000f, mbsExposure: 1000f, interbankLinkage: 0.5f),
                new FinancialInstitution("脆弱B", capital: 30f, assets: 1000f, mbsExposure: 0f, interbankLinkage: 0.5f),
                new FinancialInstitution("脆弱C", capital: 30f, assets: 1000f, mbsExposure: 0f, interbankLinkage: 0.5f),
                new FinancialInstitution("脆弱D", capital: 30f, assets: 1000f, mbsExposure: 0f, interbankLinkage: 0.5f),
            };
            var r = FinancialCrisisRules.ResolveCrisis(market, 0.1f, FinancialCrisisRules.DefaultLGD);
            Assert.Greater(r.failedCount, 1); // 震源だけでなく連鎖して複数破綻
        }

        [Test]
        public void Bailout_AndSystemicRisk()
        {
            var fi = new FinancialInstitution("大手", capital: 50f, assets: 5000f, mbsExposure: 1000f, interbankLinkage: 0.5f, tooBigToFail: true);
            // 損失120→債務超過の穴=120-50=70 を国庫が注入
            Assert.AreEqual(70f, FinancialCrisisRules.BailoutCost(fi, 120f), 1e-3f);
            Assert.IsTrue(FinancialCrisisRules.ShouldBailout(fi));
            // システミックリスク：証券化過多＋高接続ほど高い
            var fragile = new List<FinancialInstitution> { new FinancialInstitution("a", 50f, 1000f, 900f, 0.8f) };
            var stable = new List<FinancialInstitution> { new FinancialInstitution("b", 500f, 1000f, 50f, 0.1f) };
            Assert.Greater(FinancialCrisisRules.SystemicRisk(fragile), FinancialCrisisRules.SystemicRisk(stable));
        }
    }
}
