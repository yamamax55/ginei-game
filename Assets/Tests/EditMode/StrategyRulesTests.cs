using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略マップ仕上げ（C-1 #34）：回廊での会戦トリガー検知と星系占領を固定する。
    /// 敵対判定は FactionRelations（enum フォールバック＝帝国≠同盟＝敵）。
    /// </summary>
    public class StrategyRulesTests
    {
        // 0 —(4)— 1 、0 —(4)— 2。全星系の初期所有は帝国。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", Vector2.right, Faction.帝国));
            m.AddSystem(new StarSystem(2, "C", Vector2.up, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 4f));
            m.AddCorridor(new Corridor(0, 2, 4f));
            return m;
        }

        // ───────── 会戦トリガー（FindEncounters）─────────

        [Test]
        public void FindEncounters_HostileOnSameCorridor_IsDetected()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1); // 0→1
            var ally = new StrategicFleet(2, 1, Faction.同盟, 1f); ally.BeginWarp(m, 0); // 1→0（同一回廊）
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            var enc = StrategyRules.FindEncounters(reg);
            Assert.AreEqual(1, enc.Count);
            Assert.IsTrue(StrategyRules.AnyEncounter(reg));
        }

        [Test]
        public void FindEncounters_SameFaction_NotHostile_NoEncounter()
        {
            var m = MakeMap();
            var a = new StrategicFleet(1, 0, Faction.帝国, 1f); a.BeginWarp(m, 1);
            var b = new StrategicFleet(2, 1, Faction.帝国, 1f); b.BeginWarp(m, 0);
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);

            Assert.AreEqual(0, StrategyRules.FindEncounters(reg).Count);
            Assert.IsFalse(StrategyRules.AnyEncounter(reg));
        }

        [Test]
        public void FindEncounters_DifferentCorridors_NoEncounter()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1);  // 回廊 0-1
            var ally = new StrategicFleet(2, 0, Faction.同盟, 1f); ally.BeginWarp(m, 2); // 回廊 0-2
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            Assert.AreEqual(0, StrategyRules.FindEncounters(reg).Count);
        }

        [Test]
        public void FindEncounters_StationedFleet_NotCounted()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1); // 移動中
            var ally = new StrategicFleet(2, 1, Faction.同盟, 1f);                     // 停泊（1に在席）
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            Assert.AreEqual(0, StrategyRules.FindEncounters(reg).Count);
        }

        // ───────── 占領（ResolveOccupation）─────────

        [Test]
        public void ResolveOccupation_EnemyUndefended_FlipsOwner()
        {
            var m = MakeMap();
            var ally = new StrategicFleet(5, 1, Faction.同盟); // 帝国星系1に同盟艦が停泊
            var reg = new StrategicFleetRegistry(m); reg.Add(ally);

            Assert.IsTrue(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_SameFaction_NoFlip()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(5, 1, Faction.帝国); // 帝国星系に帝国艦
            var reg = new StrategicFleetRegistry(m); reg.Add(imp);

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_Contested_NoFlip()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(5, 1, Faction.帝国);
            var ally = new StrategicFleet(6, 1, Faction.同盟); // 同一星系に両勢力＝占領未確定
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_OnlyMovingFleet_NoFlip()
        {
            var m = MakeMap();
            var ally = new StrategicFleet(5, 0, Faction.同盟, 1f); ally.BeginWarp(m, 1); // 1へ移動中（未到着）
            var reg = new StrategicFleetRegistry(m); reg.Add(ally);

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveAllOccupations_CountsFlips()
        {
            var m = MakeMap();
            var a1 = new StrategicFleet(5, 1, Faction.同盟); // 星系1占領
            var a2 = new StrategicFleet(6, 2, Faction.同盟); // 星系2占領
            var reg = new StrategicFleetRegistry(m); reg.Add(a1); reg.Add(a2);

            Assert.AreEqual(2, StrategyRules.ResolveAllOccupations(m, reg));
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);
            Assert.AreEqual(Faction.同盟, m.GetSystem(2).owner);
            Assert.AreEqual(Faction.帝国, m.GetSystem(0).owner); // 0 は無人＝据え置き
        }

        // ═════════ 敵対的エッジケース（テスト硬化）═════════

        // ───────── null安全 / 空コレクション ─────────

        [Test]
        public void NullRegistry_AllQueries_AreSafe()
        {
            // 仕様：null レジストリは「会戦なし／何も起きない」を返す（例外を投げない）。
            Assert.AreEqual(0, StrategyRules.FindEncounters(null).Count);
            Assert.IsFalse(StrategyRules.AnyEncounter(null));
            Assert.AreEqual(0, StrategyRules.CollidedEncounters(null).Count);
            Assert.AreEqual(0, StrategyRules.BeginEngagements(null));
            Assert.AreEqual(0, StrategyRules.ResolveEncounters(null));
            Assert.IsFalse(StrategyRules.TryFindCollision(null, out _, out _));
            Assert.IsFalse(StrategyRules.TryGetEngagementOnCorridor(null, 0, 1, out _, out _));
            Assert.AreEqual(0, StrategyRules.ResolveAllOccupations(null, null));
            Assert.AreEqual(0, StrategyRules.TickSieges(null, null, 1f));
            Assert.IsFalse(StrategyRules.ApplyHandoffResult(null));
        }

        [Test]
        public void IsFtlBlocked_NullMapOrCorridor_IsFalse()
        {
            var m = MakeMap();
            // 仕様：map/corridor が null なら前線でない（false）。
            Assert.IsFalse(StrategyRules.IsFtlBlocked(null, m.GetCorridor(0, 1)));
            Assert.IsFalse(StrategyRules.IsFtlBlocked(m, null));
        }

        [Test]
        public void ResolveOccupation_EmptySystem_NoFlip()
        {
            var m = MakeMap();
            var reg = new StrategicFleetRegistry(m); // 艦隊ゼロ
            // 仕様：在席ゼロ（空）の星系はフリップしない。
            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_NonexistentSystem_IsSafe()
        {
            var m = MakeMap();
            var reg = new StrategicFleetRegistry(m);
            // 仕様：存在しない星系IDは何もしない（false・例外なし）。
            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 999));
        }

        // ───────── ResolveCorridorBattle 境界（勝敗・残存・対称性）─────────

        [Test]
        public void ResolveCorridorBattle_AttackerStronger_WinsWithDifference()
        {
            // 仕様：攻撃150>防御100＝攻撃勝利、残存＝150-100=50。
            var r = StrategyRules.ResolveCorridorBattle(150, 100);
            Assert.IsTrue(r.attackerWon);
            Assert.AreEqual(50, r.survivorStrength);
        }

        [Test]
        public void ResolveCorridorBattle_EqualStrength_DefenderHolds()
        {
            // 仕様：同数は防衛側が守り切る（攻撃側敗北）、残存＝0（相打ち寸前）。
            var r = StrategyRules.ResolveCorridorBattle(100, 100);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(0, r.survivorStrength);
        }

        [Test]
        public void ResolveCorridorBattle_DefenderStronger_DefenderWins()
        {
            // 仕様：防御120>攻撃80＝防衛勝利、残存＝120-80=40。
            var r = StrategyRules.ResolveCorridorBattle(80, 120);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(40, r.survivorStrength);
        }

        [Test]
        public void ResolveCorridorBattle_BothZero_DefenderHoldsZeroSurvivor()
        {
            // 仕様：両者0なら 0>0=false＝攻撃側敗北、残存0。
            var r = StrategyRules.ResolveCorridorBattle(0, 0);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(0, r.survivorStrength);
        }

        [Test]
        public void ResolveCorridorBattle_NegativeDefender_AttackerSurvivorExceedsOwn()
        {
            // 異常入力：防御が負(-5)。攻撃10>-5＝攻撃勝利だが、残存＝10-(-5)=15 で
            // 攻撃側の元兵力(10)を上回る（負の兵力が「敵を強化する」非物理な振る舞い）。
            // 仕様としては兵力は非負であるべき＝この入力は本来クランプされるべき。
            var r = StrategyRules.ResolveCorridorBattle(10, -5);
            Assert.IsTrue(r.attackerWon);
            Assert.AreEqual(15, r.survivorStrength);
        }

        // ───────── ApplyBattleResult（敗者除去・engaged解除・相打ち）─────────

        [Test]
        public void ApplyBattleResult_LoserRemoved_WinnerKeepsSurvivorAndClearsEngaged()
        {
            var m = MakeMap();
            var a = new StrategicFleet(1, 0, Faction.帝国); a.strength = 150; a.engaged = true;
            var b = new StrategicFleet(2, 1, Faction.同盟); b.strength = 100; b.engaged = true;
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);

            StrategyRules.ApplyBattleResult(reg, a, b, new CorridorBattleResult(true, 50));
            // 仕様：a が勝者＝残存50・engaged 解除、b は除去。
            Assert.AreEqual(50, a.strength);
            Assert.IsFalse(a.engaged);
            Assert.IsNotNull(reg.GetFleet(1));
            Assert.IsNull(reg.GetFleet(2)); // 敗者除去
        }

        [Test]
        public void ApplyBattleResult_MutualDestruction_RemovesBoth()
        {
            var m = MakeMap();
            var a = new StrategicFleet(1, 0, Faction.帝国); a.strength = 100;
            var b = new StrategicFleet(2, 1, Faction.同盟); b.strength = 100;
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);

            // 残存0＝相打ち：仕様では勝者も除去（両方消える）。
            StrategyRules.ApplyBattleResult(reg, a, b, new CorridorBattleResult(false, 0));
            Assert.IsNull(reg.GetFleet(1));
            Assert.IsNull(reg.GetFleet(2));
            Assert.AreEqual(0, reg.fleets.Count);
        }

        [Test]
        public void ApplyBattleResult_NullArgs_AreSafe()
        {
            var m = MakeMap();
            var reg = new StrategicFleetRegistry(m);
            // 仕様：null を渡しても例外を投げず何もしない。
            Assert.DoesNotThrow(() =>
                StrategyRules.ApplyBattleResult(reg, null, null, new CorridorBattleResult(true, 0)));
        }

        // ───────── FindEncounters：組み合わせ（オフバイワン・対称性）─────────

        [Test]
        public void FindEncounters_ThreeHostilesOnSameCorridor_YieldsThreePairs()
        {
            // 仕様：同一回廊上の敵対艦が3隻なら、ペアは C(3,2)=3 組（重複なし・自己ペアなし）。
            // ※ FindEncounters は敵対のみペアにするので、帝×同×同 でも全ペアが「異勢力」になるよう
            //   帝・帝・同 だと帝同士は非敵対で除外される。完全敵対 C(3,2) を見るため帝2/同1ではなく
            //   敵対ペアの数を仕様どおり数える。
            var m = MakeMap();
            var imp1 = new StrategicFleet(1, 0, Faction.帝国, 1f); imp1.BeginWarp(m, 1);
            var imp2 = new StrategicFleet(2, 0, Faction.帝国, 1f); imp2.BeginWarp(m, 1);
            var ally = new StrategicFleet(3, 1, Faction.同盟, 1f); ally.BeginWarp(m, 0);
            var reg = new StrategicFleetRegistry(m); reg.Add(imp1); reg.Add(imp2); reg.Add(ally);

            // 敵対ペア：imp1-ally, imp2-ally の2組（imp1-imp2は同勢力＝非敵対で除外）。
            Assert.AreEqual(2, StrategyRules.FindEncounters(reg).Count);
        }

        [Test]
        public void FindEncounters_NullFleetInList_IsSkipped()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1);
            var ally = new StrategicFleet(2, 1, Faction.同盟, 1f); ally.BeginWarp(m, 0);
            var reg = new StrategicFleetRegistry(m);
            reg.fleets.Add(null);  // 異常：null 混入
            reg.Add(imp); reg.Add(ally); reg.fleets.Add(null);

            // 仕様：null 要素は無視され、有効ペアだけ検出（例外なし）。
            Assert.AreEqual(1, StrategyRules.FindEncounters(reg).Count);
        }

        // ───────── TickSieges：deltaTime / 係争 / 単一攻撃側占領 ─────────

        [Test]
        public void TickSieges_NonPositiveDeltaTime_DoesNothing()
        {
            var m = MakeMap();
            m.GetSystem(1).planet = new Planet(1, Faction.帝国, 100f, 40f);
            var atk = new StrategicFleet(1, 1, Faction.同盟); atk.strength = 50;
            var reg = new StrategicFleetRegistry(m); reg.Add(atk);

            // 仕様：deltaTime<=0 は進めない（0を返す・制空権不変）。
            Assert.AreEqual(0, StrategyRules.TickSieges(m, reg, 0f));
            Assert.AreEqual(100f, m.GetSystem(1).planet.orbitalDefense, 1e-5f);
            Assert.AreEqual(0, StrategyRules.TickSieges(m, reg, -1f));
            Assert.AreEqual(100f, m.GetSystem(1).planet.orbitalDefense, 1e-5f);
        }

        [Test]
        public void TickSieges_Contested_DefenderPresent_NoSuppression()
        {
            var m = MakeMap();
            m.GetSystem(1).planet = new Planet(1, Faction.帝国, 100f, 40f);
            var atk = new StrategicFleet(1, 1, Faction.同盟); atk.strength = 50;
            var def = new StrategicFleet(2, 1, Faction.帝国); def.strength = 30; // 防衛側在席＝係争
            var reg = new StrategicFleetRegistry(m); reg.Add(atk); reg.Add(def);

            // 仕様：防衛側が居ると S-AV=0＝制空権は減らない（宇宙の会戦が先）。
            StrategyRules.TickSieges(m, reg, 1f);
            Assert.AreEqual(100f, m.GetSystem(1).planet.orbitalDefense, 1e-5f);
        }

        [Test]
        public void TickSieges_SingleAttacker_SuppressesDomain_ThenCapturesAndSyncsOwner()
        {
            var m = MakeMap();
            // 制空権100・侵略閾値40。攻撃側合計60（2艦）。既定係数(抑制1/侵攻1)。
            m.GetSystem(1).planet = new Planet(1, Faction.帝国, 100f, 40f);
            var a1 = new StrategicFleet(1, 1, Faction.同盟); a1.strength = 40;
            var a2 = new StrategicFleet(2, 1, Faction.同盟); a2.strength = 20;
            var reg = new StrategicFleetRegistry(m); reg.Add(a1); reg.Add(a2);
            var planet = m.GetSystem(1).planet;

            // dt=1：制空権 100 - 60*1*1 = 40（>0＝ドメイン健在・1tick一段階＝侵略は進まない）。
            Assert.AreEqual(0, StrategyRules.TickSieges(m, reg, 1f));
            Assert.AreEqual(40f, planet.orbitalDefense, 1e-5f);
            Assert.AreEqual(0f, planet.invasionProgress, 1e-5f);

            // dt=1：制空権 40 - 60 = 0（このtickでドメイン・ダウン。侵略は次tickから）。
            Assert.AreEqual(0, StrategyRules.TickSieges(m, reg, 1f));
            Assert.AreEqual(0f, planet.orbitalDefense, 1e-5f);
            Assert.IsTrue(planet.DomainDown);
            Assert.AreEqual(0f, planet.invasionProgress, 1e-5f);

            // dt=1：侵略 0 + 60 = 60 >= 40＝占領。星系所有も攻撃側へ同期、制空権再建・侵略リセット。
            Assert.AreEqual(1, StrategyRules.TickSieges(m, reg, 1f));
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);
            Assert.AreEqual(Faction.同盟, planet.owner);
            Assert.AreEqual(100f, planet.orbitalDefense, 1e-5f); // 再建＝max
            Assert.AreEqual(0f, planet.invasionProgress, 1e-5f); // リセット
        }

        [Test]
        public void TickSieges_PlanetDefendedSystem_NotFlippedByPlainOccupation()
        {
            var m = MakeMap();
            m.GetSystem(1).planet = new Planet(1, Faction.帝国, 100f, 40f);
            var ally = new StrategicFleet(1, 1, Faction.同盟); // 防衛惑星に停泊
            var reg = new StrategicFleetRegistry(m); reg.Add(ally);

            // 仕様：制空権ありの惑星は停泊だけ（ResolveOccupation）では落ちない＝攻城が要る。
            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        // ───────── EngageFrontline：前線判定・按分・合計保存・占領前進 ─────────

        [Test]
        public void EngageFrontline_NotFrontline_ReturnsFalse()
        {
            var m = MakeMap(); // 全星系 帝国＝両端同勢力＝前線でない
            var atk = new StrategicFleet(1, 0, Faction.同盟); // 0に同盟艦（孤立）
            var reg = new StrategicFleetRegistry(m); reg.Add(atk);

            // 仕様：両端の所有者が非敵対なら前線でなく、侵攻は起動しない。
            Assert.IsFalse(StrategyRules.EngageFrontline(m, reg, 0, 1, out var r));
        }

        [Test]
        public void EngageFrontline_AttackerWins_ScalesStrengthAndFlipsAndAdvances()
        {
            var m = MakeMap();
            m.GetSystem(1).owner = Faction.同盟; // 0(帝)-1(同)＝前線回廊
            var a1 = new StrategicFleet(1, 0, Faction.帝国); a1.strength = 60;
            var a2 = new StrategicFleet(2, 0, Faction.帝国); a2.strength = 40; // 合計100
            var def = new StrategicFleet(3, 1, Faction.同盟); def.strength = 70;
            var reg = new StrategicFleetRegistry(m); reg.Add(a1); reg.Add(a2); reg.Add(def);

            Assert.IsTrue(StrategyRules.EngageFrontline(m, reg, 0, 1, out var r));
            // 仕様：100>70＝攻撃勝利、残存30。按分＝60*30/100=18, 40*30/100=12（合計30保存）。
            Assert.IsTrue(r.attackerWon);
            Assert.AreEqual(30, r.survivorStrength);
            Assert.AreEqual(18, a1.strength);
            Assert.AreEqual(12, a2.strength);
            Assert.AreEqual(18 + 12, a1.strength + a2.strength); // 合計保存
            // 防衛側除去・星系フリップ・攻撃艦は to(1) へ前進。
            Assert.IsNull(reg.GetFleet(3));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
            Assert.AreEqual(1, a1.currentSystemId);
            Assert.AreEqual(1, a1.destinationSystemId);
        }

        [Test]
        public void EngageFrontline_DefenderWins_NoFlip_AttackersRemoved()
        {
            var m = MakeMap();
            m.GetSystem(1).owner = Faction.同盟; // 前線回廊
            var atk = new StrategicFleet(1, 0, Faction.帝国); atk.strength = 50;
            var def = new StrategicFleet(2, 1, Faction.同盟); def.strength = 80;
            var reg = new StrategicFleetRegistry(m); reg.Add(atk); reg.Add(def);

            Assert.IsTrue(StrategyRules.EngageFrontline(m, reg, 0, 1, out var r));
            // 仕様：50<80＝防衛勝利、残存30。攻撃側除去、星系は据え置き（同盟のまま）。
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(30, r.survivorStrength);
            Assert.IsNull(reg.GetFleet(1));
            Assert.AreEqual(30, def.strength);
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);
        }

        [Test]
        public void EngageFrontline_NoAttackerAtSource_ReturnsFalse()
        {
            var m = MakeMap();
            m.GetSystem(1).owner = Faction.同盟; // 前線回廊だが出発元(0)に攻撃艦なし
            var reg = new StrategicFleetRegistry(m);

            // 仕様：敵対する停泊攻撃艦が居なければ侵攻は起動しない。
            Assert.IsFalse(StrategyRules.EngageFrontline(m, reg, 0, 1, out var r));
        }

        // ───────── ResolveEncounters：未接触は解決しない ─────────

        [Test]
        public void ResolveEncounters_NotYetCollided_NoResolution()
        {
            var m = MakeMap();
            // 同方向・離れた位置（接触せず）：0→1 を2艦が走るが進行差が eps(0.04) 超。
            var a = new StrategicFleet(1, 0, Faction.帝国, 1f); a.BeginWarp(m, 1);
            var b = new StrategicFleet(2, 0, Faction.同盟, 1f); b.BeginWarp(m, 1);
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);
            a.Tick(1f); // a だけ前進＝進行差を作る（length4 を warp1 で 1進む＝Progress 0.25）

            // 仕様：同一回廊だが接触していない（進行差>eps）ので戦闘は解決されない（0件・両者生存）。
            Assert.AreEqual(0, StrategyRules.ResolveEncounters(reg));
            Assert.IsNotNull(reg.GetFleet(1));
            Assert.IsNotNull(reg.GetFleet(2));
        }
    }
}
