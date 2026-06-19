using UnityEngine;

namespace Ginei
{
    /// <summary>難民危機の調整係数（戦災・占領による強制移動）。</summary>
    public readonly struct RefugeeCrisisParams
    {
        /// <summary>難民発生の感度（戦災×占領苛烈の積に掛かる人口比の上限割合 0..1）。大きいほど住民が一気に流出する。</summary>
        public readonly float displacementSensitivity;
        /// <summary>占領苛烈が発生量を底上げする重み（0..1・苛烈な占領ほど住民が逃げ出す）。</summary>
        public readonly float occupationWeight;
        /// <summary>流入圧の非線形度（発生量/受入能力の冪指数・1以上）。受入能力を超えるほど一気に圧が高まる。</summary>
        public readonly float influxExponent;
        /// <summary>受入安定が社会負荷を緩める強さ（0..1・安定した受入先ほど負荷を吸収する）。</summary>
        public readonly float stabilityRelief;
        /// <summary>人道対応の運営効率の重み（0..1・支援物資を活かす組織力ほど対応水準が上がる）。</summary>
        public readonly float organizationWeight;
        /// <summary>キャンプ困窮への対応の効き（0..1以上・人道対応が困窮を割り引く強さ）。</summary>
        public readonly float reliefStrength;
        /// <summary>治安リスクの非線形度（困窮×負荷の冪指数・1以上）。困窮と負荷が重なるほど一気に荒れる。</summary>
        public readonly float riskExponent;
        /// <summary>帰還見込みの故地治安の重み（0..1・故地が安全なほど帰る）。</summary>
        public readonly float homelandWeight;
        /// <summary>帰還見込みの復興の重み（0..1・復興が進むほど帰る）。</summary>
        public readonly float reconstructionWeight;

        public RefugeeCrisisParams(float displacementSensitivity, float occupationWeight,
            float influxExponent, float stabilityRelief, float organizationWeight,
            float reliefStrength, float riskExponent, float homelandWeight, float reconstructionWeight)
        {
            this.displacementSensitivity = Mathf.Clamp01(displacementSensitivity);
            this.occupationWeight = Mathf.Clamp01(occupationWeight);
            this.influxExponent = Mathf.Max(1f, influxExponent);
            this.stabilityRelief = Mathf.Clamp01(stabilityRelief);
            this.organizationWeight = Mathf.Clamp01(organizationWeight);
            this.reliefStrength = Mathf.Max(0f, reliefStrength);
            this.riskExponent = Mathf.Max(1f, riskExponent);
            this.homelandWeight = Mathf.Clamp01(homelandWeight);
            this.reconstructionWeight = Mathf.Clamp01(reconstructionWeight);
        }

        /// <summary>既定＝発生感度0.5・占領重み0.6・流入冪2・受入安定緩和0.5・運営重み0.5・対応の効き1・リスク冪2・故地重み0.6・復興重み0.4。</summary>
        public static RefugeeCrisisParams Default =>
            new RefugeeCrisisParams(0.5f, 0.6f, 2f, 0.5f, 0.5f, 1f, 2f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 難民危機の純ロジック（戦災・占領による住民流出と受入負荷・人道危機）。
    /// 戦災や苛烈な占領で住民が故地を追われ（強制移動）、避難先へ流れ込み、受入勢力の収容能力を超えて
    /// 社会負荷をかける。人道支援が困窮を和らげるが、対応が追いつかなければキャンプは荒れ、犯罪・過激化・
    /// 疫病の治安リスクが高まる。故地の治安と復興が整えば難民は帰還する。
    /// <see cref="PopulationMigrationRules"/>（平時の住みよさによる自発的移住）とは別系統＝こちらは
    /// 戦災避難の<b>強制移動</b>と受入側の危機管理を扱う。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。入力は全て clamp。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RefugeeCrisisRules
    {
        /// <summary>
        /// 難民発生量（0..1・人口比）。戦災（0..1）×人口（0..1）で住民が故地を追われ、占領苛烈（0..1）が
        /// （1＋苛烈×占領重み）で発生を底上げする＝苛烈な占領ほど多く逃げ出す。感度を上限割合として掛ける。
        /// 戦災または人口が0なら発生0（戦災と人口の積）。占領苛烈は増幅子（戦災のある所をより苛烈にする）。
        /// </summary>
        public static float DisplacementVolume(float warDamage, float occupationHarshness, float population,
            RefugeeCrisisParams p)
        {
            float damage = Mathf.Clamp01(warDamage);
            float harsh = Mathf.Clamp01(occupationHarshness);
            float pop = Mathf.Clamp01(population);
            float baseVol = damage * pop * p.displacementSensitivity;
            float amplified = baseVol * (1f + harsh * p.occupationWeight);
            return Mathf.Clamp01(amplified);
        }

        public static float DisplacementVolume(float warDamage, float occupationHarshness, float population)
            => DisplacementVolume(warDamage, occupationHarshness, population, RefugeeCrisisParams.Default);

        /// <summary>
        /// 流入圧（0..1）。難民発生量（0..1）を受入能力（0..1）で割って受入先への流入の強さを出す。
        /// 受入能力が小さいほど圧が高く、冪で非線形に効く＝能力を超えるほど一気に逼迫する。
        /// 発生量0なら0、受入能力0でも分母を最小値で守り発生量に応じて1へ近づく。
        /// </summary>
        public static float InfluxPressure(float displacementVolume, float hostCapacity, RefugeeCrisisParams p)
        {
            float vol = Mathf.Clamp01(displacementVolume);
            float cap = Mathf.Clamp01(hostCapacity);
            float ratio = vol / Mathf.Max(0.0001f, cap);
            float pressure = Mathf.Pow(Mathf.Clamp01(ratio), p.influxExponent);
            return Mathf.Clamp01(pressure);
        }

        public static float InfluxPressure(float displacementVolume, float hostCapacity)
            => InfluxPressure(displacementVolume, hostCapacity, RefugeeCrisisParams.Default);

        /// <summary>
        /// 受入側の社会負荷（0..1）。流入圧（0..1）を受入安定（0..1）が緩める＝安定した社会ほど吸収する。
        /// 受入安定は（1−安定×緩和強さ）で負荷を割り引く。流入圧0なら負荷0、安定0かつ高い流入圧で1へ近づく。
        /// </summary>
        public static float AbsorptionStrain(float influxPressure, float hostStability, RefugeeCrisisParams p)
        {
            float pressure = Mathf.Clamp01(influxPressure);
            float stab = Mathf.Clamp01(hostStability);
            float relief = 1f - stab * p.stabilityRelief;
            return Mathf.Clamp01(pressure * relief);
        }

        public static float AbsorptionStrain(float influxPressure, float hostStability)
            => AbsorptionStrain(influxPressure, hostStability, RefugeeCrisisParams.Default);

        /// <summary>
        /// 人道対応水準（0..1）。支援物資（0..1）×運営能力（0..1）で難民への人道対応の質を出す。
        /// 運営能力は（1−運営重み＋運営×運営重み）で物資を活かす度合いを表す＝物資があっても回す組織力が要る。
        /// 物資0なら対応0（物資が要る）、物資・運営とも高いほど1へ近づく。
        /// </summary>
        public static float HumanitarianResponse(float aidResources, float organizationCapacity, RefugeeCrisisParams p)
        {
            float aid = Mathf.Clamp01(aidResources);
            float org = Mathf.Clamp01(organizationCapacity);
            float orgFactor = (1f - p.organizationWeight) + org * p.organizationWeight;
            return Mathf.Clamp01(aid * orgFactor);
        }

        public static float HumanitarianResponse(float aidResources, float organizationCapacity)
            => HumanitarianResponse(aidResources, organizationCapacity, RefugeeCrisisParams.Default);

        /// <summary>
        /// 難民キャンプの困窮度（0..1）。流入圧（0..1）を人道対応（0..1）が和らげる＝対応水準が高いほど困窮が下がる。
        /// 流入圧/(1＋対応×対応の効き)で割る＝対応がなければ困窮はそのまま、対応が進むほど割り引かれる。
        /// 流入圧0なら困窮0、対応0かつ高い流入圧で1へ近づく。
        /// </summary>
        public static float CampDestitution(float influxPressure, float humanitarianResponse, RefugeeCrisisParams p)
        {
            float pressure = Mathf.Clamp01(influxPressure);
            float response = Mathf.Clamp01(humanitarianResponse);
            float relieved = pressure / (1f + response * p.reliefStrength);
            return Mathf.Clamp01(relieved);
        }

        public static float CampDestitution(float influxPressure, float humanitarianResponse)
            => CampDestitution(influxPressure, humanitarianResponse, RefugeeCrisisParams.Default);

        /// <summary>
        /// 治安リスク（0..1・犯罪・過激化・疫病）。キャンプの困窮（0..1）×受入側の社会負荷（0..1）で
        /// 難民危機の治安リスクを出す。困窮と負荷の積を冪で非線形に効かせる＝困窮と負荷が重なるほど一気に荒れる。
        /// どちらかが0ならリスク0（積）、両方高いと1へ近づく。
        /// </summary>
        public static float SecurityRisk(float campDestitution, float absorptionStrain, RefugeeCrisisParams p)
        {
            float destitution = Mathf.Clamp01(campDestitution);
            float strain = Mathf.Clamp01(absorptionStrain);
            float combined = destitution * strain;
            return Mathf.Clamp01(Mathf.Pow(combined, p.riskExponent));
        }

        public static float SecurityRisk(float campDestitution, float absorptionStrain)
            => SecurityRisk(campDestitution, absorptionStrain, RefugeeCrisisParams.Default);

        /// <summary>
        /// 帰還の見込み（0..1）。故地の治安（0..1）×復興（0..1）が高いほど難民が帰る＝安全で住める故地が要る。
        /// 故地治安・復興を重み付き平均で混ぜ（積でなく和＝どちらかが整えば帰り始める）、発生量（0..1）が
        /// 大きいほど一斉帰還は鈍る（1−発生量×0.5 で抑制）。故地治安・復興とも0なら帰還見込み0。
        /// </summary>
        public static float ReturnViability(float homelandSecurity, float reconstruction, float displacementVolume,
            RefugeeCrisisParams p)
        {
            float security = Mathf.Clamp01(homelandSecurity);
            float recon = Mathf.Clamp01(reconstruction);
            float vol = Mathf.Clamp01(displacementVolume);
            float wSum = Mathf.Max(0.0001f, p.homelandWeight + p.reconstructionWeight);
            float readiness = (security * p.homelandWeight + recon * p.reconstructionWeight) / wSum;
            float damped = readiness * (1f - vol * 0.5f); // 大量発生は帰還を鈍らせる
            return Mathf.Clamp01(damped);
        }

        public static float ReturnViability(float homelandSecurity, float reconstruction, float displacementVolume)
            => ReturnViability(homelandSecurity, reconstruction, displacementVolume, RefugeeCrisisParams.Default);

        /// <summary>
        /// 危機が管理可能か＝人道対応（0..1）が治安リスク（0..1）を閾値ぶん上回れば true。
        /// 対応水準−治安リスク≧閾値なら、支援が荒廃に勝っていて危機を制御できている。
        /// </summary>
        public static bool IsCrisisManageable(float humanitarianResponse, float securityRisk, float threshold)
        {
            float response = Mathf.Clamp01(humanitarianResponse);
            float risk = Mathf.Clamp01(securityRisk);
            float thr = Mathf.Clamp01(threshold);
            return response - risk >= thr;
        }

        public static bool IsCrisisManageable(float humanitarianResponse, float securityRisk)
            => IsCrisisManageable(humanitarianResponse, securityRisk, 0.1f);
    }
}
