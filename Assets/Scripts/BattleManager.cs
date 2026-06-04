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

            int totalStrength = 0;
            for (int i = 0; i < imperial.Count; i++) totalStrength += imperial[i].strength;
            for (int i = 0; i < alliance.Count; i++) totalStrength += alliance[i].strength;

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
            // レジストリの生存旗艦数（退却・破棄は含まれない）
            imperial = FleetRegistry.GetFlagships(Faction.帝国).Count;
            alliance = FleetRegistry.GetFlagships(Faction.同盟).Count;
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
