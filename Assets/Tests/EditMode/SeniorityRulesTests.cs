using NUnit.Framework;
using Ginei;
using SP = Ginei.SeniorityRules.SeniorityParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 席次主義 vs 実力主義（LIFE-5/6 共有 #155/#156）を固定する：席次→初期tier、政体ごとの席次の固さ、
    /// 実効序列の混合、下位席次の俊英が上位席次の凡才を追い越せるか（政体で変わる）。
    /// </summary>
    public class SeniorityRulesTests
    {
        [Test]
        public void InitialTier_TopRankHighest_FloorClamped()
        {
            var p = SP.Default; // 首席6・20席ごと−1・下限5
            Assert.AreEqual(6, SeniorityRules.InitialTier(1, p));
            Assert.AreEqual(6, SeniorityRules.InitialTier(20, p));
            Assert.AreEqual(5, SeniorityRules.InitialTier(21, p));
            Assert.AreEqual(5, SeniorityRules.InitialTier(200, p)); // 下限でクランプ
        }

        [Test]
        public void PoliticalRigidity_RoyalFirmest_WarlordLoosest()
        {
            Assert.Greater(SeniorityRules.PoliticalRigidity(CivilianControlType.君主統帥),
                           SeniorityRules.PoliticalRigidity(CivilianControlType.文民統制));
            Assert.Greater(SeniorityRules.PoliticalRigidity(CivilianControlType.文民統制),
                           SeniorityRules.PoliticalRigidity(CivilianControlType.軍部優位));
        }

        [Test]
        public void EffectiveStanding_HighRigidity_FavorsSeniority()
        {
            // 首席・低merit vs 下位・高merit、固い政体では首席が上
            float senior = SeniorityRules.EffectiveStanding(1, merit: 0.2f, rigidity: 0.9f);
            float junior = SeniorityRules.EffectiveStanding(10, merit: 0.9f, rigidity: 0.9f);
            Assert.Greater(senior, junior);
        }

        [Test]
        public void MeritOvertakes_DependsOnRegime()
        {
            // 上位席次の凡才(rank1,merit0.2) vs 下位席次の俊英(rank10,merit0.9)
            float royal = SeniorityRules.PoliticalRigidity(CivilianControlType.君主統帥);  // 0.9 固い
            float warlord = SeniorityRules.PoliticalRigidity(CivilianControlType.軍部優位); // 0.2 緩い

            Assert.IsFalse(SeniorityRules.MeritOvertakes(1, 0.2f, 10, 0.9f, royal));   // 王党派＝席次が守られる
            Assert.IsTrue(SeniorityRules.MeritOvertakes(1, 0.2f, 10, 0.9f, warlord));  // 軍閥＝実力が上書き
        }
    }
}
