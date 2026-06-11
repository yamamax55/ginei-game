using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 帝国主義の還流（ブーメラン効果・#1522）の純ロジック検証。辺境の野蛮さ・ブーメラン効果・国内の急進化・
    /// 支配手法の移植・暴力の常態化・帰還兵の伝播・文明の侵食・還流カスケード判定を既定 Params で固定する。
    /// </summary>
    public class ImperialBlowbackRulesTests
    {
        /// <summary>辺境の野蛮さ＝暴力×説明責任の不在。誰も咎めないほど粗暴になる。</summary>
        [Test]
        public void FrontierBrutality_ScalesWithViolenceAndAccountabilityVoid()
        {
            // 暴力0.8 × 不在0.5 = 0.4
            Assert.AreEqual(0.4f, ImperialBlowbackRules.FrontierBrutality(0.8f, 0.5f), 1e-4f);
            // 説明責任が完全（不在0）なら暴力があっても歯止めが効き0
            Assert.AreEqual(0f, ImperialBlowbackRules.FrontierBrutality(1f, 0f), 1e-4f);
            // クランプ（過大入力）
            Assert.AreEqual(1f, ImperialBlowbackRules.FrontierBrutality(2f, 2f), 1e-4f);
        }

        /// <summary>ブーメラン効果＝本国が近い（連絡が密）ほど還流が速い。近接で加速・遠距離で等倍。</summary>
        [Test]
        public void BoomerangEffect_AcceleratesWhenHomeIsClose()
        {
            // 遠距離(dist=1)は等倍＝野蛮さそのまま
            Assert.AreEqual(0.4f, ImperialBlowbackRules.BoomerangEffect(0.4f, 1f), 1e-4f);
            // 近接(dist=0)は proximityAcceleration=1.5倍 → 0.4×1.5=0.6
            Assert.AreEqual(0.6f, ImperialBlowbackRules.BoomerangEffect(0.4f, 0f), 1e-4f);
            // 近いほど大きい
            Assert.Greater(ImperialBlowbackRules.BoomerangEffect(0.4f, 0f),
                           ImperialBlowbackRules.BoomerangEffect(0.4f, 1f));
        }

        /// <summary>国内急進化＝還流が政治を急進化させる。tick で増え1で頭打ち。</summary>
        [Test]
        public void HomeRadicalizationTick_GrowsWithBoomerang()
        {
            // 急進化0.2 + ブーメラン0.5×rate0.2×dt1 = 0.2 + 0.1 = 0.3
            Assert.AreEqual(0.3f, ImperialBlowbackRules.HomeRadicalizationTick(0.2f, 0.5f, 1f), 1e-4f);
            // 上限クランプ
            Assert.AreEqual(1f, ImperialBlowbackRules.HomeRadicalizationTick(0.95f, 1f, 5f), 1e-4f);
            // ブーメラン0なら不変
            Assert.AreEqual(0.3f, ImperialBlowbackRules.HomeRadicalizationTick(0.3f, 0f, 10f), 1e-4f);
        }

        /// <summary>支配手法の移植＝制度的記憶が濃いほど本国の制度に焼き付く。</summary>
        [Test]
        public void MethodTransfer_StrongerWithInstitutionalMemory()
        {
            // 記憶1なら野蛮さがそのまま移植 → 0.6×((1-0.6)+0.6×1)=0.6×1=0.6
            Assert.AreEqual(0.6f, ImperialBlowbackRules.MethodTransfer(0.6f, 1f), 1e-4f);
            // 記憶0でも基礎ぶん(1-memoryWeight=0.4)は移植 → 0.6×0.4=0.24
            Assert.AreEqual(0.24f, ImperialBlowbackRules.MethodTransfer(0.6f, 0f), 1e-4f);
            // 記憶が濃いほど多く移植される
            Assert.Greater(ImperialBlowbackRules.MethodTransfer(0.6f, 1f),
                           ImperialBlowbackRules.MethodTransfer(0.6f, 0f));
        }

        /// <summary>暴力の常態化＝急進化が進むほど暴力が本国でも日常になる。tick で蓄積。</summary>
        [Test]
        public void NormalizationOfViolence_AccumulatesWithRadicalism()
        {
            // 常態化0.1 + 急進化0.5×rate0.1×dt2 = 0.1 + 0.1 = 0.2
            Assert.AreEqual(0.2f, ImperialBlowbackRules.NormalizationOfViolence(0.5f, 0.1f, 2f), 1e-4f);
            // 上限クランプ
            Assert.AreEqual(1f, ImperialBlowbackRules.NormalizationOfViolence(1f, 0.95f, 10f), 1e-4f);
        }

        /// <summary>帰還兵の伝播＝帰還割合×身につけた野蛮さ。暴力に慣れた人間が媒体になる。</summary>
        [Test]
        public void VeteranContagion_ProductOfReturnAndLearnedBrutality()
        {
            // 帰還0.5 × 学習0.6 = 0.3
            Assert.AreEqual(0.3f, ImperialBlowbackRules.VeteranContagion(0.5f, 0.6f), 1e-4f);
            // 帰還0なら持ち込みなし
            Assert.AreEqual(0f, ImperialBlowbackRules.VeteranContagion(0f, 1f), 1e-4f);
        }

        /// <summary>文明の侵食＝暴力の常態化が本国の規範を辺境の論理に置き換える。</summary>
        [Test]
        public void CivilizationalErosion_TracksNormalization()
        {
            Assert.AreEqual(0.7f, ImperialBlowbackRules.CivilizationalErosion(0.7f), 1e-4f);
            Assert.AreEqual(0f, ImperialBlowbackRules.CivilizationalErosion(0f), 1e-4f);
            Assert.AreEqual(1f, ImperialBlowbackRules.CivilizationalErosion(2f), 1e-4f);
        }

        /// <summary>還流カスケード判定＝国内急進化が臨界（既定0.7）を超えたか。</summary>
        [Test]
        public void IsBlowbackCascade_FiresAtThreshold()
        {
            Assert.IsFalse(ImperialBlowbackRules.IsBlowbackCascade(0.6f));
            Assert.IsTrue(ImperialBlowbackRules.IsBlowbackCascade(0.7f));
            Assert.IsTrue(ImperialBlowbackRules.IsBlowbackCascade(0.9f));
            // 明示閾値
            Assert.IsTrue(ImperialBlowbackRules.IsBlowbackCascade(0.5f, 0.5f));
        }
    }
}
