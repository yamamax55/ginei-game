using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星攻城（#131 PB-3〜PB-7）の純ロジックを固定する：
    /// 制空権(ピラー・ドメイン)が健在な間は接近不可→S-AVで段階制圧→ドメイン・ダウン→
    /// 侵略値を蓄積→閾値で占領（所有フリップ）。二段階・1tick一段階・遷移の単発性。
    /// </summary>
    public class PlanetSiegeTests
    {
        // 制空権10・侵略閾値10・帝国所有の惑星
        private Planet NewPlanet() => new Planet(systemId: 1, owner: Faction.帝国,
            maxOrbitalDefense: 10f, invasionThreshold: 10f);

        [Test]
        public void DomainUp_BlocksFleetApproach_DownAllows()
        {
            var p = NewPlanet();
            Assert.IsFalse(p.DomainDown);
            Assert.IsTrue(p.FleetApproachBlocked); // 制空権健在＝接近限界で止まる

            p.orbitalDefense = 0f;
            Assert.IsTrue(p.DomainDown);
            Assert.IsFalse(p.FleetApproachBlocked); // ドメイン・ダウンで接近解禁
        }

        [Test]
        public void Suppress_ReducesDefense_DomainDownReportedOnce()
        {
            var p = NewPlanet();

            // S-AV戦力5で抑制（既定係数：5*1*1=5/ tick）
            var r1 = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.AreEqual(5f, p.orbitalDefense, 1e-4f);
            Assert.IsFalse(r1.domainWentDown);
            Assert.AreEqual(0f, p.invasionProgress, 1e-4f); // ドメイン健在中は侵略しない

            // 2tick目で 0 ＝ドメイン・ダウン（単発で報告）
            var r2 = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsTrue(p.DomainDown);
            Assert.IsTrue(r2.domainWentDown);
            Assert.AreEqual(0f, p.invasionProgress, 1e-4f); // ダウンしたtickはまだ侵略しない（1tick一段階）

            // 3tick目以降は再度 down 報告しない
            var r3 = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsFalse(r3.domainWentDown);
        }

        [Test]
        public void AfterDomainDown_InvasionAccumulates_CaptureFlipsOwner()
        {
            var p = NewPlanet();
            p.orbitalDefense = 0f; // 既にドメイン・ダウン

            var a = PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 5f, deltaTime: 1f);
            Assert.AreEqual(5f, p.invasionProgress, 1e-4f);
            Assert.IsFalse(a.captured);
            Assert.AreEqual(Faction.帝国, p.owner); // まだ陥落していない

            var b = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsTrue(p.Captured);
            Assert.IsTrue(b.captured);             // 占領の遷移は単発
            Assert.AreEqual(Faction.同盟, p.owner); // 所有が攻撃側へフリップ

            // さらに叩いても二重に captured 報告しない
            var c = PlanetSiegeRules.Tick(p, Faction.同盟, 5f, 1f);
            Assert.IsFalse(c.captured);
        }

        [Test]
        public void NoAttacker_RegensDefense_NotAboveMax()
        {
            var p = NewPlanet();
            p.orbitalDefense = 4f;
            var prm = new SiegeParams(suppressRate: 1f, invadeRate: 1f, defenseRegen: 3f);

            PlanetSiegeRules.Tick(p, Faction.同盟, attackerSAV: 0f, deltaTime: 1f, prm);
            Assert.AreEqual(7f, p.orbitalDefense, 1e-4f); // 4+3 再建

            PlanetSiegeRules.Tick(p, Faction.同盟, 0f, 10f, prm);
            Assert.AreEqual(10f, p.orbitalDefense, 1e-4f); // max を超えない
        }

        [Test]
        public void NoAttacker_DoesNotRegen_AfterDomainDown()
        {
            var p = NewPlanet();
            p.orbitalDefense = 0f; // ダウン済み
            var prm = new SiegeParams(1f, 1f, defenseRegen: 5f);

            PlanetSiegeRules.Tick(p, Faction.同盟, 0f, 1f, prm);
            Assert.AreEqual(0f, p.orbitalDefense, 1e-4f); // ダウン後は再建しない（制圧された制空権は戻らない）
        }
    }
}
