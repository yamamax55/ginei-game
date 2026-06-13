# 新兵教育システム — 米軍の accession→BCT→AIT を参考に（設計＋実装 #RECRUIT）

> 兵（軍属POP）を訓練して**練度ある兵力**に変えるパイプラインを、**実在の米軍の新兵教育**（募集→基礎訓練→専門訓練）を参考に集約モデル化する。
> ネームド将校を生む士官学校系（`MilitaryAcademyRules`/`OfficerAcademyRules` #155）とは**別系統＝兵の大量養成**。「兵を率いる人」でなく「率いられる兵の質」を作る。
> 参照する実制度：徴募（accession・全志願制 AVF／徴兵）・ASVAB（適性試験＝選抜基準）・**Basic Combat Training（boot camp）**・**Advanced Individual Training（AIT＝MOS別）**・教導隊（drill instructor）・washout（脱落）・即応サイクル・総力戦の量質トレードオフ。
> 著作権注意：固有名・条文は流用せず、**訓練パイプラインの構造のみ**を参考にする。
> 状態：**Core 純ロジック実装済**（`RecruitTrainingRules`/`RecruitDepot`＋EditMode テスト）。盤面/UI 配線は §5（★）で後続。数値・創作裁定は【要・作者判断】。
> 参考にした既存イシュー：**#96 人口/徴募**（`OccupationRules.RecruitablePool`＝軍属POP）・**#2032 POPLAB-6 徴募↔生産労働の競合（総力戦）**（`MobilizationRules`）・**#2034 POP労働スキル**（`SkillEffectRules.MilitaryQuality`＝SKILL-7 #2041）・**#155 士官養成**（学校データ作法）・**#106 戦闘係数**（`CombatModifiers`）。

---

## 0. なぜ「新兵教育」が要るか／既存とのギャップ

このゲームは既に**将校（ネームド）を生む教育チェーン**を完備している（小学校→…→士官学校/大学/科挙）。だが**兵（enlisted＝軍属POP）の練度**を作る層が無い：

| 既存（カバー範囲） | モジュール | 役割 |
|---|---|---|
| 徴募源（軍属POP） | `OccupationRules.RecruitablePool`（#96） | 兵の供給プール（誰を兵にできるか） |
| 総力戦動員 | `MobilizationRules`（#2032） | 生産労働↔徴募の競合・量質トレードオフ |
| POP技能→軍の質 | `SkillEffectRules.MilitaryQuality`（#2034/#2041） | 技能→戦闘力（#106）への合流口 |
| 将校の養成（多段選抜） | `MilitaryAcademyRules`（#155） | **ネームド将校**を輩出（兵ではない） |
| 兵力プール | `FleetPool`/`FleetPoolRules`（#148） | 勢力の総艦艇＝兵力の器 |

**欠落＝「軍属プールが、訓練を経て、どれだけの数・どれだけの練度の兵になるか」**。米軍はここを accession→BCT→AIT の明確なパイプラインで設計しており、**量（頭数）と質（練度）のトレードオフ**が政策決定になる。これが本システムの核。

---

## 1. 米軍の新兵教育（参考＝抽出するメカニクス）

| 段階 | 実制度 | ゲーム化価値 |
|---|---|---|
| 募集 accession | 全志願制（AVF）／徴兵。ASVAB で適性選別＝**基準（cut score）を上げると質は上がるが頭数は減る** | 選抜基準レバー＝質 vs 量 |
| 基礎訓練 BCT | boot camp（約10週）。市民を兵に。**教導隊（drill instructor）の質**と**訓練基地の収容**が律速。**washout（脱落）**がある | 教官の質・訓練枠・脱落率 |
| 専門訓練 AIT | MOS（兵科）別の技能付与＝砲術/機関/航法… | 兵科別練度（将来拡張） |
| 出力 | 練度ある兵が部隊へ。**訓練所要時間**＝パイプライン遅延 | 補充の供給と遅延 |
| 総力戦 | 戦時は**基準を下げ・訓練を短縮**＝頭数は増えるが練度は落ちる | 動員の量質トレードオフ（#2032） |

---

## 2. 実装（Core 純ロジック・集約・後方互換・実効値パターン）

> **個体粒度へ降りない＝訓練所×集約**（兵1人ずつのシミュはしない＝スカラビリティ規律 PERF #1117）。状態は変えない read-only。練度→軍の質は既存窓口へ**委譲**して二重実装しない。

### 2-1. データ `RecruitDepot`（新兵訓練所）— 学校データ作法に倣う
`Academy`/`HighSchool`/`VocationalTrainingSchool` と同型の純データ：
```csharp
[System.Serializable]
public class RecruitDepot {
    public int depotId;
    public Faction faction;
    public int capacity = 200;       // 年間の基礎訓練スループット上限（訓練基地のボトルネック）
    public float cadreQuality = 0.5f; // 教官（教導隊）の質 0..1
    public float standards = 0.5f;    // 選抜基準 0..1（高いほど厳選＝ASVAB カット相当）
    public int foundedYear;
}
```

