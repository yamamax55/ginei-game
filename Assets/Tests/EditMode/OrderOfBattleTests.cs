using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 編制ツリー（#147 軍団システム）の純ロジックを固定する：
    /// 司令部固定・中身流動（艦隊/下位梯団の attach/detach・単一所属）／梯団別の司令配属（階級ゲート #14）／
    /// 配下集計（ツリー再帰）／循環防止／勢力独立。
    /// </summary>
    public class OrderOfBattleTests
    {
        private static AdmiralData Admiral(int tier)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = "提督"; a.rankTier = tier;
            return a;
        }

        [SetUp]
        public void Reset() => OrderOfBattle.Clear();

        [Test]
        public void Create_AssignsUniqueIds()
        {
            var a = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var b = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            Assert.AreNotEqual(a.id, b.id);
            Assert.AreSame(a, OrderOfBattle.Get(a.id));
        }

        [Test]
        public void AttachDetachFleet()
        {
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.同盟);
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 13));
            Assert.Contains(13, corps.fleetNumbers);
            Assert.IsTrue(OrderOfBattle.DetachFleet(corps.id, 13));
            Assert.IsFalse(corps.fleetNumbers.Contains(13));
        }

        [Test]
        public void Fleet_SingleMembership_MovesBetweenCorps()
        {
            var a = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var b = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.AttachFleet(a.id, 5);
            OrderOfBattle.AttachFleet(b.id, 5); // 中身流動：a から b へ移る（単一所属）
            Assert.IsFalse(a.fleetNumbers.Contains(5));
            Assert.Contains(5, b.fleetNumbers);
        }

        [Test]
        public void Tree_AggregatesFleetsAcrossEchelons()
        {
            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var corps1 = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var corps2 = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFormation(group.id, corps1.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(group.id, corps2.id));
            OrderOfBattle.AttachFleet(corps1.id, 1);
            OrderOfBattle.AttachFleet(corps1.id, 2);
            OrderOfBattle.AttachFleet(corps2.id, 3);

            var all = OrderOfBattle.AllFleetNumbersUnder(group.id);
            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.Contains(1) && all.Contains(2) && all.Contains(3));
            Assert.AreEqual(3, OrderOfBattle.CountFleetsUnder(group.id));
            Assert.AreEqual(group.id, corps1.parentId);
        }

        [Test]
        public void AttachFormation_PreventsCycle()
        {
            var g = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var c = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFormation(g.id, c.id));
            // c の下に g を付けると循環 → 拒否
            Assert.IsFalse(OrderOfBattle.AttachFormation(c.id, g.id));
        }

        [Test]
        public void Commander_RankGate_PerEchelon()
        {
            Assert.AreEqual(7, OrderOfBattle.RequiredTier(EchelonType.艦隊));   // 中将
            Assert.AreEqual(8, OrderOfBattle.RequiredTier(EchelonType.軍団));   // 大将
            Assert.AreEqual(10, OrderOfBattle.RequiredTier(EchelonType.軍集団)); // 元帥

            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(corps.id, Admiral(7))); // 中将では軍団を持てない
            Assert.IsTrue(OrderOfBattle.AssignCommander(corps.id, Admiral(8)));  // 大将ならOK
            Assert.IsTrue(corps.HasCommander);

            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(group.id, Admiral(8)));  // 大将では軍集団を持てない
            Assert.IsTrue(OrderOfBattle.AssignCommander(group.id, Admiral(10)));  // 元帥ならOK

            Assert.IsFalse(OrderOfBattle.AssignCommander(corps.id, null));
        }

        [Test]
        public void DetachFormation_ClearsParent()
        {
            var g = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var c = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.AttachFormation(g.id, c.id);
            Assert.IsTrue(OrderOfBattle.DetachFormation(g.id, c.id));
            Assert.AreEqual(0, c.parentId);
            Assert.IsFalse(g.childFormationIds.Contains(c.id));
        }

        [Test]
        public void DisplayName_FallsBackToEchelonAndId()
        {
            var c = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.AreEqual($"軍団#{c.id}", c.DisplayName);
            var named = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国, "ローエングラム軍集団");
            Assert.AreEqual("ローエングラム軍集団", named.DisplayName);
        }

        [Test]
        public void Fleet_NumberSpace_IsPerFaction_AtFormationLevel()
        {
            var imp = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var all = OrderOfBattle.Create(EchelonType.軍団, Faction.同盟);
            OrderOfBattle.AttachFleet(imp.id, 1);
            OrderOfBattle.AttachFleet(all.id, 1); // 同盟の第1艦隊は帝国の第1艦隊と別物（単一所属は同勢力のみ）
            Assert.Contains(1, imp.fleetNumbers);
            Assert.Contains(1, all.fleetNumbers);
        }

        // ===== 敵対的エッジケース（追記） =====

        /// <summary>Get/Attach/Detach は未知の formationId に対して null/false を返し、例外を出さない。</summary>
        [Test]
        public void UnknownFormationId_IsRejectedSafely()
        {
            Assert.IsNull(OrderOfBattle.Get(99999));
            Assert.IsFalse(OrderOfBattle.AttachFleet(99999, 1));
            Assert.IsFalse(OrderOfBattle.DetachFleet(99999, 1));
            Assert.IsFalse(OrderOfBattle.AttachFormation(99999, 1));
            Assert.IsFalse(OrderOfBattle.DetachFormation(99999, 1));
            Assert.IsFalse(OrderOfBattle.CanAttachFleet(99999, 1));
            Assert.IsFalse(OrderOfBattle.AssignCommander(99999, Admiral(10)));
            // 未知 id への UnassignCommander は no-op（例外を出さない）
            Assert.DoesNotThrow(() => OrderOfBattle.UnassignCommander(99999));
            // 未知 id の集計は空・0
            Assert.AreEqual(0, OrderOfBattle.AllFleetNumbersUnder(99999).Count);
            Assert.AreEqual(0, OrderOfBattle.CountFleetsUnder(99999));
        }

        /// <summary>艦隊番号は正のみ有効：0 と負はクランプの下端で拒否される（仕様：fleetNumber &gt; 0）。</summary>
        [Test]
        public void FleetNumber_ZeroOrNegative_IsRejected()
        {
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AttachFleet(corps.id, 0));   // 境界：0 は無効
            Assert.IsFalse(OrderOfBattle.AttachFleet(corps.id, -1));  // 負は無効
            Assert.IsFalse(OrderOfBattle.CanAttachFleet(corps.id, 0));
            Assert.IsFalse(OrderOfBattle.CanAttachFleet(corps.id, -5));
            Assert.AreEqual(0, corps.fleetNumbers.Count);
            // 境界の反対端：1 は最小の有効番号
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 1));
            Assert.AreEqual(1, corps.fleetNumbers.Count);
        }

        /// <summary>同一艦隊の二重編入は重複を作らない（冪等＝集合的所属）。</summary>
        [Test]
        public void AttachFleet_Twice_IsIdempotent_NoDuplicate()
        {
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 7));
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 7)); // もう一度同じ番号
            Assert.AreEqual(1, corps.fleetNumbers.Count(n => n == 7)); // 重複しない
            Assert.AreEqual(1, corps.fleetNumbers.Count);
        }

        /// <summary>非メンバーの DetachFleet は false（消す対象が無い）。空梯団・他番号でも false。</summary>
        [Test]
        public void DetachFleet_NonMember_ReturnsFalse()
        {
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.DetachFleet(corps.id, 5)); // 空梯団
            OrderOfBattle.AttachFleet(corps.id, 5);
            Assert.IsFalse(OrderOfBattle.DetachFleet(corps.id, 6)); // 居ない番号
            Assert.Contains(5, corps.fleetNumbers);                  // 5 は残る
        }

        /// <summary>梯団を自分自身へ編入はできない（parentId == childId 拒否）。</summary>
        [Test]
        public void AttachFormation_SelfAttach_IsRejected()
        {
            var g = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AttachFormation(g.id, g.id));
            Assert.IsFalse(g.childFormationIds.Contains(g.id));
            Assert.AreEqual(0, g.parentId);
        }

        /// <summary>勢力をまたいだ梯団編入は拒否（faction が違えば付けられない）。</summary>
        [Test]
        public void AttachFormation_CrossFaction_IsRejected()
        {
            var impGroup = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var allCorps = OrderOfBattle.Create(EchelonType.軍団, Faction.同盟);
            Assert.IsFalse(OrderOfBattle.AttachFormation(impGroup.id, allCorps.id));
            Assert.IsFalse(impGroup.childFormationIds.Contains(allCorps.id));
            Assert.AreEqual(0, allCorps.parentId);
        }

        /// <summary>下位梯団も単一親：別の親に付け替えると旧親の子リストから消える。</summary>
        [Test]
        public void AttachFormation_SingleParent_MovesBetweenGroups()
        {
            var g1 = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var g2 = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFormation(g1.id, corps.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(g2.id, corps.id)); // g1 から g2 へ移る
            Assert.IsFalse(g1.childFormationIds.Contains(corps.id));        // 旧親から消える
            Assert.IsTrue(g2.childFormationIds.Contains(corps.id));
            Assert.AreEqual(g2.id, corps.parentId);
        }

        /// <summary>循環防止は孫レベルでも効く：祖父を孫の下に付けると循環になるため拒否（多段 WouldCycle）。</summary>
        [Test]
        public void AttachFormation_PreventsDeepCycle_Grandchild()
        {
            var grand = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var mid = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var leaf = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFormation(grand.id, mid.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(mid.id, leaf.id));
            // leaf の下に grand を付けると grand→mid→leaf→grand の循環 → 拒否
            Assert.IsFalse(OrderOfBattle.AttachFormation(leaf.id, grand.id));
            Assert.IsFalse(leaf.childFormationIds.Contains(grand.id));
        }

        /// <summary>同一艦隊番号が複数の下位梯団に（不正に）居ても、集計は重複排除して1回だけ数える（合計保存則）。</summary>
        [Test]
        public void AllFleetNumbersUnder_DeduplicatesSharedNumber()
        {
            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var c1 = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var c2 = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.AttachFormation(group.id, c1.id);
            OrderOfBattle.AttachFormation(group.id, c2.id);
            // 単一所属を破る経路（直接 list 操作）で同じ番号 9 を両方へ：集計の dedup を検証
            c1.fleetNumbers.Add(9);
            c2.fleetNumbers.Add(9);
            var all = OrderOfBattle.AllFleetNumbersUnder(group.id);
            Assert.AreEqual(1, all.Count(n => n == 9)); // 9 は1回だけ
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(1, OrderOfBattle.CountFleetsUnder(group.id));
        }

        /// <summary>GetOrCreate：名前一致は再利用、faction/echelon/名前のいずれか違えば別物を作る。null/空名は毎回新規。</summary>
        [Test]
        public void GetOrCreate_NameMatch_ReusesElseCreates()
        {
            var first = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.帝国, "第1軍団");
            var same = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.帝国, "第1軍団");
            Assert.AreSame(first, same); // 同勢力・同echelon・同名 → 再利用

            var otherFaction = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.同盟, "第1軍団");
            Assert.AreNotSame(first, otherFaction); // 勢力違いは別物

            var otherEchelon = OrderOfBattle.GetOrCreate(EchelonType.軍集団, Faction.帝国, "第1軍団");
            Assert.AreNotSame(first, otherEchelon); // echelon 違いは別物

            // 空名/null は照合せず毎回新規（仕様：IsNullOrEmpty はスキップ）
            var anon1 = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.帝国, "");
            var anon2 = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.帝国, "");
            Assert.AreNotSame(anon1, anon2);
            var anonNull1 = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.帝国, null);
            var anonNull2 = OrderOfBattle.GetOrCreate(EchelonType.軍団, Faction.帝国, null);
            Assert.AreNotSame(anonNull1, anonNull2);
        }

        /// <summary>AllFormations は勢力で厳密に絞る（他勢力は混じらない・件数一致）。</summary>
        [Test]
        public void AllFormations_FiltersByFactionExactly()
        {
            OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            OrderOfBattle.Create(EchelonType.軍団, Faction.同盟);
            Assert.AreEqual(2, OrderOfBattle.AllFormations(Faction.帝国).Count);
            Assert.AreEqual(1, OrderOfBattle.AllFormations(Faction.同盟).Count);
            Assert.IsTrue(OrderOfBattle.AllFormations(Faction.帝国).All(f => f.faction == Faction.帝国));
        }

        /// <summary>艦隊echelon の階級ゲートは tier==7 ちょうどで通る（境界の等号・オフバイワン）。tier 6 は不可。</summary>
        [Test]
        public void AssignCommander_FleetEchelon_BoundaryTier()
        {
            var fleet = OrderOfBattle.Create(EchelonType.艦隊, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(fleet.id, Admiral(6))); // 7 未満は不可
            Assert.IsFalse(fleet.HasCommander);
            Assert.IsTrue(OrderOfBattle.AssignCommander(fleet.id, Admiral(7)));  // ちょうど 7 で可（>= 判定）
            Assert.IsTrue(fleet.HasCommander);
            // 上位tierも当然可
            Assert.IsTrue(OrderOfBattle.AssignCommander(fleet.id, Admiral(10)));
        }

        /// <summary>分艦隊 echelon（RANKCMD-4 #1714）：必要階級は少将6。准将5は不可・少将6ちょうどで可（境界・銀英伝準拠の最下段）。</summary>
        [Test]
        public void AssignCommander_SubFleetEchelon_RequiresMajorGeneral()
        {
            Assert.AreEqual(6, OrderOfBattle.RequiredTier(EchelonType.分艦隊));
            var sub = OrderOfBattle.Create(EchelonType.分艦隊, Faction.同盟);
            Assert.IsFalse(OrderOfBattle.AssignCommander(sub.id, Admiral(5))); // 准将は不可（6未満）
            Assert.IsFalse(sub.HasCommander);
            Assert.IsTrue(OrderOfBattle.AssignCommander(sub.id, Admiral(6)));  // 少将ちょうどで可（>= 判定）
            Assert.IsTrue(sub.HasCommander);
            // 分艦隊は最下段＝艦隊(7)を持てる中将も当然持てる
            Assert.IsTrue(OrderOfBattle.CanCommand(Admiral(7), EchelonType.分艦隊));
        }

        /// <summary>CanCommand は null提督・各echelonの境界で正しく判定する（単調性：高tierほど多くの梯団を持てる）。</summary>
        [Test]
        public void CanCommand_NullAndBoundaries()
        {
            Assert.IsFalse(OrderOfBattle.CanCommand(null, EchelonType.艦隊));
            // 大将(8)は艦隊(7)・軍団(8)を持てるが軍集団(10)は持てない
            var general = Admiral(8);
            Assert.IsTrue(OrderOfBattle.CanCommand(general, EchelonType.艦隊));
            Assert.IsTrue(OrderOfBattle.CanCommand(general, EchelonType.軍団));
            Assert.IsFalse(OrderOfBattle.CanCommand(general, EchelonType.軍集団));
            // 軍集団境界：9 は不可、10 は可
            Assert.IsFalse(OrderOfBattle.CanCommand(Admiral(9), EchelonType.軍集団));
            Assert.IsTrue(OrderOfBattle.CanCommand(Admiral(10), EchelonType.軍集団));
        }

        /// <summary>
        /// 指揮可能規模ゲート（RANKCMD-3 #1713）：配下兵力（StrengthUnder＝baseStrength 合計）が大きい梯団は、
        /// 梯団の階級ゲートを満たしても指揮限界を超える階級では司令になれない。兵力0（RANKCMD-1 未完）は規模0扱いで従来どおり。
        /// </summary>
        [Test]
        public void AssignCommander_CommandCapacityGate()
        {
            FleetRoster.Clear();
            var fleet = OrderOfBattle.Create(EchelonType.艦隊, Faction.帝国); // 階級ゲート=中将7
            var unit = FleetRoster.CreateFleet(Faction.帝国, 1);
            unit.baseStrength = 50000;                          // 大将(15000)を大きく超える＝元帥級
            Assert.IsTrue(OrderOfBattle.AttachFleet(fleet.id, 1));
            Assert.AreEqual(50000, OrderOfBattle.StrengthUnder(fleet.id));

            // 階級ゲート(7)は満たすが指揮可能規模を超える中将/大将は不可
            Assert.IsFalse(OrderOfBattle.AssignCommander(fleet.id, Admiral(7))); // 中将 cap12000 < 50000
            Assert.IsFalse(OrderOfBattle.AssignCommander(fleet.id, Admiral(8))); // 大将 cap15000 < 50000
            Assert.IsFalse(fleet.HasCommander);
            // 元帥は規模も満たす
            Assert.IsTrue(OrderOfBattle.AssignCommander(fleet.id, Admiral(10))); // 元帥 cap60000 >= 50000

            // 兵力0の梯団は規模0扱い＝中将でも可（後方互換）
            var empty = OrderOfBattle.Create(EchelonType.艦隊, Faction.帝国);
            Assert.AreEqual(0, OrderOfBattle.StrengthUnder(empty.id));
            Assert.IsTrue(OrderOfBattle.AssignCommander(empty.id, Admiral(7)));
            FleetRoster.Clear();
        }

        /// <summary>AssignCommander が階級ゲートで失敗しても既存の司令は維持される（現状維持＝置き換えない）。</summary>
        [Test]
        public void AssignCommander_FailedGate_KeepsExistingCommander()
        {
            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var marshal = Admiral(10);
            Assert.IsTrue(OrderOfBattle.AssignCommander(group.id, marshal));
            // 大将(8)は軍集団を持てない → 失敗、元帥のまま据え置き
            Assert.IsFalse(OrderOfBattle.AssignCommander(group.id, Admiral(8)));
            Assert.AreSame(marshal, group.commander);
        }

        /// <summary>DetachFormation：親リストに居ない子は false（false 時に child.parentId を巻き込まない）。</summary>
        [Test]
        public void DetachFormation_NonChild_ReturnsFalse_DoesNotClearParent()
        {
            var g1 = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var g2 = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.AttachFormation(g1.id, corps.id); // corps の親は g1
            // g2 から corps を外そうとしても g2 の子ではない → false
            Assert.IsFalse(OrderOfBattle.DetachFormation(g2.id, corps.id));
            Assert.AreEqual(g1.id, corps.parentId); // 親は g1 のまま（巻き込み無し）
            Assert.IsTrue(g1.childFormationIds.Contains(corps.id));
        }

        // ===== ORBAT-1 #1717：EchelonType の多段化（戦隊/分艦隊/艦隊/軍団/軍/軍集団/宇宙艦隊） =====

        /// <summary>
        /// ORBAT-1：全梯団の必要階級 tier が現実準拠の段で定まる（戦隊4/分艦隊6/艦隊7/軍団8/軍9/軍集団10/宇宙艦隊10）。
        /// 段が上がるほど必要 tier は単調非減少（高い段ほど高い階級が要る）。
        /// </summary>
        [Test]
        public void RequiredTier_MultiTierEchelons_ORBAT1()
        {
            Assert.AreEqual(4, OrderOfBattle.RequiredTier(EchelonType.戦隊));
            Assert.AreEqual(6, OrderOfBattle.RequiredTier(EchelonType.分艦隊));
            Assert.AreEqual(7, OrderOfBattle.RequiredTier(EchelonType.艦隊));
            Assert.AreEqual(8, OrderOfBattle.RequiredTier(EchelonType.軍団));
            Assert.AreEqual(9, OrderOfBattle.RequiredTier(EchelonType.軍));
            Assert.AreEqual(10, OrderOfBattle.RequiredTier(EchelonType.軍集団));
            Assert.AreEqual(10, OrderOfBattle.RequiredTier(EchelonType.宇宙艦隊));

            // enum の並び順＝序列（低→高）で必要 tier が単調非減少であることを固定
            var order = new[]
            {
                EchelonType.戦隊, EchelonType.分艦隊, EchelonType.艦隊,
                EchelonType.軍団, EchelonType.軍, EchelonType.軍集団, EchelonType.宇宙艦隊
            };
            for (int i = 1; i < order.Length; i++)
                Assert.LessOrEqual(OrderOfBattle.RequiredTier(order[i - 1]),
                                   OrderOfBattle.RequiredTier(order[i]),
                                   $"{order[i - 1]} → {order[i]} で必要tierが下がってはならない");
        }

        /// <summary>
        /// ORBAT-1：新段の司令配属が階級ゲートで効く。戦隊は准将(5≥4)で持てる／軍は上級大将(9)が要る（大将8は不可）／
        /// 宇宙艦隊は元帥(10)のみ（上級大将9は不可）。
        /// </summary>
        [Test]
        public void AssignCommander_NewEchelons_RankGate_ORBAT1()
        {
            var squadron = OrderOfBattle.Create(EchelonType.戦隊, Faction.同盟);
            Assert.IsTrue(OrderOfBattle.AssignCommander(squadron.id, Admiral(5)));  // 准将でも戦隊は持てる（5≥4）

            var army = OrderOfBattle.Create(EchelonType.軍, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(army.id, Admiral(8)));     // 大将は軍を持てない（8<9）
            Assert.IsTrue(OrderOfBattle.AssignCommander(army.id, Admiral(9)));      // 上級大将で可

            var grand = OrderOfBattle.Create(EchelonType.宇宙艦隊, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(grand.id, Admiral(9)));    // 上級大将は不可（9<10）
            Assert.IsTrue(OrderOfBattle.AssignCommander(grand.id, Admiral(10)));    // 元帥で可
        }

        /// <summary>
        /// ORBAT-1：多段ツリー（宇宙艦隊⊃軍集団⊃軍⊃軍団⊃艦隊⊃分艦隊⊃戦隊）を組み、葉に置いた艦隊が
        /// 全段の StrengthUnder/AllFleetNumbersUnder へ再帰で巻き上がる（段が増えても集計は流用で通る）。
        /// </summary>
        [Test]
        public void DeepEchelonTree_RollsUpLeafFleet_ORBAT1()
        {
            FleetRoster.Clear();
            var grand = OrderOfBattle.Create(EchelonType.宇宙艦隊, Faction.帝国);
            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var army = OrderOfBattle.Create(EchelonType.軍, Faction.帝国);
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var fleet = OrderOfBattle.Create(EchelonType.艦隊, Faction.帝国);
            var sub = OrderOfBattle.Create(EchelonType.分艦隊, Faction.帝国);
            var squadron = OrderOfBattle.Create(EchelonType.戦隊, Faction.帝国);

            Assert.IsTrue(OrderOfBattle.AttachFormation(grand.id, group.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(group.id, army.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(army.id, corps.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(corps.id, fleet.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(fleet.id, sub.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(sub.id, squadron.id));

            var unit = FleetRoster.CreateFleet(Faction.帝国, 1);
            unit.baseStrength = 12000;
            Assert.IsTrue(OrderOfBattle.AttachFleet(squadron.id, 1)); // 最下段の戦隊に艦隊を置く

            // 最上段の宇宙艦隊まで再帰で巻き上がる
            Assert.AreEqual(1, OrderOfBattle.CountFleetsUnder(grand.id));
            Assert.AreEqual(12000, OrderOfBattle.StrengthUnder(grand.id));
            CollectionAssert.Contains(OrderOfBattle.AllFleetNumbersUnder(grand.id).ToList(), 1);
            FleetRoster.Clear();
        }
    }
}
