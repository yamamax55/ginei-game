# L5: セーブ/ロード往復のギャップ — 何が永続し何が消えるか

> テスト計画 [test-completion-plan.md] の L5。`CampaignSerializer`（`CampaignSaveData`/`CampaignSaveManager`）の往復で、戦役世界状態が保存復元されるかを検証。
> 検証＝`CampaignSaveRoundTripTests`（EditMode）。

## 0. 結論

**永続する（保存される）**：
- 銀河＝星系（id/名/座標/所有/惑星/類型/居住）・回廊・惑星の攻城状態。
- 勢力の**社会・政治状態**＝`regime`（正統性/腐敗/徳）・`polity`（人口/協力/正統性/抑圧）・`organization`（結束/制度化/カリスマ/分裂）・`community`（希望/抑圧/末人）・`inclusiveness`。
- **`governmentForm`（政体形態 #117）**＝本タスクで保存対象に追加（往復で `共産主義` 等が維持される）。

**永続しない（在席状態＝ロードで既定に戻る）**：
| 状態 | 理由 | 完成に向けた判断 |
|---|---|---|
| `treasury`/`budget`/`fiscal`（財政フロー #163） | 設計上の在席状態（毎期再導出） | 据え置きで可（フローは再構築） |
| `Province` の内政（安定度/統合/思想/人口動態/職業/技能…） | `CampaignState` 非保持＝`StrategySession.Provinces`(static) デモローカル（Battle 往復でリセット） | **要判断**＝惑星内政を永続するなら `CampaignSaveData` へ Province 群を追加 |
| 人物ロスター（`commanders`/`civilians`）＝提督・文官、`serviceStatus`/`militaryDegree`/`hammockNumber`/`warCollegeRank`/`schoolPostingUntilYear`/`captiveStatus`/`wealth`… | `GalaxyView` の in-session リスト＝セーブ非対象 | **要判断・重要**＝銀英伝的にネームド提督喪失は痛い。永続候補の筆頭 |
| `FleetPool`/`FleetRoster`/`OrderOfBattle`/`GovernmentRegistry`/`NamedAssetRegistry`/`FinancialHoldingRegistry` | static レジストリ＝セーブ非対象 | **要判断**＝軍編制・要職・資産を継続するなら永続化 |
| 戦略艦隊（`StrategicFleetRegistry`）・外交（`DiplomacySession`）・通知履歴 | static/in-session | 要判断 |

## 1. 本タスクの修正

- **`governmentForm` を保存対象に追加**（`FactionStateSave.governmentForm`＋`CampaignSerializer` の往復）。これまで保存対象外で、**ロード時に政体が首長制（既定）へ戻るリグレッション**だった（今セッションで政体進化/政変を実装したため顕在化）。前方互換＝旧セーブ（フィールド欠落）は既定 0＝首長制（`JsonUtility` 既定埋め）。

## 2. テスト（`CampaignSaveRoundTripTests`）

- 銀河（星系/回廊/所有/名）と勢力の社会・政治状態が往復で一致。
- **`governmentForm` が往復で保存**（ToSaveData/FromSaveData＋ToJson/FromJson の両経路）。
- 財政フロー（treasury）は往復で既定0＝在席状態の境界を pin。
- 旧JSON（governmentForm 欠落）→ 首長制 既定＝前方互換を pin。

## 3. 完成に向けた推奨（要・作者判断）

セーブが世界状態の**社会・政治・地理のコア**を保存することは確認できた。**継続プレイの実用性**には次の永続化判断が要る（優先度順）：
1. **人物ロスター（ネームド提督/文官）**＝最優先候補。失うと「続きから」で将が消える。`CampaignSaveData` に人物配列（id/名/勢力/能力/階級/学歴/在役/捕虜/生年…）を足す。
2. **軍の static レジストリ**（`FleetPool`/`FleetRoster`/`OrderOfBattle`）＝編制と兵力の継続。
3. **`Province` 内政群**＝惑星の安定度/人口動態を継続するなら（さもなくば Battle 往復で既にリセットされる既知挙動を許容）。
4. 財政フローは在席のままで可（再導出）。

> 注：本タスクでは**最小・確実な修正（`governmentForm` 永続）**のみ適用。1〜3 はそれぞれ `CampaignSaveData` のスキーマ拡張＋`GalaxyView`/static の流し込みが要る広めの作業＝承認の上で個別に。
