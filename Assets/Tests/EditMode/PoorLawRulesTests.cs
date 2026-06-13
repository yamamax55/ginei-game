using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>救貧の人口学的逆説（MALT-4 #1580・マルサス救貧法批判）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class PoorLawRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>短期救済＝救貧水準×貧困×効き0.8（貧困が深く救済が手厚いほど大きい）。</summary>
        [Test]
        public void ShortTermRelief_ScalesWithWelfareAndPoverty()
        {
            // 0.8 * 0.5 * 0.5 = 0.20
            Assert.AreEqual(0.20f, PoorLawRules.ShortTermRelief(0.5f, 0.5f), Eps);
            // 貧困がゼロなら救う相手なし＝0
            Assert.AreEqual(0f, PoorLawRules.ShortTermRelief(1f, 0f), Eps);
        }

        /// <summary>出生刺激＝救貧水準×感度0.6（食べられると子を持てる＝人口増の引き金）。</summary>
        [Test]
        public void FertilityStimulus_RisesWithWelfare()
        {
            Assert.AreEqual(0.6f, PoorLawRules.FertilityStimulus(1f), Eps);
            Assert.AreEqual(0.3f, PoorLawRules.FertilityStimulus(0.5f), Eps);
            Assert.AreEqual(0f, PoorLawRules.FertilityStimulus(0f), Eps);
        }

        /// <summary>人口圧＝刺激された出生が人口を時間で増やす（慈悲が人口圧を高める）。</summary>
        [Test]
        public void PopulationPressureTick_GrowsPopulationOverTime()
        {
            // growth = 0.05 * 0.6 * 1 = 0.03 → 100 * 1.03 = 103
            float pop = PoorLawRules.PopulationPressureTick(100f, 0.6f, 1f);
            Assert.AreEqual(103f, pop, 1e-2f);
            // 刺激ゼロなら増えない
            Assert.AreEqual(100f, PoorLawRules.PopulationPressureTick(100f, 0f, 1f), Eps);
        }

        /// <summary>賃金希釈＝人口が食糧供給を上回るほど一人あたりを薄める（マルサス＝需要増で価格上昇）。</summary>
        [Test]
        public void WageDilution_RisesWithPopulationOvershoot()
        {
            // food=0.5 → overshoot = 1.5/0.5 - 1 = 2 → clamp 0.5*2 = 1.0(clamp01)
            Assert.AreEqual(1.0f, PoorLawRules.WageDilution(1.5f, 0.5f), Eps);
            // 人口が食糧供給と均衡（pop=food=1）なら超過なし＝希釈0
            Assert.AreEqual(0f, PoorLawRules.WageDilution(1f, 1f), Eps);
            // 軽い超過：food=0.8, pop=1.0 → 1/0.8-1=0.25 → 0.5*0.25=0.125
            Assert.AreEqual(0.125f, PoorLawRules.WageDilution(1f, 0.8f), Eps);
        }

        /// <summary>純効果は短期は正（救済が効く）だが希釈が育つと負へ反転＝逆説の核心。</summary>
        [Test]
        public void NetWelfareEffect_FlipsFromPositiveToNegative()
        {
            // 短期：救済0.4・希釈0.1 → +0.3（正）
            Assert.AreEqual(0.3f, PoorLawRules.NetWelfareEffect(0.4f, 0.1f), Eps);
            // 長期：救済0.2・希釈0.7 → -0.5（負へ反転＝帳消し）
            Assert.AreEqual(-0.5f, PoorLawRules.NetWelfareEffect(0.2f, 0.7f), Eps);
        }

        /// <summary>逆説の強さ＝救貧水準×人口弾力性×1.0（出生に響きやすいほど強い・弾力性ゼロなら逆説なし）。</summary>
        [Test]
        public void ParadoxIndex_StrongerWithElasticity()
        {
            Assert.AreEqual(0.5f, PoorLawRules.ParadoxIndex(1f, 0.5f), Eps);
            Assert.AreEqual(0.42f, PoorLawRules.ParadoxIndex(0.7f, 0.6f), Eps);
            // 弾力性ゼロ＝救済が出生に響かない社会では逆説は起きない
            Assert.AreEqual(0f, PoorLawRules.ParadoxIndex(1f, 0f), Eps);
        }

        /// <summary>長期は救済水準に関わらず生存水準へ収束＝マルサスの鉄則（豊かさが生存ぎりぎりへ引き戻される）。</summary>
        [Test]
        public void LongRunSubsistence_ConvergesTowardSubsistence()
        {
            // welfare=0 → target = 0.3。current=0.9 から pull で下がる
            // t = 0.1*1 = 0.1 → lerp(0.9, 0.3, 0.1) = 0.9 - 0.6*0.1 = 0.84
            Assert.AreEqual(0.84f, PoorLawRules.LongRunSubsistence(0f, 0.9f, 1f), Eps);
            // 多tick回せば生存水準0.3へ近づく（鉄則が支配）
            float ls = 0.9f;
            for (int i = 0; i < 200; i++) ls = PoorLawRules.LongRunSubsistence(0f, ls, 1f);
            Assert.That(ls, Is.LessThan(0.31f).And.GreaterThan(0.29f));
        }

        /// <summary>福祉の罠＝純効果が負へ反転すると救済が自らを無効化した罠と判定（既定しきい値0）。</summary>
        [Test]
        public void IsWelfareTrap_TriggersWhenNetEffectNegative()
        {
            Assert.IsTrue(PoorLawRules.IsWelfareTrap(-0.5f));   // 帳消し＝罠
            Assert.IsFalse(PoorLawRules.IsWelfareTrap(0.3f));   // まだ救済が効く
            Assert.IsFalse(PoorLawRules.IsWelfareTrap(0f));     // しきい値ちょうどは罠でない（< 判定）
        }
    }
}
