using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 会戦中の勝敗判定と戦績の記録を管理するクラス。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [Header("設定")]
        [Tooltip("勝利判定を行う間隔 (秒)")]
        public float checkInterval = 1.0f;

        private float nextCheckTime;
        private int initialImperialCount;
        private int initialAllianceCount;
        private bool isBattleOver = false;
        private bool initialized = false;

        // 勝利条件評価用
        private ScenarioData activeScenario;     // この会戦の勝利条件・パラメータ
        private float battleElapsed = 0f;        // 会戦経過時間（timeScale 追従。ポーズで停止・倍速で加速）
        private Faction vipFaction;              // 旗艦撃破/護衛の対象VIPの陣営（開始時に解決）
        private bool vipResolved = false;        // VIPの陣営を解決できたか

        private void Start()
        {
            // 開始時にタイムスケールをリセット
            Time.timeScale = 1f;
            AudioManager.Instance.PlayBGM(AudioManager.Instance.bgmBattle);
            // 開始時の隻数記録は、全艦の登録(Start)が済んだ最初の Update で行う（実行順非依存）
        }

        private void Update()
        {
            // デバッグ用：Rキーでリスタート
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartBattle();
                return;
            }

            // 全 Start 完了後（＝レジストリ登録後）の最初の Update で開始時隻数・勝利条件を記録
            if (!initialized)
            {
                CountFleets(out initialImperialCount, out initialAllianceCount);
                ResolveScenarioAndVip();
                initialized = true;
                if (initialImperialCount == 0 && initialAllianceCount == 0)
                {
                    Debug.LogWarning("BattleManager: 開始時に艦隊が見つかりませんでした。");
                }
                return;
            }

            if (isBattleOver) return;

            // 会戦経過時間を積算（timeScale 追従＝ポーズで止まり倍速で速く進む）
            battleElapsed += Time.deltaTime;

            // どちらかの陣営が1隻もいない状態で始まっている場合は判定をスキップ
            if (initialImperialCount == 0 || initialAllianceCount == 0) return;

            if (Time.time >= nextCheckTime)
            {
                CheckVictory();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        /// <summary>
        /// 勝利条件を評価し、決着していれば戦績を記録して結果画面へ遷移します。
        /// </summary>
        private void CheckVictory()
        {
            if (!EvaluateVictory(out Faction winner, out string reason)) return;

            isBattleOver = true;

            // 決着時に時間を停止
            Time.timeScale = 0f;

            RecordResults(winner, reason);

            // 結果画面へ遷移（非同期ロード中も時間は停止しているが、SceneLoaderがunscaledTimeを使っていれば動作する）
            SceneLoader.Instance.LoadScene("Result");
        }

        /// <summary>
        /// シナリオの勝利条件に従って決着を評価する。決着していれば true＋勝者・勝因を返す。
        /// どの条件でも「片陣営の旗艦全滅（殲滅）」は常に終了条件として働く。
        /// </summary>
        private bool EvaluateVictory(out Faction winner, out string reason)
        {
            winner = Faction.同盟;
            reason = "";

            int imp = FleetRegistry.GetFlagships(Faction.帝国).Count;
            int all = FleetRegistry.GetFlagships(Faction.同盟).Count;

            VictoryCondition cond = activeScenario != null ? activeScenario.victoryCondition : VictoryCondition.殲滅;

            // --- 旗艦撃破 / 護衛：対象VIP旗艦の生死・時間で先に決着しうる ---
            if ((cond == VictoryCondition.旗艦撃破 || cond == VictoryCondition.護衛)
                && activeScenario != null && activeScenario.targetAdmiral != null && vipResolved)
            {
                AdmiralData vip = activeScenario.targetAdmiral;
                bool vipAlive = FindLivingFlagshipByAdmiral(vip) != null;

                if (!vipAlive)
                {
                    // VIP喪失 → 反対陣営の勝利
                    winner = Opposite(vipFaction);
                    reason = (cond == VictoryCondition.護衛)
                        ? $"護衛対象「{vip.admiralName}」を喪失"
                        : $"敵旗艦「{vip.admiralName}」を撃破";
                    return true;
                }

                // VIP生存かつ時間切れ → VIP陣営（守備側）の勝利
                if (activeScenario.timeLimit > 0f && battleElapsed >= activeScenario.timeLimit)
                {
                    winner = vipFaction;
                    reason = (cond == VictoryCondition.護衛)
                        ? "護衛成功（制限時間まで守り切った）"
                        : $"旗艦「{vip.admiralName}」を制限時間まで守り切った";
                    return true;
                }
            }

            // --- 時間防衛：防衛側が制限時間まで生存で勝利 ---
            if (cond == VictoryCondition.時間防衛 && activeScenario != null)
            {
                Faction defender = activeScenario.objectiveFaction;
                int defenderCount = (defender == Faction.帝国) ? imp : all;
                if (defenderCount > 0 && activeScenario.timeLimit > 0f && battleElapsed >= activeScenario.timeLimit)
                {
                    winner = defender;
                    reason = "時間防衛成功";
                    return true;
                }
            }

            // --- 殲滅（全条件共通の終了条件）：片陣営の旗艦全滅 ---
            if (imp == 0 || all == 0)
            {
                if (imp == 0 && all == 0)
                {
                    winner = Faction.同盟; // 同時壊滅は便宜上同盟側を勝者扱い
                    reason = "両軍壊滅";
                }
                else
                {
                    winner = (imp > 0) ? Faction.帝国 : Faction.同盟;
                    reason = "敵旗艦全滅";
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// この会戦のシナリオ（勝利条件）と、旗艦撃破/護衛の対象VIPの陣営を解決する。
        /// </summary>
        private void ResolveScenarioAndVip()
        {
            activeScenario = ScenarioData.ActiveScenario;
            if (activeScenario == null)
            {
                activeScenario = ScenarioData.Resolve(GameSettings.Instance.scenarioName);
            }

            vipResolved = false;
            if (activeScenario != null && activeScenario.targetAdmiral != null)
            {
                FleetStrength vipFlag = FindLivingFlagshipByAdmiral(activeScenario.targetAdmiral);
                // 実際の陣営はシナリオで上書きされ得るので、生存中の旗艦から確定する。
                // 開始時に見つからなければ提督データの陣営をフォールバックに使う。
                vipFaction = (vipFlag != null) ? vipFlag.faction : activeScenario.targetAdmiral.faction;
                vipResolved = true;
            }
        }

        /// <summary>指定 AdmiralData を持つ生存旗艦を探す（退却・破棄済みは登録外なので見つからない＝撃破扱い）。</summary>
        private static FleetStrength FindLivingFlagshipByAdmiral(AdmiralData admiral)
        {
            if (admiral == null) return null;
            FleetStrength found = FindInList(FleetRegistry.GetFlagships(Faction.帝国), admiral);
            if (found != null) return found;
            return FindInList(FleetRegistry.GetFlagships(Faction.同盟), admiral);
        }

        private static FleetStrength FindInList(IReadOnlyList<FleetStrength> list, AdmiralData admiral)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].admiralData == admiral) return list[i];
            }
            return null;
        }

        /// <summary>陣営の反対側を返す。</summary>
        private static Faction Opposite(Faction f) => (f == Faction.帝国) ? Faction.同盟 : Faction.帝国;

        /// <summary>旗艦リストの残存兵力合計。</summary>
        private static int SumStrength(IReadOnlyList<FleetStrength> flagships)
        {
            int total = 0;
            for (int i = 0; i < flagships.Count; i++) total += flagships[i].strength;
            return total;
        }

        /// <summary>勝者側の旗艦から与ダメージ最大の提督名を返す。いなければ空。</summary>
        private static string FindMvpAdmiral(IReadOnlyList<FleetStrength> winnerFlagships)
        {
            FleetStrength best = null;
            for (int i = 0; i < winnerFlagships.Count; i++)
            {
                FleetStrength fs = winnerFlagships[i];
                if (best == null || fs.DamageDealt > best.DamageDealt) best = fs;
            }
            return best != null ? best.admiralName : "";
        }

        private void CountFleets(out int imperial, out int alliance)
        {
            // レジストリの生存旗艦数（退却・破棄は含まれない）
            imperial = FleetRegistry.GetFlagships(Faction.帝国).Count;
            alliance = FleetRegistry.GetFlagships(Faction.同盟).Count;
        }

        /// <summary>
        /// 戦績を GameSettings に保存します（勝者・喪失数・陣営別残存兵力・MVP・勝因）。
        /// 勝者・勝因は勝利条件評価(EvaluateVictory)の結果を受け取る。
        /// </summary>
        private void RecordResults(Faction winner, string reason)
        {
            GameSettings settings = GameSettings.Instance;

            IReadOnlyList<FleetStrength> imperial = FleetRegistry.GetFlagships(Faction.帝国);
            IReadOnlyList<FleetStrength> alliance = FleetRegistry.GetFlagships(Faction.同盟);

            int impRem = imperial.Count;
            int allRem = alliance.Count;
            int impStr = SumStrength(imperial);
            int allStr = SumStrength(alliance);

            settings.winner = winner;
            settings.imperialSunkCount = initialImperialCount - impRem;
            settings.allianceSunkCount = initialAllianceCount - allRem;
            settings.remainingStrength = impStr + allStr;
            settings.imperialRemainingStrength = impStr;
            settings.allianceRemainingStrength = allStr;

            // MVP：勝者側の生存旗艦で与ダメージ最大の提督
            settings.mvpAdmiral = FindMvpAdmiral(winner == Faction.帝国 ? imperial : alliance);

            // 勝因（勝利条件の評価結果）
            settings.victoryReason = string.IsNullOrEmpty(reason) ? "敵旗艦全滅" : reason;
        }

        /// <summary>
        /// 会戦を最初からやり直します。
        /// </summary>
        public void RestartBattle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
