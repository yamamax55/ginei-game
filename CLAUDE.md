# CLAUDE.md — 銀英伝風 戦術艦隊戦（プロジェクトメモリ）

> Claude Code が毎セッション開始時に自動で読み込む。プロジェクトルート（`Assets/` や `ProjectSettings/` のある階層）に置く。
> これは「プロジェクトの最上位ルール」。プロンプトと衝突したら、このファイルが優先。

## プロジェクト概要
- Unity 6.4（6000.4.x） / 2D (URP)。
- ジャンル：PS版『銀河英雄伝説』風の戦術艦隊戦（リアルタイム会戦）。将来は戦略レイヤー・複数勢力（諸侯型）へ拡張予定。
- 名前空間：全クラス `namespace Ginei`。
- 入力：新 Input System（`UnityEngine.InputSystem`）で統一。旧 `Input.xxx` は使わない。
- UI：uGUI + TextMeshPro。
- シーン遷移を伴う一連の流れ（タイトル→会戦→結果）を持つ。提督・シナリオはデータ（ScriptableObject）駆動。

## 絶対規約（必ず守る）
- 2D。座標は XY 平面、回転は Z 軸のみ。
- 艦の正面は `Transform.up`。
- 調整値は `public` にし、`[Header]` / `[Tooltip]` を付ける。
- 移動・回転・タイマーはフレームレート非依存（`Time.deltaTime`）。timeScale に追従させる（ポーズ=0／倍速対応）。
- コメントは日本語で簡潔に。
- 既存スクリプトを壊さない。指示外のファイルを大きく書き換えない。大きな変更は段階的に。
- **実効値パターン**：能力を一時的に上下させるときは基準値（publicフィールド）を上書きせず、ローカル変数で実効値を計算する。現状の実装例：
  - `FleetMovement.GetMobilityFactor()`（提督機動・士気・交戦状態を掛け合わせて実効速度/回頭速度を出す）。
  - `FleetMorale.GetMoraleFactor()`（士気から能力補正倍率を返す。0.5〜1.0）。
  - `FleetWeapon.PerformAttack()`（提督攻撃・士気・側背面倍率を掛けて最終ダメージを算出）。
  - `FleetStrength.TakeDamage()`（提督防御で被ダメージを軽減。基準ダメージは変えない）。

## コーディング規約
- 1ファイル1クラス、ファイル名=クラス名。命名：型/メソッド/public定数=PascalCase、フィールド=camelCase。
- 依存コンポーネントは `[RequireComponent]` で明示（`FleetWeapon`→`WeaponArc`、`FleetAI`→`FleetMovement`/`FleetWeapon`/`FleetStrength`、`FactionColor`→`FleetStrength`、`FleetMorale`→`FleetStrength`）。
- 他コンポーネント参照は Awake/Start でキャッシュし、使用前に null チェック。
- マジックナンバー禁止（`public` か `const`）。
- データ（提督・シナリオ・勢力など）は ScriptableObject に分離する方針。
- 実行時生成した `Material` は `OnDestroy` で `Destroy` してリークを防ぐ（`WeaponArc`/`FleetWeapon`/`DamagePopup` が踏襲）。

## シーン構成とゲームフロー
- シーンは3つ：**Title** → **Battle** → **Result**（シーン名はこの文字列。`SceneLoader.LoadScene(name)` で遷移）。
- 起動〜会戦の流れ：
  1. `TitleManager.StartBattle()`（「会戦開始」ボタン）は**シナリオ選択画面を開くだけ**。選択画面の「この設定で会戦開始」＝`BeginBattle()` が現在の設定を `SaveManager.Save()` し、`GameSettings.ResetStats()` 後に `SceneLoader.LoadScene("Battle")`。
  2. Battle シーンで `BattleSetup`（`[DefaultExecutionOrder(-100)]`、Awake）が `ScenarioData` を解決し、手置き艦隊をクリアしてから `fleetPrefab` を生成・配置。**Battle シーン以外（Title 等）では Awake で即 return し何もしない**（誤配置で戦闘が始まらないようガード）。
  3. `BattleManager`（Start）が開始時の隻数を記録し、`checkInterval`（1秒）ごとに全滅判定。決着で `Time.timeScale=0` → 戦績を `GameSettings` に記録 → `SceneLoader.LoadScene("Result")`。
  4. `ResultManager` が `GameSettings` の戦績を表示、`BackToTitle()` で Title へ。
