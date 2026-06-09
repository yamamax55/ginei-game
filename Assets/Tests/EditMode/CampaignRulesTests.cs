using UnityEngine;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦役の世界状態（社会シミュ ↔ 地理盤面の統合）を固定する：勢力ごとの国家状態の用意・時間進行、
    /// 版図の一体化度が実効安定度を割り引くこと、暫定優勢勢力。
    /// </summary>
    public class CampaignRulesTests
    {
        private static StarSystem Sys(int id, Faction owner) => new StarSystem(id, "S" + id, Vector2.zero, owner);

        [Test]
        public void EnsureStates_CreatesPerOwningFaction()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.同盟));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);

            Assert.AreEqual(2, c.states.Count);
            Assert.IsNotNull(CampaignRules.GetState(c, Faction.帝国));
            Assert.IsNotNull(CampaignRules.GetState(c, Faction.同盟));

            CampaignRules.EnsureStates(c); // 冪等：再呼び出しで増えない
            Assert.AreEqual(2, c.states.Count);
        }

        [Test]
        public void Fragmentation_DiscountsEffectiveStability()
        {
            // 帝国は {0,1} と {2,3} の2塊に分断（回廊 0-1 と 2-3 のみ）＝一体化度 0.5
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.帝国));
            m.AddSystem(Sys(3, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(2, 3, 1f));

            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c); // 帝国の国家状態（既定＝安定度1.0）

            // 安定度1.0 × 一体化度0.5 = 0.5
            Assert.AreEqual(0.5f, CampaignRules.EffectiveStability(c, Faction.帝国), 1e-4f);
        }

        [Test]
        public void Tick_AdvancesAllStates()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);
            var s = CampaignRules.GetState(c, Faction.帝国);
            s.regime.virtue = 0f;

            CampaignRules.Tick(c, 1f);
            Assert.Greater(s.regime.corruption, 0f); // 腐敗が進んだ
        }

        [Test]
        public void LeadingFaction_HighestEffectiveStability()
        {
            // 帝国＝連結（一体化1.0）／同盟＝分断（一体化0.5）。両方とも国家安定度は既定1.0。
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.同盟));
            m.AddSystem(Sys(3, Faction.同盟));
            m.AddSystem(Sys(4, Faction.同盟));
            m.AddCorridor(new Corridor(0, 1, 1f)); // 帝国連結
            m.AddCorridor(new Corridor(2, 3, 1f)); // 同盟 {2,3} と {4} に分断（一体化 2/3）

            var c = new CampaignState(m);
            CampaignRules.EnsureStates(c);

            // 帝国 1.0×1.0=1.0 ＞ 同盟 1.0×(2/3)≈0.667
            Assert.AreEqual(Faction.帝国, CampaignRules.LeadingFaction(c));
        }
    }
}
