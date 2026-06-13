# 宮崎駿『風の谷のナウシカ』（原作漫画）参考設計（EPIC #NAUS）

> 参照元：宮崎駿『風の谷のナウシカ』原作漫画（全7巻）。千年後の地球を舞台に、複数勢力が覇を競いながら、古代文明が残した汚染と「腐海」の謎を解き明かしていく作品。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**生態系メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「風の谷のナウシカ」が本システムに役立つか

当プロジェクトはすでに政治・経済・社会の純ロジック層を大量に保有している。

| 既存（カバー範囲） | 内容 |
|---|---|
| `GovernanceRules.OutputFactor` | 安定度比例の産出係数（政治的安定→経済産出） |
| `HopeRules`/`Community` | 希望・絶望・末人の発火（フロストパンク参考） |
| `ConsentRules`/`Polity` | 合意の撤回・統治不能（ガンジー参考） |
| `NonviolenceRules`/`Movement` | 非暴力・道徳の柔術 |
| `ReligionRules`/`Religion` | 改宗圧力・異端・聖戦 |
| `DisclosureLedger` + `DisclosureEntry` | 秘史の連鎖開示・真相→エンディング（FND-4） |
| `PlanetSiegeRules` | 惑星攻城・制空権・占領 |
| `FactionStateRules` | 社会・政治シミュの合成 |
| `DynastyRules`/`Regime` | 天命喪失・王朝サイクル（孔子参考） |

**しかしこれらは「政治的安定」を中心に回っており、ナウシカ漫画が固有に描く以下が欠けている：**

| ナウシカが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **惑星・星系の生態系健全度**（腐海の浄化サイクル） | 政治的安定とは独立した**生態系状態変数**が無い。戦闘・開発が環境を永続的に傷つける仕組みが無い |
| **汚染累積と多世代浄化サイクル** | `Calendar`は人間スケール（年）。**数十〜数百年スケールの環境過程**が無い |
| **超兵器のジレンマ**（短期最強・長期最悪の禁断選択肢） | `PlanetSiegeRules`に兵器概念あり。**「使えば世界を永続的に傷つける」禁断の選択肢とそのコスト構造**が無い |
| **感情伝染と集合行動の波及** | `HopeRules`は単一共同体の内部状態。**絶望・悲嘆が隣接システムへ伝播し蜂起が波及する**回路が無い |
| **真実の開示→陣営の政治的行動転換** | `DisclosureEntry.onReveal`は任意デリゲート。**開示が系統的に勢力の正統性・戦略を書き換えるモデル**が無い |

**結論**：ナウシカ漫画は当プロジェクトに①環境健全度という新しい星系属性、②世代をまたぐ汚染・浄化サイクル、③超兵器の禁断コスト、④感情波及と蜂起の動態、⑤開示が政治を変える因果モデルという**5つの欠落軸**を与える。いずれも政治・社会層と接続しながら独立した新モジュールとして実装可能であり、既存の純ロジック群を後退させない。

---

## 1. 役に立つ視点（要約）

1. **腐海は敵ではなく浄化機構**＝「有害に見えるものが実は治癒」。開示が世界観を根本から反転させる。→ `DisclosureLedger`の連鎖開示に最も深いloreを与える。
2. **環境は政治とは独立したタイムスケールで動く**＝王朝は滅びても環境は残り、環境が滅べば王朝は意味を失う。→ **生態系健全度**という新しい星系属性（既存の`Province.stability`とは独立）。
3. **感情伝染・集合行動のモデル**＝個々の悲嘆が閾値を超えると不可逆な集団行動へ波及する。→ `HopeRules`×`GalaxyMap`の波及モデル。
4. **超兵器は勝利より先に世界を壊す**＝短期軍事利益と長期生態コストのトレードオフ。→ 軍功→天命喪失→環境ダメージの三重コスト構造。
5. **多勢力が互いを滅ぼしながらも共存する構造**＝「全滅」でも「統一」でもなく生態系的均衡。→ 既存の多勢力システム（#189 外交・`FactionRelations`）への世界観的根拠。
6. **開示が政治を変える**＝ある真実を知った勢力はその真実の重みぶん正統性・戦略を変える。→ `DisclosureLedger`と`FactionState`を繋ぐ因果モデル。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`Province`/`HopeRules`/`DisclosureLedger`/`PlanetSiegeRules` を作り直さない**。NAUSはそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ナウシカの signature）

