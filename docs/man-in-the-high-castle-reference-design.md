# ディック『高い城の男』参考設計（EPIC #MHCL）

> 参照元：フィリップ・K・ディック『高い城の男』(The Man in the High Castle)。枢軸国が第二次世界大戦に勝利した世界を舞台に、ナチス・ドイツと帝国日本が分割占領する米国を描く。
> 偽の歴史を通じた支配・禁書（if 歴史を描いた小説）・占卜（易経）・真贋の問い、という4つの構造が核。
> 本ドキュメントは、当プロジェクト（銀英伝風 星間国家戦略ゲーム）への**構造パターン抽出のみ**を目的とする。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**歴史改変・偽史・競合占領・記録の真偽という世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ『高い城の男』が本システムに役立つか

当プロジェクトは占領の**マクロ純ロジックを保有**している（[CLAUDE.md] 参照）：

| 既存（占領・支配・正統性） | カバー範囲 |
|---|---|
| `GovernanceRules.OnOccupied` / `Province.integration` | 占領直後の統合リセット・安定度低下→時間で統合・安定回復 |
| `ConsentRules` / `Polity` | 住民の非協力・支持閾値・統治不能 |
| `SecurityRules` | 秘密警察・反乱鎮圧・クーデター検知 |
| `CoupRules` / `DynastyRules.Regime` | 政権交代・天命喪失・腐敗・改革 |
| `EspionageRules` | 諜報成功率・情報獲得・破壊工作 |
| `DisclosureLedger` | 条件付き情報開示・連鎖開示・lore |
| `Province.nativeIdeology` + `GovernanceRules.IdeologyModifier` | 住民思想の長期ドリフト・占領勢力との乖離 |

**しかし、これらは「単一占領者 × 均一な住民」を前提**としている。『高い城の男』が固有に持つ以下が**欠けている**：

| 『高い城の男』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **競合占領**（2勢力が同一星系を分割占有・緊張が続く） | `StarSystem.ownerData` は単一。**2占領者が緩衝域を挟んで並立し互いを牽制**するモデルが無い |
| **偽史プロパガンダ**（占領者が自己都合の歴史を植え付けて安定を偽造する） | `Province.integration` は有機的収束のみ。**プロパガンダ注入による人工安定（虚偽安定）→真実暴露で崩壊**の動学が無い |
| **禁書・対抗史観**（「別の歴史がありえた」という禁じられた記録が占領正統性を侵食する） | `DisclosureLedger` に占領勢力を狙い撃ちにする開示カテゴリが無い。**抑圧コスト**（暴露を防ぐ投資）も無い |
| **傀儡政権の脆弱性**（有機的に育った政権と外から設置した政権では腐敗速度・クーデターリスクが違う） | `Regime` に `isInstalled` 区別が無く、傀儡は生え際から正統性が弱いという差分が無い |
| **協力者と住民分断**（占領者に与する協力者が生まれ、住民の連帯が割れる） | `Province.stability` は単一値。**協力者比率が上がると短期産出↑・長期反乱↑**という分断動学が無い |

**結論**：『高い城の男』は当プロジェクトの占領・支配ロジックに**「歴史を偽れる者が支配を維持し、偽史が暴かれると崩れ去る」**という動学——①競合占領 ②偽史プロパガンダ ③禁書・対抗史観 ④傀儡の脆弱性 ⑤協力者分断——という5つの欠落軸を与える。**戦略レイヤーの占領・内政が「情報と記録の真偽が権力を決める」次元を持つ**ようになる。

---

## 1. 役に立つ視点（要約）

作品の構造を**本システムに効く形**で1行ずつ：

