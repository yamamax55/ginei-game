using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>平等化の潮流と身分侵食（TOCQ-5 #1498・トクヴィル）の純ロジックを担保する。</summary>
    public class EqualityDriftRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>平等化の圧力＝民主的心性×経済的平準化×規模。両者揃うほど強い。</summary>
        [Test]
        public void EqualizationPressure_民主心性と経済平準化の積()
        {
            // 0.8 × 0.5 × pressureScale(1.0) = 0.4
            Assert.AreEqual(0.4f, EqualityDriftRules.EqualizationPressure(0.8f, 0.5f), Tol);
            // どちらか0なら圧力0
            Assert.AreEqual(0f, EqualityDriftRules.EqualizationPressure(0f, 1f), Tol);
        }

        /// <summary>平等化が身分・序列を時間で溶かす＝圧力×侵食レート×dt ぶん下がる。</summary>
        [Test]
        public void HierarchyErosionTick_平等化が身分を侵食する()
        {
            // 身分1.0、圧力0.4、erosionRate0.03、dt5年 → 1.0 - 0.4*0.03*5 = 0.94
            float eroded = EqualityDriftRules.HierarchyErosionTick(1f, 0.4f, 5f);
            Assert.AreEqual(0.94f, eroded, Tol);
            // 圧力0なら不変
            Assert.AreEqual(1f, EqualityDriftRules.HierarchyErosionTick(1f, 0f, 10f), Tol);
        }

        /// <summary>不可逆性＝一度溶けた身分は復元しにくい（ラチェット）。</summary>
        [Test]
        public void Irreversibility_溶けた身分は戻りにくい()
        {
            // 侵食済み0.5、全力復元1.0 → headroom0.5 × 1.0 × (1-0.85) = 0.075 だけ戻る → 0.575
            float restored = EqualityDriftRules.Irreversibility(0.5f, 1f);
            Assert.AreEqual(0.575f, restored, Tol);
            // 復元試行0なら戻らない
            Assert.AreEqual(0.5f, EqualityDriftRules.Irreversibility(0.5f, 0f), Tol);
            // わずかしか戻らない＝平等化は戻らない
            Assert.Less(restored, 0.6f);
        }

        /// <summary>身分の平準化＝貴賤の差が時間で縮む（単調増・上限飽和）。</summary>
        [Test]
        public void StatusLeveling_身分差が縮まる()
        {
            // 現在0.2、圧力0.5、levelingRate0.04、dt10年 → 0.2 + 0.5*0.04*10 = 0.4
            Assert.AreEqual(0.4f, EqualityDriftRules.StatusLeveling(0.2f, 0.5f, 10f), Tol);
            // 上限1で飽和
            Assert.AreEqual(1f, EqualityDriftRules.StatusLeveling(0.9f, 1f, 100f), Tol);
        }

        /// <summary>貴族制の名残＝伝統が強いと身分の名残が残る（速度は緩むが止まらない）。</summary>
        [Test]
        public void AristocraticResidue_伝統が名残を残す()
        {
            // 身分0.8、伝統1.0、traditionDrag0.5 → 0.8 * 1.0 * (1-0.5) = 0.4
            Assert.AreEqual(0.4f, EqualityDriftRules.AristocraticResidue(0.8f, 1f), Tol);
            // 伝統が弱いほど名残は小さい
            Assert.Less(EqualityDriftRules.AristocraticResidue(0.8f, 0.3f),
                        EqualityDriftRules.AristocraticResidue(0.8f, 1f));
        }

        /// <summary>流動性の増加＝平準化が生まれより能力の社会を生む。</summary>
        [Test]
        public void MobilityIncrease_平準化が流動性を高める()
        {
            // 平準化0.5 × mobilityScale0.8 = 0.4
            Assert.AreEqual(0.4f, EqualityDriftRules.MobilityIncrease(0.5f), Tol);
            // 平準化が進むほど流動性は単調増
            Assert.Greater(EqualityDriftRules.MobilityIncrease(0.9f),
                           EqualityDriftRules.MobilityIncrease(0.3f));
        }

        /// <summary>平等vs自由＝平等への情熱が自由を犠牲にしうる緊張（平等な隷従の誘惑）。</summary>
        [Test]
        public void EqualityVsLiberty_平等が自由と緊張する()
        {
            // 平等0.9 × (1-自由0.2) = 0.72 ＝緊張が高い
            Assert.AreEqual(0.72f, EqualityDriftRules.EqualityVsLiberty(0.9f, 0.2f), Tol);
            // 自由が十分なら緊張は小さい
            Assert.AreEqual(0.09f, EqualityDriftRules.EqualityVsLiberty(0.9f, 0.9f), Tol);
            // 平等が高く自由が低い領域ほど誘惑が強い
            Assert.Greater(EqualityDriftRules.EqualityVsLiberty(0.9f, 0.1f),
                           EqualityDriftRules.EqualityVsLiberty(0.9f, 0.8f));
        }

        /// <summary>平等社会判定＝身分が閾値以下まで溶けたら民主的平等社会。</summary>
        [Test]
        public void IsEgalitarianSociety_身分が溶けたら平等社会()
        {
            Assert.IsTrue(EqualityDriftRules.IsEgalitarianSociety(0.1f, 0.2f));
            Assert.IsFalse(EqualityDriftRules.IsEgalitarianSociety(0.8f, 0.2f));
            // 境界（閾値ちょうど）は到達扱い
            Assert.IsTrue(EqualityDriftRules.IsEgalitarianSociety(0.2f, 0.2f));
        }
    }
}
