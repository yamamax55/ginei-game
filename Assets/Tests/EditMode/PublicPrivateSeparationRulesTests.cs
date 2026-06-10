using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 公私の分離（国庫 vs 元首私財・#1035）を固定する：制度化が分離度を引き上げ（家産国家→近代国家）、
    /// 分離が弱く君主が貪欲なほど国庫が私財に漏れる（汚職の余地）、分離は正統性と継承の安定を生み、
    /// 公私分離は私腹を奪うため既得層が抵抗する。クランプを担保。
    /// </summary>
    public class PublicPrivateSeparationRulesTests
    {
        private static readonly PublicPrivateSeparationParams P = PublicPrivateSeparationParams.Default;
        // 基礎分離0.1/制度化重み0.9/私物化係数1.0/継承重み0.7/正統性重み0.6/既得抵抗係数1.0

        [Test]
        public void SeparationLevel_RisesWithInstitutionalization()
        {
            // 制度化ゼロ＝家産国家＝基礎分離のみ
            Assert.AreEqual(0.1f, PublicPrivateSeparationRules.SeparationLevel(0f, P), 1e-5f);
            // 制度化満タン＝近代国家＝0.1+0.9=1.0
            Assert.AreEqual(1f, PublicPrivateSeparationRules.SeparationLevel(1f, P), 1e-5f);
            // 中間＝0.1+0.5×0.9=0.55
            Assert.AreEqual(0.55f, PublicPrivateSeparationRules.SeparationLevel(0.5f, P), 1e-5f);
            // 入力クランプ
            Assert.AreEqual(1f, PublicPrivateSeparationRules.SeparationLevel(2f, P), 1e-5f);
            Assert.AreEqual(0.1f, PublicPrivateSeparationRules.SeparationLevel(-1f, P), 1e-5f);
        }

        [Test]
        public void PrivatizationRisk_WeakSeparationAndGreedyRuler()
        {
            // 分離ゼロ×貪欲満タン＝(1−0)×1×1＝確実に私物化（汚職の余地最大）
            Assert.AreEqual(1f, PublicPrivateSeparationRules.PrivatizationRisk(0f, 1f, P), 1e-5f);
            // 完全分離なら貪欲でも漏れない＝近代国家
            Assert.AreEqual(0f, PublicPrivateSeparationRules.PrivatizationRisk(1f, 1f, P), 1e-5f);
            // 清廉な君主なら分離が弱くても私物化しない
            Assert.AreEqual(0f, PublicPrivateSeparationRules.PrivatizationRisk(0f, 0f, P), 1e-5f);
            // 中間＝(1−0.5)×0.6＝0.3
            Assert.AreEqual(0.3f, PublicPrivateSeparationRules.PrivatizationRisk(0.5f, 0.6f, P), 1e-5f);
        }

        [Test]
        public void TreasuryLeakage_DrainsWhenSeparationWeak()
        {
            // 家産国家（分離0.1）＋貪欲な君主＝国庫1000の大半が私財へ流れる
            float leak = PublicPrivateSeparationRules.TreasuryLeakage(1000f, 0.1f, 1f, P);
            Assert.AreEqual(900f, leak, 1e-3f); // 1000×(1−0.1)×1
            // 近代国家（高分離）では漏出は小さい
            Assert.Less(PublicPrivateSeparationRules.TreasuryLeakage(1000f, 0.9f, 1f, P), leak);
            // 公金マイナスはゼロ扱い
            Assert.AreEqual(0f, PublicPrivateSeparationRules.TreasuryLeakage(-1000f, 0.1f, 1f, P), 1e-4f);
        }

        [Test]
        public void LegitimacyFromSeparation_RewardsRuleOfLaw()
        {
            // 分離なし＝正統性寄与なし
            Assert.AreEqual(0f, PublicPrivateSeparationRules.LegitimacyFromSeparation(0f, P), 1e-5f);
            // 完全分離＝1×0.6＝法の支配の土台
            Assert.AreEqual(0.6f, PublicPrivateSeparationRules.LegitimacyFromSeparation(1f, P), 1e-5f);
            // 分離が進むほど正統性は単調増
            Assert.Greater(PublicPrivateSeparationRules.LegitimacyFromSeparation(0.8f, P),
                           PublicPrivateSeparationRules.LegitimacyFromSeparation(0.3f, P));
        }

        [Test]
        public void SuccessionStability_PublicInstitutionPreventsInheritanceWar()
        {
            // 国家が君主の私物（分離ゼロ）＝相続争い＝(1−0.7)=0.3 と不安定
            Assert.AreEqual(0.3f, PublicPrivateSeparationRules.SuccessionStability(0f, P), 1e-5f);
            // 公的制度（完全分離）＝0.3+0.7=1.0 と円滑
            Assert.AreEqual(1f, PublicPrivateSeparationRules.SuccessionStability(1f, P), 1e-5f);
            // 分離が進むほど継承は安定＝制度化が継承戦争を防ぐ
            Assert.Greater(PublicPrivateSeparationRules.SuccessionStability(0.9f, P),
                           PublicPrivateSeparationRules.SuccessionStability(0.2f, P));
        }

        [Test]
        public void ReformResistanceFromElites_PrivilegeOpposesSeparation()
        {
            // 高分離を特権層に押し付ける＝私腹を奪う＝最大抵抗
            Assert.AreEqual(1f, PublicPrivateSeparationRules.ReformResistanceFromElites(1f, 1f, P), 1e-5f);
            // 特権がなければ抵抗なし
            Assert.AreEqual(0f, PublicPrivateSeparationRules.ReformResistanceFromElites(1f, 0f, P), 1e-5f);
            // 分離を進めるほど（私腹を奪うほど）抵抗は強い
            Assert.Greater(PublicPrivateSeparationRules.ReformResistanceFromElites(0.9f, 0.8f, P),
                           PublicPrivateSeparationRules.ReformResistanceFromElites(0.3f, 0.8f, P));
        }

        [Test]
        public void Params_CtorClamps()
        {
            var p = new PublicPrivateSeparationParams(2f, 2f, -1f, 2f, 2f, -1f);
            Assert.AreEqual(1f, p.baseSeparation, 1e-5f);        // 0..1
            Assert.AreEqual(1f, p.institutionWeight, 1e-5f);     // 0..1
            Assert.AreEqual(0f, p.privatizationScale, 1e-5f);    // 非負
            Assert.AreEqual(1f, p.successionWeight, 1e-5f);      // 0..1
            Assert.AreEqual(1f, p.legitimacyWeight, 1e-5f);      // 0..1
            Assert.AreEqual(0f, p.eliteResistanceScale, 1e-5f);  // 非負
        }

        [Test]
        public void Story_PatrimonialVsModernState()
        {
            // 家産国家（制度化0.1）＝低分離＝国庫漏出多・継承不安定
            float patrimonialSep = PublicPrivateSeparationRules.SeparationLevel(0.1f, P);
            // 近代国家（制度化0.9）＝高分離＝漏出僅少・継承円滑
            float modernSep = PublicPrivateSeparationRules.SeparationLevel(0.9f, P);
            Assert.Greater(modernSep, patrimonialSep);

            // 同じ貪欲な君主でも、近代国家は国庫を守る
            Assert.Greater(
                PublicPrivateSeparationRules.TreasuryLeakage(1000f, patrimonialSep, 0.8f, P),
                PublicPrivateSeparationRules.TreasuryLeakage(1000f, modernSep, 0.8f, P));
            // 近代国家は継承も安定＝制度化が相続争いを防ぐ
            Assert.Greater(
                PublicPrivateSeparationRules.SuccessionStability(modernSep, P),
                PublicPrivateSeparationRules.SuccessionStability(patrimonialSep, P));
        }
    }
}
