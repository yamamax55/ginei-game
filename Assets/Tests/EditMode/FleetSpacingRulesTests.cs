using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 隊列整流（味方の重なり回避・追い越し制限）：FleetSpacingRules の純ロジックテスト。
    /// SeparationPush（重なり有/無/完全重複）と LimitOvertake（前方味方有/無/後方・横成分保持）を担保する。
    /// </summary>
    public class FleetSpacingRulesTests
    {
        // ── SeparationPush ──────────────────────────────────────
        [Test]
        public void SeparationPush_重なり無しならゼロ()
        {
            var friends = new List<FleetSpacingRules.Neighbor> { new FleetSpacingRules.Neighbor(new Vector2(100f, 0f), 5f) };
            Vector2 push = FleetSpacingRules.SeparationPush(Vector2.zero, 5f, friends, 1f, 1.5f, 1);
            Assert.AreEqual(0f, push.magnitude, 1e-4f);
        }

        [Test]
        public void SeparationPush_重なるとき離れる方向へ押す()
        {
            // 味方は右(+x)に居て近接（半径和10+margin1=11 に対し距離4）→ 左(-x)へ押し出す
            var friends = new List<FleetSpacingRules.Neighbor> { new FleetSpacingRules.Neighbor(new Vector2(4f, 0f), 5f) };
            Vector2 push = FleetSpacingRules.SeparationPush(Vector2.zero, 5f, friends, 1f, 1f, 1);
            Assert.Less(push.x, 0f);             // 左へ
            Assert.AreEqual(0f, push.y, 1e-4f);  // 真横なのでyは0
            Assert.AreEqual(-7f, push.x, 1e-3f); // 重なり量(11-4)=7 × strength1、−x方向
        }

        [Test]
        public void SeparationPush_完全重複でも非ゼロで散る()
        {
            var friends = new List<FleetSpacingRules.Neighbor> { new FleetSpacingRules.Neighbor(Vector2.zero, 5f) };
            Vector2 push = FleetSpacingRules.SeparationPush(Vector2.zero, 5f, friends, 1f, 1f, 7);
            Assert.Greater(push.magnitude, 0f);
        }

        // ── LimitOvertake ───────────────────────────────────────
        [Test]
        public void LimitOvertake_前方に味方なしなら素通し()
        {
            var friends = new List<Vector2> { new Vector2(0f, -10f) }; // 後方(-y)の味方のみ
            Vector2 dest = new Vector2(0f, 50f);
            Vector2 r = FleetSpacingRules.LimitOvertake(Vector2.zero, dest, Vector2.up, friends, 5f);
            Assert.AreEqual(dest, r);
        }

        [Test]
        public void LimitOvertake_前方の味方手前でクランプ()
        {
            // 前進方向 +y、前方(+y20)に味方。keepBehind5 → 上限 y=15。dest y=50 を 15 にクランプ。
            var friends = new List<Vector2> { new Vector2(0f, 20f) };
            Vector2 r = FleetSpacingRules.LimitOvertake(Vector2.zero, new Vector2(0f, 50f), Vector2.up, friends, 5f);
            Assert.AreEqual(15f, r.y, 1e-3f);
        }

        [Test]
        public void LimitOvertake_横成分は保持する()
        {
            // 前方(+y20)に味方。dest=(30,50) → y は 15 にクランプされるが x(横)は保持。
            var friends = new List<Vector2> { new Vector2(0f, 20f) };
            Vector2 r = FleetSpacingRules.LimitOvertake(Vector2.zero, new Vector2(30f, 50f), Vector2.up, friends, 5f);
            Assert.AreEqual(30f, r.x, 1e-3f);
            Assert.AreEqual(15f, r.y, 1e-3f);
        }

        [Test]
        public void LimitOvertake_既に上限内なら素通し()
        {
            var friends = new List<Vector2> { new Vector2(0f, 20f) };
            Vector2 dest = new Vector2(0f, 10f); // 上限15以内
            Vector2 r = FleetSpacingRules.LimitOvertake(Vector2.zero, dest, Vector2.up, friends, 5f);
            Assert.AreEqual(dest, r);
        }
    }
}
