# カエサル『ガリア戦記』参考設計（EPIC #GAL）

> 参照元：ユリウス・カエサル『ガリア戦記』（Commentarii de Bello Gallico）。
> ローマ将軍カエサルがガリア（現フランス）を征服した7年間の自著従軍記。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略）に**役に立つ構造パターンのみ**を抽出する。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**メカニクス／世界観の構造パターンのみ**参考。

---

## 0. なぜ「ガリア戦記」が本システムに役立つか

### 既存（マクロ・抽象）— カバー済み

| 既存モジュール | カバー範囲 |
|---|---|
| `LoyaltyRules`/`Allegiance`（#817） | 忠誠度から旗幟を解決・寝返りカスケード |
| `DiplomacyRules`/`DiplomacyState`（#189） | 外交状態・条約・宣戦 |
| `CaptivityRules`（#154） | 戦闘捕虜の捕縛・解放・処断・登用 |
| `PlanetSiegeRules`/`SiegeArena`（#131） | 惑星攻城（単層） |
| `StrategyRules` | 戦略接触・自動解決 |
| `SupplyRules`（#94） | 補給線・補給切れ |
| `LogisticsRules`（#844） | 版図連結・一体化度 |
| `GovernanceRules`/`Province` | 占領後の統合度・安定度 |
| `WarGoalRules`/`CasusBelli`（DIP-3 #192） | 開戦事由・厭戦 |

### ガリア戦記が固有に持つ視点 × 当プロジェクトでの欠落

| ガリア戦記固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **部族連合の構造的亀裂** | `LoyaltyRules` は各諸侯の忠誠度を個別に解く。**連合に内在する部族間対立（亀裂）の事前モデル**が無い |
| **分割操作（選択的和解で連合を溶かす）** | `ApplyIntrigue` は外部からの調略圧力。**こちら側から特定部族に個別和解を申し込み連合を分断**する窓口が無い |
| **ホスタージュ外交（自発的人質＝政治的担保）** | `CaptivityRules` は戦闘捕虜。**自発的人質提出（非戦闘起源）＝政治拘束**の経路が無い |
| **野戦工兵速度（橋梁・包囲壕の急速建設）** | `ShipyardRules` は艦船建造。**野戦での時間競争的工事＝時間差による戦略的奇襲**が無い |
| **二重包囲（包囲しながら援軍を迎撃）** | `PlanetSiegeRules` は単層。**包囲線の内と外で同時に戦う二正面**が無い |
| **征服叙述の政治資本（勝利報告→権力資本）** | `DisclosureLedger` は秘史の開示。**将軍の軍事成果→国内政治力への変換回路**が無い |

**結論**：ガリア戦記は当プロジェクトに、**①連合亀裂の事前モデル ②分割操作の窓口 ③ホスタージュ外交 ④野戦工兵速度＋二重包囲 ⑤征服叙述の政治資本**という5つの欠落軸を与える。
「戦う前に連合を溶かし、工兵で時間を盗み、人質で平和を買い、勝利で政治を制する」という**武力以外の征服工学**がテーマ。

---

## 1. 役に立つ視点（要約）

ガリア戦記の世界観を**本システムに効く形**で1行ずつ：

