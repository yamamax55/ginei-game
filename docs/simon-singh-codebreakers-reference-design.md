# サイモン・シン『暗号解読』参考設計（EPIC #CRPT）

> 参照元：サイモン・シン『暗号解読』（The Code Book）。古代換字暗号から公開鍵暗号まで暗号技術の歴史を辿るノンフィクション。
> 「知識を持つ者が戦争を制す」——暗号作成者と解読者の終わりなき軍拡競争と、情報こそが歴史の隠れた主役であることを描く。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に諜報純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#CRPT` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**暗号/情報戦のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ『暗号解読』が本システムに役立つか

当プロジェクトは諜報・情報の**攻撃側ロジック**を保有している：

| 既存（攻撃側・抽象） | カバー範囲 |
|---|---|
| `EspionageRules`/`SpyNetwork` | `MissionSuccessChance`/`InfoGain`/`DetectionRisk`/`SabotageEffect`（攻撃一方向） |
| `ResearchRules`/`ResearchProject`+`ResearchField` | 研究投資→生産/技術改善。暗号分野はフィールド未定義 |
| `EventEngine`/`GameEventDef` | 条件発火イベント（暗号漏洩型は未定義） |
| `DisclosureLedger` | 秘史の段階的開示 |
| `DiplomacyRules`/`DiplomacyState` | 外交状態・条約（真正性検証コストは無い） |
| `NotificationCenter` | 情報通知（発信側の暗号化は無い） |

**しかし、これらは「攻撃側が一方的に情報を盗む」モデル**であり、『暗号解読』が固有に描く以下が**欠けている**：

| 『暗号解読』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **通信暗号強度（防御側係数）** | `EspionageRules.InfoGain` は攻撃側一律。**守る側が暗号で InfoGain を抑止する**回路がない |
| **解読能力（クリプタナリシス）** | InfoGain は成否2値。**解読研究が蓄積するほど敵の戦略計画が読める**動態がない |
| **欺瞞作戦（偽情報の植付け）** | `SabotageEffect` は物理破壊。**敵の信頼する通信路に偽情報を注入し行動を歪める**回路がない |
| **暗号漏洩イベント（突発的優位逆転）** | EventEngine に「鍵が破られた瞬間に情報格差が逆転する」型の特殊戦略イベントがない |
| **暗号/解読の研究競争** | `ResearchField` に暗号技術・解読分野が無く、投資競争が表現できない |

**結論**：『暗号解読』は当プロジェクトの諜報ロジックに**①通信防御（守る側の係数）②解読優位（研究→戦略情報化）③欺瞞作戦（偽情報注入）④漏洩イベント（突発逆転）⑤研究競争（暗号vs解読レース）**という5つの欠落軸を与える。EspionageRules を攻防両立させ、「武力なき情報戦」を戦略の第三の軸（武力・経済に並ぶ）として立てる。

---

## 1. 役に立つ視点（要約）

『暗号解読』の世界観を、**本システムに効く形**で1行ずつ：

1. **情報優位が艦隊優位を覆す** — 通信を守る＋敵を読む＝武力を超える戦略資源。`EspionageRules` に防御側係数を追加する根拠。→ 「武力でなく知略で勝つ」王道ナラティブを補強。
2. **全ての暗号はいつか解読される** — 技術的優位は一時的で、研究投資と鍵管理が必要。→ `ResearchField` 拡張＋`ResearchRules` の研究競争に暗号/解読軸を足す。
3. **欺瞞は「信頼された通信路」を逆用する** — 敵が信頼する通信路に偽情報を混ぜるほど効果的（二重スパイ）。→ `DeceptionRules` の設計原理：暗号強度が高い通信路の欺瞞は信頼度が高く危険。
4. **漏洩は突発的に情報格差を逆転させる** — 鍵危殆化の瞬間から全てが変わる。→ EventEngine 型の `CipherCompromise` イベントで「一瞬で状況が変わる」緊張を表現。
5. **解読者は「知っていることを知られてはならない」** — 解読結果を使い続けると相手に気付かれる。→ `DeceptionRules` と `CryptanalysisRules` の相互作用：情報優位を露出させると失う。
6. **知識の民主化と国家安全保障の矛盾** — 暗号技術を公開することで全員が守られるか、独占することで一者が優位に立つか。→ `DisclosureLedger` lore で世界観EPICに接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`EspionageRules`/`ResearchRules`/`EventEngine`/`DisclosureLedger` を作り直さない**。CRPT はそれらに**欠落軸を足し、接続するだけ**（additive）。

