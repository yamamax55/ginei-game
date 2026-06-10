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

        [Header("惑星攻城（戦略マップから突入・#131）")]
        [Tooltip("アルテミスの首飾り射程＝接近限界リングの半径（艦隊はここまでしか近づけない）")]
        public float siegeApproachRadius = 5f;
        [Tooltip("攻城艦隊が惑星を取り囲む半径（首飾り射程の外）")]
        public float siegeBesiegerRingRadius = 8.5f;
        [Tooltip("惑星を取り囲む攻城艦隊の数")]
        public int siegeBesiegerCount = 6;
        [Tooltip("攻城艦隊1隊あたりの基準兵力")]
        public int siegeBesiegerFleetStrength = 200;
        [Tooltip("中心の惑星の見た目スケール")]
        public float siegePlanetScale = 3f;

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
            FleetRoster.Clear(); // 艦隊編制台帳(#146)も会戦ごとに作り直す（永続化は #108 で別途）
            OrderOfBattle.Clear(); // 編制ツリー(#147)も会戦ごとに作り直す

            // 戦略マップからの遭遇（実会戦・C-3）が予約されていれば、それを生成して終了
            if (BattleHandoff.Pending)
            {
                if (BattleHandoff.IsSystemView) SetupSystemView();      // 非戦闘＝星系の閲覧（恒星系ビュー）
                else if (BattleHandoff.IsPlanetSiege) SetupPlanetSiege();
                else SetupFromHandoff();
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

                // 艦隊編制（#146）：番号指定があれば台帳へ登録し提督を配属、表示用に番号を持たせる。
                // 未指定（0）なら従来どおり提督名のみ（後方互換）。
                if (entry.fleetNumber > 0)
                {
                    strength.fleetNumber = entry.fleetNumber;
                    strength.fleetUnitName = entry.fleetName;
                    FleetUnitData unit = FleetRoster.CreateFleet(strength.faction, entry.fleetNumber, entry.fleetName);
                    if (unit != null)
                    {
                        unit.factionData = strength.factionData;
                        FleetRoster.AssignAdmiral(unit, entry.admiral); // デモは階級ゲート無し
                    }

                    // 編制ツリー（#147）：軍団・軍集団に編入し、表示用の梯団名を持たせる。
                    if (!string.IsNullOrEmpty(entry.corps))
                    {
                        var corps = OrderOfBattle.GetOrCreate(EchelonType.軍団, strength.faction, entry.corps);
                        OrderOfBattle.AttachFleet(corps.id, entry.fleetNumber);
                        if (!string.IsNullOrEmpty(entry.armyGroup))
                        {
                            var group = OrderOfBattle.GetOrCreate(EchelonType.軍集団, strength.faction, entry.armyGroup);
                            OrderOfBattle.AttachFormation(group.id, corps.id);
                        }
                        strength.corpsName = entry.corps;
                        strength.armyGroupName = entry.armyGroup;
                    }
                }
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

        // ===== 惑星攻城＝戦術マップ突入（#131 PB-1/PB-5）=====

        /// <summary>
        /// 非戦闘のシステムビューを生成する（星系をダブルクリックで入場）。中心に恒星を置き、
        /// 星系名と軌道リング（惑星配置のプレースホルダ）を描くだけ。艦隊・勝利条件は無し。
        /// ★後の宿題（方針=#767）：恒星を中心に第一惑星・第二惑星…を象徴配置し、惑星単位の内政を操作可にする（SystemView へ追加）。
        /// </summary>
        private void SetupSystemView()
        {
            ClearExistingFleets();
            ScenarioData.ActiveScenario = null; // 勝利条件なし（戦闘判定しない）

            var view = new GameObject("SystemView").AddComponent<SystemView>();
            view.systemId = BattleHandoff.systemViewId;
            view.ownerFaction = BattleHandoff.systemViewOwner;
            view.systemName = string.IsNullOrEmpty(BattleHandoff.systemViewName) ? "星系" : BattleHandoff.systemViewName;
            // 恒星の色は所有勢力でうっすら寄せる（無所属＝既定の暖色）
            if (BattleHandoff.systemViewOwner == Faction.同盟) view.starColor = new Color(0.7f, 0.85f, 1f);
            view.Build();
        }

        /// <summary>
        /// 惑星攻城の戦術マップを生成する。中心に惑星(＋アルテミスの首飾り射程＝接近限界リング)、
        /// 攻城艦隊を惑星の周囲に円環状（首飾り射程の外）に配置して惑星へ正対させる。
        /// 艦隊は SiegeArena が射程内へ入れないよう押し出す＝首飾り射程の外までしか近づけない。
        /// </summary>
        private void SetupPlanetSiege()
        {
            if (fleetPrefab == null) { Debug.LogError("BattleSetup: fleetPrefab が未設定です。"); return; }
            ClearExistingFleets();
            ScenarioData.ActiveScenario = null; // 勝利条件なし（攻城の決着は戦略側の TickSieges）

            // 中心の惑星＋首飾り射程リング＋接近限界の押し出し＋攻城進行（S-AV/ゲージ）
            var arena = new GameObject("SiegeArena").AddComponent<SiegeArena>();
            arena.transform.position = Vector3.zero;
            arena.approachRadius = siegeApproachRadius;
            arena.planetScale = siegePlanetScale;
            arena.planetColor = (BattleHandoff.planetOwner == Faction.帝国)
                ? new Color(0.85f, 0.3f, 0.25f) : new Color(0.3f, 0.5f, 0.9f);
            // 中心ラベルは「種別 名称」。要塞/コロニーは種別を前置（惑星は従来どおり名称のみ＝後方互換）。
            string siegeName = string.IsNullOrEmpty(BattleHandoff.planetName) ? "惑星" : BattleHandoff.planetName;
            arena.planetLabel = BattleHandoff.planetKind == Planet.SiegeTargetKind.惑星
                ? siegeName
                : $"{BattleHandoff.planetKind}　{siegeName}";
            arena.besiegerFaction = BattleHandoff.besiegerFaction;
            arena.planetOwner = BattleHandoff.planetOwner;
            arena.initialDefenseRatio = BattleHandoff.planetDefenseRatio;
            arena.initialInvasionRatio = BattleHandoff.planetInvasionRatio;
            arena.Build();

            // 攻城艦隊を惑星の周囲に円環状に配置（首飾り射程の外・惑星へ正対）。突入した艦隊はプレイヤー操作。
            Faction playerFaction = GameSettings.Instance.playerFaction;
            int n = Mathf.Max(1, siegeBesiegerCount);
            float ringR = Mathf.Max(siegeApproachRadius + 1.5f, siegeBesiegerRingRadius);
            for (int i = 0; i < n; i++)
            {
                float ang = (Mathf.PI * 2f / n) * i;
                Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringR;
                var entry = MakeBesiegerEntry(BattleHandoff.besiegerFaction, siegeBesiegerFleetStrength, pos);
                GameObject g = SpawnFleet(entry, playerFaction);
                if (g == null) continue;

                // SpawnFleet の spawnSeparation 倍を無視して、リング上の実位置へ配置
                g.transform.position = new Vector3(pos.x, pos.y, 0f);

                // 惑星（中心）へ正対（前方=Transform.up を中心へ）
                Vector2 dir = -pos;
                float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                g.transform.rotation = Quaternion.Euler(0f, 0f, a);

                // 突入した攻城艦隊はプレイヤーが手動指揮（AIに乗っ取らせない）
                FleetAI ai = g.GetComponent<FleetAI>();
                if (ai != null) ai.enabled = false;
            }

            Debug.Log($"BattleSetup: 惑星攻城マップを生成（{BattleHandoff.planetName} / {BattleHandoff.besiegerFaction} {n}隊が包囲）。");
        }

        /// <summary>攻城艦隊1隊ぶんのエントリ（StrengthScale を掛けない素の基準兵力）。</summary>
        private ScenarioData.FleetEntry MakeBesiegerEntry(Faction f, int baseStrength, Vector2 pos)
        {
            var ad = ScriptableObject.CreateInstance<AdmiralData>();
            ad.admiralName = f + "攻城艦隊";
            ad.faction = f;
            ad.leadership = 50;
            ad.baseStrength = Mathf.Max(1, baseStrength);
            return new ScenarioData.FleetEntry
            {
                admiral = ad,
                faction = f,
                factionData = null,
                spawnPosition = pos,
                formation = Formation.鶴翼陣
            };
        }
    }
}
