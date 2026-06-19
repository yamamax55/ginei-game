using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>特殊部隊襲撃の純ロジック（#特殊部隊襲撃）EditMode テスト。既定 Params で期待値を固定。</summary>
    public class SpecialForcesRaidRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を含む箇所のみ緩める

        [Test]
        public void InfiltrationSuccess_RatioAndEdges()
        {
            // 60/(60+40)=0.6, pow(0.6,1)=0.6
            Assert.AreEqual(0.6f, SpecialForcesRaidRules.InfiltrationSuccess(60f, 40f), PowEps);
            Assert.AreEqual(1f, SpecialForcesRaidRules.InfiltrationSuccess(50f, 0f), Eps);  // 警戒皆無＝完全浸透
            Assert.AreEqual(0f, SpecialForcesRaidRules.InfiltrationSuccess(0f, 50f), Eps);  // 隠密皆無＝浸透不能
        }

        [Test]
        public void SurpriseAchieved_IsProductOfInfilAndTiming()
        {
            // 0.6 * 0.5 = 0.3
            Assert.AreEqual(0.3f, SpecialForcesRaidRules.SurpriseAchieved(0.6f, 0.5f), Eps);
            Assert.AreEqual(0f, SpecialForcesRaidRules.SurpriseAchieved(0f, 1f), Eps);   // 浸透ゼロ＝奇襲ゼロ
        }

        [Test]
        public void TargetStrike_SurpriseTimesPenetration()
        {
            // 部隊80/(80+20)=0.8 → 0.5*1*0.8 = 0.4
            Assert.AreEqual(0.4f, SpecialForcesRaidRules.TargetStrike(0.5f, 80f, 20f), Eps);
            Assert.AreEqual(0f, SpecialForcesRaidRules.TargetStrike(0f, 80f, 20f), Eps);   // 奇襲ゼロ＝打撃ゼロ
            Assert.AreEqual(0.5f, SpecialForcesRaidRules.TargetStrike(0.5f, 80f, 0f), Eps); // 堅固0＝部隊満額
        }

        [Test]
        public void Exfiltration_PlanAndEnemyResponse()
        {
            // pow(1,1)=1 → 0.8 * 1 * (1-0.25) = 0.6
            Assert.AreEqual(0.6f, SpecialForcesRaidRules.Exfiltration(0.8f, 1f, 0.25f), PowEps);
            Assert.AreEqual(0f, SpecialForcesRaidRules.Exfiltration(0.8f, 1f, 1f), Eps);   // 退路封鎖＝脱出ゼロ
            Assert.AreEqual(0f, SpecialForcesRaidRules.Exfiltration(0f, 1f, 0f), Eps);     // 浸透ゼロ＝戻れない
        }

        [Test]
        public void RaidSuccess_IsProductOfStrikeAndExfil()
        {
            // 0.4 * 0.6 = 0.24
            Assert.AreEqual(0.24f, SpecialForcesRaidRules.RaidSuccess(0.4f, 0.6f), Eps);
            Assert.AreEqual(0f, SpecialForcesRaidRules.RaidSuccess(0.9f, 0f), Eps);   // 戻れなければ不成立
        }

        [Test]
        public void LossOnFailure_NoSurpriseHighResponse()
        {
            // (1-0.3)*0.8 = 0.56
            Assert.AreEqual(0.56f, SpecialForcesRaidRules.LossOnFailure(0.3f, 0.8f), Eps);
            Assert.AreEqual(0f, SpecialForcesRaidRules.LossOnFailure(1f, 1f), Eps);   // 完全奇襲＝損失ゼロ
        }

        [Test]
        public void AsymmetricImpact_LeveragesTargetValue()
        {
            // 0.4 * 0.9 * 1 = 0.36
            Assert.AreEqual(0.36f, SpecialForcesRaidRules.AsymmetricImpact(0.4f, 0.9f), Eps);
            Assert.AreEqual(0f, SpecialForcesRaidRules.AsymmetricImpact(0.4f, 0f), Eps); // 価値ゼロ＝戦果ゼロ
        }

        [Test]
        public void IsRaidWorthwhile_NetGainVsThreshold()
        {
            // 0.8-0.1=0.7 >= 0 → true
            Assert.IsTrue(SpecialForcesRaidRules.IsRaidWorthwhile(0.8f, 0.1f));
            // 0.24-0.56=-0.32 >= 0 → false
            Assert.IsFalse(SpecialForcesRaidRules.IsRaidWorthwhile(0.24f, 0.56f));
        }

        [Test]
        public void Narrative_DaringRaidOnEnemyHeadquarters()
        {
            // 精鋭が薄い警戒の敵地へ浸透：隠密80 vs 警戒20 → 0.8
            float infil = SpecialForcesRaidRules.InfiltrationSuccess(80f, 20f);
            Assert.AreEqual(0.8f, infil, PowEps);

            // 完璧なタイミングで奇襲：0.8 * 0.9 = 0.72
            float surprise = SpecialForcesRaidRules.SurpriseAchieved(infil, 0.9f);
            Assert.AreEqual(0.72f, surprise, Eps);

            // 高能力の部隊で堅固な司令部へ打撃：90/(90+30)=0.75 → 0.72*0.75 = 0.54
            float strike = SpecialForcesRaidRules.TargetStrike(surprise, 90f, 30f);
            Assert.AreEqual(0.72f * 0.75f, strike, Eps);

            // 周到な脱出計画、敵対応は鈍い：0.8 * 1 * (1-0.2) = 0.64
            float exfil = SpecialForcesRaidRules.Exfiltration(infil, 1f, 0.2f);
            Assert.AreEqual(0.64f, exfil, PowEps);

            // 作戦成否：0.54 * 0.64 = 0.3456
            float success = SpecialForcesRaidRules.RaidSuccess(strike, exfil);
            Assert.AreEqual((0.72f * 0.75f) * 0.64f, success, Eps);

            // 高価値の司令部＝非対称な戦果：0.54 * 0.95 = 0.513
            float impact = SpecialForcesRaidRules.AsymmetricImpact(strike, 0.95f);
            Assert.AreEqual((0.72f * 0.75f) * 0.95f, impact, Eps);

            // 失敗時損失は小さい（奇襲が高く敵対応も鈍い）：(1-0.72)*0.3 = 0.084
            float loss = SpecialForcesRaidRules.LossOnFailure(surprise, 0.3f);
            Assert.AreEqual((1f - 0.72f) * 0.3f, loss, Eps);

            // 正味は成功>損失＝割に合う作戦
            Assert.IsTrue(SpecialForcesRaidRules.IsRaidWorthwhile(success, loss));
        }
    }
}