- `TitleManager.ContinueBattle()` は `SaveManager.Load()` で直近セットアップを復元して会戦へ。`continueButton` は `SaveManager.HasSave()` の時だけ活性。

## シングルトン（`Instance` ＋ `DontDestroyOnLoad`）
シーンを跨いで生きる。`Instance` ゲッターが無ければ自動生成する（手置き不要）。重複させない。
| クラス | 役割 | 主なフィールド/API |
|---|---|---|
| `GameSettings` | 設定と戦績の保管庫（シーン間共有の唯一の窓口） | `playerFaction`/`scenarioName`/`selectedAdmiral`/`masterVolume`/`defaultTimeScale`/`alwaysShowGizmos`、戦績 `winner`/`imperialSunkCount`/`allianceSunkCount`/`remainingStrength`、`ResetStats()` |
| `AudioManager` | BGM/SE の一元管理 | `PlayBGM`/`StopBGM`/`PlaySE`/`PlayBeam`/`PlayHit`/`PlayExplosion`/`PlayUIClick`。音量は `bgmVolume`/`seVolume`×`GameSettings.masterVolume` |
| `SceneLoader` | 非同期シーン遷移＋ロード画面（コードで自動生成、`unscaledTime` 駆動） | `LoadScene(string)` |

## データ / 列挙 / ScriptableObject
- `Faction.cs`：`enum Faction { 帝国, 同盟 }`（旧式・後方互換用）。陣営の置き場所は `FleetStrength.faction`。
- **多勢力化（B-1〜B-4）**：`FactionData`(ScriptableObject、メニュー `Ginei/Create Faction Data`→`Resources/Factions/`)＝`factionName`/`color`/`ideology`/`nonHostileFactions`/`legacyFaction`＋`IsHostileTo(other)`（既定=異勢力は敵）。`FactionRelations.IsHostile(...)` が敵対判定の唯一の窓口（双方に `FactionData` があればそれで、無ければ enum 違いで判定＝後方互換）。`IShipTarget.FactionData`／`FleetStrength.factionData`／`ScenarioData.FleetEntry.factionData`／`GameSettings.playerFactionData` で勢力を持たせる。**`FactionData` 未割当なら従来の2勢力 enum 動作のまま**。3勢力目はアセット追加＋シナリオ割当のみで動く（コード変更不要）。`legacyFaction` は既存UI/セーブ/プレイヤー操作判定の橋渡し。
- `Formation`：`enum { 紡錘陣, 鶴翼陣, 円陣, 横陣, 方陣 }`（定義は `Squadron.cs` 内。既定=紡錘陣）。すべて旗艦＝中心(原点)・左右対称・旗艦の向き(Transform.up=前方)に追従。`ChangeFormation(int)` のインデックスはこの並び順。
- `AIState`：`enum { 接近, 交戦, 撤退 }`（定義は `FleetAI.cs` 内）。
- `AdmiralData`（ScriptableObject、メニュー `Ginei/Admiral Data`）：提督能力。`leadership`(統率)/`attack`(攻撃)/`defense`(防御)/`mobility`(機動)/`operation`(運営・将来用)/`intelligence`(情報・将来用)＋`baseStrength`/`admiralName`/`faction`。
- `ScenarioData`（ScriptableObject、メニュー `Ginei/Scenario Data`）：会戦定義。`scenarioName` と `List<FleetEntry> fleets`（各エントリ＝`admiral`/`faction`/`spawnPosition`/`formation`）。`BattleSetup` が `Resources` 全走査で `scenarioName` 一致を解決。サンプル会戦・提督アセットはエディタメニュー `Ginei/Create Sample Scenarios`（`Assets/Editor/SampleScenarioCreator.cs`）でワンクリック生成（シナリオ→`Resources/`、提督→`Assets/Data/Admirals/`、既存提督は上書きしない）。
- `SaveData`（`[Serializable]` 平データ）：`playerFaction`(int)/`scenarioName`/`selectedAdmiral`。`SaveManager`(static) が `persistentDataPath/setup_save.json` に JSON 保存。

