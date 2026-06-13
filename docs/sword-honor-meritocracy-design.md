# 恩賜の軍刀組と学閥主義↔実力主義（史実ベース・設計＋実装 #MILEDU-SWORD）

> 史実（旧日本陸軍）の **恩賜の軍刀組** を取り込み、**大学校卒（星）を優遇し、その成績上位5名（恩賜の軍刀組）を最優遇**する学閥credential を実装する。あわせて **米軍との対比＝実力主義（meritocracy）も選べる**ようにし、credential 偏重か実力偏重かを**選択レバー（昇進ドクトリン）**にする。
> 史実の背景：陸軍大学校（陸大）の卒業者は参謀エリートの徽章（天保銭＝本書の「星」）を帯び、成績上位者は天皇から**恩賜の軍刀**を下賜された（「軍刀組」）。軍刀組は将官人事を事実上独占し、**学歴・席次が実務能力より重んじられる学閥主義**の象徴となった。本書はその構造（credential が昇進を支配する）と、その対極の米軍型実力主義（merit が支配・credential は小さなボーナス）を選べる形で再現する。
> 著作権注意：史実の構造パターンのみ参照（固有名・人物・記述は流用しない）。
> 状態：**Core 純ロジック実装済**（`MilitarySwordHonorRules`＋EditMode テスト）。盤面/UI・人事配線は §5（★）。数値・創作裁定は【要・作者判断】。
> 既存との接続：**`MilitaryAcademyRules`**（大学校卒の輩出＝Pass大学校・`WarCollegeTierBonus`＝任官時の星の優遇）／**`SeniorityRules`**（席次↔実力の rigidity 混合＝重複実装しない・本書は credential 軸を足す）／#14 階級・#155 士官教育・#79 戦功昇進・#543 叩き上げ・#106 係数。

---

## 0. 位置づけ — 既存の「席次 vs 実力」に credential 軸を足す

本プロジェクトは既に **`SeniorityRules`**（LIFE-5/6）で「席次（ハンモックナンバー）が初期序列を決めるが、実務 merit が席次を追い越せる。追い越しやすさ＝rigidity は政体で変わる」を持つ。
**恩賜の軍刀組**はこの上に乗る**別の軸＝学歴credential**：
- **星**＝大学校卒（エリート参謀の徽章）。
- **恩賜の軍刀**＝大学校卒の成績上位5名（最優遇）。
- **「星あり・軍刀あり」がもっとも優遇**＝昇進で恩賜＞星＞なし（隊付＝一般将校）。

そして **学閥主義↔実力主義** の選択で、この credential が昇進をどれだけ支配するかを切り替える（史実の旧軍 vs 米軍の対比）。

---

## 1. 実装（Core 純ロジック・後方互換・実効値パターン）

### 1-1. 列挙
```csharp
public enum MilitaryHonor { なし, 星, 恩賜の軍刀 };        // なし / 大学校卒 / 大学校卒TOP5
public enum PromotionDoctrine { 学閥主義, 実力主義 };       // credential 支配 / merit 支配（米軍対比）
```

### 1-2. ルール `MilitarySwordHonorRules`（唯一の窓口）
```csharp
public const int SwordQuota = 5;   // 恩賜の軍刀組＝大学校卒の成績上位この人数

public static bool IsWarCollegeGraduate(MilitaryDegree degree);          // 星（大学校卒）か
public static MilitaryHonor HonorOf(MilitaryDegree degree, int warCollegeRank); // 大学校卒×席次→なし/星/恩賜
public static bool IsSwordGroup(MilitaryDegree degree, int warCollegeRank);

public static float CredentialScore(MilitaryHonor honor);                // 恩賜1.0 > 星0.6 > なし0.3
public static float CredentialWeight(PromotionDoctrine doctrine);        // 学閥0.85 / 実力0.25

// 昇進優遇＝credential と merit を doctrine の重みで混ぜる（大きいほど優遇）
public static float PromotionFavor(MilitaryHonor honor, float merit, PromotionDoctrine doctrine);
public static float PromotionFavor(MilitaryDegree degree, int warCollegeRank, float merit, PromotionDoctrine doctrine);
```

