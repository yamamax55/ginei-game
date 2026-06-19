using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊即応性（レディネス）の調整値。整備の寄与重み・乗員充足の寄与重み・出撃準備の基準時間・警戒度の下限
    /// （ゼロ除算回避）・即応待機の消耗レート・緊急増勢の上限・既定の戦闘即応しきい値。すべて ctor で安全側へ
    /// クランプ。盤面非依存・決定論。
    /// </summary>
    public readonly struct FleetReadinessParams
    {
        /// <summary>物的即応度で整備状態が占める重み（残りは補給。0.5＝整備と補給が等価）。</summary>
        public readonly float maintenanceWeight;
        /// <summary>人的即応度で乗員充足が占める重み（残りは士気。0.5＝充足と士気が等価）。</summary>
        public readonly float crewWeight;
        /// <summary>即応度満タンでも残る出撃準備の素の所要時間（即応0なら更に伸びる前の基準）。</summary>
        public readonly float basePrepTime;
        /// <summary>警戒度の下限（これ未満として扱わない＝ゼロ除算・準備時間の発散回避）。</summary>
        public readonly float minAlert;
        /// <summary>即応待機1単位あたりの消耗レート（警戒度×待機時間に掛ける＝張り詰めるほど疲れる）。</summary>
        public readonly float standbyAttritionRate;
        /// <summary>即応度満タン時に上乗せできる緊急増勢の上限比（0.5＝一時的に5割増しまで無理が利く）。</summary>
        public readonly float surgeMax;
        /// <summary>戦闘即応とみなす総合即応度の既定しきい値。</summary>
        public readonly float combatReadyThreshold;

        public FleetReadinessParams(float maintenanceWeight, float crewWeight, float basePrepTime,
            float minAlert, float standbyAttritionRate, float surgeMax, float combatReadyThreshold)
        {
            this.maintenanceWeight = Mathf.Clamp01(maintenanceWeight);
            this.crewWeight = Mathf.Clamp01(crewWeight);
            this.basePrepTime = Mathf.Max(0f, basePrepTime);
            this.minAlert = Mathf.Clamp(minAlert, 0.01f, 1f);
            this.standbyAttritionRate = Mathf.Clamp(standbyAttritionRate, 0f, 4f);
            this.surgeMax = Mathf.Clamp(surgeMax, 0f, 2f);
            this.combatReadyThreshold = Mathf.Clamp01(combatReadyThreshold);
        }

        /// <summary>既定：整備重み0.5・乗員重み0.5・準備基準2.0・警戒下限0.1・消耗レート0.5・増勢上限0.5・即応しきい0.6。</summary>
        public static FleetReadinessParams Default => new FleetReadinessParams(
            DefaultMaintenanceWeight, DefaultCrewWeight, DefaultBasePrepTime,
            DefaultMinAlert, DefaultStandbyAttritionRate, DefaultSurgeMax, DefaultCombatReadyThreshold);

        public const float DefaultMaintenanceWeight = 0.5f;
        public const float DefaultCrewWeight = 0.5f;
        public const float DefaultBasePrepTime = 2.0f;
        public const float DefaultMinAlert = 0.1f;
        public const float DefaultStandbyAttritionRate = 0.5f;
        public const float DefaultSurgeMax = 0.5f;
        public const float DefaultCombatReadyThreshold = 0.6f;
    }

    /// <summary>
    /// 艦隊即応性（出撃可能態勢）の純ロジック（唯一の窓口）＝<b>いつでも出撃できる態勢をどれだけ保てているか</b>。
    /// 整備・補給で<b>物的即応度</b>を、乗員充足・士気で<b>人的即応度</b>を出し、両者を掛け合わせて<b>総合即応度</b>を得る
    /// （どちらか欠ければ総合は落ちる＝艦が直っても人が足りなければ出られない）。総合即応度から出撃準備の所要時間と
    /// 緊急増勢の余地が決まり、警戒度を上げて即応待機を続けると消耗が積もる（張り詰め続けは疲れる）。ローテーションで
    /// 一部を常時即応・一部を休養に回し、常時即応のカバー率を保つ。<b>分担</b>：整備サイクルそのものは
    /// <see cref="MaintenanceCycleRules"/>、補給優先度は <see cref="SupplyPriorityRules"/>、作戦テンポは
    /// <see cref="OperationalTempoRules"/>。こちらは<b>出撃前の即応態勢</b>に特化。盤面非依存の plain 引数・
    /// 実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class FleetReadinessRules
    {
        /// <summary>既定パラメータで物的即応度。</summary>
        public static float MaterialReadiness(float maintenanceState, float supplyLevel)
            => MaterialReadiness(maintenanceState, supplyLevel, FleetReadinessParams.Default);

        /// <summary>
        /// 整備状態×補給で物的即応度（0..1）。整備重みでブレンドした加重平均だが、片方が0なら成立しないので
        /// 不足側に引っ張られる（最小値で底を打つ）。material＝lerp(min(整備,補給), 加重平均, 0.5）の形で
        /// 「整備されていても弾薬が無ければ撃てない」を表す。
        /// </summary>
        public static float MaterialReadiness(float maintenanceState, float supplyLevel, FleetReadinessParams p)
        {
            float maint = Mathf.Clamp01(maintenanceState);
            float supply = Mathf.Clamp01(supplyLevel);
            float weighted = p.maintenanceWeight * maint + (1f - p.maintenanceWeight) * supply;
            float worst = Mathf.Min(maint, supply);
            return Mathf.Clamp01(Mathf.Lerp(worst, weighted, 0.5f));
        }

        /// <summary>既定パラメータで人的即応度。</summary>
        public static float PersonnelReadiness(float crewStrength, float crewMorale)
            => PersonnelReadiness(crewStrength, crewMorale, FleetReadinessParams.Default);

        /// <summary>
        /// 乗員充足×士気で人的即応度（0..1）。乗員重みでブレンドした加重平均を、不足側（最小値）へ半分引き寄せる。
        /// personnel＝lerp(min(充足,士気), 加重平均, 0.5）＝「定員が揃っても士気が無ければ動かない」。
        /// </summary>
        public static float PersonnelReadiness(float crewStrength, float crewMorale, FleetReadinessParams p)
        {
            float crew = Mathf.Clamp01(crewStrength);
            float morale = Mathf.Clamp01(crewMorale);
            float weighted = p.crewWeight * crew + (1f - p.crewWeight) * morale;
            float worst = Mathf.Min(crew, morale);
            return Mathf.Clamp01(Mathf.Lerp(worst, weighted, 0.5f));
        }

        /// <summary>既定パラメータで総合即応度。</summary>
        public static float OverallReadiness(float materialReadiness, float personnelReadiness)
            => OverallReadiness(materialReadiness, personnelReadiness, FleetReadinessParams.Default);

        /// <summary>
        /// 物的×人的で総合即応度（0..1）。両者の幾何平均＝sqrt(物的×人的)。どちらか欠けると総合が下がる
        /// （艦は直っても人が居なければ／人は居ても艦が動かなければ出撃できない）。
        /// </summary>
        public static float OverallReadiness(float materialReadiness, float personnelReadiness, FleetReadinessParams p)
        {
            float mat = Mathf.Clamp01(materialReadiness);
            float per = Mathf.Clamp01(personnelReadiness);
            return Mathf.Clamp01(Mathf.Sqrt(mat * per));
        }

        /// <summary>既定パラメータで出撃準備の所要時間。</summary>
        public static float SortiePreparationTime(float overallReadiness, float alertLevel)
            => SortiePreparationTime(overallReadiness, alertLevel, FleetReadinessParams.Default);

        /// <summary>
        /// 出撃準備の所要時間。即応度が高いほど短く、警戒度（普段から身構えている度）が高いほど短い。
        /// time＝基準×(1−即応)/警戒度。即応1なら0（即出撃）、即応0・警戒0.5なら基準×2。警戒度は下限でクランプ
        /// （ゼロ除算・発散回避）。
        /// </summary>
        public static float SortiePreparationTime(float overallReadiness, float alertLevel, FleetReadinessParams p)
        {
            float ready = Mathf.Clamp01(overallReadiness);
            float alert = Mathf.Max(p.minAlert, Mathf.Clamp01(alertLevel));
            return p.basePrepTime * (1f - ready) / alert;
        }

        /// <summary>既定パラメータで即応待機の消耗。</summary>
        public static float StandbyAttrition(float alertLevel, float standbyDuration)
            => StandbyAttrition(alertLevel, standbyDuration, FleetReadinessParams.Default);

        /// <summary>
        /// 警戒度×待機時間で即応待機の消耗（0..1）。高い警戒度で長く張り詰めるほど人も機も疲弊する。
        /// attrition＝clamp01(警戒度×待機時間×レート)。警戒0.8・待機2.5・レート0.5なら1.0（限界）。
        /// 低警戒の長期待機は消耗が緩やか＝平時のローテ運用が利く根拠。
        /// </summary>
        public static float StandbyAttrition(float alertLevel, float standbyDuration, FleetReadinessParams p)
        {
            float alert = Mathf.Clamp01(alertLevel);
            float duration = Mathf.Max(0f, standbyDuration);
            return Mathf.Clamp01(alert * duration * p.standbyAttritionRate);
        }

        /// <summary>既定パラメータで常時即応のカバー率。</summary>
        public static float RotationCoverage(int unitsInRotation, int totalUnits)
            => RotationCoverage(unitsInRotation, totalUnits, FleetReadinessParams.Default);

        /// <summary>
        /// 即応ローテに入れている隊数/総隊数で常時即応のカバー率（0..1）。一部を即応待機・残りを休養に回しても
        /// 即応待機の割合だけは盤面を守れる。coverage＝即応数/総数。総数0なら0。即応数は総数で頭打ち。
        /// </summary>
        public static float RotationCoverage(int unitsInRotation, int totalUnits, FleetReadinessParams p)
        {
            int total = Mathf.Max(0, totalUnits);
            if (total <= 0) return 0f;
            int inRot = Mathf.Clamp(unitsInRotation, 0, total);
            return Mathf.Clamp01((float)inRot / total);
        }

        /// <summary>既定パラメータで緊急増勢の余地。</summary>
        public static float SurgeCapacity(float overallReadiness)
            => SurgeCapacity(overallReadiness, FleetReadinessParams.Default);

        /// <summary>
        /// 即応度に応じた緊急増勢（一時的に無理が利く度合い、0..上限）。態勢が整っているほど無理が利く＝
        /// 余裕のある艦隊ほど緊急時に踏ん張れる。surge＝即応度×増勢上限。即応0なら0（無理は利かない）。
        /// 既定上限0.5＝満タンで最大5割増し。
        /// </summary>
        public static float SurgeCapacity(float overallReadiness, FleetReadinessParams p)
        {
            float ready = Mathf.Clamp01(overallReadiness);
            return Mathf.Clamp(ready * p.surgeMax, 0f, p.surgeMax);
        }

        /// <summary>戦闘即応か（bool）＝総合即応度が既定しきい値以上。</summary>
        public static bool IsCombatReady(float overallReadiness)
            => IsCombatReady(overallReadiness, FleetReadinessParams.DefaultCombatReadyThreshold);

        /// <summary>
        /// 戦闘即応か（bool）＝総合即応度がしきい値以上。中途半端な態勢で出すと各個撃破される＝出撃の足切り。
        /// </summary>
        public static bool IsCombatReady(float overallReadiness, float threshold)
        {
            float ready = Mathf.Clamp01(overallReadiness);
            return ready >= Mathf.Clamp01(threshold);
        }
    }
}
