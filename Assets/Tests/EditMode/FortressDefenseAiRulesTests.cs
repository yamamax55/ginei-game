using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞戦（#40）の守備側AI＝<see cref="FortressDefenseAiRules"/>。
    /// 「要塞を活用した守備」＝支援範囲の内側で戦う／突破ルートを横一線で塞ぐ／深追いしない／
    /// 最低1隊は要塞を空けない、を既定パラメータの具体値で固定する。
    /// 地形（迂回不能）は <see cref="CorridorArenaRulesTests"/> の担当で、こちらはその中の居場所だけを見る。
    /// </summary>
    public class FortressDefenseAiRulesTests
    {
        private const float Eps = 1e-3f;

        // 既定の回廊＝半幅18・半長60・要塞は +18 に封鎖半径20・突破線 +52。
        private static CorridorArenaRules.CorridorArenaBounds B => CorridorArenaRules.CorridorArenaBounds.Default;
        private static FortressDefenseAiParams P => FortressDefenseAiParams.Default;

        // 突破線が要塞の −x 側にある鏡像の回廊（守備側が −x）。
        private static CorridorArenaRules.CorridorArenaBounds Mirror =>
            new CorridorArenaRules.CorridorArenaBounds(18f, 60f, -18f, 20f, -52f);

        // ── 支援範囲 ──

        [Test]
        public void SupportRadius_TakesLargerOfCloseAndMainGun()
        {
            // 封鎖20＋近接14＝34 と 主砲48 → 主砲が勝つ。
            Assert.AreEqual(48f, FortressDefenseAiRules.SupportRadius(20f, 14f, 48f), Eps);
            // 主砲が無い（0）なら近接支援＝封鎖＋近接。
            Assert.AreEqual(34f, FortressDefenseAiRules.SupportRadius(20f, 14f, 0f), Eps);
            // 負値は 0 として扱う（null 安全相当）。
            Assert.AreEqual(0f, FortressDefenseAiRules.SupportRadius(-5f, -5f, -5f), Eps);
        }

        // ── 守備側の向き ──

        [Test]
        public void DefenseSideAndFacing_FollowBreakthroughSide()
        {
            Assert.AreEqual(1f, FortressDefenseAiRules.DefenseSide(B), Eps);
            Assert.AreEqual(90f, FortressDefenseAiRules.DefenseFacingDeg(B), Eps);   // 攻撃側（−x）へ正対
            Assert.AreEqual(-1f, FortressDefenseAiRules.DefenseSide(Mirror), Eps);
            Assert.AreEqual(-90f, FortressDefenseAiRules.DefenseFacingDeg(Mirror), Eps);
        }

        // ── 役割の配分（要塞を空にしない／全員突撃しない） ──

        [Test]
        public void GuardCount_AlwaysKeepsAtLeastOneOnTheFortress()
        {
            Assert.AreEqual(0, FortressDefenseAiRules.GuardCount(0, 0.34f, true));   // 隊が無ければ0
            Assert.AreEqual(1, FortressDefenseAiRules.GuardCount(1, 0.34f, true));   // 最後の1隊は直衛
            Assert.AreEqual(1, FortressDefenseAiRules.GuardCount(2, 0.34f, true));
            Assert.AreEqual(1, FortressDefenseAiRules.GuardCount(3, 0.34f, true));
            Assert.AreEqual(1, FortressDefenseAiRules.GuardCount(4, 0.34f, true));
            Assert.AreEqual(2, FortressDefenseAiRules.GuardCount(5, 0.34f, true));
            // 比率0でも最低1隊は残る＝要塞を空にしない。
            Assert.AreEqual(1, FortressDefenseAiRules.GuardCount(4, 0f, true));
        }

        [Test]
        public void GuardCount_NeverConsumesEveryFleet()
        {
            // 比率1.0でも1隊は迎撃へ回す＝全員が要塞に貼り付いて突破線が空くことは無い。
            Assert.AreEqual(3, FortressDefenseAiRules.GuardCount(4, 1f, true));
            Assert.AreEqual(1, FortressDefenseAiRules.InterceptCount(4, 1f, true));
        }

        [Test]
        public void GuardCount_ReleasedWhenFortressFallen()
        {
            Assert.AreEqual(0, FortressDefenseAiRules.GuardCount(4, 0.34f, false));
            Assert.AreEqual(4, FortressDefenseAiRules.InterceptCount(4, 0.34f, false));
        }

        [Test]
        public void RoleFor_SplitsInterceptAndGuard_Deterministically()
        {
            // 4隊＝迎撃3・直衛1。並び順だけで決まる（同じ入力なら常に同じ役割）。
            Assert.AreEqual(FortressDefenseRole.迎撃, FortressDefenseAiRules.RoleFor(0, 4, 0.34f, true));
            Assert.AreEqual(FortressDefenseRole.迎撃, FortressDefenseAiRules.RoleFor(1, 4, 0.34f, true));
            Assert.AreEqual(FortressDefenseRole.迎撃, FortressDefenseAiRules.RoleFor(2, 4, 0.34f, true));
            Assert.AreEqual(FortressDefenseRole.直衛, FortressDefenseAiRules.RoleFor(3, 4, 0.34f, true));
            // 範囲外の index はクランプ（落ちない）。
            Assert.AreEqual(FortressDefenseRole.迎撃, FortressDefenseAiRules.RoleFor(-3, 4, 0.34f, true));
            Assert.AreEqual(FortressDefenseRole.直衛, FortressDefenseAiRules.RoleFor(99, 4, 0.34f, true));
        }

        [Test]
        public void RoleFor_SingleFleet_GuardsTheFortress()
        {
            // 1隊しかいなければ要塞に張り付く（飛び出して要塞を空けない）。
            Assert.AreEqual(FortressDefenseRole.直衛, FortressDefenseAiRules.RoleFor(0, 1, 0.34f, true));
            // 要塞が落ちれば縛りは解ける。
            Assert.AreEqual(FortressDefenseRole.迎撃, FortressDefenseAiRules.RoleFor(0, 1, 0.34f, false));
        }

        // ── 持ち場（門前の迎撃線／要塞直衛） ──

        [Test]
        public void StationDistance_KeepsOutsideTheBlockadeCircle()
        {
            Assert.AreEqual(26f, FortressDefenseAiRules.StationDistance(FortressDefenseRole.迎撃, B, P), Eps); // 20+6
            Assert.AreEqual(23f, FortressDefenseAiRules.StationDistance(FortressDefenseRole.直衛, B, P), Eps); // 20+3
            // 直衛は必ず迎撃線より要塞寄り。
            Assert.Less(FortressDefenseAiRules.StationDistance(FortressDefenseRole.直衛, B, P),
                        FortressDefenseAiRules.StationDistance(FortressDefenseRole.迎撃, B, P));
        }

        [Test]
        public void StationLineX_IsInFrontOfTheFortressOnTheDefenderSide()
        {
            Assert.AreEqual(44f, FortressDefenseAiRules.StationLineX(FortressDefenseRole.迎撃, B, P), Eps);
            Assert.AreEqual(41f, FortressDefenseAiRules.StationLineX(FortressDefenseRole.直衛, B, P), Eps);
            // 鏡像の回廊では符号が反転する（守備は必ず突破線のある側）。
            Assert.AreEqual(-44f, FortressDefenseAiRules.StationLineX(FortressDefenseRole.迎撃, Mirror, P), Eps);
            Assert.AreEqual(-41f, FortressDefenseAiRules.StationLineX(FortressDefenseRole.直衛, Mirror, P), Eps);
        }

        [Test]
        public void StationLineX_NeverPassesTheBreakthroughLine_ButNeverEntersTheFortress()
        {
            // 出口が要塞の直後にある歪んだ寸法：出口の手前へ引くが、封鎖円の縁より内へは入らない。
            var tight = new CorridorArenaRules.CorridorArenaBounds(18f, 60f, 18f, 20f, 30f);
            float x = FortressDefenseAiRules.StationLineX(FortressDefenseRole.迎撃, tight, P);
            Assert.AreEqual(38f, x, Eps);                       // 封鎖円の縁（18+20）
            Assert.IsTrue(FortressDefenseAiRules.IsStationValid(new Vector2(x, 0f), tight));
        }

        [Test]
        public void LateralOffset_SpreadsAcrossTheChannel()
        {
            Assert.AreEqual(0f, FortressDefenseAiRules.LateralOffset(0, 1, 12.6f), Eps);   // 1隊は中央
            Assert.AreEqual(-12.6f, FortressDefenseAiRules.LateralOffset(0, 3, 12.6f), Eps);
            Assert.AreEqual(0f, FortressDefenseAiRules.LateralOffset(1, 3, 12.6f), Eps);
            Assert.AreEqual(12.6f, FortressDefenseAiRules.LateralOffset(2, 3, 12.6f), Eps);
        }

        [Test]
        public void Station_InterceptorsFormALineAcrossTheBreakoutRoute()
        {
            // 4隊＝迎撃3が門前(x=44)に横一線（水路半幅18×0.7＝±12.6）、直衛1は要塞直近(x=41)の中央。
            Vector2 s0 = FortressDefenseAiRules.Station(0, 4, B, P, true);
            Vector2 s1 = FortressDefenseAiRules.Station(1, 4, B, P, true);
            Vector2 s2 = FortressDefenseAiRules.Station(2, 4, B, P, true);
            Vector2 s3 = FortressDefenseAiRules.Station(3, 4, B, P, true);

            Assert.AreEqual(44f, s0.x, Eps); Assert.AreEqual(-12.6f, s0.y, Eps);
            Assert.AreEqual(44f, s1.x, Eps); Assert.AreEqual(0f, s1.y, Eps);
            Assert.AreEqual(44f, s2.x, Eps); Assert.AreEqual(12.6f, s2.y, Eps);
            Assert.AreEqual(41f, s3.x, Eps); Assert.AreEqual(0f, s3.y, Eps);
        }

        [Test]
        public void Station_AfterFortressFalls_AllFleetsFormOneScreen()
        {
            // 直衛の縛りが解け、全4隊が1本の迎撃線（x=44）に等間隔で並ぶ＝突破ルートの最終スクリーン。
            for (int i = 0; i < 4; i++)
            {
                Vector2 s = FortressDefenseAiRules.Station(i, 4, B, P, false);
                Assert.AreEqual(44f, s.x, Eps);
            }
            Assert.AreEqual(-12.6f, FortressDefenseAiRules.Station(0, 4, B, P, false).y, Eps);
            Assert.AreEqual(-4.2f, FortressDefenseAiRules.Station(1, 4, B, P, false).y, Eps);
            Assert.AreEqual(4.2f, FortressDefenseAiRules.Station(2, 4, B, P, false).y, Eps);
            Assert.AreEqual(12.6f, FortressDefenseAiRules.Station(3, 4, B, P, false).y, Eps);
        }

        [Test]
        public void Station_AlwaysRespectsTerrainAndStaysOnTheDefenderSide()
        {
            // どの隊数・どちらの回廊でも、持ち場は壁の内側・水路の内側・要塞の外・守備側。
            for (int count = 1; count <= 8; count++)
                for (int i = 0; i < count; i++)
                {
                    Vector2 s = FortressDefenseAiRules.Station(i, count, B, P, true);
                    Assert.IsTrue(FortressDefenseAiRules.IsStationValid(s, B), $"{count}/{i}");
                    Assert.IsTrue(FortressDefenseAiRules.IsOnDefenderSide(s, B), $"{count}/{i}");

                    Vector2 m = FortressDefenseAiRules.Station(i, count, Mirror, P, true);
                    Assert.IsTrue(FortressDefenseAiRules.IsStationValid(m, Mirror), $"mirror {count}/{i}");
                    Assert.IsTrue(FortressDefenseAiRules.IsOnDefenderSide(m, Mirror), $"mirror {count}/{i}");
                }
        }

        [Test]
        public void IsStationValid_RejectsInsideFortressAndOutsideWalls()
        {
            Assert.IsFalse(FortressDefenseAiRules.IsStationValid(new Vector2(18f, 0f), B));   // 要塞にめり込む
            Assert.IsFalse(FortressDefenseAiRules.IsStationValid(new Vector2(44f, 25f), B));  // 岩壁の外
            Assert.IsFalse(FortressDefenseAiRules.IsStationValid(new Vector2(80f, 0f), B));   // 水路の外
            Assert.IsTrue(FortressDefenseAiRules.IsStationValid(new Vector2(44f, 12.6f), B));
        }

        [Test]
        public void IsOnDefenderSide_RejectsCrossingToTheAttackerEntrance()
        {
            // 守備が攻撃側の入口へ回り込む位置は「守備側でない」と判定される（迂回不能の趣旨を守る）。
            Assert.IsFalse(FortressDefenseAiRules.IsOnDefenderSide(new Vector2(-40f, 0f), B));
            Assert.IsTrue(FortressDefenseAiRules.IsOnDefenderSide(new Vector2(44f, 0f), B));
        }

        // ── 局所座標（FleetAI のスロット規約との往復） ──

        [Test]
        public void StationLocal_RoundTripsThroughDefenseFacing()
        {
            // FleetAI は corpsCommanderTf(要塞) + Rotate(corpsSlotLocal, corpsFacingDeg) を持ち場にする。
            // その規約どおり往復すること＝配線でズレない。
            Vector2 station = FortressDefenseAiRules.Station(0, 3, B, P, true);
            Vector2 local = FortressDefenseAiRules.StationLocal(station, B);
            Vector2 back = FortressDefenseAiRules.Rotate(local, FortressDefenseAiRules.DefenseFacingDeg(B))
                           + new Vector2(B.fortressX, 0f);
            Assert.AreEqual(station.x, back.x, Eps);
            Assert.AreEqual(station.y, back.y, Eps);
        }

        [Test]
        public void StationLocal_PutsTheStationBehindTheFortressFront()
        {
            // 守備正面（+Y）は攻撃側。持ち場はその反対＝局所 y が負（要塞の後ろに控える）。
            Vector2 local = FortressDefenseAiRules.StationLocal(new Vector2(44f, 0f), B);
            Assert.Less(local.y, 0f);
            Assert.AreEqual(-26f, local.y, Eps);
        }

        // ── 追撃の上限（要塞を空にしない） ──

        [Test]
        public void PursuitRadius_InterceptFartherThanGuard_BothInsideSupport()
        {
            float intercept = FortressDefenseAiRules.PursuitRadius(FortressDefenseRole.迎撃, B, P, true);
            float guard = FortressDefenseAiRules.PursuitRadius(FortressDefenseRole.直衛, B, P, true);
            Assert.AreEqual(18f, intercept, Eps);  // 封鎖半径20×0.9
            Assert.AreEqual(10f, guard, Eps);      // 封鎖半径20×0.5
            Assert.Greater(intercept, guard);      // 直衛が一番遠くまで出て行くことは無い
        }

        [Test]
        public void MaxDistanceFromFortress_NeverLeavesTheSupportRadius()
        {
            Assert.AreEqual(44f, FortressDefenseAiRules.MaxDistanceFromFortress(FortressDefenseRole.迎撃, B, P, true), Eps);
            Assert.AreEqual(33f, FortressDefenseAiRules.MaxDistanceFromFortress(FortressDefenseRole.直衛, B, P, true), Eps);
            Assert.LessOrEqual(FortressDefenseAiRules.MaxDistanceFromFortress(FortressDefenseRole.迎撃, B, P, true),
                               P.supportRadius + Eps);
            Assert.LessOrEqual(FortressDefenseAiRules.MaxDistanceFromFortress(FortressDefenseRole.直衛, B, P, true),
                               P.supportRadius + Eps);
        }

        [Test]
        public void PursuitRadius_IsCutDownWhenSupportIsShort()
        {
            // 支援が28しかない＝迎撃の持ち場26から出られるのは2まで。下限4も支援範囲を超えて広がらない。
            var tight = new FortressDefenseAiParams(28f, 6f, 3f, 0.7f, 0.35f, 0.34f, 0.9f, 0.5f, 6f, 4f, 4f);
            float r = FortressDefenseAiRules.PursuitRadius(FortressDefenseRole.迎撃, B, tight, true);
            Assert.AreEqual(2f, r, Eps);
            Assert.LessOrEqual(FortressDefenseAiRules.MaxDistanceFromFortress(FortressDefenseRole.迎撃, B, tight, true),
                               tight.supportRadius + Eps);
        }

        [Test]
        public void PursuitRadius_AfterFortressFalls_CoversTheBreakoutRouteOnly()
        {
            // 要塞喪失後は縛りを解くが、要塞跡〜突破線（52−18＝34）を超えて追わない＝背後を空けない。
            Assert.AreEqual(34f, FortressDefenseAiRules.PursuitRadius(FortressDefenseRole.迎撃, B, P, false), Eps);
            Assert.AreEqual(34f, FortressDefenseAiRules.PursuitRadius(FortressDefenseRole.直衛, B, P, false), Eps);
        }

        // ── 復帰条件 ──

        [Test]
        public void ShouldReturnToStation_TriggersWhenChasedTooFar()
        {
            Vector2 station = new Vector2(44f, 0f);
            Vector2 fortress = new Vector2(18f, 0f);
            // 追撃18＋余裕6＝24 まではその場で戦ってよい。
            Assert.IsFalse(FortressDefenseAiRules.ShouldReturnToStation(
                new Vector2(20f, 0f), station, fortress, 18f, 48f, 6f));
            Assert.IsTrue(FortressDefenseAiRules.ShouldReturnToStation(
                new Vector2(19f, 0f), station, fortress, 18f, 48f, 6f));
        }

        [Test]
        public void ShouldReturnToStation_TriggersWhenOutOfFortressSupport()
        {
            Vector2 station = new Vector2(44f, 0f);
            Vector2 fortress = new Vector2(18f, 0f);
            // 持ち場からは23（許容24内）でも、要塞から49＝支援48の外なので戻る。
            Assert.IsTrue(FortressDefenseAiRules.ShouldReturnToStation(
                new Vector2(67f, 0f), station, fortress, 18f, 48f, 6f));
            // 支援半径0（要塞喪失後）は支援判定を行わない＝持ち場の距離だけで決める。
            Assert.IsFalse(FortressDefenseAiRules.ShouldReturnToStation(
                new Vector2(67f, 0f), station, fortress, 18f, 0f, 6f));
        }

        [Test]
        public void ShouldReturnToStation_FalseOnStation()
        {
            Assert.IsFalse(FortressDefenseAiRules.ShouldReturnToStation(
                new Vector2(44f, 0f), new Vector2(44f, 0f), new Vector2(18f, 0f), 18f, 48f, 6f));
        }

        // ── 追撃目標の丸め ──

        [Test]
        public void ClampPursuit_PullsTheTargetBackIntoTheLeash()
        {
            Vector2 station = new Vector2(44f, 0f);
            // 敵を追って x=10 まで行こうとしても、持ち場から18の円へ丸める。
            Vector2 clamped = FortressDefenseAiRules.ClampPursuit(new Vector2(10f, 0f), station, 18f);
            Assert.AreEqual(26f, clamped.x, Eps);
            Assert.AreEqual(0f, clamped.y, Eps);
            // 円の内側はそのまま。
            Vector2 near = FortressDefenseAiRules.ClampPursuit(new Vector2(38f, 2f), station, 18f);
            Assert.AreEqual(38f, near.x, Eps);
            Assert.AreEqual(2f, near.y, Eps);
            // 半径0でも落ちない（持ち場そのものへ丸まる）。
            Vector2 zero = FortressDefenseAiRules.ClampPursuit(new Vector2(10f, 0f), station, 0f);
            Assert.AreEqual(44f, zero.x, Eps);
        }

        // ── パラメータのクランプ ──

        [Test]
        public void Params_ClampInvertedMarginsAndRatios()
        {
            // 直衛の余白が迎撃より大きくても、必ず迎撃線より要塞寄りへ丸める（役割が逆転しない）。
            var p = new FortressDefenseAiParams(48f, 6f, 20f, 5f, -1f, 3f, 2f, 5f, -3f, -1f, -2f);
            Assert.AreEqual(6f, p.guardMargin, Eps);
            Assert.AreEqual(1f, p.spreadRatio, Eps);
            Assert.AreEqual(0f, p.guardSpanRatio, Eps);
            Assert.AreEqual(1f, p.guardRatio, Eps);
            Assert.AreEqual(1f, p.interceptEngageRatio, Eps);
            Assert.AreEqual(1f, p.guardEngageRatio, Eps);   // 迎撃を超えない
            Assert.AreEqual(0f, p.returnSlack, Eps);
            Assert.AreEqual(0f, p.minPursuitRadius, Eps);
            Assert.AreEqual(0f, p.exitStandoff, Eps);
        }

        [Test]
        public void Default_ParamsAreTheDocumentedValues()
        {
            Assert.AreEqual(48f, P.supportRadius, Eps);
            Assert.AreEqual(6f, P.interceptMargin, Eps);
            Assert.AreEqual(3f, P.guardMargin, Eps);
            Assert.AreEqual(0.7f, P.spreadRatio, Eps);
            Assert.AreEqual(0.35f, P.guardSpanRatio, Eps);
            Assert.AreEqual(0.34f, P.guardRatio, Eps);
            Assert.AreEqual(0.9f, P.interceptEngageRatio, Eps);
            Assert.AreEqual(0.5f, P.guardEngageRatio, Eps);
            Assert.AreEqual(6f, P.returnSlack, Eps);
            Assert.AreEqual(4f, P.minPursuitRadius, Eps);
            Assert.AreEqual(4f, P.exitStandoff, Eps);
        }
    }
}
