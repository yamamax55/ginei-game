using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍団隊形<b>命令</b>の純ロジック：軍団キー（シーン分離）／手動と AI 自動の優先順位／
    /// 隷下スロットの割り当て（軍団長基準）／形成判定／前列交代。
    /// 「軍団全体の隊形（艦隊の並べ方）」の層であり、各艦隊内の配下艦の陣形（Squadron）とは別レイヤー。
    /// </summary>
    public class CorpsFormationOrderRulesTests
    {
        // ===== 軍団キー（別戦場・別軍団が混ざらない）=====

        [Test]
        public void Key_SameCorpsNameInDifferentScenes_AreDistinct()
        {
            string a = CorpsFormationOrderRules.MakeKey(101, "帝国", "第1軍団");
            string b = CorpsFormationOrderRules.MakeKey(202, "帝国", "第1軍団");
            Assert.AreNotEqual(a, b, "別戦場（別シーン）の同名軍団が同一キーになってはいけない");
            Assert.IsFalse(CorpsFormationOrderRules.IsSameCorps(a, b));
        }

        [Test]
        public void Key_SameSceneSameCorps_AreEqual()
        {
            string a = CorpsFormationOrderRules.MakeKey(101, "帝国", "第1軍団");
            string b = CorpsFormationOrderRules.MakeKey(101, "帝国", "第1軍団");
            Assert.IsTrue(CorpsFormationOrderRules.IsSameCorps(a, b));
        }

        [Test]
        public void Key_DifferentFactionOrCorps_AreDistinct()
        {
            string imp = CorpsFormationOrderRules.MakeKey(1, "帝国", "第1軍団");
            string all = CorpsFormationOrderRules.MakeKey(1, "同盟", "第1軍団");
            string second = CorpsFormationOrderRules.MakeKey(1, "帝国", "第2軍団");
            Assert.AreNotEqual(imp, all);
            Assert.AreNotEqual(imp, second);
        }

        [Test]
        public void Key_RoundTripsSceneFactionAndCorpsName()
        {
            string k = CorpsFormationOrderRules.MakeKey(77, "同盟", "第13艦隊軍団");
            Assert.AreEqual(77, CorpsFormationOrderRules.SceneKeyOf(k));
            Assert.AreEqual("同盟", CorpsFormationOrderRules.FactionOf(k));
            Assert.AreEqual("第13艦隊軍団", CorpsFormationOrderRules.CorpsNameOf(k));
            Assert.IsTrue(CorpsFormationOrderRules.BelongsToScene(k, 77));
            Assert.IsFalse(CorpsFormationOrderRules.BelongsToScene(k, 78));
        }

        [Test]
        public void Key_NegativeSceneHandleIsParsed()
        {
            // Scene.handle は負値も取りうる（会戦シーンのハンドル）。
            string k = CorpsFormationOrderRules.MakeKey(-42, "帝国", "第1軍団");
            Assert.AreEqual(-42, CorpsFormationOrderRules.SceneKeyOf(k));
            Assert.AreEqual("第1軍団", CorpsFormationOrderRules.CorpsNameOf(k));
        }

        [Test]
        public void AdhocKey_IsUniquePerCommander()
        {
            string a = CorpsFormationOrderRules.MakeAdhocKey(1, "帝国", 1001L);
            string b = CorpsFormationOrderRules.MakeAdhocKey(1, "帝国", 1002L);
            Assert.AreNotEqual(a, b, "別々の臨時編成が同一キーになってはいけない");
            Assert.IsTrue(CorpsFormationOrderRules.IsAdhoc(a));
            Assert.AreEqual("臨時軍団", CorpsFormationOrderRules.DisplayName(a));
        }

        [Test]
        public void DisplayName_NamedCorpsKeepsItsName()
        {
            string k = CorpsFormationOrderRules.MakeKey(3, "帝国", "第1軍団");
            Assert.AreEqual("第1軍団", CorpsFormationOrderRules.DisplayName(k));
            Assert.IsFalse(CorpsFormationOrderRules.IsAdhoc(k));
        }

        [Test]
        public void Key_NullAndEmptyAreSafe()
        {
            Assert.AreEqual(int.MinValue, CorpsFormationOrderRules.SceneKeyOf(null));
            Assert.AreEqual("", CorpsFormationOrderRules.FactionOf(null));
            Assert.AreEqual("", CorpsFormationOrderRules.CorpsNameOf(""));
            Assert.IsFalse(CorpsFormationOrderRules.IsSameCorps(null, null));
            Assert.AreEqual("臨時軍団", CorpsFormationOrderRules.DisplayName(""));
        }

        // ===== 手動 / AI 自動の優先順位 =====

        [Test]
        public void ManualOrder_BeatsAiRecommendation()
        {
            Formation f = CorpsFormationOrderRules.ResolveFormation(
                true, Formation.横陣, Formation.円陣, out CorpsOrderSource src);
            Assert.AreEqual(Formation.横陣, f, "AI の推奨で手動指定が戻ってはいけない");
            Assert.AreEqual(CorpsOrderSource.手動, src);
        }

        [Test]
        public void NoManualOrder_UsesAiRecommendation()
        {
            Formation f = CorpsFormationOrderRules.ResolveFormation(
                false, Formation.横陣, Formation.円陣, out CorpsOrderSource src);
            Assert.AreEqual(Formation.円陣, f);
            Assert.AreEqual(CorpsOrderSource.自動, src);
        }

        [Test]
        public void AiMayOverride_OnlyWithoutManualOrder()
        {
            Assert.IsTrue(CorpsFormationOrderRules.AiMayOverride(false));
            Assert.IsFalse(CorpsFormationOrderRules.AiMayOverride(true));
        }

        [Test]
        public void ManualOrder_SurvivesRepeatedAiCycles()
        {
            // AI の解決周期を何度回しても手動指定は保たれる（受入条件：意図せず戻らない）。
            for (int cycle = 0; cycle < 10; cycle++)
            {
                Formation f = CorpsFormationOrderRules.ResolveFormation(
                    true, Formation.方陣, Formation.紡錘陣, out _);
                Assert.AreEqual(Formation.方陣, f);
                Assert.IsFalse(CorpsFormationOrderRules.ShouldReleaseManual(true, true, false, false));
            }
        }

        [Test]
        public void ShouldReleaseManual_OnlyOnExplicitOrCorpsLossOrRetreat()
        {
            Assert.IsFalse(CorpsFormationOrderRules.ShouldReleaseManual(true, true, false, false), "平時は解除しない");
            Assert.IsTrue(CorpsFormationOrderRules.ShouldReleaseManual(true, true, false, true), "明示解除");
            Assert.IsTrue(CorpsFormationOrderRules.ShouldReleaseManual(true, false, false, false), "軍団消滅");
            Assert.IsTrue(CorpsFormationOrderRules.ShouldReleaseManual(true, true, true, false), "総退却");
            Assert.IsFalse(CorpsFormationOrderRules.ShouldReleaseManual(false, false, true, true), "そもそも手動でない");
        }

        // ===== 隷下スロットの割り当て（軍団長基準）=====

        [Test]
        public void SubordinateSlots_CountMatchesSubordinates()
        {
            var offsets = CorpsFormationOrderRules.SubordinateSlotOffsets(5, Formation.横陣, 6f);
            Assert.AreEqual(4, offsets.Count, "軍団長を除いた隷下ぶんのスロットが要る");
        }

        [Test]
        public void SubordinateSlots_SingleFleetHasNone()
        {
            Assert.AreEqual(0, CorpsFormationOrderRules.SubordinateSlotOffsets(1, Formation.方陣, 6f).Count);
            Assert.AreEqual(0, CorpsFormationOrderRules.SubordinateSlotOffsets(0, Formation.方陣, 6f).Count);
        }

        [Test]
        public void SubordinateSlots_AreRelativeToCommanderSlot()
        {
            const float spacing = 6f;
            var slots = CorpsFormationRules.ComputeSlots(6, Formation.方陣, spacing);
            Vector2 cmd = CorpsFormationOrderRules.CommanderLocal(slots);
            var offsets = CorpsFormationOrderRules.SubordinateSlotOffsets(6, Formation.方陣, spacing);

            int k = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].commander) continue;
                Assert.AreEqual(slots[i].localPos.x - cmd.x, offsets[k].x, 1e-4f);
                Assert.AreEqual(slots[i].localPos.y - cmd.y, offsets[k].y, 1e-4f);
                k++;
            }
            Assert.AreEqual(offsets.Count, k);
        }

        [Test]
        public void SubordinateSlots_AllAheadOfCommander()
        {
            // 軍団長は後方中央＝原点。隷下は必ず前方(+Y)側に就く（軍団長を前線に出さない）。
            var offsets = CorpsFormationOrderRules.SubordinateSlotOffsets(8, Formation.横陣, 6f);
            for (int i = 0; i < offsets.Count; i++)
                Assert.Greater(offsets[i].y, 0f, "隷下スロットが軍団長より後方にある");
        }

        [Test]
        public void SubordinateSlots_DifferentFormationsGiveDifferentLayouts()
        {
            // 「A軍団＝横陣／B軍団＝方陣」を同時に維持できる前提＝隊形ごとに配置が別物であること。
            var line = CorpsFormationOrderRules.SubordinateSlotOffsets(7, Formation.横陣, 6f);
            var square = CorpsFormationOrderRules.SubordinateSlotOffsets(7, Formation.方陣, 6f);
            Assert.AreEqual(line.Count, square.Count);
            bool anyDifferent = false;
            for (int i = 0; i < line.Count; i++)
                if ((line[i] - square[i]).sqrMagnitude > 1e-4f) { anyDifferent = true; break; }
            Assert.IsTrue(anyDifferent, "横陣と方陣で軍団の並べ方が変わらないのはおかしい");
        }

        [Test]
        public void CommanderLocal_NullSlotsIsOrigin()
        {
            Assert.AreEqual(Vector2.zero, CorpsFormationOrderRules.CommanderLocal(null));
            Assert.AreEqual(Vector2.zero, CorpsFormationOrderRules.CommanderLocal(new List<CorpsSlot>()));
        }

        // ===== 形成判定 =====

        [Test]
        public void IsFormed_WithinToleranceIsComplete()
        {
            Assert.IsTrue(CorpsFormationOrderRules.IsFormed(3f, 8f));
            Assert.IsTrue(CorpsFormationOrderRules.IsFormed(8f, 8f));
            Assert.IsFalse(CorpsFormationOrderRules.IsFormed(9f, 8f));
        }

        [Test]
        public void FormationProgress_IsOneWhenFormedAndDecreasesWithError()
        {
            Assert.AreEqual(1f, CorpsFormationOrderRules.FormationProgress(0f, 8f), 1e-4f);
            Assert.AreEqual(1f, CorpsFormationOrderRules.FormationProgress(8f, 8f), 1e-4f);
            float near = CorpsFormationOrderRules.FormationProgress(16f, 8f);
            float far = CorpsFormationOrderRules.FormationProgress(80f, 8f);
            Assert.Less(near, 1f);
            Assert.Less(far, near, "遠いほど形成の進みは小さい");
            Assert.GreaterOrEqual(far, 0f);
        }

        // ===== 前列交代 =====

        [Test]
        public void RotateOrder_MovesFrontRankToBack()
        {
            // 方陣・隷下4隊＝2列。前列2隊が後方へ回る。
            int[] order = CorpsFormationOrderRules.RotateOrder(4, Formation.方陣);
            Assert.AreEqual(4, order.Length);
            int cols = CorpsFormationRules.ColumnsFor(Formation.方陣, 4);
            for (int i = 0; i < 4 - cols; i++) Assert.AreEqual(cols + i, order[i]);
            for (int i = 0; i < cols; i++) Assert.AreEqual(i, order[4 - cols + i]);
        }

        [Test]
        public void RotateOrder_IsAPermutation()
        {
            int[] order = CorpsFormationOrderRules.RotateOrder(9, Formation.横陣);
            var seen = new HashSet<int>();
            for (int i = 0; i < order.Length; i++)
            {
                Assert.IsTrue(order[i] >= 0 && order[i] < 9);
                Assert.IsTrue(seen.Add(order[i]), "同じ艦隊が2回現れた（隊列が壊れる）");
            }
            Assert.AreEqual(9, seen.Count);
        }

        [Test]
        public void RotateOrder_EmptyIsSafe()
        {
            Assert.AreEqual(0, CorpsFormationOrderRules.RotateOrder(0, Formation.方陣).Length);
            Assert.AreEqual(0, CorpsFormationOrderRules.RotateOrder(-3, Formation.方陣).Length);
        }

        [Test]
        public void AutoRotates_OnlySquare()
        {
            Assert.IsTrue(CorpsFormationOrderRules.AutoRotates(Formation.方陣));
            Assert.IsFalse(CorpsFormationOrderRules.AutoRotates(Formation.横陣));
            Assert.IsFalse(CorpsFormationOrderRules.AutoRotates(Formation.円陣));
        }

        // ===== 表示 =====

        [Test]
        public void StatusLabel_ShowsCorpsFormationSourceAndProgress()
        {
            string s = CorpsFormationOrderRules.StatusLabel("第1軍団", Formation.横陣, true, false);
            StringAssert.Contains("第1軍団", s);
            StringAssert.Contains("横陣", s);
            StringAssert.Contains("手動", s);
            StringAssert.Contains("形成中", s);

            string done = CorpsFormationOrderRules.StatusLabel("", Formation.方陣, false, true);
            StringAssert.Contains("臨時軍団", done);
            StringAssert.Contains("自動", done);
            StringAssert.Contains("完了", done);
        }

        // ===== 命令レコード =====

        [Test]
        public void Order_StoresKeyFormationAndSource()
        {
            string key = CorpsFormationOrderRules.MakeKey(5, "帝国", "第1軍団");
            var o = new CorpsFormationOrder(key, Formation.鶴翼陣, CorpsOrderSource.手動);
            Assert.AreEqual(key, o.corpsKey);
            Assert.AreEqual(Formation.鶴翼陣, o.formation);
            Assert.AreEqual(CorpsOrderSource.手動, o.source);
            Assert.IsTrue(o.active);
        }

        [Test]
        public void TwoCorps_KeepIndependentOrders()
        {
            // 受入条件：A軍団＝横陣、B軍団＝方陣を同時に維持できる（命令はキーで引く辞書に持つ）。
            var ledger = new Dictionary<string, CorpsFormationOrder>();
            string a = CorpsFormationOrderRules.MakeKey(1, "帝国", "A軍団");
            string b = CorpsFormationOrderRules.MakeKey(1, "帝国", "B軍団");
            ledger[a] = new CorpsFormationOrder(a, Formation.横陣, CorpsOrderSource.手動);
            ledger[b] = new CorpsFormationOrder(b, Formation.方陣, CorpsOrderSource.手動);

            // A へ再命令しても B は動かない。
            ledger[a] = new CorpsFormationOrder(a, Formation.円陣, CorpsOrderSource.手動);
            Assert.AreEqual(Formation.円陣, ledger[a].formation);
            Assert.AreEqual(Formation.方陣, ledger[b].formation, "A への再命令が B へ波及した");
        }
    }
}
