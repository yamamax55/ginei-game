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

        /// <summary>
        /// 防衛惑星の攻城を1tick進め、<b>攻城開始</b>と<b>占領完了</b>を通知へ出す（自動解決経路）。
        /// あわせて攻城中の星系集合 <see cref="besiegedSystems"/> を更新＝占領が終われば集合から外れ、
        /// ラベル（制空/侵攻%）は <see cref="IsActiveSiege"/> 判定で消える（占領後にラベルが残らない）。
        /// </summary>
        private void RunPlanetSiegeTick(float dt)
        {
            if (map == null || reg == null || dt <= 0f) return;

            // 1) 攻城開始の検出（敵対艦隊が在席し始めた惑星）＋占領前の所有を控える。
            //    占領完了の差分検出のため、攻城中の惑星の現所有を一時記録する。
            var ownerBefore = new Dictionary<int, Faction>();
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.planet == null) continue;
                bool active = IsActiveSiege(s, s.planet);
                bool was = besiegedSystems.Contains(s.id);
                if (active)
                {
                    ownerBefore[s.id] = s.planet.owner;
                    if (!was)
                    {
                        besiegedSystems.Add(s.id);
                        StrategicFleet b = FindBesieger(s.id, s.planet.owner);
                        string who = b != null ? b.faction.ToString() : "敵艦隊";
                        NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                            $"{who} が {s.systemName} の攻城を開始");
                    }
                }
                else if (was)
                {
                    // 攻城が解かれた（攻め手が去った／守りが固まった）＝占領前に解囲。
                    besiegedSystems.Remove(s.id);
                }
            }

            // 2) 攻城を進める（占領で planet/owner がフリップ）。
            StrategyRules.TickSieges(map, reg, dt, new SiegeParams(siegeSuppressRate, siegeInvadeRate, siegeDefenseRegen));

            // 3) 占領完了の検出（攻城中だった惑星の所有が変わった）。
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.planet == null) continue;
                if (ownerBefore.TryGetValue(s.id, out Faction before) && before != s.planet.owner)
                {
                    besiegedSystems.Remove(s.id);
                    NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.警告,
                        $"{s.planet.owner} が {s.systemName} を占領");
                }
            }
        }

        /// <summary>
        /// 無防備星系の停泊占領（<see cref="StrategyRules.ResolveAllOccupations"/>）を実行し、所有が移った非防衛星系を
        /// 「占領完了」として通知する（惑星攻城は <see cref="RunPlanetSiegeTick"/> が担うので二重通知しない＝惑星持ちは除外）。
        /// </summary>
        private void NotifyStationingCaptures()
        {
            if (map == null || reg == null) { StrategyRules.ResolveAllOccupations(map, reg); return; }

            var ownerBefore = new Dictionary<int, Faction>();
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.planet != null) continue; // 惑星持ちは攻城経路で通知
                ownerBefore[s.id] = s.owner;
            }

            StrategyRules.ResolveAllOccupations(map, reg);

            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.planet != null) continue;
                if (ownerBefore.TryGetValue(s.id, out Faction before) && before != s.owner)
                    NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                        $"{s.owner} が {s.systemName} を占領");
            }
        }

        /// <summary>
        /// その防衛惑星が現に攻城されているか（敵対艦隊が在席）。<see cref="StrategyRules.TickSieges"/> の攻め手判定と同条件。
        /// ラベル表示（制空/侵攻%）と攻城通知の両方がこの単一判定を使う＝占領完了/解囲でラベルが残らない。
        /// </summary>
        private bool IsActiveSiege(StarSystem s, Planet p)
        {
            if (s == null || p == null || reg == null) return false;
            List<StrategicFleet> present = reg.FleetsAt(s.id);
            for (int k = 0; k < present.Count; k++)
            {
                StrategicFleet f = present[k];
                if (f != null && FactionRelations.IsHostile(null, f.faction, null, p.owner)) return true;
            }
            return false;
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
            PersonDecisionLedger.Clear(); // 人物の決裁履歴も戦役を跨いで持ち越さない（稟議基盤整備）
            RingiDirector.Ledger.Clear(); // 進行中の稟議在庫も戦役跨ぎでクリア（DESK-6 合流）
            // ネームド財産（金融資産#2070・不動産権利証#2070/惑星の土地#2019）は戦役固有＝持ち越さない（再シードで作り直す）。
            FinancialHoldingRegistry.Clear();
            PropertyDeedRegistry.Clear();
            LandLeaseRegistry.Clear(); // 土地賃貸借（貸出基盤#2019）も戦役固有
            CollateralLoanRegistry.Clear(); // 資産担保融資（#186）も戦役固有
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
            // 主人公の立身出世（TKO #2477・P1-c）も同じセーブへ（在席ならディレクタから写す）。
            ProtagonistCareerSave career = ProtagonistCareerDirector.Instance != null
                ? ProtagonistCareerDirector.Instance.CaptureSave() : null;
            CampaignSaveManager.SaveSession(StrategySession.Campaign, people, reg, StrategySession.Clock, StrategySession.Provinces, StrategySession.CourtAuthority, career);
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
