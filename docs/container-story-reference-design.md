# レビンソン『コンテナ物語』参考設計（EPIC #CNTR）

> 参照元：マルク・レビンソン著『コンテナ物語』（原題 *The Box*、2006）。コンテナの規格統一が輸送コストを激変させ、世界の経済地理そのものを書き換えた過程を描くノンフィクション。
> 本ドキュメントは、当プロジェクト（Ginei＝星間国家戦略）への**役立つ視点だけ**を抽出し、EPIC `#CNTR` として issue 化する提案。
> **著作権注意**：固有名・文章・人名・固有設定は流用せず、**輸送メカニクス／経済地理の構造パターンのみ**を参考にする。

---

## 0. なぜ本システムに役立つか

当プロジェクトは輸送・物流関連の純ロジックをすでに保有している：

| 既存（バイナリ接続ベース） | カバー範囲 |
|---|---|
| `LogisticsRules` (GEO-3 #844) | 所有星系の最大連結成分→一体化度 `CohesionFactor`（接続の有/無） |
| `SupplyRules` (L-2 #94) | 補給線到達（到達/遮断のバイナリ） |
| `CommerceRaidingRules` (L-3 #95) | 通商破壊（輸送の阻害） |
| `GalaxyMap`/`Corridor`/`Corridor.type` | 星系ネットワーク（通商/要衝2種） |
| `ResourceProductionRules` (L-1 #93) | 安定度比例の資源産出 |
| `MarketRules`/`Market` (#179-182) | 単一市場の需給均衡 |

**しかし、これらはすべて「繋がるか/繋がらないか」というバイナリ。**コンテナ物語が固有に持つ視点は：

| 本作が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **輸送コストは連続変数**（規格化度×積み替え能力で下がる） | `LogisticsRules.CohesionFactor` はバイナリ接続。高コスト回廊と低コスト回廊の差がない |
| **ハブ星系（中継拠点）の機能**：積み替え能力が高い星系は周辺ネットワーク全体の効率を高める | `Corridor` は直接接続のみ。中継拠点の優位性がない |
| **規格化の外部性**：採用者が増えるほど互いの輸送コストが下がるネットワーク効果 | 外交的インセンティブとして「共通規格採用」がない |
| **先行投資と遅れの非対称性**：整備した星系がハブを独占し繁栄、遅れた星系は衰退 | `Province` に安定度/統合度はあるが、輸送インフラ水準がない |
| **技術変化が旧来中継点を無効化する**（旧港から新港へ貿易が移動） | 既存インフラの陳腐化・星系の盛衰が輸送構造から生成されない |

**結論**：コンテナ物語は既存の輸送・物流レイヤーに**「輸送コストの動学」**という欠落軸を与える。版図の一体化度が「繋がるか否か」から**「どのコストで繋がるか」**に格上げされ、星系の盛衰が経済地理として自然発生する。フェザーン#160（商社国家）の「なぜここが栄えているか」に最もリアルな裏打ちを供給する。

---

## 1. 役に立つ視点（要約）

1. **輸送コストの連続変化が比較優位を書き換える**。遠くても安く運べれば生産地になれる。→ `LogisticsRules.CohesionFactor` をコスト加重に拡張。
2. **ハブ&スポーク：中継点の能力が周辺ネットワーク全体を変える**。星系に「積み替え能力」を与えると経済地理が自然発生。→ `TransshipmentRules` 新設。
3. **規格化の外部性：隣接勢力との共通規格採用が双方に経済的利益**。外交が「合意→経済的利得」に直結する。→ `StandardizationRules` 新設・`FactionRelations` 接続。
4. **先行投資と遅れの非対称性：最初に整備した星系がハブを独占**。インフラ整備の意思決定が地政学的勝敗を決める。→ `EventEngine` 接続・`Province` 拡張。
5. **技術変化が旧来の中継点を無効化する**。経済地理の「当然」は変わりうる。→ `EventEngine` + `DisclosureLedger` の世界観 lore。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LogisticsRules`/`SupplyRules`/`MarketRules`/`CommerceRaidingRules` を作り直さない**。CNTR はそれらに**欠落軸を足し、接続する**だけ（additive）。タイクン化回避＝微操作でなく高位の決断とエンジン駆動。

### ★★★ 最優先（真の欠落・本作の signature）

#### CNTR 輸送コスト係数 `TransportCostRules`（CNTR-1）
- **回廊に輸送コスト係数**（連続値 0.5〜2.0）を持たせる。係数は両端星系の「積み替え能力」と勢力間の「規格一致度」から `TransportCostRules.ComputeCost` が算出。
- `LogisticsRules.CohesionFactor` の拡張：`WeightedCohesion`（最大連結成分をコスト加重で計算）。コストが高い回廊は一体化に寄与しにくい。
- 低コスト回廊ほど `ResourceProductionRules.Produce` の産出や `MarketRules` の星系間価格差が縮小（遠距離生産地が競争可能になる）。
- 接続：`LogisticsRules` 拡張 ＋ `ResourceProductionRules` ＋ `MarketRules`。**純ロジック・test-first（TestHarness/EditMode テスト必須）**。

#### CNTR ハブ星系・積み替え能力 `TransshipmentRules`（CNTR-2）
- 星系に**積み替え能力** `hubCapacity`（0..1）を持たせる（`Province` フィールド拡張 or 別 DTO）。
- `TransshipmentRules.EffectiveCost(corridor, map, provinces)`：経路上のハブ能力が高いほど通過コストを低減。
- ハブ化した星系は `GovernanceRules.OutputFactor` に加算ボーナス＝貿易量増加の恩恵（比較優位の自然発生）。
- 接続：`TransportCostRules`（CNTR-1）＋ `Province` 拡張 ＋ `GovernanceRules.OutputFactor`。

### ★★ 高（マクロ均衡に動学を足す）

#### CNTR 規格化の外部性 `StandardizationRules`（CNTR-3）
- 勢力ペアごとの**共通規格採用度** `standardCompatibility`（0..1）→ 接続回廊の輸送コスト低減係数に乗算。
- 採用勢力数が増えるほど互いの恩恵が大きくなる（ネットワーク効果）。
- `FactionRelations`（非敵対勢力）との連携：外交交渉に**経済的インセンティブ**を追加（規格共有条約 = DIP-2#191 `TreatyType` 拡張候補）。
- 接続：`TransportCostRules`（CNTR-1）×`FactionRelations`×外交 DIP 系。**純ロジック・test-first**。

#### CNTR インフラ先行投資イベント（CNTR-4）
- `EventEngine` 接続：星系に「港湾整備投資」の決断イベント（コスト支出→`hubCapacity`↑）。
- 先行投資→ハブ化（通過貿易が集まり `OutputFactor`↑）、後手→旧来港の衰退（`Province.stability` 低下）。
- AI 勢力も投資判定を行う（`CampaignRules.Tick` 経由）。
- 接続：`EventEngine`（#116）×`Province` 拡張（CNTR-2）×`GovernanceRules`。コード新設最小（`EventEngine` データ追加のみ）。

### ★ 中（lore）

#### CNTR（lore）技術革新が経済地理を書き換えるという世界観開示（CNTR-5）
- 「輸送コストの断絶的低下が産業立地を書き換えた」「旧型中継点の衰退は規格化の必然帰結だった」というゲーム内史観。
- **コード新設なし**。`DisclosureLedger`（FND-4）への lore データ入力のみ。CCX-6（世界観 Codex）方針に一貫。
- 接続：`DisclosureLedger`×`EventEngine`（輸送コスト低下イベントから連鎖開示）。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 実際の海運物理（コンテナ仕様・積載計算・荷役手順） | タイクン化。微操作を避け高位の決断のみ |
| 港湾労働組合との交渉システム | `PartyRules`/`MinistryRules`/`FactionState` で十分カバー |
| 多段サプライチェーン（BOM・加工工程） | SCM#982 がカバー |
| 輸送会社・船会社の経営・株式上場 | FRM#1022（商社国家）×`StockMarketRules`#185 がカバー |
| 港湾建設コストの詳細計算 | タイクン化。係数の背景として扱う |
| 回廊ごとの輸送量のリアルタイム渋滞計算 | タイクン化。係数近似で十分 |

---

## 3. EPIC #CNTR の子 Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存物流ロジックは**接続のみ・重複新設しない**。
> **著作権注意**：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CNTR-1** | #1611 | 輸送コスト係数 `TransportCostRules`（回廊コスト連続値・`LogisticsRules.WeightedCohesion`拡張） | `LogisticsRules`拡張・`ResourceProductionRules`・`MarketRules` |
| **CNTR-2** | #1612 | ハブ星系・積み替え能力 `TransshipmentRules`（`hubCapacity`投資→周辺コスト低減・`OutputFactor`↑） | `Province`拡張・`GovernanceRules.OutputFactor`・CNTR-1 |
| **CNTR-3** | #1614 | 規格化の外部性 `StandardizationRules`（共通規格採用度→輸送コスト低減・外交経済インセンティブ） | `FactionRelations`・外交 DIP 系・CNTR-1 |
| **CNTR-4** | #1616 | インフラ先行投資イベント（港湾整備決断→ハブ化 or 後手→旧来港衰退・`EventEngine`接続） | `EventEngine`（#116）・`Province` (CNTR-2)・`GovernanceRules` |
| **CNTR-5** | #1618 | （lore）技術革新が経済地理を書き換えるという世界観開示データ | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`CNTR-1`（輸送コスト関数の純ロジック基盤）→ `CNTR-2`（ハブ能力・`Province`拡張）→ `CNTR-3`（規格化外部性）→ `CNTR-4`（イベント配線）→ `CNTR-5`（lore）。

> いずれも既存輸送・物流・市場ロジックを**後退させず接続**する additive 設計。フェザーン#160（商社国家）の経済的優位の「なぜ」に最もリアルな裏付けを与える。
