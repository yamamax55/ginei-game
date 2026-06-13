# 軍政（文民統制）の改善 — 米軍／米国 civil-military 構造を参考に（設計提案 #MILGOV-US）

> 既存の軍政関係（`CivilianControlRules` GOV-4 #145 ほか）を、**実在の米軍／米国の文民統制構造**と照合し、役に立つメカニクスだけを抽出して改善提案する設計ドキュメント。
> 参照する実制度：国家安全保障法（1947）・**ゴールドウォーター゠ニコルズ法（1986）**・統合参謀本部（JCS/CJCS）・統合軍（Combatant Commands）・国防長官（SecDef）と各軍長官（Service Secretaries）・上院の助言と承認（advice and consent）・DOPMA（将校人事法）・ポッセ・コミタタス法・ハンティントン／ヤノヴィッツの文民統制理論。
> **著作権注意**：条文・固有の歴史記述は流用せず、**統制メカニクスの構造パターンのみ**を参考にする。固有名（CENTCOM 等）は型名に使わず日本語の一般名で抽象化する。
> 状態：**提案のみ（未実装）**。実装は Core 純ロジック（test-first）＋暦境界 Tick 配線を想定。数値・創作裁定は【要・作者判断】。
> 関連：軍編制 [military-organization-design.md]（ORBAT＝部隊の階層）・[fleet-organization-design.md]・[local-autonomy-design.md]。本書は「**誰が軍を握るか**＝civil-military」を担当し、ORBAT は「**部隊の階層構造**」を担当する（直交）。

---

## 0. なぜ米軍構造が本システムに役立つか

このゲームは銀英伝風＝**門閥貴族の帝国（君主統帥）／民主共和制の同盟（文民統制）**の対比が主題で、軍政（クーデター・統帥権・軍の独走）は物語の核。米国の文民統制は **「世界最強の軍が一度もクーデターを起こさない」設計** であり、その構造的な仕掛けは本ゲームの `CivilianControlType` を**スカラー値の黒箱から、説明可能な構造モデル**へ引き上げるのに最適。

既に本プロジェクトが持っているもの：

| 既存（カバー範囲） | 該当モジュール | 状態 |
|---|---|---|
| 文民統制の型（5アーキタイプ） | `CivilianControlType{文民統制,君主統帥,党軍,軍部優位,未分化}` | 型あり |
| 軍人の政治兼任可否・軍人事の所在・クーデターリスク | `CivilianControlRules` | 純ロジックのみ・**未配線** |
| 政府役職・任命台帳 | `Office`/`OfficeRules`/`GovernmentRegistry`/`ICharacter` | 純ロジックのみ・**未配線** |
| 省庁ツリー | `Ministry`/`MinistryRules` | 純ロジックのみ・**未配線** |
| 政党・最小選挙・総裁選 | `Party`/`PartyRules`/`LeadershipElectionRules` | 純ロジックのみ・**未配線** |
| クーデター発火と帰結 | `CoupRules{軍部,宮廷,革命}×{成功,粛清,内戦}` | 純ロジックのみ・**未配線** |
| 秘密警察・寡頭・立憲・三権分立 | `SecurityRules`/`PowerRules`/`ConstitutionRules`/`SeparationOfPowersRules` | 純ロジックのみ・**未配線** |
| 軍の編制ツリー（指揮系統） | `OrderOfBattle`/`MilitaryFormation`（#147/ORBAT） | **配線済（会戦・戦略）** |
| 予算の分野配分 | `NationalBudget`/`BudgetRules{軍事,建艦,…}` | 配線済（日次 Tick） |
| 将校の停年・アップオアアウト・戦時召集 | `RetirementRules{現役,予備役,退役}` | 純ロジック |

**問題は二つ**：(A) 軍政モジュール群が**ほぼ全部 `GalaxyView` 未配線＝凍結状態**（型は完備だがゲームループが呼ばない）、(B) **`CivilianControlRules.CoupRisk` の `controlStrength` が単一スカラーで、なぜ統制が効くのかが表現されていない**。米軍構造はこの両方に効く。

