using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// NormalizationRules の調整係数（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// </summary>
    public readonly struct NormalizationParams
    {
        /// <summary>信頼性の最大値（訓練×標準化が完全なときの均質な部隊の信頼性）。</summary>
        public readonly float maxReliability;
        /// <summary>標準化による創発シナジーの最大ペナルティ（標準化=1で創発がこれだけ削られる）。</summary>
        public readonly float maxSynergyPenalty;
        /// <summary>予測可能性の最大値（信頼性=1で指揮しやすさが最大）。</summary>
        public readonly float maxPredictability;
        /// <summary>従順な身体が時間で蓄積する速度（規律×監視×dt の係数）。</summary>
        public readonly float docilityRate;
        /// <summary>画一化が多様性を犠牲にするコストの最大値。</summary>
        public readonly float maxHomogenizationCost;
        /// <summary>規格遵守＝逸脱を正常へ矯正する力の最大値（内面化した規範が高いほど強く矯正）。</summary>
        public readonly float maxNormCompliance;
        /// <summary>規律vs自律で訓練が自律的創意を抑える最大削減量（訓練=1で自律がこれだけ抑制）。</summary>
        public readonly float maxInitiativeSuppression;

        public NormalizationParams(
            float maxReliability, float maxSynergyPenalty, float maxPredictability,
            float docilityRate, float maxHomogenizationCost, float maxNormCompliance,
            float maxInitiativeSuppression)
        {
            this.maxReliability = Mathf.Max(0f, maxReliability);
            this.maxSynergyPenalty = Mathf.Max(0f, maxSynergyPenalty);
            this.maxPredictability = Mathf.Max(0f, maxPredictability);
            this.docilityRate = Mathf.Max(0f, docilityRate);
            this.maxHomogenizationCost = Mathf.Max(0f, maxHomogenizationCost);
            this.maxNormCompliance = Mathf.Max(0f, maxNormCompliance);
            this.maxInitiativeSuppression = Mathf.Max(0f, maxInitiativeSuppression);
        }

        /// <summary>
        /// 既定＝信頼性上限1.0／創発ペナ最大0.6／予測可能性上限1.0／従順化速度0.1／
        /// 画一化コスト最大0.5／規格矯正最大0.9／自律抑制最大0.7。
        /// </summary>
        public static NormalizationParams Default => new NormalizationParams(
            maxReliability: 1.0f,
            maxSynergyPenalty: 0.6f,
            maxPredictability: 1.0f,
            docilityRate: 0.1f,
            maxHomogenizationCost: 0.5f,
            maxNormCompliance: 0.9f,
            maxInitiativeSuppression: 0.7f);
    }

    /// <summary>
    /// 規律訓練と標準化＝ミシェル・フーコー『監獄の誕生』の規律権力（disciplinary power）の純ロジック（PANO-2 #1508）。
    /// 訓練・反復・標準化が個人を「従順で有用な身体」に作り変える＝信頼性・均質性は上がるが、
    /// 画一化が自発性・創発性を奪う（規律↑×創発性↓のトレードオフ）。任務の複雑さに応じて
    /// 標準化と自律のバランスを取るのが最適。乱数なし・決定論・基準非破壊（実効値パターン）。
    /// 分担：<see cref="VeterancyRules"/>＝会戦経験による練度、<see cref="DisciplineRules"/>＝軍紀と査問、
    /// <see cref="AutonomyRules"/>＝自律分散の創発シナジー（ここでは read-only でその削減量を返す）、
    /// PanoptismRules＝監視（同 EPIC PANO・別ロジック）。本ルールは規律権力による標準化のトレードオフを担う。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NormalizationRules
    {
        /// <summary>
        /// 信頼性・均質性＝訓練強度 trainingIntensity(0..1)×標準化 standardization(0..1)。
        /// 規律訓練が従順で有用な身体を作り、両者が揃って初めて部隊が均質で信頼できる
        /// （どちらか欠ければ信頼性は立たない＝積で表す）。0..maxReliability。
        /// </summary>
        public static float Reliability(float trainingIntensity, float standardization, NormalizationParams p)
        {
            float ti = Mathf.Clamp01(trainingIntensity);
            float sd = Mathf.Clamp01(standardization);
            return p.maxReliability * ti * sd;
        }

        public static float Reliability(float trainingIntensity, float standardization)
            => Reliability(trainingIntensity, standardization, NormalizationParams.Default);

        /// <summary>
        /// 創発シナジーのペナルティ＝標準化 standardization(0..1) が進むほど自律分散の創発
        /// （<see cref="AutonomyRules.EmergentSynergy"/>）が削られる量（画一化が自発性を奪う）。
        /// 標準化に比例し 0..maxSynergyPenalty を返す。創発シナジー本体は変えず削減分のみ返す（基準非破壊）。
        /// </summary>
        public static float EmergentSynergyPenalty(float standardization, NormalizationParams p)
        {
            return p.maxSynergyPenalty * Mathf.Clamp01(standardization);
        }

        public static float EmergentSynergyPenalty(float standardization)
            => EmergentSynergyPenalty(standardization, NormalizationParams.Default);

        /// <summary>
        /// 予測可能性＝信頼性 reliability(0..1) が高いほど行動が予測でき指揮しやすい。
        /// 規律権力の見返り（規格化された身体は読みやすい）。0..maxPredictability。
        /// </summary>
        public static float Predictability(float reliability, NormalizationParams p)
        {
            return p.maxPredictability * Mathf.Clamp01(reliability);
        }

        public static float Predictability(float reliability)
            => Predictability(reliability, NormalizationParams.Default);

        /// <summary>
        /// 従順な身体（フーコー）＝規律 disciplineIntensity(0..1)×監視 surveillance(0..1) が
        /// 時間 dt で従順度を積み上げる。規律と監視が揃うほど速く従順化する（積×時間）。
        /// 現在値 current(0..1) に加算し 0..1 にクランプして返す（時間積分・dt 非負）。
        /// </summary>
        public static float DocileBody(float current, float disciplineIntensity, float surveillance, float dt, NormalizationParams p)
        {
            float d = Mathf.Clamp01(disciplineIntensity);
            float s = Mathf.Clamp01(surveillance);
            float t = Mathf.Max(0f, dt);
            float gain = p.docilityRate * d * s * t;
            return Mathf.Clamp01(Mathf.Clamp01(current) + gain);
        }

        public static float DocileBody(float current, float disciplineIntensity, float surveillance, float dt)
            => DocileBody(current, disciplineIntensity, surveillance, dt, NormalizationParams.Default);

        /// <summary>
        /// 画一化コスト＝標準化 standardization(0..1) が多様性 diversityValue(0..1)（柔軟な対応力）を
        /// 犠牲にする量。標準化が高く多様性が貴重なほど大きい（積に比例）。0..maxHomogenizationCost。
        /// </summary>
        public static float HomogenizationCost(float standardization, float diversityValue, NormalizationParams p)
        {
            float sd = Mathf.Clamp01(standardization);
            float dv = Mathf.Clamp01(diversityValue);
            return p.maxHomogenizationCost * sd * dv;
        }

        public static float HomogenizationCost(float standardization, float diversityValue)
            => HomogenizationCost(standardization, diversityValue, NormalizationParams.Default);

        /// <summary>
        /// 規格遵守＝逸脱 deviation(0..1) を正常へ矯正する力。内面化した規範 internalizedNorm(0..1) が
        /// 高いほど、また逸脱が大きいほど強く矯正がかかる（規格からの外れを正す＝積に比例）。
        /// 0..maxNormCompliance。
        /// </summary>
        public static float NormCompliance(float internalizedNorm, float deviation, NormalizationParams p)
        {
            float norm = Mathf.Clamp01(internalizedNorm);
            float dev = Mathf.Clamp01(deviation);
            return p.maxNormCompliance * norm * dev;
        }

        public static float NormCompliance(float internalizedNorm, float deviation)
            => NormCompliance(internalizedNorm, deviation, NormalizationParams.Default);

        /// <summary>
        /// 規律vs自律のトレードオフ＝訓練 trainingIntensity(0..1) が自律 autonomy(0..1) を抑える
        /// 実効自律度を返す。訓練された軍は信頼できるが自律的創意は乏しい
        /// ＝autonomy から trainingIntensity×maxInitiativeSuppression ぶんを削った 0..1 を返す（基準非破壊）。
        /// </summary>
        public static float DisciplineVsInitiative(float trainingIntensity, float autonomy, NormalizationParams p)
        {
            float ti = Mathf.Clamp01(trainingIntensity);
            float au = Mathf.Clamp01(autonomy);
            float suppression = ti * p.maxInitiativeSuppression;
            return Mathf.Clamp01(au * (1f - suppression));
        }

        public static float DisciplineVsInitiative(float trainingIntensity, float autonomy)
            => DisciplineVsInitiative(trainingIntensity, autonomy, NormalizationParams.Default);

        /// <summary>
        /// 最適標準化＝任務の複雑さ missionComplexity(0..1) が高いほど標準化を緩め創発を許す最適点。
        /// 単純任務は標準化（規律で信頼性を取る）、複雑任務は自律（標準化を緩めて創発を許す）。
        /// (1 - missionComplexity) を 0..1 で返す。
        /// </summary>
        public static float OptimalStandardization(float missionComplexity)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(missionComplexity));
        }
    }
}
