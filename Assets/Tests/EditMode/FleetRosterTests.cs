using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊編制台帳（#146）の純ロジックを固定する：
    /// 番号払い出し（勢力ごとに独立・解隊は再利用・永久欠番は不可）／提督配属（階級ゲート）／
    /// 解隊・永久欠番／表示名。
    /// </summary>
    public class FleetRosterTests
    {
        private static AdmiralData Admiral(int tier)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = "テスト提督"; a.rankTier = tier;
            return a;
        }

        [SetUp]
        public void Reset() => FleetRoster.Clear();

        [Test]
        public void NextAvailableNumber_StartsAtOne_ThenIncrements()
        {
            Assert.AreEqual(1, FleetRoster.NextAvailableNumber(Faction.帝国));
            FleetRoster.CreateFleet(Faction.帝国);            // 1
            FleetRoster.CreateFleet(Faction.帝国);            // 2
            FleetRoster.CreateFleet(Faction.帝国);            // 3
            Assert.AreEqual(4, FleetRoster.NextAvailableNumber(Faction.帝国));
        }

        [Test]
        public void NumberSpace_IsPerFaction()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1);
            // 帝国の第1艦隊があっても、同盟の番号空間は独立
            Assert.AreEqual(1, FleetRoster.NextAvailableNumber(Faction.同盟));
            Assert.IsNotNull(FleetRoster.CreateFleet(Faction.同盟, 1));
            Assert.AreNotSame(FleetRoster.GetFleet(Faction.帝国, 1), FleetRoster.GetFleet(Faction.同盟, 1));
        }

        [Test]
        public void CreateFleet_SameActiveNumber_ReturnsExisting()
        {
            var u1 = FleetRoster.CreateFleet(Faction.帝国, 13);
            var u2 = FleetRoster.CreateFleet(Faction.帝国, 13);
            Assert.AreSame(u1, u2); // 現役の同番号は重複生成しない
        }

        [Test]
        public void RetireNumber_BlocksReuse()
        {
            FleetRoster.CreateFleet(Faction.同盟, 13);
            FleetRoster.RetireNumber(Faction.同盟, 13);
            Assert.IsTrue(FleetRoster.IsRetired(Faction.同盟, 13));
            Assert.AreEqual(FleetStatus.永久欠番, FleetRoster.GetFleet(Faction.同盟, 13).status);
            Assert.IsNull(FleetRoster.CreateFleet(Faction.同盟, 13)); // 永久欠番は払い出せない
        }

        [Test]
        public void Disband_AllowsReuse()
        {
            FleetRoster.CreateFleet(Faction.帝国); // 1
            FleetRoster.CreateFleet(Faction.帝国); // 2
            FleetRoster.CreateFleet(Faction.帝国); // 3
            Assert.AreEqual(4, FleetRoster.NextAvailableNumber(Faction.帝国));

            var f2 = FleetRoster.GetFleet(Faction.帝国, 2);
            Assert.IsTrue(FleetRoster.Disband(Faction.帝国, 2));
            Assert.AreEqual(FleetStatus.解隊, f2.status);
            // 解隊した番号は払い出しの最小候補に戻る（永久欠番と違い再利用可）
            Assert.AreEqual(2, FleetRoster.NextAvailableNumber(Faction.帝国));

            var reused = FleetRoster.CreateFleet(Faction.帝国, 2);
            Assert.AreEqual(FleetStatus.現役, reused.status);
            Assert.AreNotSame(f2, reused); // 新ユニットで再利用
        }

        [Test]
        public void AssignAdmiral_RankGate()
        {
            var unit = FleetRoster.CreateFleet(Faction.帝国, 1);

            // 階級ゲート無し（requiredTier=0）：非null提督なら配属可
            Assert.IsTrue(FleetRoster.AssignAdmiral(unit, Admiral(5)));
            Assert.IsTrue(unit.HasAdmiral);

            // ゲートあり：tier 不足は配属拒否（現状維持）
            var low = Admiral(5);
            Assert.IsFalse(FleetRoster.AssignAdmiral(unit, low, requiredTier: 7));

            // tier 充足は配属可
            var high = Admiral(7);
            Assert.IsTrue(FleetRoster.AssignAdmiral(unit, high, requiredTier: 7));
            Assert.AreSame(high, unit.assignedAdmiral);

            // null 提督は不可
            Assert.IsFalse(FleetRoster.AssignAdmiral(unit, null));
        }

        /// <summary>
        /// 指揮可能規模ゲート（RANKCMD-3 #1713）：過大兵力の艦隊は階級ゲートを満たしても下位階級では配属不可。
        /// baseStrength=0（兵力は提督側＝RANKCMD-1 未完）の艦隊は規模0扱い＝従来どおり階級ゲートのみ（後方互換）。
        /// </summary>
        [Test]
        public void AssignAdmiral_CommandCapacityGate()
        {
            var unit = FleetRoster.CreateFleet(Faction.帝国, 1);
            unit.baseStrength = 20000; // 大将の指揮限界(15000)超＝上級大将/元帥級が要る

            // 階級ゲート無し(0)でも、指揮可能規模を超える階級は配属不可
            Assert.IsFalse(FleetRoster.CanAssign(Admiral(8), unit));   // 大将 cap15000 < 20000
            Assert.IsFalse(FleetRoster.AssignAdmiral(unit, Admiral(8)));
            Assert.IsFalse(unit.HasAdmiral);

            // 規模を満たす階級は配属可
            Assert.IsTrue(FleetRoster.CanAssign(Admiral(9), unit));    // 上級大将 cap30000 >= 20000
            Assert.IsTrue(FleetRoster.AssignAdmiral(unit, Admiral(9)));
            Assert.AreEqual(9, unit.assignedAdmiral.rankTier);

            // 規模を満たしても階級ゲート(requiredTier)が優先（両方必要）
            Assert.IsFalse(FleetRoster.CanAssign(Admiral(9), unit, requiredTier: 10));

            // baseStrength=0 の艦隊は規模0扱い＝准将でも通る（後方互換）
            var small = FleetRoster.CreateFleet(Faction.帝国, 2);
            Assert.IsTrue(FleetRoster.CanAssign(Admiral(5), small));
            Assert.IsTrue(FleetRoster.AssignAdmiral(small, Admiral(5)));
        }

        [Test]
        public void Unassign_And_Reassign()
        {
            var unit = FleetRoster.CreateFleet(Faction.帝国, 2);
            var a = Admiral(8);
            FleetRoster.AssignAdmiral(unit, a);
            FleetRoster.Unassign(unit);
            Assert.IsFalse(unit.HasAdmiral);

            var b = Admiral(8);
            Assert.IsTrue(FleetRoster.ReassignAdmiral(unit, b));
            Assert.AreSame(b, unit.assignedAdmiral);
        }

        [Test]
        public void DisplayName_FallsBackToNumber()
        {
            var plain = FleetRoster.CreateFleet(Faction.同盟, 13);
            Assert.AreEqual("第13艦隊", plain.DisplayName);

            var named = FleetRoster.CreateFleet(Faction.帝国, 1, "黒色槍騎兵艦隊");
            Assert.AreEqual("黒色槍騎兵艦隊", named.DisplayName);
        }

        [Test]
        public void RetiredNumber_IsSkippedByNextAvailable()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1);
            FleetRoster.RetireNumber(Faction.帝国, 2); // 2 を欠番（ユニット未作成でも欠番化できる）
            // 1=現役, 2=永久欠番 → 次は 3
            Assert.AreEqual(3, FleetRoster.NextAvailableNumber(Faction.帝国));
        }

        // ===== ここから敵対的エッジケース（追記） =====

        /// <summary>
        /// 番号空間に「穴」（未使用の小さい番号）があれば最小の穴を払い出すべき（連番でなく最小空きを埋める仕様）。
        /// 1,3,5 を現役にすると次は 2。さらに 2 を埋めたら次は 4。
        /// </summary>
        [Test]
        public void NextAvailableNumber_FillsLowestGap()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1);
            FleetRoster.CreateFleet(Faction.帝国, 3);
            FleetRoster.CreateFleet(Faction.帝国, 5);
            // 1,3,5 現役 → 最小空き = 2
            Assert.AreEqual(2, FleetRoster.NextAvailableNumber(Faction.帝国));
            FleetRoster.CreateFleet(Faction.帝国, 2);
            // 1,2,3,5 現役 → 次の最小空き = 4
            Assert.AreEqual(4, FleetRoster.NextAvailableNumber(Faction.帝国));
        }

        /// <summary>
        /// CanAssign の階級ゲートは「以上（>=）」。境界（tier == requiredTier）はちょうど通る（オフバイワン検出）。
        /// 1 不足はダメ・ちょうどは可。
        /// </summary>
        [Test]
        public void CanAssign_RankGate_BoundaryIsInclusive()
        {
            // tier == requiredTier はちょうど可（>= の境界）
            Assert.IsTrue(FleetRoster.CanAssign(Admiral(7), 7));
            // 1 だけ不足は不可
            Assert.IsFalse(FleetRoster.CanAssign(Admiral(6), 7));
            // 1 上回るのは当然可
            Assert.IsTrue(FleetRoster.CanAssign(Admiral(8), 7));
            // null 提督は常に不可
            Assert.IsFalse(FleetRoster.CanAssign(null, 0));
        }

        /// <summary>
        /// requiredTier=0（ゲート無し）なら、tier=0 の提督でも配属できる（0>=0）。
        /// 負の requiredTier でも 0>= 負 で通る（実質ゲート無効）。
        /// </summary>
        [Test]
        public void CanAssign_ZeroAndNegativeGate()
        {
            Assert.IsTrue(FleetRoster.CanAssign(Admiral(0), 0));   // 0 >= 0
            Assert.IsTrue(FleetRoster.CanAssign(Admiral(0), -5));  // 0 >= -5
            // 負 tier の提督 vs ゲート無し(0)：-1 >= 0 は不成立
            Assert.IsFalse(FleetRoster.CanAssign(Admiral(-1), 0));
        }

        /// <summary>
        /// CreateFleet に number<=0 を渡すと NextAvailableNumber が使われる（番号自動払い出し）。
        /// 負数でも 0 と同じく自動採番されるべき。
        /// </summary>
        [Test]
        public void CreateFleet_NonPositiveNumber_AutoAssigns()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1); // 1 を占有
            var auto0 = FleetRoster.CreateFleet(Faction.帝国, 0);   // → 2
            Assert.AreEqual(2, auto0.fleetNumber);
            var autoNeg = FleetRoster.CreateFleet(Faction.帝国, -99); // → 3
            Assert.AreEqual(3, autoNeg.fleetNumber);
        }

        /// <summary>
        /// CreateFleet を解隊済み番号に対して呼ぶと、既存(解隊)ユニットは現役でないので新インスタンスで上書きされる。
        /// GetFleet は新しい現役ユニットを返し、旧ユニットとは別物（再利用＝差し替え）。
        /// </summary>
        [Test]
        public void CreateFleet_OnDisbandedNumber_ReplacesWithNewInstance()
        {
            var orig = FleetRoster.CreateFleet(Faction.同盟, 4);
            FleetRoster.Disband(Faction.同盟, 4);
            Assert.AreEqual(FleetStatus.解隊, orig.status);

            var fresh = FleetRoster.CreateFleet(Faction.同盟, 4);
            Assert.AreNotSame(orig, fresh);
            Assert.AreEqual(FleetStatus.現役, fresh.status);
            Assert.AreSame(fresh, FleetRoster.GetFleet(Faction.同盟, 4)); // 台帳は新ユニットを指す
            Assert.AreEqual(FleetStatus.解隊, orig.status); // 旧ユニットは解隊のまま孤児化
        }

        /// <summary>
        /// Register(null) は null を返し、何も登録しない（NRE を投げない）。
        /// </summary>
        [Test]
        public void Register_Null_ReturnsNull_NoThrow()
        {
            Assert.IsNull(FleetRoster.Register(null));
            // 台帳は空のまま：どの番号も未登録
            Assert.IsNull(FleetRoster.GetFleet(Faction.帝国, 1));
        }

        /// <summary>
        /// 永久欠番状態のユニットを Register すると、欠番集合にも反映され IsRetired=true になる
        /// （以後その番号は払い出されない）。
        /// </summary>
        [Test]
        public void Register_RetiredUnit_MarksNumberRetired()
        {
            var u = ScriptableObject.CreateInstance<FleetUnitData>();
            u.faction = Faction.帝国; u.fleetNumber = 9; u.status = FleetStatus.永久欠番;
            FleetRoster.Register(u);

            Assert.IsTrue(FleetRoster.IsRetired(Faction.帝国, 9));
            Assert.IsNull(FleetRoster.CreateFleet(Faction.帝国, 9)); // 払い出し拒否
            // 1..8 が空き → NextAvailable は 9 を飛ばす必要はないが、9 を占有しようとしても不可
            Assert.AreEqual(1, FleetRoster.NextAvailableNumber(Faction.帝国));
        }

        /// <summary>
        /// Register は番号で上書きする。faction は unit 自身の faction が出所。
        /// 同番号を別インスタンスで Register すると後者で置き換わる。
        /// </summary>
        [Test]
        public void Register_OverwritesByNumber()
        {
            var a = ScriptableObject.CreateInstance<FleetUnitData>();
            a.faction = Faction.同盟; a.fleetNumber = 2; a.status = FleetStatus.現役;
            var b = ScriptableObject.CreateInstance<FleetUnitData>();
            b.faction = Faction.同盟; b.fleetNumber = 2; b.status = FleetStatus.現役;

            FleetRoster.Register(a);
            FleetRoster.Register(b);
            Assert.AreSame(b, FleetRoster.GetFleet(Faction.同盟, 2)); // 後勝ち
            Assert.AreNotSame(a, FleetRoster.GetFleet(Faction.同盟, 2));
        }

        /// <summary>
        /// Disband は存在しない番号には false（空集合・未作成への異常入力）。
        /// 一方 RetireNumber は存在しなくても true を返し、その番号を欠番化する（仕様の非対称性）。
        /// </summary>
        [Test]
        public void DisbandVsRetire_OnMissingNumber_Asymmetry()
        {
            Assert.IsFalse(FleetRoster.Disband(Faction.帝国, 42)); // 未作成 → false
            Assert.IsTrue(FleetRoster.RetireNumber(Faction.帝国, 42)); // 未作成でも true で欠番化
            Assert.IsTrue(FleetRoster.IsRetired(Faction.帝国, 42));
        }

        /// <summary>
        /// AllFleets は解隊・永久欠番ユニットも含めて返す（現役だけに絞らない）。
        /// 作成3・うち1を解隊しても件数は3（台帳の全エントリ）。
        /// </summary>
        [Test]
        public void AllFleets_IncludesNonActiveUnits()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1);
            FleetRoster.CreateFleet(Faction.帝国, 2);
            FleetRoster.CreateFleet(Faction.帝国, 3);
            FleetRoster.Disband(Faction.帝国, 2);
            Assert.AreEqual(3, FleetRoster.AllFleets(Faction.帝国).Count);
            // 別勢力は空（混入しない＝勢力独立の不変条件）
            Assert.AreEqual(0, FleetRoster.AllFleets(Faction.同盟).Count);
        }

        /// <summary>
        /// 空文字の fleetName は DisplayName で「第N艦隊」へフォールバックするが、
        /// 空白のみ（" "）は IsNullOrEmpty が false ＝そのまま空白名が返る（バグ候補：空白名が表示され得る）。
        /// </summary>
        [Test]
        public void DisplayName_WhitespaceName_IsNotTreatedAsEmpty()
        {
            var u = FleetRoster.CreateFleet(Faction.帝国, 7, "   ");
            // 仕様上 IsNullOrEmpty は空白を空とみなさない → " " がそのまま返る
            Assert.AreEqual("   ", u.DisplayName);
        }

        /// <summary>
        /// 解隊→欠番化の順で同じ番号を処理しても、最終的に IsRetired=true かつ payout 不可。
        /// 状態遷移の単調性（一度欠番にしたら現役へは自動で戻らない）。
        /// </summary>
        [Test]
        public void Disband_ThenRetire_StaysRetired()
        {
            // 番号 1〜4 を占有してから 5 を作って解隊（5 を最小空きにするための地ならし）
            FleetRoster.CreateFleet(Faction.同盟, 1);
            FleetRoster.CreateFleet(Faction.同盟, 2);
            FleetRoster.CreateFleet(Faction.同盟, 3);
            FleetRoster.CreateFleet(Faction.同盟, 4);
            FleetRoster.CreateFleet(Faction.同盟, 5);
            Assert.IsTrue(FleetRoster.Disband(Faction.同盟, 5));
            // 1〜4 現役・5 解隊 → 解隊は再利用可なので最小空きは 5
            Assert.AreEqual(5, FleetRoster.NextAvailableNumber(Faction.同盟));

            // 欠番化すると 5 は払い出し不可へ（単調：現役へ自動復帰しない）
            FleetRoster.RetireNumber(Faction.同盟, 5);
            Assert.IsTrue(FleetRoster.IsRetired(Faction.同盟, 5));
            Assert.AreEqual(FleetStatus.永久欠番, FleetRoster.GetFleet(Faction.同盟, 5).status);
            Assert.IsNull(FleetRoster.CreateFleet(Faction.同盟, 5));
            // 5 が欠番なので次の最小空きは 6
            Assert.AreEqual(6, FleetRoster.NextAvailableNumber(Faction.同盟));
        }
    }
}
