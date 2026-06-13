using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// キャンペーン勝敗（遊べる縦スライスの核）の純ロジックを固定する：
    /// 制覇（支配率）・全制圧（敵排除）・滅亡（星系0）・継続。
    /// </summary>
    public class CampaignVictoryRulesTests
    {
        private static GalaxyMap Map(params Faction[] owners)
        {
            var m = new GalaxyMap();
            for (int i = 0; i < owners.Length; i++)
                m.AddSystem(new StarSystem(i + 1, "S" + i, new Vector2(i, 0), owners[i]));
            return m;
        }

        [Test]
        public void Defeat_WhenPlayerOwnsNoSystems()
        {
            var m = Map(Faction.同盟, Faction.同盟, Faction.同盟);
            Assert.AreEqual(CampaignOutcome.敗北, CampaignVictoryRules.Evaluate(m, Faction.帝国));
        }

        [Test]
        public void Victory_WhenNoRivalSystemsRemain()
        {
            var m = Map(Faction.帝国, Faction.帝国);
            Assert.AreEqual(CampaignOutcome.勝利, CampaignVictoryRules.Evaluate(m, Faction.帝国)); // 全制圧
        }

        [Test]
        public void Victory_WhenDominationFractionReached()
        {
            // 5星系中4を帝国＝0.8 ≥ 0.7 → 制覇勝利
            var m = Map(Faction.帝国, Faction.帝国, Faction.帝国, Faction.帝国, Faction.同盟);
            Assert.AreEqual(CampaignOutcome.勝利, CampaignVictoryRules.Evaluate(m, Faction.帝国));
        }

        [Test]
        public void Continues_WhenContested()
        {
            // 4星系中2を帝国＝0.5（自0.5<0.7・敵0.5<0.7）→ 継続
            var m = Map(Faction.帝国, Faction.帝国, Faction.同盟, Faction.同盟);
            Assert.AreEqual(CampaignOutcome.継続, CampaignVictoryRules.Evaluate(m, Faction.帝国));
        }

        [Test]
        public void Defeat_WhenRivalReachesDominationThreshold()
        {
            // 5星系中4を敵（同盟）＝0.8 ≥ 0.7。プレイヤー（帝国）は1星系を残すが敵に制覇され敗北。
            var m = Map(Faction.同盟, Faction.同盟, Faction.同盟, Faction.同盟, Faction.帝国);
            Assert.AreEqual(CampaignOutcome.敗北, CampaignVictoryRules.Evaluate(m, Faction.帝国));
        }

        [Test]
        public void NotDefeat_WhenRivalBelowThreshold()
        {
            // 5星系中3を敵＝0.6 < 0.7・プレイヤー2＝0.4 → まだ継続（敵制覇に至らず）。
            var m = Map(Faction.同盟, Faction.同盟, Faction.同盟, Faction.帝国, Faction.帝国);
            Assert.AreEqual(CampaignOutcome.継続, CampaignVictoryRules.Evaluate(m, Faction.帝国));
        }

        [Test]
        public void MaxRivalFraction_TakesLargestRival()
        {
            // 敵が残る限り最大の敵勢力の支配率を返す（敵居なければ0）。
            var m = Map(Faction.同盟, Faction.同盟, Faction.帝国);
            Assert.AreEqual(2f / 3f, CampaignVictoryRules.MaxRivalFraction(m, Faction.帝国), 1e-4f);
            Assert.AreEqual(0f, CampaignVictoryRules.MaxRivalFraction(Map(Faction.帝国, Faction.帝国), Faction.帝国), 1e-4f);
        }

        [Test]
        public void PlayerVictory_TakesPriorityOverRivalDefeat()
        {
            // 自制覇（0.8≥0.7）は敵制覇判定より先＝勝利になる（敵はそもそも0.2）。
            var m = Map(Faction.帝国, Faction.帝国, Faction.帝国, Faction.帝国, Faction.同盟);
            Assert.AreEqual(CampaignOutcome.勝利, CampaignVictoryRules.Evaluate(m, Faction.帝国));
        }

        [Test]
        public void EmptyMap_Continues()
        {
            Assert.AreEqual(CampaignOutcome.継続, CampaignVictoryRules.Evaluate(new GalaxyMap(), Faction.帝国));
            Assert.AreEqual(CampaignOutcome.継続, CampaignVictoryRules.Evaluate(null, Faction.帝国));
        }

        [Test]
        public void Metrics_AreCorrect()
        {
            var m = Map(Faction.帝国, Faction.帝国, Faction.同盟);
            Assert.AreEqual(3, CampaignVictoryRules.TotalSystems(m));
            Assert.AreEqual(2, CampaignVictoryRules.OwnedCount(m, Faction.帝国));
            Assert.AreEqual(2f / 3f, CampaignVictoryRules.OwnedFraction(m, Faction.帝国), 1e-4f);
            Assert.IsTrue(CampaignVictoryRules.RivalSystemsRemain(m, Faction.帝国));
            Assert.IsFalse(CampaignVictoryRules.RivalSystemsRemain(Map(Faction.帝国), Faction.帝国));
        }

        [Test]
        public void CustomThreshold_Respected()
        {
            // 3星系中2を帝国＝0.667。閾値0.9なら未達＝継続、0.5なら達成＝勝利。
            var m = Map(Faction.帝国, Faction.帝国, Faction.同盟);
            Assert.AreEqual(CampaignOutcome.継続,
                CampaignVictoryRules.Evaluate(m, Faction.帝国, new CampaignVictoryRules.CampaignVictoryParams(0.9f)));
            Assert.AreEqual(CampaignOutcome.勝利,
                CampaignVictoryRules.Evaluate(m, Faction.帝国, new CampaignVictoryRules.CampaignVictoryParams(0.5f)));
        }
    }
}
