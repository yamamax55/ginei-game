using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class PoliticalPurgeRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void PurgeScope_DefaultParams_CombinesThreatAndParanoia()
        {
            // 0.8*0.5 + 0.6*0.5 = 0.4 + 0.3 = 0.7
            float scope = PoliticalPurgeRules.PurgeScope(0.8f, 0.6f);
            Assert.AreEqual(0.7f, scope, Eps);
        }

        [Test]
        public void Intimidation_DefaultParams_ScalesScopeByVisibility()
        {
            // 0.7 * (1.0*0.8) = 0.56
            float intim = PoliticalPurgeRules.Intimidation(0.7f, 1.0f);
            Assert.AreEqual(0.56f, intim, Eps);
        }

        [Test]
        public void TalentDrain_CompetentTargetsDrainMore()
        {
            // 0.7 * (1.0*0.7) = 0.49（有能）／ 0.7 * (0.2*0.7) = 0.098（無能）
            float competent = PoliticalPurgeRules.TalentDrain(0.7f, 1.0f);
            float incompetent = PoliticalPurgeRules.TalentDrain(0.7f, 0.2f);
            Assert.AreEqual(0.49f, competent, Eps);
            Assert.AreEqual(0.098f, incompetent, Eps);
            Assert.Greater(competent, incompetent);
        }

        [Test]
        public void DenunciationIncentive_DefaultParams_NeedsBothFearAndReward()
        {
            // 0.56 * (1.0*0.6) = 0.336
            float incent = PoliticalPurgeRules.DenunciationIncentive(0.56f, 1.0f);
            Assert.AreEqual(0.336f, incent, Eps);
            // 報奨ゼロなら密告は奨励されない
            float noReward = PoliticalPurgeRules.DenunciationIncentive(0.56f, 0f);
            Assert.AreEqual(0f, noReward, Eps);
        }

        [Test]
        public void SurfaceCompliance_DefaultParams_SaturatesWithIntimidation()
        {
            // exponent = 1/(1+1.5) = 0.4; 0.56^0.4 ≈ 0.7930
            float compliance = PoliticalPurgeRules.SurfaceCompliance(0.56f);
            Assert.AreEqual(0.7930f, compliance, PowEps);
            // 威圧ゼロなら服従ゼロ、最大威圧なら最大服従
            Assert.AreEqual(0f, PoliticalPurgeRules.SurfaceCompliance(0f), Eps);
            Assert.AreEqual(1f, PoliticalPurgeRules.SurfaceCompliance(1f), Eps);
        }

        [Test]
        public void InwardDefection_ExcessivePurgeBreedsResentment()
        {
            // (0.7+0.49)*0.5 = 0.595; *0.8 = 0.476
            float defection = PoliticalPurgeRules.InwardDefection(0.7f, 0.49f);
            Assert.AreEqual(0.476f, defection, Eps);
            // 粛清なし・枯渇なしなら離反なし
            Assert.AreEqual(0f, PoliticalPurgeRules.InwardDefection(0f, 0f), Eps);
        }

        [Test]
        public void PurgeSelfPoisoning_FalseAccusationsDriveSpiral()
        {
            // 0.7 * (0.5*0.7) = 0.245
            float poison = PoliticalPurgeRules.PurgeSelfPoisoning(0.7f, 0.5f);
            Assert.AreEqual(0.245f, poison, Eps);
            // 冤罪ゼロなら自家中毒は起きない
            Assert.AreEqual(0f, PoliticalPurgeRules.PurgeSelfPoisoning(0.7f, 0f), Eps);
        }

        [Test]
        public void IsRegimeStabilized_ComplianceMustOutweighDefection()
        {
            // 0.793 - 0.476 = 0.317 >= 0.2 → true
            Assert.IsTrue(PoliticalPurgeRules.IsRegimeStabilized(0.793f, 0.476f));
            // 離反が拮抗すれば不安定
            Assert.IsFalse(PoliticalPurgeRules.IsRegimeStabilized(0.5f, 0.45f));
        }

        [Test]
        public void Inputs_AreClamped_NoOverflow()
        {
            // 範囲外入力でも 0..1 に収まる
            float scope = PoliticalPurgeRules.PurgeScope(5f, 5f);
            Assert.AreEqual(1f, scope, Eps);
            float intim = PoliticalPurgeRules.Intimidation(-3f, 9f);
            Assert.AreEqual(0f, intim, Eps);
        }

        [Test]
        public void Narrative_TyrantParanoiaSpiralsIntoSelfDestruction()
        {
            // 物語：脅威に怯える暴君が広く粛清し恐怖で統べるが、有能な者を斬り冤罪を重ねた末に
            // 表面の服従の裏で離反が育ち、疑心暗鬼の自家中毒へ陥る。
            var p = PoliticalPurgeParams.Default;

            // 高い脅威認識と深い猜疑 → 広範な粛清
            float scope = PoliticalPurgeRules.PurgeScope(0.9f, 0.95f, p);
            Assert.Greater(scope, 0.8f);

            // 公開処刑で見せしめ → 強い威圧
            float intim = PoliticalPurgeRules.Intimidation(scope, 1.0f, p);
            Assert.Greater(intim, 0.6f);

            // 有能な政敵を斬る → 深刻な人材枯渇
            float drain = PoliticalPurgeRules.TalentDrain(scope, 0.9f, p);
            Assert.Greater(drain, 0.5f);

            // 報奨つき密告社会化
            float denounce = PoliticalPurgeRules.DenunciationIncentive(intim, 0.9f, p);
            Assert.Greater(denounce, 0.3f);

            // 表面は服従するが…
            float compliance = PoliticalPurgeRules.SurfaceCompliance(intim, p);
            Assert.Greater(compliance, 0.7f);

            // 内心は深く離反する（過剰な粛清＋人材喪失）
            float defection = PoliticalPurgeRules.InwardDefection(scope, drain, p);
            Assert.Greater(defection, 0.5f);

            // 冤罪まみれの粛清は自家中毒へ
            float poison = PoliticalPurgeRules.PurgeSelfPoisoning(scope, 0.8f, p);
            Assert.Greater(poison, 0.4f);

            // 表面服従と内心離反が拮抗 → 真の安定には至らない（恐怖は忠誠を買えない）
            // compliance ≈ 0.83, defection ≈ 0.65 → 差 ≈ 0.18 < 閾値0.3
            Assert.IsFalse(PoliticalPurgeRules.IsRegimeStabilized(compliance, defection, 0.3f));
        }
    }
}
