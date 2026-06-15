using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 出撃・仕掛けのタイミング（先手後手の有利不利）の純ロジック。
    /// 会戦では「いつ仕掛けるか」が勝敗を分ける＝早すぎる出撃は準備不足、遅すぎると好機を逃す。
    /// 敵の隙・自軍の準備・士気のピークが噛み合う瞬間に最大効果。
    ///
    /// 史実・思想の要点：
    /// ・出撃準備度＝準備（陣形・補給・展開）と士気のピークの噛み合い（どちらか欠けると崩れる）。
    /// ・好機＝敵の脆弱性×混乱（隙は時間で閉じる＝待ちすぎは損）。
    /// ・先手で敵の反応が遅れるほど主導権（initiative）が大きい。
    ///
    /// 分担（重複させない）：
    /// ・<see cref="FleetInBeingRules"/>（存在脅威で受動的に縛る）とは別＝こちらは<b>能動的に仕掛ける好機判定</b>。
    /// ・<see cref="MissionCommandRules"/>（任務に応じた兵力<b>動員</b>）とは別＝こちらは<b>戦術タイミング</b>に特化。
    /// 盤面非依存の plain 引数・乱数なし・Core 純ロジック・test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class SortieTimingRules
    {
        /// <summary>
        /// 出撃準備度（0..1）。準備と士気ピークの幾何平均的合成＝どちらか低いと準備度は伸びない。
        /// preparation/moralePeak はともに 0..1。
        /// </summary>
        public static float Readiness(float preparation, float moralePeak, SortieTimingParams p)
        {
            float prep = Mathf.Clamp01(preparation);
            float morale = Mathf.Clamp01(moralePeak);
            // 準備に重み・士気に重み（合計1）で線形合成しつつ、両方揃わないと伸びないよう積で底上げ。
            float weighted = prep * p.preparationWeight + morale * (1f - p.preparationWeight);
            float synergy = prep * morale; // 両方高いほど相乗
            return Mathf.Clamp01(Mathf.Lerp(weighted, synergy, p.synergyBlend));
        }

        /// <summary>Default 版。</summary>
        public static float Readiness(float preparation, float moralePeak)
            => Readiness(preparation, moralePeak, SortieTimingParams.Default);

        /// <summary>
        /// 好機（0..1）＝敵の脆弱性×混乱。隙は両方が揃ったとき最大（積）。
        /// enemyVulnerability/enemyDisorder はともに 0..1。
        /// </summary>
        public static float Opportunity(float enemyVulnerability, float enemyDisorder)
            => Mathf.Clamp01(enemyVulnerability) * Mathf.Clamp01(enemyDisorder);

        /// <summary>
        /// タイミングスコア（0..1）＝準備度と好機が噛み合うほど高い（積）。
        /// 準備だけでも好機だけでも低く、両者が同時に高いときに最大。
        /// </summary>
        public static float TimingScore(float readiness, float opportunity)
            => Mathf.Clamp01(readiness) * Mathf.Clamp01(opportunity);

        /// <summary>
        /// 準備不足の早撃ちペナルティ（0..1の損失率）。閾値 prematureThreshold 未満の準備度ほど損が増える。
        /// 準備が閾値以上なら 0（損なし）。
        /// </summary>
        public static float PrematureLoss(float readiness, SortieTimingParams p)
        {
            float r = Mathf.Clamp01(readiness);
            if (r >= p.prematureThreshold) return 0f;
            // 閾値からの不足分を 0..1 に正規化し、最大ペナルティへ線形に写す。
            float shortfall = (p.prematureThreshold - r) / Mathf.Max(1e-4f, p.prematureThreshold);
            return Mathf.Clamp01(shortfall * p.maxPrematurePenalty);
        }

        /// <summary>Default 版。</summary>
        public static float PrematureLoss(float readiness)
            => PrematureLoss(readiness, SortieTimingParams.Default);

        /// <summary>
        /// 好機を逃した損失（0..1）＝隙は時間で閉じる。opportunityDecay は好機が閉じた度合い 0..1。
        /// そのまま損失率として返す（隙が完全に閉じたら最大損）。
        /// </summary>
        public static float MissedWindowLoss(float opportunityDecay)
            => Mathf.Clamp01(opportunityDecay);

        /// <summary>
        /// 先手の主導権ボーナス（>=0）。タイミングスコアが高く、敵の反応が遅れるほど大きい。
        /// enemyReactionDelay は 0..1（敵の反応の遅さ）。
        /// </summary>
        public static float InitiativeBonus(float timingScore, float enemyReactionDelay, SortieTimingParams p)
        {
            float ts = Mathf.Clamp01(timingScore);
            float delay = Mathf.Clamp01(enemyReactionDelay);
            return ts * delay * p.maxInitiativeBonus;
        }

        /// <summary>Default 版。</summary>
        public static float InitiativeBonus(float timingScore, float enemyReactionDelay)
            => InitiativeBonus(timingScore, enemyReactionDelay, SortieTimingParams.Default);

        /// <summary>
        /// あと何待てば最適か（>0 待つべき秒数相当／&lt;0 今が好機＝もう動くべき）。
        /// 現在準備度が目標に足りなければ「準備が整うまで待つ」、整っているのに好機の見込みが薄ければ「逃す前に今動く」。
        /// currentReadiness/expectedOpportunity はともに 0..1。返り値は ±maxDelay にクランプ。
        /// </summary>
        public static float OptimalDelay(float currentReadiness, float expectedOpportunity, SortieTimingParams p)
        {
            float r = Mathf.Clamp01(currentReadiness);
            float opp = Mathf.Clamp01(expectedOpportunity);
            // 準備の不足分（正）＝待つほう／好機の見込み（高いほど今動く＝負へ）。
            float prepGap = p.optimalReadiness - r;     // >0 なら準備不足＝待ち
            float urgency = opp * p.opportunityUrgency; // 好機が大きいほど今動く圧力
            float delay = (prepGap * p.delayPerReadiness) - urgency;
            return Mathf.Clamp(delay, -p.maxDelay, p.maxDelay);
        }

        /// <summary>Default 版。</summary>
        public static float OptimalDelay(float currentReadiness, float expectedOpportunity)
            => OptimalDelay(currentReadiness, expectedOpportunity, SortieTimingParams.Default);

        /// <summary>出撃機が来たか（タイミングスコアが閾値以上）。</summary>
        public static bool IsSortieFavorable(float timingScore, float threshold)
            => Mathf.Clamp01(timingScore) >= threshold;

        /// <summary>Default 閾値版。</summary>
        public static bool IsSortieFavorable(float timingScore)
            => IsSortieFavorable(timingScore, SortieTimingParams.Default.favorableThreshold);
    }

    /// <summary>
    /// 出撃タイミングの調整値（実効値パターン・基準値非破壊）。ctor で全 Clamp。
    /// </summary>
    public readonly struct SortieTimingParams
    {
        /// <summary>準備度合成での準備の重み（残りが士気の重み）。0..1。</summary>
        public readonly float preparationWeight;
        /// <summary>準備度の線形合成→積（相乗）への寄せ具合。0..1。</summary>
        public readonly float synergyBlend;
        /// <summary>これ未満の準備度で早撃ちペナルティが生じる閾値。0..1。</summary>
        public readonly float prematureThreshold;
        /// <summary>早撃ちペナルティの上限（準備度0で被る最大損失率）。0..1。</summary>
        public readonly float maxPrematurePenalty;
        /// <summary>先手主導権ボーナスの上限。>=0。</summary>
        public readonly float maxInitiativeBonus;
        /// <summary>OptimalDelay の目標準備度。0..1。</summary>
        public readonly float optimalReadiness;
        /// <summary>準備不足1あたりの待ち秒数換算。>=0。</summary>
        public readonly float delayPerReadiness;
        /// <summary>好機が今動くべき圧力に変換される係数。>=0。</summary>
        public readonly float opportunityUrgency;
        /// <summary>OptimalDelay の絶対値クランプ上限（秒相当）。>=0。</summary>
        public readonly float maxDelay;
        /// <summary>出撃機の既定閾値（IsSortieFavorable）。0..1。</summary>
        public readonly float favorableThreshold;

        public SortieTimingParams(
            float preparationWeight, float synergyBlend,
            float prematureThreshold, float maxPrematurePenalty,
            float maxInitiativeBonus, float optimalReadiness,
            float delayPerReadiness, float opportunityUrgency,
            float maxDelay, float favorableThreshold)
        {
            this.preparationWeight = Mathf.Clamp01(preparationWeight);
            this.synergyBlend = Mathf.Clamp01(synergyBlend);
            this.prematureThreshold = Mathf.Clamp01(prematureThreshold);
            this.maxPrematurePenalty = Mathf.Clamp01(maxPrematurePenalty);
            this.maxInitiativeBonus = Mathf.Max(0f, maxInitiativeBonus);
            this.optimalReadiness = Mathf.Clamp01(optimalReadiness);
            this.delayPerReadiness = Mathf.Max(0f, delayPerReadiness);
            this.opportunityUrgency = Mathf.Max(0f, opportunityUrgency);
            this.maxDelay = Mathf.Max(0f, maxDelay);
            this.favorableThreshold = Mathf.Clamp01(favorableThreshold);
        }

        public static SortieTimingParams Default => new SortieTimingParams(
            preparationWeight: 0.5f,
            synergyBlend: 0.5f,
            prematureThreshold: 0.5f,
            maxPrematurePenalty: 0.6f,
            maxInitiativeBonus: 0.5f,
            optimalReadiness: 0.8f,
            delayPerReadiness: 10f,
            opportunityUrgency: 6f,
            maxDelay: 30f,
            favorableThreshold: 0.5f);
    }
}