---

## 1. 米国 civil-military 構造のうち「役に立つ視点」

米国が（理論上も実務上も）クーデターを構造的に封じている仕掛けを、ゲーム化価値の高い順に抽出する。

### 1-1. 指揮の二系統分離（ゴールドウォーター゠ニコルズ法）★最重要
米軍の指揮系統は **二本に割れている**：

- **作戦指揮系統（operational chain）**：大統領 → 国防長官 → **統合軍司令官（CCDR）**。実際に部隊を動かして戦争を遂行する。
- **管理系統（administrative chain＝organize, train, equip）**：各軍長官 → **各軍参謀総長** → 部隊。人を集め・訓練し・装備するが、**戦争では指揮しない**。

要点＝**戦力を「育てる者」と「使う者」を別人にする**。陸海空軍の制服トップ（参謀総長）は強大な人事・予算・訓練の権限を持つが、**作戦指揮権を持たない**。逆に統合軍司令官は強大な作戦権を持つが、軍政（人事・予算）を握らない。**一人の将軍に「兵・銭・命令」が集中しない**＝クーデターの物理的母体が分断されている。

### 1-2. 助言権限と指揮権限の分離（統合参謀本部議長 CJCS）★重要
制服組のトップである**統合参謀本部議長（CJCS）は、最高位の軍事顧問だが指揮権を持たない**。大統領・国防長官への助言が職務で、命令は彼を**経由しない**（作戦命令は文民の SecDef から CCDR へ直接）。意図的に「最高位の軍人を武装解除」してある＝**“馬上の人（man on horseback）”を制度で作らせない**。

### 1-3. 文民長官の制度化と「文民要件」
- **国防長官（SecDef）・各軍長官は文民**。SecDef は退役後7年未満の軍人は就任不可（特例には立法府の免除が要る）。
- 軍の頂点に**必ず文民の官職が一段乗る**。制服組の指揮系統は文民官職の**下**で閉じる。

### 1-4. 立法府の歯止め（advice and consent / power of the purse）
- 将官（O-7 以上）への昇進と上級文民職の任命は**上院の承認**を要する（advice and consent）。
- 軍の予算は**議会が握る（appropriations）**。行政府＝大統領は指揮するが、金は立法府が握る。
- 宣戦は議会、指揮は大統領＝**戦争権限の分割**。

### 1-5. 専業規範と憲法への宣誓（ハンティントン objective control）
- 将校は**個人や政権ではなく憲法に宣誓**する。
- 「政治には関与しない代わりに、軍事専門領域の自律を認められる」**プロフェッショナリズム規範**が文民優位を内面化させる（＝法でなく規範による統制）。

### 1-6. 軍と国内治安の分離（ポッセ・コミタタス法）
- 連邦軍は**国内の法執行（警察活動）に従事できない**。
- 国内治安は別組織（州兵・連邦法執行機関）。**「軍が国民に銃を向ける」回路が法で塞がれている**。本ゲームの `軍部優位`／秘密警察モデルはここが曖昧。

### 1-7. 予備役の二重統制（連邦 vs 州 / 動員権限）
- 州兵は平時は州知事、連邦化（動員）されると大統領の指揮下。**召集権限が誰にあるか**が統制点。本ゲームの `RetirementRules.CanRecall`（戦時召集）に「召集権者」の軸を足せる。

---

## 2. ギャップ分析（現状 ↔ 米軍構造）