### ★★★ 最優先（真の欠落・本書の signature）

#### CRPT 通信暗号強度 `CommunicationSecurityRules` / `CipherStrength`

- **`CipherStrength`**（int 0–100）：勢力が保有する通信暗号の強度。高いほど敵の InfoGain が下がる。
- **`CommunicationSecurityRules.EffectiveInfoGain(attacker, defender)`**：攻撃側 InfoGain × `(1 - DefenseReduction(defender.CipherStrength))`。純関数・test-first。
- **`CipherFactor(strength)`**：`strength/100 × maxReduction`（既定 `maxReduction=0.7`、完全解読不可・0まで落とさない）。
- 接続：`EspionageRules.InfoGain` の呼び出し側へ `EffectiveInfoGain` を差し込む（EspionageRules 本体を書き換えず additive）。

#### CRPT 解読能力 `CryptanalysisRules`

- **`CryptanalysisLevel`**（int 0–100）：勢力の解読能力。研究で上昇、相手の CipherStrength で抑制。
- **`DecryptionMultiplier(cryptanalysisLevel, defenderCipherStrength)`**：解読成功率＝解読能力が暗号強度を上回るほど高い。純関数・test-first。
- **`CanRevealStrategy(attacker, defender)`**（bool）：閾値超過で敵の戦略移動計画が「読める」（InfoGain のカテゴリが「戦術情報」→「戦略計画」に昇格）。
- 接続：`EspionageRules` × `ResearchRules`（解読 `ResearchField` の `ResearchOutput` が `CryptanalysisLevel` に積算）。

### ★★ 高（情報戦の動学に奥行きを足す）

#### CRPT 欺瞞作戦 `DeceptionRules` / `DisinformationOp`

- **`DisinformationOp`**（struct）：`targetFaction`/`credibility`（0–1 ＝ 虚偽情報の信頼度）/`durationTurns`/`intentEffect`（敵AIへの誤行動バイアス：目標地点の嘘など）。
- **`DeceptionRules.SuccessChance(spyNetwork, targetCipherStrength)`**：欺瞞成功率。ターゲットの暗号強度が低いほど偽情報が「公式通信」として受け入れられやすい。純関数。
- **`Credibility(spyReputation, channelSecurity)`**：偽情報がバレる確率を下げる。安全と信頼された通信路は欺瞞の武器になる（逆用パターン）。
- 接続：`EspionageRules.SabotageEffect` の情報版として並列追加。`NotificationCenter` への偽通知注入はコード新設せずデータで制御。

#### CRPT 暗号漏洩イベント `CipherCompromise`（EventEngine 型）

- **`GameEventDef` サンプル**：条件「`attacker.CryptanalysisLevel - defender.CipherStrength > threshold`」→ 暗号漏洩イベント発火。
- 効果：`CipherStrength` を一時的に 0 近傍まで落とす（鍵更新まで通信筒抜け）。`durationTurns` 後に自然回復。
- **突発逆転の緊張**：平時は無敵と思われた暗号が一瞬で崩れ、情報格差が逆転する演出。
- 接続：`EventEngine.Register`（`SampleEvents` に追加）。`NotificationCenter.Push` で通知。コード新設最小。

### ★ 中（研究競争・世界観 lore）

#### CRPT `ResearchField` 拡張（暗号技術/解読）

