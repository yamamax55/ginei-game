using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 援軍（ワープイン・#38 C-5）の<b>所要時間と到着時刻</b>の純ロジック。乱数なし決定論。
    ///
    /// 速度モデルは <see cref="StrategicFleet"/> と同じ考え方＝「回廊 length を warpSpeed で消化し、
    /// 前線回廊（FTL不可）は sublightFactor ぶん遅い」。数式を二重実装しないよう、判定は既存窓口
    /// （<see cref="StrategyRules.IsFtlBlocked"/>／<see cref="GalaxyPathfinder.FindPath"/>）へ委譲し、
    /// ここは「距離÷速度＝所要 game-秒」と「now＋所要＝到着 game-秒」の変換だけを担う。
    ///
    /// 出現座標（自陣側の端）は既存の <see cref="ReinforcementRules.EdgePosition"/> が唯一の窓口＝ここでは持たない。
    /// 到着済みの取り出し・二重出現の防止は <see cref="WarpReinforcementLedger"/> が担う。test-first。
    /// </summary>
    public static class WarpReinforcementRules
    {
        /// <summary>到達不能／速度0を表す所要時間（永遠に着かない）。</summary>
        public const float Unreachable = float.PositiveInfinity;

        /// <summary>
        /// ワープ距離（回廊 length の合計）÷ ワープ速度＝所要 game-秒。
        /// 距離0以下は0（即時）、速度0以下は <see cref="Unreachable"/>（永遠に着かない）。
        /// </summary>
        public static float TravelSeconds(float distance, float warpSpeed)
        {
            if (distance <= 0f) return 0f;
            if (warpSpeed <= 0f) return Unreachable;
            return distance / warpSpeed;
        }

        /// <summary>
        /// 前線（FTL不可＝亜光速）を考慮した所要 game-秒。<paramref name="sublight"/> のとき
        /// 実効速度は <c>warpSpeed × sublightFactor</c>（<see cref="StrategicFleet.sublightFactor"/> と同じ扱い）。
        /// </summary>
        public static float TravelSeconds(float distance, float warpSpeed, bool sublight, float sublightFactor)
        {
            float speed = warpSpeed * (sublight ? Mathf.Max(0f, sublightFactor) : 1f);
            return TravelSeconds(distance, speed);
        }

        /// <summary>
        /// 経路（星系IDの並び・path[0]＝出発地）に沿った所要 game-秒。各ホップの回廊 length を
        /// 実効速度で消化して足し合わせる（前線回廊のホップだけ亜光速）。
        /// 経路が繋がっていない（回廊が無い）ホップがあれば <see cref="Unreachable"/>。
        /// </summary>
        public static float TravelSecondsAlongRoute(GalaxyMap map, IList<int> path, float warpSpeed, float sublightFactor)
        {
            if (map == null || path == null || path.Count == 0) return Unreachable;
            if (path.Count == 1) return 0f;                       // 出発地＝目的地
            if (warpSpeed <= 0f) return Unreachable;

            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Corridor c = map.GetCorridor(path[i - 1], path[i]);
                if (c == null) return Unreachable;                // 回廊以外は通れない
                bool sublight = StrategyRules.IsFtlBlocked(map, c);
                float hop = TravelSeconds(Mathf.Max(0f, c.length), warpSpeed, sublight, sublightFactor);
                if (float.IsPositiveInfinity(hop)) return Unreachable;
                total += hop;
            }
            return total;
        }

        /// <summary>
        /// 出発星系から戦場までの所要 game-秒。回廊戦場は<b>両端のうち早く着くほうの端</b>から入る
        /// （＝自陣側の入口。<paramref name="entrySystemId"/> にその端を返す）。星系戦場はその星系まで。
        /// 到達不能なら <see cref="Unreachable"/>／<paramref name="entrySystemId"/>＝-1。
        /// 経路探索は <see cref="GalaxyPathfinder"/> に委譲する（並行する探索を作らない）。
        /// </summary>
        public static float TravelSecondsToBattlefield(GalaxyMap map, int fromSystemId, BattlefieldKey battlefield,
                                                       float warpSpeed, float sublightFactor, out int entrySystemId)
        {
            entrySystemId = -1;
            if (map == null || !battlefield.IsValid) return Unreachable;

            float best = Unreachable;
            // 星系戦場は端が1つ（systemA＝systemB）。回廊戦場は両端を比べて早いほうから入る。
            for (int side = 0; side < 2; side++)
            {
                int endpoint = (side == 0) ? battlefield.systemA : battlefield.systemB;
                if (side == 1 && battlefield.IsSystemBattle) break;

                List<int> path = (endpoint == fromSystemId)
                    ? new List<int> { fromSystemId }
                    : GalaxyPathfinder.FindPath(map, fromSystemId, endpoint);
                if (path == null || path.Count == 0) continue;    // 到達不能な端

                float t = TravelSecondsAlongRoute(map, path, warpSpeed, sublightFactor);
                if (float.IsPositiveInfinity(t)) continue;

                // ★端の星系に着いただけでは戦場に着いたことにならない。回廊戦場では、そこから
                // <b>回廊へ入って戦っている地点まで進む</b>ぶんの時間が要る。これを足さないと、
                // 戦場の端に停泊している艦隊の所要時間が 0 になり、派遣した瞬間にワープインしてしまう
                // （実機QA：入口に置いた予備が戦術マップへ入った直後に出現した）。
                t += InCorridorSeconds(map, battlefield, warpSpeed, sublightFactor);

                if (t < best) { best = t; entrySystemId = endpoint; }
            }
            return best;
        }

        /// <summary>
        /// 戦場が回廊のとき、端から<b>戦っている地点まで</b>進むのに要る game-秒。
        /// 距離は回廊長の <see cref="EngagementFraction"/> ぶん（＝おおむね中ほどで戦っている）。
        /// 星系戦場・回廊が見つからないときは 0。
        /// </summary>
        public static float InCorridorSeconds(GalaxyMap map, BattlefieldKey battlefield,
                                              float warpSpeed, float sublightFactor)
        {
            if (map == null || !battlefield.IsValid || battlefield.IsSystemBattle) return 0f;
            Corridor c = map.GetCorridor(battlefield.systemA, battlefield.systemB);
            if (c == null) return 0f;

            bool sublight = StrategyRules.IsFtlBlocked(map, c);
            return TravelSeconds(Mathf.Max(0f, c.length) * EngagementFraction,
                                 warpSpeed, sublight, sublightFactor);
        }

        /// <summary>回廊のどのあたりで戦っているとみなすか（端からの割合）。</summary>
        public const float EngagementFraction = 0.5f;

        /// <summary>到着する絶対 game-秒＝現在時刻＋所要時間（負の所要は0扱い）。</summary>
        public static double ArrivalTime(double now, float travelSeconds)
        {
            if (float.IsPositiveInfinity(travelSeconds)) return double.PositiveInfinity;
            return now + Mathf.Max(0f, travelSeconds);
        }

        /// <summary>到着済みか（現在 game-秒が到着時刻以上）。</summary>
        public static bool HasArrived(double arrivalTime, double now) => now >= arrivalTime;

        /// <summary>到着までの残り game-秒（到着済みは0・到達不能は <see cref="Unreachable"/>）。UI の「到着予定」表示用。</summary>
        public static float RemainingSeconds(double arrivalTime, double now)
        {
            if (double.IsPositiveInfinity(arrivalTime)) return Unreachable;
            double remain = arrivalTime - now;
            return remain <= 0.0 ? 0f : (float)remain;
        }

        /// <summary>航行の進捗 0..1（派遣時=0・到着時=1）。所要0や不正な区間は1（＝即時到着）。UI のゲージ用。</summary>
        public static float ArrivalProgress(double dispatchTime, double arrivalTime, double now)
        {
            double span = arrivalTime - dispatchTime;
            if (double.IsPositiveInfinity(arrivalTime)) return 0f;
            if (span <= 0.0) return 1f;
            return Mathf.Clamp01((float)((now - dispatchTime) / span));
        }
    }
}
