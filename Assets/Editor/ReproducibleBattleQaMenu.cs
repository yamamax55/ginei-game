using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// QA：<b>再現可能な会戦QA（固定会戦）</b>の入口（Editor 限定・Play 中のみ）。
    ///
    /// 本体は <see cref="ReproducibleBattleQaSession"/>（PlayMode 試験と共用）。ここはメニューと、
    /// 既存QA（陣形保持の変化記録 <see cref="FormationObservationQaMenu"/>・士気の原因台帳 <see cref="MoraleAuditLog"/>）との連携だけ。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>準備完了まで一時停止（PauseManager と timeScale を一致）。開始・再試行・終了を分ける。</item>
    ///   <item>セーブ・シーン・プレハブ・提督SOに書かない（使い捨てシーンとメモリ上のデータだけ）。</item>
    ///   <item>Play を抜けたら必ず終了扱いにして状態を戻す。</item>
    ///   <item>★会戦イベントの自然発火の確認には使えない（隔離して止めている）。通常会戦の観測は別メニュー。</item>
    /// </list>
    /// </summary>
    public static class ReproducibleBattleQaMenu
    {
        private const string Root = "Ginei/QA: 固定会戦 ";
        private const string MenuSeedToggle = Root + "seed を別値にする（区別の確認用）";
        private const string Title = "固定会戦QA";

        /// <summary>準備に使う seed（既定＝カタログの既定値。トグルで別値）。</summary>
        private static bool useAlternateSeed;

        /// <summary>このメニューが陣形保持の記録係を立てたか（終了時にそれだけ片付ける）。</summary>
        private static bool recorderStartedByUs;

        private static int CurrentSeed => useAlternateSeed ? BattleQaPresetCatalog.AlternateSeed : BattleQaPresetCatalog.DefaultSeed;

        // ===== 調整プリセット（SPEED-07・開始前に選ぶ。準備済みのセッションには効かない＝再試行は準備時の調整を使う） =====
        private const string TuningRoot = Root + "調整プリセット/";
        private const string MenuTuning0 = TuningRoot + "既定（実効値のまま）";
        private const string MenuTuning1 = TuningRoot + "移動速度×1.5";
        private const string MenuTuning2 = TuningRoot + "回頭速度×1.5";
        private const string MenuTuning3 = TuningRoot + "士気回復量×2";
        private const string MenuTuning4 = TuningRoot + "敗走回復待ち×0.5";
        private const string MenuTuning5 = TuningRoot + "軍団隊形間隔（最小間隔）×2";
        private static readonly string[] TuningMenus = { MenuTuning0, MenuTuning1, MenuTuning2, MenuTuning3, MenuTuning4, MenuTuning5 };

        /// <summary>次の準備に使う調整プリセットの番号（<see cref="BattleQaTuningCatalog.All"/> の並び）。</summary>
        private static int tuningIndex;

        private static BattleQaTuningProfile CurrentTuning
        {
            get
            {
                var all = BattleQaTuningCatalog.All();
                return tuningIndex >= 0 && tuningIndex < all.Count ? all[tuningIndex] : BattleQaTuningProfile.Default;
            }
        }

        [MenuItem(MenuTuning0, false, 390)] private static void SelectTuning0() => SelectTuning(0);
        [MenuItem(MenuTuning1, false, 391)] private static void SelectTuning1() => SelectTuning(1);
        [MenuItem(MenuTuning2, false, 392)] private static void SelectTuning2() => SelectTuning(2);
        [MenuItem(MenuTuning3, false, 393)] private static void SelectTuning3() => SelectTuning(3);
        [MenuItem(MenuTuning4, false, 394)] private static void SelectTuning4() => SelectTuning(4);
        [MenuItem(MenuTuning5, false, 395)] private static void SelectTuning5() => SelectTuning(5);

        [MenuItem(MenuTuning0, true)] private static bool ValidateTuning0() => ValidateTuning();
        [MenuItem(MenuTuning1, true)] private static bool ValidateTuning1() => ValidateTuning();
        [MenuItem(MenuTuning2, true)] private static bool ValidateTuning2() => ValidateTuning();
        [MenuItem(MenuTuning3, true)] private static bool ValidateTuning3() => ValidateTuning();
        [MenuItem(MenuTuning4, true)] private static bool ValidateTuning4() => ValidateTuning();
        [MenuItem(MenuTuning5, true)] private static bool ValidateTuning5() => ValidateTuning();

        private static void SelectTuning(int index)
        {
            tuningIndex = index;
            ValidateTuning();
            Debug.Log("［固定会戦QA］次の準備の" + CurrentTuning.Describe() +
                      (ReproducibleBattleQaSession.Active != null ? "（現在のセッションには効かない。終了してから準備し直す）" : ""));
        }

        /// <summary>チェック表示を同期する（選択はいつでも可＝次の準備から効く）。</summary>
        private static bool ValidateTuning()
        {
            for (int i = 0; i < TuningMenus.Length; i++) Menu.SetChecked(TuningMenus[i], i == tuningIndex);
            return true;
        }

        // ===== 機能スイッチ（SPEED-08・開始前に個別に切り替える。準備済みのセッションには効かない＝再試行は準備時のスイッチを使う） =====
        private const string SwitchRoot = Root + "機能スイッチ/";
        private const string MenuSwitchEnvelopment = SwitchRoot + "自動包囲（軍団長AIの回り込み）";
        private const string MenuSwitchReinforcement = SwitchRoot + "援軍（BattleSetup の時限増援・QA所有・固定合格の比較対象外）";
        private const string MenuSwitchEvents = SwitchRoot + "戦況イベント（自然抽選・固定合格の比較対象外）";

        /// <summary>次の準備に使うスイッチ（既定＝固定QAと同じ：自動包囲ON・援軍OFF・戦況イベントOFF）。</summary>
        private static bool switchEnvelopment = BattleQaFeatureSwitches.FixedDefaultOf(BattleQaFeature.自動包囲);
        private static bool switchReinforcement = BattleQaFeatureSwitches.FixedDefaultOf(BattleQaFeature.援軍);
        private static bool switchEvents = BattleQaFeatureSwitches.FixedDefaultOf(BattleQaFeature.戦況イベント);

        private static BattleQaFeatureSwitches CurrentSwitches =>
            new BattleQaFeatureSwitches(BattleQaFeatureSwitches.AutoName(switchEnvelopment, switchReinforcement, switchEvents),
                BattleQaFeatureSwitches.CurrentVersion, switchEnvelopment, switchReinforcement, switchEvents);

        [MenuItem(MenuSwitchEnvelopment, false, 396)] private static void ToggleEnvelopment() { switchEnvelopment = !switchEnvelopment; AfterSwitchToggle(); }
        [MenuItem(MenuSwitchReinforcement, false, 397)] private static void ToggleReinforcement() { switchReinforcement = !switchReinforcement; AfterSwitchToggle(); }
        [MenuItem(MenuSwitchEvents, false, 398)] private static void ToggleEvents() { switchEvents = !switchEvents; AfterSwitchToggle(); }

        [MenuItem(MenuSwitchEnvelopment, true)] private static bool ValidateEnvelopment() => ValidateSwitches();
        [MenuItem(MenuSwitchReinforcement, true)] private static bool ValidateReinforcement() => ValidateSwitches();
        [MenuItem(MenuSwitchEvents, true)] private static bool ValidateEvents() => ValidateSwitches();

        private static void AfterSwitchToggle()
        {
            ValidateSwitches();
            Debug.Log("［固定会戦QA］次の準備の" + CurrentSwitches.Describe() +
                      (ReproducibleBattleQaSession.Active != null ? "（現在のセッションには効かない。終了してから準備し直す）" : ""));
        }

        /// <summary>チェック表示を同期する（切替はいつでも可＝次の準備から効く）。</summary>
        private static bool ValidateSwitches()
        {
            Menu.SetChecked(MenuSwitchEnvelopment, switchEnvelopment);
            Menu.SetChecked(MenuSwitchReinforcement, switchReinforcement);
            Menu.SetChecked(MenuSwitchEvents, switchEvents);
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play を抜ける＝終了扱い（状態を戻す）。記録係は FormationObservationQaMenu 側でも片付く。</summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            if (ReproducibleBattleQaSession.Active != null) ReproducibleBattleQaSession.Active.End("Play 停止");
            recorderStartedByUs = false;
        }

        [MenuItem(Root + "準備：退却（Play中）", false, 380)]
        private static void PrepareRetreat() => Prepare(BattleQaPresetKind.退却);

        [MenuItem(Root + "準備：不退転（Play中）", false, 381)]
        private static void PrepareMoraleLock() => Prepare(BattleQaPresetKind.不退転);

        [MenuItem(Root + "準備：陣形変更（Play中）", false, 382)]
        private static void PrepareFormation() => Prepare(BattleQaPresetKind.陣形変更);

        [MenuItem(MenuSeedToggle, false, 383)]
        private static void ToggleSeed()
        {
            useAlternateSeed = !useAlternateSeed;
            Menu.SetChecked(MenuSeedToggle, useAlternateSeed);
            Debug.Log("［固定会戦QA］次の準備の seed＝" + CurrentSeed);
        }

        [MenuItem(MenuSeedToggle, true)]
        private static bool ToggleSeedValidate()
        {
            Menu.SetChecked(MenuSeedToggle, useAlternateSeed);
            return true;
        }

        [MenuItem(Root + "開始（準備完了後）", false, 384)]
        private static void StartRun()
        {
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Active;
            if (s == null) { Dialog("セッションがありません。先に準備してください。"); return; }
            if (!s.StartRun()) Dialog("開始できません（現在＝" + s.CurrentPhase + "）。準備完了まで待ってください。");
        }

        [MenuItem(Root + "再試行（同じプリセット・同じ seed）", false, 385)]
        private static void Retry()
        {
            if (ReproducibleBattleQaSession.Active == null) { Dialog("再試行するセッションがありません。"); return; }
            ReproducibleBattleQaSession.Retry();
            Debug.Log("［固定会戦QA］再試行：前の実行の状態を戻して片付け、同じ条件で準備し直します。準備完了後に開始してください。");
        }

        [MenuItem(Root + "終了（状態を戻す）", false, 386)]
        private static void End()
        {
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Active;
            if (s == null) { Dialog("セッションはありません（終了済み）。"); return; }
            s.End("メニューから終了");
            StopRecorderIfOurs();
            Debug.Log("［固定会戦QA］終了：" + ReproducibleBattleQaSession.LastRestoreReport);
        }

        [MenuItem(Root + "結果ログを出力", false, 387)]
        private static void Dump()
        {
            BattleQaRunLog log = ReproducibleBattleQaSession.LastLog;
            if (log == null) { Dialog("結果ログがありません。準備してください。"); return; }

            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Active;
            string header =
                "[固定会戦QA・結果ログ]\n" +
                "★固定したのは初期条件と命令だけ。フレーム単位の完全な決定論は保証しない（seed が同じでも戦闘結果の一致は扱わない）。\n" +
                "★未判定は合格ではない。" +
                (s != null && s.Switches != null && s.Switches.Get(BattleQaFeature.戦況イベント)
                    ? "★戦況イベント=ON（QAシーン所有の自然抽選）＝固定合格の比較対象外。通常会戦での自然発火の合格にも使わない。\n"
                    : "★会戦イベントの自然発火はこのモードでは観測していない（通常会戦の観測は未完了のまま別途）。\n") +
                (s != null && s.Switches != null ? s.Switches.Describe() + "\n" : "") +
                (!string.IsNullOrEmpty(ReproducibleBattleQaSession.LastSwitchDisposal) ? "直近の機能スイッチ後始末：" + ReproducibleBattleQaSession.LastSwitchDisposal + "\n" : "") +
                "セッション=" + (s != null ? s.CurrentPhase.ToString() : "終了済み") +
                "　士気の原因台帳：件数=" + MoraleAuditLog.Count + " あふれ=" + MoraleAuditLog.Dropped + " 記録中=" + MoraleAuditLog.Enabled +
                "　陣形保持記録：行数=" + FormationObservationQaMenu.Count + " 上限超過=" + FormationObservationQaMenu.Truncated + "\n";
            string loss = BattleQaRunLog.LossNotice(log.Dropped, MoraleAuditLog.Dropped, FormationObservationQaMenu.Truncated);
            if (loss.Length > 0) header += loss + "\n";
            if (s != null && s.IsolationNotes.Count > 0) header += "隔離：" + string.Join("／", s.IsolationNotes) + "\n";
            if (!string.IsNullOrEmpty(ReproducibleBattleQaSession.LastRestoreReport))
                header += "直近の復元：" + ReproducibleBattleQaSession.LastRestoreReport + "\n";

            Debug.Log(log.Dump(header));
            Dialog("結果ログを Console に出力しました（run_id=" + log.runId + " / 結論=" + log.Overall + "）。\n" +
                   "士気の原因・陣形保持の詳細は既存メニュー「士気の原因 記録を出力」「陣形保持 変化の記録を出力」で読めます。");
        }

        // ===== 補助 =====

        private static void Prepare(BattleQaPresetKind kind)
        {
            if (!Application.isPlaying) { Dialog("Play 中に実行してください（Title シーンからの Play を推奨）。"); return; }
            if (ReproducibleBattleQaSession.Active != null)
            {
                Dialog("既にセッションがあります（" + ReproducibleBattleQaSession.Active.Preset.name + "）。終了か再試行を使ってください。");
                return;
            }

            // 既存QA：陣形保持の変化記録を並走させる（無ければ立てる＝終了時に片付ける）。
            if (Object.FindFirstObjectByType<FormationChangeRecorder>() == null)
            {
                var go = new GameObject("QA_FormationChangeRecorder");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<FormationChangeRecorder>();
                recorderStartedByUs = true;
            }

            BattleQaPreset preset = BattleQaPresetCatalog.Create(kind, CurrentSeed);
            BattleQaTuningProfile tuning = CurrentTuning;
            BattleQaFeatureSwitches switches = CurrentSwitches;
            ReproducibleBattleQaSession s = ReproducibleBattleQaSession.Begin(preset, tuning, switches);
            if (s == null) { StopRecorderIfOurs(); return; }
            Debug.Log("［固定会戦QA］準備を開始：" + preset.name + " seed=" + preset.seed + " AI=" + preset.AiMode +
                      " / " + tuning.Describe() + " / " + switches.Describe() +
                      "。画面左上の段階が「準備完了」になったら「開始」を実行してください。準備失敗時は理由を Console に出します。");
        }

        private static void StopRecorderIfOurs()
        {
            if (!recorderStartedByUs) return;
            FormationChangeRecorder rec = Object.FindFirstObjectByType<FormationChangeRecorder>();
            if (rec != null) Object.Destroy(rec.gameObject);   // 履歴は FormationObservationQaMenu に残る
            recorderStartedByUs = false;
        }

        private static void Dialog(string message) => EditorUtility.DisplayDialog(Title, message, "OK");
    }
}
