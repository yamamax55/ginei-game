using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍団編成メニューの配属・解除（<see cref="CorpsAssignmentRules"/>）。
    /// 二重所属が起きないこと・他勢力へ配属できないこと・進行中の行動を止めないこと・
    /// 艦艇数や所在地を書き換えないことを固定する。
    /// </summary>
    public class CorpsAssignmentRulesTests
    {
        private static StrategicFleet Fleet(int id, Faction f, int corpsId = -1, string corpsName = null,
                                            bool flagship = false, int systemId = 0)
            => new StrategicFleet(id, systemId, f)
            { strength = 100, corpsId = corpsId, corpsName = corpsName, isCorpsFlagship = flagship };

        private static CorpsAssignmentRules.CorpsInfo Corps(int id, string name, Faction f)
            => new CorpsAssignmentRules.CorpsInfo(id, name, f, 0, -1);

        // ── 可否 ──

        [Test]
        public void Assign_OwnFleetToOwnCorps_Allowed()
        {
            StrategicFleet f = Fleet(1, Faction.同盟);
            Assert.AreEqual(CorpsAssignmentRejection.なし,
                CorpsAssignmentRules.CanAssign(f, Faction.同盟, 3, Faction.同盟));
        }

        [Test]
        public void Assign_ToOtherFactionCorps_Rejected()
        {
            StrategicFleet f = Fleet(1, Faction.同盟);
            Assert.AreEqual(CorpsAssignmentRejection.他勢力の軍団,
                CorpsAssignmentRules.CanAssign(f, Faction.同盟, 3, Faction.帝国));
            Assert.IsFalse(CorpsAssignmentRules.Assign(f, Faction.同盟, Corps(3, "帝国第1軍団", Faction.帝国), out _));
            Assert.AreEqual(-1, f.corpsId, "拒否したのに所属が変わってはいけない");
        }

        [Test]
        public void Assign_OtherFactionsFleet_Rejected()
        {
            StrategicFleet f = Fleet(1, Faction.帝国);
            Assert.AreEqual(CorpsAssignmentRejection.他勢力の艦隊,
                CorpsAssignmentRules.CanAssign(f, Faction.同盟, 3, Faction.同盟));
        }

        [Test]
        public void Assign_SameCorpsTwice_Rejected()
        {
            StrategicFleet f = Fleet(1, Faction.同盟, 3, "第1軍団");
            Assert.AreEqual(CorpsAssignmentRejection.すでにその軍団,
                CorpsAssignmentRules.CanAssign(f, Faction.同盟, 3, Faction.同盟));
        }

        [Test]
        public void Assign_WhileEngagedOrWarping_Rejected_AndDoesNotStopThem()
        {
            StrategicFleet engaged = Fleet(1, Faction.同盟);
            engaged.engaged = true;
            Assert.AreEqual(CorpsAssignmentRejection.交戦中,
                CorpsAssignmentRules.CanAssign(engaged, Faction.同盟, 3, Faction.同盟));
            Assert.IsTrue(engaged.engaged, "編成を断っただけで戦闘を止めてはいけない");

            StrategicFleet warping = Fleet(2, Faction.同盟);
            warping.warpingAsReinforcement = true;
            Assert.AreEqual(CorpsAssignmentRejection.増援航行中,
                CorpsAssignmentRules.CanAssign(warping, Faction.同盟, 3, Faction.同盟));
            Assert.IsTrue(warping.warpingAsReinforcement, "編成を断っただけで航行を止めてはいけない");
        }

        [Test]
        public void CanAssign_NullFleet_IsSafe()
        {
            Assert.AreEqual(CorpsAssignmentRejection.艦隊が無い,
                CorpsAssignmentRules.CanAssign(null, Faction.同盟, 1, Faction.同盟));
            Assert.AreEqual(CorpsAssignmentRejection.軍団が無い,
                CorpsAssignmentRules.CanAssign(Fleet(1, Faction.同盟), Faction.同盟, -1, Faction.同盟));
        }

        // ── 実行 ──

        /// <summary>所属は艦隊が1つだけ持つフィールドなので、配属すると前の軍団からは自動的に外れる。</summary>
        [Test]
        public void Assign_MovingBetweenCorps_LeavesNoDoubleMembership()
        {
            StrategicFleet f = Fleet(1, Faction.同盟, 1, "第1軍団");
            Assert.IsTrue(CorpsAssignmentRules.Assign(f, Faction.同盟, Corps(2, "第2軍団", Faction.同盟), out _));

            Assert.AreEqual(2, f.corpsId);
            Assert.AreEqual("第2軍団", f.corpsName);

            var all = new List<StrategicFleet> { f };
            Assert.AreEqual(0, CorpsAssignmentRules.FleetsIn(all, Faction.同盟, 1).Count, "前の軍団に残っている");
            Assert.AreEqual(1, CorpsAssignmentRules.FleetsIn(all, Faction.同盟, 2).Count);
        }

        [Test]
        public void Assign_MovingCorpsFlagship_DropsTheFlag()
        {
            StrategicFleet f = Fleet(1, Faction.同盟, 1, "第1軍団", flagship: true);
            CorpsAssignmentRules.Assign(f, Faction.同盟, Corps(2, "第2軍団", Faction.同盟), out _);
            Assert.IsFalse(f.isCorpsFlagship, "別の軍団の指揮を持ち込んではいけない");
        }

        [Test]
        public void Assign_DoesNotTouchShipsOrLocation()
        {
            StrategicFleet f = Fleet(7, Faction.同盟, systemId: 4);
            f.SetShips(9600);
            f.commanderPersonId = 12;

            CorpsAssignmentRules.Assign(f, Faction.同盟, Corps(1, "第1軍団", Faction.同盟), out _);

            Assert.AreEqual(9600, f.Ships, "配属で艦艇数が変わってはいけない");
            Assert.AreEqual(4, f.currentSystemId, "配属で所在地が変わってはいけない");
            Assert.AreEqual(12, f.commanderPersonId, "配属で司令官が変わってはいけない");
            Assert.AreEqual(100, f.strength);
        }

        [Test]
        public void Unassign_MakesItIndependent()
        {
            StrategicFleet f = Fleet(1, Faction.同盟, 1, "第1軍団");
            Assert.IsTrue(CorpsAssignmentRules.Unassign(f, Faction.同盟, out _));
            Assert.IsFalse(f.HasCorps);
            Assert.AreEqual(-1, f.corpsId);
        }

        [Test]
        public void Unassign_NotInCorps_Rejected()
        {
            StrategicFleet f = Fleet(1, Faction.同盟);
            Assert.IsFalse(CorpsAssignmentRules.Unassign(f, Faction.同盟, out CorpsAssignmentRejection r));
            Assert.AreEqual(CorpsAssignmentRejection.軍団に属していない, r);
        }

        /// <summary>軍団旗艦を黙って抜くと軍団が指揮官不在になるので、先に指揮を移させる。</summary>
        [Test]
        public void Unassign_CorpsFlagship_Rejected_WithReason()
        {
            StrategicFleet f = Fleet(1, Faction.同盟, 1, "第1軍団", flagship: true);
            Assert.IsFalse(CorpsAssignmentRules.Unassign(f, Faction.同盟, out CorpsAssignmentRejection r));
            Assert.AreEqual(CorpsAssignmentRejection.軍団旗艦は解除できない, r);
            Assert.AreEqual(1, f.corpsId, "拒否したのに外れている");
        }

        // ── 軍団旗艦の付け替え ──

        [Test]
        public void SetCorpsFlagship_MovesCommandAndLeavesExactlyOne()
        {
            StrategicFleet a = Fleet(1, Faction.同盟, 1, "第1軍団", flagship: true);
            StrategicFleet b = Fleet(2, Faction.同盟, 1, "第1軍団");
            StrategicFleet other = Fleet(3, Faction.同盟, 2, "第2軍団", flagship: true);
            var all = new List<StrategicFleet> { a, b, other };

            Assert.IsTrue(CorpsAssignmentRules.SetCorpsFlagship(all, b, Faction.同盟, out _));
            Assert.IsTrue(b.isCorpsFlagship);
            Assert.IsFalse(a.isCorpsFlagship, "1軍団に指揮艦隊は1つ");
            Assert.IsTrue(other.isCorpsFlagship, "別の軍団の指揮まで降ろしてはいけない");
        }

        [Test]
        public void SetCorpsFlagship_WithoutCorps_Rejected()
        {
            StrategicFleet f = Fleet(1, Faction.同盟);
            Assert.IsFalse(CorpsAssignmentRules.SetCorpsFlagship(new List<StrategicFleet> { f }, f, Faction.同盟,
                                                                 out CorpsAssignmentRejection r));
            Assert.AreEqual(CorpsAssignmentRejection.軍団に属していない, r);
        }

        // ── 照会 ──

        [Test]
        public void CorpsOf_CountsFromBoard_Deterministically()
        {
            var all = new List<StrategicFleet>
            {
                Fleet(3, Faction.同盟, 2, "第2軍団"),
                Fleet(1, Faction.同盟, 1, "第1軍団", flagship: true),
                Fleet(2, Faction.同盟, 1, "第1軍団"),
                Fleet(9, Faction.帝国, 1, "帝国第1軍団"),   // 別勢力は数えない
                Fleet(4, Faction.同盟),                      // 独立艦隊
            };

            List<CorpsAssignmentRules.CorpsInfo> corps = CorpsAssignmentRules.CorpsOf(all, Faction.同盟);
            Assert.AreEqual(2, corps.Count);
            Assert.AreEqual(1, corps[0].corpsId, "軍団IDの昇順（決定論）");
            Assert.AreEqual(2, corps[0].fleetCount);
            Assert.AreEqual(1, corps[0].flagshipFleetId);
            Assert.AreEqual("第1軍団", corps[0].corpsName);
            Assert.AreEqual(2, corps[1].corpsId);
            Assert.AreEqual(-1, corps[1].flagshipFleetId, "指揮艦隊が居なければ -1");
        }

        [Test]
        public void Unassigned_ListsIndependentFleetsOnly()
        {
            var all = new List<StrategicFleet>
            {
                Fleet(1, Faction.同盟, 1, "第1軍団"),
                Fleet(5, Faction.同盟),
                Fleet(2, Faction.同盟),
                Fleet(7, Faction.帝国),
            };
            List<StrategicFleet> free = CorpsAssignmentRules.Unassigned(all, Faction.同盟);
            Assert.AreEqual(2, free.Count);
            Assert.AreEqual(2, free[0].id, "艦隊ID順");
            Assert.AreEqual(5, free[1].id);
        }

        /// <summary>軍団の一覧と独立艦隊を足すと、自軍の全艦隊になる（一覧から漏れない）。</summary>
        [Test]
        public void CorpsAndUnassigned_TogetherCoverEveryOwnFleet()
        {
            var all = new List<StrategicFleet>
            {
                Fleet(1, Faction.同盟, 1, "第1軍団"),
                Fleet(2, Faction.同盟, 1, "第1軍団"),
                Fleet(3, Faction.同盟, 2, "第2軍団"),
                Fleet(4, Faction.同盟),
                Fleet(5, Faction.帝国, 1, "帝国第1軍団"),
            };

            int listed = CorpsAssignmentRules.Unassigned(all, Faction.同盟).Count;
            List<CorpsAssignmentRules.CorpsInfo> corps = CorpsAssignmentRules.CorpsOf(all, Faction.同盟);
            for (int i = 0; i < corps.Count; i++)
                listed += CorpsAssignmentRules.FleetsIn(all, Faction.同盟, corps[i].corpsId).Count;

            Assert.AreEqual(4, listed, "自軍艦隊が過不足なく一覧に出ること");
        }

        [Test]
        public void NextCorpsId_IsMaxPlusOne()
        {
            var all = new List<StrategicFleet>
            {
                Fleet(1, Faction.同盟, 1, "第1軍団"),
                Fleet(2, Faction.帝国, 4, "帝国第2軍団"),
                Fleet(3, Faction.同盟),
            };
            Assert.AreEqual(5, CorpsAssignmentRules.NextCorpsId(all));
            Assert.AreEqual(0, CorpsAssignmentRules.NextCorpsId(null));
        }

        [Test]
        public void Queries_NullList_AreSafe()
        {
            Assert.AreEqual(0, CorpsAssignmentRules.CorpsOf(null, Faction.同盟).Count);
            Assert.AreEqual(0, CorpsAssignmentRules.FleetsIn(null, Faction.同盟, 1).Count);
            Assert.AreEqual(0, CorpsAssignmentRules.Unassigned(null, Faction.同盟).Count);
        }

        // ── 理由文字列 ──

        [Test]
        public void RejectionText_CoversEveryReason()
        {
            Assert.AreEqual("", CorpsAssignmentRules.RejectionText(CorpsAssignmentRejection.なし));
            var all = (CorpsAssignmentRejection[])System.Enum.GetValues(typeof(CorpsAssignmentRejection));
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == CorpsAssignmentRejection.なし) continue;
                string text = CorpsAssignmentRules.RejectionText(all[i]);
                Assert.IsFalse(string.IsNullOrEmpty(text), all[i].ToString());
                Assert.AreNotEqual("編成を変えられません", text, all[i].ToString());
            }
            Assert.AreEqual("編成を変えられません",
                CorpsAssignmentRules.RejectionText((CorpsAssignmentRejection)999));
        }
    }
}
