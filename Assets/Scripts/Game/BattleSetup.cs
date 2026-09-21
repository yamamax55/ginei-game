using Ginei.Data;
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

        [Header("旗艦画像（FSH-3）")]
        [Tooltip("勢力別旗艦画像を会戦内で表示する高さ。rootは拡縮せず、FlagshipBody子だけをこのworld unit高へ合わせる。")]
        public float flagshipVisualHeight = 1.2f;
        [Tooltip("旗艦画像の艦首をTransform.upへ合わせるZ回転補正（度）。現行画像は上向きなので0。")]
        public float flagshipVisualRotationOffset;

        [Header("配置")]
        [Tooltip("シナリオの生成位置を原点中心に拡大する倍率（大きいほど両軍が離れて開始＝いきなり交戦距離にしない）")]
        public float spawnSeparation = 2.5f;

        [Header("増援（時間差投入・#2182）")]
        [Tooltip("増援が出現する戦場端の半径")]
        public float reinforcementEdgeRadius = 135f;

        // 増援の時限スポーン（game-time で経過を計る＝倍速/ポーズ追従）。
        private readonly System.Collections.Generic.List<ScenarioData.FleetEntry> pendingReinforcements = new System.Collections.Generic.List<ScenarioData.FleetEntry>();
        private float reinforcementElapsed;
        private Faction reinforcementPlayerFaction;

        // 固定会戦QA（SPEED-08）が自分の使い捨てシーンに限って時限増援の実経路を使うための許可。
        // 既定＝無し＝従来どおり（Battle シーン以外の Awake は警告して何もしない）。
        private static Scene qaHostScene;

        /// <summary>このシーンに置いた個体だけ、QAの時限増援の予約を受け付ける（QA専用・終了時に <see cref="ClearQaHostScene"/>）。</summary>
        public static void AllowQaHostScene(Scene scene) => qaHostScene = scene;

        /// <summary>QAの許可を解く。</summary>
        public static void ClearQaHostScene() => qaHostScene = default;

        /// <summary>QAの許可が残っているか（試験・復元確認用）。</summary>
        public static bool HasQaHostScene => qaHostScene.IsValid();

        private static bool IsQaHost(Scene scene) => qaHostScene.IsValid() && scene == qaHostScene && scene.isLoaded;

        /// <summary>予約中の時限増援の件数（観測用）。</summary>
        public int PendingReinforcementCount => pendingReinforcements.Count;
        /// <summary>時限増援で実際に生成した件数（観測用）。</summary>
        public int SpawnedReinforcementCount { get; private set; }
        /// <summary>時限増援の経過（game-time・観測用）。</summary>
        public float ReinforcementElapsed => reinforcementElapsed;

        /// <summary>時限増援が出現した直後（配置・向きを決めた後・通知の前）に呼ぶ。null＝何もしない（通常会戦は未使用）。</summary>
        public System.Action<GameObject, ScenarioData.FleetEntry> ReinforcementSpawned { get; set; }

        /// <summary>通知の送り先。null＝従来どおり <see cref="NotificationCenter"/>（QAはローカルログへ差し替える）。</summary>
        public System.Action<NotificationCategory, NotificationSeverity, string> NotificationSink { get; set; }

        /// <summary>
        /// 固定会戦QA専用：時限増援を1件予約する（シナリオ解決・開戦時の生成は通らない）。以後は通常と同じ
        /// <see cref="Update"/>→<see cref="SpawnReinforcement"/>→<see cref="SpawnFleet"/> で生成される。
        /// QA許可のシーンの個体で、到着遅延&gt;0・fleetPrefab 設定済みのときだけ受け付ける。
        /// </summary>
        public bool ScheduleReinforcementForQa(ScenarioData.FleetEntry entry, Faction playerFaction)
        {
            if (!IsQaHost(gameObject.scene) || entry == null || fleetPrefab == null || !(entry.reinforcementDelay > 0f)) return false;
            reinforcementPlayerFaction = playerFaction;
            pendingReinforcements.Add(entry);
            return true;
        }

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
            // 固定会戦QAの使い捨てシーン：通常の初期化はせず、QAが予約する時限増援だけを扱う（台帳のクリアもしない）。
            if (IsQaHost(gameObject.scene)) return;

            // Battle シーン以外では一切動作しない（Title 等に誤って置かれても戦闘を始めない）。
            // additive ロード（WIN-1 ウィンドウ化会戦）では active シーンが Strategy のため、自分のシーン名で判定する。
            if (gameObject.scene.name != "Battle")
            {
                Debug.LogWarning($"BattleSetup: Battle シーン以外では動作しません（現在: {gameObject.scene.name}）。このオブジェクトはこのシーンから削除してください。");
                return;
            }

            // ウィンドウ化（複数同時）会戦か＝additive ロードで自分のシーンが active でない（フルスクリーンは active＝自分）。
            // ※BattleViewport.Active はカーソルが窓上のときだけ true になる「フォーカス」状態で、ここの判定には使えない
            //   （過去バグ：2つ目の会戦の初期化時にカーソルが窓外だと FleetRegistry.Clear() が走り1つ目の艦を全消去していた）。
            bool windowed = gameObject.scene != SceneManager.GetActiveScene();

            // 0. 索敵レジストリの初期化。**常に自分のシーンの艦だけをクリア**する（FleetRegistry.Clear() の全消去は使わない）。
            //    こうすれば、2つ目の会戦のセットアップが1つ目の会戦の旗艦を消す事故が原理的に起きない
            //    （windowed 判定がどう転んでも自シーン以外には触れない＝複数同時会戦の安全策）。
            //    フルスクリーン会戦でも「自シーン＝Battle の全艦」なので従来と同義。
            FleetRegistry.ClearScene(gameObject.scene);
            FortressRegistry.ClearScene(gameObject.scene); // 要塞在庫も自シーン分をクリア（#78）

            // FleetRoster/OrderOfBattle/ShipNameRegistry は static 単一の台帳。フルスクリーン会戦では会戦ごとに
            // 作り直すが、ウィンドウ化会戦（複数同時・戦略マップ同居）で全クリアすると戦略や他会戦の台帳を壊すため、
            // ウィンドウ時はクリアしない（台帳の会戦ごと分離は今後・WIN-4）。
            if (!windowed)
            {
                FleetRoster.Clear(); // 艦隊編制台帳(#146)
                OrderOfBattle.Clear(); // 編制ツリー(#147)
                ShipNameRegistry.Clear(); // 旗艦名(#旗艦名)の払い出し
            }

            // 戦略マップからの遭遇（実会戦・C-3）が予約されていれば、それを生成して終了
            if (BattleHandoff.Pending)
            {
                if (BattleHandoff.IsSystemView) SetupSystemView();      // 非戦闘＝星系の閲覧（恒星系ビュー）
                else if (BattleHandoff.IsCorridorFortress) SetupCorridorFortress(); // #40 回廊要塞＝岩壁の水路
                else if (BattleHandoff.IsPlanetSiege) SetupPlanetSiege();
                else SetupFromHandoff();
                ApplyWorldOffset();
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
            reinforcementPlayerFaction = playerFaction; // 増援スポーン用に保持
            System.Collections.Generic.List<GameObject> spawnedFleets = new System.Collections.Generic.List<GameObject>();
            foreach (var entry in scenario.fleets)
            {
                // 増援（#2182）：到着遅延>0は開戦時に出さず、時限スポーンへ回す。
                if (entry != null && entry.reinforcementDelay > 0f)
                {
                    pendingReinforcements.Add(entry);
                    continue;
                }
                GameObject fleet = SpawnFleet(entry, playerFaction);
                if (fleet != null) spawnedFleets.Add(fleet);
            }

            // 3.5 要塞（固定拠点・#78）を配置
            SpawnFortresses(scenario);

            // 4. 両軍が互いに正対するよう初期の向きを設定
            OrientFleetsToEnemy(spawnedFleets);

            // 5. 軍団長の乗艦（CSG・打撃群指揮官）：軍団ごとに旗艦を1つ選び軍団長を乗艦させる。
            EmbarkCorpsCommanders(spawnedFleets);

            ApplyWorldOffset();

            Debug.Log($"BattleSetup: シナリオ「{scenario.scenarioName}」から {spawnedFleets.Count} 艦隊を生成しました。");
        }

        /// <summary>
        /// ウィンドウ化会戦では戦場を会戦ごとの遠方オフセット（<see cref="BattleField.PendingOrigin"/>）へ平行移動する。
        /// 戦略マップと同一ワールド空間に additive ロードされるため、会戦カメラに戦略マップが映り込むのを防ぐ。
        /// このシーンの全ルートオブジェクト（旗艦・カメラ・背景・攻城アリーナ等）をまとめてずらす（配下艦は旗艦の子として追従）。
        /// フルスクリーン会戦では PendingOrigin=(0,0) のため何もしない（従来どおり）。
        /// </summary>
        private void ApplyWorldOffset()
        {
            Vector2 o = BattleField.PendingOrigin;
            if (o == Vector2.zero) return;
            // 自分のシーンの戦場中心として確定登録（実行時の境界/離脱端/カメラ境界がこれを参照）。
            BattleField.Register(gameObject.scene, o);
            Vector3 delta = new Vector3(o.x, o.y, 0f);
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                roots[i].transform.position += delta;
            }
        }

        /// <summary>
        /// 軍団ごとに軍団旗艦（最上位階級の艦隊）を選び、軍団長を乗艦させる（CSG＝打撃群指揮官モデル）。
        /// `OrderOfBattle` の軍団に司令が配属されていればその人物を、無ければデモ既定として旗艦の艦隊司令を軍団長に充てる。
        /// 軍団長は艦隊を持たず旗艦に同乗する＝乗艦艦の右クリックから軍団メニューを開け、軍団全体に能力/士気バフがかかる。
        /// </summary>
        private void EmbarkCorpsCommanders(System.Collections.Generic.List<GameObject> spawnedFleets)
        {
            var byCorps = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FleetStrength>>();
            foreach (var go in spawnedFleets)
            {
                if (go == null) continue;
                FleetStrength fs = go.GetComponent<FleetStrength>();
                if (fs == null || string.IsNullOrEmpty(fs.corpsName)) continue;
                string key = fs.faction + "/" + fs.corpsName;
                if (!byCorps.TryGetValue(key, out var list)) { list = new System.Collections.Generic.List<FleetStrength>(); byCorps[key] = list; }
                list.Add(fs);
            }

            foreach (var kv in byCorps)
            {
                var list = kv.Value;
                if (list.Count < 2) continue; // 単艦隊は軍団を成さない

                // 旗艦＝最上位階級の艦隊。
                FleetStrength flagship = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    int t = list[i].admiralData != null ? list[i].admiralData.rankTier : 0;
                    int bt = flagship.admiralData != null ? flagship.admiralData.rankTier : 0;
                    if (t > bt) flagship = list[i];
                }

                // 軍団長：OrderOfBattle の軍団司令があればそれを、無ければデモ既定で旗艦の艦隊司令を充てる。
                AdmiralData cc = null;
                var corps = OrderOfBattle.GetOrCreate(EchelonType.軍団, flagship.faction, flagship.corpsName);
                if (corps != null && corps.HasCommander) cc = corps.commander;
                if (cc == null) cc = flagship.admiralData; // デモ既定（軍団長＝旗艦の司令を兼任）

                flagship.corpsCommander = cc;
            }
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

        /// <summary>増援（#2182）：game-time で経過を計り、到着遅延に達したエントリを戦場端から時限スポーンする。</summary>
        private void Update()
        {
            if (pendingReinforcements.Count == 0) return;
            reinforcementElapsed += Time.deltaTime; // timeScale 追従＝ポーズで止まり倍速で速い

            for (int i = pendingReinforcements.Count - 1; i >= 0; i--)
            {
                ScenarioData.FleetEntry e = pendingReinforcements[i];
                if (e == null) { pendingReinforcements.RemoveAt(i); continue; }
                if (!ReinforcementRules.IsDue(e.reinforcementDelay, reinforcementElapsed)) continue;
                SpawnReinforcement(e);
                pendingReinforcements.RemoveAt(i);
            }
        }

        /// <summary>1隊の増援を戦場端から出現させ、戦場中央（敵方向）へ向ける。</summary>
        private void SpawnReinforcement(ScenarioData.FleetEntry entry)
        {
            GameObject fleet = SpawnFleet(entry, reinforcementPlayerFaction);
            if (fleet == null) return;
            SpawnedReinforcementCount++;

            // 自陣側の戦場端へ配置し直す（時間差で駆けつける）。
            Vector2 edge = ReinforcementRules.EdgePosition(entry.faction, entry.spawnPosition.y, reinforcementEdgeRadius);
            fleet.transform.position = new Vector3(edge.x, edge.y, 0f);

            // 戦場中央（おおむね敵方向）へ正対させる。
            Vector2 toCenter = -edge;
            if (toCenter.sqrMagnitude > 0.0001f)
                fleet.transform.up = toCenter.normalized;

            ReinforcementSpawned?.Invoke(fleet, entry);

            string who = entry.admiral != null ? entry.admiral.admiralName : "増援部隊";
            string msg = $"増援到着：{who} 隊が戦場に駆けつけた（{entry.faction}）";
            if (NotificationSink != null) NotificationSink(NotificationCategory.戦闘, NotificationSeverity.注意, msg);
            else NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意, msg);
        }

        /// <summary>
        /// シーンに既に存在する艦隊（手置きなど、これから生成する分以外）を除去します。
        /// SetActive(false) で即座にカウント・戦闘から除外しつつ Destroy します。
        /// </summary>
        private void ClearExistingFleets()
        {
            foreach (var fs in Object.FindObjectsByType<FleetStrength>(FindObjectsSortMode.None))
            {
                if (fs == null) continue;
                // ★WIN-3 重要：FindObjectsByType は**全シーン**を走査するため、自分の会戦シーンの艦だけを対象にする。
                //   さもないと2つ目の会戦のセットアップが1つ目の会戦の艦を Destroy し「1つ目が両軍壊滅で即消え」する。
                if (fs.gameObject.scene != gameObject.scene) continue;
                // ルート（親が無い艦隊）のみ対象。配下艦は親ごと消える
                if (fs.transform.parent == null)
                {
                    fs.gameObject.SetActive(false); // FindObjectsByType(既定)の集計から即除外
                    Destroy(fs.gameObject);
                }
            }
        }

        /// <summary>
        /// 使用する ScenarioData を解決します。
        /// Inspector 指定 > GameSettings.scenarioName 一致 > 利用可能な先頭シナリオ の順。索引は ContentDatabase（FND-1 #496）に集約。
        /// 既定/不正なシナリオ名（例：Title を経由せず Battle を直接再生した場合の既定値）でも空会戦にせず、
        /// 利用可能な先頭シナリオへフォールバックする（通常の Title→Battle フローは一致解決のため無影響）。
        /// </summary>
        private ScenarioData ResolveScenario()
        {
            if (scenarioOverride != null) return scenarioOverride;

            ScenarioData resolved = ScenarioData.Resolve(GameSettings.Instance.scenarioName);
            if (resolved != null) return resolved;

            // フォールバック：名前が一致しなくても、利用可能な先頭シナリオで会戦を成立させる
            var all = ContentDatabase.AllScenarios();
            if (all != null && all.Count > 0)
            {
                Debug.LogWarning($"BattleSetup: シナリオ「{GameSettings.Instance.scenarioName}」が見つからないため、" +
                                 $"先頭シナリオ「{all[0].scenarioName}」で開始します。");
                return all[0];
            }
            return null;
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
                strength.baseStrength = entry.baseStrength; // RANKCMD-1：兵力は艦隊が持つ（0なら提督側へフォールバック）
                strength.ApplyAdmiralData();          // 名前/兵力(艦隊基準×統率)/faction(提督由来)/色を反映

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

                // 旗幟（#817 関ヶ原型）：忠誠/調略浸透を反映（既定 1/0＝従来動作）
                strength.loyalty = Mathf.Clamp01(entry.loyalty);
                strength.intrigue = Mathf.Clamp01(entry.intrigue);

                // 旗艦名（#旗艦名）を払い出す。提督に専用旗艦名（例：ヤン→ヒューベリオン）があれば優先、
                // 取れなければ世界遺産プールから払い出す。重複なし＝部隊の固有名として頭上に表示。
                string signature = SignatureShipRegistry.Resolve(entry.admiral);
                strength.shipName = (!string.IsNullOrEmpty(signature) && ShipNameRegistry.TryAssignSpecific(signature))
                    ? signature
                    : ShipNameRegistry.Assign();

                // 反映した勢力で色を再適用
                FactionColor color = fleet.GetComponent<FactionColor>();
                if (color != null) color.ApplyColors();

                // 会戦の旗艦だけを戦略マップと同じ勢力別画像へ差し替える（FSH-2）。
                // 未登録勢力はプレハブの Triangle を維持し、配下艦は Squadron が保持した元画像を使う。
                ApplyFlagshipSprite(fleet, strength.faction, flagshipVisualHeight, flagshipVisualRotationOffset);

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
                        // 先に配属（この時点 unit.baseStrength=0＝規模ゲートは素通り＝シナリオ設定は権威）
                        FleetRoster.AssignAdmiral(unit, entry.admiral); // デモは階級ゲート無し
                        // RANKCMD-1：兵力を台帳へ一本化（以後のパネル再配属は指揮可能規模でゲート＝RANKCMD-3 が実戦で発火）
                        unit.baseStrength = strength.EffectiveBaseStrength;
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

            // AI 制御（AI標準＋手動上書きモデル）：全艦隊で FleetAI を有効化し、AI を基本の操舵にする。
            // プレイヤー隷下（自勢力）の艦は playerCommanded＝true として選択・手動指示の対象にし、
            // 手動指示中だけ AI を上書きする（指示完了で AI へ復帰）。敵・非プレイヤーは playerCommanded＝false。
            FleetAI ai = fleet.GetComponent<FleetAI>();
            if (ai != null)
            {
                bool playerOwned = IsPlayerControlled(entry, playerFaction);
                ai.playerCommanded = playerOwned;
                ai.enabled = true; // 全艦 AI 標準（隷下も基本AIで動き、手動指示で上書き）
            }

            // 名前を分かりやすく
            string admiralName = entry.admiral != null ? entry.admiral.admiralName : "Unknown";
            fleet.name = $"Fleet_{entry.faction}_{admiralName}";
            return fleet;
        }

        /// <summary>
        /// 旗艦本体のレンダラだけへ勢力別画像を適用する。画像が無ければ既存スプライトを変えない。
        /// root の scale と、選択リング・旗艦マーカー・配下艦には触れない。
        /// </summary>
        public static bool ApplyFlagshipSprite(
            GameObject fleet,
            Faction faction,
            float worldHeight = 1.2f,
            float rotationOffsetDegrees = 0f)
        {
            if (fleet == null) return false;
            Sprite sprite = FleetSpriteProvider.SpriteForFaction(faction);
            if (sprite == null) return false;

            Transform existing = fleet.transform.Find("FlagshipBody");
            SpriteRenderer visual = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (visual == null)
            {
                SpriteRenderer source = FindFlagshipBodyRenderer(fleet);
                if (source == null) return false;
                GameObject bodyObject = new GameObject("FlagshipBody");
                bodyObject.transform.SetParent(fleet.transform, false);
                visual = bodyObject.AddComponent<SpriteRenderer>();
                visual.color = source.color;
                visual.sortingLayerID = source.sortingLayerID;
                visual.sortingOrder = Mathf.Max(5, source.sortingOrder + 5);
                source.enabled = false;
            }

            visual.sprite = sprite;
            float spriteHeight = sprite.bounds.size.y;
            float scale = spriteHeight > 0.0001f ? Mathf.Max(0.01f, worldHeight) / spriteHeight : 1f;
            visual.transform.localScale = Vector3.one * scale;
            // pivot が中央でない画像も、bounds中心を部隊原点へ戻して中心回転させる。
            Vector3 center = sprite.bounds.center;
            Quaternion rotation = Quaternion.Euler(0f, 0f, rotationOffsetDegrees);
            visual.transform.localRotation = rotation;
            visual.transform.localPosition = -(rotation * center) * scale;
            // 勢力固有画像は固有色をそのまま表示。FactionColor再適用時も白が維持される。
            visual.color = Color.white;
            ConfigureFlagshipAdornment(fleet, visual, Mathf.Max(0.01f, worldHeight));
            return true;
        }

        private static void ConfigureFlagshipAdornment(GameObject fleet, SpriteRenderer body, float bodyHeight)
        {
            Transform ringTransform = fleet.transform.Find("SelectionRing");
            SpriteRenderer ring = ringTransform != null ? ringTransform.GetComponent<SpriteRenderer>() : null;
            if (ring != null && ring.sprite != null)
            {
                float diameter = Mathf.Max(ring.sprite.bounds.size.x, ring.sprite.bounds.size.y);
                float scale = diameter > 0.0001f ? bodyHeight * 1.25f / diameter : 1f;
                ringTransform.localPosition = Vector3.zero;
                ringTransform.localScale = Vector3.one * scale;
                ring.sortingOrder = body.sortingOrder - 2;
            }

            FlagshipMarker marker = fleet.GetComponent<FlagshipMarker>();
            if (marker != null) marker.ConfigureForArtwork(bodyHeight, body.sortingOrder);

            PositionFlagshipLabel(fleet.transform.Find("StrengthDisplay"), new Vector3(bodyHeight * 0.65f, -bodyHeight * 0.65f, 0f));
            PositionFlagshipLabel(fleet.transform.Find("MoraleLabel"), new Vector3(-bodyHeight * 0.65f, bodyHeight * 0.65f, 0f));
        }

        private static void PositionFlagshipLabel(Transform label, Vector3 localPosition)
        {
            if (label == null) return;
            label.localPosition = localPosition;
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 40;
        }

        /// <summary>既存の固定子・EscortShipを除外し、旗艦本体の最初のSpriteRendererを返す。</summary>
        public static SpriteRenderer FindFlagshipBodyRenderer(GameObject fleet)
        {
            if (fleet == null) return null;
            SpriteRenderer[] renderers = fleet.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null) continue;
                string objectName = renderer.gameObject.name;
                if (objectName == "FlagshipBody") continue;
                if (objectName == "SelectionRing" || objectName == "FlagshipMarker" || objectName == "FlagshipMarkerGlow")
                    continue;
                if (renderer.GetComponent<EscortShip>() != null) continue;
                return renderer;
            }
            return null;
        }

        /// <summary>
        /// シナリオの要塞エントリ（#78）から固定拠点を生成・配置する。位置は艦隊と同じく spawnSeparation 倍で原点から離す。
        /// </summary>
        private void SpawnFortresses(ScenarioData scenario)
        {
            if (scenario == null || scenario.fortresses == null) return;
            foreach (var entry in scenario.fortresses)
            {
                if (entry == null) continue;
                Vector3 pos = new Vector3(entry.position.x, entry.position.y, 0f) * spawnSeparation;
                GameObject go = new GameObject("Fortress");
                go.transform.position = pos;
                FortressUnit fortress = go.AddComponent<FortressUnit>();
                fortress.hasMainCannon = entry.hasMainCannon;
                int turretCount = entry.turretCount > 0 ? entry.turretCount : -1;
                int coreStrength = entry.coreStrength > 0 ? entry.coreStrength : -1;
                fortress.Setup(entry.faction, entry.factionData, entry.fortressName, turretCount, coreStrength);
                go.name = $"Fortress_{fortress.Faction}_{fortress.FortressName}";
            }
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

            // 複数艦隊モード（接敵内容を会戦へ合わせる）：明細の各艦隊を1旗艦として陣営ごとに縦に並べて湧かせる。
            if (BattleHandoff.IsMultiFleet)
            {
                SpawnHandoffFleets(fleets, playerFaction);
                OrientFleetsToEnemy(fleets);
                Debug.Log($"BattleSetup: 戦略の複数艦隊遭遇から実会戦を生成（{BattleHandoff.fleets.Count}隊）。");
                return;
            }

            var eA = MakeHandoffEntry(BattleHandoff.factionA, BattleHandoff.admiralA, BattleHandoff.strengthA, new Vector2(-6f, 0f));
            var eB = MakeHandoffEntry(BattleHandoff.factionB, BattleHandoff.admiralB, BattleHandoff.strengthB, new Vector2(6f, 0f));
            // 旗幟（#817）：戦略側が国家状態から積んだ基準忠誠/調略浸透を会戦へ（既定 1/0＝従来動作）
            eA.loyalty = BattleHandoff.loyaltyA; eA.intrigue = BattleHandoff.intrigueA;
            eB.loyalty = BattleHandoff.loyaltyB; eB.intrigue = BattleHandoff.intrigueB;
            GameObject ga = SpawnFleet(eA, playerFaction);
            GameObject gb = SpawnFleet(eB, playerFaction);
            LinkStrategicFleet(ga, BattleHandoff.fleetIdA);
            LinkStrategicFleet(gb, BattleHandoff.fleetIdB);
            // 軍の質（C4）：戦略側が補給/練度から積んだ戦闘力倍率を旗艦へ（既定1.0＝従来動作）
            if (ga != null) { var fa = ga.GetComponent<FleetStrength>(); if (fa != null) fa.qualityFactor = BattleHandoff.qualityA; }
            if (gb != null) { var fb = gb.GetComponent<FleetStrength>(); if (fb != null) fb.qualityFactor = BattleHandoff.qualityB; }
            if (ga != null) fleets.Add(ga);
            if (gb != null) fleets.Add(gb);

            OrientFleetsToEnemy(fleets);
            Debug.Log($"BattleSetup: 戦略の遭遇から実会戦を生成（{BattleHandoff.factionA} {BattleHandoff.strengthA} vs {BattleHandoff.factionB} {BattleHandoff.strengthB}）。");
        }

        /// <summary>
        /// 複数艦隊の会戦（<see cref="BattleHandoff.IsMultiFleet"/>）：明細の各艦隊を1旗艦として湧かせる。
        /// 陣営Aは左(x=-7)・陣営Bは右(x=+7)に置き、各陣営内で艦隊数に応じて Y 方向へ等間隔に散らす。
        /// 旗幟（#817）・軍の質（C4）は1隊ごとに反映（単隊潜行と一貫）。
        /// </summary>
        private void SpawnHandoffFleets(System.Collections.Generic.List<GameObject> fleets, Faction playerFaction)
        {
            var list = BattleHandoff.fleets;
            const float sideX = 7f, spreadY = 4f;

            // 守備側=中央／攻撃側=侵攻方向（#初期配置）：星系所有者が守備側のとき、守備を原点に、攻撃を侵攻方向へ寄せる。
            // 守備側が定義されない（回廊の相互衝突など）or 片側のみのときは従来の左右対称配置にフォールバック。
            int countDef = 0, countAtk = 0;
            if (BattleHandoff.hasDefender)
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].faction == BattleHandoff.defenderFaction) countDef++; else countAtk++;
                }
            bool defenderLayout = BattleHandoff.hasDefender && countDef > 0 && countAtk > 0;

            if (defenderLayout)
            {
                Vector2 axis = BattleHandoff.approachDir.sqrMagnitude > 1e-6f ? BattleHandoff.approachDir.normalized : Vector2.right;
                Vector2 perp = new Vector2(-axis.y, axis.x);     // 散開はアクシスに直交
                float standoff = 2f * sideX;                      // 守備(原点)から攻撃側までの間合い（従来の左右間隔に相当）
                int idA = 0, idd = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var hf = list[i];
                    bool isDef = hf.faction == BattleHandoff.defenderFaction;
                    int idx, cnt; Vector2 basePos;
                    if (isDef) { idx = idd++; cnt = countDef; basePos = Vector2.zero; }       // 守備＝MAP中央
                    else { idx = idA++; cnt = countAtk; basePos = axis * standoff; }          // 攻撃＝侵攻方向
                    float off = (cnt > 1) ? (idx - (cnt - 1) * 0.5f) * spreadY : 0f;
                    Vector2 pos = basePos + perp * off;

                    var e = MakeHandoffEntry(hf.faction, hf.admiral, hf.strategicStrength, pos);
                    e.loyalty = hf.loyalty; e.intrigue = hf.intrigue;
                    GameObject g = SpawnFleet(e, playerFaction);
                    if (g == null) continue;
                    LinkStrategicFleet(g, hf.fleetId);
                    var fsc = g.GetComponent<FleetStrength>();
                    if (fsc != null) fsc.qualityFactor = hf.quality;
                    fleets.Add(g);
                }
                return;
            }

            int countA = 0, countB = 0;
            for (int i = 0; i < list.Count; i++) { if (list[i].sideA) countA++; else countB++; }

            int ia = 0, ib = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var hf = list[i];
                int idx, cnt; float x;
                if (hf.sideA) { idx = ia++; cnt = countA; x = -sideX; }
                else { idx = ib++; cnt = countB; x = sideX; }
                float y = (cnt > 1) ? (idx - (cnt - 1) * 0.5f) * spreadY : 0f;

                var e = MakeHandoffEntry(hf.faction, hf.admiral, hf.strategicStrength, new Vector2(x, y));
                e.loyalty = hf.loyalty; e.intrigue = hf.intrigue;
                GameObject g = SpawnFleet(e, playerFaction);
                if (g == null) continue;
                LinkStrategicFleet(g, hf.fleetId);
                var fsc = g.GetComponent<FleetStrength>();
                if (fsc != null) fsc.qualityFactor = hf.quality;
                fleets.Add(g);
            }
        }

        /// <summary>
        /// 戦略艦隊と、その会戦シーン内の旗艦を結び付ける。static の <see cref="FleetRoster"/> は戦役全体の
        /// 人事台帳なので変更せず、会戦ごとの識別は <see cref="FleetStrength"/> にだけ保持する（WIN-4）。
        /// これにより複数会戦で同時に同じ勢力が戦っても、各窓の艦隊番号・戦果の対応が混ざらない。
        /// </summary>
        public static void LinkStrategicFleet(GameObject fleet, int strategicFleetId)
        {
            if (fleet == null || strategicFleetId <= 0) return;
            FleetStrength strength = fleet.GetComponent<FleetStrength>();
            if (strength == null) return;
            strength.strategicFleetId = strategicFleetId;
            strength.fleetNumber = strategicFleetId;
            if (string.IsNullOrEmpty(strength.fleetUnitName))
                strength.fleetUnitName = $"第{strategicFleetId}艦隊";
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
                ad.leadership = 50; // 統率50＝補正1.0＝基準兵力をそのまま反映
            }
            // RANKCMD-1：戦略兵力は艦隊側 baseStrength として渡す（人物に固定兵力を持たせない）
            return new ScenarioData.FleetEntry
            {
                admiral = ad,
                faction = f,
                factionData = null,
                spawnPosition = pos,
                formation = Formation.紡錘陣,
                baseStrength = Mathf.Max(1, strategicStrength) * BattleHandoff.StrengthScale
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
            // 守備隊の引き継ぎ（#131 第4段）。戦略の惑星が守備隊を持つときだけ上書き（0＝アリーナ既定＝後方互換）。
            if (BattleHandoff.planetMaxGarrison > 0)
            {
                arena.maxGroundGarrison = BattleHandoff.planetMaxGarrison;
                arena.initialGarrisonRatio = BattleHandoff.planetGarrisonRatio;
                arena.initialGarrisonMorale = BattleHandoff.planetGarrisonMorale;
            }
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

                // 突入した攻城艦隊はプレイヤー隷下（AI標準＋手動上書き）
                FleetAI ai = g.GetComponent<FleetAI>();
                if (ai != null)
                {
                    ai.playerCommanded = true;
                    ai.enabled = true;
                }
            }

            Debug.Log($"BattleSetup: 惑星攻城マップを生成（{BattleHandoff.planetName} / {BattleHandoff.besiegerFaction} {n}隊が包囲）。");
        }

        [Header("援軍のワープイン（#38 C-5）")]
        [Tooltip("同時に到着した援軍どうしの縦の間隔（重なり回避）")]
        public float reinforcementSpacing = 6f;
        [Tooltip("戦略から派遣された援軍が現れる端までの距離。0以下なら回廊要塞アリーナの水路長／既定 40")]
        public float warpInEdgeRadius = 0f;

        /// <summary>
        /// 戦略から派遣されて<b>到着した援軍</b>を自陣側の端へワープインさせる（#38 C-5）。
        /// シナリオ定義の時限増援（#2182・<see cref="SpawnReinforcement(ScenarioData.FleetEntry)"/>）とは別経路で、
        /// こちらは<b>戦略マップの艦隊</b>が銀河時間ぶんかけて駆けつけてくる。
        /// 出現端の座標は既存の <see cref="ReinforcementRules.EdgePosition"/> を唯一の窓口として使う。
        /// 同時到着は <paramref name="index"/>／<paramref name="total"/> で縦にずらして重ならないようにする。
        /// 生成できたら true。
        /// </summary>
        public bool SpawnWarpReinforcement(WarpReinforcement order, int index, int total)
        {
            if (fleetPrefab == null) return false;

            float radius = warpInEdgeRadius > 0f ? warpInEdgeRadius : DefaultEdgeRadius();
            float baseY = (total <= 1) ? 0f : (index - (total - 1) * 0.5f) * reinforcementSpacing;
            Vector2 pos = ReinforcementRules.EdgePosition(order.faction, baseY, radius);

            // ★回廊要塞アリーナ（#40）は勢力に依らず攻撃側を −x・守備側を +x に置く。
            // 既定の EdgePosition は「帝国＝−x／他＝+x」の固定なので、攻撃側が同盟だと
            // 援軍が<b>要塞の裏（突破線の向こう）</b>に湧いて、誰も突破していないのに突破成立になる。
            // アリーナがあるときは、その戦場の攻撃/守備の向きに合わせて端を選び直す。
            var arena = CorridorFortressArena.For(gameObject.scene) ?? CorridorFortressArena.Any();
            if (arena != null)
            {
                bool attackerSide = !FactionRelations.IsHostile(null, order.faction, null, arena.attackerFaction);
                pos = new Vector2(attackerSide ? -Mathf.Abs(radius) : Mathf.Abs(radius), baseY);
            }

            // 戦略の抽象兵力 → 戦術の基準兵力（既存の換算率を使う＝戦果の書き戻しと整合する）。
            int baseStrength = Mathf.Max(1, order.strength * BattleHandoff.StrengthScale);
            var entry = MakeBesiegerEntry(order.faction, baseStrength, pos);
            entry.admiral.admiralName = $"{order.faction}第{order.fleetId}艦隊";
            entry.fleetNumber = order.fleetId;

            GameObject g = SpawnFleet(entry, GameSettings.Instance.playerFaction);
            if (g == null) return false;

            // spawnSeparation を無視して端の実位置へ置き、戦場の中央へ正対させる。
            Vector2 world = pos + BattleField.OriginFor(gameObject.scene);
            g.transform.position = new Vector3(world.x, world.y, 0f);
            float ang = Mathf.Atan2(-pos.y, -pos.x) * Mathf.Rad2Deg - 90f;
            g.transform.rotation = Quaternion.Euler(0f, 0f, ang);

            // 元の戦略艦隊と紐付ける＝会戦後にこの援軍の実残存だけをその艦隊へ返せる。
            var fsLink = g.GetComponent<FleetStrength>();
            if (fsLink != null) fsLink.strategicFleetId = order.fleetId;

            FleetAI ai = g.GetComponent<FleetAI>();
            if (ai != null)
            {
                ai.playerCommanded = order.faction == GameSettings.Instance.playerFaction;
                ai.enabled = true;
            }
            return true;
        }

        /// <summary>援軍が現れる端までの既定距離（回廊要塞アリーナがあればその水路長）。</summary>
        private float DefaultEdgeRadius()
        {
            var arena = CorridorFortressArena.For(gameObject.scene) ?? CorridorFortressArena.Any();
            return arena != null ? arena.channelHalfLength : 40f;
        }

        [Header("回廊要塞の戦術マップ（#40 C-7）")]
        [Tooltip("突入する攻撃側の艦隊数（水路の入口に縦列で並ぶ）")]
        public int corridorAttackerCount = 5;
        [Tooltip("要塞側の守備艦隊数（要塞の背後＝守備側に置く）")]
        public int corridorDefenderCount = 2;

        /// <summary>
        /// 回廊要塞の戦術マップを生成する（#40）。両側が岩壁の一本の水路に要塞を据え、攻撃側を入口（−x）へ、
        /// 守備艦隊を要塞の背後（+x）へ置く。<see cref="CorridorFortressArena"/> が壁と封鎖線で位置を拘束するので、
        /// <b>要塞を撃破するまで攻撃側は反対側へ抜けられず、外周を回り込むこともできない</b>。
        /// </summary>
        private void SetupCorridorFortress()
        {
            if (fleetPrefab == null) { Debug.LogError("BattleSetup: fleetPrefab が未設定です。"); return; }
            ClearExistingFleets();
            ScenarioData.ActiveScenario = null; // 勝利条件なし（決着は要塞の撃破と突破線の到達）

            var arena = new GameObject("CorridorFortressArena").AddComponent<CorridorFortressArena>();
            arena.transform.position = Vector3.zero;
            arena.Configure(BattleHandoff.fortressOwner, BattleHandoff.fortressAttacker,
                            BattleHandoff.fortressName, BattleHandoff.fortressGarrison);

            // 要塞の実体（砲台＋主砲）。守備戦力を耐久へ写して「守備が厚いほど固い」を戦術側にも通す。
            var fgo = new GameObject("Fortress");
            var unit = fgo.AddComponent<FortressUnit>();
            int core = Mathf.Max(1, Mathf.RoundToInt(BattleHandoff.fortressGarrison * BattleHandoff.fortressShield));
            unit.hasMainCannon = true;
            unit.Setup(BattleHandoff.fortressOwner, null, BattleHandoff.fortressName, -1, core);
            fgo.name = $"Fortress_{BattleHandoff.fortressOwner}_{BattleHandoff.fortressName}";
            arena.AttachFortress(unit);

            Faction playerFaction = GameSettings.Instance.playerFaction;

            // ★戦略兵力 → 戦術の基準兵力は必ず StrengthScale を掛ける（他の経路と同じ換算）。
            // 掛け忘れると、同じ戦場へ来る援軍（×40 済み）と 40 倍の差が付いて戦場が塗り潰される。
            int attackerPerFleet = Mathf.Max(1, Mathf.RoundToInt(
                BattleHandoff.besiegerStrength * BattleHandoff.StrengthScale
                / (float)Mathf.Max(1, corridorAttackerCount)));

            // 守備艦隊の規模は<b>要塞の守備戦力</b>から出す（攻撃側の兵力から決めるのは論理が逆）。
            int defenderPerFleet = Mathf.Max(1, Mathf.RoundToInt(
                BattleHandoff.fortressGarrison * BattleHandoff.StrengthScale
                / (float)Mathf.Max(1, corridorDefenderCount)));

            // 攻撃側：受け渡しの明細があれば<b>戦略艦隊と1対1</b>で生成する（損害を艦隊ごとに返すため）。
            // 明細が無い場合だけ、従来どおり均等に分けた仮の隊で組む（後方互換）。
            var attackers = new System.Collections.Generic.List<BattleHandoff.HandoffFleet>();
            for (int i = 0; i < BattleHandoff.fleets.Count; i++)
                if (BattleHandoff.fleets[i].faction == BattleHandoff.fortressAttacker)
                    attackers.Add(BattleHandoff.fleets[i]);

            if (attackers.Count > 0)
            {
                for (int i = 0; i < attackers.Count; i++)
                {
                    float y = SpreadOffset(i, attackers.Count, arena.channelHalfWidth * 0.7f);
                    var pos = new Vector2(-arena.channelHalfLength * 0.75f - (i % 2) * 4f, y);
                    int baseStrength = Mathf.Max(1, attackers[i].strategicStrength * BattleHandoff.StrengthScale);
                    PlaceCorridorFleet(BattleHandoff.fortressAttacker, baseStrength, pos, 0f, playerFaction, true,
                                       attackers[i].fleetId);
                }
            }
            else
            {
                for (int i = 0; i < Mathf.Max(1, corridorAttackerCount); i++)
                {
                    float y = SpreadOffset(i, corridorAttackerCount, arena.channelHalfWidth * 0.7f);
                    var pos = new Vector2(-arena.channelHalfLength * 0.75f - (i % 2) * 4f, y);
                    PlaceCorridorFleet(BattleHandoff.fortressAttacker, attackerPerFleet, pos, 0f, playerFaction, true, 0);
                }
            }

            // 守備側＝要塞の背後（+x）。要塞を抜かれたら迎え撃つ位置。
            // ★駐留艦隊（#40）が渡されていれば<b>実在の艦隊をそのまま</b>並べる（匿名の守備艦隊を作らない）。
            // 渡されていない要塞（旧セーブ等・施設の守備値だけ）は、従来どおり仮の守備隊で組む（後方互換）。
            var defenders = new System.Collections.Generic.List<BattleHandoff.HandoffFleet>();
            for (int i = 0; i < BattleHandoff.fleets.Count; i++)
                if (BattleHandoff.fleets[i].faction == BattleHandoff.fortressOwner)
                    defenders.Add(BattleHandoff.fleets[i]);

            if (defenders.Count > 0)
            {
                for (int i = 0; i < defenders.Count; i++)
                {
                    float y = SpreadOffset(i, defenders.Count, arena.channelHalfWidth * 0.6f);
                    var pos = new Vector2(arena.fortressX + arena.fortressBlockRadius + 6f + i * 4f, y);
                    int baseStrength = Mathf.Max(1, defenders[i].strategicStrength * BattleHandoff.StrengthScale);
                    PlaceCorridorFleet(BattleHandoff.fortressOwner, baseStrength, pos, 180f, playerFaction, false,
                                       defenders[i].fleetId);
                }
            }
            else if (BattleHandoff.fortressGarrison > 0f)
            {
                // ★駐留艦隊が居ない要塞＝<b>施設の守備値だけ</b>（旧セーブ・占領直後の残置守備）。
                // ここで並べる隊は「新しい艦隊」ではなく施設の守備値（要塞兵・砲台要員）を戦術の駒に
                // 割り付けたもの＝規模は必ず fortressGarrison から出す。会戦の結果で施設の守備値は
                // 戦略側へ書き戻される（GalaxyView.ApplyFortressResult：壊滅なら 0・制圧なら残置ぶん）ので、
                // 守備値が尽きた要塞では 0 隊になり<b>毎戦闘で守備隊が湧き直すことはない</b>。
                for (int i = 0; i < Mathf.Max(0, corridorDefenderCount); i++)
                {
                    float y = SpreadOffset(i, corridorDefenderCount, arena.channelHalfWidth * 0.6f);
                    var pos = new Vector2(arena.fortressX + arena.fortressBlockRadius + 6f + i * 4f, y);
                    PlaceCorridorFleet(BattleHandoff.fortressOwner, defenderPerFleet, pos, 180f, playerFaction, false, 0);
                }
            }

            Debug.Log($"BattleSetup: 回廊要塞マップを生成（{BattleHandoff.fortressName} / " +
                      $"{BattleHandoff.fortressOwner} 保持・{BattleHandoff.fortressAttacker} が突破を試行）。");
        }

        /// <summary>水路の幅に収まる範囲で i 番目を左右に散らす（縦列が重ならないように）。</summary>
        private static float SpreadOffset(int i, int count, float halfSpan)
        {
            if (count <= 1) return 0f;
            float t = i / (float)(count - 1);          // 0..1
            return Mathf.Lerp(-halfSpan, halfSpan, t);
        }

        /// <summary>回廊アリーナへ艦隊を1隊置く（spawnSeparation を無視して実位置へ配置する）。</summary>
        private void PlaceCorridorFleet(Faction fac, int baseStrength, Vector2 pos, float headingDeg,
                                        Faction playerFaction, bool playerCommanded, int strategicFleetId)
        {
            var entry = MakeBesiegerEntry(fac, baseStrength, pos);
            entry.formation = Formation.紡錘陣; // 隘路は正面が狭い＝縦に細い陣
            GameObject g = SpawnFleet(entry, playerFaction);
            if (g == null) return;
            g.transform.position = new Vector3(pos.x, pos.y, 0f);
            g.transform.rotation = Quaternion.Euler(0f, 0f, headingDeg - 90f); // 前方=Transform.up を +x/−x へ
            // 元の戦略艦隊と紐付ける＝会戦後にこの隊の実残存だけをその艦隊へ返せる。
            var fsLink = g.GetComponent<FleetStrength>();
            if (fsLink != null) fsLink.strategicFleetId = strategicFleetId;
            FleetAI ai = g.GetComponent<FleetAI>();
            if (ai != null)
            {
                ai.playerCommanded = playerCommanded && fac == playerFaction;
                ai.enabled = true;
            }
        }

        /// <summary>攻城艦隊1隊ぶんのエントリ（StrengthScale を掛けない素の基準兵力）。</summary>
        private ScenarioData.FleetEntry MakeBesiegerEntry(Faction f, int baseStrength, Vector2 pos)
        {
            var ad = ScriptableObject.CreateInstance<AdmiralData>();
            ad.admiralName = f + "攻城艦隊";
            ad.faction = f;
            ad.leadership = 50;
            // RANKCMD-1：兵力は艦隊側 baseStrength として渡す（人物に固定兵力を持たせない）
            return new ScenarioData.FleetEntry
            {
                admiral = ad,
                faction = f,
                factionData = null,
                spawnPosition = pos,
                formation = Formation.鶴翼陣,
                baseStrength = Mathf.Max(1, baseStrength)
            };
        }
    }
}