#### NAUS 惑星環境健全度（`EnvironmentRules` / `EnvironmentState`）
- 星系/惑星に**環境健全度**（`ecologyHealth` 0..1）を持たせる純ロジック。
- 低下要因：戦闘（`BattleHandoff`の決着→ダメージ係数）、過開発（`OutputFactor`高追求）、超兵器使用（NAUS-3）。
- 回復要因：時間経過（緩やかな自然浄化・`CalendarDispatcher`の年次tick）、意図的な回復政策（`EventEngine`選択肢）。
- 効果：`ecologyHealth`が`GovernanceRules.OutputFactor`に係数として乗る（生態系破壊→産出低下）＋`Province.stability`の均衡値を下押し。
- 接続：`Province`・`GovernanceRules`・`CalendarDispatcher`（すべて既存）。純ロジック・test-first。

#### NAUS 感情伝染と集合行動の波及（`GriefContagionRules`）
- `Community.hope`が閾値以下の星系から、`GalaxyMap`の同一勢力回廊で隣接する星系へ**絶望が伝播**するモデル。
- `GriefContagionRules.Propagate(map, communities, factionId, dt)` → 感染率`contagionRate`×回廊距離の逆数で隣接系のhopeを押し下げる。
- 複数星系が同時に閾値を割ると`EventEngine`で**大蜂起イベント**を発火（`HopeRules.UpdateDissent`と連動）。
- 接続：`HopeRules`/`Community`・`GalaxyMap`・`EventEngine`（すべて既存）。純ロジック・test-first。

### ★★ 高（世代をまたぐ過程と禁断の選択）

#### NAUS 汚染累積・多世代浄化サイクル（`PollutionRules`）
- 星系ごとに**汚染濃度**（`toxicLevel` 0..1）を管理する純ロジック。
- 累積：戦闘・超兵器・過開発によって上昇（NAUS-1の`EnvironmentState`書き込み経由）。
- 浄化：年次tick（`CalendarDispatcher`）で`purificationRate`ぶん緩やかに低下（既定=年0.5%・100年で半減まで回復）。
- 効果：`toxicLevel`が高い星系は`EnvironmentRules.EcologyFactor`をさらに押し下げる（環境健全度＋汚染の二重係数）。
- 接続：NAUS-1・`CalendarDispatcher`・`GovernanceRules`（既存）。純ロジック・test-first。

#### NAUS 超兵器のジレンマ（`CatastrophicWeaponRules`）
- 圧倒的短期軍事利益 vs 長期生態コスト＋正統性崩壊の意思決定モデル。
- `CatastrophicWeaponRules.Resolve(attacker, target, weaponTier)` → `WeaponOutcome`{militaryDamage, ecologyDamage, legitimacyPenalty, warWearinessDelta}。
- `ecologyDamage`はNAUS-2の`toxicLevel`を大きく引き上げ（回復に数百年）、`legitimacyPenalty`は`Regime.legitimacy`を直撃。
- 接続：NAUS-1/2・`DynastyRules`/`Regime`・`WarGoalRules.WarWeariness`（DIP-3 #192）（すべて既存）。純ロジック・test-first。

### ★ 中（開示との接続・lore）

#### NAUS 真実開示→陣営行動転換（`RevealConsequenceRules`）
- `DisclosureEntry.onReveal`を**系統的に**`FactionState`へ接続するモデル。
- `RevealConsequenceRules.Apply(entry, factionState)` → `legitimacyDelta`・`cohesionDelta`・`strategyTag`（方針変更ヒント）。
- 例：「汚染は古代文明の業だった」＝開発推進勢力の`Regime.legitimacy`が下がる。
- 接続：`DisclosureLedger`/`DisclosureEntry`・`FactionState`/`FactionStateRules`・`DynastyRules`（すべて既存）。純ロジック・test-first。

