using UnityEngine;

namespace Ginei
{
    /// <summary>政体形態（ルソー『社会契約論』＝規模に適合させる）。小＝民主政・中＝貴族政・大＝君主政。</summary>
    public enum PolityForm { 民主政, 貴族政, 君主政 }

    /// <summary>政体規模適合の調整係数。</summary>
    public readonly struct PolityScaleParams
    {
        /// <summary>国家規模に版図が寄与する重み（広いほど大国）。</summary>
        public readonly float territoryWeight;
        /// <summary>国家規模に人口が寄与する重み（多いほど大国）。</summary>
        public readonly float populationWeight;
        /// <summary>規模適合度の非線形度（最適規模からの距離の冪指数・1以上）。離れるほど加速して合わない。</summary>
        public readonly float fitExponent;
        /// <summary>ミスマッチペナルティの最大値（0..1・統治効率の最大低下幅）。</summary>
        public readonly float maxMismatchPenalty;
        /// <summary>一般意志の希薄化の最大値（0..1・大国で市民が遠くなる上限）。</summary>
        public readonly float maxGeneralWillDilution;
        /// <summary>防衛力の下限（0..1・最小国家でも持つ最低限の防衛力）。</summary>
        public readonly float minDefensiveStrength;
        /// <summary>専制化リスクの最大値（0..1・大国×希薄化の専制上限）。</summary>
        public readonly float maxDespotismRisk;

        public PolityScaleParams(float territoryWeight, float populationWeight, float fitExponent,
            float maxMismatchPenalty, float maxGeneralWillDilution, float minDefensiveStrength,
            float maxDespotismRisk)
        {
            this.territoryWeight = Mathf.Max(0f, territoryWeight);
            this.populationWeight = Mathf.Max(0f, populationWeight);
            this.fitExponent = Mathf.Max(1f, fitExponent);
            this.maxMismatchPenalty = Mathf.Clamp01(maxMismatchPenalty);
            this.maxGeneralWillDilution = Mathf.Clamp01(maxGeneralWillDilution);
            this.minDefensiveStrength = Mathf.Clamp01(minDefensiveStrength);
            this.maxDespotismRisk = Mathf.Clamp01(maxDespotismRisk);
        }

        /// <summary>既定＝版図重み0.5・人口重み0.5・適合冪指数2・ミスマッチ上限0.6・希薄化上限0.8・防衛下限0.2・専制上限0.7。</summary>
        public static PolityScaleParams Default =>
            new PolityScaleParams(0.5f, 0.5f, 2f, 0.6f, 0.8f, 0.2f, 0.7f);
    }

