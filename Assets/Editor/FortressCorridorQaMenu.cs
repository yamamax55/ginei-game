using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞（#40 C-7）を実機で検証するための QA メニュー。
    ///
    /// <b>Play 中のみ動作し、セーブファイルには一切書かない</b>（盤面のメモリ上の状態だけを触る）。
    /// Play を止めれば元の保存データのまま＝既存セーブを汚さない。
    /// ChatGPT の実機 QA が「要塞を据える→敵艦隊をぶつける→素通りしないことを確かめる→制圧させる」を
    /// 手早く再現できるようにする。<see cref="FleetClusterQaMenu"/> と同じ作法。
    /// </summary>
    public static class FortressCorridorQaMenu
    {
        // ★メニューのパスに「半角スペース＋#」を入れないこと。Unity は空白区切りの %#&_ を
        // ショートカット指定として解釈するため、"回廊要塞 #40" は "回廊要塞" ＋ Shift+4 と読まれ、
        // 全項目の表示名が同じ "回廊要塞" に潰れて「同名のメニューが既にある」で登録に失敗する（実機で警告9件）。
        private const string Root = "Ginei/QA（Play中のみ・保存しない）/回廊要塞40/";

        // ===== 盤面の把握 =====

        [MenuItem(Root + "要塞と回廊の状態をダンプ", false, 200)]
        public static void DumpFortresses()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            GalaxyMap map = view.Map;
            if (map == null) { Debug.LogWarning("[QA #40] GalaxyMap がまだありません。"); return; }

            var sb = new StringBuilder();
            sb.AppendLine("=== [QA #40] 回廊と要塞 ===");
            int fortified = 0, commerce = 0;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null) continue;
                string a = NameOf(map, c.aId), b = NameOf(map, c.bId);
                if (c.fortress == null)
                {
                    commerce++;
                    continue; // 要塞なし＝フェザーン型は数だけ数える（多いので個別には出さない）
                }
                fortified++;
                Fortress f = c.fortress;
                bool blocksAlliance = StrategyRules.IsFortressBlocked(c, Faction.同盟);
                bool blocksEmpire = StrategyRules.IsFortressBlocked(c, Faction.帝国);
                sb.AppendLine($"[要塞] {f.fortressName}　{a}–{b}（{c.type}）");
                sb.AppendLine($"  所有={f.owner}　守備={f.garrisonStrength:0}　シールド={f.shieldIntegrity:0.00}" +
                              $"　主砲={f.mainGunPower:0}　扼する={f.controlsCorridor}");
                sb.AppendLine($"  封鎖：同盟={blocksAlliance}／帝国={blocksEmpire}" +
                              $"　実効防御={FortressRules.EffectiveDefense(f):0}" +
                              $"　力攻めに要る兵力={FortressRules.EffectiveDefense(f) * FortressParams.Default.assaultRatio:0}");
                bool bypass = FortressBlockadeRules.HasBypass(map, c, Faction.同盟, out List<int> route);
                sb.AppendLine(bypass
                    ? $"  戦略的な迂回路：あり {string.Join("→", route)}（別の通商回廊で回り込める＝仕様どおり）"
                    : "  戦略的な迂回路：なし（この回廊が唯一の道）");
            }
            sb.AppendLine($"合計：要塞つき回廊 {fortified} 本／要塞なし（通商）回廊 {commerce} 本");
            Debug.Log(sb.ToString());
        }

        [MenuItem(Root + "封鎖されている艦隊を一覧", false, 201)]
        public static void DumpBlockadedFleets()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            StrategicFleetRegistry reg = view.Registry;
            if (reg == null) { Debug.LogWarning("[QA #40] 艦隊レジストリがまだありません。"); return; }

            var sb = new StringBuilder();
            sb.AppendLine("=== [QA #40] 要塞に足止めされている艦隊 ===");
            int n = 0;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.IsBlockadedByFortress) continue;
                n++;
                sb.AppendLine($"第{f.id}艦隊　{f.faction}　兵力{f.strength}" +
                              $"　{NameOf(view.Map, f.currentSystemId)}→{NameOf(view.Map, f.destinationSystemId)}");
            }
            if (n == 0) sb.AppendLine("（該当なし）");
            Debug.Log(sb.ToString());
        }

        // ===== 盤面を作る（メモリ上のみ）=====

        [MenuItem(Root + "前線の要衝へ帝国の要塞を据える", false, 220)]
        public static void PlaceFortressOnFrontline()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            GalaxyMap map = view.Map;
            if (map == null) return;

            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress != null) continue;
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                if (!FactionRelations.IsHostile(null, a.owner, null, b.owner)) continue;

                Faction owner = a.owner == Faction.帝国 ? a.owner : b.owner;
                c.fortress = new Fortress(1200f, 740f, 1f, true)
                { owner = owner, fortressName = "QA要塞" };
                c.type = CorridorType.要衝;
                Debug.Log($"[QA #40] {NameOf(map, c.aId)}–{NameOf(map, c.bId)} に QA要塞（{owner}）を据えました。" +
                          "　※モデル表示は次回の盤面再構築から。封鎖判定は即時に効きます。");
                return;
            }
            Debug.LogWarning("[QA #40] 要塞を据えられる前線の回廊が見つかりませんでした。");
        }

        [MenuItem(Root + "要塞の守備を 1 に弱める（制圧を試しやすくする）", false, 221)]
        public static void WeakenFortresses()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            ForEachFortress(view, f =>
            {
                f.garrisonStrength = 1f;
                f.shieldIntegrity = 0.05f;
                Debug.Log($"[QA #40] {f.fortressName} の守備を 1・シールドを 0.05 に弱めました。");
            });
        }

        [MenuItem(Root + "要塞の守備を 1200 に戻す（難攻不落を試す）", false, 222)]
        public static void RestoreFortresses()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            ForEachFortress(view, f =>
            {
                FortressBlockadeRules.Regarrison(f, f.owner, 1200f, 1f);
                Debug.Log($"[QA #40] {f.fortressName} の守備を 1200・シールドを 1.00 に戻しました。");
            });
        }

        [MenuItem(Root + "要塞を同盟へ明け渡す（再占領の整合を試す）", false, 223)]
        public static void HandOverToAlliance()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            ForEachFortress(view, f =>
            {
                FortressBlockadeRules.Regarrison(f, Faction.同盟, 900f, 0.6f);
                Debug.Log($"[QA #40] {f.fortressName} を同盟の要塞にしました（今度は帝国が通れなくなります）。");
            });
        }

        [MenuItem(Root + "要塞の守備を全滅させる（通行が開くか確認）", false, 224)]
        public static void DestroyGarrisons()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            ForEachFortress(view, f =>
            {
                f.garrisonStrength = 0f;
                f.controlsCorridor = false;
                Debug.Log($"[QA #40] {f.fortressName} の守備を全滅させました（回廊が開くはずです）。");
            });
        }

        // ===== 要塞戦の再現準備（Play 中のみ・保存しない）=====

        [MenuItem(Root + "要塞戦の再現準備（足止め1隊＋予備1隊）", false, 230)]
        public static void StageFortressBattle()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            GalaxyMap map = view.Map;
            StrategicFleetRegistry reg = view.Registry;
            if (map == null || reg == null) return;

            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            // 1) プレイヤーにとって封鎖されている要塞回廊を探す。
            Corridor target = null;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                if (!StrategyRules.IsFortressBlocked(c, player)) continue;
                target = c; break;
            }
            if (target == null)
            {
                Debug.LogWarning("[QA #40] 自軍を封じている要塞回廊が見つかりません。" +
                                 "先に「前線の要衝へ帝国の要塞を据える」を実行してください。");
                return;
            }

            // 2) 自軍側の入口を決める（要塞所有者側でないほうの端）。
            StarSystem a = map.GetSystem(target.aId), b = map.GetSystem(target.bId);
            int entry = (a != null && a.owner == target.fortress.owner) ? target.bId : target.aId;
            int far = entry == target.aId ? target.bId : target.aId;

            // 3) 停泊中の自軍艦隊を2隊確保する（1隊＝足止め役、1隊＝援軍の予備）。
            var idle = new List<StrategicFleet>();
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != player) continue;
                if (f.IsOnCorridor || f.engaged || f.warpingAsReinforcement || f.strength <= 0) continue;
                idle.Add(f);
                if (idle.Count >= 2) break;
            }
            if (idle.Count == 0)
            {
                Debug.LogWarning("[QA #40] 停泊中の自軍艦隊がありません（足止め役に1隊必要）。");
                return;
            }

            // 4) 足止め役を入口へ置き、回廊へ入れて一気に進める＝要塞の手前で自動的に止まる。
            //    ここで盤面の経路規則は一切いじらない（本体の迂回可否は元のまま）。
            StrategicFleet pinned = idle[0];
            pinned.currentSystemId = entry;
            if (!pinned.BeginWarp(map, far))
            {
                Debug.LogWarning($"[QA #40] 第{pinned.id}艦隊を回廊へ入れられませんでした。");
                return;
            }
            pinned.Tick(map, 100000f);   // 封鎖上限まで前進＝要塞の手前で釘付けになる

            // 5) 予備は入口の星系に停泊させておく（艦隊メニューから援軍として送れる状態）。
            StrategicFleet reserve = idle.Count > 1 ? idle[1] : null;
            if (reserve != null) reserve.currentSystemId = entry;

            string where = $"{NameOf(map, target.aId)}–{NameOf(map, target.bId)}";
            string msg = $"[QA #40] 要塞戦の準備ができました。\n" +
                         $"　対象回廊：{where}（{target.fortress.fortressName}／{target.fortress.owner}）\n" +
                         $"　足止め役：第{pinned.id}艦隊（兵力{pinned.strength}）＝要塞の手前で停止" +
                         $"　足止め判定={pinned.IsBlockadedByFortress}\n" +
                         (reserve != null
                             ? $"　予備：第{reserve.id}艦隊（兵力{reserve.strength}）＝{NameOf(map, entry)} に停泊\n"
                             : "　予備：停泊中の空き艦隊が無いため用意できませんでした\n") +
                         $"次の操作：\n" +
                         $"　① {where} の回廊を<b>ダブルクリック</b>して要塞戦の戦術マップへ入る\n" +
                         $"　② 戦略に戻り、艦隊メニュー →「② 援軍を送る」→ {where} を選んで予備を派遣";
            Debug.Log(msg.Replace("<b>", "").Replace("</b>", ""));
            EditorUtility.DisplayDialog("QA #40 要塞戦の準備", msg.Replace("<b>", "").Replace("</b>", ""), "OK");
        }

        [MenuItem(Root + "足止めを解除して艦隊を入口へ戻す", false, 231)]
        public static void UnstageFortressBattle()
        {
            if (!RequirePlay(out GalaxyView view)) return;
            StrategicFleetRegistry reg = view.Registry;
            if (reg == null) return;

            int n = 0;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.IsBlockadedByFortress) continue;
                // 出発元の星系へ戻す（回廊から降ろす）。盤面のルールは変えない。
                int home = f.currentSystemId;
                f.engaged = false;
                f.WarpTo(view.Map, home);   // 同一星系＝回廊から降りて停泊に戻る
                f.currentSystemId = home;
                n++;
            }
            Debug.Log($"[QA #40] 足止めされていた {n} 隊を入口の星系へ戻しました。");
        }

        // ===== 戦術マップの幾何チェック（迂回不可の担保）=====

        [MenuItem(Root + "戦術アリーナの幾何を検査（外周を回り込めないか）", false, 240)]
        public static void CheckArenaGeometry()
        {
            var arena = Object.FindAnyObjectByType<CorridorFortressArena>();
            var b = arena != null
                ? new CorridorArenaRules.CorridorArenaBounds(arena.channelHalfWidth, arena.channelHalfLength,
                                                             arena.fortressX, arena.fortressBlockRadius,
                                                             arena.breakthroughX)
                : CorridorArenaRules.CorridorArenaBounds.Default;

            string where = arena != null ? "Play 中のアリーナ" : "既定値（アリーナ未生成）";
            bool noGap = CorridorArenaRules.LeavesNoGap(b);
            bool impossible = CorridorArenaRules.BreakthroughImpossibleWhileHeld(b);

            Debug.Log($"=== [QA #40] 戦術アリーナの幾何（{where}）===\n" +
                      $"水路の半幅={b.channelHalfWidth}　半長={b.channelHalfLength}\n" +
                      $"要塞 x={b.fortressX}　塞ぐ半径={b.fortressRadius}　突破線 x={b.breakthroughX}\n" +
                      $"要塞と岩壁の隙間なし＝{noGap}（false だと横をすり抜けられます）\n" +
                      $"要塞が健在なかぎり突破不能＝{impossible}");
            if (!noGap || !impossible)
                Debug.LogError("[QA #40] 迂回不可の前提が崩れています（寸法の設定ミス）。");
        }

        // ===== 補助 =====

        private static bool RequirePlay(out GalaxyView view)
        {
            view = null;
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("QA #40 回廊要塞",
                    "このメニューは Play 中のみ使えます（保存はしません）。", "OK");
                return false;
            }
            view = Object.FindAnyObjectByType<GalaxyView>();
            if (view == null)
            {
                Debug.LogWarning("[QA #40] Strategy シーンの GalaxyView が見つかりません。戦略マップで実行してください。");
                return false;
            }
            return true;
        }

        private static void ForEachFortress(GalaxyView view, System.Action<Fortress> act)
        {
            GalaxyMap map = view.Map;
            if (map == null || map.corridors == null) return;
            int n = 0;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                act(c.fortress);
                n++;
            }
            if (n == 0) Debug.LogWarning("[QA #40] 盤面に回廊要塞がありません。先に「前線の要衝へ帝国の要塞を据える」を実行してください。");
        }

        private static string NameOf(GalaxyMap map, int id)
        {
            StarSystem s = map != null ? map.GetSystem(id) : null;
            return s != null && !string.IsNullOrEmpty(s.systemName) ? s.systemName : $"#{id}";
        }
    }
}
