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

        /// <summary>
        /// この艦隊に<b>軍団長が乗艦している</b>か＝軍団旗艦（CSG・打撃群指揮官モデル）。戦略マップで識別表示する。
        ///
        /// ★誤解しやすい点：これは「軍団の指揮を担う艦隊か」であって、<b>旗艦の有無ではない</b>。
        /// 艦隊にはどれも旗艦（司令の乗艦）がある（戦術側＝<see cref="ShipNameRegistry"/> が各艦隊の旗艦に艦名を与える）。
        /// UI で単に「旗艦」と出すと「他の艦隊には旗艦が無い」と読まれるので、
        /// <b>「軍団旗艦」「軍団指揮」</b>など軍団の指揮を担うことが分かる語で出すこと。
        /// </summary>
        public bool isCorpsFlagship;

        /// <summary>
        /// この艦隊の<b>司令官</b>（人物 <see cref="Person.id"/>）。-1＝未任命（既定）。
        ///
        /// 名前・階級は人物側にあり、ここは参照だけを持つ＝<b>艦隊側で名前を作らない</b>
        /// （表示は盤面の人物ロスターから引く。解決できなければ「未任命」と出す）。
        /// 軍団長（<see cref="isCorpsFlagship"/> の艦隊に乗る上位指揮官）とは<b>別</b>＝
        /// 軍団長の名前を艦隊司令として代用しない。
        /// </summary>
        public int commanderPersonId = -1;

        /// <summary>司令官が任命されているか（<see cref="commanderPersonId"/> が有効）。</summary>
        public bool HasCommander => commanderPersonId >= 0;

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
        /// <summary>
        /// この艦隊に所属する<b>艦艇数（隻）</b>。<see cref="strength"/>（抽象兵力）とは別物で、
        /// 艦隊ごとに独立して持ち歩く＝会戦で失った船はこの艦隊からだけ減り、他艦隊へ均等割りしない。
        /// 0 のままなら未初期化＝<see cref="FleetShipCountRules.EnsureInitialized"/> が兵力から埋める
        /// （旧セーブ・既存の盤面との後方互換）。
        /// </summary>
        public int shipCount;

        /// <summary>
        /// <see cref="shipCount"/> が「本当に設定された値」か（0 が全滅を意味するか）。
        /// false のときだけ兵力から導出して埋める。これが無いと<b>全滅して0隻になった艦隊が
        /// 未初期化と誤解されて艦艇が復活</b>してしまう。
        /// </summary>
        public bool shipCountSet;

        /// <summary>艦艇数（未初期化なら兵力から導出した値）。表示・集計はこちらを読む。</summary>
        public int Ships => shipCountSet ? Mathf.Max(0, shipCount)
                                         : FleetShipCountRules.EnsureInitialized(shipCount, strength);

        /// <summary>艦艇数を確定して設定する（0＝全滅も正しく保持される）。</summary>
        public void SetShips(int count)
        {
            shipCount = Mathf.Max(0, count);
            shipCountSet = true;
        }

        public bool engaged;

        /// <summary>
        /// 進行中の戦場へ援軍として航行中か（#38 C-5）。true のあいだ盤面では「増援航行中」として扱い、
        /// 通常の移動・接敵判定から外す。到着（会戦へ出現）か差し戻し（戦闘終了）で false へ戻る。
        /// 実際の到着時刻は <see cref="WarpReinforcementLedger"/> が持ち、こちらは表示と進行の抑止だけ。
        /// </summary>
        public bool warpingAsReinforcement;

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

        /// <summary>
        /// #40：敵の要塞が扼する回廊で前進できる上限割合（1＝制限なし）。回廊へ入るとき（<see cref="BeginWarp"/>）と
        /// 毎 Tick に引き直す＝<b>要塞が落ちれば同じ艦隊がその場から先へ進める</b>／落とし返されればまた止まる。
        /// 派生値なのでセーブしない（盤面から毎回求まる）。
        /// </summary>
        private float blockadeCap = 1f;

        /// <summary>保持指示（holdFraction）と要塞の封鎖（blockadeCap）の<b>厳しいほう</b>が実際の停止位置。</summary>
        private float EffectiveHoldFraction => Mathf.Min(Mathf.Clamp01(holdFraction), Mathf.Clamp01(blockadeCap));

        private float HoldDistance => EffectiveHoldFraction * corridorLength;

        /// <summary>#40：敵要塞に前進を止められているか（回廊上で要塞の手前に釘付け）。</summary>
        public bool IsBlockadedByFortress => onCorridor && blockadeCap < 1f;

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

        /// <summary>
        /// 実際に保持している残り経路（次の目的地より先の星系ID列・読み取り専用）。
        /// 盤面の経路表示は<b>これをそのまま描く</b>こと。改めて最短経路を計算し直すと、要塞回避や
        /// 飛び石禁止で切り詰めた実航路とずれた線を描いてしまう（実機QAで判明）。
        /// </summary>
        public IReadOnlyList<int> RemainingRoute => route ?? EmptyRoute;

        private static readonly List<int> EmptyRoute = new List<int>();

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
            // #40：敵要塞が扼していれば、この回廊は途中までしか進めない（制圧するまで反対側へ抜けられない）。
            blockadeCap = FortressBlockadeRules.MaxAdvanceFraction(c, faction);
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
                List<int> p = PlanRoute(map, destinationSystemId, goalId);
                if (p.Count == 0) return false; // 到達不能
                int stop = FirstUnownedIndex(map, p); // 自勢力領を抜けて最初の非所有星系で止まる
                route = (stop >= 1) ? p.GetRange(1, stop) : new List<int>();
                holdFraction = 1f; // 保持解除＝目的地まで前進再開
                return true;
            }

            if (goalId == currentSystemId) return false;
            List<int> path = PlanRoute(map, currentSystemId, goalId);
            if (path == null || path.Count < 2) return false; // 到達不能

            // 飛び石禁止＝必ず占領してから移動：自勢力の所有星系は通り抜けられるが、経路上の最初の非所有星系で
            // 止まる（その星系へ入って占領する）。占領後に改めて命令すれば先へ進める（星系を1つずつ取る）。
            int stopIdx = FirstUnownedIndex(map, path);
            int firstHop = path[1];
            route = (stopIdx >= 2) ? path.GetRange(2, stopIdx - 1) : new List<int>();
            return BeginWarp(map, firstHop);
        }

        /// <summary>
        /// 経路を引く（#40）。まず<b>敵要塞の封鎖を避けた</b>経路を探し、無ければ封鎖を承知の最短経路へ落とす。
        ///
        /// 迂回路があるならそちらを通る＝要塞を無視して素通りする計画を立てない。迂回路が無いなら
        /// 「制圧しに行くしかない」ので、あえて封鎖回廊へ向かわせる（移動実行側が要塞の手前で足を止め、
        /// そこで力攻めが起きる）。命令そのものを拒否しないので、プレイヤーの進軍指示は常に受理される。
        /// </summary>
        private List<int> PlanRoute(GalaxyMap map, int fromId, int goalId)
        {
            List<int> detour = GalaxyPathfinder.FindPath(
                map, fromId, goalId, GalaxyPathfinder.PathQuery.AvoidingFortresses(faction));
            if (detour != null && detour.Count > 0) return detour;
            return GalaxyPathfinder.FindPath(map, fromId, goalId);
        }

        /// <summary>
        /// 飛び石移動の禁止＝<paramref name="path"/>(path[0]=起点) を歩き、<b>自勢力 owner でない最初の星系の index</b>
        /// を返す（その星系へ入って占領するため、そこで経路を打ち切る）。全て自勢力所有なら末尾 index＝目的地まで進める。
        /// </summary>
        private int FirstUnownedIndex(GalaxyMap map, List<int> path)
            => FleetOrderRules.FirstUnownedIndex(map, path, faction);

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
            if (warpingAsReinforcement) return false; // 援軍として別の戦場へ航行中＝盤面では動かさない（#38）

            // #40：要塞の封鎖状態を毎 Tick 引き直す。制圧された（守備0/所有移転）なら封鎖が解けて先へ進め、
            // 逆に落とし返されればまた止まる＝「制圧後は通行状態が更新される」を移動実行の側で保証する。
            if (map != null && onCorridor)
            {
                blockadeCap = FortressBlockadeRules.MaxAdvanceFraction(
                    map.GetCorridor(currentSystemId, destinationSystemId), faction);
            }

            if (!IsMoving) return false; // 前進中のみ進む（保持中・要塞前で足止め中・停泊中は動かない）
            traveled += CurrentSpeed * deltaTime;
            float hold = HoldDistance;
            if (traveled >= hold)
            {
                // 保持指示と要塞の封鎖の両方が解けているときだけ「到着」＝封鎖中は反対側へ抜けない。
                if (EffectiveHoldFraction >= 1f)
                {
                    // 目的地の星系に到達
                    currentSystemId = destinationSystemId;
                    onCorridor = false;
                    traveled = 0f;
                    corridorLength = 0f;
                    blockadeCap = 1f;   // 回廊を出たので封鎖の足止めは解除（次の回廊で引き直す）

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
