using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 指揮系統外への<b>支援要請</b>を実際に往復させる（GitHub #67・会戦シーンごとに1つ）。
    ///
    /// 通知を出して終わりにしない：要請を受理し、相手の指揮官が game-時間で判断し、
    /// <b>応じたらその命令を実際に実行し</b>、断られた／流れたらそう返す。
    /// 同じ部隊への同種の要請は1件だけ（重ねて出しても増えない＝二重に実行しない）。
    /// </summary>
    public class SupportRequestDirector : MonoBehaviour
    {
        [Tooltip("相手が返事をするまでの秒数（game-time）")]
        public float replySeconds = 4f;
        [Tooltip("返事が来ないまま流れるまでの秒数")]
        public float expireSeconds = 12f;

        private SupportRequestParams Params
            => new SupportRequestParams(replySeconds, expireSeconds,
                                        SupportRequestParams.Default.acceptThreshold);

        /// <summary>受理した要請1件。命令の中身も持つ（承諾されたらそのまま実行する）。</summary>
        private sealed class Request
        {
            public Selectable target;
            public FleetStrength targetFleet;
            public SupportOrderKind kind;
            public float startTime;          // game-time
            public bool done;

            /// <summary>実行結果（★未実行以外になったら二度と実行しない＝二重実行の防止）。</summary>
            public SupportOrderExecution execution = SupportOrderExecution.未実行;

            // 命令の中身（種類ごとに使うものだけ）
            public Vector2 position;
            public Squadron attackTarget;
            public int formationIndex;
        }

        private readonly List<Request> requests = new List<Request>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            if (FindInScene(scene) != null) return;
            var go = new GameObject("SupportRequestDirector");
            if (scene.IsValid() && scene != SceneManager.GetActiveScene())
                SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<SupportRequestDirector>();
        }

        /// <summary>その会戦シーンの司令塔（無ければ null）。</summary>
        public static SupportRequestDirector FindInScene(Scene scene)
        {
            SupportRequestDirector[] all = FindObjectsByType<SupportRequestDirector>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene == scene) return all[i];
            return null;
        }

        // ===== 要請を出す =====

        /// <summary>移動を要請する。</summary>
        public void RequestMove(Selectable target, Vector2 position)
            => Enqueue(target, SupportOrderKind.移動, r => r.position = position);

        /// <summary>攻撃を要請する。</summary>
        public void RequestAttack(Selectable target, Squadron attackTarget)
            => Enqueue(target, SupportOrderKind.攻撃, r => r.attackTarget = attackTarget);

        /// <summary>陣形変更を要請する。</summary>
        public void RequestFormation(Selectable target, int formationIndex)
            => Enqueue(target, SupportOrderKind.陣形変更, r => r.formationIndex = formationIndex);

        private void Enqueue(Selectable target, SupportOrderKind kind, System.Action<Request> fill)
        {
            FleetStrength fs = target != null ? target.GetComponent<FleetStrength>() : null;
            if (fs == null || !fs.IsAlive) return;

            // ★同じ部隊への同種の要請は1件だけ（重ねても増えない＝二重実行しない）。
            for (int i = 0; i < requests.Count; i++)
            {
                Request r = requests[i];
                if (r.done || r.target != target || r.kind != kind) continue;
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                    SupportRequestRules.DuplicateText(fs.admiralName, kind));
                return;
            }

            var req = new Request
            {
                target = target, targetFleet = fs, kind = kind, startTime = Now(),
            };
            fill?.Invoke(req);

            // ★出す前に命令の対象を見る。すでに目標が失われているなら要請自体を受理しない
            //   （「要請しました」と出してから黙って流れる、を作らない）。
            if (!OrderTargetValid(req))
            {
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                    SupportOrderExecutionRules.CannotRequestText(fs.admiralName, kind));
                return;
            }

            requests.Add(req);

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                SupportRequestRules.SentText(fs.admiralName, kind));
        }

        // ===== 相手の判断と実行 =====

        private void Update()
        {
            if (requests.Count == 0) return;
            float now = Now();

            for (int i = requests.Count - 1; i >= 0; i--)
            {
                Request r = requests[i];
                if (r.done) { requests.RemoveAt(i); continue; }

                bool alive = r.targetFleet != null && r.targetFleet.IsAlive;
                SupportRequestOutcome outcome = SupportRequestRules.Judge(
                    alive, OrderTargetValid(r), now - r.startTime, Willingness(r.targetFleet), Params);
                if (outcome == SupportRequestOutcome.検討中) continue;

                r.done = true;
                requests.RemoveAt(i);

                // ★先に実行してから知らせる（実行できていないのに「応じました」を出さない）。
                //   実行は1件につき1回だけ（CanExecute が未実行のときしか通さない）。
                if (SupportOrderExecutionRules.CanExecute(r.execution, outcome) && alive)
                {
                    r.execution = Execute(r);

                    // ★実際に実行できたときだけ AI 操舵を譲らせる（＝要請した命令が上書きされない）。
                    //   どの命令で必要かは Core の窓口が決める（ここで条件を書き分けない）。
                    if (SupportOrderExecutionRules.IsCarriedOut(r.execution)
                        && SupportOrderExecutionRules.RequiresManualSteering(r.kind))
                        BeginManualOverride(r.target);   // ★支援要請として立てる（緊急退却で中断できる）
                }

                NotificationCenter.Push(NotificationCategory.戦闘,
                    SupportOrderExecutionRules.IsNoteworthy(outcome, r.execution)
                        ? NotificationSeverity.注意 : NotificationSeverity.情報,
                    SupportOrderExecutionRules.ResultText(outcome, r.execution,
                        r.targetFleet != null ? r.targetFleet.admiralName : "", r.kind));
            }
        }

        /// <summary>
        /// 要請した<b>命令の対象</b>がまだ有効か（攻撃目標が生きているか）。
        /// 移動・陣形変更は対象が座標／陣形なので常に有効。
        /// </summary>
        private static bool OrderTargetValid(Request r)
        {
            if (r.kind != SupportOrderKind.攻撃) return true;
            if (r.attackTarget == null) return false;          // 破棄済み（Unity の null 判定を通す）
            FleetStrength fs = r.attackTarget.GetComponent<FleetStrength>();
            return fs != null && fs.IsAlive;
        }

        /// <summary>相手が応じる気（統率・士気・自分が交戦中か）。決定論。</summary>
        private static float Willingness(FleetStrength fs)
        {
            if (fs == null) return 0f;
            int leadership = fs.admiralData != null ? fs.admiralData.EffectiveLeadership : 50;
            FleetMorale morale = fs.GetComponent<FleetMorale>();
            float morale01 = morale != null ? morale.GetMoraleFactor() : 1f;
            FleetWeapon weapon = fs.GetComponent<FleetWeapon>();
            bool busy = weapon != null && weapon.IsInCombat;
            return SupportRequestRules.Willingness(leadership, morale01, busy);
        }

        /// <summary>
        /// 要請どおりに動く（応じた相手が自分で実行する形）。
        /// <b>実行できたかを返す</b>＝呼び手はこれを見て通知の文面と重要度を決める
        /// （何もできなかったときに「応じました」で終わらせない）。
        /// </summary>
        private static SupportOrderExecution Execute(Request r)
        {
            if (r.target == null) return SupportOrderExecution.対象消失;

            switch (r.kind)
            {
                case SupportOrderKind.移動:
                    {
                        FleetMovement mv = r.target.GetComponent<FleetMovement>();
                        if (mv == null) return SupportOrderExecution.手段なし;   // 動く機能が無い（要塞など）
                        FleetStandardOrder order = r.target.GetComponent<FleetStandardOrder>();
                        if (order != null) order.ClearOrder();
                        mv.SetDestination(r.position, null);
                        return SupportOrderExecution.実行;
                    }
                case SupportOrderKind.攻撃:
                    {
                        // 返事を待つあいだに目標が沈むことがある＝そのときは実行しない。
                        if (!OrderTargetValid(r)) return SupportOrderExecution.対象消失;
                        FleetWeapon weapon = r.target.GetComponent<FleetWeapon>();
                        if (weapon == null) return SupportOrderExecution.手段なし;   // 撃つ機能が無い
                        weapon.SetManualTargetFleet(r.attackTarget);
                        return SupportOrderExecution.実行;
                    }
                case SupportOrderKind.陣形変更:
                    {
                        Squadron squad = r.target.GetComponent<Squadron>();
                        if (squad == null) return SupportOrderExecution.手段なし;    // 編制が無い
                        // ★承諾された要請＝明示指定として受理し、以後 AI は上書きしない（確定仕様1）。
                        //   BeginManualOverride は呼ばない：陣形の保持は移動・攻撃と独立で、
                        //   足を止める必要がないため（保持は Squadron 側が持つ）。
                        //   断られたら理由を残す（旧指定と保持はそのまま＝消費なし）。
                        FormationOrderResult fr =
                            squad.RequestFormation((Formation)r.formationIndex, FormationOrderSource.支援要請);
                        if (fr == FormationOrderResult.受理) return SupportOrderExecution.実行;
                        NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                            FleetFormationOrderRules.ResultText(fr, (Formation)r.formationIndex,
                                r.targetFleet != null ? r.targetFleet.admiralName : ""));
                        return SupportOrderExecution.実行不可;
                    }
            }
            return SupportOrderExecution.手段なし;
        }

        /// <summary>
        /// 承諾した命令のあいだ AI 操舵を譲らせる（<see cref="FleetCommander"/> の直接命令と同じ扱い）。
        ///
        /// これが無いと <see cref="FleetAI.Update"/> が次のフレームから
        /// <c>UpdateStateBehavior</c> で自分の判断の行き先を <c>SetDestination</c> し、
        /// <b>要請した移動先が即座に上書きされる</b>（実機で「承諾したのに指定先へ行かない」に見える）。
        ///
        /// <b>解除は自動</b>：移動が終わり手動標的も標準命令も無くなった時点で
        /// <see cref="FleetAI"/> 自身が <c>manualOverride</c> を落として AI へ復帰する
        /// ＝ここで復帰処理を持たない（二重実装しない）。
        /// </summary>
        private static void BeginManualOverride(Selectable sel)
        {
            if (sel == null) return;
            FleetAI ai = sel.GetComponent<FleetAI>();
            // ★出どころを 支援要請 にする＝敗走・総退却では中断して退がれる
            //   （直接命令は従来どおり中断しない。ここを 直接命令 にすると、
            //    要請を承諾したせいで退却できなくなる回帰になる）。
            if (ai != null) ai.BeginManualOverride(ManualOverrideKind.支援要請);
        }

        /// <summary>game-time（ポーズで止まり倍速で進む）。</summary>
        private static float Now()
        {
            GameClock clock = StrategySession.Clock;
            return clock != null ? (float)clock.ElapsedSeconds : Time.time;
        }
    }
}
