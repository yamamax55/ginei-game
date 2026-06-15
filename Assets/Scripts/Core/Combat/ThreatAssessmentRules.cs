using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 脅威評価のパラメータ（AI が「どの敵が危ないか・退くべきか」を量る調整値）。基準値は ctor で全クランプ。
    /// </summary>
    public readonly struct ThreatAssessmentParams
    {
        /// <summary>距離正規化の基準（この距離で脅威の距離係数が下限・0で最大）。</summary>
        public readonly float threatRange;
        /// <summary>近接脅威の最小係数（遠方でも完全に無害にはしない）。</summary>
        public readonly float minDistanceFactor;
        /// <summary>自軍の側背面を取られている脅威の加算重み（包囲・側射の危険）。</summary>
        public readonly float flankThreatWeight;
        /// <summary>敵が既に他と交戦中なら脅威を割り引く係数（0..1・1=割引なし）。</summary>
        public readonly float engagedRelief;
        /// <summary>被脅威/自防御がこの比を超えたら劣勢＝撤退圧力が立つしきい値。</summary>
        public readonly float overwhelmRatio;

        public ThreatAssessmentParams(
            float threatRange,
            float minDistanceFactor,
            float flankThreatWeight,
            float engagedRelief,
            float overwhelmRatio)
        {
            this.threatRange = Mathf.Max(threatRange, 0.01f);
            this.minDistanceFactor = Mathf.Clamp01(minDistanceFactor);
            this.flankThreatWeight = Mathf.Max(0f, flankThreatWeight);
            this.engagedRelief = Mathf.Clamp01(engagedRelief);
            this.overwhelmRatio = Mathf.Max(1f, overwhelmRatio);
        }

        /// <summary>既定値（射程12・最小距離係数0.2・側背面重み0.5・交戦中割引0.6・劣勢比1.5）。</summary>
        public static ThreatAssessmentParams Default =>
            new ThreatAssessmentParams(12f, 0.2f, 0.5f, 0.6f, 1.5f);
    }

    /// <summary>
    /// 脅威評価の純ロジック（盤面非依存・plain引数）。AI が複数の敵から「最も危険な相手」を選び、
    /// 受けている総脅威と自軍防御力を突き合わせて交戦／撤退を決めるための数値を与える。
    /// 現状の <c>FleetAI</c> は自軍兵力比（<c>retreatRatio</c>）のみで退くため、近接・火力・側背面・敵の手番を
    /// 織り込んだ脅威の見立てが無い＝これを純Coreで補う（配線は Game の1箇所）。
    ///
    /// 分担：
    /// - 火力の質×数の畳み込みは <c>ForceQualityRules</c>/<c>AttritionExchangeRules</c>（こちらは受け手の危険度）。
    /// - 側背面倍率の公式は <see cref="CombatModifiers.FlankFactor"/>（ダメージ側）。こちらは脅威の重み付け側。
    /// すべて Mathf のみ・LINQ/乱数なし・入力非破壊（実効値パターン）・決定論。test-first。
    /// </summary>
    public static class ThreatAssessmentRules
    {
        private const float Eps = 0.0001f;

        /// <summary>
        /// 距離による脅威係数 minDistanceFactor..1（近いほど1・基準射程で下限）。
        /// </summary>
        public static float DistanceFactor(float distance, ThreatAssessmentParams p)
        {
            float d = Mathf.Max(distance, 0f);
            float near = Mathf.Clamp01(1f - d / p.threatRange);
            return Mathf.Lerp(p.minDistanceFactor, 1f, near);
        }

        /// <summary>
        /// 単一の敵が自軍に与える脅威スコア（0以上）。
        /// ＝敵火力 × 距離係数 × (1＋側背面危険) × (交戦中なら割引)。
        /// </summary>
        /// <param name="enemyFirepower">敵の実効火力（兵力×質など・呼び出し側で算出）。</param>
        /// <param name="distance">自軍→敵の距離。</param>
        /// <param name="ownFlankExposure">自軍が敵に晒している被弾面 0..1（1=完全な背面を取られている）。</param>
        /// <param name="enemyEngagedElsewhere">敵が既に他の味方と交戦中か（手が塞がっている）。</param>
        public static float ThreatScore(
            float enemyFirepower,
            float distance,
            float ownFlankExposure,
            bool enemyEngagedElsewhere,
            ThreatAssessmentParams p)
        {
            float fp = Mathf.Max(enemyFirepower, 0f);
            float dist = DistanceFactor(distance, p);
            float flank = 1f + p.flankThreatWeight * Mathf.Clamp01(ownFlankExposure);
            float relief = enemyEngagedElsewhere ? p.engagedRelief : 1f;
            return fp * dist * flank * relief;
        }

        /// <summary>脅威スコア（既定Params）。</summary>
        public static float ThreatScore(
            float enemyFirepower, float distance, float ownFlankExposure, bool enemyEngagedElsewhere) =>
            ThreatScore(enemyFirepower, distance, ownFlankExposure, enemyEngagedElsewhere,
                ThreatAssessmentParams.Default);

        /// <summary>
        /// 2脅威の優先比較（-1=A危険 / 1=B危険 / 0=同等）。スコア高優先・同点は近い方優先＝決定論的タイブレーク。
        /// </summary>
        public static int MoreDangerous(float threatA, float distA, float threatB, float distB)
        {
            if (threatA > threatB + Eps) return -1;
            if (threatB > threatA + Eps) return 1;
            if (distA < distB - Eps) return -1;
            if (distB < distA - Eps) return 1;
            return 0;
        }

        /// <summary>
        /// 撤退圧力 0..1（受けている総脅威と自軍防御力の比から導く）。
        /// 比 ≤1 で 0・overwhelmRatio で 1 へ線形＝劣勢ほど退きたくなる。
        /// </summary>
        public static float RetreatPressure(
            float totalIncomingThreat, float ownDefenseStrength, ThreatAssessmentParams p)
        {
            float own = Mathf.Max(ownDefenseStrength, Eps);
            float ratio = Mathf.Max(totalIncomingThreat, 0f) / own;
            if (ratio <= 1f) return 0f;
            float span = Mathf.Max(p.overwhelmRatio - 1f, Eps);
            return Mathf.Clamp01((ratio - 1f) / span);
        }

        /// <summary>劣勢で撤退すべきか（被脅威/自防御 が overwhelmRatio 超）。Params版。</summary>
        public static bool IsOverwhelmed(
            float totalIncomingThreat, float ownDefenseStrength, ThreatAssessmentParams p)
        {
            float own = Mathf.Max(ownDefenseStrength, Eps);
            return Mathf.Max(totalIncomingThreat, 0f) / own > p.overwhelmRatio;
        }

        /// <summary>劣勢で撤退すべきか（既定Params）。</summary>
        public static bool IsOverwhelmed(float totalIncomingThreat, float ownDefenseStrength) =>
            IsOverwhelmed(totalIncomingThreat, ownDefenseStrength, ThreatAssessmentParams.Default);
    }
}
