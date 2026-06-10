using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>AlienationStratagemRules（離間の計・#1106）の EditMode テスト。既定Paramsで期待値を固定。</summary>
    public class AlienationStratagemRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>不信効果＝偽証×（1＋不和×0.5）×（1−信頼×0.6）。既定値で固定。</summary>
        [Test]
        public void SowDiscordEffect_既定値で確定する()
        {
            // 0.8 × (1 + 0.4×0.5) × (1 − 0.5×0.6) = 0.8 × 1.2 × 0.7 = 0.672
            Assert.AreEqual(0.672f, AlienationStratagemRules.SowDiscordEffect(0.8f, 0.4f, 0.5f), Eps);
        }

        /// <summary>既存の不和があるほど離間が効く＝隙に付け込む（韓遂と馬超）。</summary>
        [Test]
        public void SowDiscordEffect_既存の不和に付け込むほど効く()
        {
            float calm = AlienationStratagemRules.SowDiscordEffect(0.6f, 0f, 0.3f);
            float tense = AlienationStratagemRules.SowDiscordEffect(0.6f, 1f, 0.3f);
            Assert.Greater(tense, calm);
        }

        /// <summary>信頼の固い同盟ほど崩しにくい＝信頼が防壁。</summary>
        [Test]
        public void SowDiscordEffect_固い信頼ほど崩れない()
        {
            float loose = AlienationStratagemRules.SowDiscordEffect(0.8f, 0.2f, 0f);
            float solid = AlienationStratagemRules.SowDiscordEffect(0.8f, 0.2f, 1f);
            Assert.Greater(loose, solid);
            // 信頼1.0で 0.8×(1+0.2×0.5)×(1−1×0.6) = 0.8×1.1×0.4 = 0.352
            Assert.AreEqual(0.352f, solid, Eps);
        }

        /// <summary>opinion 打撃＝不信効果×関係の深さ。深い同盟ほど崩した落差が大きい。</summary>
        [Test]
        public void OpinionDamage_不信と関係の深さに比例する()
        {
            // 0.6 × 0.8 = 0.48
            Assert.AreEqual(0.48f, AlienationStratagemRules.OpinionDamage(0.6f, 0.8f), Eps);
            Assert.AreEqual(0f, AlienationStratagemRules.OpinionDamage(0.6f, 0f), Eps);
        }

        /// <summary>発覚は雑な偽証ほど・敵防諜が強いほど起きる＝決定論 roll で分岐。</summary>
        [Test]
        public void ExposureRisk_雑な偽証は強防諜に見破られる()
        {
            // (1 − 0.2) × 1.0 = 0.8
            Assert.AreEqual(0.8f, AlienationStratagemRules.ExposureRisk(0.2f, 1f), Eps);
            Assert.IsTrue(AlienationStratagemRules.IsExposed(0.2f, 1f, 0.5f));   // roll<0.8
            Assert.IsFalse(AlienationStratagemRules.IsExposed(0.2f, 1f, 0.9f));  // roll>=0.8
            // 精巧な偽証は見破られにくい
            Assert.IsFalse(AlienationStratagemRules.IsExposed(0.9f, 1f, 0.5f));  // 露見率0.1
        }

        /// <summary>発覚すると逆効果＝標的同士が結束する（下手な離間は敵を団結させる）。</summary>
        [Test]
        public void Backfire_発覚で結束し未発覚で不信が残る()
        {
            // 未発覚＝不信ぶん opinion が下がる（負）
            Assert.AreEqual(-0.5f, AlienationStratagemRules.Backfire(false, 0.5f), Eps);
            // 発覚＝結束して opinion が上がる（正・逆効果1.5倍）= 0.5×1.5 = 0.75
            Assert.AreEqual(0.75f, AlienationStratagemRules.Backfire(true, 0.5f), Eps);
            Assert.Greater(AlienationStratagemRules.Backfire(true, 0.5f), 0f);
            Assert.Less(AlienationStratagemRules.Backfire(false, 0.5f), 0f);
        }

        /// <summary>同盟崩壊は累積不信が閾値0.6を超えて初めて起き、靭性で割り引かれる。</summary>
        [Test]
        public void AllianceCollapseChance_閾値超過で割れ靭性で堪える()
        {
            // 閾値未満は崩れない
            Assert.AreEqual(0f, AlienationStratagemRules.AllianceCollapseChance(0.5f, 0f), Eps);
            // 累積0.8・靭性0＝(0.8−0.6)/(1−0.6)×(1−0) = 0.2/0.4 = 0.5
            Assert.AreEqual(0.5f, AlienationStratagemRules.AllianceCollapseChance(0.8f, 0f), Eps);
            // 靭性が高いと同じ不信でも堪える
            float weak = AlienationStratagemRules.AllianceCollapseChance(0.8f, 0f);
            float tough = AlienationStratagemRules.AllianceCollapseChance(0.8f, 0.5f);
            Assert.Greater(weak, tough);
            Assert.IsTrue(AlienationStratagemRules.AllianceCollapses(0.8f, 0f, AlienationStratagemParams.Default, 0.4f));   // roll<0.5
            Assert.IsFalse(AlienationStratagemRules.AllianceCollapses(0.8f, 0f, AlienationStratagemParams.Default, 0.6f)); // roll>=0.5
        }

        /// <summary>疑心は繰り返しで積み上がる＝離間は一度でなく重ねて効く。</summary>
        [Test]
        public void SuspicionTick_繰り返しで疑心が育つ()
        {
            // 0.2 + 0.3×1 = 0.5
            float once = AlienationStratagemRules.SuspicionTick(0.2f, 0.3f, 1f);
            Assert.AreEqual(0.5f, once, Eps);
            // さらに重ねると積み上がる
            float twice = AlienationStratagemRules.SuspicionTick(once, 0.3f, 1f);
            Assert.AreEqual(0.8f, twice, Eps);
            Assert.Greater(twice, once);
        }
    }
}