| 観点 | 現状の本システム | 米軍構造 | ギャップ／改善余地 |
|---|---|---|---|
| 指揮系統 | `OrderOfBattle`（作戦＝梯団ツリー）と `Office`/`Ministry`（管理＝役職/省庁）が**別物として併存するが連結されていない** | 作戦系統と管理系統を**明示的に分離**し、両方を文民の下に置く | **二系統を概念として連結**し「兵を育てる者≠使う者」を表現（§3-A） |
| 制服トップ | `軍部優位` は軍を一枚岩の権力主体として扱う | CJCS＝**助言権限のみ・指揮権なし**で武装解除 | 役職に**「助言権 vs 指揮権」属性**が無い（§3-B） |
| 文民の長 | `CivilianControlRules.CiviliansAppointMilitary` は真偽だけ。文民長官という**官職**が無い | 軍の頂点に**文民官職（SecDef/長官）が制度として乗る** | `OfficeDomain.軍事` に**文民長官ポスト**＋文民要件（§3-C） |
| 任命の歯止め | `politicalAppointmentOnly` フラグはあるが**承認ゲートが無い** | 将官昇進・上級職に**立法府の承認** | `Party`/議会と昇進を結ぶ**承認ゲート**（§3-D） |
| クーデターリスク | `CoupRisk(t, controlStrength, …)` ＝**統制強度が単一スカラー**（なぜ効くか不明） | 統制は**構造の合成**（指揮分断・専業規範・財政依存・宣誓正統性） | **`controlStrength` を構造分解**＝説明可能化（§3-E）★ |
| 軍と治安 | 軍と内治の分離が無い（`軍部優位`/秘密警察が混線） | ポッセ・コミタタス＝**軍は国内法執行不可** | 軍の**国内治安投入ゲート**（§3-F） |
| 配線 | 軍政モジュール群が**全部 `GalaxyView` 未配線** | — | **暦境界 Tick へ配線**して凍結解除（§4）★ |

---

## 3. 改善提案（Core 純ロジック・test-first・実効値パターン・後方互換）

> いずれも **既存の `CivilianControlRules`／`OrderOfBattle`／`Office` を壊さず additive**。スカラビリティ規律（個体粒度に降りない・暦境界 Tick・差分/集約）を順守。`CivilianControlType` 未設定なら従来動作。

### 3-A. 指揮の二系統分離（ゴールドウォーター゠ニコルズ）★最優先 ✅実装済（Core 純ロジック・`CommandChainRules`）
> **状態：実装済**＝`Assets/Scripts/Core/CommandChainRules.cs`＋`Office.commandChain`＋EditMode テスト（`CommandChainRulesTests`）。盤面/UI 配線（§4 ★1）は別途。下記 API は実装と一致。

**狙い**：作戦指揮（戦力を使う）と軍政管理（戦力を育てる）を分け、**一人に集中させない**。クーデター母体の有無をデータで表す。

既存資産の連結で実装できる（新レジストリ不要）：
- 作戦系統＝`OrderOfBattle`（梯団ツリーの司令）。
- 管理系統＝`Office`（`OfficeDomain.軍事` の役職＝参謀総長・軍政ポスト）＋`Ministry`（軍政省庁）。

