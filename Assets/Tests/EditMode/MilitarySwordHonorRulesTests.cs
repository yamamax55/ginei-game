using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 恩賜の軍刀組（史実ベース）の純ロジックを固定する：
    /// 栄誉credential（なし/星/恩賜＝大学校卒TOP5）／学閥主義↔実力主義（米軍対比）の昇進優遇の切替。
    /// 「星あり・軍刀あり」が最優遇。実力主義では俊英が credential を追い越せる。
    /// </summary>
    public class MilitarySwordHonorRulesTests
    {
        // ===== HonorOf（星・恩賜の軍刀） =====

        [Test]
        public void HonorOf_WarCollegeTop5_IsSword()
        {
            Assert.AreEqual(MilitaryHonor.恩賜の軍刀, MilitarySwordHonorRules.HonorOf(MilitaryDegree.大学校卒, 1));
            Assert.AreEqual(MilitaryHonor.恩賜の軍刀, MilitarySwordHonorRules.HonorOf(MilitaryDegree.大学校卒, MilitarySwordHonorRules.SwordQuota));
        }

        [Test]
        public void HonorOf_WarCollegeBelowQuota_IsStar()
        {
            Assert.AreEqual(MilitaryHonor.星, MilitarySwordHonorRules.HonorOf(MilitaryDegree.大学校卒, MilitarySwordHonorRules.SwordQuota + 1));
            Assert.AreEqual(MilitaryHonor.星, MilitarySwordHonorRules.HonorOf(MilitaryDegree.大学校卒, 0)); // 席次不明は星止まり
        }

        [Test]
        public void HonorOf_NonWarCollege_IsNone()
        {
            Assert.AreEqual(MilitaryHonor.なし, MilitarySwordHonorRules.HonorOf(MilitaryDegree.士官学校卒, 1));
            Assert.AreEqual(MilitaryHonor.なし, MilitarySwordHonorRules.HonorOf(MilitaryDegree.無資格, 1));
        }

        [Test]
        public void IsSwordGroup_OnlyTop5WarCollege()
        {
            Assert.IsTrue(MilitarySwordHonorRules.IsSwordGroup(MilitaryDegree.大学校卒, 3));
            Assert.IsFalse(MilitarySwordHonorRules.IsSwordGroup(MilitaryDegree.大学校卒, 6));
            Assert.IsFalse(MilitarySwordHonorRules.IsSwordGroup(MilitaryDegree.士官学校卒, 1));
        }

        // ===== credential スコアと重み =====

        [Test]
        public void CredentialScore_SwordHighestLineLowest()
        {
            Assert.Greater(MilitarySwordHonorRules.CredentialScore(MilitaryHonor.恩賜の軍刀),
                           MilitarySwordHonorRules.CredentialScore(MilitaryHonor.星));
            Assert.Greater(MilitarySwordHonorRules.CredentialScore(MilitaryHonor.星),
                           MilitarySwordHonorRules.CredentialScore(MilitaryHonor.なし));
        }

        [Test]
        public void CredentialWeight_FactionalismHeavierThanMeritocracy()
        {
            Assert.Greater(MilitarySwordHonorRules.CredentialWeight(PromotionDoctrine.学閥主義),
                           MilitarySwordHonorRules.CredentialWeight(PromotionDoctrine.実力主義));
        }

        // ===== 昇進優遇：学閥主義＝credential 支配 =====

        [Test]
        public void Favor_Factionalism_SwordOverStarOverLine_AtEqualMerit()
        {
            const float merit = 0.5f;
            float sword = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.恩賜の軍刀, merit, PromotionDoctrine.学閥主義);
            float star = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.星, merit, PromotionDoctrine.学閥主義);
            float line = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.なし, merit, PromotionDoctrine.学閥主義);
            Assert.Greater(sword, star);
            Assert.Greater(star, line);
        }

        [Test]
        public void Favor_Factionalism_LowMeritSwordBeatsHighMeritLine()
        {
            // 史実：軍刀組の人事独占＝低 merit の恩賜が高 merit の隊付を上回る
            float sword = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.恩賜の軍刀, 0.2f, PromotionDoctrine.学閥主義);
            float line = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.なし, 0.95f, PromotionDoctrine.学閥主義);
            Assert.Greater(sword, line);
        }

        // ===== 昇進優遇：実力主義（米軍対比）＝merit 支配 =====

        [Test]
        public void Favor_Meritocracy_TalentedLineOvertakesMediocreSword()
        {
            // 米軍対比：恩賜でなくとも俊英が追い越せる
            float sword = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.恩賜の軍刀, 0.2f, PromotionDoctrine.実力主義);
            float line = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.なし, 0.95f, PromotionDoctrine.実力主義);
            Assert.Greater(line, sword);
        }

        [Test]
        public void Favor_DoctrineFlipsTheOutcome()
        {
            // 同じ2人でも doctrine で勝敗が反転する（学閥→恩賜・実力→隊付）
            float swordF = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.恩賜の軍刀, 0.2f, PromotionDoctrine.学閥主義);
            float lineF = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.なし, 0.95f, PromotionDoctrine.学閥主義);
            float swordM = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.恩賜の軍刀, 0.2f, PromotionDoctrine.実力主義);
            float lineM = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.なし, 0.95f, PromotionDoctrine.実力主義);
            Assert.Greater(swordF, lineF); // 学閥主義＝恩賜が勝つ
            Assert.Greater(lineM, swordM); // 実力主義＝隊付が勝つ
        }

        [Test]
        public void Favor_OneShotFromDegreeAndRank()
        {
            // 大学校卒・首席（恩賜）の一括版が honor 版と一致
            float a = MilitarySwordHonorRules.PromotionFavor(MilitaryDegree.大学校卒, 1, 0.5f, PromotionDoctrine.学閥主義);
            float b = MilitarySwordHonorRules.PromotionFavor(MilitaryHonor.恩賜の軍刀, 0.5f, PromotionDoctrine.学閥主義);
            Assert.AreEqual(b, a, 1e-5f);
        }
    }
}
