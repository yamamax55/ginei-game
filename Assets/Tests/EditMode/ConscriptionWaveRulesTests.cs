using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ConscriptionWaveRules（徴兵波＝総動員）の純ロジック検証。
    /// 既定 Params で期待値を固定（許容 1e-4f、Pow 箇所のみ緩め）。
    /// </summary>
    public class ConscriptionWaveRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void DraftPool_ScalesWithPopulationAndIntensity()
        {
            // 1000 * 0.5(maxFraction) * 0.5(intensity) = 250
            Assert.AreEqual(250f, ConscriptionWaveRules.DraftPool(1000f, 0.5f), Eps);
            // 強度0なら兵は集まらない
            Assert.AreEqual(0f, ConscriptionWaveRules.DraftPool(1000f, 0f), Eps);
        }

        [Test]
        public void LaborDrain_StrongerDraftDrainsMoreLabor()
        {
            // 1000 * 0.5 * 0.6(sensitivity) = 300
            Assert.AreEqual(300f, ConscriptionWaveRules.LaborDrain(0.5f, 1000f), Eps);
            // 強度を上げるほど流出が増える（単調増加）
            Assert.Greater(
                ConscriptionWaveRules.LaborDrain(0.9f, 1000f),
                ConscriptionWaveRules.LaborDrain(0.3f, 1000f));
        }

        [Test]
        public void ConscriptQuality_DropsAsDraftWidens()
        {
            // drop=0.5^2=0.25; q=1-0.7*0.25=0.825
            Assert.AreEqual(0.825f, ConscriptionWaveRules.ConscriptQuality(0.5f), Eps);
            // 最大強度では下限 MinQuality=0.3
            Assert.AreEqual(0.3f, ConscriptionWaveRules.ConscriptQuality(1f), Eps);
            // 軽い徴兵は質が高い（>= 強い徴兵）
            Assert.GreaterOrEqual(
                ConscriptionWaveRules.ConscriptQuality(0.1f),
                ConscriptionWaveRules.ConscriptQuality(0.8f));
        }

        [Test]
        public void SocialStrain_HeavierWhenWarSupportLow()
        {
            // 0.5*0.8*(2-1)=0.4（高支持）
            Assert.AreEqual(0.4f, ConscriptionWaveRules.SocialStrain(0.5f, 1f), Eps);
            // 0.5*0.8*(2-0)=0.8（無支持）
            Assert.AreEqual(0.8f, ConscriptionWaveRules.SocialStrain(0.5f, 0f), Eps);
        }

        [Test]
        public void BarrelScraping_RisesWithCumulativeDraft()
        {
            // ratio=0.5; 0.5^(1/1.5)=0.5^0.6667 ≈ 0.6300
            Assert.AreEqual(0.6300f, ConscriptionWaveRules.BarrelScraping(500f, 1000f), PowEps);
            // 取り尽くせば 1
            Assert.AreEqual(1f, ConscriptionWaveRules.BarrelScraping(1000f, 1000f), Eps);
            // 人口0は枯渇扱い
            Assert.AreEqual(1f, ConscriptionWaveRules.BarrelScraping(0f, 0f), Eps);
        }

        [Test]
        public void DraftEvasion_HighWhenEnforcementWeak()
        {
            // 0.8*(1-0.25)=0.6
            Assert.AreEqual(0.6f, ConscriptionWaveRules.DraftEvasion(0.8f, 0.25f), Eps);
            // 完全な強制力なら忌避は0
            Assert.AreEqual(0f, ConscriptionWaveRules.DraftEvasion(0.8f, 1f), Eps);
        }

        [Test]
        public void ManpowerSustainability_ComparesPoolToCasualties()
        {
            // 250 / 500 = 0.5（補えない）
            Assert.AreEqual(0.5f, ConscriptionWaveRules.ManpowerSustainability(250f, 500f), Eps);
            // 損耗を上回れば 1 で頭打ち
            Assert.AreEqual(1f, ConscriptionWaveRules.ManpowerSustainability(500f, 250f), Eps);
            // 損耗ゼロなら持続可能
            Assert.AreEqual(1f, ConscriptionWaveRules.ManpowerSustainability(0f, 0f), Eps);
        }

        [Test]
        public void IsManpowerExhausted_FiresAtThreshold()
        {
            Assert.IsTrue(ConscriptionWaveRules.IsManpowerExhausted(0.63f, 0.6f));
            Assert.IsFalse(ConscriptionWaveRules.IsManpowerExhausted(0.5f, 0.6f));
        }

        [Test]
        public void Narrative_TotalWarBleedsTheNation()
        {
            // 物語：長期戦で人的資源が尽き、最後の総動員に踏み切る勢力。
            // 序盤の穏当な徴兵。
            var p = ConscriptionWaveParams.Default;
            float earlyPool = ConscriptionWaveRules.DraftPool(2000f, 0.3f, p);
            float earlyQuality = ConscriptionWaveRules.ConscriptQuality(0.3f, p);
            float earlyStrain = ConscriptionWaveRules.SocialStrain(0.3f, 0.8f, p);

            // 終盤の総動員（強度を引き上げる）。
            float latePool = ConscriptionWaveRules.DraftPool(2000f, 1f, p);
            float lateQuality = ConscriptionWaveRules.ConscriptQuality(1f, p);
            float lateStrain = ConscriptionWaveRules.SocialStrain(1f, 0.3f, p);

            // 総動員でより多く集まるが、質は落ち、社会負荷は跳ね上がる。
            Assert.Greater(latePool, earlyPool);
            Assert.Less(lateQuality, earlyQuality);
            Assert.Greater(lateStrain, earlyStrain);

            // 累積徴兵が総人口の大半に達し底をさらう＝枯渇。
            float scrape = ConscriptionWaveRules.BarrelScraping(1900f, 2000f, p);
            Assert.IsTrue(ConscriptionWaveRules.IsManpowerExhausted(scrape, 0.8f));

            // 損耗が徴兵を上回り、補充は持続不能（latePool=1000 < 損耗1500）。
            float casualties = 1500f;
            float sustain = ConscriptionWaveRules.ManpowerSustainability(latePool, casualties);
            Assert.Less(sustain, 1f);
        }
    }
}