1. **敵連合は名目兵力より内部亀裂で決まる**。同盟は共通の敵が居る間だけ成立し、個別和解を提示した瞬間に溶ける。→ `LoyaltyRules.ResolveCascade`（#817）の*前段*として連合の構造的脆弱性モデルが必要。
2. **分割統治は武力でなく外交の精度で決まる**。最強部族に総力をぶつけるより、周辺部族を個別に切り崩す方が安い。→ `DiplomacyRules`（#189）に**選択的和解**の窓口を追加。
3. **工兵は将軍の分身**（10日でライン川に橋を架ける）。敵の「到達不可能」という前提を裏切る工事速度＝純粋な時間圧縮の武器。→ 野戦工兵速度として新規軸。
4. **攻城は援軍との競争**。アレシア包囲（内：包囲壕、外：反包囲壕）＝同時に2方向を向く必要がある構造。→ `PlanetSiegeRules`（#131）に援軍迎撃の第二層を追加。
5. **人質は戦わずに平和を買う担保機構**。自発的人質提出＝ゲーム理論的コミットメント（破棄コストを高める）。→ `CaptivityRules`（#154）に非戦闘起源の人質経路を追加。
6. **コメンタリーは政治的武器**。勝利を書いて元老院・民衆に届けることで凱旋式・権力延長を手に入れる。叙述が権力資本になる。→ `DisclosureLedger`（FND-4）への世界観loreデータ入力。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LoyaltyRules`/`DiplomacyRules`/`CaptivityRules`/`PlanetSiegeRules` を作り直さない**。
> GAL は**欠落軸だけを足し接続する**（additive）。タイクン化回避＝マイクロ操作を増やさない。

### ★★★ 最優先（真の欠落・ガリア戦記の signature）

#### GAL 部族連合の亀裂モデル（`CoalitionFaultlineRules`/`TribeFaultline`）
- **亀裂**：複数勢力が構成する連合内の「部族間対立スコア」。歴史的恨み・領土紛争・宗教差など。
- 亀裂スコアが高い部族は `Allegiance.loyalty` が低くスタートし、個別和解提示（divide et impera）で離脱しやすい。
- `LoyaltyRules.ResolveCascade` が走る*前*に亀裂から初期忠誠度を決定する**前段フィルタ**。
- 接続：`LoyaltyRules.Allegiance`（亀裂→初期loyalty修正子）×`FactionRelations`（既存部族間対立の有無）。

#### GAL 分割操作ルール（`DivideRules`・Divide et Impera）
- **選択的和解**：こちら側から特定部族だけに条件提示（停戦/利権/自治）。受け入れた部族は連合から脱落。
- 残存連合の実効兵力は `LoyaltyRules.EffectiveStrength` で自動更新（カスケードが止まるまで再計算）。
- **接続コスト**：選択的和解は外交資源を消費（`DiplomacyRules`の延長）＋`WarGoalRules`にcasus belli修正。
- 接続：GAL-1（亀裂スコア）×`LoyaltyRules.ApplyIntrigue`×`DiplomacyRules.SignTreaty`。

### ★★ 高（外交担保・工兵・攻城の拡張）

#### GAL ホスタージュ外交（`HostageRules`/`HostagePledge`）
- **HostagePledge（人質誓約）**：戦闘外で勢力が自発的に提出する「政治的担保」。
- 誓約中は `Allegiance.loyalty` が底上げ（破棄コストが高い＝コミットメント装置）。
- 破棄（人質を処断/脱走/拒否）→ 即 `WarGoalRules.CasusBelli` 発生＋opinion大幅低下。
- 接続：`CaptivityRules`（別経路=非戦闘）×`DiplomacyRules.TreatyRules`×`WarGoalRules`。

#### GAL 野戦工兵速度＋アレシア型二重包囲（`MilitaryEngineeringRules`/`ContravallationRules`）
- **MilitaryEngineeringRules**：勢力の工兵能力（`engineeringLevel`）が**野戦工事完成時間**を短縮。
  - 橋梁：未接続の回廊を一時的に接続（補給線・進軍経路を開く）→ `StrategyRules.IsFtlBlocked` 回避の時間窓。
  - 包囲壕：`SiegeArena` の `approachRadius` を外側から収縮（攻撃側有利）。
- **ContravallationRules（反包囲壕）**：援軍が到着した場合に**外向きの防御線**を自動展開。
  - 攻城者は内側（惑星/要塞）と外側（援軍）を同時に相手にする。
  - `PlanetSiegeRules.Tick` の攻城進行を援軍存在時は減速（援軍が防御線を突破するまで収束しない）。
- 接続：`PlanetSiegeRules`（#131 拡張）×`StrategyRules`（援軍判定）×`MilitaryEngineeringRules`（建設速度）。

### ★ 中（世界観lore）

#### GAL （lore）征服叙述の政治資本 → `DisclosureLedger` への入力
- カエサルの『コメンタリー』が示す「勝利を叙述することが政治を動かす」構造。
- `Organization.PersonalCharisma`（#812）と軍事成果の関係。
- `Regime.Reform`（#867）＝叙述→正統性の接続。
- **コード新設なし**。`DisclosureLedger`（FND-4）＋`EventEngine`（#116）へのloreデータとして入力。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| ローマ軍団の階梯（百人隊・大隊・軍団）の詳細実装 | `OrderOfBattle`/`FleetRoster`（#146/#147）で十分。新規実装はタイクン化 |
| 地形地物（ガリアの森・川・霧）の物理シミュ | 係数で背景的に効かせる（新EPIC化しない）。`LogisticsRules`の一体化度で近似 |
| 個人提督の戦闘日誌・叙述レポートの自動生成 | UIコスト大・コード必要なし。`DisclosureLedger`への手入力で足りる |
| ローマ属州制度・市民権の詳細モデル | `GovernanceRules.integration`＋`Province`で十分 |
| 部族ごとの固有ユニット・特殊能力 | ビジュアルマイクロ化。`AdmiralData`/`FactionData`の係数差で十分 |

---

## 3. EPIC #GAL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1340**。GAL-1〜5 = #1343/#1346/#1349/#1352/#1356。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GAL-1** | #1343 | 部族連合の亀裂モデル（`CoalitionFaultlineRules`/`TribeFaultline`・亀裂スコア→初期忠誠度修正子） | 新 `CoalitionFaultlineRules`。`LoyaltyRules.Allegiance` への前段フィルタ |
| **GAL-2** | #1346 | 分割操作ルール（`DivideRules`・Divide et Impera＝選択的和解で連合分断） | GAL-1×`LoyaltyRules.ApplyIntrigue`×`DiplomacyRules.SignTreaty` |
| **GAL-3** | #1349 | ホスタージュ外交（`HostageRules`/`HostagePledge`・自発的人質＝政治的担保） | 新 `HostageRules`。`CaptivityRules`（非戦闘経路）×`WarGoalRules.CasusBelli` |
| **GAL-4** | #1352 | 野戦工兵速度＋アレシア型二重包囲（`MilitaryEngineeringRules`/`ContravallationRules`） | 新 `MilitaryEngineeringRules`。`PlanetSiegeRules`（#131）拡張＝援軍迎撃の反包囲壕 |
| **GAL-5** | #1356 | （lore）征服叙述の政治資本 → `DisclosureLedger`/`EventEngine` への入力 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`GAL-1 → GAL-2`（亀裂モデル→分割操作の連鎖＝ガリア戦記の signature）
→ `GAL-3`（ホスタージュ外交＝戦わずに安定を買う）
→ `GAL-4`（工兵速度＋アレシア型二重包囲＝攻城の拡張）
→ `GAL-5`（lore＝叙述の政治資本）

> いずれも既存の忠誠・外交・攻城ロジックを**後退させず接続**するadditive設計。
> `LoyaltyRules`（#817）の関ヶ原モデルに「戦前の連合脆弱性」という前段を追加することで、
> 「名目でなく実効兵力で勝敗が決まる」原則がより深みを持つ。