## 提督能力が効く場所（実効値パターンで反映）
- `leadership` → `FleetStrength.ApplyAdmiralData()` が `maxStrength` を補正。`FleetMorale` の `maxMorale`。
- `attack` → `FleetWeapon.PerformAttack()` のダメージ倍率（50で1.0倍、100で1.5倍、0で0.5倍）。
- `defense` → `FleetStrength.TakeDamage()` の被ダメージ軽減（最大90%カット）。
- `mobility` → `FleetMovement.GetMobilityFactor()` の速度/回頭補正。
- `operation`/`intelligence` は現状未使用（将来用）。

## 個艦戦闘モデル（部隊＝旗艦＋配下艦）
- 部隊は「旗艦(`FleetStrength`)＋配下艦(`EscortShip`)」で構成。攻撃対象は**個々の艦艇**で、共通インターフェイス `IShipTarget`(`Transform`/`Faction`/`IsAlive`/`TakeDamage(int)`) を旗艦・配下艦の両方が実装する。
- **選択・指揮・勝敗カウントは従来どおり部隊(旗艦)単位**。配下艦は個別選択しない（クリックは `GetComponentInParent` で旗艦の `Selectable` に解決）。勝敗は生存旗艦数で数える。
- 旗艦の「艦艇数」は既存 `strength`（兵力）を流用（多め）。配下艦は1部隊 `Squadron.escortCount`(既定50)隻、1隻あたり `EscortShip.shipCount`=`Squadron.escortShipCount`(既定200)で合計兵力が過大にならないよう調整。
- 攻撃ロジックは `ShipCombat`(static) に集約：`FindNearestEnemyInArc`/`FindPrioritizedEnemyInArc`/`FindNearestEnemyInArcOfFleet`(指定艦隊の射界内最寄り)/`GetSquadronOf`(個艦→所属部隊)/`AnyEnemyInArc`/`IsInArc`/`ComputeDamage`(提督攻撃×士気×側背面)/`IsValidTarget`(破棄済み判定)。**ダメージ式や敵探索を各所に重複実装しない**。各艦が自分の1発を撃つ（二重計算しない）。
- 配下艦(`EscortShip`)の自動索敵は `FindPrioritizedEnemyInArc` を使う：第一優先＝射界内かつ**射線の通る敵旗艦**（最寄り）、無ければ第二優先＝射界内の敵配下艦（最寄り）。射線判定 `HasClearShot` は from→旗艦の線分上に（標的以外の）**敵配下艦**がいれば遮蔽とみなし、その旗艦を第一候補から除外する＝配下艦が旗艦を守るスクリーンになる。旗艦の `FleetWeapon` は従来どおり `FindNearestEnemyInArc`／手動指定（`SetManualTarget`）に従い、射線判定は適用しない。
- 旗艦の艦艇数が0になると**破棄せず部隊退却**（`FleetStrength.BeginRetreat`）。退却部隊は `IsAlive=false` となり、標的・発砲・勝敗カウントから一括除外される。

