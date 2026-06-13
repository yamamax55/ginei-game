# 下士官教育システム — 米軍 NCO PME（NCOPDS）を参考に（設計＋実装 #210 / #NCOEDU）

> 部隊の「背骨」＝**下士官団（NCO corps）の育成エンジン**を、実在の米軍の **NCO 専門職教育（NCO Professional Development System＝NCOPDS／Enlisted PME）** を参考に集約モデル化する。
> 既存イシュー **#210「兵・下士官・将校の三層 — 下士官団＝部隊の練度・結束・自律の担い手（損耗の質）」** の下士官層の**育成ロジック**を担う（#210 が示す「下士官は経験でしか育たない部隊の背骨／損耗の質／再建に時間」を数値化）。
> 系統の位置づけ：**新兵教育（`RecruitTrainingRules` RECRUIT）＝兵の量産** ／ **士官教育（`MilitaryAcademyRules` #155）＝将校** ／ **本書＝下士官（NCO）の質**。三者は別系統。
> 参照する実制度：BLC（Basic Leader Course）→ALC（Advanced Leader Course）→SLC（Senior Leader Course）→MLC/USASMA（Master Leader/Sergeants Major Academy）の **PME ラダー**、**STEP（Select-Train-Educate-Promote＝教育が昇進の前提＝no school, no promotion）**、NCO＝"backbone of the army"、"you can't surge experience"（経験は急造できない）。
> 著作権注意：固有名・条文は流用せず、**教育パイプラインと昇進ゲートの構造のみ**を参考にする。
> 状態：**Core 純ロジック実装済**（`NcoEducationRules`/`NcoCorps`＋EditMode テスト）。盤面/UI・`Squadron`/`FleetMorale` 配線は §5（★）で後続。数値・創作裁定は【要・作者判断】。
> 参考にした既存イシュー：**#210 三層/下士官団**（本体）・#96 徴募（兵の補充元）・#155 士官教育（将校・対比）・#147 任務戦術／#206 通信断（自律の前提）・#169 門地開放／#110 階級／#168 貴族（将校 vs 下士官の文化）・#106 係数・#192 厭戦（ベテラン損耗）。

---

## 0. なぜ「下士官教育」が要るか（#210 の核）

現状の部隊は **提督（`AdmiralData`）＋抽象兵力（`strength`/`EscortShip`）** で、**兵力の「中身の質」**が無い。#210 はこれを**兵・下士官・将校の三層**で表し、核心を **下士官団＝経験でしか育たない部隊の背骨** に置く：

- **兵は徴募（#96）で量的に補充できる**が、**下士官は質的資産**。損耗で失うと部隊が弱体化し、**再建に時間（年単位）**がかかる。
- 強い下士官団は **練度（命中/回避）・士気の粘り（`FleetMorale`）・結束（崩壊耐性）・自律（命令なしで動ける＝任務戦術#147／ミノフスキー通信断#206 下でも崩れない）** の背骨。
- **損耗の「質」**＝戦闘損耗は無差別だが、下士官を失うと institutional experience の喪失＝**ベテラン部隊の壊滅は痛恨**。

米軍はこの「下士官団をどう育て、なぜ急造できないか」を **NCOPDS（PME ラダー＋STEP）** として制度化しており、本システムの育成ロジックに最適。

---

## 1. 米軍 NCO PME（参考＝抽出するメカニクス）

| 米制度 | 内容 | ゲーム化価値 |
|---|---|---|
| PME ラダー（BLC→ALC→SLC→MLC） | 下士官の段位ごとに対応する学校課程 | 多段の選抜（上ほど狭き門・#155 に倣う） |
| **STEP（no school, no promotion）** | **対応 PME を修了しないと次の段位へ昇進できない** | 教育＝昇進の前提＝下士官の質の担保（headline） |
| NCO＝backbone | 小部隊指揮・訓練・規律・標準の担い手 | 下士官団の厚み×質→部隊実効の背骨倍率 |
| you can't surge experience | 経験ある下士官は急造できない・損耗の再建が遅い | 損耗の質的打撃＋再建の年数（#210 核心） |
| 大衆動員の希薄化 | 急拡大すると下士官比が薄まり質が落ちる | 職業軍 vs 徴集大量軍のトレードオフ |

---

## 2. 実装（Core 純ロジック・集約・後方互換）

> **個体粒度へ降りない＝下士官団は2スカラー（厚み density・質 quality）に集約**（#210 の「個々の兵管理でなく部隊の下士官の厚みという質指標」＝タイクン化/終盤ラグ回避 PERF #1117）。状態は変えない read-only。

### 2-1. データ `NcoCorps`（下士官団）
```csharp
[System.Serializable]
public class NcoCorps {
    public float density = 0.5f; // 下士官の厚み 0..1（理想比に対する充足）
    public float quality = 0.5f; // 下士官団の質 0..1（PME 到達段＋経験）
}
```

