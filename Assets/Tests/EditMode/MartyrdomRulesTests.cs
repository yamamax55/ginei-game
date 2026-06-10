using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 殉教の政治を固定する：劇的な死は生前の名声を超える強度を生む（死者が生者より強い）、
    /// 動員力は強度に比例して生前を超えうる、遺志の正統性は近さより語りの独占が決める、
    /// 風化はゆっくり・0で止まる、カルト判定の境界。入力クランプを担保。
    /// </summary>
    public class MartyrdomRulesTests
    {
        private static readonly MartyrdomParams P = MartyrdomParams.Default;
        // 劇的増幅1.0/動員スケール0.5/近さ重み0.4/独占重み0.6/風化0.01/正統性スケール0.3

        [Test]
        public void MartyrIntensity_DramaticDeathExceedsLivingRenown()
        {
            // 劇的な死＝生前の名声を超える（死者が生者より強い）
            Assert.AreEqual(2f, MartyrdomRules.MartyrIntensity(1f, 1f, P), 1e-5f);
            Assert.Greater(MartyrdomRules.MartyrIntensity(0.5f, 1f, P), 0.5f);
            // 平凡な死（drama=0）＝名声のまま（超えない）
            Assert.AreEqual(0.5f, MartyrdomRules.MartyrIntensity(0.5f, 0f, P), 1e-5f);
        }

        [Test]
        public void MartyrIntensity_ClampsInputs()
        {
            // 範囲外入力はクランプ＝最大でも 1×(1+1)=2
            Assert.AreEqual(2f, MartyrdomRules.MartyrIntensity(5f, 5f, P), 1e-5f);
            Assert.AreEqual(0f, MartyrdomRules.MartyrIntensity(-1f, 1f, P), 1e-5f);
        }

        [Test]
        public void MobilizationBonus_DeadOutmobilizeLiving()
        {
            Assert.AreEqual(1f, MartyrdomRules.MobilizationBonus(0f, P), 1e-5f);   // 殉教なし＝等倍
            Assert.AreEqual(2f, MartyrdomRules.MobilizationBonus(2f, P), 1e-5f);   // 最大強度＝2倍
            // 名声0.8の英雄：生前相当(強度=名声)より劇的死後（強度1.6）が強い
            float living = MartyrdomRules.MobilizationBonus(0.8f, P);
            float dead = MartyrdomRules.MobilizationBonus(MartyrdomRules.MartyrIntensity(0.8f, 1f, P), P);
            Assert.Greater(dead, living);
            // 負の強度は0扱い
            Assert.AreEqual(1f, MartyrdomRules.MobilizationBonus(-1f, P), 1e-5f);
        }

        [Test]
        public void LegacyClaimStrength_ControlBeatsProximity()
        {
            // 語りを独占した遠い者(0,1)＞近かったが語らない者(1,0)＝独占解釈が正統を決める
            Assert.AreEqual(0.6f, MartyrdomRules.LegacyClaimStrength(0f, 1f, P), 1e-5f);
            Assert.AreEqual(0.4f, MartyrdomRules.LegacyClaimStrength(1f, 0f, P), 1e-5f);
            Assert.Greater(MartyrdomRules.LegacyClaimStrength(0f, 1f, P),
                           MartyrdomRules.LegacyClaimStrength(1f, 0f, P));
            // 近さ＋独占の両取り＝満額1、範囲外はクランプ
            Assert.AreEqual(1f, MartyrdomRules.LegacyClaimStrength(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, MartyrdomRules.LegacyClaimStrength(-1f, -1f, P), 1e-5f);
        }

        [Test]
        public void SuccessionLegitimacyBonus_ScalesWithIntensityAndClaim()
        {
            // 完全独占×最大強度＝1×2×0.3=0.6
            Assert.AreEqual(0.6f, MartyrdomRules.SuccessionLegitimacyBonus(2f, 1f, 1f, P), 1e-5f);
            // 殉教者がいなければ（強度0）遺志の独占も無意味
            Assert.AreEqual(0f, MartyrdomRules.SuccessionLegitimacyBonus(0f, 1f, 1f, P), 1e-5f);
            // 偉大な殉教者ほど借りられる威光が大きい（強度に単調増加）
            Assert.Greater(MartyrdomRules.SuccessionLegitimacyBonus(2f, 0.5f, 0.5f, P),
                           MartyrdomRules.SuccessionLegitimacyBonus(1f, 0.5f, 0.5f, P));
        }

        [Test]
        public void IntensityTick_SlowDecayAndFloorsAtZero()
        {
            // 風化はゆっくり＝1時間で0.01しか減らない
            Assert.AreEqual(0.99f, MartyrdomRules.IntensityTick(1f, 1f, P), 1e-5f);
            // dt=0＝不変
            Assert.AreEqual(1f, MartyrdomRules.IntensityTick(1f, 0f, P), 1e-5f);
            // 長時間でも0で止まる（負にならない）
            Assert.AreEqual(0f, MartyrdomRules.IntensityTick(0.5f, 1000f, P), 1e-5f);
        }

        [Test]
        public void IsCultFigure_ThresholdBoundary()
        {
            Assert.IsTrue(MartyrdomRules.IsCultFigure(1.5f, 1.5f));   // ちょうど＝カルト化
            Assert.IsFalse(MartyrdomRules.IsCultFigure(1.49f, 1.5f));
            Assert.IsTrue(MartyrdomRules.IsCultFigure(2f, 1.5f));
        }
    }
}