## 既存コンポーネント（現状＝正。これに合わせる）
### システム・管理層
| クラス | 対象 | 責務 / 主なAPI |
|---|---|---|
| `BattleSetup` | Battle シーンに1つ | `[DefaultExecutionOrder(-100)]`。**`SceneManager.GetActiveScene().name != "Battle"` の場合は Awake で即 return**（Title 等に誤配置されても艦隊を湧かせない）。`ScenarioData` から艦隊生成・配置。`fleetPrefab` 必須。生成時に手置き艦隊をクリア＋`FleetRegistry.Clear()`。生成位置は原点中心に `spawnSeparation`(既定2.5)倍して両軍を離す。生成後 `OrientFleetsToEnemy` で各艦を相手陣営重心へ正対させる。プレイヤー陣営以外のみ `FleetAI` を有効化。`scenarioOverride` で直接指定可。 |
| `BattleManager` | Battle シーンに1つ | 勝敗判定（`checkInterval` 秒ごと）・戦績記録。**生存中の部隊(旗艦)数で数える**（`FleetRegistry.GetFlagships()` の件数。退却・破棄は含まれず、配下艦も数えない）。開始時隻数は全 Start 完了後の最初の Update で記録（実行順非依存）。決着で timeScale=0→Result へ遷移。Rキーでリスタート。 |
| `FleetRegistry` | static（シーン内在庫） | 全 `IShipTarget`（旗艦＋配下艦）を**陣営非依存の単一リスト**で保持（多勢力対応）。各艦が出現時 `Register`／破棄・退却時 `Unregister`。`AllTargets`(全個艦)/`AllFlagships`(全旗艦)/`Clear()`。敵味方は探索側が `FactionRelations.IsHostile` で判定する（陣営別バケットは廃止）。`ShipCombat`/`FleetWeapon`/`EscortShip`/`FleetAI`/`BattleManager`/`FleetCommander` がここを参照（`FindObjectsByType` を置換）。`BattleSetup.Awake` が `Clear()`。 |
| `TitleManager` | Title シーン | 新規開始/続きから/設定/終了。`SaveManager` と連携、`continueButton`/`settingsPanel` 参照、`SetVolume`。**シナリオ選択／プレイ陣営選択UIを実行時生成**（自前 Canvas＋全画面ディマーのモーダル、初期非表示）。フロー：「会戦開始」=`StartBattle()`→`ShowScenarioSelect()` で選択画面表示／`SelectScenario(string)`・`SelectPlayerFaction(int)` で `GameSettings` に反映＆ハイライト更新／「この設定で会戦開始」=`BeginBattle()` で Save＋ResetStats＋`LoadScene("Battle")`／「戻る」=`HideScenarioSelect()`。Canvas/EventSystem(`InputSystemUIInputModule`) が無ければ生成。 |
| `ResultManager` | Result シーン | `GameSettings` の戦績を `winnerText`/`statsText` に表示。`BackToTitle()`。 |
| `PauseManager` | Battle シーン | Space=ポーズ／Esc=ポーズメニュー／数字1-3=倍速。`Time.timeScale` 制御。ポーズUIをコードで自動生成し EventSystem(`InputSystemUIInputModule`) を保証。`SetVolume`/`SetAlwaysShowGizmos`。 |

