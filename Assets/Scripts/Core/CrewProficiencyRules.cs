using UnityEngine;

namespace Ginei
{
    /// <summary>乗員練度の調整係数（個艦の操艦・戦闘習熟。基準非破壊・全入力 Clamp）。</summary>
    public readonly struct CrewProficiencyParams
    {
        /// <summary>習熟が頭打ちになる訓練時間の規模（時間。逓減の基準）。</summary>
        public readonly float trainingScale;
        /// <summary>実戦経験が効く規模（実戦は訓練より価値が高い＝experienceWeight で重み付け）。</summary>
        public readonly float experienceWeight;
        /// <summary>習熟逓減の指数（0..1。小さいほど早く頭打ち）。</summary>
        public readonly float diminishExponent;
        /// <summary>砲術が訓練頻度で得る上乗せの最大（0..1）。</summary>
        public readonly float gunneryDrillBonus;
        /// <summary>機関が機器熟知で得る上乗せの最大（0..1）。</summary>
        public readonly float engineeringFamiliarityBonus;
        /// <summary>ダメコンが訓練実戦度で得る上乗せの最大（0..1）。</summary>
        public readonly float damageControlRealismBonus;
        /// <summary>チームワークが同乗期間で頭打ちになる規模（時間/月など）。</summary>
        public readonly float cohesionTimeScale;
        /// <summary>チームワークに対する指揮の質の寄与（0..1）。</summary>
        public readonly float leadershipWeight;
        /// <summary>性能補正の最大振れ幅（±これ＝0.5..1.5なら 0.5）。</summary>
        public readonly float performanceSwing;

        public CrewProficiencyParams(
            float trainingScale,
            float experienceWeight,
            float diminishExponent,
            float gunneryDrillBonus,
            float engineeringFamiliarityBonus,
            float damageControlRealismBonus,
            float cohesionTimeScale,
            float leadershipWeight,
            float performanceSwing)
        {
            this.trainingScale = Mathf.Max(0.0001f, trainingScale);
            this.experienceWeight = Mathf.Clamp01(experienceWeight);
            this.diminishExponent = Mathf.Clamp(diminishExponent, 0.0001f, 1f);
            this.gunneryDrillBonus = Mathf.Clamp01(gunneryDrillBonus);
            this.engineeringFamiliarityBonus = Mathf.Clamp01(engineeringFamiliarityBonus);
            this.damageControlRealismBonus = Mathf.Clamp01(damageControlRealismBonus);
            this.cohesionTimeScale = Mathf.Max(0.0001f, cohesionTimeScale);
            this.leadershipWeight = Mathf.Clamp01(leadershipWeight);
            this.performanceSwing = Mathf.Clamp(performanceSwing, 0f, 0.5f);
        }

        /// <summary>既定＝訓練規模500h・実戦重み0.6・逓減0.5・各上乗せ0.3・同乗規模24・指揮寄与0.4・振れ幅0.5。</summary>
        public static CrewProficiencyParams Default =>
            new CrewProficiencyParams(500f, 0.6f, 0.5f, 0.3f, 0.3f, 0.3f, 24f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 乗員練度の純ロジック＝個艦の乗員がどれだけ艦を熟知し戦えるか。
    /// 配置別習熟（操艦・砲術・機関・ダメコン）を訓練時間と実戦経験から逓減で積み、
    /// 同乗期間と指揮でチームワークを得る。損耗を新兵で埋めると練度は希釈される（古参比率で加重）。
    /// 総合練度から艦性能補正（0.5..1.5 の倍率）を返す＝基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 練度はすべて 0..1 の正規化値。乱数なし・決定論。VeterancyRules（部隊単位）とは別＝こちらは個艦の乗員。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CrewProficiencyRules
    {
        /// <summary>
        /// 配置別習熟（0..1）＝訓練時間＋実戦経験（experienceWeight 重み）を trainingScale で正規化し逓減指数で頭打ち。
        /// 実戦は訓練より価値が高い。
        /// </summary>
        public static float StationProficiency(float trainingHours, float combatExperience, CrewProficiencyParams p)
        {
            float t = Mathf.Max(0f, trainingHours);
            float c = Mathf.Max(0f, combatExperience);
            // 実戦は (1+experienceWeight) 倍の価値。合算を訓練規模で正規化。
            float effective = t + c * (1f + p.experienceWeight);
            float ratio = Mathf.Clamp01(effective / p.trainingScale);
            return Mathf.Clamp01(Mathf.Pow(ratio, p.diminishExponent));
        }

        public static float StationProficiency(float trainingHours, float combatExperience) =>
            StationProficiency(trainingHours, combatExperience, CrewProficiencyParams.Default);

        /// <summary>砲術練度（0..1）＝配置習熟に訓練頻度の上乗せ（drillFrequency 0..1×gunneryDrillBonus）を乗じる。</summary>
        public static float GunneryProficiency(float stationProficiency, float drillFrequency, CrewProficiencyParams p)
        {
            float s = Mathf.Clamp01(stationProficiency);
            float d = Mathf.Clamp01(drillFrequency);
            return Mathf.Clamp01(s * (1f + p.gunneryDrillBonus * d));
        }

        public static float GunneryProficiency(float stationProficiency, float drillFrequency) =>
            GunneryProficiency(stationProficiency, drillFrequency, CrewProficiencyParams.Default);

        /// <summary>機関運用練度（0..1）＝配置習熟に機器熟知の上乗せ（equipmentFamiliarity 0..1×engineeringFamiliarityBonus）を乗じる。</summary>
        public static float EngineeringProficiency(float stationProficiency, float equipmentFamiliarity, CrewProficiencyParams p)
        {
            float s = Mathf.Clamp01(stationProficiency);
            float e = Mathf.Clamp01(equipmentFamiliarity);
            return Mathf.Clamp01(s * (1f + p.engineeringFamiliarityBonus * e));
        }

        public static float EngineeringProficiency(float stationProficiency, float equipmentFamiliarity) =>
            EngineeringProficiency(stationProficiency, equipmentFamiliarity, CrewProficiencyParams.Default);

        /// <summary>ダメコン練度（0..1）＝配置習熟に訓練の実戦度の上乗せ（drillRealism 0..1×damageControlRealismBonus）を乗じる。</summary>
        public static float DamageControlProficiency(float stationProficiency, float drillRealism, CrewProficiencyParams p)
        {
            float s = Mathf.Clamp01(stationProficiency);
            float r = Mathf.Clamp01(drillRealism);
            return Mathf.Clamp01(s * (1f + p.damageControlRealismBonus * r));
        }

        public static float DamageControlProficiency(float stationProficiency, float drillRealism) =>
            DamageControlProficiency(stationProficiency, drillRealism, CrewProficiencyParams.Default);

        /// <summary>
        /// チームワーク（0..1）＝同乗期間（cohesionTimeScale で正規化・平方根で逓減）に指揮の質を加味。
        /// leadershipWeight ぶんを指揮の質、残りを同乗実績が占める。
        /// </summary>
        public static float CrewCohesion(float timeServedTogether, float leadershipQuality, CrewProficiencyParams p)
        {
            float served = Mathf.Clamp01(Mathf.Sqrt(Mathf.Clamp01(Mathf.Max(0f, timeServedTogether) / p.cohesionTimeScale)));
            float lead = Mathf.Clamp01(leadershipQuality);
            float v = served * (1f - p.leadershipWeight) + lead * p.leadershipWeight;
            return Mathf.Clamp01(v);
        }

        public static float CrewCohesion(float timeServedTogether, float leadershipQuality) =>
            CrewCohesion(timeServedTogether, leadershipQuality, CrewProficiencyParams.Default);

        /// <summary>
        /// 練度希釈倍率（0..1）＝古参比率と補充流入から、新兵で薄まったぶんを表す残存率。
        /// veteranRatio が高く replacementInflux が低いほど 1 に近い（練度を保つ）。
        /// </summary>
        public static float ProficiencyDilution(float veteranRatio, float replacementInflux, CrewProficiencyParams p)
        {
            float vet = Mathf.Clamp01(veteranRatio);
            float inflow = Mathf.Clamp01(replacementInflux);
            // 補充ぶんは古参比率の練度で薄まる。残存率＝古参が占める＋補充が古参練度で埋める分。
            return Mathf.Clamp01((1f - inflow) + inflow * vet);
        }

        public static float ProficiencyDilution(float veteranRatio, float replacementInflux) =>
            ProficiencyDilution(veteranRatio, replacementInflux, CrewProficiencyParams.Default);

        /// <summary>
        /// 艦の総合練度（0..1）＝砲術・機関・ダメコン・チームワークの加重平均。
        /// 砲術を最重視（戦闘）、次いでダメコン（生存）、機関、チームワーク。
        /// </summary>
        public static float OverallProficiency(float gunnery, float engineering, float damageControl, float crewCohesion, CrewProficiencyParams p)
        {
            float g = Mathf.Clamp01(gunnery);
            float e = Mathf.Clamp01(engineering);
            float d = Mathf.Clamp01(damageControl);
            float c = Mathf.Clamp01(crewCohesion);
            // 重み 砲術0.35 / ダメコン0.25 / 機関0.20 / チームワーク0.20 = 1.0
            float v = g * 0.35f + d * 0.25f + e * 0.20f + c * 0.20f;
            return Mathf.Clamp01(v);
        }

        public static float OverallProficiency(float gunnery, float engineering, float damageControl, float crewCohesion) =>
            OverallProficiency(gunnery, engineering, damageControl, crewCohesion, CrewProficiencyParams.Default);

        /// <summary>
        /// 艦性能補正（performanceSwing が 0.5 なら 0.5..1.5 の倍率）＝総合練度 0..1 を中心 1.0 の周りへ写す。
        /// 練度0.5で 1.0、0で 1-swing、1で 1+swing。基準性能に掛けて使う（基準非破壊）。
        /// </summary>
        public static float PerformanceModifier(float overallProficiency, CrewProficiencyParams p)
        {
            float prof = Mathf.Clamp01(overallProficiency);
            return Mathf.Lerp(1f - p.performanceSwing, 1f + p.performanceSwing, prof);
        }

        public static float PerformanceModifier(float overallProficiency) =>
            PerformanceModifier(overallProficiency, CrewProficiencyParams.Default);
    }
}
