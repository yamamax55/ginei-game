using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 支援要請の実行中に敗走したとき、<b>本物の <see cref="FleetAI"/>.Update が</b>
    /// 引き受けた命令を中断して撤退へ移るかを、実際の Game コンポーネントで確かめる統合試験。
    ///
    /// EditMode 側（<c>ManualOverrideRulesTests</c>）は判断そのものを固定するが、
    /// <b>それだけでは「実際に FleetAI が中断するか」は担保できない</b>
    /// （呼び忘れ・順序ミス・コンポーネント欠落は素通りする）。ここはその隙間を埋める。
    ///
    /// <b>結果は書かない</b>：士気を下げるだけで、<c>manualOverride</c> の解除も
    /// <c>currentState</c> も触らず、フレームを進めて観測する。
    /// </summary>
    public class SupportEmergencyPlayModeTests
    {
        private GameObject fleetGo;

        [TearDown]
        public void TearDown()
        {
            if (fleetGo != null) Object.DestroyImmediate(fleetGo);
            fleetGo = null;
        }

        /// <summary>
        /// 会戦の艦隊を最小構成で組む（RequireComponent の依存を明示的に足す）。
        /// AI が即座に離脱判定へ入らないよう原点付近に置く。
        /// </summary>
        private GameObject BuildFleet(string name, Faction faction)
        {
            var go = new GameObject(name);
            go.transform.position = Vector3.zero;

            var strength = go.AddComponent<FleetStrength>();
            strength.faction = faction;
            strength.admiralName = name;
            strength.strength = 10000;
            strength.maxStrength = 10000;

            go.AddComponent<FleetMorale>();          // RequireComponent(FleetStrength)
            go.AddComponent<FleetMovement>();
            go.AddComponent<WeaponArc>();            // FleetWeapon の RequireComponent
            go.AddComponent<FleetWeapon>();
            var ai = go.AddComponent<FleetAI>();     // RequireComponent(Movement/Weapon/Strength)
            ai.autoFormation = false;                // 陣形の自動切替はこの試験の対象外
            return go;
        }

        /// <summary>★支援要請の命令は、敗走したら中断されて撤退へ移る。</summary>
        [UnityTest]
        public IEnumerator SupportOrder_IsInterruptedByRout()
        {
            fleetGo = BuildFleet("QA支援艦隊", Faction.同盟);
            yield return null;   // Awake/Start を通す

            var ai = fleetGo.GetComponent<FleetAI>();
            var movement = fleetGo.GetComponent<FleetMovement>();
            var morale = fleetGo.GetComponent<FleetMorale>();

            // 承諾された支援要請と同じ形で命令を立てる（SupportRequestDirector と同じ呼び方）。
            var destination = new Vector2(40f, 0f);
            movement.SetDestination(destination, null);
            ai.BeginManualOverride(ManualOverrideKind.支援要請);

            // 平時：AI は口を出さない＝行き先が保たれる。
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(ManualOverrideKind.支援要請, ai.OverrideKind,
                "平時に支援の上書きが外れている");
            Assert.IsTrue(movement.IsMoving, "移動が止まっている");
            Assert.AreEqual(destination.x, movement.Destination.x, 0.01f,
                "AI に行き先を上書きされた（承諾した移動が尊重されていない）");

            // ★入力条件だけを変える：士気を 0 にする（敗走フラグも撤退状態も書かない）。
            morale.ApplyMoraleDelta(-morale.morale);
            Assert.IsTrue(morale.IsRouted, "士気0で敗走扱いになっていない");

            // 本物の FleetAI.Update に判断させる。
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(ManualOverrideKind.なし, ai.OverrideKind,
                "敗走しても支援の命令が中断されない（承諾したせいで退がれない＝回帰）");
            Assert.AreEqual(FleetAI.AIState.撤退, ai.currentState,
                "中断後に撤退状態へ移っていない");
        }

        /// <summary>★対照：直接命令は敗走でも中断しない（既存動作の維持）。</summary>
        [UnityTest]
        public IEnumerator DirectOrder_SurvivesRout()
        {
            fleetGo = BuildFleet("QA直接命令艦隊", Faction.同盟);
            yield return null;

            var ai = fleetGo.GetComponent<FleetAI>();
            var movement = fleetGo.GetComponent<FleetMovement>();
            var morale = fleetGo.GetComponent<FleetMorale>();

            var destination = new Vector2(40f, 0f);
            movement.SetDestination(destination, null);
            ai.BeginManualOverride();   // 引数なし＝直接命令（FleetCommander と同じ呼び方）

            Assert.AreEqual(ManualOverrideKind.直接命令, ai.OverrideKind,
                "引数なしの BeginManualOverride が 直接命令 になっていない");

            morale.ApplyMoraleDelta(-morale.morale);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(ManualOverrideKind.直接命令, ai.OverrideKind,
                "直接命令が敗走で中断されるようになっている（既存仕様の変更）");
            Assert.IsTrue(movement.IsMoving, "直接命令の移動が止められている");
        }

        /// <summary>★攻撃の支援を中断したら、手動標的が残らない（退がりながら追尾しない）。</summary>
        [UnityTest]
        public IEnumerator SupportAttack_ClearsManualTargetOnInterrupt()
        {
            fleetGo = BuildFleet("QA支援攻撃艦隊", Faction.同盟);
            var enemyGo = BuildFleet("QA敵艦隊", Faction.帝国);
            var enemySquadron = enemyGo.AddComponent<Squadron>();
            enemyGo.transform.position = new Vector3(30f, 0f, 0f);
            yield return null;

            var ai = fleetGo.GetComponent<FleetAI>();
            var weapon = fleetGo.GetComponent<FleetWeapon>();
            var morale = fleetGo.GetComponent<FleetMorale>();

            weapon.SetManualTargetFleet(enemySquadron);
            ai.BeginManualOverride(ManualOverrideKind.支援要請);
            yield return null;

            Assert.IsTrue(weapon.HasManualTarget, "手動標的が設定されていない");

            morale.ApplyMoraleDelta(-morale.morale);
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(ManualOverrideKind.なし, ai.OverrideKind,
                "敗走しても支援の攻撃命令が中断されない");
            Assert.IsFalse(weapon.HasManualTarget,
                "中断したのに手動標的が残っている（退がりながら追尾し続ける）");

            Object.DestroyImmediate(enemyGo);
        }

        /// <summary>平時は中断しない＝引き受けた支援を勝手に投げ出さない。</summary>
        [UnityTest]
        public IEnumerator SupportOrder_IsKeptWhileNotInEmergency()
        {
            fleetGo = BuildFleet("QA平時支援艦隊", Faction.同盟);
            yield return null;

            var ai = fleetGo.GetComponent<FleetAI>();
            var movement = fleetGo.GetComponent<FleetMovement>();

            movement.SetDestination(new Vector2(60f, 0f), null);
            ai.BeginManualOverride(ManualOverrideKind.支援要請);

            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(ManualOverrideKind.支援要請, ai.OverrideKind,
                "敗走していないのに支援の命令が中断された");
            Assert.IsTrue(movement.IsMoving, "平時に移動が止められている");
        }
    }
}