### 入力・UI・カメラ
| クラス | 対象 | 責務 / 主なAPI |
|---|---|---|
| `FleetCommander` | 入力統括(1) | 選択の唯一の窓口 `SelectedFleets`(`List<Selectable>`)。左ク=選択（空白で解除／別艦で切替、UIは無視）／右ク=`CommandMenu`。移動先指定モード `StartWaitingForMoveTarget()`＋`IsWaitingForMoveTarget`：カーソルで位置→**右押下で目標確定→押したままドラッグで向き→離して確定**（`SetDestination(pos, 向き)`）、Esc/で取消。`FormationPreview` で配置を半透明表示。攻撃目標指定モード `StartWaitingForAttackTarget()`＋`IsWaitingForAttackTarget`：**左クリック=通常攻撃を即時発令／右クリック=攻撃種別メニュー(`CommandMenu.OpenAttackTypeMenu`、通常/ミサイル)を開く**。敵艦（旗艦/配下艦）を親までさかのぼり**その敵艦隊全体**を選択中全部隊の手動攻撃目標に設定（`ConfirmAttack`→`SetManualTargetFleet`＋`SetMissileMode`、確定で `ConfirmPendingAttack`）、`FleetHUDManager.ShowMessage` で「攻撃対象：◯◯艦隊（種別）」を表示、Escで取消。**艦隊円**(`UpdateFleetCircles`/LateUpdate、`Squadron.GetBoundingCircle`＋LineRenderer プール、マテリアル共有・頂点色で色分け)：選択中の艦隊は常時緑円、攻撃目標指定中は全敵艦隊を黄円（カーソル下は橙）で囲う。色/線幅は `selectionCircleColor`/`targetCircleColor`/`targetHoverColor`/`circleWidth`。確定/取消後 `DeselectAll`。`SelectFleet`/`GetMouseWorldPosition()`。 |
| `CameraController` | Main Camera | パン(WASD/矢印/中ドラッグ)・ズーム(ホイール,`zoomSpeed`/min/maxZoom)・開始時は `startZoom` で少し引いた画・F=選択艦隊フォーカス・min/maxBounds クランプ・撃沈時 `Shake()`（unscaled で減衰、LateUpdate でオフセット復元）。 |
| `CommandMenu` | uGUI | 右クリックメニュー。選択状況で 移動/攻撃/陣形変更/情報/選択 を動的生成。「攻撃」は選択中つねに表示し、押すと `FleetCommander.StartWaitingForAttackTarget()` で目標指定モードへ移行（左ク=通常攻撃／右ク=攻撃種別メニュー）。`OpenAttackTypeMenu(screenPos)` で通常/ミサイルの選択メニューを開く。陣形サブメニューのボタンは `Formation` enum から動的生成（`BuildFormationButtons`、シーン手配線に非依存）。`OpenMenu`/`CloseMenu`/`IsOpen`/`ChangeFormation(int)`。画面端クランプ。 |
| `FleetHUDManager` | uGUI | 選択艦隊の 提督/陣営/兵力バー/士気バー/陣形 を表示。任意で `shipCountText`(旗艦艦艇数＋配下艦残存数)を表示。`ChangeFormation(int)`。`ShowMessage(text, duration)` で画面上部に通知を実時間で一定時間表示（攻撃対象通知などに使用、TMPテキストを実行時生成）。 |

