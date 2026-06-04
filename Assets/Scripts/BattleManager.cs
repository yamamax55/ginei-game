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
        private bool initialHadHostilePair = false; // 開始時に敵対する旗艦同士が居たか（自動決着の前提）

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

            // 全 Start 完了後（＝レジストリ登録後）の最初の Update で開始時隻数・敵対状況を記録
            if (!initialized)
            {
                CountFleets(out initialImperialCount, out initialAllianceCount);
                initialHadHostilePair = HasHostilePair(FleetRegistry.AllFlagships);
                initialized = true;
                if (FleetRegistry.AllFlagships.Count == 0)
                {
                    Debug.LogWarning("BattleManager: 開始時に艦隊が見つかりませんでした。");
                }
                return;
            }

            if (isBattleOver) return;

            // 開始時に敵対する旗艦が無い構成（単一勢力など）では自動決着しない
            if (!initialHadHostilePair) return;

            if (Time.time >= nextCheckTime)
            {
                CheckVictory();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        /// <summary>
        /// 敵対する旗艦同士が残っているかを確認し、いなくなったら決着とします（多勢力対応）。
        /// </summary>
        private void CheckVictory()
        {
            IReadOnlyList<FleetStrength> alive = FleetRegistry.AllFlagships;

            // まだ敵対する旗艦のペアが残っていれば戦闘継続
            if (HasHostilePair(alive)) return;

            isBattleOver = true;
            Time.timeScale = 0f;          // 決着時に時間を停止
            RecordResults(alive);
            // 結果画面へ遷移（非同期ロード中も時間は停止しているが、SceneLoaderがunscaledTimeを使う）
            SceneLoader.Instance.LoadScene("Result");
        }

        /// <summary>生存旗艦の中に敵対するペアが1組でもあるか。</summary>
        private static bool HasHostilePair(IReadOnlyList<FleetStrength> flagships)
        {
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength a = flagships[i];
                if (a == null || !a.IsAlive) continue;
                for (int j = i + 1; j < flagships.Count; j++)
                {
                    FleetStrength b = flagships[j];
                    if (b == null || !b.IsAlive) continue;
                    if (FactionRelations.IsHostile(a, b)) return true;
                }
            }
            return false;
        }

        /// <summary>旧 enum Faction に正規化（FactionData があればその legacyFaction）。</summary>
        private static Faction LegacyOf(FleetStrength fs)
            => fs.factionData != null ? fs.factionData.legacyFaction : fs.faction;

        /// <summary>2 旗艦が同一勢力か（FactionData 優先、無ければ enum）。</summary>
        private static bool SameFaction(FleetStrength a, FleetStrength b)
        {
            if (a.factionData != null && b.factionData != null) return a.factionData == b.factionData;
            if (a.factionData == null && b.factionData == null) return a.faction == b.faction;
            return false;
        }

        /// <summary>勝者勢力の代表旗艦（残存旗艦数が最多、同数なら残存兵力が最大の勢力）。全滅なら null。</summary>
        private static FleetStrength DetermineWinner(IReadOnlyList<FleetStrength> alive)
        {
            FleetStrength best = null;
            int bestCount = -1, bestStrength = -1;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength rep = alive[i];
                if (rep == null) continue;
                int count = 0, strength = 0;
                for (int j = 0; j < alive.Count; j++)
                {
                    FleetStrength other = alive[j];
                    if (other == null || !SameFaction(rep, other)) continue;
                    count++; strength += other.strength;
                }
                if (count > bestCount || (count == bestCount && strength > bestStrength))
                {
                    bestCount = count; bestStrength = strength; best = rep;
                }
            }
            return best;
        }

        /// <summary>勝者勢力の生存旗艦で与ダメージ最大の提督名。</summary>
        private static string FindMvpAdmiral(IReadOnlyList<FleetStrength> alive, FleetStrength winnerRep)
        {
            FleetStrength best = null;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null || winnerRep == null || !SameFaction(winnerRep, fs)) continue;
                if (best == null || fs.DamageDealt > best.DamageDealt) best = fs;
            }
            return best != null ? best.admiralName : "";
        }

        private void CountFleets(out int imperial, out int alliance)
        {
            // 旧 enum バケツ別の生存旗艦数（後方互換の戦績集計用）
            imperial = 0; alliance = 0;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (fs == null) continue;
                if (LegacyOf(fs) == Faction.帝国) imperial++; else alliance++;
            }
        }

        /// <summary>
        /// 戦績を GameSettings に保存します（勝者・勝者名・喪失数・残存兵力・MVP・勝因）。
        /// </summary>
        private void RecordResults(IReadOnlyList<FleetStrength> alive)
        {
            GameSettings settings = GameSettings.Instance;

            // 後方互換の enum 別集計（帝国/同盟バケツ）
            int impRem = 0, allRem = 0, impStr = 0, allStr = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null) continue;
                if (LegacyOf(fs) == Faction.帝国) { impRem++; impStr += fs.strength; }
                else { allRem++; allStr += fs.strength; }
            }

            FleetStrength winnerRep = DetermineWinner(alive);
            if (winnerRep != null)
            {
                settings.winner = LegacyOf(winnerRep);
                settings.winnerName = (winnerRep.factionData != null) ? winnerRep.factionData.factionName : winnerRep.faction.ToString();
                settings.victoryReason = "敵旗艦全滅";
                settings.mvpAdmiral = FindMvpAdmiral(alive, winnerRep);
            }
            else
            {
                // 全旗艦喪失＝引き分け
                settings.winner = Faction.同盟;
                settings.winnerName = "引き分け";
                settings.victoryReason = "両軍壊滅";
                settings.mvpAdmiral = "";
            }

            settings.imperialSunkCount = initialImperialCount - impRem;
            settings.allianceSunkCount = initialAllianceCount - allRem;
            settings.remainingStrength = impStr + allStr;
            settings.imperialRemainingStrength = impStr;
            settings.allianceRemainingStrength = allStr;
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