新規 Core：`CommandChainRules`（static・唯一の窓口）。識別子は `int`（作戦頂点の司令＝`AdmiralData` と管理頂点の役職保持者＝`ICharacter` で型が異なるため**型非依存**。配線層が id を解決する）。
```csharp
public enum CommandChain { 作戦, 管理 }   // 作戦=部隊を動かす / 管理=organize-train-equip

public static class CommandChainRules
{
    public const int Vacant = int.MinValue;             // 頂点空席の番兵
    // 集中度の重み（作戦0.45+管理0.35+予算0.20=1.0）／分断度の配点（両頂点別人0.6+予算独立0.4）

    // 役職がどちらの系統か（軍事所掌は Office.commandChain、非軍事は管理）
    public static CommandChain ChainOf(Office o);

    // 構造の頂点を既存資産から抽出
    public static MilitaryFormation OperationalApexFormation(IEnumerable<MilitaryFormation> formations);
    public static MilitaryFormation OperationalApexFormation(Faction faction);   // OrderOfBattle 版
    public static Office AdministrativeApexOffice(IEnumerable<Office> offices);  // 国家・軍事・最高tier

    // 一人が握る権限の束→指揮集中度 0..1（作戦頂点+軍政頂点+予算権）
    public static float Concentration(bool operationalApex, bool administrativeApex, bool budgetAuthority);
    public static bool ConcentratesCommand(bool operationalApex, bool administrativeApex); // 両頂点兼任=GN違反

    // ゴールドウォーター゠ニコルズ準拠か（両頂点が埋まり別人か）
    public static bool IsUnifiedCommandSeparated(int operationalApexHolderId, int administrativeApexHolderId);

    // 指揮分断度 0..1 ＝§3-E の CoupRisk 駆動因（両頂点別人+予算独立=power of the purse）
    public static float CommandSeparation(int operationalApexHolderId, int administrativeApexHolderId, int budgetHolderId);
}
```
**ゲーム的帰結**：帝国（門閥）は一人の元帥に作戦＋軍政＋財政が集中しやすく `CommandConcentration` 高＝クーデター母体あり。同盟（民主）は二系統が割れ低い。プレイヤーが大将に権限を集めるほど効率は上がるが**独走リスクが上がる**トレードオフ。

### 3-B. 助言権限 vs 指揮権限（統合参謀本部議長モデル）★
`Office` に属性追加（additive・既定で従来動作）：
```csharp
public class Office {
    // 既存 …
    public bool advisoryOnly;   // 助言権のみ＝指揮権を持たない（CJCS型）。既定 false。
}
```
- `OfficeRules.CanCommand(...)`：`advisoryOnly` の制服トップは**梯団司令に就けない**（`OrderOfBattle.AssignCommander` のゲートに合流）。
- **効果**：最高位の軍人を「顧問」に置くと、能力ボーナス（助言＝`CommandStaffRules` の参謀補完に合流）は得るが**指揮権を持たない**＝クーデター駆動因 `commandUnity` を下げる。文民統制の制度的肝を、プレイヤーが選べるレバーにする。

### 3-C. 文民長官の制度化＋文民要件
`OfficeDomain.軍事` に**文民が就く頂点ポスト**（国防長官相当）を定義可能にする。`CivilianControlRules` に補助：
```csharp
// 軍の頂点に文民官職が乗っているか（制服系統がその下で閉じるか）
public static bool HasCivilianApex(CivilianControlType t, IEnumerable<Office> militaryOffices);

// 文民要件：退役後N年未満の軍人を文民長官に就けるか（既定: 文民統制は不可＝免除が要る）
public static bool MeetsCivilianRequirement(CivilianControlType t, ICharacter c, int yearsSinceService, int cooloffYears = 7);
```
- `文民統制`＝軍事頂点に文民ポスト必須・元軍人は冷却期間ゲート。`党軍`＝党の政治将校が監督（既存コメントの #17 に接続）。`君主統帥`＝君主が統帥権（文民でなく君主が頂点）。`軍部優位`＝頂点も軍人（＝`MilitaryMayHoldPoliticalOffice` true と整合）。

### 3-D. 立法府の承認ゲート（advice and consent）
`PartyRules`／議会（`Party.support` で多数派）と昇進を結ぶ：
```csharp
public static class ConfirmationRules
{
    // 将官（tier≥閾値）の昇進・上級職任命に立法府の承認が要るか（政体依存）
    public static bool RequiresConfirmation(CivilianControlType t, Office o, int seniorTierThreshold);

    // 承認可決度 0..1（与党の支持・候補の能力/正統性・対立度から）。閾値未満は否決＝任命保留
    public static float ConfirmationOdds(...);
}
```
- **効果**：民主政（同盟）では将官人事が議会に縛られ、政争で**有能な提督が承認されず塩漬け**になりうる（銀英伝の同盟の宿痾＝政治が軍を縛る）。専制（帝国）は承認不要で即任命＝速いが暴走の歯止めも無い。`power of the purse` は既存 `BudgetRules` の軍事シェアで既に表現できる（議会が予算を絞る＝`MilitaryReadinessFactor` 低下）。