- `ResearchField` enum に `暗号技術`（`CipherStrength` を上昇）と `解読`（`CryptanalysisLevel` を上昇）を追加。
- `ResearchRules.IdeologyBias`：民主制は解読（情報の自由）を優先、権威制は暗号技術（通信管理）を優先するバイアスを追加。
- 接続：`ResearchRules.Tick` の `ResearchOutput` が `CommunicationSecurityRules`/`CryptanalysisRules` へ積算。

#### CRPT（lore）世界観の開示データ

- **コード新設なし**。`DisclosureLedger`（FND-4）への lore データ入力のみ。
- 「情報こそ最大の武器——艦隊の数を超えて」「暗号は権力の非対称を崩す——解読者が帝国を倒す」「知識の民主化と国家安全保障のトレードオフ」。
- 接続：CCX-6（世界観 codex 退避）方針に一貫。世界観 EPIC（秘史/啓蒙/ニーチェ）と連鎖。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 通信路の物理的建設・暗号機の製造 | **SCM#982/造船#884** が類似のインフラ生産をカバー。暗号機を別モジュール化するとタイクン化 |
| 量子暗号・格子暗号などの技術詳細 | ゲームの抽象度に合わない。`ResearchField` の係数として内包すれば十分 |
| プレイヤーが実際に暗号を解くミニゲーム | アクション・パズル系の逸脱。Ginei は戦略決断→創発帰結の方針（タイクン化回避） |
| 条約の電子署名・真正性検証 | `DiplomacyRules` の条約層（DIP-2 #191）に「信頼度修飾子」として薄く乗せれば十分（独立 EPIC 不要） |
| 情報の非対称と風説の相場（市場） | **SAW-3 がカバー済み**（市場価格の情報優位）。CRPT は軍事/戦略情報に特化 |

---

## 3. EPIC #CRPT の子 Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存諜報ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1900**。GitHub issue 起票済み（#1902〜#1914）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CRPT-1** | #1902 | 通信暗号強度 `CipherStrength`・`CommunicationSecurityRules`（防御側 InfoGain 抑止） | 新純ロジック。`EspionageRules.EffectiveInfoGain` の差し込み口 |
| **CRPT-2** | #1903 | 解読能力 `CryptanalysisRules`（解読研究蓄積→敵通信の戦略情報化） | CRPT-1 × `ResearchRules`。`CanRevealStrategy` が戦略計画 InfoGain を解放 |
| **CRPT-3** | #1907 | 欺瞞作戦 `DeceptionRules`（偽情報注入→敵行動バイアス・暗号逆用パターン） | CRPT-1×`EspionageRules`。`DisinformationOp` struct。純ロジック test-first |
| **CRPT-4** | #1909 | 暗号漏洩イベント `CipherCompromise`（解読超過→鍵危殆化→情報格差逆転） | `EventEngine`/`SampleEvents` に追加。CRPT-1/2 の閾値判定を活用 |
| **CRPT-5** | #1911 | `ResearchField` 拡張（暗号技術/解読フィールド追加＋政体バイアス） | `ResearchRules` enum 拡張。CRPT-1/2 の `CipherStrength`/`CryptanalysisLevel` に積算 |
| **CRPT-6** | #1914 | （lore）世界観の開示データ（情報こそ最大の武器・解読者が帝国を倒す・知識の民主化の矛盾） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`CRPT-1 → CRPT-2`（通信防御と解読能力＝本書の signature・防攻両立の基盤）→ `CRPT-3`（欺瞞作戦＝逆用パターンで最も戦略的に面白い）→ `CRPT-4`（漏洩イベント＝緊張感の演出・EventEngine 接続）→ `CRPT-5`（研究競争＝投資動機の完成）→ `CRPT-6`（lore）。

> いずれも既存 EspionageRules/ResearchRules/EventEngine を**後退させず接続**する additive 設計。「武力・経済・情報」の三軸戦略を完成させる。