**公式**：`PromotionFavor = Lerp(merit, CredentialScore(honor), CredentialWeight(doctrine))`。
- **学閥主義（重み0.85）**＝credential が支配：**低 merit の恩賜組が高 merit の隊付を上回る**（史実の軍刀組の人事独占）。
- **実力主義（重み0.25・米軍対比）**＝merit が支配：**恩賜でなくとも俊英が追い越せる**。
- `warCollegeRank` は大学校卒コホート内の席次（1=首席）。`Person` のスキーマは変えず派生関数で判定する（配線層が大学校卒を席次順に並べて与える）。

### 1-3. ドラマ（テストで固定）
- 同じ2人（低 merit の恩賜 / 高 merit の隊付）でも、**doctrine を反転すると勝敗が逆転**する：学閥主義＝恩賜が勝つ（門閥）／実力主義＝隊付が勝つ（米軍）。これがプレイヤーの選択する軍制改革のレバー（銀英伝：ゴールデンバウム門閥 vs ラインハルト/同盟の実力主義 #169）。

---

## 2. 既存システムとの接続（重複実装しない）

| 接続先 | 関係 |
|---|---|
| `MilitaryAcademyRules`（#155） | 大学校卒（星）の輩出元。`WarCollegeTierBonus` は**任官時**の星の優遇、本書は**その後の昇進**の優遇（恩賜＞星＞なし） |
| `SeniorityRules`（LIFE-5/6） | 席次↔実力の rigidity 混合（別軸）。doctrine の `CredentialWeight` は credential 軸の rigidity に相当（`PoliticalRigidity` と相補） |
| 政体/門地（#169/#168/#110） | 学閥主義＝門地閉鎖の門閥（帝国旧軍）／実力主義＝門地開放（改革・米軍・同盟）。doctrine の既定を政体から引く想定 |
| #79 戦功昇進・#543 叩き上げ・#106 | merit の出所（戦功）と昇進係数。叩き上げ（非大学校卒）が実力主義でのみ伸びる |

---

## 3. 非目標（タイクン化回避）

- 個々の将校の細密な人事はしない（credential＝enum＋スコア、doctrine＝1レバー）。
- 恩賜組内の席次は群としてのみ扱う（TOP5を組として優遇＝史実準拠・1位〜5位の微差は持たない）。
- 昇進の最終決定アルゴリズムを新設しない（`PromotionFavor` は人事のランキング入力＝既存 #79/人事窓口へ供給）。

---

## 4. 選択（米軍対比）

`PromotionDoctrine` が選択レバー：
- **学閥主義**＝旧軍/門閥（帝国ゴールデンバウム）。恩賜の軍刀組が人事を独占し、有能でも非エリートは伸びない＝硬直と派閥（だが結束・予測可能性はある）。
- **実力主義**＝米軍/改革（ラインハルト改革・自由惑星同盟）。戦功 merit が支配し叩き上げ（#543）が登用される＝流動的だが学閥の安定は失う。

既定は政体（#117/#145）から引く想定（門地閉鎖→学閥主義／開放→実力主義）。プレイヤーが軍制改革で切り替えられる。

---

## 5. 配線（後続・★）

1. `MilitaryAcademyRules` が大学校卒を出すとき、コホート内の席次（warCollegeRank）を確定し、上位 `SwordQuota` 名を恩賜の軍刀組として記録（`Person` に `warCollegeRank` 等の任意フィールド追加 or 派生計算）。
2. 勢力/政体ごとに `PromotionDoctrine` を持たせ（既定は政体から）、人事（#79 戦功昇進・将官登用）の候補ランキングに `PromotionFavor` を使う。
3. HUD/人物名鑑に **星・恩賜の軍刀**のアイコンを表示（「星あり・軍刀あり」を一目で）。観測層（`MilitaryObserverOverlay` M）に doctrine と恩賜組を表示。
4. プレイヤーの軍制改革イベント（#116）で 学閥主義↔実力主義 を切替＝既得権（軍刀組）の反発（#169 門閥の抵抗）をドラマ化。

---

## 6. テスト（EditMode・`MilitarySwordHonorRulesTests`）

栄誉判定（大学校卒TOP5=恩賜・上位外=星・非大学校卒=なし）／credential 順（恩賜>星>なし）／重み（学閥>実力）／**学閥主義＝低merit恩賜が高merit隊付に勝つ**（史実）／**実力主義＝俊英が追い越す**（米軍対比）／**doctrine 反転で勝敗逆転**／一括版の一致を固定。`TestHarness`（dotnet）でも回帰。
