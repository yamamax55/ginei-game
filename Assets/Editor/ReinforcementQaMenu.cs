using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 援軍（ワープイン・#38 C-5）を実機で検証するための QA メニュー。
    ///
    /// <b>Play 中のみ動作し、セーブファイルには一切書かない</b>（台帳と盤面のメモリ上の状態だけを触る）。
    /// 到着まで待つ検証は時間がかかるので、「到着予定を今すぐに寄せる」操作を用意して待ち時間を潰す。
    /// </summary>
    public static class ReinforcementQaMenu
    {
        // ★メニューのパスに「半角スペース＋#」を入れないこと（Unity がショートカット指定と解釈する）。
        // 詳細は FortressCorridorQaMenu の同じ定数のコメントを参照。
        private const string Root = "Ginei/QA（Play中のみ・保存しない）/援軍38/";

        [MenuItem(Root + "航行中の援軍を一覧", false, 300)]
        public static void DumpPending()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            var ledger = StrategySession.Reinforcements;
            if (ledger == null) { Debug.LogWarning("[QA #38] 援軍台帳がありません。"); return; }

            var all = new List<WarpReinforcement>();
            ledger.PeekAll(all);

            var sb = new StringBuilder();
            sb.AppendLine("=== [QA #38] 航行中の援軍 ===");
            sb.AppendLine($"台帳の現在時刻（game-秒）＝{ledger.Elapsed:0.0}　閉じた戦場＝{ledger.ClosedCount}");
            if (all.Count == 0) sb.AppendLine("（航行中の援軍はありません）");
            for (int i = 0; i < all.Count; i++)
            {
                WarpReinforcement o = all[i];
                float remain = WarpReinforcementRules.RemainingSeconds(o.arrivalTime, ledger.Elapsed);
                sb.AppendLine($"[{o.id}] {o.faction} 第{o.fleetId}艦隊　兵力{o.strength}　" +
                              $"宛先={Where(view, o.battlefield)}　" +
                              $"到着={o.arrivalTime:0.0}（あと {remain:0.0} 秒）　" +
                              $"進捗={WarpReinforcementRules.ArrivalProgress(o.dispatchTime, o.arrivalTime, ledger.Elapsed):0.00}");
            }
            sb.AppendLine($"帰投待ち（差し戻しキュー）＝{ReinforcementReturnQueue.Count} 件");
            Debug.Log(sb.ToString());
        }

        [MenuItem(Root + "交戦中の回廊を一覧（援軍の宛先候補）", false, 301)]
        public static void DumpEngagedCorridors()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            StrategicFleetRegistry reg = view.Registry;
            if (reg == null) return;

            var seen = new HashSet<long>();
            var sb = new StringBuilder();
            sb.AppendLine("=== [QA #38] 交戦中の戦場（ここへ援軍を送れる）===");
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.engaged) continue;
                var key = BattlefieldKey.Corridor(f.currentSystemId, f.destinationSystemId);
                if (!seen.Add(key.Encode())) continue;
                sb.AppendLine($"{Where(view, key)}　（{f.faction} 第{f.id}艦隊 ほかが交戦中）");
            }
            if (seen.Count == 0) sb.AppendLine("（交戦中の戦場はありません。まず敵艦隊とぶつけてください）");
            Debug.Log(sb.ToString());
        }

        [MenuItem(Root + "自軍の停泊艦隊を交戦中の戦場へ全部派遣", false, 320)]
        public static void DispatchAllIdle()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            StrategicFleetRegistry reg = view.Registry;
            if (reg == null) return;

            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            // 最初に見つかった交戦中の回廊を宛先にする。
            int a = -1, b = -1;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.engaged || !f.IsOnCorridor) continue;
                a = f.currentSystemId; b = f.destinationSystemId; break;
            }
            if (a < 0) { Debug.LogWarning("[QA #38] 交戦中の回廊がありません。先に敵艦隊とぶつけてください。"); return; }

            int sent = 0;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != player) continue;
                if (f.engaged || f.IsOnCorridor || f.warpingAsReinforcement) continue;
                if (view.DispatchReinforcement(f, a, b)) sent++;
            }
            Debug.Log($"[QA #38] 停泊中の自軍 {sent} 隊を {a}–{b} の戦場へ派遣しました。" +
                      "　※到着まで銀河時間がかかります（「到着を10秒後に寄せる」で短縮できます）。");
        }

        [MenuItem(Root + "到着を10秒後に寄せる（待ち時間の短縮）", false, 321)]
        public static void HastenArrivals()
        {
            if (!RequirePlay(out _)) return;
            var ledger = StrategySession.Reinforcements;
            if (ledger == null) return;

            var all = new List<WarpReinforcement>();
            ledger.PeekAll(all);
            if (all.Count == 0) { Debug.LogWarning("[QA #38] 航行中の援軍がありません。"); return; }

            // 取り消して同じ内容を「10秒後 到着」で入れ直す（台帳の内部時刻は動かさない＝時計は本物のまま）。
            for (int i = 0; i < all.Count; i++)
            {
                WarpReinforcement o = all[i];
                if (!ledger.Cancel(o.id, out _)) continue;
                ledger.DispatchAt(o.battlefield, o.faction, o.fleetId, o.strength, ledger.Elapsed + 10.0);
            }
            Debug.Log($"[QA #38] {all.Count} 件の到着を 10 秒後に寄せました。");
        }

        [MenuItem(Root + "航行中の援軍を全部取り消す（盤面へ戻す）", false, 322)]
        public static void CancelAll()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            var ledger = StrategySession.Reinforcements;
            StrategicFleetRegistry reg = view.Registry;
            if (ledger == null || reg == null) return;

            var all = new List<WarpReinforcement>();
            ledger.PeekAll(all);
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!ledger.Cancel(all[i].id, out WarpReinforcement o)) continue;
                StrategicFleet f = reg.GetFleet(o.fleetId);
                if (f != null) f.warpingAsReinforcement = false;
                n++;
            }
            Debug.Log($"[QA #38] {n} 件の派遣を取り消し、艦隊を盤面へ戻しました。");
        }

        private static string Where(GalaxyView view, BattlefieldKey key)
        {
            GalaxyMap map = view != null ? view.Map : null;
            string A = Name(map, key.systemA), B = Name(map, key.systemB);
            return key.IsSystemBattle ? $"{A} 星系" : $"{A}–{B} 回廊";
        }

        private static string Name(GalaxyMap map, int id)
        {
            StarSystem s = map != null ? map.GetSystem(id) : null;
            return s != null && !string.IsNullOrEmpty(s.systemName) ? s.systemName : $"#{id}";
        }

        private static bool RequirePlay(out GalaxyView view)
        {
            view = null;
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("QA #38 援軍", "このメニューは Play 中のみ使えます（保存はしません）。", "OK");
                return false;
            }
            view = Object.FindAnyObjectByType<GalaxyView>();
            if (view == null)
            {
                Debug.LogWarning("[QA #38] Strategy シーンの GalaxyView が見つかりません。戦略マップで実行してください。");
                return false;
            }
            return true;
        }
    }
}
