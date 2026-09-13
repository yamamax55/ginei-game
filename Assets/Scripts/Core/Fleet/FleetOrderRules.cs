using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 移動命令を出せない理由。<see cref="なし"/>＝発令できる。
    /// <see cref="要塞封鎖"/> だけは「拒否」ではなく<b>警告</b>＝呼び手は発令してよい（詳細は
    /// <see cref="FleetOrderRules.CanMoveTo"/> を参照）。
    /// </summary>
    public enum MoveOrderRejection
    {
        なし,
        敵軍艦隊,
        増援航行中,
        交戦中,
        目的地が現在地,
        回廊が無い,
        到達不能,
        要塞封鎖,
    }

    /// <summary>
    /// 戦略艦隊への<b>移動命令の可否</b>と<b>現在の行先</b>を判定する純ロジック（艦隊メニューからの発令用）。
    ///
    /// 移動命令の入口は艦隊メニューへ集約し、戦略MAPは「どこからどこへ移動中か」の表示専用にする。
    /// ここはその判定と表示文字列だけを持つ＝盤面・UI に依存しない（非 MonoBehaviour・決定論）。
    ///
    /// 経路探索は <see cref="GalaxyPathfinder.FindPath(GalaxyMap,int,int,GalaxyPathfinder.PathQuery)"/>、
    /// 要塞の封鎖は <see cref="GalaxyPathfinder.PathQuery.AvoidingFortresses"/>（＝<see cref="FortressBlockadeRules.Blocks"/>
    /// ＝<see cref="StrategyRules.IsFortressBlocked"/>）へ<b>委譲</b>する＝ここで再実装しない。
    /// 艦隊の状態（回廊上か・交戦中か・要塞に足止めか）は <see cref="StrategicFleet"/> の派生プロパティを読むだけ。
    /// </summary>
    public static class FleetOrderRules
    {
        /// <summary>
        /// その艦隊に命令を出せるか（目的地に依らない事前判定）。<paramref name="player"/> は操作している勢力。
        /// 艦隊が null なら「自軍の艦隊ではない」扱い＝<see cref="MoveOrderRejection.敵軍艦隊"/>（命令不可）。
        /// </summary>
        public static MoveOrderRejection CanOrder(StrategicFleet fleet, Faction player)
        {
            if (fleet == null) return MoveOrderRejection.敵軍艦隊;          // 選択されていない＝命令できない
            if (fleet.faction != player) return MoveOrderRejection.敵軍艦隊; // 他勢力の駒は動かせない
            if (fleet.warpingAsReinforcement) return MoveOrderRejection.増援航行中; // #38 別の戦場へ航行中
            if (fleet.engaged) return MoveOrderRejection.交戦中;            // 戦闘に固着＝離脱は決着待ち
            return MoveOrderRejection.なし;
        }

        /// <summary>
        /// その艦隊を <paramref name="goalId"/> へ動かせるか（<see cref="CanOrder"/> の判定＋目的地ごとの判定）。
        ///
        /// <b>要塞封鎖について</b>：素の経路は通るのに要塞を避けた経路（<see cref="GalaxyPathfinder.PathQuery.AvoidingFortresses"/>）
        /// が無いとき <see cref="MoveOrderRejection.要塞封鎖"/> を返すが、これは<b>発令を拒否する理由ではない</b>。
        /// 「行けるが要塞を落とすしかない」という<b>警告</b>で、呼び手（艦隊メニュー）は注意書きを出したうえで
        /// そのまま発令してよい（<see cref="StrategicFleet.WarpTo"/> は封鎖を承知で受理し、要塞の手前で足を止める）。
        /// 他の理由（<see cref="MoveOrderRejection.敵軍艦隊"/> 等）は発令してはならない。
        ///
        /// 移動中の艦隊は「到達予定の星系」から経路を引き直す＝<see cref="StrategicFleet.WarpTo"/> と同じ起点で判定する。
        /// </summary>
        public static MoveOrderRejection CanMoveTo(GalaxyMap map, StrategicFleet fleet, Faction player, int goalId)
        {
            MoveOrderRejection pre = CanOrder(fleet, player);
            if (pre != MoveOrderRejection.なし) return pre;

            // 盤面が無い＝目的地の星系を解決できない（回廊もたどれない）。
            if (map == null) return MoveOrderRejection.回廊が無い;
            if (map.GetSystem(goalId) == null) return MoveOrderRejection.回廊が無い;

            // 停泊中に自分の居る星系を指した＝命令の意味が無い（移動中なら出発元を指しても引き返しになるので許す）。
            if (!fleet.IsOnCorridor && goalId == fleet.currentSystemId) return MoveOrderRejection.目的地が現在地;

            int startId = fleet.IsOnCorridor ? fleet.destinationSystemId : fleet.currentSystemId;

            // 素の最短経路（封鎖を無視）が無ければ、そもそもグラフ上つながっていない。
            List<int> raw = GalaxyPathfinder.FindPath(map, startId, goalId);
            if (raw == null || raw.Count == 0) return MoveOrderRejection.到達不能;

            // 迂回路（敵要塞の封鎖を避けた経路）が無い＝要塞を落とすしかない＝警告。
            List<int> detour = GalaxyPathfinder.FindPath(
                map, startId, goalId, GalaxyPathfinder.PathQuery.AvoidingFortresses(player));
            if (detour == null || detour.Count == 0) return MoveOrderRejection.要塞封鎖;

            return MoveOrderRejection.なし;
        }

        /// <summary>理由の日本語1行（UI にそのまま出す）。<see cref="MoveOrderRejection.なし"/>＝空文字。</summary>
        public static string RejectionText(MoveOrderRejection reason)
        {
            switch (reason)
            {
                case MoveOrderRejection.なし: return "";
                case MoveOrderRejection.敵軍艦隊: return "自軍の艦隊ではないため命令できません";
                case MoveOrderRejection.増援航行中: return "増援として航行中のため命令できません";
                case MoveOrderRejection.交戦中: return "交戦中のため命令できません";
                case MoveOrderRejection.目的地が現在地: return "すでにその星系に停泊しています";
                case MoveOrderRejection.回廊が無い: return "その星系へ通じる回廊がありません";
                case MoveOrderRejection.到達不能: return "そこへ至る経路がありません";
                case MoveOrderRejection.要塞封鎖: return "敵要塞が回廊を扼しています。迂回路が無いため要塞を攻略する必要があります";
                default: return "命令できません";
            }
        }

        /// <summary>
        /// 艦隊の状態ラベル（停泊中／移動中／交戦中／要塞に足止め／増援航行中）。null は空文字。
        /// 回廊上で停止保持している艦も「移動中」に含める（回廊上に居る＝盤面では航行中の駒として扱う）。
        /// </summary>
        public static string StateLabel(StrategicFleet fleet)
        {
            if (fleet == null) return "";
            if (fleet.warpingAsReinforcement) return "増援航行中"; // #38 盤面の進行から外れている
            if (fleet.engaged) return "交戦中";
            if (fleet.IsBlockadedByFortress) return "要塞に足止め"; // #40 要塞の手前で釘付け
            if (fleet.IsOnCorridor) return "移動中";
            return "停泊中";
        }

        /// <summary>
        /// 現在地と行先。移動中は <paramref name="fromId"/>=出発元・<paramref name="hopToId"/>=いま向かっている隣の星系・
        /// <paramref name="finalId"/>=最終目的地。停泊中は3つとも現在星系。艦隊が null なら false（出力は 0）。
        /// </summary>
        public static bool TryDescribeRoute(StrategicFleet fleet, out int fromId, out int hopToId, out int finalId)
        {
            fromId = 0;
            hopToId = 0;
            finalId = 0;
            if (fleet == null) return false;

            if (!fleet.IsOnCorridor)
            {
                fromId = fleet.currentSystemId;
                hopToId = fleet.currentSystemId;
                finalId = fleet.currentSystemId;
                return true;
            }

            // 回廊上：currentSystemId は出発元のまま、destinationSystemId が現在区間の相手。
            fromId = fleet.currentSystemId;
            hopToId = fleet.destinationSystemId;
            // 残り経路があればその終点が最終目的地。無ければ現在区間の相手で終わり
            // （保持中は FinalDestinationId が出発元を返すため、そのまま使わない）。
            finalId = fleet.HasRoute ? fleet.FinalDestinationId : hopToId;
            return true;
        }

        /// <summary>複数ホップか（現在区間の先にまだ経路が残っている）。null は false。</summary>
        public static bool IsMultiHop(StrategicFleet fleet) => fleet != null && fleet.HasRoute;

        // ===== 飛び石移動の禁止（必ず占領してから先へ進む）=====

        /// <summary>
        /// 経路上で<b>最初の非 <paramref name="faction"/> 所有星系</b>の index を返す（path[0]=起点）。
        /// 全部が自勢力なら末尾 index。これが「飛び石禁止」の唯一の窓口で、
        /// <see cref="StrategicFleet.WarpTo"/> はここで経路を打ち切る＝その星系へ入って占領し、
        /// 改めて命令すれば先へ進める。<b>経路配列そのものが切り詰められる</b>ので、
        /// これは表示の話ではなく実際の到着地でもある。
        /// </summary>
        public static int FirstUnownedIndex(GalaxyMap map, List<int> path, Faction faction)
        {
            if (map == null || path == null || path.Count == 0) return 0;
            for (int i = 1; i < path.Count; i++)
            {
                StarSystem s = map.GetSystem(path[i]);
                if (s == null || s.owner != faction) return i;
            }
            return path.Count - 1;
        }

        /// <summary>
        /// <paramref name="goalId"/> を指示したとき、飛び石禁止によって<b>実際に止まる星系</b>を先読みする。
        /// 目的地まで自勢力領が続いていれば goalId そのもの。途中に他勢力の星系があればそこで止まる。
        /// 発令前の説明・発令後の通知・盤面の表示を、この1つの答えで揃えるために使う
        /// （実機QA：「アイガーへ移動」と言いながらセロトーレで止まる食い違いがあった）。
        /// 到達不能・引数不正のときは <paramref name="goalId"/> を返す。
        /// </summary>
        public static int PredictedStop(GalaxyMap map, StrategicFleet fleet, Faction player, int goalId)
        {
            if (map == null || fleet == null) return goalId;

            List<int> path = PlannedRoute(map, fleet, goalId);
            if (path == null || path.Count < 2) return goalId;

            int stop = FirstUnownedIndex(map, path, fleet.faction);
            return path[Mathf.Clamp(stop, 0, path.Count - 1)];
        }

        /// <summary>
        /// その艦隊がその目的地へ<b>実際に取る航路</b>（起点を含む星系ID列）。到達不能なら空。
        ///
        /// 起点は <see cref="StrategicFleet.WarpTo"/> と同じ＝移動中は到達予定の星系から引き直す。
        /// 経路の選び方も <c>PlanRoute</c> と同じ順序＝<b>要塞を避けた経路を優先し、無ければ素の最短</b>。
        /// 表示（距離・経路・停止予定）と発令が別々に経路を引くと食い違うので、
        /// <b>この1本を通す</b>（<see cref="PredictedStop"/> もここを使う）。
        ///
        /// 返すのは<b>切り詰める前</b>の経路＝目的地まで含む。飛び石禁止でどこまで進むかは
        /// <see cref="FirstUnownedIndex"/>／<see cref="PredictedStop"/> が別に決める。
        /// </summary>
        public static List<int> PlannedRoute(GalaxyMap map, StrategicFleet fleet, int goalId)
        {
            if (map == null || fleet == null) return new List<int>();

            int startId = fleet.IsOnCorridor ? fleet.destinationSystemId : fleet.currentSystemId;
            if (startId == goalId) return new List<int> { startId };

            List<int> path = GalaxyPathfinder.FindPath(
                map, startId, goalId, GalaxyPathfinder.PathQuery.AvoidingFortresses(fleet.faction));
            if (path == null || path.Count == 0) path = GalaxyPathfinder.FindPath(map, startId, goalId);
            return path ?? new List<int>();
        }

        /// <summary>
        /// 実際に取る航路のホップ数。到達不能は <see cref="FleetDestinationSortRules.Unreachable"/>（-1）
        /// ＝並べ替えで「近い」と扱わせないための番兵。現在地はもちろん 0。
        /// </summary>
        public static int PlannedHops(GalaxyMap map, StrategicFleet fleet, int goalId)
        {
            List<int> path = PlannedRoute(map, fleet, goalId);
            if (path == null || path.Count == 0) return FleetDestinationSortRules.Unreachable;
            return path.Count - 1;
        }

        /// <summary>
        /// 発令前に出す一言（実際の挙動と一致させるため）。目的地まで一気に行けるなら空文字、
        /// 途中で止まるなら「まず ○○ を占領（そこで一旦停止）」を返す。星系名は呼び手が解決して渡す。
        /// </summary>
        public static string StopoverNote(GalaxyMap map, StrategicFleet fleet, Faction player, int goalId,
                                          System.Func<int, string> systemName)
        {
            int stop = PredictedStop(map, fleet, player, goalId);
            if (stop == goalId) return "";
            string name = systemName != null ? systemName(stop) : ("#" + stop);
            return $"まず {name} を占領（そこで一旦停止）";
        }
    }
}
