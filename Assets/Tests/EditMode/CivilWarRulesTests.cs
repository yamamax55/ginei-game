using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内戦（リップシュタット型）を固定する：経済崩壊は時間×強度で進み上限で止まる（双方が痩せる）、
    /// 対外無防備は注いだ戦力以上に空く（外敵の好機）、勝者総取りも長期戦の勝者は荒野の王、
    /// 優勢側への雪崩（バンドワゴン）は拮抗でゼロ・優勢で指数加速。境界とクランプを担保。
    /// </summary>
    public class CivilWarRulesTests
    {
        private static readonly CivilWarParams P = CivilWarParams.Default;
        // 崩壊速度0.02/崩壊上限0.8/対外空き係数1.25/厭戦0.01/雪崩指数2

        [Test]
        public void EconomicCollapse_GrowsWithDurationAndCaps()
        {
            Assert.AreEqual(0.2f, CivilWarRules.EconomicCollapse(10f, 1f, P), 1e-5f);  // 0.02×10×1
            Assert.AreEqual(0.1f, CivilWarRules.EconomicCollapse(10f, 0.5f, P), 1e-5f); // 強度半分＝半分
            Assert.AreEqual(0.8f, CivilWarRules.EconomicCollapse(100f, 1f, P), 1e-5f);  // 上限で停止
            Assert.AreEqual(0f, CivilWarRules.EconomicCollapse(-5f, 1f, P), 1e-5f);     // 負時間クランプ
        }

        [Test]
        public void EconomicOutputFactor_BothSidesShrink()
        {
            Assert.AreEqual(0.8f, CivilWarRules.EconomicOutputFactor(10f, 1f, P), 1e-5f);  // 1−0.2
            Assert.AreEqual(0.2f, CivilWarRules.EconomicOutputFactor(100f, 1f, P), 1e-5f); // 最悪でも0.2は残る
        }

        [Test]
        public void ExternalVulnerability_OpensMoreThanCommitted()
        {
            Assert.AreEqual(0.625f, CivilWarRules.ExternalVulnerability(0.5f, P), 1e-5f); // 0.5×1.25＝注いだ分以上
            Assert.AreEqual(1f, CivilWarRules.ExternalVulnerability(0.8f, P), 1e-5f);     // 8割投入で全開
            Assert.AreEqual(0f, CivilWarRules.ExternalVulnerability(0f, P), 1e-5f);
            Assert.AreEqual(1f, CivilWarRules.ExternalVulnerability(1.5f, P), 1e-5f);     // 入力クランプ
            Assert.AreEqual(0.375f, CivilWarRules.BorderDefenseFactor(0.5f, P), 1e-5f);   // 1−0.625
        }

        [Test]
        public void WarExhaustion_LinearAndClamped()
        {
            Assert.AreEqual(0.5f, CivilWarRules.WarExhaustion(50f, P), 1e-5f); // 0.01×50
            Assert.AreEqual(1f, CivilWarRules.WarExhaustion(200f, P), 1e-5f);  // 上限1
            Assert.AreEqual(0f, CivilWarRules.WarExhaustion(-1f, P), 1e-5f);   // 負時間クランプ
        }

        [Test]
        public void VictorConsolidation_LongWarMakesKingOfRuins()
        {
            Assert.AreEqual(1f, CivilWarRules.VictorConsolidation(1f, 0f, P), 1e-5f);    // 即決＝無傷の総取り
            Assert.AreEqual(0.5f, CivilWarRules.VictorConsolidation(1f, 50f, P), 1e-5f); // 半分疲弊
            Assert.AreEqual(0.3f, CivilWarRules.VictorConsolidation(0.6f, 50f, P), 1e-5f);
            Assert.AreEqual(0f, CivilWarRules.VictorConsolidation(1f, 100f, P), 1e-5f);  // 荒野の王
        }

        [Test]
        public void DefectionMomentum_BandwagonOnlyWhenWinning()
        {
            Assert.AreEqual(0f, CivilWarRules.DefectionMomentum(0.5f, P), 1e-5f);    // 拮抗＝雪崩なし
            Assert.AreEqual(0f, CivilWarRules.DefectionMomentum(0.3f, P), 1e-5f);    // 劣勢＝なし
            Assert.AreEqual(0.25f, CivilWarRules.DefectionMomentum(0.75f, P), 1e-5f); // (0.5)^2
            Assert.AreEqual(0.64f, CivilWarRules.DefectionMomentum(0.9f, P), 1e-5f);  // (0.8)^2＝指数加速
            Assert.AreEqual(1f, CivilWarRules.DefectionMomentum(1f, P), 1e-5f);
            // 単調増加（優勢ほど雪崩は強い）
            Assert.Less(CivilWarRules.DefectionMomentum(0.6f, P), CivilWarRules.DefectionMomentum(0.7f, P));
        }

        [Test]
        public void Params_ClampInvalidValues()
        {
            var p = new CivilWarParams(-1f, 2f, -1f, -1f, 0.5f);
            Assert.AreEqual(0f, p.collapseRate, 1e-5f);
            Assert.AreEqual(1f, p.maxCollapse, 1e-5f);      // Clamp01
            Assert.AreEqual(0f, p.vulnerabilityScale, 1e-5f);
            Assert.AreEqual(0f, p.exhaustionRate, 1e-5f);
            Assert.AreEqual(1f, p.bandwagonExponent, 1e-5f); // 指数は1未満にしない
        }
    }
}