#### NAUS（lore）世界観の開示データ（腐海の秘史・古代文明の業・生命の連帯）
- 新コードなし。`DisclosureLedger`への**データ入力**：
  - 断片A：「生態系の毒素は長期的に土壌を浄化する」（条件：環境研究ある程度進展）
  - 断片B：「この生態系は過去の文明が汚染対策として設計した人工機構」（条件：断片A＋古代遺跡探索）
  - 真相：「汚染が完全に浄化された後の世界には清浄な環境が待っている」（条件：断片A/B両方）
  - エンディング候補：「生態系との共生ルート」（条件：真相開示＋特定の政策選択）
- 接続：`DisclosureLedger`（FND-4）。コード新設なし・テスト不要（データのみ）。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 非暴力・抵抗運動 | `NonviolenceRules`/`Movement`（ガンジー参考 #831/#832）がカバー済み |
| 宗教による支配・異教の弾圧 | `ReligionRules`（#172-175）がカバー済み |
| 複数勢力の外交・同盟 | `DiplomacyRules`/`FactionRelations`（#189 DIP-1）がカバー済み |
| カリスマの日常化・英雄死後の組織 | `SuccessionRules`/`Organization`（#812）がカバー済み |
| 合意の撤回・統治不能 | `ConsentRules`/`Polity`（ガンジー #836）がカバー済み |
| 生態系の個体レベル詳細シミュ | タイクン化回避。係数・確率モデルで十分 |
| 固有の生物種・固有名・設定 | 著作権注意事項により使用禁止 |

---

## 3. EPIC #NAUS の子Issue（採用分のみ・着手順）

> 純ロジックはTestHarness/EditModeで先に固定（test-first）→ 盤面/UIへ配線。既存の政治・社会ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

**EPIC = #2269**。GitHub issue起票済み（#2273〜#2292）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **NAUS-1** | #2273 | 惑星環境健全度（`EnvironmentRules`/`EnvironmentState`・戦闘/開発→劣化・年次自然回復） | 新純ロジック。`Province`×`GovernanceRules.OutputFactor`×`CalendarDispatcher` |
| **NAUS-2** | #2277 | 汚染累積・多世代浄化サイクル（`PollutionRules`・toxicLevel・年次tick・百年スケール） | NAUS-1×`CalendarDispatcher`×`GovernanceRules` |
| **NAUS-3** | #2282 | 超兵器のジレンマ（`CatastrophicWeaponRules`・圧倒的軍事力 vs 永続的環境コスト＋正統性崩壊） | NAUS-1/2×`Regime.legitimacy`×`WarGoalRules.WarWeariness` |
| **NAUS-4** | #2285 | 感情伝染と集合行動の波及（`GriefContagionRules`・hope閾値割れが隣接系へ波及→大蜂起） | 新純ロジック。`HopeRules`/`Community`×`GalaxyMap`×`EventEngine` |
| **NAUS-5** | #2289 | 真実開示→陣営行動転換（`RevealConsequenceRules`・開示が`FactionState`を系統的に書き換え） | `DisclosureLedger`×`FactionStateRules`×`DynastyRules` |
| **NAUS-6** | #2292 | （lore）世界観の開示データ（生態系の秘史・古代文明の業・生命の連帯） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`NAUS-1`（環境健全度＝後続全体の基盤）→ `NAUS-4`（感情伝染＝NAUS-1に非依存で最も独立した新動態）→ `NAUS-2`（汚染サイクル＝NAUS-1依存）→ `NAUS-3`（超兵器＝NAUS-1/2依存・最も重い決断）→ `NAUS-5`（開示接続）→ `NAUS-6`（lore）。

> いずれも既存の政治・社会ロジックを**後退させず接続**する additive 設計。`DisclosureLedger`（秘史）に最も深い世界観テクスチャを与える。
