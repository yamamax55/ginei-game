using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 傭兵団ロジック（MercenaryCompanyRules）の EditMode テスト。既定 Params の期待値を手計算で固定。
    /// </summary>
    public class MercenaryCompanyRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void CombatQuality_経験と装備の加重平均()
        {
            // 0.8*0.6 + 0.5*0.4 = 0.68
            Assert.AreEqual(0.68f, MercenaryCompanyRules.CombatQuality(0.8f, 0.5f), Tol);
            // clamp：範囲外入力でも 0..1
            Assert.AreEqual(1f, MercenaryCompanyRules.CombatQuality(2f, 2f), Tol);
            Assert.AreEqual(0f, MercenaryCompanyRules.CombatQuality(-1f, -1f), Tol);
        }

        [Test]
        public void ContractCost_品質と規模で嵩む()
        {
            // 0.68*1000*1 = 680
            Assert.AreEqual(680f, MercenaryCompanyRules.ContractCost(0.68f, 1000f), Tol);
            // 規模0なら無料、負規模も0
            Assert.AreEqual(0f, MercenaryCompanyRules.ContractCost(0.68f, 0f), Tol);
            Assert.AreEqual(0f, MercenaryCompanyRules.ContractCost(0.68f, -50f), Tol);
        }

        [Test]
        public void Loyalty_支払いと分け前で買う()
        {
            // 0.9*0.7 + 0.4*0.3 = 0.75
            Assert.AreEqual(0.75f, MercenaryCompanyRules.Loyalty(0.9f, 0.4f), Tol);
            // 無給・無分け前は忠誠ゼロ
            Assert.AreEqual(0f, MercenaryCompanyRules.Loyalty(0f, 0f), Tol);
            // 満額・満分け前は1
            Assert.AreEqual(1f, MercenaryCompanyRules.Loyalty(1f, 1f), Tol);
        }

        [Test]
        public void DefectionRisk_買収と遅配で上がる()
        {
            // disloyal=0.25; pressure=1+0.3+0.2=1.5; 0.25*1.5 = 0.375
            Assert.AreEqual(0.375f, MercenaryCompanyRules.DefectionRisk(0.75f, 0.6f, 0.4f), Tol);
            // 忠誠満点なら買収・遅配があっても寝返らない
            Assert.AreEqual(0f, MercenaryCompanyRules.DefectionRisk(1f, 1f, 1f), Tol);
            // 忠誠ゼロ＋満額買収＋満額遅配は上限1へ clamp
            Assert.AreEqual(1f, MercenaryCompanyRules.DefectionRisk(0f, 1f, 1f), Tol);
        }

        [Test]
        public void Reputation_戦績と信頼性()
        {
            // 0.7*0.6 + 0.5*0.4 = 0.62
            Assert.AreEqual(0.62f, MercenaryCompanyRules.Reputation(0.7f, 0.5f), Tol);
            Assert.AreEqual(0f, MercenaryCompanyRules.Reputation(0f, 0f), Tol);
        }

        [Test]
        public void RecruitmentDraw_評判と報酬で人を集める()
        {
            // 0.8*0.5 + 0.62*0.5 = 0.71
            Assert.AreEqual(0.71f, MercenaryCompanyRules.RecruitmentDraw(0.62f, 0.8f), Tol);
            // 無名・無報酬は誰も来ない
            Assert.AreEqual(0f, MercenaryCompanyRules.RecruitmentDraw(0f, 0f), Tol);
        }

        [Test]
        public void PlunderTendency_規律の緩みと給与不足()
        {
            // indiscipline=0.3; 0.3*0.75 + 0.5*0.5*0.3 = 0.225+0.075 = 0.3
            Assert.AreEqual(0.3f, MercenaryCompanyRules.PlunderTendency(0.7f, 0.5f), Tol);
            // 鉄の規律なら略奪しない
            Assert.AreEqual(0f, MercenaryCompanyRules.PlunderTendency(1f, 1f), Tol);
            // 無規律＋完全給与不足は上限へ
            Assert.AreEqual(1f, MercenaryCompanyRules.PlunderTendency(0f, 1f), Tol);
        }

        [Test]
        public void MercenaryValue_品質から寝返りとコストを引く()
        {
            // 0.68 - 0.375*0.6 - 0.5*0.4 = 0.68 - 0.225 - 0.2 = 0.255
            Assert.AreEqual(0.255f, MercenaryCompanyRules.MercenaryValue(0.68f, 0.375f, 0.5f), Tol);
            // 高寝返り・高コストは負＝雇う価値なし、-1 で下限 clamp
            Assert.AreEqual(-1f, MercenaryCompanyRules.MercenaryValue(0f, 1f, 1f), Tol);
            // 高品質・無寝返り・無コストは正味＝品質そのもの
            Assert.AreEqual(0.9f, MercenaryCompanyRules.MercenaryValue(0.9f, 0f, 0f), Tol);
        }

        [Test]
        public void ParamsClamp_範囲外は丸められる()
        {
            var p = new MercenaryCompanyParams(2f, -1f, 2f, 2f, -1f, 2f, -1f, -1f, -1f);
            Assert.AreEqual(1f, p.experienceWeight, Tol);   // Clamp01
            Assert.AreEqual(0f, p.costScale, Tol);          // Max(0,...)
            Assert.AreEqual(1f, p.paymentWeight, Tol);
            Assert.AreEqual(1f, p.bribeInfluence, Tol);
            Assert.AreEqual(0f, p.arrearsInfluence, Tol);
            Assert.AreEqual(1f, p.recordWeight, Tol);
            Assert.AreEqual(0f, p.payOfferInfluence, Tol);
            Assert.AreEqual(0f, p.defectionPenalty, Tol);
            Assert.AreEqual(0f, p.costPenalty, Tol);
        }

        [Test]
        public void Story_金の切れ目が縁の切れ目()
        {
            var p = MercenaryCompanyParams.Default;

            // 練達した精鋭傭兵団：経験0.9・良装備0.85
            float quality = MercenaryCompanyRules.CombatQuality(0.9f, 0.85f, p);

            // 雇い主は当初きちんと払い分け前も与え忠誠は高い → 寝返りリスクは低い
            float loyaltyPaid = MercenaryCompanyRules.Loyalty(0.95f, 0.6f, p);
            float riskPaid = MercenaryCompanyRules.DefectionRisk(loyaltyPaid, 0.3f, 0.05f, p);

            // 戦況悪化で雇い主は破産、給与は遅配・敵は破格の買収を提示
            float loyaltyBroke = MercenaryCompanyRules.Loyalty(0.2f, 0.1f, p);
            float riskBroke = MercenaryCompanyRules.DefectionRisk(loyaltyBroke, 0.9f, 0.8f, p);

            // 金が切れれば忠誠は崩れ寝返りリスクが跳ね上がる
            Assert.Less(loyaltyBroke, loyaltyPaid);
            Assert.Greater(riskBroke, riskPaid);

            // 給与不足は規律ある団でも略奪を呼ぶ
            float plunderPaid = MercenaryCompanyRules.PlunderTendency(0.7f, 0.0f, p);
            float plunderBroke = MercenaryCompanyRules.PlunderTendency(0.7f, 0.9f, p);
            Assert.Greater(plunderBroke, plunderPaid);

            // 正味価値：払えていた頃は雇う価値あり（正）、破産後は寝返りリスクで価値が下がる
            float cost = MercenaryCompanyRules.ContractCost(quality, 1f, p); // 規模1基準で 0..1 正規化済み相当
            float valuePaid = MercenaryCompanyRules.MercenaryValue(quality, riskPaid, cost, p);
            float valueBroke = MercenaryCompanyRules.MercenaryValue(quality, riskBroke, cost, p);
            Assert.Greater(valuePaid, valueBroke);
        }
    }
}