### 2-2. ルール `NcoEducationRules`（唯一の窓口）
```csharp
public enum NcoCourse { 初級, 中級, 上級, 最先任 }   // BLC / ALC / SLC / MLC(SMA)

public static class NcoEducationRules {
    // STEP（教育が昇進の前提）
    public static int GradeTierFor(NcoCourse course);                       // 課程→段位(1..4)
    public static NcoCourse RequiredCourseForTier(int ncoTier);
    public static bool PromotionEligible(NcoCourse highestCompleted, int targetTier); // no school, no promotion

    // PME ラダーの選抜（上ほど狭き門）
    public static float PassRate(NcoCourse course);
    public static int QuotaPassing(int sitters, NcoCourse course);
    public static int Graduates(int eligiblePool, int capacity, NcoCourse course);

    // 下士官団の質
    public static float ProgramQuality(NcoCourse highestProgram, float academyQuality);

    // 背骨効果（#210：練度・結束・自律）
    public static float Thickness(float ncoCount, float troopStrength);
    public static float ProficiencyMultiplier(NcoCorps corps);  // 命中/回避
    public static float CohesionMultiplier(NcoCorps corps);     // 士気の粘り
    public static float AutonomyFactor(NcoCorps corps);         // 命令なしで動ける(#147/#206)

    // 損耗の質・再建（“経験は急造できない”）
    public static float AttritionExperienceLoss(NcoCorps corps, float casualtyFraction); // 質的打撃
    public static float DilutionFactor(float expansionRate);                              // 急拡大で薄まる
    public static float RebuildYears(float currentQuality, float targetQuality);          // 経験育成は年単位
}
```

**公式（係数は const・調整可・実効値パターン）**：
- 合格数 ＝ `floor(sitters × PassRate)`、PassRate＝初級0.70/中級0.55/上級0.40/最先任0.25（狭き門）。
- 下士官団の質 ＝ `academyQuality ×(0.40 + 0.60×ladder)`（ladder＝0初級..1最先任）。
- 厚み ＝ `clamp01(下士官比 / 0.15)`（理想比0.15＝兵約7に1）。
- 練度倍率 ＝ `1 + 0.30×density×quality`／結束倍率 ＝ `1 + 0.40×density×quality`／自律 ＝ `density×quality`。
- 損耗の質的打撃Δ ＝ `clamp(casualty ×(1 + 0.5×quality), 0, quality)`（**ベテランほど失う経験が大きい**）。
- 希薄化 ＝ `1 − 0.5×expansionRate`／再建 ＝ `(target−current)×6 年`。

### 2-3. 中核トレードオフ／ドラマ（テストで固定）
- **STEP**：教育を経ない昇進は不可＝下士官の質が制度的に担保される（学校が律速）。
- **背骨**：厚み×質の**両方**が要る＝兵だけ（下士官枯渇）の部隊は数はいても弱く、自律≒0で中央指揮頼みに麻痺（#210/#147/#206）。
- **損耗の質**：ベテラン下士官団ほど1損耗あたりの経験喪失が大きく、再建は年単位＝**ベテラン部隊の壊滅は痛恨**（#210 核心・#192 厭戦へ）。
- **量 vs 質**：急拡大（大衆動員）は下士官比を薄め質を落とす＝職業軍 vs 徴集大量軍。

---

## 3. 既存システムとの接続（窓口を増やさない・#210 の方針）

| 接続先 | 何を渡す／受ける |
|---|---|
| `RecruitTrainingRules`（RECRUIT・新兵教育） | 適格な熟練兵プール＝下士官候補の供給（兵→経験→下士官） |
| `OccupationRules.RecruitablePool`（#96） | 兵の補充元（下士官は即補充できない＝対比） |
| `FleetMorale`/`Squadron`/`EscortShip`（#210） | 背骨倍率（練度/結束）・自律を部隊属性へ（配線層で係数#106 として乗算） |
| 任務戦術#147／通信断#206 | `AutonomyFactor` を分散指揮可否の判定に |
| `MilitaryAcademyRules`（#155）／門地#169/#168/#110 | 将校 vs 下士官の文化・叩き上げ将校（#543）の入口 |

---

## 4. 非目標（タイクン化／終盤ラグ回避）

- 個々の下士官を管理しない（下士官団＝density/quality の2スカラー）。
- 兵科別 MOS の全網羅はしない（PME 段は4段に絞る）。
- 戦闘力公式を新設しない（背骨は係数#106／`FleetMorale` に乗る）。
- 毎フレーム評価しない（配線は暦境界 Tick・損耗は会戦結果イベント）。

---

## 5. 配線（後続・★）

1. 部隊（`Squadron`/`EscortShip`）または勢力に `NcoCorps`（厚み・質）を属性として持たせる（#210・SaveData#108 永続化）。
2. **背骨倍率を会戦に反映**＝`ProficiencyMultiplier`/`CohesionMultiplier` を命中/回避・`FleetMorale` の粘りへ（#106 合流）。`AutonomyFactor` を任務戦術#147/通信断#206 の自律判定へ。
3. **損耗の質**＝会戦の損耗率から `AttritionExperienceLoss` で下士官団の質を削り、`RebuildYears` ぶんかけて年次 Tick で経験育成（兵は#96 で即補充）。
4. **育成（PME）**＝`GalaxyView` 年次 Tick（士官学校 `MilitaryAcademyRules` の近傍）で熟練兵プールから `Graduates`/`ProgramQuality` を解き下士官団の質を底上げ。`DilutionFactor` で急拡大の希薄化。
5. 観測層（`MilitaryObserverOverlay` M／`CoreStateInspector` J）に「下士官団：厚み/質/自律/再建年」を表示＋glossary 追記（規約どおり）。

---

## 6. テスト（EditMode・`NcoEducationRulesTests`）

STEP（教育が昇進の前提）／PME 選抜（上ほど狭き門・枠と候補で律速）／下士官団の質（ラダー到達で上昇）／厚み（理想比正規化）／背骨倍率（厚み×質の両立が必要・下士官枯渇は倍率1.0）／自律（厚く質高い時のみ）／**損耗の質**（ベテランほど経験喪失大・現在質で頭打ち）／希薄化／再建年数を固定。`TestHarness`（dotnet）でも回帰。