### 艦隊コンポーネント（旗艦 GameObject に付く）
| クラス | 責務 / 主なAPI |
|---|---|
| `Selectable` | 選択状態と `selectionRing` 表示。`SetSelected(bool)`/`IsSelected`。 |
| `Squadron` | 旗艦中心の陣形配置(SmoothDamp追従、向きは旗艦同期)。Start で旗艦スプライトを流用し `escortCount`(既定50)隻まで配下艦を生成（手置きの子があれば含めて補う）。配下艦は `memberScale`(既定0.8)で縮小（root スケールは変えない）。各配下艦に `EscortShip` 付与＋艦艇数=`escortShipCount`(既定200)設定(`SetupEscorts`)。生成後 `FactionColor.ApplyColors` で着色。Awake で `FlagshipMarker` 自動付与。陣形スロットは隻数/陣形変化時のみ再計算しキャッシュ。交戦中(`FleetWeapon.IsInCombat`)は `combatSmoothTime`(既定1.2)でゆっくり追従（穴埋め移動も緩やか）、非交戦時は `smoothTime`(0.3)。消滅艦は `RemoveMember` で除外（`velocities` と添字同期）。`GetEscortStatus`(HUD用)/`GetFormationSlots`/`GetShipSprite`/`GetShipColor`(プレビュー用)。`currentFormation`/`spacing`/`smoothTime`/`combatSmoothTime`/`escortCount`/`escortShipCount`/`memberShips`/`memberScale`。`Formation` enum 定義元。 |
| `EscortShip` | 配下艦の戦闘単位。`IShipTarget` 実装。`shipCount`(艦艇数、旗艦より少なめ既定200、通常は `Squadron.escortShipCount` が設定)＋陣営は所属旗艦に従属。出現時 `FleetRegistry.Register`／消滅・退却時 `Unregister`、初回発砲位相をランダム化。`TakeDamage` で減算、0以下で `Squadron.RemoveMember`＋`Destroy`。被弾判定用 `CircleCollider2D`(trigger) を Awake で自動付与。攻撃は旗艦の `WeaponArc`/`FleetWeapon` を流用し、自分の位置・向きで射界内最寄り敵を撃つ（自前 LineRenderer ビーム、`fireInterval` 間隔にスロットル）。旗艦退却中は `IsAlive=false`＋発砲停止。**`Squadron` が Start で自動付与**。 |
| `FlagshipMarker` | 旗艦識別マーカー。頭上に金色＋黒フチのダイヤ型アイコン（子 `"FlagshipMarker"`(SpriteRenderer)）を実行時生成。配下艦には付かないので旗艦の目印になる。色は**陣営非依存の固定色**(`markerColor`、既定=金)で `FactionColor` 着色対象外＝艦に埋もれず一目で分かる。常に艦の真上・水平に表示(LateUpdate ビルボード)。`height`/`markerScale`/`sortingOrder`/`markerColor`。**root スケールは変えない**（陣形計算が狂うため）。`[RequireComponent] Squadron`。**`Squadron` が Awake で自動付与する**（プレハブ編集不要。色等を調整したい時だけ手動で付けて値を変える）。 |
| `FleetMovement` | `SetDestination(Vector2 pos, float? facingAngleZ=null)`（回頭→加減速前進。到着後 facing 指定があればその場回頭してから停止）／`FaceTarget(Vector2)`（その場回頭、交戦中の射界維持用）。実効速度は `GetMobilityFactor()`（提督機動×士気×交戦`combatMobilityRatio`）。public: maxSpeed/acceleration/deceleration/rotationSpeed/faceThreshold/arriveDistance。 |
| `WeaponArc` | range/halfAngle/gizmoColor。`IsInArc(Transform)`。`OnDrawGizmos` で扇形描画。実行時は子 `"WeaponArcLine"`(LineRenderer) を生成し、`GameSettings.alwaysShowGizmos` が true の時だけ表示。 |
| `FleetWeapon` | 射界内の最寄り敵**個艦(`IShipTarget`)**を自動攻撃(`fireInterval`)。手動攻撃目標は**艦隊単位**が基本＝`SetManualTargetFleet(Squadron)`（旗艦単艦ではなく敵艦隊全体を標的。射界内のその艦隊の最寄り艦を撃つ）。`SetManualTarget(IShipTarget)`(単艦・後方互換)/`ClearManualTarget()`/`HasManualTarget`。手動目標があると `HandlePursuit`→`PursueToward` で**追尾**：射程外(`pursuitStopRatio`)は `FleetMovement.SetDestination` で接近、射程内は `FaceTarget` で停止＆敵を向き続ける。艦隊目標は旗艦位置を追尾基準にし、敵旗艦が退却＝艦隊消滅で自動解除（単艦目標は撃沈・退却で解除）。AI制御中(`FleetAI.enabled`)の艦は追尾しない。移動命令(`FleetCommander.ExecuteMoveCommand`)で手動目標は解除される。**ミサイル攻撃**：`SetMissileMode(bool)`／`missileAmmo`(残弾)／`missileDamageMultiplier`／`missileBeamColor`／`MissileAmmo`。ミサイルモード中は残弾がある間だけ威力強化×倍率で1発消費、弾切れで自動的に通常攻撃へ移行（目標艦隊消滅・`ClearManualTarget` でも解除）。敵探索・ダメージ計算は `ShipCombat` に集約。側背面ボーナス(`flankMultiplier`、真後ろで最大)。`IsInCombat`。`combatMobilityRatio` はここに置く。旗艦退却中(`FleetStrength.IsRetreating`)は発砲停止。ビーム演出、`DamagePopup.Show`、`AudioManager.PlayBeam`。`[RequireComponent] WeaponArc`。 |
| `FleetStrength` | `IShipTarget` 実装（旗艦＝個艦）。admiralData/admiralName/strength(=旗艦艦艇数)/maxStrength/faction。`ApplyAdmiralData()`（能力反映＋色再適用）、`TakeDamage(int)`（防御で軽減→士気低下→被弾フラッシュ→**0で破棄せず `BeginRetreat()`＝部隊退却**）。`IsRetreating`/`IsAlive`(退却で false)/`retreatDistance`。退却時：AI停止＋敵と反対方向へ離脱、以降は標的・勝敗カウントから除外。頭上ラベル(legacy `TextMesh`、子名 `"StrengthDisplay"`)。 |
| `FleetMorale` | 士気管理。非交戦で回復・交戦で低下・被弾で低下(`OnTakeDamage`)。`GetMoraleFactor()`/`IsRouted`。**敗走(士気0)は交戦が `routedRecoveryDelay` 秒途切れたら自然回復し復帰**する。頭上ラベル(子名 `"MoraleLabel"`、低下/敗走時に表示)。`[RequireComponent] FleetStrength`。 |
| `FleetAI` | 敵/非プレイヤー艦隊。`enum AIState{接近,交戦,撤退}`。retreatRatio/searchInterval。敗走(`IsRouted`)や兵力低下で撤退へ。`[RequireComponent] FleetMovement/FleetWeapon/FleetStrength`。 |
| `FactionColor` | 陣営色で子SpriteRenderer全部(`"SelectionRing"`/`"FlagshipMarker"`除外)/TextMesh/`FleetWeapon.beamColor`/`WeaponArc.gizmoColor` を着色。`ApplyColors()`。imperialColor/allianceColor。`[RequireComponent] FleetStrength`。 |

