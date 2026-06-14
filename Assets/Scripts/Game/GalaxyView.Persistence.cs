using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        /// <summary>戦術マップでの攻城進捗（割合）を戦略の惑星へ反映する（#131）。占領なら所有フリップ＋再建。</summary>
        private void ApplySiegeResult()
        {
            StarSystem s = map.GetSystem(BattleHandoff.planetSystemId);
            if (s != null && s.planet != null)
            {
                Planet p = s.planet;
                if (BattleHandoff.siegeResultCaptured)
                {
                    p.owner = BattleHandoff.besiegerFaction;
                    s.owner = BattleHandoff.besiegerFaction;
                    p.orbitalDefense = p.maxOrbitalDefense; // 新所有者が制空権を再建
                    p.invasionProgress = 0f;
                    NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意, $"{s.systemName} を占領しました");
                }
                else
                {
                    p.orbitalDefense = Mathf.Clamp01(BattleHandoff.siegeResultDefense) * p.maxOrbitalDefense;
                    p.invasionProgress = Mathf.Clamp01(BattleHandoff.siegeResultInvasion) * p.invasionThreshold;
                    NotificationCenter.Push(NotificationCategory.占領, $"{s.systemName} の攻城を進めました");
                }
            }
            BattleHandoff.Clear();
        }

        // --- 政体進化（#117 配線）：首長制→民主/独裁→下位形態 ---
        private bool regimeFormsSeeded;

        // --- キャンペーン勝敗（遊べる縦スライスの核） ---
        private bool campaignDecided;

        /// <summary>
        /// プレイヤー勢力の戦略的決着を年次で判定し、勝利/敗北したら時計を止めて終了画面を出す（一度きり）。
        /// 判定は <see cref="CampaignVictoryRules"/>（制覇=支配率/全制圧/滅亡）。終了画面は <see cref="CampaignEndOverlay"/>。
        /// </summary>
        private void RunCampaignVictoryCheck()
        {
            if (campaignDecided || map == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            CampaignOutcome outcome = CampaignVictoryRules.Evaluate(map, player, ActiveVictoryParams());
            if (outcome == CampaignOutcome.継続) return;

            campaignDecided = true;
            if (StrategySession.Clock != null) StrategySession.Clock.Pause(); // 進行を止める
            int frac = Mathf.RoundToInt(CampaignVictoryRules.OwnedFraction(map, player) * 100f);
            bool win = outcome == CampaignOutcome.勝利;
            string msg = win
                ? $"【勝利】{player} が銀河を制覇（支配 {frac}%）"
                : $"【敗北】{player} は星系をすべて失った";
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.警告, msg);
            CampaignEndOverlay.Show(win, player, CampaignVictoryRules.OwnedFraction(map, player)); // 終了画面（遊べる縦スライスの締め）
        }

        /// <summary>
        /// 戦役を跨いで残る static 状態をリセットする（終了画面「タイトルへ戻る」/新規キャンペーン開始時）。
        /// 同一アプリ実行内で2周目を始めても目標提示が再び出るよう、オンボーディングのフラグを戻す。
        /// </summary>
        public static void ResetCampaignStatics()
        {
            objectiveAnnounced = false;
        }

        /// <summary>
        /// タイトルから新規キャンペーンを始める前処理（戦略の世界状態を破棄＝Strategy シーンで一から構築される）。
        /// `TitleManager` が呼んでから "Strategy" シーンへ遷移する。
        /// </summary>
        public static void BeginNewCampaign()
        {
            StrategySession.Clear();
            BattleHandoff.Clear();
            ResetCampaignStatics();
        }

        /// <summary>戦役の全状態（銀河/勢力/財政/人物/艦隊/時間/内政）をファイルへ書き出す共通処理。</summary>
        private void WriteCampaignSave()
        {
            var people = new System.Collections.Generic.List<Person>();
            if (commanders != null) people.AddRange(commanders);
            if (civilians != null) people.AddRange(civilians);
            CampaignSaveManager.SaveSession(StrategySession.Campaign, people, reg, StrategySession.Clock, StrategySession.Provinces, StrategySession.CourtAuthority);
        }

        /// <summary>戦役の全状態をファイルへ保存する（F5・手動）。</summary>
        /// <summary>世界状態を全永続化する（F5／システムメニューの「セーブ」から）。</summary>
        public void SaveCampaign()
        {
            WriteCampaignSave();
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報, "セーブしました（F9 で再開）");
        }

        /// <summary>年境界ごとの自動保存（閉じても進行が消えないように）。F9/タイトルの「戦役を再開」で復帰できる。</summary>
        private void AutoSaveCampaign()
        {
            WriteCampaignSave();
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報, "オートセーブ");
        }

        /// <summary>セーブから全状態を StrategySession へ復元し、Strategy シーンを再ロードして盤面を再構築する（F9）。</summary>
        private void LoadCampaign()
        {
            if (!CampaignSaveManager.HasSave()) { NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意, "セーブがありません"); return; }
            if (CampaignSaveManager.LoadSession())
                SceneManager.LoadScene("Strategy");
        }

    }
}
