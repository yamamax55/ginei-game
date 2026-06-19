using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 密貿易（フェザーン型の密輸）純ロジックの EditMode テスト。既定 Params で期待値を固定する。
    /// </summary>
    public class SmugglingRulesTests
    {
        const float Eps = 1e-4f;
        // Sqrt を含む箇所はわずかに緩める。
        const float SqrtEps = 1e-3f;

        [Test]
        public void RouteConcealment_KnowledgeAndSkill_GeometricMean()
        {
            // rk=0.64, es=0.81 -> sqrt(0.64*0.81)=sqrt(0.5184)=0.72
            float conceal = SmugglingRules.RouteConcealment(64f, 81f);
            Assert.AreEqual(0.72f, conceal, SqrtEps);
        }

        [Test]
        public void RouteConcealment_EitherZero_Collapses()
        {
            // どちらかが0なら幾何平均は0＝丸見え。
            Assert.AreEqual(0f, SmugglingRules.RouteConcealment(0f, 100f), Eps);
            Assert.AreEqual(0f, SmugglingRules.RouteConcealment(90f, 0f), Eps);
        }

        [Test]
        public void ContrabandMargin_ScarceAndEmbargoed_HighPremium()
        {
            // sc=0.8, es=0.5 -> 0.2 + 1*0.8*0.5 = 0.6
            float margin = SmugglingRules.ContrabandMargin(80f, 50f);
            Assert.AreEqual(0.6f, margin, Eps);
        }

        [Test]
        public void ContrabandMargin_NoScarcity_BaseOnly()
        {
            // sc=0 -> 利幅は基礎0.2 のみ。
            Assert.AreEqual(0.2f, SmugglingRules.ContrabandMargin(0f, 100f), Eps);
        }

        [Test]
        public void InterdictionChance_HeavyPatrolThinCover_HighChance()
        {
            // pd=1.0, conceal=0.72 -> 1*1*(1-0.72)=0.28
            float chance = SmugglingRules.InterdictionChance(100f, 0.72f);
            Assert.AreEqual(0.28f, chance, Eps);
        }

        [Test]
        public void InterdictionChance_PerfectConcealment_NoInterdiction()
        {
            // 隠密性1.0なら (1-1)=0 で摘発されない。
            Assert.AreEqual(0f, SmugglingRules.InterdictionChance(100f, 1f), Eps);
        }

        [Test]
        public void BribeEvasion_BribeMeetsCorruption_Evades()
        {
            // bribe=0.5, corruption=0.4 -> 0.5*0.4=0.2
            float ev = SmugglingRules.BribeEvasion(50f, 40f);
            Assert.AreEqual(0.2f, ev, Eps);
        }

        [Test]
        public void BribeEvasion_IncorruptibleOfficial_NoEvasion()
        {
            // 腐敗0なら賄賂が通じない。
            Assert.AreEqual(0f, SmugglingRules.BribeEvasion(100f, 0f), Eps);
        }

        [Test]
        public void EffectiveInterdiction_BribeDiscountsInterdiction()
        {
            // ic=0.28, be=0.2 -> 0.28*(1-0.2)=0.224
            float ei = SmugglingRules.EffectiveInterdiction(0.28f, 0.2f);
            Assert.AreEqual(0.224f, ei, Eps);
        }

        [Test]
        public void SeizureLoss_InterdictedCargo_LosesValue()
        {
            // ei=0.224, value=1000 -> 0.224*1000=224
            float loss = SmugglingRules.SeizureLoss(0.224f, 1000f);
            Assert.AreEqual(224f, loss, 1e-2f);
        }

        [Test]
        public void SmugglerReliability_RepAndStability_GeometricMean()
        {
            // rep=0.81, stab=0.64 -> sqrt(0.81*0.64)=0.72
            float rel = SmugglingRules.SmugglerReliability(81f, 64f);
            Assert.AreEqual(0.72f, rel, SqrtEps);
        }

        [Test]
        public void ExpectedProfit_ThickMarginLowRisk_Profitable()
        {
            // margin=0.6, value=1000, ei=0.224 -> 0.6*1000*(1-0.224)=465.6
            float profit = SmugglingRules.ExpectedProfit(0.6f, 1000f, 0.224f);
            Assert.AreEqual(465.6f, profit, 1e-2f);
        }

        [Test]
        public void Story_FezzanRunsContraband_BribesPastTheBlockade()
        {
            // フェザーンの密貿易商：禁輸された希少品を、腐敗した検問へ賄賂を握らせて闇に流す物語。
            var p = SmugglingParams.Default;

            // 熟知したルートと巧みな回避で高い隠密性。
            float conceal = SmugglingRules.RouteConcealment(90f, 80f, p);
            Assert.Greater(conceal, 0.8f);

            // 厳しい禁輸下の希少品＝厚い利幅。
            float margin = SmugglingRules.ContrabandMargin(85f, 90f, p);
            Assert.Greater(margin, 0.7f);

            // 哨戒は濃いが、隠密ですり抜けて素の摘発確率は低め。
            float ic = SmugglingRules.InterdictionChance(80f, conceal, p);

            // 腐敗した検問へ賄賂＝見逃しが成立。
            float bribe = SmugglingRules.BribeEvasion(70f, 75f, p);
            Assert.Greater(bribe, 0.4f);

            // 賄賂で実効摘発率はさらに下がる。
            float ei = SmugglingRules.EffectiveInterdiction(ic, bribe, p);
            Assert.Less(ei, ic);

            // 押収損失は小さく、信頼できる組織で取引は履行され、期待利益は厚い。
            float loss = SmugglingRules.SeizureLoss(ei, 1000f, p);
            float rel = SmugglingRules.SmugglerReliability(85f, 80f, p);
            float profit = SmugglingRules.ExpectedProfit(margin, 1000f, ei, p);
            Assert.Greater(rel, 0.7f);
            Assert.Greater(profit, loss);
            Assert.Greater(profit, 600f);
        }
    }
}
