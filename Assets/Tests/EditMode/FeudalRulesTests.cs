using NUnit.Framework;
using Ginei;
using FP = Ginei.FeudalRules.FeudalParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 封建制・貴族制（#168/#169）を固定する：忠誠×levy の軍役供出、自治権が押し上げ・君主威令が抑える反乱リスク、
    /// 門地開放投資→平民登用率。境界・クランプ・各分岐を決定論で担保。
    /// </summary>
    public class FeudalRulesTests
    {
        [Test]
        public void LevyContribution_ScalesWithLoyalty()
        {
            var p = FP.Default;
            // 忠誠1.0＝全軍役、0.5＝半分、0＝出さない
            Assert.AreEqual(100, FeudalRules.LevyContribution(new Fief(1f, 100, 0.5f), p));
            Assert.AreEqual(50, FeudalRules.LevyContribution(new Fief(0.5f, 100, 0.5f), p));
            Assert.AreEqual(0, FeudalRules.LevyContribution(new Fief(0f, 100, 0.5f), p));
        }

        [Test]
        public void LevyContribution_ClampsAndNullSafe()
        {
            var p = FP.Default;
            // 忠誠は0..1にクランプ、負のlevyは0、nullは0
            Assert.AreEqual(100, FeudalRules.LevyContribution(new Fief(2f, 100, 0.5f), p));   // 忠誠>1→1扱い
            Assert.AreEqual(0, FeudalRules.LevyContribution(new Fief(0.5f, -10, 0.5f), p));   // 負levy→0
            Assert.AreEqual(0, FeudalRules.LevyContribution(null, p));                         // null安全
        }

        [Test]
        public void VassalRebellionRisk_HighLoyaltyStrongKing_Low()
        {
            var p = FP.Default;
            // 忠誠高×君主強＝反乱リスクほぼ0
            float low = FeudalRules.VassalRebellionRisk(new Fief(1f, 100, 0.1f), 1f, p);
            // 忠誠低×君主弱×自治高＝反乱リスク高
            float high = FeudalRules.VassalRebellionRisk(new Fief(0.1f, 100, 0.9f), 0f, p);
            Assert.Less(low, high);
            Assert.AreEqual(0f, low, 0.0001f);
        }

        [Test]
        public void VassalRebellionRisk_AutonomyRaises_KingPowerLowers()
        {
            var p = FP.Default;
            var baseFief = new Fief(0.5f, 100, 0.3f);
            var highAutonomy = new Fief(0.5f, 100, 0.8f);
            // 自治権が高いほどリスク上昇
            Assert.Greater(FeudalRules.VassalRebellionRisk(highAutonomy, 0.3f, p),
                           FeudalRules.VassalRebellionRisk(baseFief, 0.3f, p));
            // 君主威令が強いほどリスク低下
            Assert.Less(FeudalRules.VassalRebellionRisk(baseFief, 0.8f, p),
                        FeudalRules.VassalRebellionRisk(baseFief, 0.2f, p));
        }

        [Test]
        public void VassalRebellionRisk_ClampedToUnit()
        {
            var p = FP.Default;
            // 完全不忠×君主皆無×自治最大→1でクランプ（1超えない）
            float r = FeudalRules.VassalRebellionRisk(new Fief(0f, 100, 1f), 0f, p);
            Assert.AreEqual(1f, r);
            // 完全忠誠×君主最強→0でクランプ（負にならない）
            float r2 = FeudalRules.VassalRebellionRisk(new Fief(1f, 100, 0f), 1f, p);
            Assert.AreEqual(0f, r2);
        }

        [Test]
        public void MonopolyOpening_ScalesAndCaps()
        {
            var p = FP.Default; // 投資効き0.5・上限0.8
            Assert.AreEqual(0f, FeudalRules.MonopolyOpening(0f, p));        // 無投資＝開放なし
            Assert.AreEqual(0.25f, FeudalRules.MonopolyOpening(0.5f, p), 0.0001f); // 0.5×0.5
            Assert.AreEqual(0.5f, FeudalRules.MonopolyOpening(1f, p), 0.0001f);    // 1×0.5（上限0.8未満）
        }

        [Test]
        public void MonopolyOpening_RespectsMaxCap()
        {
            // 投資効きを強くしても上限を超えない＋投資は0..1にクランプ
            var p = new FP(0.6f, 0.7f, 2f, 0.8f); // 投資効き2.0
            Assert.AreEqual(0.8f, FeudalRules.MonopolyOpening(1f, p), 0.0001f);  // 2.0でも上限0.8
            Assert.AreEqual(0.8f, FeudalRules.MonopolyOpening(5f, p), 0.0001f);  // 投資>1→1扱い→上限
        }
    }
}