1. **歴史は偽れるが真実は漏れる**。占領者は都合のよい歴史を教育・メディアで植え付けるが、真実は禁書・口伝・芸術として流通し、ある閾値で爆発する。→ `GovernanceRules`(偽史安定)×`DisclosureLedger`(暴露で崩壊)×`EventEngine`(プロパガンダ選択肢)。
2. **2勢力の分割占領は「どちらが本当の主権者か」という永続的な正統性危機を生む**。緩衝域の小競り合い、双方からの働きかけ、住民の帰属揺れ。→ 銀河の星系が複数勢力に引き裂かれる銀英伝的構図に直接重なる。
3. **「もし歴史が違っていたら」という可能性の想像が、現存秩序への問いを産む**。禁書は武器より危険。→ `DisclosureLedger` の連鎖開示が既存秩序を揺るがすメカニズムに。
4. **占領者に協力する者は短期は利益を得るが共同体を割る**。協力者が多い地域は産出効率が高い一方、住民抵抗が地下へもぐり、後に大爆発する。→ `GovernanceRules` の産出×反乱の動学に深みを加える。
5. **傀儡政権は外から設置した分だけ、内から腐り始める**。正統性の起源が外圧にある政権は、外圧が弱まると即座に瓦解する。→ `DynastyRules`/`CoupRules` の傀儡係数。
6. **真贋の問い = 制度の真正性**。本物の主権（有機的に育った秩序）と偽物の主権（設置された秩序）の区別が長期的な安定度の分岐点になる。→ `Regime.isInstalled` × 制度化投資（`SuccessionRules.InvestInstitution`）に接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`DynastyRules`/`DisclosureLedger`/`CoupRules` を作り直さない**。MHCL はそれらに**欠落軸を足し接続するだけ**（additive）。

### ★★★ 最優先（真の欠落・作品の signature）

#### MHCL 競合占領と緩衝域（2勢力並立占領）
- **競合占領状態**：同一星系に2勢力の「占領主張」が並立。`StarSystem.ownerData` は単一だが、**占領権 Claim を2件保持**した競合状態として純ロジックで表現。
- 競合下の産出：片方が集める間もう一方が干渉→産出効率低下。緩衝域（どちらの実効支配でもない空間）。
- 競合解消条件：一方の軍事排除 or 交渉による分割 or 一方の撤退（`StrategyRules` 側への接続窓口）。
- 接続：`StrategyRules`（星系占領ロジック）× `GovernanceRules`（産出低下・不安定）× `FactionRelations`（敵対/非敵対）。新 `CompetingOccupationRules` + `OccupationClaim`（pure logic・test-first）。

#### MHCL 偽史プロパガンダと虚偽安定（真実暴露で崩壊）
- **プロパガンダ投資**：占領者が `propagandaLevel`（0..1）を積み、`Province.integration` の収束を人工的に加速するが**「虚偽安定ボーナス」として別計上**（`falsifiedStability`）。
- **暴露崩壊**：`DisclosureLedger` による関連真実の開示、または高い `EspionageRules.InfoGain` によって `falsifiedStability` が一掃され `RebelPressure` が急増（「安定の真の底」が出る）。
- 接続：`GovernanceRules`（Tick に組み込み）× `DisclosureLedger`（暴露トリガー）× `EspionageRules`（プロパガンダ破壊）。新 `PropagandaRules` + `ProvincePropagandaData`（pure logic・test-first）。

### ★★ 高（既存への深み追加）

#### MHCL 禁書・対抗史観と占領侵食
- **占領侵食型開示**：`DisclosureEntry` に `targetOccupierFaction`（FactionData or null）を追加。開示されると `Regime.legitimacy` を比例削減（通常開示の2〜3倍効果）。
- **抑圧コスト**：占領者は「禁書抑圧」投資（`suppressionCost`）をかけて開示確率を下げられるが、`SecurityRules.RepressionSupportPenalty` に連動して支持を削る。
- 接続：`DisclosureLedger.TryReveal` に抑圧チェックを追加 × `EspionageRules.SabotageEffect`（禁書流通/抑圧）。`OccupationDisclosureRules`（pure logic・test-first）。

#### MHCL 傀儡政権の正統性欠乏
- `Regime.isInstalled`（bool）フラグ：外部勢力に設置された政権。
- **加速腐敗**：`DynastyRules.Tick` で `corruption` の進行速度が `PuppetParams.decayMultiplier`（既定1.5）倍。
- **クーデターリスク上昇**：`CoupRules.WouldCoup` の閾値が `PuppetParams.coupBias` だけ低い（設置政権は内側から崩れやすい）。
- **制度化投資で有機化**：`SuccessionRules.InvestInstitution` を十分積むと `isInstalled` の不利が薄れる（天命を自分のものにする）。
- 接続：`DynastyRules`/`Regime` × `CoupRules`。`PuppetRegimeRules` + `PuppetParams`（pure logic・test-first）。

