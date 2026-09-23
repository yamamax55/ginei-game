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

        // ── 兵糧攻め（補給遮断の守備漸減）：TickFortressSiegeAttrition（#40）──

        [Test]
        public void Starve_ErodesGarrison_WhenBesieged()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            bool fell = StrategyRules.TickFortressSiegeAttrition(f, besieged: true, days: 1f);
            Assert.IsFalse(fell);                              // 1日では落ちない
            Assert.AreEqual(920f, f.garrisonStrength, 1e-3f); // 1000 - (0.03×1000 + 50) = 920
            Assert.IsTrue(f.controlsCorridor);                // まだ封鎖継続
        }

        [Test]
        public void Starve_NoChange_WhenNotBesieged()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            bool fell = StrategyRules.TickFortressSiegeAttrition(f, besieged: false, days: 30f);
            Assert.IsFalse(fell);
            Assert.AreEqual(1000f, f.garrisonStrength, 1e-3f); // 補給が通る＝減らない
        }

        [Test]
        public void Starve_EventuallyFalls_OpensCorridor()
        {
            // 難攻不落（力攻め不可）でも封鎖を続ければ兵糧が尽きて開城する＝銀英伝の落とし方。
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            bool fell = false;
            for (int day = 0; day < 200 && !fell; day++)
                fell = StrategyRules.TickFortressSiegeAttrition(f, besieged: true, days: 1f);
            Assert.IsTrue(fell);                               // いつかは落城
            Assert.AreEqual(0f, f.garrisonStrength, 1e-3f);    // 守備が尽きる
            Assert.IsFalse(f.controlsCorridor);                // 回廊が開く
            Assert.AreEqual(Faction.帝国, f.owner);            // 所有は不変（占領は停泊で別途）
        }

        [Test]
        public void Starve_NullOrEmpty_NoThrowNoFall()
        {
            Assert.DoesNotThrow(() => StrategyRules.TickFortressSiegeAttrition(null, true, 1f));
            var empty = new Fortress(0f, 100f, 1f, true) { owner = Faction.帝国 };
            Assert.IsFalse(StrategyRules.TickFortressSiegeAttrition(empty, true, 1f)); // 既に守備0＝落城扱いしない
        }

        // ── 補給下の回復：TickFortressGarrisonRegen（兵糧攻めの対・#40）──

        [Test]
        public void Regen_RecoversGarrisonAndShield_WhenSupplied()
        {
            // 上限1000、削れた守備800・シールド0.5。補給下で1日：守備+2%×1000=20、シールド+0.05。
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            f.garrisonStrength = 800f; f.shieldIntegrity = 0.5f;
            StrategyRules.TickFortressGarrisonRegen(f, supplied: true, days: 1f);
            Assert.AreEqual(820f, f.garrisonStrength, 1e-3f);
            Assert.AreEqual(0.55f, f.shieldIntegrity, 1e-4f);
        }

        [Test]
        public void Regen_ClampsAtCapacityAndFullShield()
        {
            var f = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            f.garrisonStrength = 995f; f.shieldIntegrity = 0.99f;
            StrategyRules.TickFortressGarrisonRegen(f, supplied: true, days: 10f);
            Assert.AreEqual(1000f, f.garrisonStrength, 1e-3f); // 上限で頭打ち
            Assert.AreEqual(1f, f.shieldIntegrity, 1e-4f);     // シールドは1.0で頭打ち
        }

        [Test]
        public void Regen_NoChange_WhenBlockaded_OrFallen()
        {
            var besieged = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            besieged.garrisonStrength = 800f;
            StrategyRules.TickFortressGarrisonRegen(besieged, supplied: false, days: 5f); // 封鎖中＝補給なし
            Assert.AreEqual(800f, besieged.garrisonStrength, 1e-3f);

            var fallen = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            fallen.garrisonStrength = 0f; // 開城済み
            StrategyRules.TickFortressGarrisonRegen(fallen, supplied: true, days: 5f);
            Assert.AreEqual(0f, fallen.garrisonStrength, 1e-3f); // 自然回復しない（再駐留が要る）
        }

        [Test]
        public void Regen_NullSafe()
        {
            Assert.DoesNotThrow(() => StrategyRules.TickFortressGarrisonRegen(null, true, 1f));
        }

        [Test]
        public void GarrisonCapacity_DefaultsToInitialGarrison()
        {
            var f = new Fortress(1234f, 100f);
            Assert.AreEqual(1234f, f.garrisonCapacity, 1e-3f); // 初期守備＝満員
        }
    }
}
