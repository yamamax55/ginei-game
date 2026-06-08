using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// コロニー・要塞の同枠適用（PB-6・#755）の純ロジックを固定する：
    /// 種別ごとの既定スケール（要塞＞惑星＞コロニー）・統一生成窓口・
    /// コロニーは制空権なし＝即ドメイン・ダウンで接近限界なし＝そのまま侵攻で占領できる。
    /// 数値ロジック自体は値駆動なので惑星と完全に同枠（PlanetSiegeRules.Tick を流用）。
    /// </summary>
    public class PlanetSiegeKindTests
    {
        [Test]
        public void DefaultProfile_ScalesByKind_FortressStrongest_ColonyHasNoDefense()
        {
            var planet = PlanetSiegeRules.DefaultProfile(Planet.SiegeTargetKind.惑星);
            var fortress = PlanetSiegeRules.DefaultProfile(Planet.SiegeTargetKind.要塞);
            var colony = PlanetSiegeRules.DefaultProfile(Planet.SiegeTargetKind.コロニー);

            // 防衛（制空権）：要塞＞惑星＞コロニー(=0)
            Assert.Greater(fortress.maxOrbitalDefense, planet.maxOrbitalDefense);
            Assert.Greater(planet.maxOrbitalDefense, colony.maxOrbitalDefense);
            Assert.AreEqual(0f, colony.maxOrbitalDefense, 1e-4f); // コロニーは軌道超兵器なし

            // 侵略（占領閾値）：要塞＞惑星＞コロニー
            Assert.Greater(fortress.invasionThreshold, planet.invasionThreshold);
            Assert.Greater(planet.invasionThreshold, colony.invasionThreshold);
        }

        [Test]
        public void CreateTarget_SetsKindAndProfileValues()
        {
            var fortress = PlanetSiegeRules.CreateTarget(systemId: 3, owner: Faction.帝国, kind: Planet.SiegeTargetKind.要塞);
            Assert.AreEqual(Planet.SiegeTargetKind.要塞, fortress.kind);
            Assert.AreEqual(3, fortress.systemId);
            Assert.AreEqual(Faction.帝国, fortress.owner);
            Assert.AreEqual(PlanetSiegeRules.FortressDefense, fortress.maxOrbitalDefense, 1e-4f);
            Assert.AreEqual(PlanetSiegeRules.FortressInvasion, fortress.invasionThreshold, 1e-4f);
            Assert.AreEqual("要塞", fortress.KindName);
        }

        [Test]
        public void Fortress_DomainUp_BlocksApproach_LikePlanet()
        {
            var fortress = PlanetSiegeRules.CreateTarget(1, Faction.帝国, Planet.SiegeTargetKind.要塞);
            Assert.IsFalse(fortress.DomainDown);         // 制空権健在
            Assert.IsTrue(fortress.FleetApproachBlocked); // 接近限界で止まる（惑星と同枠 PB-5）
        }

        [Test]
        public void Colony_NoOrbitalWeapon_DomainDownImmediately_NoApproachLimit()
        {
            var colony = PlanetSiegeRules.CreateTarget(1, Faction.帝国, Planet.SiegeTargetKind.コロニー);
            Assert.IsTrue(colony.DomainDown);             // 超兵器なし＝最初からダウン
            Assert.IsFalse(colony.FleetApproachBlocked);  // 接近限界なし＝そのまま近づける
        }

        [Test]
        public void Colony_SkipsSuppression_InvadesDirectly_AndCaptures()
        {
            var colony = PlanetSiegeRules.CreateTarget(1, Faction.帝国, Planet.SiegeTargetKind.コロニー);

            // 制空権が無いので最初のtickから侵略値が直接たまる（制圧フェーズを飛ばす）
            var r1 = PlanetSiegeRules.Tick(colony, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.IsFalse(r1.domainWentDown);                 // もともとダウン済み＝段階遷移は起きない
            Assert.AreEqual(5f, colony.invasionProgress, 1e-4f); // 侵略値が直接蓄積

            // 閾値(ColonyInvasion=18)到達まで叩くと占領＝所有フリップ
            PlanetSiegeRules.Tick(colony, Faction.同盟, 5f, 1f); // 10
            PlanetSiegeRules.Tick(colony, Faction.同盟, 5f, 1f); // 15
            var rCap = PlanetSiegeRules.Tick(colony, Faction.同盟, 5f, 1f); // 20 >= 18
            Assert.IsTrue(colony.Captured);
            Assert.IsTrue(rCap.captured);
            Assert.AreEqual(Faction.同盟, colony.owner);
        }

        [Test]
        public void Fortress_NeedsMoreSuppression_ThanPlanet_BeforeDomainDown()
        {
            var planet = PlanetSiegeRules.CreateTarget(1, Faction.帝国, Planet.SiegeTargetKind.惑星);
            var fortress = PlanetSiegeRules.CreateTarget(1, Faction.帝国, Planet.SiegeTargetKind.要塞);

            // 同じ S-AV・同じ時間で叩いても、要塞の方が制空権が高く残る（より堅い）
            PlanetSiegeRules.Tick(planet, Faction.同盟, attackerSAV: 50f, deltaTime: 1f);
            PlanetSiegeRules.Tick(fortress, Faction.同盟, attackerSAV: 50f, deltaTime: 1f);
            Assert.Greater(fortress.orbitalDefense, planet.orbitalDefense);
        }
    }
}