    /// <summary>
    /// 政体規模適合の純ロジック（ルソー『社会契約論』・ROUS-3 #1466）。
    /// 「政体には適正規模がある＝小国は直接民主・一般意志が働きやすいが防衛に弱い。大国は防衛に強いが
    /// 一般意志が希薄化し専制化しやすい。政府の形態（民主政=小・貴族政=中・君主政=大）は国家の規模に
    /// 適合せねばならない」を式に出す。版図×人口で国家規模を出し、規模に適した政体形態を導く。政体形態と
    /// 規模のミスマッチは統治効率にペナルティを生む。大国ほど一般意志が希薄化して市民が遠くなり、直接参加
    /// （直接民主）が困難になり代表制が要る。一方で小国は防衛に弱く征服されやすい（ルソーのジレンマ＝
    /// 理想の小国は弱い）。大国で一般意志が薄れると専制化リスクが高まる。
    /// 拡大共和国（<see cref="ExtendedRepublicRules"/>＝大共和国ほど派閥が中和される）とは別＝こちらは
    /// 規模と政体形態の適合（どの規模にどの政体が向くか）を扱う。過拡張（<see cref="OverextensionRules"/>＝
    /// 負担と国力の比＝版図の重さ）とも別。一般意志の希薄化は GeneralWillRules（同EPIC ROUS）へ、
    /// 政体そのものの腐化は PolityCorruptionRules（別EPIC MONT・政体腐化）へ接続する。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PolityScaleRules
    {
        /// <summary>各政体形態が最適とする規模の代表値（民主政＝小・貴族政＝中・君主政＝大）。</summary>
        private const float DemocracyOptimalScale = 0.0f; // 民主政＝最小（都市国家）
        private const float AristocracyOptimalScale = 0.5f; // 貴族政＝中規模
        private const float MonarchyOptimalScale = 1.0f; // 君主政＝大国（帝国）
        /// <summary>民主政⇄貴族政・貴族政⇄君主政の規模しきい値（OptimalFormForScale）。</summary>
        private const float DemocracyAristocracyThreshold = 1f / 3f;
        private const float AristocracyMonarchyThreshold = 2f / 3f;

        /// <summary>
        /// 国家の規模（0..1）＝版図×版図重み＋人口×人口重み（重み和で正規化）。
        /// 広い版図・多い人口ほど大国。版図も人口も大きいほど1へ近づく。
        /// </summary>
        public static float StateScale(float territorySize, float population, PolityScaleParams p)
        {
            float t = Mathf.Clamp01(territorySize);
            float pop = Mathf.Clamp01(population);
            float weightSum = p.territoryWeight + p.populationWeight;
            if (weightSum <= 0f) return 0f;
            float raw = (t * p.territoryWeight + pop * p.populationWeight) / weightSum;
            return Mathf.Clamp01(raw);
        }

        public static float StateScale(float territorySize, float population)
            => StateScale(territorySize, population, PolityScaleParams.Default);

        /// <summary>
        /// 規模に適した政体形態。小国＝民主政（一般意志が直接働く）・中規模＝貴族政・大国＝君主政
        /// （広域を一意志で束ねる）。ルソー＝政府の形態は国家の規模に適合せねばならない。
        /// </summary>
        public static PolityForm OptimalFormForScale(float stateScale)
        {
            float s = Mathf.Clamp01(stateScale);
            if (s < DemocracyAristocracyThreshold) return PolityForm.民主政;
            if (s < AristocracyMonarchyThreshold) return PolityForm.貴族政;
            return PolityForm.君主政;
        }

        /// <summary>各政体形態が最適とする規模（民主政0・貴族政0.5・君主政1.0）。</summary>
        private static float OptimalScaleOf(PolityForm form)
        {
            switch (form)
            {
                case PolityForm.民主政: return DemocracyOptimalScale;
                case PolityForm.貴族政: return AristocracyOptimalScale;
                default: return MonarchyOptimalScale;
            }
        }

        /// <summary>
        /// 政体形態と規模の適合度（0..1）。政体の最適規模と実規模の距離が小さいほど高い＝
        /// 民主政は小国で高く大国で低い・君主政は逆。距離を冪で非線形に効かせる（離れるほど加速して合わない）。
        /// </summary>
        public static float ScaleFormFit(PolityForm form, float stateScale, PolityScaleParams p)
        {
            float s = Mathf.Clamp01(stateScale);
            float optimal = OptimalScaleOf(form);
            float distance = Mathf.Abs(s - optimal); // 0..1
            float penalized = Mathf.Pow(distance, p.fitExponent);
            return Mathf.Clamp01(1f - penalized);
        }

        public static float ScaleFormFit(PolityForm form, float stateScale)
            => ScaleFormFit(form, stateScale, PolityScaleParams.Default);

        /// <summary>
        /// ミスマッチペナルティ（0..maxMismatchPenalty）。政体と規模の適合度が低いほど統治効率が落ちる
        /// ＝（1−適合度）×ペナルティ上限。適合度1.0なら0（ぴったり合えば無罰）。
        /// 各統治係数に（1−これ）を掛けて使う（実効値パターン・基準非破壊）。
        /// </summary>
        public static float MismatchPenalty(float scaleFormFit, PolityScaleParams p)
        {
            float fit = Mathf.Clamp01(scaleFormFit);
            return Mathf.Clamp01((1f - fit) * p.maxMismatchPenalty);
        }

        public static float MismatchPenalty(float scaleFormFit)
            => MismatchPenalty(scaleFormFit, PolityScaleParams.Default);

        /// <summary>
        /// 一般意志の希薄化（0..maxGeneralWillDilution）。規模が大きいほど市民が遠くなり一般意志が薄れる
        /// ＝規模×希薄化上限。小国（規模0）なら希薄化なし＝一般意志が直接働く。GeneralWillRules へ接続。
        /// </summary>
        public static float GeneralWillDilution(float stateScale, PolityScaleParams p)
        {
            float s = Mathf.Clamp01(stateScale);
            return Mathf.Clamp01(s * p.maxGeneralWillDilution);
        }

        public static float GeneralWillDilution(float stateScale)
            => GeneralWillDilution(stateScale, PolityScaleParams.Default);

        /// <summary>
        /// 直接参加（直接民主）の実現可能性（0..1）。小国ほど市民が一堂に会せて直接参加が成り立つ
        /// ＝（1−規模）。大国は不可能で代表制が要る（規模1.0なら0＝直接参加は不可能）。
        /// </summary>
        public static float DirectParticipationFeasibility(float stateScale)
        {
            float s = Mathf.Clamp01(stateScale);
            return Mathf.Clamp01(1f - s);
        }

        /// <summary>
        /// 防衛力（minDefensiveStrength..1）。規模が大きいほど防衛に強い＝下限＋規模×（1−下限）。
        /// 小国は征服されやすい（ルソーのジレンマ＝理想の小国は弱い）。最小国家でも下限ぶんは持つ。
        /// </summary>
        public static float DefensiveStrength(float stateScale, PolityScaleParams p)
        {
            float s = Mathf.Clamp01(stateScale);
            return Mathf.Clamp01(p.minDefensiveStrength + s * (1f - p.minDefensiveStrength));
        }

        public static float DefensiveStrength(float stateScale)
            => DefensiveStrength(stateScale, PolityScaleParams.Default);

        /// <summary>
        /// 規模に伴う専制化リスク（0..maxDespotismRisk）。大国で一般意志が希薄化すると専制に傾く
        /// ＝規模×希薄化×専制上限。小国（規模0）や希薄化なしならリスクなし。
        /// 両方が高い（大国かつ市民が遠い）ときに専制化が進む＝ルソーが大国を警戒した核心。
        /// </summary>
        public static float ScaleDespotismRisk(float stateScale, float generalWillDilution, PolityScaleParams p)
        {
            float s = Mathf.Clamp01(stateScale);
            float dilution = Mathf.Clamp01(generalWillDilution);
            return Mathf.Clamp01(s * dilution * p.maxDespotismRisk);
        }

        public static float ScaleDespotismRisk(float stateScale, float generalWillDilution)
            => ScaleDespotismRisk(stateScale, generalWillDilution, PolityScaleParams.Default);

        /// <summary>
        /// 政体が規模に適合した健全な状態か＝適合度が閾値以上。ぴったり合っていれば（民主政の小国・
        /// 君主政の大国など）統治が円滑＝true。閾値未満は規模と政体がちぐはぐで非効率＝false。
        /// </summary>
        public static bool IsScaleAppropriate(float scaleFormFit, float threshold)
        {
            float fit = Mathf.Clamp01(scaleFormFit);
            float th = Mathf.Clamp01(threshold);
            return fit >= th;
        }
    }
}
