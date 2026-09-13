using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 支援要請の実行中に<b>敗走・総退却が起きたとき、引き受けた命令が中断されて退却へ移るか</b>を
    /// 実機で再現・記録するための QA 補助（Play 中のみ・何も保存しない）。
    ///
    /// <b>結果を QA が書かない</b>のが要点：
    /// <list type="bullet">
    ///   <item>敗走は<b>士気</b>を、総退却は<b>残存兵力</b>を動かすだけ＝<b>入力条件</b>しか触らない。
    ///         <c>manualOverride</c> の解除も <c>currentState = 撤退</c> も QA からは設定しない。</item>
    ///   <item>判断は本物の <see cref="FleetAI"/>.Update と
    ///         <see cref="BattlefieldCommandManager"/> に行わせ、その結果を<b>観測するだけ</b>。</item>
    ///   <item>誘発の前に<b>前提</b>（出どころ＝支援要請 かつ 移動中 or 手動標的あり）を確かめ、
    ///         満たしていなければ<b>未成立</b>として何もしない。</item>
    /// </list>
    ///
    /// 記録は前・直後・数フレーム後・数秒後を自動で取り、<b>有限時間で自動停止</b>する
    /// （観測を逃さず、Play を放置しても増え続けない）。
    /// </summary>
    public static class SupportEmergencyQaMenu
    {
        // ===== 記録の設定 =====
        //
        // ★時間軸は「会戦が実際に進んだぶん」だけを数える（Time.time の増分を積算する）。
        //   実時間（realtimeSinceStartup）で数えると、
        //     ・EditorUtility.DisplayDialog を開いている間（Play ループごと止まる）
        //     ・一時停止中（timeScale = 0）
        //     ・Unity を止めている間
        //   にも観測期間を使い切ってしまい、再開する前に記録が終わる（実機で発生）。
        //
        // ★倍速の扱い：Time.time は timeScale に追従するので、2倍速なら実時間の半分で
        //   同じ「会戦 10 秒ぶん」を観測して終わる。数えているのは実時間ではなく
        //   <b>盤面が進んだ量</b>なので、倍速でも観測できる会戦の長さは変わらない。

        /// <summary>観測期間＝<b>会戦が進んだ秒数</b>（実時間ではない。倍速なら実時間は短くなる）。</summary>
        private const float RecordBattleSeconds = 10f;
        /// <summary>最初のこの<b>「進んだフレーム」</b>数は毎フレーム記録する（直後の変化を逃さない）。</summary>
        private const int DenseProgressFrames = 6;
        /// <summary>その後の記録間隔＝<b>会戦が進んだ秒数</b>。</summary>
        private const float SparseInterval = 0.25f;

        /// <summary>
        /// 安全網：会戦が一向に進まなくても、実時間でこれを超えたら記録を畳む
        /// （Play を止め忘れたまま放置してもフックが残り続けない）。観測期間そのものではない。
        /// </summary>
        private const float AbsoluteRealSeconds = 600f;

        /// <summary>総退却を確実に跨ぐよう、しきい値のさらに下へ落とす割合。</summary>
        private const float RetreatMargin = 0.6f;

        // ===== 記録の状態 =====

        /// <summary>記録の進み具合。<b>未観測を合否と混同しない</b>ために状態を分けて持つ。</summary>
        private enum RecordState
        {
            /// <summary>会戦が進んでいて記録できている。</summary>
            記録中,
            /// <summary>盤面が止まっている（一時停止・ダイアログ・Play停止）＝観測期間を消費しない。</summary>
            再開待ち,
            /// <summary>観測期間ぶん会戦が進んだので締めた。</summary>
            完了,
            /// <summary>会戦が進まないまま安全網で打ち切った＝<b>判定材料にならない</b>。</summary>
            打ち切り,
        }

        private sealed class Watch
        {
            public string label;                 // 何を誘発したか
            public List<FleetStrength> fleets = new List<FleetStrength>();
            public FleetStrength focus;          // 主役（支援を引き受けた艦／直接命令の艦）
            public ManualOverrideKind kindAtStart;

            // ★時間軸（会戦の進行だけを積む）
            public float lastBattleTime;         // 直前に観測した Time.time
            public float progressSeconds;        // 会戦が進んだ累計秒
            public int progressFrames;           // 会戦が進んだ累計フレーム
            public float nextSparse;             // 次に記録する progressSeconds
            public float startRealTime;          // 安全網の基準（観測期間には使わない）

            public RecordState state = RecordState.記録中;
            public bool waitingLogged;           // 「再開待ち」を1行だけ書くための印

            public bool interruptionSeen;
            public bool retreatSeen;
            public readonly List<string> lines = new List<string>();
        }

        private static Watch watch;
        private static bool hooked;

        // ===== 誘発（入力条件だけを変える） =====

        [MenuItem("Ginei/QA: 緊急中断 支援移動中に敗走させる（Play中）", false, 370)]
        public static void RoutDuringSupportMove()
            => InduceRout(needMoving: true, needTarget: false, "支援の移動中に敗走");

        [MenuItem("Ginei/QA: 緊急中断 支援攻撃中に敗走させる（Play中）", false, 371)]
        public static void RoutDuringSupportAttack()
            => InduceRout(needMoving: false, needTarget: true, "支援の攻撃中に敗走");

        private static void InduceRout(bool needMoving, bool needTarget, string label)
        {
            if (!RequirePlaying()) return;

            FleetStrength focus = FindSupportFleet(needMoving, needTarget, out string why);
            if (focus == null) { Report("緊急中断テスト", "【未成立】" + why + "\n\n" + HowToPrepare()); return; }

            var morale = focus.GetComponent<FleetMorale>();
            if (morale == null) { Report("緊急中断テスト", "【未成立】" + focus.admiralName + " に FleetMorale がありません。"); return; }

            WarnIfPaused();
            BeginWatch(label, focus, CollectCorps(focus.corpsName));

            // ★入力条件だけを動かす：士気を 0 まで下げる（IsRouted は士気から自動で決まる）。
            //   敗走フラグや撤退状態は書かない。
            morale.ApplyMoraleDelta(-morale.morale);

            Sample("誘発（士気を0へ）");
            Report("緊急中断テスト",
                label + " を誘発しました。\n\n" +
                "対象：" + focus.admiralName + "（" + focus.corpsName + "）\n" +
                "士気を 0 にしただけです（敗走判定・命令解除・撤退状態は触っていません）。\n\n" +
                "★Space で再開してください。記録は<会戦が進んだぶん>で " + RecordBattleSeconds +
                " 秒たまるまで続きます\n（一時停止中・このダイアログ中は観測期間を消費しません）。\n" +
                "終わったら「QA: 緊急中断 記録を出力」を実行してください。");
        }

        [MenuItem("Ginei/QA: 陣形保持 保持中の艦を敗走させる（Play中）", false, 374)]
        public static void RoutFleetHoldingFormation()
        {
            if (!RequirePlaying()) return;

            // 前提：陣形を保持している艦がいること（出どころは問わない＝直接命令の対照にも使える）。
            // ★実機で対象が追撃されて撃沈し、直接移動の継続を判定できなかったため、
            //   保持中の艦が複数いるときは<b>敵から最も遠い</b>艦を選ぶ（安全な距離で敗走を観察する）。
            FleetStrength focus = null;
            float focusEnemyDist = -1f;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive) continue;
                Squadron sq = f.GetComponent<Squadron>();
                if (sq == null || !sq.IsFormationHeld) continue;

                float d = NearestHostileDistance(f);
                if (focus == null || d > focusEnemyDist) { focus = f; focusEnemyDist = d; }
            }

            if (focus == null)
            {
                Report("陣形保持テスト",
                    "【未成立】陣形を保持している艦隊がありません。\n\n" +
                    "艦隊を選んで「陣形 ▸ 円陣」などを指定してから実行してください\n" +
                    "（HUD が『円陣（保持・直接命令）』になれば成立です）。");
                return;
            }

            var morale = focus.GetComponent<FleetMorale>();
            if (morale == null) { Report("陣形保持テスト", "【未成立】FleetMorale がありません。"); return; }

            WarnIfPaused();
            Squadron squad = focus.GetComponent<Squadron>();
            var ai = focus.GetComponent<FleetAI>();
            BeginWatch("陣形保持中の艦を敗走させる", focus, CollectCorps(focus.corpsName));

            // ★入力条件だけ：士気を 0 へ。保持の解除も撤退状態も書かない。
            morale.ApplyMoraleDelta(-morale.morale);

            Sample("誘発（士気を0へ）");
            Report("陣形保持テスト",
                "対象：" + focus.admiralName + "（" + focus.corpsName + "）\n" +
                "保持していた陣形＝" + squad.FormationHold.formation +
                "（" + squad.FormationHold.source + "）\n" +
                "命令の出どころ＝" + (ai != null ? ai.OverrideKind.ToString() : "－") + "\n\n" +
                "最寄りの敵との距離＝" + (focusEnemyDist < 0f ? "敵なし" : focusEnemyDist.ToString("0.0")) + "\n\n" +
                "士気を 0 にしただけです。\n" +
                "★Space で再開してください。期待＝陣形の保持だけが解除され、\n" +
                "　直接の移動／攻撃命令は続くこと。\n" +
                (focusEnemyDist >= 0f && focusEnemyDist < SafeRoutDistance
                    ? "⚠ 敵が近いため追撃で撃沈され、移動の継続を判定できないことがあります。\n" +
                      "　いったん敵から離れた艦に陣形を保持させてから実行すると確実です。\n"
                    : "") +
                "記録は会戦が進んだぶんでたまります。「QA: 緊急中断 記録を出力」で確認してください。");
        }

        [MenuItem("Ginei/QA: 緊急中断 他軍団を総退却の兵力まで減らす（Play中）", false, 372)]
        public static void CorpsRetreatForSupport()
            => InduceCorpsRetreat(CommandAuthorityQaMenu.OtherCorps, ManualOverrideKind.支援要請,
                                  "支援の実行中に軍団が総退却");

        [MenuItem("Ginei/QA: 緊急中断 自軍団を総退却の兵力まで減らす（直接命令の対照）（Play中）", false, 373)]
        public static void CorpsRetreatForDirect()
            => InduceCorpsRetreat(CommandAuthorityQaMenu.OwnCorps, ManualOverrideKind.直接命令,
                                  "直接命令の実行中に軍団が総退却（対照）");

        private static void InduceCorpsRetreat(string corpsName, ManualOverrideKind expectKind, string label)
        {
            if (!RequirePlaying()) return;

            List<FleetStrength> corps = CollectCorps(corpsName);
            if (corps.Count == 0)
            {
                Report("緊急中断テスト", "【未成立】" + corpsName + " の生存艦隊がありません。\n\n" + HowToPrepare());
                return;
            }

            // 前提：その軍団に「実行中の命令」を持つ艦がいること。
            FleetStrength focus = null;
            for (int i = 0; i < corps.Count; i++)
            {
                var ai = corps[i].GetComponent<FleetAI>();
                if (ai == null || ai.OverrideKind != expectKind) continue;
                if (!IsOrderRunning(corps[i])) continue;
                focus = corps[i]; break;
            }
            if (focus == null)
            {
                Report("緊急中断テスト",
                    "【未成立】" + corpsName + " に「出どころ=" + expectKind +
                    " かつ 実行中（移動中 or 手動標的あり）」の艦隊がいません。\n\n" + HowToPrepare());
                return;
            }

            // 軍団旗艦（＝判断する軍団長）を探し、そのしきい値を求める。
            FleetStrength cmd = null;
            for (int i = 0; i < corps.Count; i++) if (corps[i].IsCorpsFlagship) { cmd = corps[i]; break; }
            if (cmd == null) { Report("緊急中断テスト", "【未成立】" + corpsName + " に軍団旗艦がいません。"); return; }

            AdmiralData decider = cmd.corpsCommander != null ? cmd.corpsCommander : cmd.admiralData;
            int lead = decider != null ? decider.EffectiveLeadership : 50;
            int amb = decider != null ? decider.ambition : 50;
            float threshold = CorpsRetreatRules.RetreatThreshold(lead, amb);

            // ★軍団長の士気は正常のまま（＝残存比だけで総退却させる）。
            var cmdMorale = cmd.GetComponent<FleetMorale>();
            bool cmdRouted = cmdMorale != null && cmdMorale.IsRouted;
            if (cmdRouted)
            {
                Report("緊急中断テスト",
                    "【未成立】" + corpsName + " の軍団長がすでに敗走しています。\n" +
                    "この検証は「士気は正常なまま、艦艇数の減少で総退却させる」ものです。会戦を組み直してください。");
                return;
            }

            WarnIfPaused();
            BeginWatch(label, focus, corps);

            // ★入力条件だけを動かす：残存兵力をしきい値の下へ落とす（撤退状態は書かない）。
            float target = Mathf.Clamp01(threshold * RetreatMargin);
            for (int i = 0; i < corps.Count; i++)
            {
                FleetStrength f = corps[i];
                int want = Mathf.Max(1, Mathf.RoundToInt(f.maxStrength * target));
                if (want < f.strength) f.strength = want;
            }

            Sample("誘発（残存比を " + target.ToString("0.00") + " へ／しきい値 " + threshold.ToString("0.00") + "）");
            Report("緊急中断テスト",
                label + " を誘発しました。\n\n" +
                "軍団：" + corpsName + "（" + corps.Count + " 隊）\n" +
                "軍団長：" + (decider != null ? decider.admiralName : "不明") +
                "（統率 " + lead + " / 功名心 " + amb + "）→ しきい値 " + threshold.ToString("0.00") + "\n" +
                "残存比を " + target.ToString("0.00") + " まで落としました（士気は正常のまま）。\n\n" +
                "BattlefieldCommandManager は約1秒ごとに判断します。" +
                "★Space で再開してください。記録は<会戦が進んだぶん>で " + RecordBattleSeconds +
                " 秒たまるまで続きます\n（一時停止中・このダイアログ中は観測期間を消費しません）。\n" +
                "終わったら「QA: 緊急中断 記録を出力」を実行してください。");
        }

        // ===== 記録 =====

        [MenuItem("Ginei/QA: 緊急中断 記録を出力（Play中）", false, 380)]
        public static void DumpRecord()
        {
            if (watch == null) { Report("緊急中断テスト", "記録がありません。先に誘発コマンドを実行してください。"); return; }

            // ★観測できたかどうかと、合否は別物として書く。
            //   会戦が十分に進んでいないうちは「起きなかった」ではなく「まだ見ていない」。
            bool enoughProgress = watch.state == RecordState.完了;
            string interruption = watch.interruptionSeen ? "観測あり"
                                : enoughProgress ? "観測なし（会戦は進んだ）" : "未観測（会戦がまだ進んでいない）";
            string retreat = watch.retreatSeen ? "観測あり"
                                : enoughProgress ? "観測なし（会戦は進んだ）" : "未観測（会戦がまだ進んでいない）";

            var sb = new StringBuilder();
            sb.Append("■ ").Append(watch.label).Append('\n');
            sb.Append("主役：").Append(watch.focus != null ? watch.focus.admiralName : "（消滅）")
              .Append("　誘発時の出どころ=").Append(watch.kindAtStart).Append('\n');
            sb.Append("記録の状態：").Append(watch.state)
              .Append("　会戦の進行=").Append(watch.progressSeconds.ToString("0.00")).Append(" 秒 / ")
              .Append(RecordBattleSeconds).Append(" 秒")
              .Append("（進行フレーム ").Append(watch.progressFrames).Append("）\n");
            sb.Append("　※この秒数は<会戦が進んだぶん>です。一時停止・ダイアログ・Play停止の待ち時間は含みません。\n");
            sb.Append("　※倍速でも観測できる会戦の長さは同じです（実時間だけが短くなります）。\n");
            sb.Append("観測：中断=").Append(interruption).Append("　撤退状態=").Append(retreat).Append('\n');

            sb.Append("判定の目安：\n");
            if (watch.kindAtStart == ManualOverrideKind.支援要請)
                sb.Append("  支援要請 → 中断あり＋撤退状態あり＋出どころが なし へ落ちる のが期待\n");
            else
                sb.Append("  直接命令 → 中断なし（命令が続く）のが期待＝既存動作の維持\n");

            if (!enoughProgress)
            {
                sb.Append("★この記録だけで合否を決めないでください。")
                  .Append(watch.state == RecordState.再開待ち
                          ? "盤面が止まったままです。Space で再開すると続きを記録します。\n"
                          : "観測期間ぶん会戦が進む前に記録が終わっています。\n");
            }
            sb.Append('\n');

            for (int i = 0; i < watch.lines.Count; i++) sb.Append(watch.lines[i]).Append('\n');

            string msg = sb.ToString();
            Debug.Log("[緊急中断QA]\n" + msg);
            // ダイアログは長文で切れるので、要約だけ出して詳細は Console へ誘導する。
            Report("緊急中断テスト（記録）",
                watch.label + "\n\n" +
                "記録の状態：" + watch.state +
                "（会戦の進行 " + watch.progressSeconds.ToString("0.00") + " / " + RecordBattleSeconds + " 秒）\n" +
                "中断=" + interruption + "\n" +
                "撤退状態=" + retreat + "\n" +
                "記録 " + watch.lines.Count + " 行\n\n" +
                (enoughProgress ? "" : "★まだ判定できません。Space で再開して会戦を進めてください。\n\n") +
                "全文は Console の [緊急中断QA] に出しました。");
        }

        [MenuItem("Ginei/QA: 緊急中断 記録を消す（Play中）", false, 381)]
        public static void ClearRecord()
        {
            StopWatch();
            watch = null;
            Report("緊急中断テスト", "記録を消しました。");
        }

        private static void BeginWatch(string label, FleetStrength focus, List<FleetStrength> corps)
        {
            StopWatch();
            var ai = focus.GetComponent<FleetAI>();
            watch = new Watch
            {
                label = label,
                focus = focus,
                kindAtStart = ai != null ? ai.OverrideKind : ManualOverrideKind.なし,
                // ★時間軸の起点は「会戦の時刻」。実時間は安全網にしか使わない。
                lastBattleTime = Time.time,
                startRealTime = Time.realtimeSinceStartup,
                nextSparse = 0f,
                state = RecordState.記録中,
            };
            watch.fleets.AddRange(corps);
            if (!watch.fleets.Contains(focus)) watch.fleets.Add(focus);

            Sample("誘発前");
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            hooked = true;
        }

        private static void StopWatch()
        {
            if (!hooked) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            hooked = false;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                StopWatch();
        }

        private static void Tick()
        {
            if (watch == null) { StopWatch(); return; }
            if (!Application.isPlaying)
            {
                // Play を抜けた＝これ以上会戦は進まない。ここまでを「打ち切り」として畳む。
                Finish(RecordState.打ち切り, "Play が終了しました");
                return;
            }

            float now = Time.time;

            // ★会戦が進んでいない（一時停止・ダイアログ・停止中）＝観測期間を消費しない。
            if (now <= watch.lastBattleTime)
            {
                if (watch.state != RecordState.再開待ち)
                {
                    watch.state = RecordState.再開待ち;
                    if (!watch.waitingLogged)
                    {
                        watch.waitingLogged = true;
                        watch.lines.Add("[再開待ち] 盤面が止まっています（timeScale=" +
                            Time.timeScale.ToString("0.0") + "）。" +
                            "観測期間は消費していません。Space で再開すると記録を続けます。");
                    }
                }

                // 安全網：会戦が進まないまま長時間放置されたらフックを畳む（判定材料にはしない）。
                if (Time.realtimeSinceStartup - watch.startRealTime > AbsoluteRealSeconds)
                    Finish(RecordState.打ち切り, "会戦が進まないまま実時間 " + AbsoluteRealSeconds + " 秒を超えました");
                return;
            }

            // ここから先は「会戦が進んだ」フレーム。
            watch.progressSeconds += now - watch.lastBattleTime;
            watch.lastBattleTime = now;
            watch.progressFrames++;
            watch.state = RecordState.記録中;

            if (watch.progressSeconds > RecordBattleSeconds)
            {
                Finish(RecordState.完了, "会戦が " + RecordBattleSeconds + " 秒ぶん進みました");
                return;
            }

            // 最初の数フレームは毎フレーム（＝進んだフレームだけを数える）。
            if (watch.progressFrames <= DenseProgressFrames)
            {
                Sample("+" + watch.progressFrames + "F（進行）");
                return;
            }

            if (watch.progressSeconds >= watch.nextSparse)
            {
                watch.nextSparse = watch.progressSeconds + SparseInterval;
                Sample("+" + watch.progressSeconds.ToString("0.00") + "s（会戦時間）");
            }
        }

        /// <summary>記録を締める（状態を残す＝あとで「未観測」と「観測して起きなかった」を区別する）。</summary>
        private static void Finish(RecordState state, string why)
        {
            if (watch == null) { StopWatch(); return; }
            watch.state = state;
            if (Application.isPlaying) Sample("記録終了（" + why + "）");
            else watch.lines.Add("[記録終了] " + why);
            StopWatch();
            Debug.Log("[緊急中断QA] 記録を終了しました（" + state + "：" + why + "）。" +
                      "「QA: 緊急中断 記録を出力」で全文を出せます。");
        }

        /// <summary>いまの状態を1件記録する（★読み取りだけ・何も変えない）。</summary>
        private static void Sample(string tag)
        {
            if (watch == null) return;

            var sb = new StringBuilder();
            sb.Append("[").Append(tag).Append("] t=").Append(Time.time.ToString("0.00"))
              .Append(" F=").Append(Time.frameCount)
              .Append(" timeScale=").Append(Time.timeScale.ToString("0.0"));
            if (TimeDisplay.TryFormatNow(out string clock, out _))
                sb.Append(" 暦=").Append(clock.Replace('\n', ' '));
            sb.Append('\n');

            for (int i = 0; i < watch.fleets.Count; i++)
            {
                FleetStrength f = watch.fleets[i];
                if (f == null) { sb.Append("    （消滅した艦隊）\n"); continue; }

                var ai = f.GetComponent<FleetAI>();
                var mv = f.GetComponent<FleetMovement>();
                var mo = f.GetComponent<FleetMorale>();
                var wp = f.GetComponent<FleetWeapon>();

                float ratio = f.maxStrength > 0 ? (float)f.strength / f.maxStrength : 0f;

                sb.Append(f == watch.focus ? "  ★ " : "    ");
                sb.Append(f.admiralName).Append('/').Append(f.corpsName)
                  .Append(" 生存=").Append(f.IsAlive)
                  .Append(" 士気=").Append(mo != null ? mo.morale.ToString("0") : "-")
                  .Append(mo != null && mo.IsRouted ? "(敗走)" : "")
                  .Append(" 残存比=").Append(ratio.ToString("0.00"))
                  .Append(" override=").Append(ai != null ? ai.OverrideKind.ToString() : "-")
                  .Append(" 手動標的=").Append(wp != null ? wp.HasManualTarget.ToString() : "-")
                  .Append(" AI=").Append(ai != null ? ai.currentState.ToString() : "-")
                  .Append(" 座標=").Append(((Vector2)f.transform.position).ToString("0.0"))
                  .Append(" 移動中=").Append(mv != null ? mv.IsMoving.ToString() : "-");
                if (mv != null && mv.IsMoving) sb.Append(" 移動先=").Append(mv.Destination.ToString("0.0"));
                sb.Append('\n');

                if (f != watch.focus || ai == null) continue;

                // 主役の変化を拾う（判定はしない＝観測の記録）。
                if (!watch.interruptionSeen
                    && watch.kindAtStart == ManualOverrideKind.支援要請
                    && ai.OverrideKind == ManualOverrideKind.なし)
                {
                    watch.interruptionSeen = true;
                    sb.Append("      → ここで支援の命令が中断されました（出どころが なし へ）\n");
                }
                if (!watch.retreatSeen && ai.currentState == FleetAI.AIState.撤退)
                {
                    watch.retreatSeen = true;
                    sb.Append("      → ここで撤退状態に入りました\n");
                }
            }

            watch.lines.Add(sb.ToString().TrimEnd('\n'));
        }

        // ===== 補助 =====

        private static bool RequirePlaying()
        {
            if (Application.isPlaying) return true;
            EditorUtility.DisplayDialog("緊急中断テスト", "Play 中に実行してください。", "OK");
            return false;
        }

        private static void WarnIfPaused()
        {
            if (Time.timeScale > 0f) return;
            Debug.LogWarning("[緊急中断QA] 一時停止中（timeScale=0）です。" +
                             "このままだと Update が進まず何も起きません。Space で再開してください。");
        }

        /// <summary>この距離より敵が近いと、敗走させた艦が追撃で沈んで観察できなくなりやすい。</summary>
        private const float SafeRoutDistance = 25f;

        /// <summary>最寄りの敵対艦隊までの距離（敵がいなければ -1）。</summary>
        private static float NearestHostileDistance(FleetStrength f)
        {
            if (f == null) return -1f;
            float best = -1f;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength e = all[i];
                if (e == null || e == f || !e.IsAlive) continue;
                if (!FactionRelations.IsHostile(f, e)) continue;
                float d = Vector2.Distance(f.transform.position, e.transform.position);
                if (best < 0f || d < best) best = d;
            }
            return best;
        }

        /// <summary>命令を実行中か（移動中 または 手動標的あり）。</summary>
        private static bool IsOrderRunning(FleetStrength f)
        {
            var mv = f.GetComponent<FleetMovement>();
            var wp = f.GetComponent<FleetWeapon>();
            return (mv != null && mv.IsMoving) || (wp != null && wp.HasManualTarget);
        }

        /// <summary>支援要請を引き受けて実行中の艦を探す（前提の確認込み）。</summary>
        private static FleetStrength FindSupportFleet(bool needMoving, bool needTarget, out string why)
        {
            why = "";
            List<FleetStrength> corps = CollectCorps(CommandAuthorityQaMenu.OtherCorps);
            if (corps.Count == 0) { why = CommandAuthorityQaMenu.OtherCorps + " の生存艦隊がありません。"; return null; }

            bool anySupport = false;
            for (int i = 0; i < corps.Count; i++)
            {
                FleetStrength f = corps[i];
                var ai = f.GetComponent<FleetAI>();
                if (ai == null || ai.OverrideKind != ManualOverrideKind.支援要請) continue;
                anySupport = true;

                var mv = f.GetComponent<FleetMovement>();
                var wp = f.GetComponent<FleetWeapon>();
                if (needMoving && (mv == null || !mv.IsMoving)) continue;
                if (needTarget && (wp == null || !wp.HasManualTarget)) continue;
                return f;
            }

            why = anySupport
                ? (needMoving ? "支援を引き受けた艦はいますが、まだ移動中ではありません。"
                              : "支援を引き受けた艦はいますが、手動の攻撃目標を持っていません。")
                : "出どころ＝支援要請 の艦隊がいません（要請が承諾されていません）。";
            return null;
        }

        /// <summary>同じ軍団の生存艦隊（同一戦場）。</summary>
        private static List<FleetStrength> CollectCorps(string corpsName)
        {
            var result = new List<FleetStrength>();
            if (string.IsNullOrEmpty(corpsName)) return result;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive || f.corpsName != corpsName) continue;
                result.Add(f);
            }
            return result;
        }

        private static string HowToPrepare()
        {
            return
                "【準備】\n" +
                "1. Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）\n" +
                "2. Ginei/QA: 支援要請 他軍団を協力的にする（承諾の再現）\n" +
                "3. Space で再開\n" +
                "4-移動: QA他軍1 を選んで右クリックで遠くへ移動 →「応じました」を待つ\n" +
                "4-攻撃: QA他軍1 を選んで QA敵1 へ攻撃を要請 →「応じました」を待つ\n" +
                "5. 対照（直接命令）は QA自軍1 を選んで通常どおり移動命令を出す\n\n" +
                "承諾されると出どころが 支援要請 になります" +
                "（Ginei/QA: 支援要請 対象艦隊の命令状態を出力 で確認できます）。";
        }

        private static void Report(string title, string message)
            => EditorUtility.DisplayDialog(title, message, "OK");
    }
}
