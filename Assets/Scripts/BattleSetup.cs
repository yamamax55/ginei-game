using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// シナリオ定義(ScenarioData)に基づいて会戦開始時に艦隊を生成・配置するクラス。
    /// Battle シーンに1つ配置する。
    /// 生成は Awake で行い、BattleManager の隻数カウント(Start)より前に完了させる。
    /// 起動時に手置きの艦隊が残っていても自動でクリアしてから生成する。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BattleSetup : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("生成する艦隊プレハブ（FleetUnit）。FleetAI は無効状態で含まれている想定")]
        public GameObject fleetPrefab;

        [Tooltip("使用するシナリオを直接指定（未設定なら GameSettings.scenarioName で Resources から検索）")]
        public ScenarioData scenarioOverride;

        [Header("配置")]
        [Tooltip("シナリオの生成位置を原点中心に拡大する倍率（大きいほど両軍が離れて開始＝いきなり交戦距離にしない）")]
        public float spawnSeparation = 2.5f;

        private void Awake()
        {
            // Battle シーン以外では一切動作しない（Title 等に誤って置かれても戦闘を始めない）
            if (SceneManager.GetActiveScene().name != "Battle")
            {
                Debug.LogWarning($"BattleSetup: Battle シーン以外では動作しません（現在: {SceneManager.GetActiveScene().name}）。このオブジェクトはこのシーンから削除してください。");
                return;
            }

            // 0. 索敵レジストリを初期化（静的状態がシーン再読込を跨いで残るのを防ぐ）
            FleetRegistry.Clear();

            // 戦略マップからの遭遇（実会戦・C-3）が予約されていれば、それを生成して終了
            if (BattleHandoff.Pending)
            {
                SetupFromHandoff();
                return;
            }

            // 1. シナリオを決定
            ScenarioData scenario = ResolveScenario();
            if (scenario == null)
            {
                Debug.LogWarning("BattleSetup: 対応する ScenarioData が見つかりませんでした。艦隊を生成しません。");
                return;
            }

            // 勝利条件評価のため、解決したシナリオを公開（BattleManager が参照）
            ScenarioData.ActiveScenario = scenario;

            if (fleetPrefab == null)
            {
                Debug.LogError("BattleSetup: fleetPrefab が未設定です。Inspector で艦隊プレハブを割り当ててください。");
                return;
            }

            // 2. 手置き等で既に存在する艦隊をクリア（二重・不要艦の混在を防ぐ）
            ClearExistingFleets();

            // 3. 各エントリから艦隊を生成
            Faction playerFaction = GameSettings.Instance.playerFaction;
            System.Collections.Generic.List<GameObject> spawnedFleets = new System.Collections.Generic.List<GameObject>();
            foreach (var entry in scenario.fleets)
            {
                GameObject fleet = SpawnFleet(entry, playerFaction);
                if (fleet != null) spawnedFleets.Add(fleet);
            }

            // 4. 両軍が互いに正対するよう初期の向きを設定
            OrientFleetsToEnemy(spawnedFleets);

            Debug.Log($"BattleSetup: シナリオ「{scenario.scenarioName}」から {spawnedFleets.Count} 艦隊を生成しました。");
        }

        /// <summary>
        /// 各艦隊を、相手陣営の重心方向（Transform.up=前方）に向ける。両軍が正対して開始する。
        /// </summary>
        private void OrientFleetsToEnemy(System.Collections.Generic.List<GameObject> fleets)
        {
            Vector2 impSum = Vector2.zero; int impN = 0;
            Vector2 allSum = Vector2.zero; int allN = 0;
            foreach (var f in fleets)
            {
                FleetStrength fs = f.GetComponent<FleetStrength>();
                if (fs == null) continue;
                if (fs.faction == Faction.帝国) { impSum += (Vector2)f.transform.position; impN++; }
                else { allSum += (Vector2)f.transform.position; allN++; }
            }
            if (impN == 0 || allN == 0) return; // 片陣営のみなら向きは変更しない

            Vector2 impCentroid = impSum / impN;
            Vector2 allCentroid = allSum / allN;

            foreach (var f in fleets)
            {
                FleetStrength fs = f.GetComponent<FleetStrength>();
                if (fs == null) continue;

                Vector2 enemyCentroid = (fs.faction == Faction.帝国) ? allCentroid : impCentroid;
                Vector2 dir = enemyCentroid - (Vector2)f.transform.position;
                if (dir.sqrMagnitude < 0.0001f) continue;

                // 2D：Z回転のみ。FleetMovement と同じ「Atan2 - 90°」で前方(up)を敵へ向ける
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                f.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// シーンに既に存在する艦隊（手置きなど、これから生成する分以外）を除去します。
        /// SetActive(false) で即座にカウント・戦闘から除外しつつ Destroy します。
        /// </summary>
        private void ClearExistingFleets()
        {
            foreach (var fs in Object.FindObjectsByType<FleetStrength>(FindObjectsSortMode.None))
            {
                // ルート（親が無い艦隊）のみ対象。配下艦は親ごと消える
                if (fs != null && fs.transform.parent == null)
                {
                    fs.gameObject.SetActive(false); // FindObjectsByType(既定)の集計から即除外
                    Destroy(fs.gameObject);
                }
            }
        }

        /// <summary>
        /// 使用する ScenarioData を解決します。
        /// Inspector 指定 > GameSettings.scenarioName 一致(Resources 全走査) の順。
        /// </summary>
        private ScenarioData ResolveScenario()
        {
            if (scenarioOverride != null) return scenarioOverride;

            string targetName = GameSettings.Instance.scenarioName;

            // Resources 配下の全 ScenarioData から scenarioName 一致を探す（ファイル名に依存しない）
            ScenarioData[] all = Resources.LoadAll<ScenarioData>("");
            foreach (var s in all)
            {
                if (s.scenarioName == targetName) return s;
            }

            // フォールバック：ファイル名一致で直接ロード
            return Resources.Load<ScenarioData>(targetName);
        }

        /// <summary>
        /// 1エントリ分の艦隊を生成し、提督・陣営・陣形・AIを設定します。
        /// </summary>
        /// <returns>生成した GameObject（失敗時は null）</returns>
        private GameObject SpawnFleet(ScenarioData.FleetEntry entry, Faction playerFaction)
        {
            if (entry == null) return null;

            // 原点中心に spawnSeparation 倍して両軍を離す（いきなり交戦距離にしない）
            Vector3 pos = new Vector3(entry.spawnPosition.x, entry.spawnPosition.y, 0f) * spawnSeparation;
            GameObject fleet = Instantiate(fleetPrefab, pos, Quaternion.identity);

            // 提督データを適用（名前・兵力・陣営・色）
            FleetStrength strength = fleet.GetComponent<FleetStrength>();
            if (strength != null)
            {
                strength.admiralData = entry.admiral;
                strength.ApplyAdmiralData();          // 名前/兵力/faction(提督由来)/色を反映

                // 勢力を反映：FactionData があればそれを優先（enum も legacyFaction で同期）
                if (entry.factionData != null)
                {
                    strength.factionData = entry.factionData;
                    strength.faction = entry.factionData.legacyFaction;
                }
                else
                {
                    strength.faction = entry.faction; // シナリオの enum 陣営を優先して上書き
                }

                // 反映した勢力で色を再適用
                FactionColor color = fleet.GetComponent<FactionColor>();
                if (color != null) color.ApplyColors();
            }

            // 陣形を設定
            Squadron squadron = fleet.GetComponent<Squadron>();
            if (squadron != null)
            {
                squadron.currentFormation = entry.formation;
            }

            // 武器は必ず有効化（プレハブで無効化されていても発砲できるように）
            FleetWeapon weapon = fleet.GetComponent<FleetWeapon>();
            if (weapon != null) weapon.enabled = true;

            // AI 制御：プレイヤー勢力以外のみ FleetAI を有効化（プレイヤーは Selectable で操作）。
            // ただし主人公（GON-6・isProtagonist）は陣営に関わらず常にプレイヤー操作＝AI無効。
            FleetAI ai = fleet.GetComponent<FleetAI>();
            if (ai != null)
            {
                ai.enabled = ProtagonistRules.ShouldEnableAI(entry.admiral, IsPlayerControlled(entry, playerFaction));
            }

            // 名前を分かりやすく
            string admiralName = entry.admiral != null ? entry.admiral.admiralName : "Unknown";
            fleet.name = $"Fleet_{entry.faction}_{admiralName}";
            return fleet;
        }

        /// <summary>
        /// このエントリがプレイヤー操作かを判定する。
        /// GameSettings.playerFactionData とエントリ FactionData が揃っていれば FactionData 同一性で、
        /// 無ければ旧 enum（entry.faction == playerFaction）で判定する（後方互換）。
        /// </summary>
        private bool IsPlayerControlled(ScenarioData.FleetEntry entry, Faction playerFaction)
        {
            FactionData playerData = GameSettings.Instance.playerFactionData;
            if (playerData != null && entry.factionData != null)
                return entry.factionData == playerData;
            return entry.faction == playerFaction;
        }

        // ===== 戦略マップからの遭遇＝実会戦の生成（C-3）=====

        /// <summary>BattleHandoff の2勢力から会戦を生成する（殲滅勝利・両軍を左右に配置）。</summary>
        private void SetupFromHandoff()
        {
            if (fleetPrefab == null)
            {
                Debug.LogError("BattleSetup: fleetPrefab が未設定です。");
                return;
            }
            ClearExistingFleets();
            ScenarioData.ActiveScenario = null; // 勝利条件は殲滅にフォールバック

            Faction playerFaction = GameSettings.Instance.playerFaction;
            var fleets = new System.Collections.Generic.List<GameObject>();

            var eA = MakeHandoffEntry(BattleHandoff.factionA, BattleHandoff.admiralA, BattleHandoff.strengthA, new Vector2(-6f, 0f));
            var eB = MakeHandoffEntry(BattleHandoff.factionB, BattleHandoff.admiralB, BattleHandoff.strengthB, new Vector2(6f, 0f));
            GameObject ga = SpawnFleet(eA, playerFaction);
            GameObject gb = SpawnFleet(eB, playerFaction);
            if (ga != null) fleets.Add(ga);
            if (gb != null) fleets.Add(gb);

            OrientFleetsToEnemy(fleets);
            Debug.Log($"BattleSetup: 戦略の遭遇から実会戦を生成（{BattleHandoff.factionA} {BattleHandoff.strengthA} vs {BattleHandoff.factionB} {BattleHandoff.strengthB}）。");
        }

        /// <summary>遭遇の1勢力ぶんのエントリを作る。提督が無ければ戦略兵力から臨時提督を生成。</summary>
        private ScenarioData.FleetEntry MakeHandoffEntry(Faction f, AdmiralData provided, int strategicStrength, Vector2 pos)
        {
            AdmiralData ad = provided;
            if (ad == null)
            {
                ad = ScriptableObject.CreateInstance<AdmiralData>();
                ad.admiralName = f + "艦隊";
                ad.faction = f;
                ad.leadership = 50; // maxStrength ≒ baseStrength（戦略兵力をそのまま反映）
                ad.baseStrength = Mathf.Max(1, strategicStrength) * BattleHandoff.StrengthScale;
            }
            return new ScenarioData.FleetEntry
            {
                admiral = ad,
                faction = f,
                factionData = null,
                spawnPosition = pos,
                formation = Formation.紡錘陣
            };
        }
    }
}
