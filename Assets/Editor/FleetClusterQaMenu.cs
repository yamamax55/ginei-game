using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊集約の実機QA用コマンド（#戦略MAPの艦艇表示）。多数の艦隊が1星系に集中した状態は
    /// 通常のプレイでは作りにくいので、<b>Play 中に一時的な艦隊を撒いて</b>集約→一覧→最終行の個別選択
    /// →右クリック進軍まで通しで確認できるようにする。
    ///
    /// <b>セーブには触れない</b>：追加するのは実行中のセッション（<see cref="StrategySession.Reg"/>）だけで、
    /// ファイルの読み書きは一切しない。撤去コマンドで完全に消せる。
    /// ※ただし追加したまま手動セーブ（F5 / メニューのセーブ）を行うと、その時点の盤面として保存されてしまう。
    ///   QA 中はセーブせず、確認が済んだら「撤去」してから通常の操作に戻ること。
    /// </summary>
    public static class FleetClusterQaMenu
    {
        /// <summary>QA で撒いた艦隊の目印（撤去はこの名前で判別する）。</summary>
        private const string QaTag = "QA-CLUSTER";
        /// <summary>QA 艦隊の id はこの値から採る（既存 id と衝突させない）。</summary>
        private const int QaIdBase = 900000;

        [MenuItem("Ginei/QA: 艦隊集中テストを撒く（Play中）", false, 300)]
        public static void Spawn()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("艦隊集中テスト", "Play 中に実行してください。", "OK");
                return;
            }
            var reg = StrategySession.Reg;
            var map = StrategySession.Map;
            if (reg == null || map == null || map.systems == null || map.systems.Count == 0)
            {
                EditorUtility.DisplayDialog("艦隊集中テスト", "戦略マップがまだ構築されていません。", "OK");
                return;
            }

            // プレイヤー勢力の星系を1つ選び、そこへ大量の艦隊を置く（集約の見え方と一覧の限界を確認する）。
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            StarSystem target = null;
            for (int i = 0; i < map.systems.Count; i++)
                if (map.systems[i] != null && map.systems[i].owner == pf) { target = map.systems[i]; break; }
            if (target == null) target = map.systems[0];

            const int count = 32; // 「20〜50隊」の帯域で一覧のスクロールと最終行選択を確認できる数
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                var f = new StrategicFleet
                {
                    id = QaIdBase + i,
                    faction = pf,
                    strength = 60 + (i * 7) % 240,
                    currentSystemId = target.id,
                    destinationSystemId = target.id,
                    corpsName = QaTag,
                    isCorpsFlagship = (i % 8 == 0),
                };
                reg.fleets.Add(f);
                added++;
            }

            Debug.Log($"[FleetClusterQA] {target.systemName} に QA 艦隊 {added} 隊を追加しました。" +
                      "集約マーカーをクリック→一覧をスクロール→最終行を選択→盤面を右クリックで進軍、を確認してください。" +
                      "確認後は「QA: 艦隊集中テストを撤去」で消してください（この状態でセーブしないこと）。");
            EditorUtility.DisplayDialog("艦隊集中テスト",
                $"{target.systemName} に {added} 隊を追加しました。\n\n" +
                "集約マーカー → 一覧 → 最終行の選択 → 右クリック進軍 を確認できます。\n" +
                "確認後は「QA: 艦隊集中テストを撤去」で消してください（この状態でセーブしないこと）。", "OK");
        }

        [MenuItem("Ginei/QA: マップ窓ドラッグの受信ログ 切替", false, 305)]
        public static void ToggleDragLog()
        {
            MapWindowDrag.LogEvents = !MapWindowDrag.LogEvents;
            string state = MapWindowDrag.LogEvents ? "ON" : "OFF";
            Debug.Log($"[MapWindowDrag] 受信ログ {state}。ON の間はタイトルバー/グリップの " +
                      "PointerDown・OnDrag・反映 delta が Console に出ます。" +
                      "①何も出ない＝イベント自体が届いていない ②PointerDown だけ出る＝移動が届いていない " +
                      "③反映 delta は出るのに窓が動かない＝窓側のクランプ、の切り分けに使ってください。");
            EditorUtility.DisplayDialog("ドラッグ受信ログ", $"受信ログを {state} にしました。", "OK");
        }

        [MenuItem("Ginei/QA: 艦隊集中テストを撤去", false, 301)]
        public static void Remove()
        {
            var reg = StrategySession.Reg;
            if (reg == null || reg.fleets == null)
            {
                EditorUtility.DisplayDialog("艦隊集中テスト", "対象がありません。", "OK");
                return;
            }

            var keep = new List<StrategicFleet>(reg.fleets.Count);
            int removed = 0;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f != null && f.corpsName == QaTag && f.id >= QaIdBase) { removed++; continue; }
                keep.Add(f);
            }
            reg.fleets.Clear();
            reg.fleets.AddRange(keep);

            Debug.Log($"[FleetClusterQA] QA 艦隊を {removed} 隊 撤去しました。");
            EditorUtility.DisplayDialog("艦隊集中テスト", $"{removed} 隊を撤去しました。", "OK");
        }
    }
}