### 表示・背景
| クラス | 責務 / 主なAPI |
|---|---|
| `DamagePopup` | 動的生成のダメージ数値ポップアップ。`static Show(worldPos, damage, isFlank)`。jitterで団子化防止、timeScale 追従でフェード。側背面は赤橙＋強調。**同時表示数を `MaxActive`(48) に制限**し、多数の配下艦が同時に撃っても出しすぎない（超過分は間引く）。 |
| `SpaceBackground` | ParticleSystem で星生成、パララックス。SortingLayer `"Background"`/order -100。 |
| `FormationPreview` | 移動先決定中に選択部隊の陣形を半透明表示（旗艦中心＋配下艦スロットに艦スプライトを淡い陣営色 alpha≒0.3 で描画）。`FleetCommander` が実行時生成し `Show(Squadron)`/`SetPose(pos, angleZ)`/`Hide()`。スロットは `Squadron.GetFormationSlots()` を利用。 |

## 壊すと不具合になる依存・命名
- 旗艦の子オブジェクト名は固定。`FactionColor`/`FleetStrength.Flash`/`Squadron` がこれらの名前で除外・再利用判定する。**変えない・重複生成しない**：
  - `"SelectionRing"`（選択リング。陣営着色・配下艦集計から除外、黄色固定）。
  - `"StrengthDisplay"`（兵力ラベル。`FleetStrength` が実行時生成する legacy `TextMesh`）。
  - `"MoraleLabel"`（士気ラベル。`FleetMorale` が生成）。
  - `"WeaponArcLine"`（射界線。`WeaponArc` が生成。`FleetWeapon` のビーム用 LineRenderer と別物）。
  - `"FlagshipMarker"`（旗艦識別マーカー。`FlagshipMarker` が生成する子 SpriteRenderer。固定の金色＝`FactionColor` も `FleetStrength.Flash` も着色対象から除外する。`Squadron` の配下艦自動収集もこの名前を除外する）。
