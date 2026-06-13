# 学校の入学/卒業年齢の精緻化 — 史実ベース（設計＋実装 #SCHOOL-AGE）

> 各学校の**入学年齢・修業年限・卒業年齢**を史実（現代日本の学制＋旧日本軍の学校＋科挙）に即して精緻化し、散在していた `GraduationAge` 定数を**単一窓口 `SchoolAgeRules`** に集約する。
> 状態：**Core 純ロジック実装済**（`SchoolAgeRules`＋EditMode テスト＋既存 *Rules の委譲）。
> 既存との接続：教育チェーン #155-157（`ElementarySchool`〜`University`/`MilitaryAcademyRules`/`ImperialExamRules`）・LifecycleRules（年齢/死亡 #152）・`Person.birthYear`（生年逆算）。

---

## 0. 現状と問題

卒業年齢が各 *Rules に**定数で散在**し、史実と乖離していた：

| 学校 | 旧 GraduationAge | 問題 |
|---|---|---|
| 士官学校 (`OfficerAcademyRules`) | 22 | 妥当 |
| 大学 (`UniversityRules`) | **24** | 学部は18入学・**22卒業**が史実（24は院相当） |
| 高専/短大/専門 | 20 | 妥当 |
| **陸軍大学校** (`MilitaryAcademyRules`) | **22（一律）** | **致命的に非史実**＝陸大は現役将校が約28歳で選抜入校・**約31歳卒業**。全軍学校卒を一律22歳にしていた |
| 科挙 (`ImperialExamRules`) | 24（大学に相乗り） | 科挙は**年齢制限なし**・進士登用は平均30代。大学と同一視は誤り |

**入学年齢の概念が存在しなかった**（卒業年齢のみ）。本対応で入学年齢を史実で精緻化し、単一窓口へ集約する。

---

## 1. 実装 — 単一窓口 `SchoolAgeRules`

```csharp
public enum SchoolType {
  保育園, 幼稚園, 小学校, 中学校, 高校,   // 一般教育チェーン
  高専, 短大, 専門学校, 大学,            // 高等教育
  幼年学校, 士官学校, 陸軍大学校,         // 軍（将校）
  新兵訓練, 下士官学校,                  // 軍（兵・下士官）
  科挙                                  // 文官登用（年齢制限なし）
}

public static class SchoolAgeRules {
  public static int EntryAge(SchoolType s);        // 入学年齢（史実）
  public static int GraduationAge(SchoolType s);   // 卒業年齢（史実）
  public static int DurationYears(SchoolType s);   // = 卒業 − 入学
  public static bool IsAgeCapped(SchoolType s);    // 科挙のみ false＝年齢制限なし
  public static int GraduationAgeForDegree(MilitaryDegree d); // 軍学歴別の卒業年齢
}
```

### 史実ベースの年齢表

| 学校 | 入学 | 修業 | 卒業 | 史実根拠 |
|---|---|---|---|---|
| 保育園 | 0 | 6 | 6 | 0歳〜の保育 |
| 幼稚園 | 3 | 3 | 6 | 満3歳入園 |
| 小学校 | 6 | 6 | 12 | |
| 中学校 | 12 | 3 | 15 | |
| 高校 | 15 | 3 | 18 | |
| 高専 | 15 | 5 | 20 | 中学から5年制 |
| 短大 | 18 | 2 | 20 | 高校卒後 |
| 専門学校 | 18 | 2 | 20 | 高校卒後 |
| 大学 | 18 | 4 | **22** | 学部4年（旧24から精緻化） |
| 幼年学校 | 13 | 3 | 16 | 陸軍幼年学校＝高等小学校卒程度 |
| 士官学校 | 16 | 6 | 22 | 予科＋本科 |
| **陸軍大学校** | **28** | **3** | **31** | **現役将校（中尉〜大尉）が選抜入校** |
| 新兵訓練 | 18 | 0 | 18 | 入隊（月単位は `RecruitTrainingRules`） |
| 下士官学校 | 24 | 0 | 24 | 経験ある兵から（PMEは在職中） |
| 科挙 | 15 | (15) | 30 | **年齢制限なし**・進士登用は平均30代 |

- **年齢チェーン整合**：幼稚園卒6＝小学校入6／小学校卒12＝中学校入12／中学校卒15＝高校入15＝高専入15／高校卒18＝大学/短大/専門入18（テストで固定）。

---

## 2. 既存への適用（単一窓口へ集約・委譲）

- `OfficerAcademyRules.GraduationAge` / `UniversityRules.GraduationAge` / `TechnicalCollegeRules.GraduationAge` / `JuniorCollegeRules.GraduationAge` / `VocationalSchoolRules.GraduationAge` を **`SchoolAgeRules.GraduationAge(...)` へ委譲**（`static readonly`）＝二重定義を解消。既存テストはシンボル参照なので不変（大学のみ 24→22 の史実精緻化が反映）。
- `ImperialExamRules`：進士の生年を `SchoolAgeRules.GraduationAge(科挙)`（≒30）から算出（大学への相乗りをやめる）。
- **`MilitaryAcademyRules`：Funnel 後に学歴別で生年を精緻化** ＝ `GraduationAgeForDegree`（大学校卒31・士官学校卒22・幼年学校卒/退校16）。**同年卒でも到達学歴が高いほど年長**になり、陸大卒の参謀が史実どおり最古参になる。

---

## 3. 非目標 / 後方互換

- 月単位の訓練期間（新兵）は `RecruitTrainingRules.TrainingMonths` が担当（本書は年齢の年単位）。
- POP 層の小中高は人口動態（#153）でマクロに動き、本書は人物（`Person`）の生年逆算に効く。
- 既存テストはシンボル参照で不変（値の史実精緻化＝大学22・陸大31・科挙30 のみ反映）。

---

## 4. テスト（EditMode・`SchoolAgeRulesTests`）

年齢チェーン整合／修業年限＝卒業−入学／陸大は現役将校（28→31・士官学校22より年長）／幼年学校は若年（13→16）／科挙は年齢制限なし（30）／軍学歴別の生年（大学校卒>士官学校卒>幼年学校卒）／既存定数の委譲一致／高専<大学 を固定。`TestHarness`（dotnet）でも回帰。