### 2-2. ルール `RecruitTrainingRules`（唯一の窓口）
```csharp
public enum RecruitStage { 募集, 基礎訓練, 専門訓練 }   // accession / BCT / AIT

public static class RecruitTrainingRules {
    // 募集＝徴募源(軍属)から訓練枠の範囲で受け入れる。基準↑で絞り、動員↑で増やす。capacity が上限。
    public static int Accessions(RecruitDepot depot, float recruitablePool, float mobilizationRate);
    // 基礎訓練の脱落率 0..1（教官の質↑/厳選↑で↓・動員サージで↑）
    public static float WashoutFraction(RecruitDepot depot, float mobilizationRate);
    // 修了者数（trained manpower）＝募集×(1−脱落)
    public static int Graduates(int accessions, float washoutFraction);
    public static int Graduates(RecruitDepot depot, float recruitablePool, float mobilizationRate);
    // 練度 0..1（教官の質×厳選で↑・動員＝訓練短縮で↓）
    public static float Proficiency(RecruitDepot depot, float mobilizationRate);
    // 訓練所要（game-月・動員で短縮＝補充の遅延）
    public static float TrainingMonths(float mobilizationRate);
    // 軍の質への寄与＝練度を SkillEffectRules.MilitaryQuality（#2034/#106）へ委譲
    public static float MilitaryQuality(RecruitDepot depot, float mobilizationRate, float baseline);
}
```

**公式（係数は const・調整可・実効値パターン＝基準値非破壊）**：
- 募集率 ＝ `0.20 ×(1 − 0.5×standards)×(1 + mobilizationRate)`、受入数 ＝ `min(capacity, pool×募集率)`。
- 脱落率 ＝ `clamp01(0.15 − 0.10×cadre − 0.10×standards + 0.15×mob)`。
- 練度 ＝ `clamp01(0.35 + 0.40×cadre + 0.25×standards − 0.30×mob)`。
- 訓練所要 ＝ `max(2, 6×(1 − 0.6×mob))` 月。

### 2-3. 中核トレードオフ（テストで固定）
- **厳選（standards↑）**＝受入は減るが練度が上がる（少数精鋭）。
- **総力戦動員（mob↑）**＝頭数は増えるが練度は落ち、訓練は短縮される（量質トレードオフ＝#2032 と整合）。
- **教官の質（cadre↑）**＝練度↑・脱落↓（量を保ったまま質を上げる唯一のレバー＝投資先）。
- **訓練枠（capacity）**＝スループット上限（基地が足りないと頭数が出ない）。

---

## 3. 既存システムとの接続（二重実装しない）

| 接続先 | 何を渡すか |
|---|---|
| `OccupationRules.RecruitablePool`（#96） | `recruitablePool` 入力＝軍属POPの徴募源 |
| `MobilizationRules`（#2032） | `mobilizationRate` 入力＝総力戦の動員率（生産労働と競合） |
| `SkillEffectRules.MilitaryQuality`（#2034/#106） | `Proficiency` を militarySkill として渡し**軍の質→戦闘力**へ（委譲＝唯一の窓口） |
| `FleetPool`（#148）／補充 | `Graduates`（修了者数）＝兵力の補充供給（配線層で `FleetPool.Add` 等へ） |
| `SkillStock`（#2034） | 将来＝惑星の軍属技能ストックを練度の素地に使う（教育格差→練度格差） |

---

## 4. 非目標（タイクン化／終盤ラグ回避）

- 兵1人ずつの訓練・配属シミュはしない（訓練所×集約のスカラー）。
- MOS（兵科）の全網羅展開はしない（`RecruitStage.専門訓練` は型のみ・兵科別練度は将来の最小拡張）。
- 戦闘力公式を新設しない（練度は `SkillEffectRules.MilitaryQuality`→`CombatModifiers` #106 に合流）。
- 毎フレーム評価しない（配線は暦境界 Tick）。

---

## 5. 配線（後続・★）

`GalaxyView` の年次/日次 Tick（士官学校 `MilitaryAcademyRules` を回している近傍）に相乗りして：
1. 勢力ごとに `RecruitDepot`（デモ＝帝国/同盟に1つ）を持たせ、所有惑星の `RecruitablePool` 合計と現在の `mobilizationRate` から `Graduates`/`Proficiency` を解く。
2. **修了者数を補充供給**＝`FleetPool.Add` 等で兵力を増やす（造船 `ShipyardRules.CommissionToPool` #884 と並ぶ「人」の供給路）。`TrainingMonths` ぶんの遅延キューで補充が効く。
3. **練度を軍の質へ**＝`MilitaryQuality` を勢力の戦闘力係数に反映（#106 合流）。
4. 観測層（`MilitaryObserverOverlay` M／`CoreStateInspector` J）に「新兵教育：募集/修了/練度/訓練所要」を表示し、`CoreStateInspector.glossary` にフィールド説明を追記（規約どおり）。
5. 第2層（操作化）＝プレイヤーが**選抜基準・訓練枠投資・教官の質**を回すレバーを `StrategyMapWindow` の軍事メニューへ（自動化はしない）。

---

## 6. テスト（EditMode・`RecruitTrainingRulesTests`）

募集（基準/動員で増減・訓練枠で上限）／脱落率（教官・厳選で↓・動員で↑）／修了者数（合成一致）／練度（最大/床/動員低下）／訓練所要（短縮＋下限）／軍の質の委譲一致／**質 vs 量**（厳選は少数精鋭・総力戦は頭数だが練度↓）を固定。`TestHarness`（dotnet）でも回帰。
