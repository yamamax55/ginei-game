using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>占領地平定（鎮静化作戦）の純ロジック PacificationRules の検証。</summary>
    public class PacificationRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void SweepProgress_BalancedForceAndTerrain()
        {
            // force=6×1=6 / shelter=Lerp(1,2,0.5)=1.5 / foe=4×1.5=6 / 6/12=0.5
            float v = PacificationRules.SweepProgress(6f, 4f, 0.5f);
            Assert.AreEqual(0.5f, v, Eps);
        }

        [Test]
        public void SweepProgress_NoInsurgentsIsComplete()
        {
            // 反乱勢力0なら掃討すべき敵がいない＝進捗1.0
            float v = PacificationRules.SweepProgress(3f, 0f, 1f);
            Assert.AreEqual(1f, v, Eps);
        }

        [Test]
        public void HeartsAndMinds_RestraintAndGrievanceBalance()
        {
            // receptivity=0.5/(0.5+0.5)=0.5 / 1×0.5×0.8=0.4
            float v = PacificationRules.HeartsAndMinds(1f, 0.5f, 0.5f);
            Assert.AreEqual(0.4f, v, Eps);
        }

        [Test]
        public void HeavyHandedBacklash_ForceAndCollateral()
        {
            // 0.8×0.5×0.6=0.24
            float v = PacificationRules.HeavyHandedBacklash(0.8f, 0.5f);
            Assert.AreEqual(0.24f, v, Eps);
        }

        [Test]
        public void InsurgentAttrition_SweepTimesHearts()
        {
            // 0.5×0.4×0.7=0.14
            float v = PacificationRules.InsurgentAttrition(0.5f, 0.4f);
            Assert.AreEqual(0.14f, v, Eps);
        }

        [Test]
        public void RecruitmentToInsurgency_BacklashFeedsGrievance()
        {
            // 0.24×0.5×0.5=0.06
            float v = PacificationRules.RecruitmentToInsurgency(0.24f, 0.5f);
            Assert.AreEqual(0.06f, v, Eps);
        }

        [Test]
        public void NetInsurgentStrength_AttritionMinusRecruitment()
        {
            // 0.14-0.06=0.08（正＝差し引きで抵抗が減る）
            float v = PacificationRules.NetInsurgentStrength(0.14f, 0.06f);
            Assert.AreEqual(0.08f, v, Eps);
        }

        [Test]
        public void NetInsurgentStrength_BacklashOutweighsIsNegative()
        {
            // 弱体化0.1 < 新規参加0.4 ＝鎮圧が敵を増やし逆効果（負）
            float v = PacificationRules.NetInsurgentStrength(0.1f, 0.4f);
            Assert.Less(v, 0f);
            Assert.AreEqual(-0.3f, v, Eps);
        }

        [Test]
        public void PacificationLevel_GainMinusPenalty()
        {
            // gain=(0.14+0.4)×0.5×0.6=0.162 / penalty=0.24×0.4=0.096 / 0.066
            float v = PacificationRules.PacificationLevel(0.14f, 0.4f, 0.24f);
            Assert.AreEqual(0.066f, v, Eps);
        }

        [Test]
        public void IsPacified_ThresholdAndDefault()
        {
            Assert.IsTrue(PacificationRules.IsPacified(0.8f, 0.75f));
            Assert.IsFalse(PacificationRules.IsPacified(0.7f, 0.75f));
            // 既定閾値0.75委譲版
            Assert.IsTrue(PacificationRules.IsPacified(0.75f));
            Assert.IsFalse(PacificationRules.IsPacified(0.74f));
        }

        [Test]
        public void InputsAreClamped()
        {
            // 範囲外入力でも壊れず0..1（符号付きは-1..1）に収まる
            float sweep = PacificationRules.SweepProgress(-5f, 99f, 5f);
            Assert.AreEqual(0f, sweep, Eps);
            float hm = PacificationRules.HeartsAndMinds(9f, 9f, -9f);
            Assert.AreEqual(0.8f, hm, Eps); // aid=1,restraint=1,grievance=0→receptivity=1→1×1×0.8
            float net = PacificationRules.NetInsurgentStrength(9f, -9f);
            Assert.AreEqual(1f, net, Eps);
        }

        [Test]
        public void Narrative_HeavyHandedOccupationBacksfires()
        {
            // 物語：強硬一辺倒の占領（少兵力で険しい地形・巻き添え多・不満放置・援助も自制も乏しい）は
            // 反発が新規参加を生み、純戦力が増え、平定度が低く完了しない。
            float sweep = PacificationRules.SweepProgress(2f, 8f, 0.9f);   // 兵力劣勢×険地で掃討進まず
            float hearts = PacificationRules.HeartsAndMinds(0.1f, 0.2f, 0.9f); // 懐柔ほぼ無し
            float backlash = PacificationRules.HeavyHandedBacklash(0.9f, 0.8f); // 過剰武力×巻き添え
            float attrition = PacificationRules.InsurgentAttrition(sweep, hearts);
            float recruit = PacificationRules.RecruitmentToInsurgency(backlash, 0.9f);
            float net = PacificationRules.NetInsurgentStrength(attrition, recruit);
            float level = PacificationRules.PacificationLevel(attrition, hearts, backlash);

            Assert.Less(net, 0f, "鎮圧が敵を増やし純戦力は逆効果（増加）");
            Assert.IsFalse(PacificationRules.IsPacified(level), "平定は完了しない");

            // 対比：兵力十分・援助厚く自制し巻き添え抑制・不満も低い懐柔型は平定が進む。
            float sweep2 = PacificationRules.SweepProgress(9f, 3f, 0.2f);
            float hearts2 = PacificationRules.HeartsAndMinds(0.9f, 0.9f, 0.2f);
            float backlash2 = PacificationRules.HeavyHandedBacklash(0.2f, 0.1f);
            float attrition2 = PacificationRules.InsurgentAttrition(sweep2, hearts2);
            float recruit2 = PacificationRules.RecruitmentToInsurgency(backlash2, 0.2f);
            float net2 = PacificationRules.NetInsurgentStrength(attrition2, recruit2);
            float level2 = PacificationRules.PacificationLevel(attrition2, hearts2, backlash2);

            Assert.Greater(net2, 0f, "懐柔型は差し引きで抵抗が減る");
            Assert.Greater(level2, level, "懐柔型のほうが平定度が高い");
        }
    }
}
