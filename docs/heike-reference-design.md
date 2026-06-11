# 『平家物語』参考設計（EPIC #HEIK）

> 参照元：『平家物語』（作者未詳・13世紀成立）。武家最初の覇権者「平家」が壇ノ浦に滅びるまでを描く軍記物語。
> **盛者必衰の理・公武二元支配の矛盾・恩顧と離反の連鎖・敗者視点の記録**——滅亡の物語構造を力学モデルに変換する。
> 本ドキュメントは、当プロジェクト（Ginei＝多勢力戦略×社会シミュ）にとって**役に立つ視点**だけを抽出し、EPIC `#HEIK` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**物語構造・社会メカニクスのパターンのみ**を参考にする。

---

## 0. なぜ『平家物語』が本システムに役立つか

当プロジェクトは王朝サイクル・忠誠崩壊・国家状態の合成を**すでに大量に保有**している：

| 既存（カバー範囲） | 担当モジュール |
|---|---|
| 天命喪失・王朝交代 | `DynastyRules`/`Regime`（#867） |
| 旗幟・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules`（#817） |
| 英雄死後の組織存続 | `SuccessionRules`/`Organization`（#812） |
| 合意と非協力 | `ConsentRules`/`Polity`（#836） |
| 国家状態の合成 | `FactionStateRules`/`CampaignRules` |
| 文民統制・クーデター | `CivilianControlRules`（GOV-4 #145） |
| 実権・傀儡 | `PowerRules`/`PowerActor`（#164） |
| 捕虜・処断 | `CaptivityRules`（#154） |
| 財政・歳出 | `FiscalRules`/`FiscalState`（#161/162） |
| 開示エンジン | `DisclosureLedger`（FND-4 #495） |

**しかし、これらは腐敗・崩壊の静的スナップショットが中心**であり、平家物語が固有に描く以下が**欠けている**：

| 平家物語が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **速い上昇ほど脆いピーク** | `DynastyRules.Tick` は腐敗の累積速度を下げるが「上昇速度→崩壊加速」の非対称性が無い |
| **報酬型忠誠（恩顧）の消耗** | `LoyaltyRules.BaselineLoyalty` は思想・正統性で忠誠を導出。物質的な恩恵（報酬・役職配分）に依存する忠誠が枯れる回路が無い |
| **正統性なき実力者の慢性的不安定** | `CivilianControlRules` はクーデター確率を計算するが「正統性保有者と実力保有者の相互依存＝どちらも単独では安定できない」という二元張力モデルが無い |
| **終末局面の各自選択（死闘・静観・離散）** | `LoyaltyRules.ResolveCascade` は平時の均衡解。勝敗が明白になった終末カスケードで「どこまで戦い続けるか」の個別選択が無い |
| **敗者視点の物語記録** | `DisclosureLedger` の開示エントリは勝者史観。敗者が「なぜ負けたか」を記録する構造が無い |

**結論**：平家物語は既存の崩壊モデルに**時間の向き（速い上昇→脆い頂点）**と**忠誠の質（報酬型 vs 理念型）**という2つの欠落軸を与え、さらに終末選択・二元権力・敗者記録という3軸を補完する。

---

## 1. 役に立つ視点（要約）

平家物語の世界構造を**本システムに効く形**で1行ずつ：

1. **盛者必衰の法則**＝栄えた者は必ず滅びる。但し「速く栄えた者ほど速く滅びる」が核心。→ 上昇速度が崩壊加速度に比例する `OvershootRules` の根拠。
2. **恩顧の連鎖は報酬が止まると切れる**。従者は主君のために戦うが、主君が勝てなくなれば次の主君に乗り換える。→ 物質依存型の `PatronageRules`。`LoyaltyRules` の理念型とは別系統。
3. **公武二元支配の構造的矛盾**。正統性は朝廷が持ち、実力は武士が持つ。どちらも単独では成立せず、互いを食い物にしながら共存する。→ `DualAuthorityRules` の核。
4. **终末の選択肢は多様**。大勢が決したとき、人は死闘・降伏・逃亡・静観と異なる選択をする（個人ごとの閾値と名誉計算）。→ `TerminalCascadeRules` の基礎。
5. **敗者の記録が後世の正統性資源になる**。滅んだ側の物語を継承した者が次の覇権者に「大義」を提供する。→ 開示エンジンの敗者記録エントリ。
6. **海戦・制海権の非対称**。陸戦が得意でも海戦で負ければ逃げ場が無い。→ 既存 `SupplyRules`/`ZoneOfControl`/`StrategyRules` の接続（新規不要）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`LoyaltyRules`/`CivilianControlRules`/`FactionStateRules` を作り直さない**。HEIK はそれらに**欠落軸を足し接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・平家物語の signature）

#### HEIK 絶頂脆弱性 — `OvershootRules` / `AscentRecord`
- **上昇速度の記録**：`AscentRecord` が `FactionState.Stability` の変化率（Δ/ターン）を追跡する。
- **ピーク脆弱性**：安定度が上昇→ピーク→下降に転じた瞬間、上昇時の平均変化率に比例して崩壊加速度を乗算する `BrittlenessModifier`。
  - 緩やかに育った勢力（Δ小）はピーク後も緩やかに衰える。
  - 急速に膨張した勢力（Δ大）はピーク後に急落する（平家モデル）。
- 接続：`DynastyRules.Tick`（腐敗進行への係数）、`FactionStateRules.Stability`（合成時に適用）、`CampaignRules.Tick`（全勢力進行）。
- **基準値非破壊**：`FactionState` の既存フィールドは上書きしない。`AscentRecord` を別構造体として添付するだけ。

#### HEIK 恩顧と配下離散 — `PatronageRules` / `PatronNetwork`
- **報酬型忠誠の分離**：`PatronNetwork`（恩顧主・被恩顧者数・配分可能報酬）を独立構造体として保持。`LoyaltyRules.BaselineLoyalty`（思想/正統性/希望の平均）と**別トラック**で並行動作。
- **恩顧の消耗**：軍事敗北・領土喪失 → 財政収入低下（`FiscalRules`）→ `patronageCapacity` 減少 → 被恩顧者の `clientLoyalty` 低下 → `LoyaltyRules.BaselineLoyalty` に乗算してベースラインを引き下げる。
- **離散閾値**：報酬型忠誠が理念型より低い被恩顧者は「最初に離れる者」になる（`IsPragmaticClient`）。
- 接続：`LoyaltyRules`（ベースライン入力）、`FiscalRules.TaxRevenue`（財政）、`FleetPool`（兵力配分）。既存忠誠ロジックは後退させない。

### ★★ 高（既存モデルに深みを加える）

#### HEIK 公武二元支配の張力 — `DualAuthorityRules`
- **権力ギャップ**：`AuthorityGap = legitimacyScore - militaryScore`（正統性と実力の差）。ギャップが大きいと双方に `AuthorityStrain`（不安定ストレス）が乗る。
- **共依存均衡**：正統性保有者（朝廷型勢力）と実力保有者（武家型勢力）が互いを必要とする共依存状態 `SymbioticPair` を検出し、一方の崩壊が他方を道連れにするリスクを算出（`CollapseContagion`）。
- 接続：`CivilianControlRules`（クーデターリスクへ係数）、`PowerRules.EffectivePower`（実力側の入力）、`ConsentRules.Polity`（正統性側の入力）。既存 `CivilianControlRules` を後退させない。

#### HEIK 終末カスケード — `TerminalCascadeRules`
- **終末フラグ**：`LoyaltyRules.ResolveWinner` で特定の実効兵力比（`terminalThreshold`、既定0.15）を下回ったとき「終末局面」を宣言。
- **個別選択分布**：終末局面の残存者を4分類に分ける：`FightToEnd`（義・指揮官依存）／`SurrenderAttempt`（生存最適）／`Disperse`（逃亡・脱落）／`IdleObserve`（様子見）。分類は `loyalty`×`intrigue`×`rankTier` の組み合わせで決定論的に導出。
- 接続：`LoyaltyRules.ResolveCascade`（既存カスケードの延長）、`CaptivityRules.DefaultDisposition`（降伏者の処遇）、`BattleAllegianceRules`（会戦配線）。

### ★ 中（lore・開示エンジン）

#### HEIK（lore）敗者視点の開示記録 — DisclosureLedger エントリ
- **コード新設なし**：`DisclosureLedger`（FND-4）への**データ入力**のみ。
- 内容：「滅んだ側の最後の選択が後世の規範になる（武士道の起源）」「速く上昇した者が速く滅びるのは歴史の繰り返しパターン」「二元支配は常に短命で終わる」という世界観テーゼを `DisclosureEntry` として登録。
- `onReveal` で：`DynastyRules` へのlore説明ポップアップ解放、endingフラグとの接続。
- 接続：`DisclosureLedger`（FND-4）、`EventEngine`（#116）、`CampaignSaveData`（FND-2 セーブ）。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 制海権モデルの新規実装 | `SupplyRules.SuppliedSystems`・`StrategyRules.IsFtlBlocked`・`ZoneOfControl` が回廊制圧を担保。新規不要。 |
| 朝廷/天皇の制度モデル | `GovernmentRegistry`/`OfficeRules` が役職台帳をカバー。天皇モデルは `Office.scope=国家` の特殊例 |
| 戦場の美学・演出 | コード外の作品性。既存 `NotificationCenter`/`DisclosureLedger` のloreデータ |
| 仏教・浄土思想の信仰モデル | `ReligionRules`（#172-175）が改宗・社会効果をカバー。HEIK 固有の追加なし |
| 武士道・名誉システム | `SeniorityRules`・`CareerPipelineRules`（士官学校系）が威信・席次を担保。専用モジュール不要 |

---

## 3. EPIC #HEIK の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**物語構造・社会メカニクスのパターンのみ**参考。

> **EPIC = #1313**。GitHub issue 起票済み（#1315〜#1325）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HEIK-1** | #1315 | 絶頂脆弱性（`OvershootRules`/`AscentRecord` — 上昇速度→ピーク崩壊加速） | 新 `OvershootRules`。`DynastyRules.Tick`×`FactionStateRules.Stability` |
| **HEIK-2** | #1317 | 恩顧と配下離散（`PatronageRules`/`PatronNetwork` — 報酬型忠誠と物質的消耗） | 新 `PatronageRules`。`LoyaltyRules.BaselineLoyalty`×`FiscalRules` |
| **HEIK-3** | #1319 | 公武二元支配の張力（`DualAuthorityRules` — 正統性と実力の共依存ギャップ） | 新 `DualAuthorityRules`。`CivilianControlRules`×`PowerRules`×`ConsentRules` |
| **HEIK-4** | #1322 | 終末カスケード（`TerminalCascadeRules` — 敗勢確定後の個別選択分布） | 新 `TerminalCascadeRules`。`LoyaltyRules.ResolveCascade`×`CaptivityRules` |
| **HEIK-5** | #1325 | （lore）敗者視点の開示記録 — DisclosureLedger への敗者史テーゼ入力 | コード新設なし。`DisclosureLedger`（FND-4）×`EventEngine`（#116） |

### 推奨着手順

`HEIK-1`（絶頂脆弱性＝最も固有な欠落）→ `HEIK-2`（恩顧消耗＝忠誠の別トラック）→ `HEIK-3`（二元張力＝政治構造）→ `HEIK-4`（終末カスケード＝会戦配線）→ `HEIK-5`（lore入力）。

> いずれも既存の崩壊・忠誠・政治モデルを**後退させず接続**する additive 設計。多勢力の中期戦役（盛期→衰退→滅亡）に最も効く。
