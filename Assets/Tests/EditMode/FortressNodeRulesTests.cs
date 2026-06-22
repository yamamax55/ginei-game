using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞＝戦略ノード（#40）：回廊封鎖判定（IsFortressBlocked）と力攻めの自動解決（AssaultFortress）。
    /// </summary>
    public class FortressNodeRulesTests
    {
        private static Corridor CorridorWithFortress(Faction owner, float garrison, bool controls = true)
        {
            var c = new Corridor(0, 1, 1f, CorridorType.要衝);
            c.fortress = new Fortress(garrison, 100f, 1f, controls) { owner = owner };
            return c;
        }

        // ── 封鎖判定 ──

        [Test]
        public void Blocked_WhenHostileViewerAndGarrisonAlive()
        {
            var c = CorridorWithFortress(Faction.帝国, 1000f);
            Assert.IsTrue(StrategyRules.IsFortressBlocked(c, Faction.同盟));  // 敵対勢力は通れない
            Assert.IsFalse(StrategyRules.IsFortressBlocked(c, Faction.帝国)); // 所有者は自由通過
        }

        [Test]
        public void NotBlocked_NoFortress_FezzanType()
        {
            var c = new Corridor(0, 1, 1f, CorridorType.通商); // フェザーン型＝要塞なし
            Assert.IsFalse(StrategyRules.IsFortressBlocked(c, Faction.同盟));
            Assert.IsFalse(StrategyRules.IsFortressBlocked(null, Faction.同盟));
        }

        [Test]
        public void NotBlocked_WhenGarrisonGone_OrNotControlling()
        {
            Assert.IsFalse(StrategyRules.IsFortressBlocked(CorridorWithFortress(Faction.帝国, 0f), Faction.同盟));        // 守備全滅＝開通
            Assert.IsFalse(StrategyRules.IsFortressBlocked(CorridorWithFortress(Faction.帝国, 1000f, false), Faction.同盟)); // 回廊を扼さない
        }

        // ── 力攻めの自動解決（既定 FortressParams：シールド+200%・assaultRatio 5.0）──

        [Test]
        public void Assault_Captures_WhenOverwhelmingForce()
        {
            // garrison1000×シールド倍率3.0＝実効防御3000、必要兵力=3000×5=15000。
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            var r = StrategyRules.AssaultFortress(f, Faction.同盟, 20000);
            Assert.IsTrue(r.captured);
            Assert.AreEqual(17000, r.attackerSurvivor);      // 20000 - 実効防御3000
            Assert.AreEqual(0f, f.garrisonStrength, 1e-3f);  // 守備全滅
            Assert.IsFalse(f.controlsCorridor);              // 通過開放
            Assert.AreEqual(Faction.同盟, f.owner);          // 所有移転
        }

        [Test]
        public void Assault_Repelled_WhenInsufficientForce()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            var r = StrategyRules.AssaultFortress(f, Faction.同盟, 5000); // < 必要15000＝難攻不落
            Assert.IsFalse(r.captured);
            Assert.AreEqual(3500, r.attackerSurvivor);       // 5000 - 損害min(5000,1500)=1500
            Assert.AreEqual(500f, f.garrisonStrength, 1e-3f);// 守備は一部削れる（1000 - 5000×0.1）
            Assert.IsTrue(f.controlsCorridor);               // まだ封鎖継続
            Assert.AreEqual(Faction.帝国, f.owner);          // 所有は不変
        }

        [Test]
        public void Assault_NullFortress_PassesThrough()
        {
            var r = StrategyRules.AssaultFortress(null, Faction.同盟, 8000);
            Assert.IsTrue(r.captured);
            Assert.AreEqual(8000, r.attackerSurvivor);
        }

        [Test]
        public void Assault_RepeatedAttrition_EventuallyWeakens()
        {
            // 繰り返し撃退で守備が徐々に削れる（兵糧攻めの素地）。
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            float before = f.garrisonStrength;
            StrategyRules.AssaultFortress(f, Faction.同盟, 3000);
            Assert.Less(f.garrisonStrength, before); // 守備が目減り
            Assert.Greater(f.garrisonStrength, 0f);  // ただし即全滅はしない
        }

        // ── 戦術潜行（実会戦）の決着反映：ApplyFortressBattleResult（#40 次スライス）──

        [Test]
        public void TacticalResult_Captured_FallsAndTransfers()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            StrategyRules.ApplyFortressBattleResult(f, Faction.同盟, captured: true);
            Assert.AreEqual(0f, f.garrisonStrength, 1e-3f); // 守備全滅＝陥落
            Assert.IsFalse(f.controlsCorridor);             // 通過開放
            Assert.AreEqual(Faction.同盟, f.owner);         // 所有移転
            Assert.IsFalse(StrategyRules.IsFortressBlocked(
                new Corridor(0, 1, 1f, CorridorType.要衝) { fortress = f }, Faction.同盟)); // もう封じない
        }

        [Test]
        public void TacticalResult_Repelled_SurvivesButShieldErodes()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            StrategyRules.ApplyFortressBattleResult(f, Faction.同盟, captured: false); // 既定 repulseShieldLoss=0.2
            Assert.AreEqual(1000f, f.garrisonStrength, 1e-3f); // 守備は健在（撃退）
            Assert.IsTrue(f.controlsCorridor);                 // まだ封鎖継続
            Assert.AreEqual(Faction.帝国, f.owner);            // 所有は不変
            Assert.AreEqual(0.8f, f.shieldIntegrity, 1e-4f);   // 戦術会戦で叩かれシールド目減り（段階攻略）
        }

        [Test]
        public void TacticalResult_RepeatedRepulse_ShieldClampsAtZero()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            for (int i = 0; i < 10; i++) StrategyRules.ApplyFortressBattleResult(f, Faction.同盟, captured: false);
            Assert.AreEqual(0f, f.shieldIntegrity, 1e-4f); // シールドは0で下げ止まり（負にならない）
            Assert.IsTrue(f.controlsCorridor);             // シールド0でも守備が残れば封鎖は続く
        }

        [Test]
        public void TacticalResult_NullFortress_NoThrow()
        {
            Assert.DoesNotThrow(() => StrategyRules.ApplyFortressBattleResult(null, Faction.同盟, true));
        }
    }
}
