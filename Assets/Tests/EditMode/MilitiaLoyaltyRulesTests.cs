using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 市民軍忠誠（DISC-2 #1483・マキャヴェッリ）の純ロジックを既定 Params 具体値で固定するテスト。
    /// 市民軍は逆境に強く傭兵は逃げる＝徴募源が逆境忠誠の差を生むことを担保する。
    /// </summary>
    public class MilitiaLoyaltyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>基礎忠誠は市民軍が最も高く傭兵が最も低い（市民軍0.9＞志願兵0.75＞徴集兵0.55＞傭兵0.35）。</summary>
        [Test]
        public void BaseLoyalty_市民軍が最高で傭兵が最低()
        {
            Assert.AreEqual(0.9f, MilitiaLoyaltyRules.BaseLoyalty(RecruitmentSource.市民軍), Eps);
            Assert.AreEqual(0.75f, MilitiaLoyaltyRules.BaseLoyalty(RecruitmentSource.志願兵), Eps);
            Assert.AreEqual(0.55f, MilitiaLoyaltyRules.BaseLoyalty(RecruitmentSource.徴集兵), Eps);
            Assert.AreEqual(0.35f, MilitiaLoyaltyRules.BaseLoyalty(RecruitmentSource.傭兵), Eps);
            Assert.Greater(MilitiaLoyaltyRules.BaseLoyalty(RecruitmentSource.市民軍),
                           MilitiaLoyaltyRules.BaseLoyalty(RecruitmentSource.傭兵));
        }

        /// <summary>逆境耐性＝市民軍は深く踏みとどまり傭兵は早く崩れる（adversity0.5：市民軍0.87・傭兵0.155）。</summary>
        [Test]
        public void AdversityResilience_市民軍は逆境に強く傭兵は崩れる()
        {
            // 市民軍：0.9 − 0.5×0.6×(1−0.9)=0.03 ＝ 0.87
            Assert.AreEqual(0.87f, MilitiaLoyaltyRules.AdversityResilience(RecruitmentSource.市民軍, 0.5f), Eps);
            // 傭兵：0.35 − 0.5×0.6×(1−0.35)=0.195 ＝ 0.155
            Assert.AreEqual(0.155f, MilitiaLoyaltyRules.AdversityResilience(RecruitmentSource.傭兵, 0.5f), Eps);
            Assert.Greater(MilitiaLoyaltyRules.AdversityResilience(RecruitmentSource.市民軍, 0.5f),
                           MilitiaLoyaltyRules.AdversityResilience(RecruitmentSource.傭兵, 0.5f));
        }

        /// <summary>離反確率＝逆境＋未払いで傭兵は跳ね上がり、市民軍は低く抑えられる。</summary>
        [Test]
        public void DefectionProbability_傭兵は未払いと逆境で跳ね上がる()
        {
            // 市民軍：1−0.87 ＝ 0.13（未払いの加算は傭兵のみ）
            Assert.AreEqual(0.13f, MilitiaLoyaltyRules.DefectionProbability(RecruitmentSource.市民軍, 0.5f, 0f), Eps);
            // 傭兵：1−0.155 ＋ 0.2×0.5=0.1 ＝ 0.945
            Assert.AreEqual(0.945f, MilitiaLoyaltyRules.DefectionProbability(RecruitmentSource.傭兵, 0.5f, 0.2f), Eps);
            Assert.Greater(MilitiaLoyaltyRules.DefectionProbability(RecruitmentSource.傭兵, 0.5f, 0.2f),
                           MilitiaLoyaltyRules.DefectionProbability(RecruitmentSource.市民軍, 0.5f, 0.2f));
        }

        /// <summary>離反判定は roll で決定論：高確率(傭兵)は容易に離反し、低確率(市民軍)はめったに離反しない。</summary>
        [Test]
        public void WillDefect_rollで決定論()
        {
            // 傭兵 確率0.945：roll=0.5 < 0.945 ＝ 離反
            Assert.IsTrue(MilitiaLoyaltyRules.WillDefect(RecruitmentSource.傭兵, 0.5f, 0.2f, 0.5f));
            // 市民軍 確率0.13：roll=0.5 ≥ 0.13 ＝ 踏みとどまる
            Assert.IsFalse(MilitiaLoyaltyRules.WillDefect(RecruitmentSource.市民軍, 0.5f, 0f, 0.5f));
        }

        /// <summary>愛国的動機＝祖国の脅威で市民軍の戦意が上がり、傭兵は祖国と無関係で0。</summary>
        [Test]
        public void PatrioticMotivation_市民軍は防衛戦で真価_傭兵はゼロ()
        {
            // 市民軍：0.5×1×0.6 ＝ 0.3
            Assert.AreEqual(0.3f, MilitiaLoyaltyRules.PatrioticMotivation(RecruitmentSource.市民軍, 0.5f), Eps);
            // 傭兵：祖国なし ＝ 0
            Assert.AreEqual(0f, MilitiaLoyaltyRules.PatrioticMotivation(RecruitmentSource.傭兵, 0.5f), Eps);
        }

        /// <summary>コスト効率＝傭兵は平時に解散でき身軽だが戦時は維持費で効率激落、市民軍は常在で安い。</summary>
        [Test]
        public void CostEffectiveness_傭兵は戦時に効率が落ち市民軍は安定()
        {
            Assert.AreEqual(0.85f, MilitiaLoyaltyRules.CostEffectiveness(RecruitmentSource.傭兵, true), Eps);
            Assert.AreEqual(0.4f, MilitiaLoyaltyRules.CostEffectiveness(RecruitmentSource.傭兵, false), Eps);
            // 戦時は市民軍が傭兵を上回る
            Assert.Greater(MilitiaLoyaltyRules.CostEffectiveness(RecruitmentSource.市民軍, false),
                           MilitiaLoyaltyRules.CostEffectiveness(RecruitmentSource.傭兵, false));
        }

        /// <summary>公徳心の絆＝市民軍は公徳心がそのまま絆になり、傭兵には宿らない（0）。</summary>
        [Test]
        public void CivicVirtueBond_市民軍は公徳心と一体化_傭兵はゼロ()
        {
            Assert.AreEqual(0.8f, MilitiaLoyaltyRules.CivicVirtueBond(RecruitmentSource.市民軍, 0.8f), Eps);
            Assert.AreEqual(0.4f, MilitiaLoyaltyRules.CivicVirtueBond(RecruitmentSource.志願兵, 0.8f), Eps);
            Assert.AreEqual(0f, MilitiaLoyaltyRules.CivicVirtueBond(RecruitmentSource.傭兵, 0.8f), Eps);
        }

        /// <summary>傭兵の背信＝より良い条件で寝返り(0.5→0.4)、祖国に縛られる兵は寝返らない(0)。信頼できる軍判定も担保。</summary>
        [Test]
        public void MercenaryPerfidy_と信頼できる軍判定()
        {
            // 傭兵：0.5×0.8 ＝ 0.4、市民軍：0
            Assert.AreEqual(0.4f, MilitiaLoyaltyRules.MercenaryPerfidy(RecruitmentSource.傭兵, 0.5f), Eps);
            Assert.AreEqual(0f, MilitiaLoyaltyRules.MercenaryPerfidy(RecruitmentSource.市民軍, 0.5f), Eps);
            // 信頼できる軍：閾値0.5。市民軍の耐性0.87は信頼でき、傭兵の0.155は信頼できない。
            Assert.IsTrue(MilitiaLoyaltyRules.IsReliableForce(0.87f));
            Assert.IsFalse(MilitiaLoyaltyRules.IsReliableForce(0.155f));
        }
    }
}
