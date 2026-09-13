using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 援軍（ワープイン・#38 C-5）の台帳。戦略側で派遣を登録し、統一 game-時間の進行にあわせて
    /// 「到着した派遣」を戦場ごとに取り出す。<b>唯一の窓口</b>＝並行する到着管理を各所に作らない。
    ///
    /// 設計の要（不変条件・テストで固定）：
    /// ・時間は<b>絶対 game-秒</b>で持つ（<see cref="WarpReinforcement.arrivalTime"/>）＝倍速・分割 dt・ポーズで到着時刻がずれない。
    /// ・<see cref="Advance"/> は正の経過時間だけ進む＝<b>ポーズ中(dt=0)は到着しない</b>。
    /// ・取り出した派遣は台帳から消える＝<b>同じ援軍が二度出現しない</b>。
    /// ・取り出しは <see cref="BattlefieldKey"/> で厳密に絞る＝<b>別戦場へ混入しない</b>。
    /// ・決着した戦場は <see cref="CloseBattlefield"/> で閉じ、未参戦の派遣を呼び手へ<b>差し戻す</b>
    ///   （＝戦略側で艦隊を盤面に戻す）。閉じた戦場への新規派遣は受理しない。
    ///
    /// 純データ＋純ロジック（非 MonoBehaviour・非 static）＝Game 層が1インスタンス保持し、
    /// <see cref="GameClock"/> の累積秒を <see cref="SyncTo"/> で写して回す想定。乱数なし決定論。
    /// </summary>
    public class WarpReinforcementLedger
    {
        /// <summary>到着予定順（arrivalTime→id）に整列した未参戦の派遣。</summary>
        private readonly List<WarpReinforcement> pending = new List<WarpReinforcement>();

        /// <summary>決着して閉じた戦場（以後は参戦させない）。</summary>
        private readonly HashSet<BattlefieldKey> closed = new HashSet<BattlefieldKey>();

        private long nextId = 1L;

        /// <summary>台帳が把握している現在の game-秒（絶対時刻）。</summary>
        public double Elapsed { get; private set; }

        /// <summary>未参戦（航行中＋到着済み未取り出し）の派遣件数。</summary>
        public int PendingCount => pending.Count;

        /// <summary>閉じている戦場の数。</summary>
        public int ClosedCount => closed.Count;

        // ===== 時間進行 =====

        /// <summary>
        /// game-時間を <paramref name="deltaSeconds"/> 秒進める。<b>0以下は進まない</b>（ポーズ dt=0・巻き戻し禁止）。
        /// 倍速は呼び手（<see cref="GameClock"/>）が実時間へ speed を掛けた game-秒を渡す＝ここでは倍率を持たない。
        /// </summary>
        public void Advance(double deltaSeconds)
        {
            if (deltaSeconds <= 0.0) return;
            Elapsed += deltaSeconds;
        }

        /// <summary>
        /// 統一クロックの累積 game-秒へ同期する（<c>SyncTo(StrategySession.Clock.ElapsedSeconds)</c>）。
        /// 差分の積み上げ誤差が出ない＝<b>倍速でも到着 game-時刻がずれない</b>推奨経路。過去へは戻さない。
        /// </summary>
        public void SyncTo(double elapsedGameSeconds)
        {
            if (elapsedGameSeconds > Elapsed) Elapsed = elapsedGameSeconds;
        }

        // ===== 派遣の登録 =====

        /// <summary>
        /// 所要時間（game-秒）を指定して派遣を登録する。採番したID（1以上）を返す。
        /// 戦場キーが無効／閉じている／兵力0以下なら受理せず 0 を返す（呼び手は艦隊を戦略に留める）。
        /// </summary>
        public long Dispatch(BattlefieldKey battlefield, Faction faction, int fleetId, int strength, float travelSeconds)
            => DispatchAt(battlefield, faction, fleetId, strength,
                          WarpReinforcementRules.ArrivalTime(Elapsed, travelSeconds));

        /// <summary>
        /// 到着する絶対 game-秒を指定して派遣を登録する（セーブ復元・到着時刻を直接決める経路）。
        /// 過去の到着時刻は現在時刻へ丸める（＝次の取り出しで即参戦）。
        /// </summary>
        public long DispatchAt(BattlefieldKey battlefield, Faction faction, int fleetId, int strength, double arrivalTime)
        {
            if (!battlefield.IsValid) return 0L;
            if (closed.Contains(battlefield)) return 0L;   // 決着済みの戦場へは送らない
            if (strength <= 0) return 0L;
            if (double.IsPositiveInfinity(arrivalTime)) return 0L; // 到達不能

            var order = new WarpReinforcement
            {
                id = nextId++,
                battlefield = battlefield,
                faction = faction,
                fleetId = fleetId,
                strength = strength,
                dispatchTime = Elapsed,
                arrivalTime = arrivalTime < Elapsed ? Elapsed : arrivalTime,
            };
            InsertSorted(order);
            return order.id;
        }

        /// <summary>到着予定順（arrivalTime→id）を保って挿入する＝取り出し順が決定論になる。</summary>
        private void InsertSorted(WarpReinforcement order)
        {
            int i = pending.Count;
            while (i > 0)
            {
                WarpReinforcement prev = pending[i - 1];
                bool later = prev.arrivalTime > order.arrivalTime
                             || (prev.arrivalTime == order.arrivalTime && prev.id > order.id);
                if (!later) break;
                i--;
            }
            pending.Insert(i, order);
        }

        // ===== 到着の取り出し =====

        /// <summary>
        /// 指定戦場の<b>到着済み</b>の派遣を取り出して台帳から消す（＝二度取り出せない＝二重出現しない）。
        /// 取り出した件数を返す。戦場が閉じている場合は何も取り出さず 0（差し戻しは <see cref="CloseBattlefield"/>）。
        /// <paramref name="arrived"/> は呼び手が使い回すバッファ（null 可＝件数だけ数えて消す用途）。中身はクリアしない。
        /// </summary>
        public int TakeArrived(BattlefieldKey battlefield, List<WarpReinforcement> arrived)
        {
            if (!battlefield.IsValid || closed.Contains(battlefield)) return 0;

            int taken = 0;
            for (int i = 0; i < pending.Count; )
            {
                WarpReinforcement o = pending[i];
                if (o.battlefield != battlefield || !WarpReinforcementRules.HasArrived(o.arrivalTime, Elapsed))
                {
                    i++;
                    continue;
                }
                arrived?.Add(o);
                pending.RemoveAt(i);
                taken++;
            }
            return taken;
        }

        // ===== 戦場の開閉（決着後の差し戻し）=====

        /// <summary>
        /// 戦場を閉じる（会戦の決着時に呼ぶ）。その戦場宛の<b>未参戦の派遣をすべて差し戻し</b>、
        /// <paramref name="diverted"/> へ積んで台帳から消す（呼び手が戦略盤面へ艦隊を戻す）。差し戻した件数を返す。
        /// 以後この戦場への <see cref="Dispatch"/> は受理されない＝<b>戦闘終了後に到着しても参戦しない</b>。
        /// </summary>
        public int CloseBattlefield(BattlefieldKey battlefield, List<WarpReinforcement> diverted)
        {
            if (!battlefield.IsValid) return 0;
            closed.Add(battlefield);

            int count = 0;
            for (int i = 0; i < pending.Count; )
            {
                if (pending[i].battlefield != battlefield) { i++; continue; }
                diverted?.Add(pending[i]);
                pending.RemoveAt(i);
                count++;
            }
            return count;
        }

        /// <summary>この戦場は決着済み（閉じている）か。</summary>
        public bool IsClosed(BattlefieldKey battlefield) => battlefield.IsValid && closed.Contains(battlefield);

        /// <summary>同じ回廊で改めて会戦が起きたときに戦場を開き直す（閉じていなければ false）。</summary>
        public bool ReopenBattlefield(BattlefieldKey battlefield)
            => battlefield.IsValid && closed.Remove(battlefield);

        // ===== 照会（UI の「到着予定」表示用・状態を変えない）=====

        /// <summary>
        /// 指定戦場の未参戦の派遣を到着予定順に列挙する（台帳は変えない）。件数を返す。
        /// <paramref name="outList"/> はクリアしない（呼び手が使い回す）。
        /// </summary>
        public int PeekPending(BattlefieldKey battlefield, List<WarpReinforcement> outList)
        {
            if (!battlefield.IsValid) return 0;
            int n = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].battlefield != battlefield) continue;
                outList?.Add(pending[i]);
                n++;
            }
            return n;
        }

        /// <summary>勢力で絞った版（自軍の到着予定だけ HUD に出す）。</summary>
        public int PeekPending(BattlefieldKey battlefield, Faction faction, List<WarpReinforcement> outList)
        {
            if (!battlefield.IsValid) return 0;
            int n = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].battlefield != battlefield || pending[i].faction != faction) continue;
                outList?.Add(pending[i]);
                n++;
            }
            return n;
        }

        /// <summary>
        /// 戦場を問わず未参戦の派遣を<b>全部</b>到着予定順に列挙する（台帳は変えない）。件数を返す。
        /// セーブ（<see cref="CampaignSerializer"/>）が航行中の援軍を書き出すのに使う
        /// ＝これが無いとロードで航行中の援軍が消える。<paramref name="outList"/> はクリアしない。
        /// </summary>
        public int PeekAll(List<WarpReinforcement> outList)
        {
            for (int i = 0; i < pending.Count; i++) outList?.Add(pending[i]);
            return pending.Count;
        }

        /// <summary>IDで派遣を引く（見つからなければ false）。</summary>
        public bool TryGet(long id, out WarpReinforcement order)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].id != id) continue;
                order = pending[i];
                return true;
            }
            order = default;
            return false;
        }

        /// <summary>指定派遣の到着までの残り game-秒（見つからなければ -1）。</summary>
        public float RemainingSeconds(long id)
            => TryGet(id, out WarpReinforcement o)
                ? WarpReinforcementRules.RemainingSeconds(o.arrivalTime, Elapsed)
                : -1f;

        /// <summary>指定戦場で次に到着する予定の絶対 game-秒（予定が無ければ -1）。</summary>
        public double NextArrivalTime(BattlefieldKey battlefield)
        {
            if (!battlefield.IsValid) return -1.0;
            for (int i = 0; i < pending.Count; i++)   // 到着予定順に整列済み＝最初の一致が最短
                if (pending[i].battlefield == battlefield) return pending[i].arrivalTime;
            return -1.0;
        }

        // ===== 取り消し・全消去 =====

        /// <summary>派遣を取り消す（到着前に呼び戻す）。取り消せたら true＋<paramref name="order"/> を返す。</summary>
        public bool Cancel(long id, out WarpReinforcement order)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].id != id) continue;
                order = pending[i];
                pending.RemoveAt(i);
                return true;
            }
            order = default;
            return false;
        }

        /// <summary>台帳を空にする（戦役の作り直し・テスト用）。採番と経過時間もリセットする。</summary>
        public void Clear()
        {
            pending.Clear();
            closed.Clear();
            nextId = 1L;
            Elapsed = 0.0;
        }
    }
}
