using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 指揮権限（GitHub #67）の実機QA用の<b>安定した検証入口</b>（第5次・依頼A）。
    /// <b>Play 中のみ動作し、何も保存しない</b>（シーン/プレハブ/アセット/セーブに書かない）。
    ///
    /// 既存の <see cref="CorpsFormationQaMenu"/> は「軍団隊形が2軍団で独立して効くか」を見るためのもので、
    /// 起動→生成→仕込む→発令 の各段で<b>戦闘が進んで部隊が失われ</b>、仕込みが間に合わないことがあった。
    /// また戦役の指揮系統を明示していないため<b>自由操作の合格を戦役の合格にできない</b>。
    /// ここはその2点を直した検証専用の入口：
    ///
    /// <list type="bullet">
    ///   <item><b>1コマンドで最後まで組む</b>（会戦開始→艦隊が湧く→軍団の割り当て まで自動）。</item>
    ///   <item>組み終わるまで <c>Time.timeScale = 0</c>＝<b>準備中に部隊が失われない</b>。準備完了で一時停止のまま止まる。</item>
    ///   <item><b>戦役モード</b>（<see cref="BattleCommandMode.戦役"/>）で組む＝
    ///         <see cref="BattleHandoff.FromCampaign"/>=true・<see cref="BattleHandoff.PlayerCommandsWholeFleet"/>=false・
    ///         <see cref="BattleHandoff.PlayerCorpsName"/>=自軍団。<b>自由操作の権限免除は使わない</b>。</item>
    ///   <item>自軍団／同陣営の他軍団／敵軍団を<b>名前で見分けられる</b>ようにする
    ///         （Hierarchy のオブジェクト名と提督名の両方）。</item>
    /// </list>
    ///
    /// <b>ここは判定を代行しない</b>：合否は通常UI（クリック選択・右クリックメニュー）で出す。
    /// このメニューが提供するのは「盤面の仕込み」と「読み取り専用の状態出力」だけで、
    /// Editor 特権で命令を通す入口は<b>作らない</b>（＝通常入力の指揮権限を緩めない）。
    /// </summary>
    public static class CommandAuthorityQaMenu
    {
        // ===== 軍団名（撤去はこの名前で判別する） =====

        /// <summary>プレイヤーが直接指揮できる軍団（＝<see cref="BattleHandoff.PlayerCorpsName"/>）。</summary>
        public const string OwnCorps = "QA-自軍団";
        /// <summary>同じ陣営だが指揮系統の外＝支援要請の相手。</summary>
        public const string OtherCorps = "QA-他軍団";
        /// <summary>敵軍団＝選択も命令もできない。</summary>
        public const string EnemyCorps = "QA-敵軍団";

        /// <summary>自軍団の隊数（2隊＝1隊解除しても軍団が残る）。</summary>
        private const int OwnFleetCount = 2;
        /// <summary>他軍団の隊数（2隊＝混在選択の内訳が見える）。</summary>
        private const int OtherFleetCount = 2;
        /// <summary>敵の隊数（攻撃目標＋予備）。</summary>
        private const int EnemyFleetCount = 2;
        private const int PlayerFleetCount = OwnFleetCount + OtherFleetCount;

        /// <summary>1隊あたりの戦略兵力（会戦では ×<see cref="BattleHandoff.StrengthScale"/>）。</summary>
        private const int StrengthPerFleet = 300;

        /// <summary>要請に必ず応じる統率（士気・交戦状態がどうでも承諾側）。</summary>
        private const int CooperativeLeadership = 100;
        /// <summary>要請に必ず断る統率（士気が満タンでも拒否側）。</summary>
        private const int UncooperativeLeadership = 0;

        /// <summary>返事を保留させるときの秒数（失効の確認用）。</summary>
        private const float HoldReplySeconds = 999f;

        /// <summary>準備が終わるのを待つ上限（実時間・秒）。</summary>
        private const double SetupTimeoutSeconds = 30.0;

        // ===== 検証セッションの状態（★Play を抜けたら必ず捨てる） =====

        // QA で作った仮の提督データ（アセットではなくメモリ上のインスタンス）。
        private static readonly List<AdmiralData> tempAdmirals = new List<AdmiralData>();
        // 差し替えた提督データの元の中身（撤去時に戻す）。
        private static readonly List<FleetStrength> touchedFleets = new List<FleetStrength>();
        private static readonly List<AdmiralData> originalAdmirals = new List<AdmiralData>();

        private static bool waitingForSetup;
        private static double waitStartedAt;
        private static bool sessionActive;
        private static float savedTimeScale = 1f;
        private static bool hooked;

        // ===== 入口 =====

        [MenuItem("Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）", false, 330)]
        public static void LaunchAuthorityBattle()
        {
            if (!RequirePlaying()) return;

            Teardown(silent: true);   // 前回の残りがあれば先に片付ける（多重起動しない）

            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            Faction enemy = player == Faction.帝国 ? Faction.同盟 : Faction.帝国;

            var entries = new List<BattleHandoff.HandoffFleet>();
            for (int i = 0; i < PlayerFleetCount; i++)
                entries.Add(new BattleHandoff.HandoffFleet
                {
                    faction = player, strategicStrength = StrengthPerFleet, fleetId = 800 + i,
                    loyalty = 1f, intrigue = 0f, quality = 1f, sideA = true,
                });
            for (int i = 0; i < EnemyFleetCount; i++)
                entries.Add(new BattleHandoff.HandoffFleet
                {
                    faction = enemy, strategicStrength = StrengthPerFleet, fleetId = 860 + i,
                    loyalty = 1f, intrigue = 0f, quality = 1f, sideA = false,
                });

            BattleHandoff.QueueMulti(entries, player, enemy, 800, 860, "Strategy");
            BattleHandoff.battleLabel = "QA 指揮権限テスト（戦役）";
            BattleHandoff.battlefield = default;   // 戦場キーなし＝#38 の援軍は入ってこない（切り分けのため）

            // ★戦役モードで組む。自由操作（全部隊を動かせる演習）にはしない。
            BattleHandoff.FromCampaign = true;
            BattleHandoff.PlayerCommandsWholeFleet = false;   // 総司令官ではない＝軍団長として検証する
            BattleHandoff.PlayerCorpsName = OwnCorps;

            // ★準備が終わるまで盤面を止める（この間に部隊が失われない）。
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            sessionActive = true;
            waitingForSetup = true;
            waitStartedAt = EditorApplication.timeSinceStartup;
            Hook();

            Debug.Log("[指揮権限QA] 検証会戦を開始します（戦役モード／自軍団 " + OwnFleetCount +
                      " 隊・他軍団 " + OtherFleetCount + " 隊・敵 " + EnemyFleetCount + " 隊）。" +
                      "準備が終わるまで一時停止します。シナリオ・シーン・セーブには何も書いていません。");
            SceneManager.LoadScene("Battle");
        }

        // ===== 準備（艦隊が湧いたら自動で軍団を割り当てる） =====

        private static void Hook()
        {
            if (hooked) return;
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            hooked = true;
        }

        private static void Unhook()
        {
            if (!hooked) return;
            EditorApplication.update -= Poll;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            hooked = false;
        }

        /// <summary>★Play を抜けるときに必ず片付ける（静的な検証状態を残さない）。</summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Teardown(silent: true);
        }

        private static void Poll()
        {
            if (!waitingForSetup) return;
            if (!Application.isPlaying) { Teardown(silent: true); return; }

            // 会戦の生成側が倍速を戻すことがあるので、準備中は止め続ける。
            if (Time.timeScale != 0f) Time.timeScale = 0f;

            List<FleetStrength> mine = SideFleets(playerSide: true);
            List<FleetStrength> foes = SideFleets(playerSide: false);

            if (mine.Count >= PlayerFleetCount && foes.Count >= EnemyFleetCount)
            {
                waitingForSetup = false;
                ApplySetup(mine, foes);
                return;
            }

            if (EditorApplication.timeSinceStartup - waitStartedAt > SetupTimeoutSeconds)
            {
                waitingForSetup = false;
                Time.timeScale = savedTimeScale;
                Report("指揮権限テスト",
                    "艦隊が揃いませんでした（味方 " + mine.Count + "/" + PlayerFleetCount +
                    " 隊・敵 " + foes.Count + "/" + EnemyFleetCount + " 隊）。\n\n" +
                    "Battle シーンが開いているか確認し、もう一度「QA: 指揮権限 検証会戦を開始」を実行してください。");
            }
        }

        /// <summary>軍団の割り当てと名前付け。★命令は出さない（盤面を組むだけ）。</summary>
        private static void ApplySetup(List<FleetStrength> mine, List<FleetStrength> foes)
        {
            for (int i = 0; i < mine.Count; i++)
            {
                FleetStrength f = mine[i];
                bool own = i < OwnFleetCount;
                bool head = (i == 0) || (i == OwnFleetCount);

                Remember(f);
                f.corpsName = own ? OwnCorps : OtherCorps;
                f.armyGroupName = "";

                // 軍団旗艦にだけ軍団長を置く（IsCorpsFlagship＝corpsCommander != null）。
                f.corpsCommander = head
                    ? MakeAdmiral(own ? "QA-自軍団長" : "QA-他軍団長",
                                  own ? CooperativeLeadership : CooperativeLeadership)
                    : null;

                // 要請の応否は<b>受け手の艦隊の提督</b>の統率で決まる（SupportRequestDirector.Willingness）。
                // 検証を決定論にするため、他軍団の各隊へ仮の提督を置く（既定＝協力的）。
                if (!own)
                    f.admiralData = MakeAdmiral("QA-他軍団司令" + (i - OwnFleetCount + 1), CooperativeLeadership);

                f.admiralName = own
                    ? "QA自軍" + (i + 1) + "（直接命令できる）"
                    : "QA他軍" + (i - OwnFleetCount + 1) + "（要請のみ）";
                f.gameObject.name = (own ? "QA_OWN_" : "QA_OTHER_") + f.admiralName;
            }

            for (int i = 0; i < foes.Count; i++)
            {
                FleetStrength f = foes[i];
                Remember(f);
                f.corpsName = EnemyCorps;
                f.corpsCommander = null;
                f.admiralName = "QA敵" + (i + 1) + "（選択も命令も不可）";
                f.gameObject.name = "QA_ENEMY_" + f.admiralName;
            }

            // ★通常UIの一時停止に合わせる＝Space 一回で再開できる（isPaused を食い違わせない）。
            //   PauseManager が無い（ウィンドウ化会戦など）ときは timeScale=0 のまま止めておく。
            PauseManager pm = Object.FindFirstObjectByType<PauseManager>();
            if (pm != null && pm.isActiveAndEnabled) pm.Pause();

            Debug.Log("[指揮権限QA] 準備が終わりました（一時停止中）。" +
                      OwnCorps + "=" + OwnFleetCount + "隊 / " + OtherCorps + "=" + OtherFleetCount +
                      "隊 / " + EnemyCorps + "=" + foes.Count + "隊。" +
                      "アセット・シーン・セーブには何も書いていません。");

            Report("指揮権限テスト（準備完了・一時停止中）", ProcedureText());
        }

        // ===== 読み取り専用の状態出力（判定は代行しない） =====

        [MenuItem("Ginei/QA: 指揮権限 いまの権限を出力（Play中）", false, 331)]
        public static void DumpAuthority()
        {
            if (!RequirePlaying()) return;

            FleetCommander fc = FindCommander();
            if (fc == null) { Report("指揮権限テスト", "FleetCommander がありません（Battle シーンで実行してください）。"); return; }

            var sb = new System.Text.StringBuilder();
            sb.Append("操作モード：").Append(BattleCommandModeRules.ModeText(fc.Mode, fc.ActorChain())).Append('\n');
            sb.Append("受け渡し：FromCampaign=").Append(BattleHandoff.FromCampaign)
              .Append(" / 全軍指揮=").Append(BattleHandoff.PlayerCommandsWholeFleet)
              .Append(" / 指揮軍団=").Append(string.IsNullOrEmpty(BattleHandoff.PlayerCorpsName)
                  ? "（なし）" : BattleHandoff.PlayerCorpsName).Append("\n\n");

            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive) continue;
                Selectable sel = f.GetComponent<Selectable>();
                if (sel == null) continue;
                sb.Append(string.IsNullOrEmpty(f.corpsName) ? "（軍団なし）" : f.corpsName)
                  .Append(" / ").Append(f.admiralName)
                  .Append(" → ").Append(fc.RightFor(sel)).Append('\n');
            }

            string msg = sb.ToString();
            Debug.Log("[指揮権限QA] " + msg);
            Report("指揮権限テスト（読み取り専用）", msg);
        }

        [MenuItem("Ginei/QA: 指揮権限 検証手順をもう一度表示（Play中）", false, 332)]
        public static void ShowProcedure()
        {
            if (!RequirePlaying()) return;
            Report("指揮権限テスト（手順）", ProcedureText());
        }

        // ===== 盤面の仕込み（支援要請の各分岐を再現する） =====

        [MenuItem("Ginei/QA: 支援要請 他軍団を協力的にする（承諾の再現・Play中）", false, 340)]
        public static void MakeCooperative() => SetOtherCorpsLeadership(CooperativeLeadership, "協力的（承諾）");

        [MenuItem("Ginei/QA: 支援要請 他軍団を非協力的にする（拒否の再現・Play中）", false, 341)]
        public static void MakeUncooperative() => SetOtherCorpsLeadership(UncooperativeLeadership, "非協力的（拒否）");

        private static void SetOtherCorpsLeadership(int leadership, string label)
        {
            if (!RequirePlaying()) return;
            int changed = 0;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || f.corpsName != OtherCorps || f.admiralData == null) continue;
                if (!tempAdmirals.Contains(f.admiralData)) continue;   // ★QA が作った仮データだけ触る
                f.admiralData.leadership = leadership;
                changed++;
            }
            Report("支援要請テスト",
                changed + " 隊を" + label + "にしました。\n\n" +
                "他軍団を選んで移動／攻撃／陣形変更を出すと、数秒後に結果が通知されます。\n" +
                "（応じる気＝統率 0.6 ＋ 士気 0.4 −交戦中 0.25。統率 " + leadership + " はどちらへも振れません）");
        }

        [MenuItem("Ginei/QA: 支援要請 返事を保留させる（失効の再現・Play中）", false, 342)]
        public static void HoldReplies()
        {
            if (!RequirePlaying()) return;
            SupportRequestDirector dir = FindDirector();
            if (dir == null) { Report("支援要請テスト", "SupportRequestDirector がありません（Battle シーンで実行してください）。"); return; }
            dir.replySeconds = HoldReplySeconds;
            dir.expireSeconds = HoldReplySeconds;
            Report("支援要請テスト",
                "返事を保留させました（返事まで " + HoldReplySeconds + " 秒）。\n\n" +
                "この状態で他軍団へ要請を出し、続けて\n" +
                "「QA: 支援要請 要請先を撃沈する（失効の再現）」または\n" +
                "「QA: 支援要請 攻撃目標を撃沈する（対象消失の再現）」を実行してください。\n\n" +
                "元に戻すには「QA: 支援要請 返事の保留を解除」を実行します。");
        }

        [MenuItem("Ginei/QA: 支援要請 返事の保留を解除（Play中）", false, 343)]
        public static void ReleaseHold()
        {
            if (!RequirePlaying()) return;
            SupportRequestDirector dir = FindDirector();
            if (dir == null) { Report("支援要請テスト", "SupportRequestDirector がありません。"); return; }
            dir.replySeconds = SupportRequestParams.Default.replySeconds;
            dir.expireSeconds = SupportRequestParams.Default.expireSeconds;
            Report("支援要請テスト", "返事の保留を解除しました（既定＝" +
                SupportRequestParams.Default.replySeconds + " 秒で返事）。");
        }

        [MenuItem("Ginei/QA: 支援要請 要請先を撃沈する（失効の再現・Play中）", false, 344)]
        public static void SinkRequestTarget() => Sink(OtherCorps, "要請先（他軍団）");

        [MenuItem("Ginei/QA: 支援要請 攻撃目標を撃沈する（対象消失の再現・Play中）", false, 345)]
        public static void SinkAttackTarget() => Sink(EnemyCorps, "攻撃目標（敵）");

        /// <summary>その軍団の先頭1隊を通常の被弾経路で沈める（盤面の出来事＝権限とは無関係）。</summary>
        private static void Sink(string corpsName, string label)
        {
            if (!RequirePlaying()) return;

            // ★「いま誰かが狙っている艦」を優先して沈める。
            //   軍団の先頭を機械的に取ると、標的と撃沈対象が食い違って
            //   「標的喪失の観測のつもりが別の艦を沈めていた」になりうる。
            FleetStrength victim = null;
            FleetStrength aimedBy = null;
            FleetStrength fallback = null;

            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count && victim == null; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive || f.corpsName != corpsName) continue;
                if (fallback == null) fallback = f;

                Squadron sq = f.GetComponent<Squadron>();
                if (sq == null) continue;
                for (int j = 0; j < all.Count; j++)
                {
                    FleetStrength hunter = all[j];
                    if (hunter == null || !hunter.IsAlive || hunter == f) continue;
                    FleetWeapon w = hunter.GetComponent<FleetWeapon>();
                    if (w == null || w.ManualTargetFleet != sq) continue;
                    victim = f; aimedBy = hunter; break;
                }
            }

            if (victim == null) victim = fallback;
            if (victim == null)
            {
                Report("支援要請テスト", label + "が見つかりません（すでに全滅している可能性があります）。");
                return;
            }

            string victimName = victim.admiralName;
            victim.TakeDamage(victim.strength * 100);   // 通常の撃沈経路（捨てがまり判定を含む）

            string match = aimedBy != null
                ? "★標的と一致：" + aimedBy.admiralName + " が狙っていた艦を沈めました。\n"
                : "⚠ この艦を手動標的にしている艦隊は見つかりませんでした\n" +
                  "　（標的喪失の観測をするなら、先に攻撃を指示してから実行してください）。\n";

            Report("支援要請テスト",
                label + "「" + victimName + "」を撃沈しました。\n\n" + match + "\n" +
                "出していた要請が「流れました」／「目標がすでに失われていました」になることを確認してください。\n" +
                "陣形の保持が続いているかは「QA: 陣形保持 変化の記録を出力」で読み返せます。");
        }

        [MenuItem("Ginei/QA: 支援要請 対象艦隊の命令状態を出力（Play中）", false, 346)]
        public static void DumpOrderState()
        {
            if (!RequirePlaying()) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("承諾した命令が AI に上書きされていないかを見る（読み取り専用）。\n")
              .Append("ManualOverride=True のあいだ FleetAI は操舵に口を出しません。\n")
              .Append("到着すると自動で False へ戻り AI へ復帰します（＝それが正常）。\n\n");

            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive) continue;
                if (f.corpsName != OwnCorps && f.corpsName != OtherCorps && f.corpsName != EnemyCorps) continue;

                var mv = f.GetComponent<FleetMovement>();
                var ai = f.GetComponent<FleetAI>();
                var wp = f.GetComponent<FleetWeapon>();
                var sq = f.GetComponent<Squadron>();
                var so = f.GetComponent<FleetStandardOrder>();

                sb.Append("■ ").Append(f.corpsName).Append(" / ").Append(f.admiralName).Append('\n');
                sb.Append("   現在地=").Append((Vector2)f.transform.position);
                if (mv != null)
                {
                    sb.Append("　移動中=").Append(mv.IsMoving);
                    if (mv.IsMoving)
                        sb.Append("　指定先=").Append(mv.Destination)
                          .Append("　残り=")
                          .Append(Vector2.Distance(f.transform.position, mv.Destination).ToString("0.0"));
                }
                else sb.Append("　（FleetMovement なし）");
                sb.Append('\n');

                sb.Append("   命令維持(ManualOverride)=")
                  .Append(ai != null ? ai.ManualOverride.ToString() : "（FleetAI なし＝AIに上書きされない）")
                  .Append("　出どころ=").Append(ai != null ? ai.OverrideKind.ToString() : "－")
                  .Append(ai != null && ai.OverrideKind == ManualOverrideKind.支援要請
                          ? "（敗走・総退却で中断できる）" : "")
                  .Append("　AI状態=").Append(ai != null ? ai.currentState.ToString() : "－")
                  .Append("　手動標的=").Append(wp != null ? wp.HasManualTarget.ToString() : "－")
                  .Append("　陣形=").Append(sq != null ? sq.currentFormation.ToString() : "－")
                  .Append("　標準命令=").Append(so != null ? so.stance.ToString() : "なし")
                  .Append('\n');

                if (ai != null && mv != null && mv.IsMoving && !ai.ManualOverride)
                    sb.Append("   ★移動中なのに命令維持が False＝AI が行き先を上書きし得ます\n");
                shown++;
            }

            if (shown == 0) sb.Append("（QA の軍団が見つかりません。先に検証会戦を開始してください）\n");

            string msg = sb.ToString();
            Debug.Log("[支援要請QA] " + msg);
            Report("支援要請テスト（読み取り専用）", msg);
        }

        [MenuItem("Ginei/QA: 陣形保持 検証会戦の状態を出力（Play中）", false, 347)]
        public static void DumpFormationState()
        {
            if (!RequirePlaying()) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("陣形の保持と命令の状態（読み取り専用）。UI通知は流れるので、操作の結果はここで照合する。\n\n");

            // 時間の状態。
            sb.Append("■ 時間　timeScale=").Append(Time.timeScale.ToString("0.0"));
            PauseManager pm = Object.FindFirstObjectByType<PauseManager>();
            if (pm != null) sb.Append("　PauseManager.IsPaused=").Append(pm.IsPaused);
            if (TimeDisplay.TryFormatNow(out string clock, out _))
                sb.Append("　暦=").Append(clock.Replace('\n', ' '));
            GameClock gc = StrategySession.Clock;
            if (gc != null)
                sb.Append("　統一クロック=").Append(gc.ElapsedSeconds.ToString("0.0"))
                  .Append("秒／速度").Append(gc.speed.ToString("0.#"))
                  .Append(gc.paused ? "／停止中" : "");
            sb.Append("\n\n");

            // 各QA艦隊。
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            int shown = 0;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive) continue;
                if (f.corpsName != OwnCorps && f.corpsName != OtherCorps && f.corpsName != EnemyCorps) continue;

                var sq = f.GetComponent<Squadron>();
                var ai = f.GetComponent<FleetAI>();
                var mv = f.GetComponent<FleetMovement>();
                var wp = f.GetComponent<FleetWeapon>();
                var mo = f.GetComponent<FleetMorale>();

                // 一意名＝Hierarchy のオブジェクト名（QA_OWN_… 等）で艦隊を取り違えない。
                sb.Append("■ ").Append(f.gameObject.name).Append('\n');
                sb.Append("   軍団=").Append(string.IsNullOrEmpty(f.corpsName) ? "（なし）" : f.corpsName)
                  .Append("　提督=").Append(f.admiralName).Append('\n');

                if (sq != null)
                {
                    FleetFormationHold hold = sq.FormationHold;
                    sb.Append("   陣形=").Append(sq.currentFormation)
                      .Append("　保持=").Append(hold.held)
                      .Append("　指定元=").Append(hold.held ? hold.source.ToString() : "－")
                      .Append("　最後に決めた=").Append(sq.LastFormationSource)
                      .Append("　表示=").Append(FleetFormationOrderRules.HoldText(
                          hold, sq.currentFormation, sq.LastFormationSource))
                      .Append('\n');
                    sb.Append("   スキルP=").Append(sq.SkillPoints.ToString("0.0"))
                      .Append(" / ").Append(sq.SkillPointsMax.ToString("0.0"));
                    if (hold.held) sb.Append("　保持時の軍団キー=").Append(hold.corpsKey);
                    sb.Append('\n');
                }

                sb.Append("   移動中=").Append(mv != null ? mv.IsMoving.ToString() : "－");
                if (mv != null && mv.IsMoving) sb.Append("　行き先=").Append(mv.Destination.ToString("0.0"));
                sb.Append("　手動標的=").Append(wp != null ? wp.HasManualTarget.ToString() : "－")
                  .Append("　override=").Append(ai != null ? ai.OverrideKind.ToString() : "－")
                  .Append('\n');

                sb.Append("   敗走=").Append(mo != null ? mo.IsRouted.ToString() : "－")
                  .Append("（士気 ").Append(mo != null ? mo.morale.ToString("0") : "－").Append("）")
                  .Append("　AI状態=").Append(ai != null ? ai.currentState.ToString() : "－")
                  .Append("　軍団総退却=")
                  .Append(BattlefieldCommandManager.IsCorpsRetreatOrdered(CorpsFormation.KeyFor(f)))
                  .Append('\n');
                shown++;
            }

            if (shown == 0) sb.Append("（QA の軍団が見つかりません。先に検証会戦を開始してください）\n");

            // 直近の陣形・支援まわりの通知（UI から流れて消えたものを拾う）。
            sb.Append("\n■ 直近の通知（陣形・支援）\n");
            int printed = 0;
            IReadOnlyList<Notification> notes = NotificationCenter.All;
            for (int i = notes.Count - 1; i >= 0 && printed < RecentNoteLines; i--)
            {
                string m = notes[i].message;
                if (string.IsNullOrEmpty(m)) continue;
                if (m.IndexOf("陣形", System.StringComparison.Ordinal) < 0
                    && m.IndexOf("要請", System.StringComparison.Ordinal) < 0
                    && m.IndexOf("保持", System.StringComparison.Ordinal) < 0) continue;
                sb.Append("   ").Append(m).Append('\n');
                printed++;
            }
            if (printed == 0) sb.Append("   （該当する通知はありません）\n");

            string msg = sb.ToString();
            Debug.Log("[陣形保持QA]\n" + msg);
            Report("陣形保持テスト（読み取り専用）",
                shown + " 隊ぶんの状態を出力しました。\n\n全文は Console の [陣形保持QA] に出しています。");
        }

        /// <summary>状態ダンプに載せる通知の行数。</summary>
        private const int RecentNoteLines = 12;

        // ===== 時間の操作（PauseManager の正規APIだけを通す） =====
        // Time.timeScale を別系統で書き換えない＝Space や停止ボタンと同じ状態を共有する。

        [MenuItem("Ginei/QA: 時間 一時停止／再開（Play中）", false, 355)]
        public static void TogglePause()
        {
            PauseManager pm = RequirePauseManager();
            if (pm == null) return;
            pm.TogglePause();
            Report("時間QA", (pm.IsPaused ? "一時停止" : "再開") +
                             "しました（PauseManager 経由＝Space と同じ状態）。");
        }

        [MenuItem("Ginei/QA: 時間 1倍速（Play中）", false, 356)]
        public static void Speed1() => SetSpeed(1f);

        [MenuItem("Ginei/QA: 時間 2倍速（Play中）", false, 357)]
        public static void Speed2() => SetSpeed(2f);

        [MenuItem("Ginei/QA: 時間 3倍速（Play中）", false, 358)]
        public static void Speed3() => SetSpeed(3f);

        private static void SetSpeed(float scale)
        {
            PauseManager pm = RequirePauseManager();
            if (pm == null) return;

            // ★正規API。停止中は savedTimeScale が更新され、再開時にこの速度で戻る（製品の挙動そのまま）。
            pm.SetTimeScale(scale);
            Report("時間QA",
                scale + " 倍速にしました（PauseManager.SetTimeScale 経由）。\n" +
                (pm.IsPaused
                    ? "いまは一時停止中なので、再開するとこの速度になります。"
                    : "Time.timeScale = " + Time.timeScale.ToString("0.0")));
        }

        private static PauseManager RequirePauseManager()
        {
            if (!RequirePlaying()) return null;
            PauseManager pm = Object.FindFirstObjectByType<PauseManager>();
            if (pm != null && pm.isActiveAndEnabled) return pm;
            Report("時間QA",
                "PauseManager がありません（または停止しています）。\n\n" +
                "フルスクリーン会戦で実行してください。" +
                "ウィンドウ化会戦では時間制御を統一クロックが担うため、この操作は使えません。");
            return null;
        }

        [MenuItem("Ginei/QA: 陣形保持 保持中の艦を別軍団へ配属換え（Play中）", false, 348)]
        public static void ReassignHeldFleetToOtherCorps()
        {
            if (!RequirePlaying()) return;

            // 前提：陣形を保持している QA 艦隊がいること。
            FleetStrength focus = null;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive) continue;
                if (f.corpsName != OwnCorps && f.corpsName != OtherCorps) continue;
                Squadron sq = f.GetComponent<Squadron>();
                if (sq == null || !sq.IsFormationHeld) continue;
                focus = f; break;
            }

            if (focus == null)
            {
                Report("陣形保持テスト",
                    "【未成立】陣形を保持している QA 艦隊がありません。\n\n" +
                    "艦隊を選んで「陣形 ▸ 円陣」などを指定してから実行してください。");
                return;
            }

            string from = focus.corpsName;
            string to = from == OwnCorps ? OtherCorps : OwnCorps;

            // ★入力条件だけを変える（所属）。保持の解除は Squadron.Update に判断させる。
            focus.corpsName = to;

            Report("陣形保持テスト",
                focus.gameObject.name + " を " + from + " → " + to + " へ配属換えしました。\n\n" +
                "変えたのは所属だけです（保持の解除は書いていません）。\n" +
                "★次の進行フレームで「陣形保持を解除しました（指揮系統の変更）」が出るはずです。\n" +
                "　一時停止中なら Space で再開してください。\n\n" +
                "「QA: 陣形保持 検証会戦の状態を出力」で 保持=False になったことを確認できます。");
        }

        // ===== 撤去 =====

        [MenuItem("Ginei/QA: 指揮権限 検証を撤去（Play中）", false, 350)]
        public static void Remove()
        {
            if (!Application.isPlaying) { Teardown(silent: true); return; }
            Teardown(silent: false);
        }

        /// <summary>
        /// 検証セッションの後始末。<b>静的な検証状態を残さない</b>
        /// ＝軍団名・提督データ・受け渡しの指揮系統・timeScale・Editor のフックを全部戻す。
        /// </summary>
        private static void Teardown(bool silent)
        {
            bool had = sessionActive;
            int restored = 0;

            if (Application.isPlaying)
            {
                // 触った艦隊の提督データを元へ戻し、QA の軍団名を消す。
                for (int i = 0; i < touchedFleets.Count; i++)
                {
                    FleetStrength f = touchedFleets[i];
                    if (f == null) continue;
                    if (i < originalAdmirals.Count) f.admiralData = originalAdmirals[i];
                    if (f.corpsName == OwnCorps || f.corpsName == OtherCorps || f.corpsName == EnemyCorps)
                    {
                        // #67：QA 専用の撤去＝権限判定の対象外（明示）。通常入力からは呼べない。
                        string key = CorpsFormation.KeyFor(f);
                        if (!string.IsNullOrEmpty(key))
                            CorpsFormation.ReleaseManualOrder(key, "QA 撤去", CommandOrderSource.QA);
                        f.corpsName = "";
                        f.corpsCommander = null;
                    }
                    restored++;
                }

                SupportRequestDirector dir = FindDirector();
                if (dir != null)
                {
                    dir.replySeconds = SupportRequestParams.Default.replySeconds;
                    dir.expireSeconds = SupportRequestParams.Default.expireSeconds;
                }

                if (had) Time.timeScale = savedTimeScale;
            }

            touchedFleets.Clear();
            originalAdmirals.Clear();

            // ★仮の提督データを破棄する（残すと Play 終了時に後始末されないオブジェクトになる）。
            for (int i = 0; i < tempAdmirals.Count; i++)
                if (tempAdmirals[i] != null) Object.DestroyImmediate(tempAdmirals[i]);
            tempAdmirals.Clear();

            // 受け渡しの指揮系統を落とす（次の会戦へ前の権限を持ち越さない）。
            if (had)
            {
                BattleHandoff.FromCampaign = false;
                BattleHandoff.PlayerCommandsWholeFleet = false;
                BattleHandoff.PlayerCorpsName = "";
            }

            waitingForSetup = false;
            sessionActive = false;
            Unhook();

            if (silent) return;
            Debug.Log("[指揮権限QA] 検証を撤去しました（" + restored + " 隊を元に戻しました）。");
            Report("指揮権限テスト", restored + " 隊を元に戻し、戦役の指揮系統と時間倍率も戻しました。\n\n" +
                                    "アセット・シーン・セーブには何も書いていません。");
        }

        // ===== 補助 =====

        private static bool RequirePlaying()
        {
            if (Application.isPlaying) return true;
            EditorUtility.DisplayDialog("指揮権限テスト", "Play 中に実行してください。", "OK");
            return false;
        }

        /// <summary>元の提督データを控える（撤去で戻すため）。同じ艦隊は一度だけ。</summary>
        private static void Remember(FleetStrength f)
        {
            if (f == null || touchedFleets.Contains(f)) return;
            touchedFleets.Add(f);
            originalAdmirals.Add(f.admiralData);
        }

        /// <summary>QA 用の仮の提督データ（メモリ上のみ＝アセット化しない。撤去時に破棄）。</summary>
        private static AdmiralData MakeAdmiral(string name, int leadership)
        {
            AdmiralData ad = ScriptableObject.CreateInstance<AdmiralData>();
            ad.hideFlags = HideFlags.DontSave;   // ★アセットとして保存しない
            ad.admiralName = name;
            ad.leadership = leadership;
            ad.attack = 60; ad.defense = 60; ad.mobility = 60;
            ad.intelligence = 60; ad.operation = 60;
            ad.rankTier = 8;
            tempAdmirals.Add(ad);
            return ad;
        }

        /// <summary>同じ戦場のプレイヤー勢力／敵勢力の生存戦闘艦隊（登場順＝決定論）。</summary>
        private static List<FleetStrength> SideFleets(bool playerSide)
        {
            var result = new List<FleetStrength>();
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive || !f.IsCombatant) continue;
                bool mine = f.faction == pf;
                if (mine != playerSide) continue;
                result.Add(f);
            }
            return result;
        }

        private static FleetCommander FindCommander()
        {
            FleetCommander[] all = Object.FindObjectsByType<FleetCommander>(FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        private static SupportRequestDirector FindDirector()
        {
            SupportRequestDirector[] all =
                Object.FindObjectsByType<SupportRequestDirector>(FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>実機での操作順（通常UIで合否を出すための手順）。</summary>
        private static string ProcedureText()
        {
            return
                "準備が終わり、一時停止しています（Space で再開／1・2・3 で倍速）。\n" +
                "戦役モード・軍団長として組んであります（自由操作の権限免除は使っていません）。\n\n" +
                "【盤面】\n" +
                "  " + OwnCorps + "  … QA自軍1／QA自軍2（直接命令できる）\n" +
                "  " + OtherCorps + "  … QA他軍1／QA他軍2（同陣営・系統外＝要請のみ）\n" +
                "  " + EnemyCorps + "  … QA敵1／QA敵2（選択も命令も不可）\n\n" +
                "【1】自軍団：QA自軍1をクリック → 右クリックで移動 → 動くこと。\n" +
                "【2】他軍団：QA他軍1をクリック → 右クリックで移動 →\n" +
                "     直接は動かず「要請しました」と出ること。\n" +
                "【3】敵軍団：QA敵1をクリック → 選択できないこと。\n" +
                "【4】混在：QA自軍1とQA他軍1を範囲選択 → 移動 →\n" +
                "     「直接命令 1 隊／支援要請 1 隊」の内訳が出て、自軍団だけ動くこと。\n" +
                "【5】軍団隊形／解除：右クリックメニューで自軍団に発令 → 通ること。\n" +
                "     他軍団に「軍団指定の解除」→ 通らず理由が出ること。\n\n" +
                "【支援要請の往復】いったん Space で再開してから：\n" +
                "  承諾 … 「QA: 支援要請 他軍団を協力的にする」→ 他軍団へ移動を要請\n" +
                "         → 数秒後「応じました」→ 実際に動くこと。\n" +
                "  拒否 … 「QA: 支援要請 他軍団を非協力的にする」→ 同じ要請\n" +
                "         → 「断りました」→ 動かないこと。\n" +
                "  対象消失 … 「返事を保留させる」→ 他軍団を選び敵QA敵1へ攻撃を要請\n" +
                "         → 「QA: 支援要請 攻撃目標を撃沈する」→ 「流れました」と出ること。\n" +
                "  失効 … 「返事を保留させる」→ 他軍団へ移動を要請\n" +
                "         → 「QA: 支援要請 要請先を撃沈する」→ 「流れました」と出ること。\n\n" +
                "【状態確認】「QA: 指揮権限 いまの権限を出力」で各隊の権限を読み取れます（命令はしません）。\n" +
                "【撤去】「QA: 指揮権限 検証を撤去」。Play を抜けても自動で片付きます。";
        }

        private static void Report(string title, string message)
            => EditorUtility.DisplayDialog(title, message, "OK");
    }
}
