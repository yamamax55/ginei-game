using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// DiplomaticLeverageRules（外交的てこ＝交渉カードと圧力）の純ロジック検証。
    /// 既定 Params 期待値は手計算で固定（許容差 1e-4f／Sqrt 箇所のみ 1e-3f）。
    /// </summary>
    public class DiplomaticLeverageRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsSqrt = 1e-3f;

        [Test]
        public void MilitaryLeverage_AdvantageAndProximity()
        {
            // reach=Lerp(0.6,1,1)=1.0; 0.6*1*1=0.6
            Assert.AreEqual(0.6f, DiplomaticLeverageRules.MilitaryLeverage(1.0f, 1.0f), Eps);
            // reach=Lerp(0.6,1,0)=0.6; 0.6*1*0.6=0.36（遠ければてこは薄い）
            Assert.AreEqual(0.36f, DiplomaticLeverageRules.MilitaryLeverage(1.0f, 0.0f), Eps);
        }

        [Test]
        public void EconomicLeverage_DependencyTimesCapacity()
        {
            // 0.7*0.8*0.5=0.28
            Assert.AreEqual(0.28f, DiplomaticLeverageRules.EconomicLeverage(0.8f, 0.5f), Eps);
            // 制裁力ゼロなら効かない（積で相乗）
            Assert.AreEqual(0f, DiplomaticLeverageRules.EconomicLeverage(0.8f, 0.0f), Eps);
        }

        [Test]
        public void CoalitionLeverage_MassAndCommitment()
        {
            // sqrt(4)=2; mass=2/4=0.5; commitFactor=1; →0.5
            Assert.AreEqual(0.5f, DiplomaticLeverageRules.CoalitionLeverage(4, 1.0f), EpsSqrt);
            // 同盟国ゼロなら包囲なし
            Assert.AreEqual(0f, DiplomaticLeverageRules.CoalitionLeverage(0, 1.0f), Eps);
        }

        [Test]
        public void ExploitWeakness_VulnerabilityAndDivisions()
        {
            // split=Lerp(0.5,1,0.5)=0.75; 0.6*0.75=0.45
            Assert.AreEqual(0.45f, DiplomaticLeverageRules.ExploitWeakness(0.6f, 0.5f), Eps);
        }

        [Test]
        public void ThreatCredibility_CapabilityResolveTrackRecord()
        {
            // track=Lerp(0.5,1,1)=1; 0.8*0.7*1=0.56
            Assert.AreEqual(0.56f, DiplomaticLeverageRules.ThreatCredibility(0.8f, 0.7f, 1.0f), Eps);
            // 無実績なら半信：track=0.5; 0.8*0.7*0.5=0.28
            Assert.AreEqual(0.28f, DiplomaticLeverageRules.ThreatCredibility(0.8f, 0.7f, 0.0f), Eps);
        }

        [Test]
        public void TotalLeverage_WeightedIntegration()
        {
            // cShare=1-0.5-0.3=0.2; 0.6*0.5+0.28*0.3+0.5*0.2=0.3+0.084+0.1=0.484
            Assert.AreEqual(0.484f, DiplomaticLeverageRules.TotalLeverage(0.6f, 0.28f, 0.5f), Eps);
        }

        [Test]
        public void ExertionCost_LeverageTimesEscalation()
        {
            // 0.5*0.484*0.5=0.121（かけ過ぎは自分も傷つく）
            Assert.AreEqual(0.121f, DiplomaticLeverageRules.ExertionCost(0.484f, 0.5f), Eps);
            // エスカレーションゼロならコストなし
            Assert.AreEqual(0f, DiplomaticLeverageRules.ExertionCost(0.484f, 0.0f), Eps);
        }

        [Test]
        public void Concession_PressureVersusResolve()
        {
            // pressure=0.484*0.56=0.27104; denom=0.67104; →0.403910...
            Assert.AreEqual(0.403910f, DiplomaticLeverageRules.Concession(0.484f, 0.56f, 0.4f), Eps);
            // 圧力も決意もゼロなら譲歩0（ゼロ除算回避）
            Assert.AreEqual(0f, DiplomaticLeverageRules.Concession(0f, 0f, 0f), Eps);
        }

        [Test]
        public void ClampsAndNullSafety()
        {
            // 入力は範囲外でもクランプ（負・超過）
            Assert.AreEqual(0.6f, DiplomaticLeverageRules.MilitaryLeverage(5f, 9f), Eps);
            Assert.AreEqual(0f, DiplomaticLeverageRules.MilitaryLeverage(-3f, -3f), Eps);
            // 負の同盟国数は0扱い
            Assert.AreEqual(0f, DiplomaticLeverageRules.CoalitionLeverage(-7, 1f), Eps);
            // 全出力は 0..1 に収まる
            float c = DiplomaticLeverageRules.Concession(2f, 2f, -1f);
            Assert.GreaterOrEqual(c, 0f);
            Assert.LessOrEqual(c, 1f);
        }

        [Test]
        public void Narrative_StrongHandExtractsConcessionsButCostsToOverpress()
        {
            // 物語：軍事優位かつ隣接、相手は貿易依存、強固な連合を持つ覇権側が、
            // 信憑性ある脅しで決意の弱い相手から大きな譲歩を引き出す。だが圧力をかけ過ぎると行使コストも嵩む。
            var p = DiplomaticLeverageParams.Default;

            float mil = DiplomaticLeverageRules.MilitaryLeverage(0.9f, 0.9f, p);   // 軍事優位×近接
            float eco = DiplomaticLeverageRules.EconomicLeverage(0.8f, 0.8f, p);   // 相手は依存
            float coal = DiplomaticLeverageRules.CoalitionLeverage(5, 0.9f, p);    // 結束した連合
            float total = DiplomaticLeverageRules.TotalLeverage(mil, eco, coal, p);

            float cred = DiplomaticLeverageRules.ThreatCredibility(0.9f, 0.9f, 0.9f, p); // 信じられる脅し
            float concWeak = DiplomaticLeverageRules.Concession(total, cred, 0.2f, p);   // 決意の弱い相手
            float concFirm = DiplomaticLeverageRules.Concession(total, cred, 0.9f, p);   // 決意の固い相手

            // 決意が弱い相手からはより多くの譲歩を引き出せる。
            Assert.Greater(concWeak, concFirm);
            Assert.Greater(concWeak, 0.5f);

            // てこを総動員すると相応の譲歩が得られる。
            Assert.Greater(total, 0.4f);

            // 高エスカレーション下での行使はコストが嵩む（かけ過ぎは自分も傷つく）。
            float costHigh = DiplomaticLeverageRules.ExertionCost(total, 0.9f, p);
            float costLow = DiplomaticLeverageRules.ExertionCost(total, 0.1f, p);
            Assert.Greater(costHigh, costLow);

            // 信憑性のない脅し（実績ゼロ・決意ゼロ）では譲歩は乏しい。
            float bluff = DiplomaticLeverageRules.ThreatCredibility(0.9f, 0.0f, 0.0f, p);
            float concBluff = DiplomaticLeverageRules.Concession(total, bluff, 0.9f, p);
            Assert.Less(concBluff, concFirm);
        }
    }
}
