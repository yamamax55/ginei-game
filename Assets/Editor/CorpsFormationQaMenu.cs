using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍団隊形（<see cref="CorpsFormation"/>）の実機QA用コマンド。<b>Play 中のみ動作し、何も保存しない</b>
    /// （シーン/プレハブ/アセットに書かない・セーブファイルに触れない）。<see cref="FleetClusterQaMenu"/> の作法に倣う。
    ///
    /// 会戦中の生存艦隊を<b>2つの軍団（QA-A軍団／QA-B軍団）</b>へ一時的に振り分け、
    /// 「A＝横陣／B＝方陣を同時に維持できるか」「A への再命令・前列交代が B へ波及しないか」
    /// 「AI の解決周期（BattlefieldCommandManager）を跨いでも手動指定が戻らないか」を人手で確認できるようにする。
    ///
    /// ここで書き換えるのは実行中のコンポーネント（<see cref="FleetStrength.corpsName"/> と
    /// <see cref="FleetStrength.corpsCommander"/>）だけで、Play を抜ければ元に戻る。撤去コマンドで即座に戻せる。
    /// </summary>
    public static class CorpsFormationQaMenu
    {
        /// <summary>QA で付ける軍団名（撤去はこの名前で判別する）。</summary>
        private const string CorpsA = "QA-A軍団";
        private const string CorpsB = "QA-B軍団";

        // QA で生成した仮の軍団長データ（撤去時に破棄する＝アセットではなくメモリ上のインスタンス）。
        private static readonly List<AdmiralData> tempCommanders = new List<AdmiralData>();

        /// <summary>2軍団の検証に必要な最低艦隊数（2軍団×2隊）。</summary>
        private const int MinFleetsForTwoCorps = 4;
        /// <summary>一時生成で揃える艦隊数（各軍団3隊＝前列交代が見える）。</summary>
        private const int TargetFleetCount = 6;
        /// <summary>複製した艦隊を並べる間隔（ワールド単位）。</summary>
        private const float SpawnSpacing = 9f;

        // QA で一時生成した艦隊（撤去時にこれだけを破棄する＝元からいた艦隊は消さない）。
        private static readonly List<GameObject> spawnedFleets = new List<GameObject>();

        /// <summary>
        /// ★Play を抜けるときに、撤去し忘れた QA の一時オブジェクトを必ず捨てる。
        /// 手で「撤去」を実行しないまま停止しても、仮の提督データ（<see cref="ScriptableObject"/>）や
        /// 複製した艦隊が残らないようにする＝静的な検証状態を残さない。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void HookPlayModeCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode
                && state != PlayModeStateChange.EnteredEditMode) return;

            for (int i = 0; i < tempCommanders.Count; i++)
                if (tempCommanders[i] != null) Object.DestroyImmediate(tempCommanders[i]);
            tempCommanders.Clear();

            for (int i = spawnedFleets.Count - 1; i >= 0; i--)
                if (spawnedFleets[i] != null) Object.DestroyImmediate(spawnedFleets[i]);
            spawnedFleets.Clear();
        }

        [MenuItem("Ginei/QA: 軍団隊形 検証用の会戦を開始（Play中）", false, 318)]
        public static void LaunchTestBattle()
        {
            if (!RequirePlaying()) return;

            // 既存の「複数艦隊の会戦」経路（BattleHandoff.QueueMulti）にそのまま乗せる＝新しい生成系を作らない。
            // シナリオアセットもシーンも作らず、受け渡しに明細を積んで Battle シーンをロードするだけ。
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            Faction enemy = player == Faction.帝国 ? Faction.同盟 : Faction.帝国;

            var entries = new List<BattleHandoff.HandoffFleet>();
            for (int i = 0; i < TargetFleetCount; i++)   // 味方＝6隊（2軍団×3隊）
                entries.Add(new BattleHandoff.HandoffFleet
                {
                    faction = player, strategicStrength = 300, fleetId = 900 + i,
                    loyalty = 1f, intrigue = 0f, quality = 1f, sideA = true,
                });
            for (int i = 0; i < 3; i++)                  // 敵＝3隊（AI が動く相手役）
                entries.Add(new BattleHandoff.HandoffFleet
                {
                    faction = enemy, strategicStrength = 300, fleetId = 950 + i,
                    loyalty = 1f, intrigue = 0f, quality = 1f, sideA = false,
                });

            BattleHandoff.QueueMulti(entries, player, enemy, 900, 950, "Strategy");
            BattleHandoff.battleLabel = "QA 軍団隊形テスト";
            BattleHandoff.battlefield = default;   // 戦場キーなし＝#38 の援軍は入ってこない（切り分けのため）

            Debug.Log($"[CorpsFormationQA] 検証用の会戦を開始します（味方 {TargetFleetCount} 隊 / 敵 3 隊）。" +
                      "シナリオ・シーン・セーブには何も書いていません。");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Battle");
        }

        [MenuItem("Ginei/QA: 軍団隊形 検証用の艦隊を一時生成（Play中）", false, 319)]
        public static void SpawnFleetsForTest()
        {
            if (!RequirePlaying()) return;

            List<FleetStrength> pool = PlayerSideFleets();
            if (pool.Count == 0)
            {
                Report("軍団隊形テスト",
                    "会戦中の味方艦隊が1隊もありません。複製元が要るので、まず会戦（Battle シーン）を開始してください。");
                return;
            }
            if (pool.Count >= TargetFleetCount)
            {
                Report("軍団隊形テスト",
                    $"すでに味方が {pool.Count} 隊います（必要 {TargetFleetCount} 隊）。生成は不要です。\n\n" +
                    "「QA: 軍団隊形テストを仕込む」へ進んでください。");
                return;
            }

            // 元からいる艦隊を<b>複製</b>して足りぶんを補う。プレハブやシーンには触れず、
            // Play を抜ければ消える。撤去コマンドで即座に消せる（元の艦隊は消さない）。
            FleetStrength template = pool[0];
            int need = TargetFleetCount - pool.Count;
            Vector3 basePos = template.transform.position;
            int made = 0;

            for (int i = 0; i < need; i++)
            {
                GameObject clone = Object.Instantiate(template.gameObject);
                if (clone == null) continue;
                // ★hideFlags は付けない。Play 中のシーンはそもそもディスクへ保存されないので
                //   DontSave に利点は無く、逆に「新しいシーンをロードしても破棄されない」性質がつく
                //   ＝Play を抜けるときに片付かず
                //   「Some objects were not cleaned up when closing the scene.」の原因になる。
                clone.hideFlags = HideFlags.None;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, template.gameObject.scene);

                // 元の隊列の横に等間隔で並べる（重ならない＝軍団の集結が見える）。
                clone.transform.position = basePos + new Vector3((i + 1) * SpawnSpacing, 0f, 0f);
                clone.transform.rotation = template.transform.rotation;

                var fs = clone.GetComponent<FleetStrength>();
                if (fs != null)
                {
                    fs.fleetNumber = 900 + i;                 // 既存の艦隊番号と衝突しない帯
                    fs.corpsName = "";                        // 軍団は「仕込む」コマンドが割り当てる
                    fs.armyGroupName = "";
                }
                clone.name = $"QAFleet_{900 + i}";
                spawnedFleets.Add(clone);
                made++;
            }

            Debug.Log($"[CorpsFormationQA] 検証用に {made} 隊を一時生成しました（複製元＝{template.name}）。" +
                      "アセット・シーン・セーブには何も書いていません。");
            Report("軍団隊形テスト",
                $"検証用に {made} 隊を一時生成しました（合計 {pool.Count + made} 隊）。\n\n" +
                "1フレーム待ってから「QA: 軍団隊形テストを仕込む」を実行してください\n" +
                "（生成直後は艦隊レジストリへの登録が済んでいません。ポーズ中なら一度再生してください）。\n\n" +
                "撤去は「QA: 軍団隊形テストを撤去」で、生成した艦隊だけを消します。");
        }

        [MenuItem("Ginei/QA: 軍団隊形テストを仕込む（Play中）", false, 320)]
        public static void Setup()
        {
            if (!RequirePlaying()) return;

            List<FleetStrength> pool = PlayerSideFleets();
            if (pool.Count < MinFleetsForTwoCorps)
            {
                // 通常のアムリッツァ等は味方3隊しかおらず2軍団を作れない。QA では一時生成で補う。
                Report("軍団隊形テスト",
                    $"会戦中の味方艦隊が {pool.Count} 隊しかありません（2軍団×2隊＝最低 {MinFleetsForTwoCorps} 隊必要）。\n\n" +
                    "先に「QA: 軍団隊形 検証用の艦隊を一時生成」を実行してください。\n" +
                    "既存艦隊を複製して Play 中だけ増やします（シーン・アセット・セーブには書きません）。");
                return;
            }

            // 半分ずつ A/B へ振り分け、それぞれ先頭を軍団旗艦（軍団長乗艦）にする。
            int half = pool.Count / 2;
            for (int i = 0; i < pool.Count; i++)
            {
                FleetStrength f = pool[i];
                bool isA = i < half;
                f.corpsName = isA ? CorpsA : CorpsB;
                bool head = (i == 0) || (i == half);
                f.corpsCommander = head ? MakeCorpsCommander(f, isA ? "QA-A軍団長" : "QA-B軍団長") : null;
            }

            Debug.Log($"[CorpsFormationQA] {CorpsA}={half}隊 / {CorpsB}={pool.Count - half}隊 に振り分けました。" +
                      "次に「QA: 軍団隊形 A=横陣・B=方陣 を発令」を実行し、盤面で2つの軍団が別々の隊形を保つか確認してください。");
            Report("軍団隊形テスト",
                $"{CorpsA} = {half} 隊 / {CorpsB} = {pool.Count - half} 隊 に振り分けました。\n\n" +
                "続けて「QA: 軍団隊形 A=横陣・B=方陣 を発令」を実行してください。\n" +
                "確認後は「QA: 軍団隊形テストを撤去」で元に戻せます（セーブ不要・Play を抜けても残りません）。");
        }

        [MenuItem("Ginei/QA: 軍団隊形 A=横陣・B=方陣 を発令（Play中）", false, 321)]
        public static void OrderBoth()
        {
            if (!RequirePlaying()) return;
            CorpsFormation cf = CorpsFormation.Instance;
            if (cf == null) { Report("軍団隊形テスト", "CorpsFormation がありません（Battle シーンで実行してください）。"); return; }

            FleetStrength a = AnchorOf(CorpsA), b = AnchorOf(CorpsB);
            if (a == null || b == null) { Report("軍団隊形テスト", "先に「QA: 軍団隊形テストを仕込む」を実行してください。"); return; }

            // #67：QA 専用の仕込み＝権限判定の対象外（明示）。通常入力からは呼べない。
            cf.FormCorps(a, Formation.横陣, CommandOrderSource.QA);
            cf.FormCorps(b, Formation.方陣, CommandOrderSource.QA);
            DumpStatus("A=横陣・B=方陣 を発令");
        }

        [MenuItem("Ginei/QA: 軍団隊形 A だけ円陣へ再命令（Play中）", false, 322)]
        public static void ReorderAOnly()
        {
            if (!RequirePlaying()) return;
            CorpsFormation cf = CorpsFormation.Instance;
            FleetStrength a = AnchorOf(CorpsA);
            if (cf == null || a == null) { Report("軍団隊形テスト", "先に発令コマンドを実行してください。"); return; }
            cf.FormCorps(a, Formation.円陣, CommandOrderSource.QA);
            DumpStatus("A のみ円陣へ再命令（B が方陣のままなら合格）");
        }

        [MenuItem("Ginei/QA: 軍団隊形 A だけ前列交代（Play中）", false, 323)]
        public static void RotateAOnly()
        {
            if (!RequirePlaying()) return;
            CorpsFormation cf = CorpsFormation.Instance;
            FleetStrength a = AnchorOf(CorpsA);
            if (cf == null || a == null) { Report("軍団隊形テスト", "先に発令コマンドを実行してください。"); return; }
            bool ok = cf.RotateCorpsByKey(CorpsFormation.KeyFor(a), CommandOrderSource.QA);
            DumpStatus(ok ? "A のみ前列交代（B の隊列が動かなければ合格）" : "A に軍団隊形の命令がありません");
        }

        [MenuItem("Ginei/QA: 軍団隊形の状態を出力（Play中）", false, 324)]
        public static void Status()
        {
            if (!RequirePlaying()) return;
            DumpStatus("現在の状態");
        }

        [MenuItem("Ginei/QA: 軍団隊形テストを撤去（Play中）", false, 325)]
        public static void Remove()
        {
            if (!RequirePlaying()) return;

            CorpsFormation cf = CorpsFormation.Instance;
            int cleared = 0;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || (f.corpsName != CorpsA && f.corpsName != CorpsB)) continue;
                string key = CorpsFormation.KeyFor(f);
                // #67：QA 専用の撤去＝権限判定の対象外（明示）。通常入力からは呼べない。
                if (cf != null && !string.IsNullOrEmpty(key))
                    CorpsFormation.ReleaseManualOrder(key, "QA 撤去", CommandOrderSource.QA);
                f.corpsName = "";
                f.corpsCommander = null;
                cleared++;
            }

            for (int i = 0; i < tempCommanders.Count; i++)
                if (tempCommanders[i] != null) Object.DestroyImmediate(tempCommanders[i]);
            tempCommanders.Clear();

            // 一時生成した艦隊だけを消す（元からいた艦隊には触れない）。
            int destroyed = 0;
            for (int i = spawnedFleets.Count - 1; i >= 0; i--)
            {
                if (spawnedFleets[i] == null) continue;
                Object.DestroyImmediate(spawnedFleets[i]);
                destroyed++;
            }
            spawnedFleets.Clear();

            Debug.Log($"[CorpsFormationQA] QA 軍団を {cleared} 隊ぶん撤去し、一時生成した {destroyed} 隊を消しました" +
                      "（アセットには何も書いていません）。");
            Report("軍団隊形テスト",
                $"{cleared} 隊を元に戻し、一時生成した {destroyed} 隊を消しました。");
        }

        // ===== 補助 =====

        private static bool RequirePlaying()
        {
            if (Application.isPlaying) return true;
            EditorUtility.DisplayDialog("軍団隊形テスト", "Play 中に実行してください。", "OK");
            return false;
        }

        /// <summary>プレイヤー勢力の生存戦闘艦隊（同一戦場に限る＝最初に見つけた艦隊のシーンで絞る）。</summary>
        private static List<FleetStrength> PlayerSideFleets()
        {
            var result = new List<FleetStrength>();
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            UnityEngine.SceneManagement.Scene? scene = null;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive || !f.IsCombatant || f.faction != pf) continue;
                if (scene == null) scene = f.gameObject.scene;
                if (f.gameObject.scene != scene.Value) continue; // 別戦場は混ぜない
                result.Add(f);
            }
            return result;
        }

        /// <summary>その軍団の軍団旗艦（無ければ先頭の所属艦隊）。</summary>
        private static FleetStrength AnchorOf(string corpsName)
        {
            FleetStrength fallback = null;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || !f.IsAlive || f.corpsName != corpsName) continue;
                if (f.IsCorpsFlagship) return f;
                if (fallback == null) fallback = f;
            }
            return fallback;
        }

        /// <summary>QA 用の仮の軍団長データ（メモリ上のみ＝アセット化しない。撤去時に破棄）。</summary>
        private static AdmiralData MakeCorpsCommander(FleetStrength host, string name)
        {
            if (host != null && host.corpsCommander != null) return host.corpsCommander; // 既にいるなら流用
            AdmiralData ad = ScriptableObject.CreateInstance<AdmiralData>();
            ad.hideFlags = HideFlags.DontSave;   // ★保存しない
            ad.admiralName = name;
            ad.leadership = 70; ad.attack = 60; ad.defense = 60; ad.mobility = 60;
            ad.intelligence = 70; ad.operation = 60;
            ad.rankTier = 8;
            tempCommanders.Add(ad);
            return ad;
        }

        /// <summary>A/B 両軍団の適用状態（軍団名・隊形・手動/自動・形成中/完了）を Console とダイアログに出す。</summary>
        private static void DumpStatus(string headline)
        {
            string sa = StatusOf(CorpsA), sb = StatusOf(CorpsB);
            string msg = $"{headline}\n  {CorpsA}: {sa}\n  {CorpsB}: {sb}";
            Debug.Log("[CorpsFormationQA] " + msg);
            Report("軍団隊形テスト", msg);
        }

        private static string StatusOf(string corpsName)
        {
            FleetStrength a = AnchorOf(corpsName);
            if (a == null) return "（該当軍団なし）";
            string s = CorpsFormation.StatusFor(a);
            return string.IsNullOrEmpty(s) ? "（命令なし＝AI 自動）" : s;
        }

        private static void Report(string title, string message)
            => EditorUtility.DisplayDialog(title, message, "OK");
    }
}
