using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// ReligionTickRules の EditMode テスト（#172-175）。
    /// 数値は ReligionRules.Tick / ReligionRules.SocialEffect の実装から手計算。
    /// ReligionParams.Default: conversionMax=1, conversionSpeed=0.05, affinityBoost=1.5,
    ///                         socialBase=0.9, socialGain=0.3
    /// </summary>
    public class ReligionTickRulesTests
    {
        // --- EnsureReligion ---

        [Test]
        public void EnsureReligion_初期化()
        {
            // religion が null のとき、nativeFaith で初期化されること
            var prov = new Province(1, "民主");
            Assert.IsNull(prov.religion);

            ReligionTickRules.EnsureReligion(prov, "光の信仰");

            Assert.IsNotNull(prov.religion);
            Assert.AreEqual("光の信仰", prov.religion.faithName);
            Assert.AreEqual(0.5f, prov.religion.devotion, 1e-5f); // Religion() デフォルト値
        }

        [Test]
        public void EnsureReligion_冪等()
        {
            // 2回呼んでも devotion がリセットされないこと
            var prov = new Province(1, "専制");
            ReligionTickRules.EnsureReligion(prov, "鉄の宗教");
            prov.religion.devotion = 0.8f; // 状態を変えておく

            ReligionTickRules.EnsureReligion(prov, "別の宗教"); // 2回目

            // devotion は上書きされず 0.8 のまま
            Assert.AreEqual(0.8f, prov.religion.devotion, 1e-5f);
            Assert.AreEqual("鉄の宗教", prov.religion.faithName);
        }

        // --- TickYear ---

        [Test]
        public void TickYear_devotionが均衡へ動く()
        {
            // devotion=0.5, rulerFaith=1.0, affinityMatch=true のとき
            // pressure = (1.0-0.5)*1.0*1.5 = 0.75
            // target = Lerp(0.5, 1.0, 0.75) = 0.875
            // new devotion = MoveTowards(0.5, 0.875, 0.05*1) = 0.55
            var prov = new Province(1, "民主");
            prov.religion = new Religion("テスト信仰", 0.5f);

            ReligionTickRules.TickYear(prov, rulerFaithDevotion: 1.0f, affinityMatch: true);

            // 目標(0.875)に向かって 0.05 動く → 0.55
            Assert.AreEqual(0.55f, prov.religion.devotion, 1e-5f);
        }

        [Test]
        public void TickYear_null_religionを自動初期化して進める()
        {
            // religion が null のとき EnsureReligion を経て自動初期化され、Tick が走ること
            var prov = new Province(1, "専制");
            Assert.IsNull(prov.religion);

            // 自動初期化（devotion=0.5）後に rulerFaith=1.0/affinity=false で1年進む
            // pressure = (1.0-0.5)*1.0*1.0 = 0.5, target = Lerp(0.5,1.0,0.5) = 0.75
            // new devotion = MoveTowards(0.5, 0.75, 0.05) = 0.55
            Assert.DoesNotThrow(() => ReligionTickRules.TickYear(prov, 1.0f, false));
            Assert.IsNotNull(prov.religion);
            Assert.AreEqual(0.55f, prov.religion.devotion, 1e-5f);
        }

        // --- SocialFactor ---

        [Test]
        public void SocialFactor_null中立()
        {
            // religion が null のとき socialBase(0.9) を返すこと（未配線＝中立）
            var prov = new Province(1, "民主");
            Assert.IsNull(prov.religion);

            float factor = ReligionTickRules.SocialFactor(prov);

            // ReligionParams.Default.socialBase = 0.9
            Assert.AreEqual(0.9f, factor, 1e-5f);
        }

        [Test]
        public void SocialFactor_単調性()
        {
            // devotion が高いほど SocialFactor が大きいこと
            // SocialEffect = socialBase + socialGain * devotion = 0.9 + 0.3 * t
            // devotion=0.2 → 0.9 + 0.3*0.2 = 0.96
            // devotion=0.8 → 0.9 + 0.3*0.8 = 1.14
            var provLow = new Province(1, "専制");
            provLow.religion = new Religion("信仰A", 0.2f);

            var provHigh = new Province(2, "専制");
            provHigh.religion = new Religion("信仰A", 0.8f);

            float factorLow  = ReligionTickRules.SocialFactor(provLow);
            float factorHigh = ReligionTickRules.SocialFactor(provHigh);

            Assert.AreEqual(0.96f, factorLow,  1e-5f);
            Assert.AreEqual(1.14f, factorHigh, 1e-5f);
            Assert.Less(factorLow, factorHigh, "devotion が高いほど SocialFactor は大きい");
        }
    }
}