### 3-E. クーデターリスクの構造分解 ★（最も波及効果が大きい）
現状 `CoupRisk(t, controlStrength, support, recentDefeat)` の `controlStrength`（単一スカラー）を、**米国がクーデターを封じている4つの構造因の合成**に置き換える（後方互換のオーバーロードで追加）：

```csharp
public readonly struct ControlStructure   // 統制の構造（0..1 各因子）
{
    public readonly float commandSeparation; // 指揮二系統の分断度（§3-A・高いほど母体なし）
    public readonly float professionalNorm;  // 専業規範＝政治不関与の内面化（ハンティントン）
    public readonly float fiscalDependence;  // 財政が文民/議会に握られている度（§3-D・power of the purse）
    public readonly float constitutionalOath;// 憲法/正統性への宣誓＝個人でなく制度への忠誠

    public float Strength { get; } // 4因子の合成＝従来の controlStrength 相当（既定の重み付き平均）
}

public static float CoupRisk(CivilianControlType t, ControlStructure s, float support, bool recentDefeat);
```
- 既存 `CoupRisk(t, float controlStrength, …)` は**残す**（後方互換）。新オーバーロードは `ControlStructure.Strength` を渡すだけで従来式に合流＝**式の二重実装をしない**。
- **これで「なぜ文民統制は coup 0.10 なのか」が説明される**：指揮が割れ・規範が根付き・金を議会が握り・憲法に宣誓しているから。プレイヤーがどれか一つを崩す（例：戦時に一人の元帥へ権限集中＝`commandSeparation`↓）と、同じ政体でもリスクが跳ねる＝**創発的な civil-military ドラマ**。
- 帝国の門閥／クーデター（ラインハルト以前のリップシュタット戦役・救国軍事会議）も、この4因子で表現できる。

### 3-F. 軍と国内治安の分離（ポッセ・コミタタス）
`CivilianControlRules` に補助：
```csharp
// 軍を国内治安（鎮圧・法執行）に投入できるか（政体依存）
//   文民統制＝原則不可（投入には正統性ペナルティ＝戒厳の重み）
//   党軍/軍部優位/未分化＝可
public static bool MilitaryMayPoliceDomestic(CivilianControlType t);

// 軍を鎮圧投入したときの支持/正統性コスト（分離規範を破る重み）
public static float DomesticDeploymentPenalty(CivilianControlType t, float repression);
```
- 既存 `LawEnforcementRules`（#2126 治安）・`SecurityApparatus`（秘密警察）と接続。**「軍を国民に向ける」と短期に鎮圧できるが正統性が崩れ `CoupRisk` の `professionalNorm` を毀損**＝末人化（#852）/易姓革命（#867）への回路。

---

## 4. 配線（凍結解除）— 最優先の実務

§3 の数値以前に、**既存軍政モジュールが `GalaxyView` から一切呼ばれていない**のが最大の問題。まず薄く配線して「見えて・効く」状態にする（スカラビリティ規律＝**暦境界 Tick**・集約・差分）。

1. **`CampaignState`/`FactionState` に統制状態を持たせる**：`CivilianControlType`（政体から既定）＋`ControlStructure`（4因子）。デモ＝帝国=君主統帥／同盟=文民統制／（将来）フェザーン=未分化。
2. **年次 Tick（`RunAnnualLifecycleTick` 近傍）で `CoupRisk` を評価**し、閾値超で `CoupRules.Resolve`（決定論 roll）→`NotificationCenter`（政治カテゴリ）へ通知。成功＝政体/正統性が `PostCoupLegitimacy` で書き換わる。N²を避け**勢力ごと1回／年**。
3. **観測層に出す**：`CoreStateInspector`（J）の glossary に統制4因子・coup risk を追記（規約どおり）。可能なら `CampaignObserverOverlay`（G）に「軍政」行を足し、**統制構造とクーデターリスクをライブ表示**＝「Core は増えるが見えない」乖離を作らない。
4. **`GovernmentRegistry` を `StrategySession` 永続化の射程に入れる**（現状 static で往復消失の懸念）。少なくとも軍事頂点ポストの保持者は `CampaignSaveData` へ。
5. **第2層（操作化）**：プレイヤーが文民統制レバー（権限集中⇄分散・文民長官の任免・軍の国内投入）を回せる UI を `StrategyMapWindow` の「軍事/人事」メニューへ。自動化はせず手で昇格（観測層の規約どおり）。

