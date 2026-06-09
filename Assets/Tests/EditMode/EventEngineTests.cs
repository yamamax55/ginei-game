using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// イベントエンジン（#116・横断基盤）を固定する：発火資格（条件/一回限り/クールダウン）、重み付き抽選、
    /// 発火→キュー→選択肢解決→効果適用の通し、多重提示の抑止、サンプルイベントの End-to-End。
    /// </summary>
    public class EventEngineTests
    {
        // ===== EventRules =====

        [Test]
        public void IsEligible_RespectsConditionOnceAndCooldown()
        {
            var def = new GameEventDef("e", "t", "b") { repeatable = false };
            var st = new EventRuntimeState();
            Assert.IsTrue(EventRules.IsEligible(def, st, null, 0f)); // 条件null=真・未発火

            EventRules.MarkFired(st, 0f);
            Assert.IsFalse(EventRules.IsEligible(def, st, null, 10f)); // 一回限り＝再発火不可

            var rep = new GameEventDef("r", "t", "b") { repeatable = true, cooldown = 5f };
            var rs = new EventRuntimeState();
            EventRules.MarkFired(rs, 0f);
            Assert.IsFalse(EventRules.IsEligible(rep, rs, null, 3f)); // クールダウン中
            Assert.IsTrue(EventRules.IsEligible(rep, rs, null, 5f));  // 明けたら可
        }

        [Test]
        public void ConditionGatesEligibility()
        {
            var def = new GameEventDef("c", "t", "b").When(ctx => ctx != null && ctx.faction == Faction.帝国);
            Assert.IsTrue(EventRules.IsEligible(def, new EventRuntimeState(), new EventContext(Faction.帝国), 0f));
            Assert.IsFalse(EventRules.IsEligible(def, new EventRuntimeState(), new EventContext(Faction.同盟), 0f));
        }

        [Test]
        public void SelectWeighted_PicksByWeight()
        {
            var a = new GameEventDef("a", "t", "b") { weight = 1f };
            var b = new GameEventDef("b", "t", "b") { weight = 3f }; // 総重み4
            var list = new List<GameEventDef> { a, b };
            Assert.AreSame(a, EventRules.SelectWeighted(list, 0.1f)); // target 0.4 < 1 → a
            Assert.AreSame(b, EventRules.SelectWeighted(list, 0.5f)); // target 2.0 ≥ 1 → b
            Assert.IsNull(EventRules.SelectWeighted(new List<GameEventDef>(), 0.5f));
        }

        // ===== EventEngine =====

        [Test]
        public void Engine_FiresEligible_IntoQueue()
        {
            var engine = new EventEngine();
            engine.Register(new GameEventDef("e", "t", "b").AddChoice("OK"));

            var fired = engine.Tick(null, now: 0f, roll: 0.5f);
            Assert.IsNotNull(fired);
            Assert.AreEqual(1, engine.PendingCount);
            Assert.AreSame(fired, engine.Current);
            Assert.AreEqual(1, engine.FireCount("e"));
        }

        [Test]
        public void Engine_DoesNotDoubleQueueSameEvent()
        {
            var engine = new EventEngine();
            engine.Register(new GameEventDef("e", "t", "b") { repeatable = true }.AddChoice("OK"));

            engine.Tick(null, 0f, 0.5f);
            engine.Tick(null, 1f, 0.5f); // 既にキュー中＝多重提示しない
            Assert.AreEqual(1, engine.PendingCount);
        }

        [Test]
        public void Engine_ResolveAppliesEffect_AndPops()
        {
            int applied = 0;
            var def = new GameEventDef("e", "t", "b")
                .AddChoice("A", ctx => applied = 1)
                .AddChoice("B", ctx => applied = 2);
            var engine = new EventEngine();
            engine.Register(def);
            engine.Tick(null, 0f, 0.5f);

            engine.Resolve(1, null); // 選択肢B
            Assert.AreEqual(2, applied);
            Assert.AreEqual(0, engine.PendingCount); // キューから外れる
        }

        [Test]
        public void Engine_OnceOnly_NotRefiredAfterResolved()
        {
            var engine = new EventEngine();
            engine.Register(new GameEventDef("once", "t", "b") { repeatable = false }.AddChoice("OK"));
            engine.Tick(null, 0f, 0.5f);
            engine.Resolve(0, null);
            Assert.IsNull(engine.Tick(null, 100f, 0.5f)); // 一回限り＝二度と発火しない
        }

        // ===== サンプル End-to-End（#116 完了条件） =====

        [Test]
        public void Sample_SupplyCrisis_FiresWhenDepleted_AndChoiceRestocks()
        {
            var front = new ResourceStockpile(0, 0, 0); // 払底＝危機の条件
            var ctx = new EventContext(Faction.帝国, payload: front);

            var engine = new EventEngine();
            engine.Register(SampleEvents.SupplyCrisis());
            engine.Register(SampleEvents.HeroAppears());

            // 危機(weight1)と英雄(0.5)が候補（総重み1.5）。危機は区間[0,0.667)＝roll=0.5 で危機を引く
            var fired = engine.Tick(ctx, now: 0f, roll: 0.5f);
            Assert.AreEqual("supply_crisis", fired.id);

            engine.Resolve(0, ctx); // 緊急増産（弾薬+50）
            Assert.AreEqual(50f, front.ammo, 1e-4f);
        }

        [Test]
        public void Sample_SupplyCrisis_DoesNotFireWhenStocked()
        {
            var front = new ResourceStockpile(100, 100, 100); // 充足＝条件不成立
            var ctx = new EventContext(Faction.帝国, payload: front);
            var engine = new EventEngine();
            engine.Register(SampleEvents.SupplyCrisis());

            Assert.IsNull(engine.Tick(ctx, 0f, 0.5f)); // 条件を満たさず発火しない
        }
    }
}
