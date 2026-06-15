using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 消耗交換比のパラメータ（質と数のトレードオフ）。基準値は ctor で全クランプ。
    /// </summary>
    public readonly struct AttritionExchangeParams
    {
        /// <summary>質の比を交換比へ写すべき指数（0.5＝平方根＝質の差を穏やかに反映）。</summary>
        public readonly float qualityExponent;
        /// <summary>交換比の下限（不利でもこれ以上は悪化しない）。</summary>
        public readonly float minRatio;
        /// <summary>交換比の上限（有利でもこれ以上は良化しない）。</summary>
        public readonly float maxRatio;
        /// <summary>有利な交換比とみなす既定しきい値（自損1あたり敵損がこれ超で有利）。</summary>
        public readonly float favorableThreshold;
        /// <summary>補充で支えられる損耗率の上限（兵站#94連動の前提・1.0未満）。</summary>
        public readonly float maxSustainableRate;
        /// <summary>消耗戦の最終勝者を決める相対戦力差のしきい値（これ以内は拮抗）。</summary>
        public readonly float winnerMargin;

        public AttritionExchangeParams(
            float qualityExponent,
            float minRatio,
            float maxRatio,
            float favorableThreshold,
            float maxSustainableRate,
            float winnerMargin)
        {
            this.qualityExponent = Mathf.Clamp(qualityExponent, 0.1f, 2f);
            // 下限は (0, 1] / 上限は [1, 大) に収める＝交換比の意味（1.0=拮抗）を保つ。
            this.minRatio = Mathf.Clamp(minRatio, 0.01f, 1f);
            this.maxRatio = Mathf.Max(1f, maxRatio);
            this.favorableThreshold = Mathf.Max(1f, favorableThreshold);
            this.maxSustainableRate = Mathf.Clamp(maxSustainableRate, 0.01f, 1f);
            this.winnerMargin = Mathf.Clamp(winnerMargin, 0f, 1f);
        }

        /// <summary>既定値（平方根写像・交換比 0.25〜4.0・有利>1.0・支持損耗率上限0.5・拮抗幅0.05）。</summary>
        public static AttritionExchangeParams Default =>
            new AttritionExchangeParams(0.5f, 0.25f, 4f, 1f, 0.5f, 0.05f);
    }

    /// <summary>
    /// 消耗交換比（質と数のトレードオフ）の純ロジック（盤面非依存・plain引数）。
    /// 会戦の消耗戦は「自損1あたりどれだけ敵を削れるか」の交換比で決まる：質（練度・火力・防御）
    /// が高い側は有利な交換比を得るが、数で押す側は不利な交換比でも総量で押し切る。
    ///
    /// 分担：
    /// - <c>LanchesterRules</c>（局所火力の二乗則）とは別物＝こちらは質×数の交換比モデル。
    /// - <c>ForceQualityRules</c>（戦力の質）の交換比版＝質差を「与損/被損の比」へ翻訳する。
    /// すべて Mathf のみ・LINQ/乱数なし・実効値パターン（入力は非破壊）。
    /// </summary>
    public static class AttritionExchangeRules
    {
        private const float Eps = 0.0001f;

        // ---- 交換比 ----

        /// <summary>質の差から交換比（自損1あたりの敵損）。質が高いほど大きい＝有利。Params版。</summary>
        public static float ExchangeRatio(float ownQuality, float enemyQuality, AttritionExchangeParams p)
        {
            float own = Mathf.Max(ownQuality, Eps);
            float enemy = Mathf.Max(enemyQuality, Eps);
            // (own/enemy)^exponent を Pow で（Log/Exp 不可）。等質なら 1.0。
            float ratio = Mathf.Pow(own / enemy, p.qualityExponent);
            return Mathf.Clamp(ratio, p.minRatio, p.maxRatio);
        }

        /// <summary>交換比（既定Params）。</summary>
        public static float ExchangeRatio(float ownQuality, float enemyQuality) =>
            ExchangeRatio(ownQuality, enemyQuality, AttritionExchangeParams.Default);

        // ---- 損害 ----

        /// <summary>与えた損害＝自軍規模×戦闘強度×交換比（相手規模で頭打ち）。</summary>
        public static float LossesInflicted(float ownStrength, float exchangeRatio, float intensity)
        {
            float own = Mathf.Max(ownStrength, 0f);
            float r = Mathf.Max(exchangeRatio, 0f);
            float it = Mathf.Clamp01(intensity);
            return own * it * r;
        }

        /// <summary>受けた損害＝敵軍規模×戦闘強度÷交換比（有利な交換比ほど被害は小さい）。</summary>
        public static float LossesTaken(float enemyStrength, float exchangeRatio, float intensity)
        {
            float enemy = Mathf.Max(enemyStrength, 0f);
            float r = Mathf.Max(exchangeRatio, Eps);
            float it = Mathf.Clamp01(intensity);
            return enemy * it / r;
        }

        /// <summary>その交換比は有利か（自損1あたり敵損がしきい値超）。Params版。</summary>
        public static bool FavorableExchange(float exchangeRatio, AttritionExchangeParams p) =>
            exchangeRatio > p.favorableThreshold;

        /// <summary>その交換比は有利か（任意しきい値）。</summary>
        public static bool FavorableExchange(float exchangeRatio, float threshold) =>
            exchangeRatio > threshold;

        // ---- 消耗戦の勝者 ----

        /// <summary>
        /// 消耗戦の最終勝者（-1=自/0=拮抗/1=敵）。質を交換比へ畳み込んだ実効戦力で比較する＝
        /// 数で劣る高品質側でも交換比で押し返せるが、差が大きければ数が質を覆す。Params版。
        /// </summary>
        public static int WarOfAttritionWinner(
            float ownStrength, float ownQuality,
            float enemyStrength, float enemyQuality,
            AttritionExchangeParams p)
        {
            float ratio = ExchangeRatio(ownQuality, enemyQuality, p);
            float ownEff = Mathf.Max(ownStrength, 0f) * ratio;
            float enemyEff = Mathf.Max(enemyStrength, 0f);
            float rel = (ownEff - enemyEff) / (ownEff + enemyEff + Eps);
            rel = Mathf.Clamp(rel, -1f, 1f);
            if (rel > p.winnerMargin) return -1;
            if (rel < -p.winnerMargin) return 1;
            return 0;
        }

        /// <summary>消耗戦の最終勝者（既定Params）。</summary>
        public static int WarOfAttritionWinner(
            float ownStrength, float ownQuality, float enemyStrength, float enemyQuality) =>
            WarOfAttritionWinner(ownStrength, ownQuality, enemyStrength, enemyQuality, AttritionExchangeParams.Default);

        // ---- 質と数の綱引き ----

        /// <summary>
        /// 質の優位と数の優位の綱引き（-1..1）。正＝自軍が総合優位（質寄り）／負＝敵が総合優位（数寄り）。
        /// それぞれ -1..1 の優位度を受け、相殺した綱引きを返す。
        /// </summary>
        public static float QualityVsQuantity(float qualityEdge, float quantityEdge)
        {
            float q = Mathf.Clamp(qualityEdge, -1f, 1f);
            float n = Mathf.Clamp(quantityEdge, -1f, 1f);
            return Mathf.Clamp(q + n, -1f, 1f);
        }

        // ---- 兵站（補充と損耗） ----

        /// <summary>
        /// 補充で支えられる損耗率（兵站#94連動の前提）。手持ち予備＋補充に対する補充の割合＝
        /// 補充が手厚いほど高い損耗まで支えられる。上限は Params。Params版。
        /// </summary>
        public static float SustainableLossRate(float reserves, float replenishment, AttritionExchangeParams p)
        {
            float res = Mathf.Max(reserves, 0f);
            float rep = Mathf.Max(replenishment, 0f);
            float baseAmt = res + rep;
            if (baseAmt <= Eps) return 0f;
            return Mathf.Clamp(rep / baseAmt, 0f, p.maxSustainableRate);
        }

        /// <summary>補充で支えられる損耗率（既定Params）。</summary>
        public static float SustainableLossRate(float reserves, float replenishment) =>
            SustainableLossRate(reserves, replenishment, AttritionExchangeParams.Default);

        /// <summary>損耗が補充を上回り消耗負けしているか（実損耗率 > 支持可能損耗率）。</summary>
        public static bool IsBleedingOut(float lossRate, float sustainableRate) =>
            Mathf.Max(lossRate, 0f) > Mathf.Max(sustainableRate, 0f);
    }
}