---

## 5. 非目標（やらないこと＝タイクン化／ラグ回避）

- **個々の将校の宣誓・査問の逐次シミュ**は持たない（集約＝`ControlStructure` の4スカラー）。
- 米国固有名（COCOM 名・法令番号）を型/データに焼き込まない（一般名で抽象化）。
- 中分類以下の官僚機構の網羅展開はしない（`Ministry` ツリーは粗いまま）。
- 毎フレーム評価しない（年次 Tick・勢力ごと1回）。
- 銀英伝の世界観に無い「選挙の細密シミュ」は `LeadershipElectionRules` の既存粒度で足りる。

---

## 6. 優先順位と EPIC/issue 化の素案

| 優先 | 提案 | 規模 | 価値 |
|---|---|---|---|
| ★1 | §4 配線（凍結解除＝CoupRisk を年次 Tick＋観測表示） | 小 | 既存資産が初めて「効く」。最大ROI |
| ★2 | §3-E クーデターリスクの構造分解（`ControlStructure`） | 小〜中 | 黒箱→説明可能。creative ドラマの源泉 |
| ★3 | §3-A 指揮の二系統分離（`CommandChainRules`） | 中 | 権限集中⇄独走のトレードオフ。最も「米軍的」 |
| 4 | §3-B 助言権 vs 指揮権（`advisoryOnly`） | 小 | §3-A/3-E の駆動因を player レバー化 |
| 5 | §3-F 軍と治安の分離（ポッセ・コミタタス） | 小 | 鎮圧↔正統性のトレードオフ・末人/革命へ接続 |
| 6 | §3-C 文民長官＋文民要件 | 小 | 制度の肝。役職データの拡張のみ |
| 7 | §3-D 立法府の承認ゲート | 中 | 同盟の「政治が軍を縛る」を表現 |

**推奨着手順＝★1→★2→★3**。まず凍結を解き、次に coup を説明可能にし、その駆動因として二系統分離を入れる。いずれも EditMode テスト併記・`TestHarness` で回帰。

---

## 付録：用語対応（実制度→ゲーム抽象）

| 米制度 | ゲーム抽象（型/概念） |
|---|---|
| 大統領→国防長官→統合軍司令官（作戦系統） | `OrderOfBattle` の司令系統＋頂点に文民官職 |
| 各軍長官→参謀総長→部隊（管理系統） | `Office`(`OfficeDomain.軍事`)＋`Ministry`（軍政省庁） |
| 統合参謀本部議長（助言・無指揮） | `Office.advisoryOnly`＋`CommandStaffRules` 補完 |
| 上院の助言と承認 | `ConfirmationRules`＋`Party.support`（議会多数） |
| 議会の予算権（power of the purse） | `BudgetRules` 軍事シェア（既存） |
| 専業規範・憲法宣誓 | `ControlStructure.professionalNorm/constitutionalOath` |
| ポッセ・コミタタス法 | `MilitaryMayPoliceDomestic`＋`DomesticDeploymentPenalty` |
| DOPMA（up-or-out） | `RetirementRules.ShouldUpOrOut`（既存） |
| 州兵 Title 32/10（動員権限） | `RetirementRules.CanRecall`＋召集権者の軸（将来） |
