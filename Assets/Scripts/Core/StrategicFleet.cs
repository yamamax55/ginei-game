using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ上の艦隊（C-1 #34）。星系に停泊、または回廊上を「時間制ワープ」で移動する。
    /// 移動は回廊コスト length を warpSpeed で消化＝ワープに時間がかかる（援軍要素の前提）。
    /// 回廊以外への移動はできない（GalaxyMap で接続を判定）。純データ＋時間進行。
    /// </summary>
    public class StrategicFleet
    {
        public int id;
        public Faction faction;

        /// <summary>戦闘力（回廊戦闘の勝敗・消耗に使う抽象兵力）。</summary>
        public int strength = 100;

        /// <summary>補給レディネス（0..1・既定1.0＝満補給・MILSUP-6 #2049）。補給線#94 から切れると下がり、低いと損耗・戦闘力低下（<see cref="MilitarySupplyTickRules"/>）。</summary>
        public float supply = 1f;

        /// <summary>ワープ速度（コスト/秒）。回廊 length をこの速度で消化する。</summary>
        public float warpSpeed = 1f;

        /// <summary>前線回廊（FTL不可）を進むときの速度倍率（&lt;1＝亜光速で遅い）。</summary>
        public float sublightFactor = 0.35f;

        public int currentSystemId;       // 停泊中の星系（移動中は出発元）
        public int destinationSystemId;   // 移動中の目的地

        /// <summary>
        /// 所属軍団のグループID（-1＝軍団に属さない＝独立艦隊・既定）。同じ <see cref="corpsId"/> の戦略艦隊どうしを
        /// 「同一軍団の隷下」として扱う（戦略マップで四角に囲って可視化する）。会戦層の編制ツリー（<see cref="OrderOfBattle"/>）
        /// とは別＝戦略盤面の駒のグルーピング。-1 のままなら従来動作（後方互換）。
        /// </summary>
        public int corpsId = -1;

        /// <summary>軍団名（表示用・空＝なし）。</summary>
        public string corpsName;

        /// <summary>この艦隊に軍団長が乗艦しているか＝軍団旗艦（CSG・打撃群指揮官モデル）。戦略マップで識別表示する。</summary>
        public bool isCorpsFlagship;

        /// <summary>軍団に属するか（<see cref="corpsId"/> が有効）。</summary>
        public bool HasCorps => corpsId >= 0;

        /// <summary>
        /// 所属軍集団のグループID（-1＝軍集団に属さない・既定）。軍団（<see cref="corpsId"/>）の上位梯団＝同じ
        /// <see cref="armyGroupId"/> の軍団どうしを「同一軍集団」として扱う。複数の軍団が同一星系に集まったとき、
        /// 戦略マップで軍団の四角の外側にさらに大きな四角を描いて軍集団を示す。-1 のままなら従来動作（後方互換）。
        /// </summary>
        public int armyGroupId = -1;

        /// <summary>軍集団名（表示用・空＝なし）。</summary>
        public string armyGroupName;

        /// <summary>軍集団に属するか（<see cref="armyGroupId"/> が有効）。</summary>
        public bool HasArmyGroup => armyGroupId >= 0;

        /// <summary>
        /// 交戦中（回廊で敵対艦隊と接触し戦闘に固着）か。true の間は Tick で前進しない
        /// ＝回廊上に「交戦中の回廊」として留まり、プレイヤーが潜行（ダブルクリック）するか
        /// 自動解決されるまで動かない（C-2 二層遷移 #586）。決着で解除される。
        /// </summary>
        public bool engaged;

        private bool onCorridor;
        private float corridorLength;
        private float traveled;
        private float holdFraction = 1f;   // 0..1。1=目的地まで／<1=回廊上のその位置で停止保持
        private bool sublightHop;          // 現在のホップが前線回廊（亜光速）か
        private List<int> route;   // 多ホップ経路の残り（次の目的地より先の星系ID列）

        /// <summary>回廊上にいるか（前進中＋停止保持中の両方）。停泊中は false。</summary>
        public bool IsOnCorridor => onCorridor;

        /// <summary>回廊上で前進中か（保持位置に未到達）。</summary>
        public bool IsMoving => onCorridor && traveled < HoldDistance;

        /// <summary>回廊上の指定位置で停止保持しているか。</summary>
        public bool IsHolding => onCorridor && traveled >= HoldDistance;

        private float HoldDistance => Mathf.Clamp01(holdFraction) * corridorLength;

        /// <summary>現在のホップの実効速度（前線は亜光速で遅い）。</summary>
        private float CurrentSpeed => warpSpeed * (sublightHop ? Mathf.Max(0f, sublightFactor) : 1f);

        /// <summary>現在のホップが前線回廊（FTL不可・亜光速）を進んでいるか。</summary>
        public bool IsSublight => IsMoving && sublightHop;

        /// <summary>現在の回廊での進行度（0..1）。停泊中は1。</summary>
        public float Progress => corridorLength > 0f ? Mathf.Clamp01(traveled / corridorLength) : 1f;

        /// <summary>保持位置までの推定時間（秒）。前進中のみ。</summary>
        public float Eta => (IsMoving && CurrentSpeed > 0f) ? Mathf.Max(0f, (HoldDistance - traveled) / CurrentSpeed) : 0f;

        /// <summary>多ホップ経路がまだ残っているか（途中星系を経由中）。</summary>
        public bool HasRoute => route != null && route.Count > 0;

        /// <summary>最終目的地の星系ID（経路があればその終点／移動中なら現在の目的地／停泊中は現在地）。</summary>
        public int FinalDestinationId =>
            (route != null && route.Count > 0) ? route[route.Count - 1]
            : (IsMoving ? destinationSystemId : currentSystemId);

        public StrategicFleet() { }

        public StrategicFleet(int id, int startSystemId, Faction faction = Faction.帝国, float warpSpeed = 1f)
        {
            this.id = id;
            this.currentSystemId = startSystemId;
            this.destinationSystemId = startSystemId;
            this.faction = faction;
            this.warpSpeed = warpSpeed;
        }

        /// <summary>
        /// 隣接星系 destId へワープを開始する。回廊が無い／移動中／同一星系なら失敗(false)。
        /// </summary>
        public bool BeginWarp(GalaxyMap map, int destId, float holdFrac = 1f)
        {
            if (map == null || IsOnCorridor || destId == currentSystemId) return false;
            Corridor c = map.GetCorridor(currentSystemId, destId);
            if (c == null) return false;                       // 回廊以外＝移動不可
            destinationSystemId = destId;
            corridorLength = Mathf.Max(0.0001f, c.length);
            traveled = 0f;
            holdFraction = Mathf.Clamp01(holdFrac);
            sublightHop = StrategyRules.IsFtlBlocked(map, c);  // 前線回廊は亜光速（FTL不可でも遅い航行は可）
            onCorridor = true;
            return true;
        }

        /// <summary>
        /// goalId まで最短経路（回廊 length 合計が最小）でワープを開始する。到達不能／同一星系なら false。
        /// 移動中でも受理し、その場合は現在のホップ（到達予定の星系まで）は維持したまま、到達予定星系から
        /// goalId への経路に引き直す＝「次の星系に着いてから新しい目的地へ向かう」。
        /// 経由星系は到着ごとに自動で次へ継続する（Tick(map,dt) を使うこと）。
        /// </summary>
        public bool WarpTo(GalaxyMap map, int goalId)
        {
            if (map == null) return false;

            // 回廊上（前進中or保持中）：現在のホップは維持し、到達予定星系から goalId へ経路を引き直す。
            // 保持していた場合は解除して前進を再開する。
            if (IsOnCorridor)
            {
                // 飛び石禁止：到達予定の星系が自勢力の所有でなければ、そこで止まって占領する（先へは進まない）。
                StarSystem destSys = map.GetSystem(destinationSystemId);
                if (destSys == null || destSys.owner != faction)
                {
                    route = new List<int>();
                    holdFraction = 1f; // 保持解除＝到達予定星系まで前進し、そこで止まる
                    return true;
                }
                List<int> p = GalaxyPathfinder.FindPath(map, destinationSystemId, goalId);
                if (p.Count == 0) return false; // 到達不能
                int stop = FirstUnownedIndex(map, p); // 自勢力領を抜けて最初の非所有星系で止まる
                route = (stop >= 1) ? p.GetRange(1, stop) : new List<int>();
                holdFraction = 1f; // 保持解除＝目的地まで前進再開
                return true;
            }

            if (goalId == currentSystemId) return false;
            List<int> path = GalaxyPathfinder.FindPath(map, currentSystemId, goalId);
            if (path == null || path.Count < 2) return false; // 到達不能

            // 飛び石禁止＝必ず占領してから移動：自勢力の所有星系は通り抜けられるが、経路上の最初の非所有星系で
            // 止まる（その星系へ入って占領する）。占領後に改めて命令すれば先へ進める（星系を1つずつ取る）。
            int stopIdx = FirstUnownedIndex(map, path);
            int firstHop = path[1];
            route = (stopIdx >= 2) ? path.GetRange(2, stopIdx - 1) : new List<int>();
            return BeginWarp(map, firstHop);
        }

        /// <summary>
        /// 飛び石移動の禁止＝<paramref name="path"/>(path[0]=起点) を歩き、<b>自勢力 owner でない最初の星系の index</b>
        /// を返す（その星系へ入って占領するため、そこで経路を打ち切る）。全て自勢力所有なら末尾 index＝目的地まで進める。
        /// </summary>
        private int FirstUnownedIndex(GalaxyMap map, List<int> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                StarSystem s = map.GetSystem(path[i]);
                if (s == null || s.owner != faction) return i;
            }
            return path.Count - 1;
        }

        /// <summary>
        /// towardSystemId 方向の回廊に入り、その回廊上の fraction（0..1・towardSystem へ向かう向き）の位置で
        /// 停止保持する（前線の途中で止まって守る・待ち伏せる用）。停泊中の艦は隣接回廊へ入って止まる。
        /// 既に同じ回廊を進行中なら保持位置だけ更新する。保持中も回廊上に居るので敵と接触すれば戦闘になる。
        /// </summary>
        public bool HoldOnCorridor(GalaxyMap map, int towardSystemId, float fraction)
        {
            if (map == null) return false;
            fraction = Mathf.Clamp01(fraction);

            if (IsOnCorridor)
            {
                if (destinationSystemId == towardSystemId) { holdFraction = fraction; route = null; return true; }
                return false; // 別回廊/逆方向は非対応（簡易）
            }

            if (towardSystemId == currentSystemId) return false;
            if (!BeginWarp(map, towardSystemId, fraction)) return false;
            route = null; // 保持は多ホップしない
            return true;
        }

        /// <summary>
        /// 銀河時間を deltaTime 進める（単一ホップ用・経路の自動継続なし・後方互換）。
        /// 回廊上を warpSpeed で前進し、到着したら true。
        /// </summary>
        public bool Tick(float deltaTime) => TickInternal(null, deltaTime);

        /// <summary>
        /// 銀河時間を deltaTime 進める（経路追従用）。到着時に残り経路があれば map を使って
        /// 次のホップへ自動継続する。各ホップ到着で true を返す。
        /// </summary>
        public bool Tick(GalaxyMap map, float deltaTime) => TickInternal(map, deltaTime);

        private bool TickInternal(GalaxyMap map, float deltaTime)
        {
            if (engaged) return false;   // 交戦中は回廊上で固着（前進しない）
            if (!IsMoving) return false; // 前進中のみ進む（保持中・停泊中は動かない）
            traveled += CurrentSpeed * deltaTime;
            float hold = HoldDistance;
            if (traveled >= hold)
            {
                if (holdFraction >= 1f)
                {
                    // 目的地の星系に到達
                    currentSystemId = destinationSystemId;
                    onCorridor = false;
                    traveled = 0f;
                    corridorLength = 0f;

                    // 残り経路があれば次のホップへ自動継続（map が要る）
                    if (map != null && route != null && route.Count > 0)
                    {
                        int next = route[0];
                        route.RemoveAt(0);
                        BeginWarp(map, next);
                    }
                }
                else
                {
                    // 回廊上の保持位置に到達＝停止保持（onCorridor のまま・以後 IsMoving は false）
                    traveled = hold;
                }
                return true; // 到達（目的地 or 保持位置）
            }
            return false;
        }

        /// <summary>
        /// 別艦隊と同じ回廊上にいるか（両者とも移動中で、回廊エッジ{出発元,目的地}が一致）。
        /// 回廊での会戦トリガー判定に使う（StrategyRules.FindEncounters）。
        /// </summary>
        public bool IsOnSameCorridor(StrategicFleet other)
        {
            if (other == null || !IsOnCorridor || !other.IsOnCorridor) return false;
            int aMin = Mathf.Min(currentSystemId, destinationSystemId);
            int aMax = Mathf.Max(currentSystemId, destinationSystemId);
            int bMin = Mathf.Min(other.currentSystemId, other.destinationSystemId);
            int bMax = Mathf.Max(other.currentSystemId, other.destinationSystemId);
            return aMin == bMin && aMax == bMax;
        }
    }
}
