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

            // 全 Start 完了後（＝レジストリ登録後）の最初の Update で開始時隻数を記録
            if (!initialized)
            {
                CountFleets(out initialImperialCount, out initialAllianceCount);
                initialized = true;
                if (initialImperialCount == 0 && initialAllianceCount == 0)
                {
                    Debug.LogWarning("BattleManager: 開始時に艦隊が見つかりませんでした。");
                }
                return;
            }

            if (isBattleOver) return;

            // どちらかの陣営が1隻もいない状態で始まっている場合は判定をスキップ
            if (initialImperialCount == 0 || initialAllianceCount == 0) return;

            if (Time.time >= nextCheckTime)
            {
                CheckVictory();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        /// <summary>
        /// 現在の全艦隊を数え、全滅している陣営がないか確認します。
        /// </summary>
        private void CheckVictory()
        {
            // 生存旗艦のみをレジストリから取得（退却・破棄は含まれない。配下艦も数に含めない）
            IReadOnlyList<FleetStrength> imperial = FleetRegistry.GetFlagships(Faction.帝国);
            IReadOnlyList<FleetStrength> alliance = FleetRegistry.GetFlagships(Faction.同盟);

            int imperialRemaining = imperial.Count;
            int allianceRemaining = alliance.Count;

            // 勝敗確定チェック
            if (imperialRemaining == 0 || allianceRemaining == 0)
            {
                isBattleOver = true;

                // 決着時に時間を停止
                Time.timeScale = 0f;

                RecordResults(imperial, alliance);

                // 結果画面へ遷移（非同期ロード中も時間は停止しているが、SceneLoaderがunscaledTimeを使っていれば動作する）
                SceneLoader.Instance.LoadScene("Result");
            }
        }

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
        /// </summary>
        private void RecordResults(IReadOnlyList<FleetStrength> imperial, IReadOnlyList<FleetStrength> alliance)
        {
            GameSettings settings = GameSettings.Instance;

            int impRem = imperial.Count;
            int allRem = alliance.Count;
            int impStr = SumStrength(imperial);
            int allStr = SumStrength(alliance);

            settings.winner = (impRem > 0) ? Faction.帝国 : Faction.同盟;
            settings.imperialSunkCount = initialImperialCount - impRem;
            settings.allianceSunkCount = initialAllianceCount - allRem;
            settings.remainingStrength = impStr + allStr;
            settings.imperialRemainingStrength = impStr;
            settings.allianceRemainingStrength = allStr;

            // MVP：勝者側の生存旗艦で与ダメージ最大の提督
            settings.mvpAdmiral = FindMvpAdmiral(settings.winner == Faction.帝国 ? imperial : alliance);

            // 勝因（現状は敵旗艦全滅で固定。A-4 導入後に連動）
            settings.victoryReason = "敵旗艦全滅";
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
