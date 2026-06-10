using NUnit.Framework;
using UnityEngine;
using Ginei;
using CParams = Ginei.ColonizationRules.ColonizationParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 入植（#129）を固定する：入植可能条件（未入植・居住可能・探索済み）、成立進捗、入植成立で星系が支配化＆
    /// 内政対象(Province)が未統合・低安定で生成されること、二重入植不可、即時入植の簡易窓口。
    /// </summary>
    public class ColonizationRulesTests
    {
        private static StarSystem Uncolonized(int id)
            => new StarSystem(id, "辺境" + id, Vector2.zero) { isColonized = false, habitable = true };

        [Test]
        public void CanColonize_RequiresUnownedHabitableExplored()
        {
            var target = Uncolonized(1);
            Assert.IsTrue(ColonizationRules.CanColonize(target, explored: true));
            Assert.IsFalse(ColonizationRules.CanColonize(target, explored: false));        // 未探索は不可
            Assert.IsFalse(ColonizationRules.CanColonize(new StarSystem(2, "領有", Vector2.zero), explored: true)); // 既存=領有済み
            Assert.IsFalse(ColonizationRules.CanColonize(new StarSystem(3, "不毛", Vector2.zero) { isColonized = false, habitable = false }, explored: true));
        }

        [Test]
        public void Tick_AdvancesProgress_ThenComplete()
        {
            var p = CParams.Default; // buildTime 30
            var m = new ColonyMission(1, Faction.同盟);
            ColonizationRules.Tick(m, 20f, p);
            Assert.IsFalse(ColonizationRules.IsComplete(m, p));
            ColonizationRules.Tick(m, 20f, p); // 合計40 → 30でクランプ
            Assert.IsTrue(ColonizationRules.IsComplete(m, p));
            Assert.AreEqual(30f, m.progress, 1e-4f); // buildTime でクランプ
        }

        [Test]
        public void Establish_TakesOwnership_AndCreatesProvince()
        {
            var target = Uncolonized(5);
            var m = new ColonyMission(5, Faction.同盟, null, "民主") { progress = 30f };
            Province prov = ColonizationRules.Establish(target, m, CParams.Default);

            Assert.IsNotNull(prov);
            Assert.AreEqual(Faction.同盟, target.owner); // 自勢力支配に
            Assert.IsTrue(target.isColonized);
            Assert.AreEqual(5, prov.systemId);
            Assert.AreEqual("民主", prov.nativeIdeology);              // 住民思想＝入植元の政体
            Assert.AreEqual(20f, prov.population, 1e-4f);             // 小規模
            Assert.AreEqual(0f, prov.integration, 1e-4f);            // 入植直後＝未統合
            Assert.AreEqual(GovernanceRules.OccupiedInitialStability, prov.stability, 1e-4f); // 低安定から
        }

        [Test]
        public void Establish_RejectsAlreadyColonized()
        {
            var target = new StarSystem(6, "既領", Vector2.zero); // isColonized=true 既定
            var m = new ColonyMission(6, Faction.帝国) { progress = 30f };
            Assert.IsNull(ColonizationRules.Establish(target, m, CParams.Default));
        }

        [Test]
        public void Colonize_OneShot_EstablishesColony()
        {
            var target = Uncolonized(7);
            Province prov = ColonizationRules.Colonize(target, Faction.帝国, null, "専制", CParams.Default);
            Assert.IsNotNull(prov);
            Assert.AreEqual(Faction.帝国, target.owner);
            Assert.IsTrue(target.isColonized);

            // 二度目は既入植で不可（1惑星→拡張の1周が一意に成立）
            Assert.IsNull(ColonizationRules.Colonize(target, Faction.同盟, null, "民主", CParams.Default));
            Assert.AreEqual(Faction.帝国, target.owner);
        }

        [Test]
        public void ColonizedProvince_IntegratesOverTime_ViaGovernance()
        {
            // 入植直後は低安定→GovernanceRules.Tick で時間統合→安定が回復する（内政対象になっている証）
            var target = Uncolonized(8);
            Province prov = ColonizationRules.Colonize(target, Faction.帝国, null, "専制", CParams.Default);
            float before = prov.integration;
            GovernanceRules.Tick(prov, null, true, false, 5f);
            Assert.Greater(prov.integration, before); // 統合が進む＝内政対象
        }
    }
}
