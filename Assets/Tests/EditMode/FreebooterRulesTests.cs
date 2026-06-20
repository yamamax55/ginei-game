using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 無頼の冒険者（独立傭兵／宇宙海賊）を固定する：腕×経験の幾何平均で技量、報酬/(1+リスク) で割の良さ、武勇伝×悪名で
    /// 両刃の評判、信条＋前金で信頼性、(1-信義)×高値で裏切り、罪×追跡でお尋ね者、無法×足のつかなさで汚れ仕事、
    /// 技量×信頼−裏切りで使い勝手。既定Params期待値・境界・クランプ・決定論を担保。
    /// </summary>
    public class FreebooterRulesTests
    {
        private static readonly FreebooterParams P = FreebooterParams.Default; // 基準100/技量1/リスク1/武勇0.6/悪名0.4/信頼下限0.2/前金0.5/裏切り1

        [Test]
        public void MercenarySkill_GeometricMeanOfProwessAndExperience()
        {
            Assert.AreEqual(1f, FreebooterRules.MercenarySkill(100f, 100f, P), 1e-3f);
            Assert.AreEqual(0.5f, FreebooterRules.MercenarySkill(100f, 25f, P), 1e-3f);   // sqrt(1*0.25)=0.5
            Assert.AreEqual(0f, FreebooterRules.MercenarySkill(0f, 100f, P), 1e-3f);      // 片輪は崩れる
        }

        [Test]
        public void JobOpportunism_RewardOverRisk()
        {
            Assert.AreEqual(1f, FreebooterRules.JobOpportunism(1f, 0f, P), 1e-4f);              // 危険ゼロは満額
            Assert.AreEqual(0.5f, FreebooterRules.JobOpportunism(1f, 1f, P), 1e-4f);            // 1/(1+1)
            Assert.AreEqual(0.8f / 1.5f, FreebooterRules.JobOpportunism(0.8f, 0.5f, P), 1e-4f); // 0.5333
        }

        [Test]
        public void Reputation_DoubleEdgedBlendOfDeedsAndInfamy()
        {
            Assert.AreEqual(0.6f, FreebooterRules.Reputation(100f, 0f, P), 1e-4f);   // 武勇伝のみ
            Assert.AreEqual(0.4f, FreebooterRules.Reputation(0f, 100f, P), 1e-4f);   // 悪名のみ
            Assert.AreEqual(1f, FreebooterRules.Reputation(100f, 100f, P), 1e-4f);   // 両取り
            Assert.AreEqual(0.5f, FreebooterRules.Reputation(50f, 50f, P), 1e-4f);
        }

        [Test]
        public void Reliability_CodeFloorBoostedByUpfront()
        {
            Assert.AreEqual(0.2f, FreebooterRules.Reliability(0f, 0f, P), 1e-4f);   // 信条なし・前金なし＝下限
            Assert.AreEqual(1f, FreebooterRules.Reliability(1f, 0f, P), 1e-4f);     // 信条満点
            Assert.AreEqual(0.6f, FreebooterRules.Reliability(0f, 1f, P), 1e-4f);   // 前金が信用を買う 0.2+0.5*0.8
            Assert.AreEqual(0.8f, FreebooterRules.Reliability(0.5f, 1f, P), 1e-4f); // 0.6+0.5*0.4
        }

        [Test]
        public void BetrayalRisk_DistrustTimesHigherBidder()
        {
            Assert.AreEqual(1f, FreebooterRules.BetrayalRisk(0f, 1f, P), 1e-4f);     // 信義なし×高値
            Assert.AreEqual(0f, FreebooterRules.BetrayalRisk(1f, 1f, P), 1e-4f);     // 信義満点は寝返らない
            Assert.AreEqual(0.25f, FreebooterRules.BetrayalRisk(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void Outlawry_CrimesTimesPursuit()
        {
            Assert.AreEqual(1f, FreebooterRules.Outlawry(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.2f, FreebooterRules.Outlawry(0.5f, 0.4f, P), 1e-4f);
            Assert.AreEqual(0f, FreebooterRules.Outlawry(1f, 0f, P), 1e-4f); // 追われなければ野放し
        }

        [Test]
        public void DeniableOperations_OutlawryTimesUntraceability()
        {
            Assert.AreEqual(1f, FreebooterRules.DeniableOperations(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0.4f, FreebooterRules.DeniableOperations(0.8f, 0.5f, P), 1e-4f);
            Assert.AreEqual(0f, FreebooterRules.DeniableOperations(1f, 0f, P), 1e-4f); // 足がつけば使えない
        }

        [Test]
        public void FreebooterUtility_SkillTimesReliabilityMinusBetrayal()
        {
            Assert.AreEqual(1f, FreebooterRules.FreebooterUtility(1f, 1f, 0f, P), 1e-4f);    // 腕・信頼満点
            Assert.AreEqual(0f, FreebooterRules.FreebooterUtility(1f, 1f, 1f, P), 1e-4f);    // 裏切りが価値を食う
            Assert.AreEqual(0.2f, FreebooterRules.FreebooterUtility(0.5f, 0.8f, 0.2f, P), 1e-4f); // 0.4-0.2
            Assert.AreEqual(-0.9f, FreebooterRules.FreebooterUtility(0.2f, 0.5f, 1f, P), 1e-4f);  // 雇うほど損（負値）
        }

        [Test]
        public void Params_ClampNegativesAndOutOfRange()
        {
            var c = new FreebooterParams(-5f, -1f, -2f, -0.3f, -0.4f, 5f, 5f, -2f);
            Assert.GreaterOrEqual(c.statMax, 1e-4f); // 0割回避
            Assert.AreEqual(0f, c.skillScale, 1e-4f);
            Assert.AreEqual(0f, c.riskWeight, 1e-4f);
            Assert.AreEqual(0f, c.deedsWeight, 1e-4f);
            Assert.AreEqual(0f, c.infamyWeight, 1e-4f);
            Assert.AreEqual(1f, c.minReliability, 1e-4f);   // Clamp01
            Assert.AreEqual(1f, c.upfrontWeight, 1e-4f);    // Clamp01
            Assert.AreEqual(0f, c.betrayalPenalty, 1e-4f);
        }

        [Test]
        public void Inputs_ClampOutOfRange()
        {
            // 負・超過入力がクランプされ NaN/暴走しない
            Assert.AreEqual(0f, FreebooterRules.JobOpportunism(-1f, -1f, P), 1e-4f);   // 報酬0
            Assert.AreEqual(1f, FreebooterRules.MercenarySkill(999f, 999f, P), 1e-3f); // 正規化で頭打ち
            Assert.AreEqual(0f, FreebooterRules.BetrayalRisk(2f, 2f, P), 1e-4f);       // 信義1で0
            Assert.AreEqual(1f, FreebooterRules.Reliability(2f, 2f, P), 1e-4f);        // 信条1で満点
        }

        [Test]
        public void Story_DeniableKnife_vs_LoyalBlade()
        {
            // 物語：足のつかない汚れ仕事に長けた裏切り者の無頼 vs 信義に厚い腕利き
            // (1) お尋ね者で足のつかない無法者＝国家にとって使い捨ての汚れ仕事に高い価値
            float outlawry = FreebooterRules.Outlawry(0.9f, 0.9f, P);              // 罪深く追われている
            float deniable = FreebooterRules.DeniableOperations(outlawry, 0.9f, P); // が足がつかない
            Assert.Greater(deniable, 0.5f);

            // (2) しかし信義が薄く高値に弱い＝雇い手側の使い勝手は裏切りで沈む
            float skillTraitor = FreebooterRules.MercenarySkill(90f, 90f, P);
            float relTraitor = FreebooterRules.Reliability(0.1f, 0f, P);
            float betrayTraitor = FreebooterRules.BetrayalRisk(0.1f, 0.9f, P);     // 信義薄×高値
            float uTraitor = FreebooterRules.FreebooterUtility(skillTraitor, relTraitor, betrayTraitor, P);

            // (3) 同じ腕でも信義に厚く前金を受けた無頼は寝返らない＝使い勝手は上
            float skillLoyal = FreebooterRules.MercenarySkill(90f, 90f, P);
            float relLoyal = FreebooterRules.Reliability(0.8f, 1f, P);
            float betrayLoyal = FreebooterRules.BetrayalRisk(0.9f, 0.9f, P);       // 信義厚く動じない
            float uLoyal = FreebooterRules.FreebooterUtility(skillLoyal, relLoyal, betrayLoyal, P);

            Assert.Greater(uLoyal, uTraitor); // 信義が使い勝手を分ける
        }
    }
}
