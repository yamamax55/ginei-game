using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// C4：軍の質→戦闘力倍率の合成（ForceQualityRules）を固定する。下士官団（背骨）×新兵練度×即応 を
    /// 単一倍率に合成し、精鋭は冴え・素人/補給切れは弱る。会戦経路がこの1倍率を掛けるだけで質が効く設計点。
    /// </summary>
    public class ForceQualityRulesTests
    {
        [Test]
        public void RecruitFactor_GreenToVeteranRange()
        {
            Assert.AreEqual(0.85f, ForceQualityRules.RecruitFactor(0f), 1e-3f);   // 素人
            Assert.AreEqual(1.15f, ForceQualityRules.RecruitFactor(1f), 1e-3f);   // 精鋭
            Assert.Greater(ForceQualityRules.RecruitFactor(1f), ForceQualityRules.RecruitFactor(0f));
        }

        [Test]
        public void CombatMultiplier_EliteBeatsGreen()
        {
            float elite = ForceQualityRules.CombatMultiplier(new NcoCorps(1f, 1f), 1f, 1f); // 厚い精鋭下士官団＋熟練＋満額
            float green = ForceQualityRules.CombatMultiplier(null, 0f, 1f);                 // 下士官枯渇＋素人
            Assert.Greater(elite, green);
            Assert.AreEqual(1.3f * 1.15f, elite, 1e-3f); // backbone1.3 × recruit1.15 × ready1
            Assert.AreEqual(0.85f, green, 1e-3f);        // backbone1.0 × recruit0.85 × ready1
        }

        [Test]
        public void CombatMultiplier_ReadinessMatters()
        {
            float full = ForceQualityRules.CombatMultiplier(new NcoCorps(0.5f, 0.5f), 0.5f, 1f);
            float starved = ForceQualityRules.CombatMultiplier(new NcoCorps(0.5f, 0.5f), 0.5f, 0.3f); // 弾薬/補給切れ
            Assert.Greater(full, starved);
        }

        [Test]
        public void CombatMultiplier_ClampedToBounds()
        {
            Assert.AreEqual(ForceQualityRules.MinFactor,
                ForceQualityRules.CombatMultiplier(null, 0f, 0f), 1e-3f);   // 即応0でも下限で底打ち
            Assert.LessOrEqual(ForceQualityRules.CombatMultiplier(new NcoCorps(1f, 1f), 1f, 2f),
                ForceQualityRules.MaxFactor + 1e-4f);                       // 上限
        }

        [Test]
        public void CombatMultiplier_ReadinessFromActualFactorWindows()
        {
            // 即応は MilitaryReadinessRules / BudgetRules の出力をそのまま渡せる（窓口委譲）。
            float ammoReady = MilitaryReadinessRules.FirepowerFactor(1f); // 満額弾薬
            float m = ForceQualityRules.CombatMultiplier(new NcoCorps(0.6f, 0.6f), 0.6f, ammoReady);
            Assert.Greater(m, 0f);
        }
    }
}
