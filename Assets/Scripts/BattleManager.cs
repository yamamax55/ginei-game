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

        private void Start()
        {
            // 開始時にタイムスケールをリセット
            Time.timeScale = 1f;

            // 開始時の隻数を記録
            CountFleets(out initialImperialCount, out initialAllianceCount);
            
            // デバッグ: 隻数が0の場合は警告
            if (initialImperialCount == 0 && initialAllianceCount == 0)
            {
                Debug.LogWarning("BattleManager: 開始時に艦隊が見つかりませんでした。");
            }
        }

        private void Update()
        {
            // デバッグ用：Rキーでリスタート
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartBattle();
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
            int imperialRemaining;
            int allianceRemaining;
            int totalStrength = 0;

            // 将来的には FleetRegistry から取得するように変更予定
            FleetStrength[] allFleets = Object.FindObjectsByType<FleetStrength>(FindObjectsSortMode.None);
            imperialRemaining = 0;
            allianceRemaining = 0;

            foreach (var fleet in allFleets)
            {
                if (fleet.faction == Faction.帝国) imperialRemaining++;
                else allianceRemaining++;
                
                totalStrength += fleet.strength;
            }

            // 勝敗確定チェック
            if (imperialRemaining == 0 || allianceRemaining == 0)
            {
                isBattleOver = true;
                
                // 決着時に時間を停止
                Time.timeScale = 0f;

                RecordResults(imperialRemaining, allianceRemaining, totalStrength);
                
                // 結果画面へ遷移（非同期ロード中も時間は停止しているが、SceneLoaderがunscaledTimeを使っていれば動作する）
                SceneLoader.Instance.LoadScene("Result");
            }
        }

        private void CountFleets(out int imperial, out int alliance)
        {
            FleetStrength[] allFleets = Object.FindObjectsByType<FleetStrength>(FindObjectsSortMode.None);
            imperial = 0;
            alliance = 0;
            foreach (var fleet in allFleets)
            {
                if (fleet.faction == Faction.帝国) imperial++;
                else alliance++;
            }
        }

        /// <summary>
        /// 戦績を GameSettings に保存します。
        /// </summary>
        private void RecordResults(int impRem, int allRem, int totalStr)
        {
            GameSettings settings = GameSettings.Instance;
            
            settings.winner = (impRem > 0) ? Faction.帝国 : Faction.同盟;
            settings.imperialSunkCount = initialImperialCount - impRem;
            settings.allianceSunkCount = initialAllianceCount - allRem;
            settings.remainingStrength = totalStr;
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
