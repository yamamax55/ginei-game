using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>資源採掘（鉱物資源の採取と枯渇）の純ロジックのテスト。既定Params期待値を手計算で固定。</summary>
    public class ResourceExtractionRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow/Sqrt を含む箇所

        [Test]
        public void ExtractionRate_GradeEquipLabor()
        {
            // 0.8 * 0.5 * sqrt(100=10) * rateScale1 = 4.0
            float rate = ResourceExtractionRules.ExtractionRate(0.8f, 0.5f, 100f);
            Assert.AreEqual(4.0f, rate, PowEps);
        }

        [Test]
        public void ExtractionRate_LaborDiminishingReturns()
        {
            // 労働力は逓減指数0.5＝4倍にしても採掘ペースは2倍（sqrt）。
            float low = ResourceExtractionRules.ExtractionRate(1f, 1f, 25f);   // sqrt(25)=5
            float high = ResourceExtractionRules.ExtractionRate(1f, 1f, 100f); // sqrt(100)=10
            Assert.AreEqual(5f, low, PowEps);
            Assert.AreEqual(10f, high, PowEps);
        }

        [Test]
        public void DepletionProgress_FasterRateDepletesSooner()
        {
            // (累積200 + ペース4×depletionPace1) / 規模1000 = 0.204
            float prog = ResourceExtractionRules.DepletionProgress(4f, 1000f, 200f);
            Assert.AreEqual(0.204f, prog, Eps);
            // 鉱床規模0以下は完全枯渇=1。
            Assert.AreEqual(1f, ResourceExtractionRules.DepletionProgress(4f, 0f, 0f), Eps);
        }

        [Test]
        public void ExtractionCost_RisesWithLowGradeAndDepletion()
        {
            // costBase0.2 + (1-0.8)*0.5 + 0.5*0.6 = 0.2 + 0.1 + 0.3 = 0.6
            float cost = ResourceExtractionRules.ExtractionCost(0.8f, 0.5f);
            Assert.AreEqual(0.6f, cost, Eps);
            // 高品位・浅い鉱床は基準コストのみ。
            Assert.AreEqual(0.2f, ResourceExtractionRules.ExtractionCost(1f, 0f), Eps);
        }

        [Test]
        public void RemainingReserves_SizeMinusCumulative()
        {
            Assert.AreEqual(800f, ResourceExtractionRules.RemainingReserves(1000f, 200f), Eps);
            // 掘り尽くしは0で下げ止まる。
            Assert.AreEqual(0f, ResourceExtractionRules.RemainingReserves(100f, 200f), Eps);
        }

        [Test]
        public void EnvironmentalImpact_MitigatedButNotEliminated()
        {
            // gross=0.5*1=0.5; mitigation=0.4*0.9=0.36; 0.5*(1-0.36)=0.32
            float impact = ResourceExtractionRules.EnvironmentalImpact(0.5f, 0.4f);
            Assert.AreEqual(0.32f, impact, Eps);
            // 環境技術MAXでも mitigationCap0.9 ぶんしか消せず1割残る。
            float full = ResourceExtractionRules.EnvironmentalImpact(1f, 1f);
            Assert.AreEqual(0.1f, full, Eps); // 1*1*(1-0.9)=0.1
        }

        [Test]
        public void ResourcePremium_ScarcityTimesStrategicValue()
        {
            // 0.9 * 0.5 * premiumScale2 = 0.9
            Assert.AreEqual(0.9f, ResourceExtractionRules.ResourcePremium(0.9f, 0.5f), Eps);
            // どちらか0ならプレミアムなし。
            Assert.AreEqual(0f, ResourceExtractionRules.ResourcePremium(0f, 1f), Eps);
        }

        [Test]
        public void NetExtractionProfit_SignAndClamp()
        {
            // 収益4*(1+0.9)=7.6; 利益7.6-0.6=7.0; 7.0/4=1.75 → clamp 1.0
            Assert.AreEqual(1f, ResourceExtractionRules.NetExtractionProfit(4f, 0.9f, 0.6f), Eps);
            // 赤字：収益1; 利益1-5=-4; -4/1=-4 → clamp -1.0
            Assert.AreEqual(-1f, ResourceExtractionRules.NetExtractionProfit(1f, 0f, 5f), Eps);
            // 穏当な黒字：収益2; 利益2-1=1; 1/2=0.5
            Assert.AreEqual(0.5f, ResourceExtractionRules.NetExtractionProfit(2f, 0f, 1f), Eps);
            // 採掘ペース0＝産出なし＝純益0。
            Assert.AreEqual(0f, ResourceExtractionRules.NetExtractionProfit(0f, 1f, 5f), Eps);
        }

        [Test]
        public void IsDepositExhausted_ThresholdAndDefault()
        {
            Assert.IsTrue(ResourceExtractionRules.IsDepositExhausted(5f, 10f));
            Assert.IsFalse(ResourceExtractionRules.IsDepositExhausted(20f, 10f));
            // 既定閾値1.0：残存1以下で枯渇。
            Assert.IsTrue(ResourceExtractionRules.IsDepositExhausted(0.5f));
            Assert.IsFalse(ResourceExtractionRules.IsDepositExhausted(2f));
        }

        [Test]
        public void Story_RichVeinExhaustsAndTurnsUnprofitable()
        {
            var p = ResourceExtractionParams.Default;

            // 序盤：高品位(0.9)・新品設備(0.9)・潤沢な労働力(100)の豊鉱。
            float earlyRate = ResourceExtractionRules.ExtractionRate(0.9f, 0.9f, 100f, p);
            // 0.9*0.9*sqrt(100=10)=8.1
            Assert.AreEqual(8.1f, earlyRate, PowEps);

            float earlyDepletion = ResourceExtractionRules.DepletionProgress(earlyRate, 1000f, 0f, p); // (0+8.1)/1000
            float earlyCost = ResourceExtractionRules.ExtractionCost(0.9f, earlyDepletion, p);
            // ありふれた鉱物＝プレミアムなし（採算は純粋にコスト/産出比で決まる）。
            float premium = ResourceExtractionRules.ResourcePremium(0f, 0f, p);
            Assert.AreEqual(0f, premium, Eps);
            float earlyProfit = ResourceExtractionRules.NetExtractionProfit(earlyRate, premium, earlyCost, p);
            // 序盤は浅く高品位＝低コストで大きく黒字。
            Assert.Greater(earlyProfit, 0.5f);

            // 残存埋蔵量はまだ潤沢＝枯渇していない。
            float earlyReserves = ResourceExtractionRules.RemainingReserves(1000f, earlyRate, p);
            Assert.IsFalse(ResourceExtractionRules.IsDepositExhausted(earlyReserves));

            // 終盤：品位が落ち(0.3)・ほぼ掘り尽くし(累積990)＝深く掘ってコスト急騰。
            float lateRate = ResourceExtractionRules.ExtractionRate(0.3f, 0.9f, 100f, p);
            float lateDepletion = ResourceExtractionRules.DepletionProgress(lateRate, 1000f, 990f, p);
            float lateCost = ResourceExtractionRules.ExtractionCost(0.3f, lateDepletion, p);
            float lateProfit = ResourceExtractionRules.NetExtractionProfit(lateRate, premium, lateCost, p);

            // 終盤はコストが嵩み採算が悪化＝序盤より純益が落ちる。
            Assert.Less(lateProfit, earlyProfit);

            // 残り少なく既定閾値を下回れば枯渇＝採掘終了。
            float lateReserves = ResourceExtractionRules.RemainingReserves(1000f, 999.5f, p); // 残0.5
            Assert.IsTrue(ResourceExtractionRules.IsDepositExhausted(lateReserves));

            // 環境負荷：採掘ペースが速いほど傷み、環境技術で緩和できる。
            float dirty = ResourceExtractionRules.EnvironmentalImpact(earlyRate, 0f, p);
            float clean = ResourceExtractionRules.EnvironmentalImpact(earlyRate, 1f, p);
            Assert.Greater(dirty, clean);
        }
    }
}