### ★ 中（分断動学・世界観lore）

#### MHCL 協力者と住民分断（協力者比率の二極化動学）
- `Province.collaboratorFraction`（0..1）：占領者に協力する住民の割合。
- **短期産出↑**：協力者が多いほど `GovernanceRules.OutputFactor` に小ボーナス（協力者は占領者のために効率よく働く）。
- **長期反乱↑**：`RebelPressure` に `collaboratorFraction × occupationDuration` の乗数。分断が長引くほど抵抗は爆発的。
- **分断加速条件**：`SecurityRules.DissentSuppression`（密告奨励）でfraction上昇、`NonviolenceRules.Repress`（弾圧可視化）で低下。
- 接続：`GovernanceRules` × `SecurityRules` × `ConsentRules`。`CollaboratorRules`（pure logic・test-first）。

#### MHCL（lore）世界観の開示データ
- コード新設なし。`DisclosureLedger`（FND-4）に開示エントリを追加：
  - 「記録の真偽が権力の出所を決める」
  - 「歴史は偽れるが証拠は残る：第一の亀裂」
  - 「並行する可能性の存在が現在への反証になる瞬間」
- 接続：**コード新設せず**、`SampleDisclosures` 拡張 or `DisclosureLedger` データ入力のみ。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 占卜・確率的意思決定（易経モデル） | `EventEngine` のランダムイベントがカバー。AIの非合理行動は `EventRules.SelectWeighted` で表現可。新規EPIC化はタイクン化 |
| 芸術品の真贋鑑定システム | ゲームプレイへの寄与が薄い。EspionageRules の情報真偽で代替 |
| 占領下の文化保存マイクロ操作 | `Province.nativeIdeology` のドリフトで表現可。マイクロ操作 = タイクン化 |
| 複数エンディング分岐の明示管理 | `DisclosureLedger.onReveal` のコールバックで十分 |
| 戦後秩序の条約設計（全占領統治の体系化） | DIP-2/3（TreatyRules/WarGoalRules）がカバー予定 |

---

## 3. EPIC #MHCL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存占領・内政ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラクターは不使用、**歴史改変・偽史・競合占領という世界観の構造パターンのみ**参考。

> **EPIC = #2334**。GitHub issue 起票済み（#2336〜#2349）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MHCL-1** | #2336 | 競合占領と緩衝域（`OccupationClaim`・2勢力並立・産出低下・競合解消条件） | 新 `CompetingOccupationRules`。`StrategyRules`×`GovernanceRules`×`FactionRelations` |
| **MHCL-2** | #2338 | 偽史プロパガンダと虚偽安定（`propagandaLevel`・人工 integration・暴露崩壊） | 新 `PropagandaRules`。`GovernanceRules.Tick`×`DisclosureLedger`×`EspionageRules` |
| **MHCL-3** | #2340 | 禁書・対抗史観と占領侵食（`DisclosureEntry.targetOccupierFaction`・抑圧コスト） | 新 `OccupationDisclosureRules`。`DisclosureLedger`×`SecurityRules` |
| **MHCL-4** | #2343 | 傀儡政権の正統性欠乏（`Regime.isInstalled`・加速腐敗・クーデターリスク上昇） | 新 `PuppetRegimeRules`。`DynastyRules`×`CoupRules`×`SuccessionRules.InvestInstitution` |
| **MHCL-5** | #2345 | 協力者と住民分断（`Province.collaboratorFraction`・短期産出↑長期反乱↑） | 新 `CollaboratorRules`。`GovernanceRules`×`SecurityRules`×`ConsentRules` |
| **MHCL-6** | #2349 | （lore）世界観の開示データ（記録真偽・並行歴史・虚偽安定の崩壊） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`MHCL-1`（競合占領＝最も独自で構造的な欠落）→ `MHCL-2`（偽史＝競合占領下での支配維持手段）→ `MHCL-4`（傀儡＝偽史と一体の脆弱性）→ `MHCL-3`（禁書＝偽史の反対面）→ `MHCL-5`（協力者分断＝住民側の動学）→ `MHCL-6`（lore）。

> いずれも既存占領/内政/開示ロジックを**後退させず接続**する additive 設計。MHCL-1/2 は `StrategyRules.ResolveOccupation` 周辺への自然な拡張。