- 旗艦と配下艦の見分けは「`FlagshipMarker` のマーカー」＋「`Squadron.memberScale` による配下艦の縮小」で行う。**旗艦 root のスケールは変えない**（`Squadron` の陣形計算が `TransformPoint` を使うため、root 拡大で陣形間隔が狂う）。
- `combatMobilityRatio` は `FleetWeapon` にある。`FleetMovement` が `IsInCombat` と共に読む。移動側に重複定義しない。
- 選択中艦隊の唯一の窓口は `FleetCommander.SelectedFleets`。別の場所に選択管理を新設しない。
- シーン間で共有する設定・戦績は `GameSettings.Instance` の一択。別の保管所を作らない。
- `Squadron.memberShips` は Inspector で明示割り当て推奨（未設定だと上記マーカー名を除く全ての子を配下艦扱いにする）。
- Esc の優先順位は「`CommandMenu` を閉じる ＞ 移動/攻撃目標指定キャンセル ＞ ポーズメニュー」。`PauseManager` は `CommandMenu.IsOpen`/`FleetCommander.IsWaitingForMoveTarget`/`FleetCommander.IsWaitingForAttackTarget` を見て譲る。崩さない。
- シーン名 `"Title"`/`"Battle"`/`"Result"` と Build Settings の登録を一致させる。
- `ScenarioData.scenarioName` と `GameSettings.scenarioName` を一致させる（`BattleSetup` が名前一致で解決）。会戦アセットは `Resources` 配下に置く。

## 既知の重複・将来の整理対象（新規はここに寄せる・増やさない）
- 陣営色は **`FactionData.color` が唯一の出所**（`FactionColor`/`FleetHUDManager` は `FactionData` があればその色、無ければ enum 既定色＝`imperial/alliance/empire/allianceColor` にフォールバック）。新たな陣営色のハードコードを増やさない。enum 既定色は後方互換のフォールバック専用。
- `ChangeFormation(int)` が `CommandMenu` と `FleetHUDManager` に重複。一本化したい。
- 敵探索は `FleetRegistry`（単一在庫）＋`FactionRelations.IsHostile` に集約済み。新たに `FindObjectsByType` での索敵や「`faction` 違い＝敵」の直書きを増やさない（敵対判定は必ず `FactionRelations` 経由）。`BattleSetup.ClearExistingFleets` の開始時除去のみ `FindObjectsByType` 例外＝登録前の手置き艦を拾うため。配下艦の発砲・索敵は `fireInterval` 間隔＋初回位相をランダムにずらして全艦同時更新を避ける。
- 日本語フォント読み込みコードが3箇所に重複（`FleetStrength`/`FleetMorale`/`DamagePopup`）。挙動は統一済み（下記）。

## 日本語表示の注意
- 頭上ラベル/ダメージ表示は legacy `TextMesh`：実行時フォントは `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`。**Unity6 で `"Arial.ttf"` は廃止され例外を投げる**（過去に一瞬で艦隊が消えるバグの原因）。エディタ内では `Assets/Fonts/msgothic.ttc` があれば優先。
- UI の TMP は `Resources` の `"JapaneseFont_TMP"`(TMP_FontAsset) をフォールバックに使う。

## NG リスト
- 旧 Input API、3D前提コード（全軸Quaternion回転・zを使った移動）、Inspector非公開のハードコード。
- 基準値を直接上書きする一時的な能力変更（実効値パターンを使う）。
- 陣営色・陣形変更ロジックの重複実装、`FleetCommander` 以外への選択管理新設、`GameSettings` 以外へのシーン間状態の新設。
- シングルトン（`GameSettings`/`AudioManager`/`SceneLoader`）の重複生成・別実装。
- 固定の子オブジェクト名（`SelectionRing`/`StrengthDisplay`/`MoraleLabel`/`WeaponArcLine`）の変更や二重生成。

## 運用
- 新コンポーネントを作ったら、この「既存コンポーネント」表に1行追記する。
- 変更は Git でコミットしながら進める（権限「Don't Ask」運用のため、レビュー・巻き戻し前提）。
- 詳細な機能ロードマップは別ファイルの「機能追加ロードマップ」を参照（必要時に手動で読み込ませる）。
