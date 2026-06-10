using UnityEngine;

namespace Ginei
{
    /// <summary>戦略的欺瞞の調整係数。</summary>
    public readonly struct DeceptionParams
    {
        /// <summary>敵の聡明さが信憑性を割り引く強さ（聡明な敵ほど偽情報を疑う）。</summary>
        public readonly float intelligenceShield;
        /// <summary>偽戦力誘導が敵を引き付ける最大割合。</summary>
        public readonly float maxMisdirection;
        /// <summary>矛盾証拠1単位が信憑性を崩す速度（per dt）。</summary>
        public readonly float consistencyDecayRate;
        /// <summary>露見が以後の偽情報全体を焼く強さ（信用の焼失＝逆効果の係数）。</summary>
        public readonly float backlashScale;

        public DeceptionParams(float intelligenceShield, float maxMisdirection, float consistencyDecayRate, float backlashScale)
        {
            this.intelligenceShield = Mathf.Clamp01(intelligenceShield);
            this.maxMisdirection = Mathf.Clamp01(maxMisdirection);
            this.consistencyDecayRate = Mathf.Max(0f, consistencyDecayRate);
            this.backlashScale = Mathf.Max(0f, backlashScale);
        }

        /// <summary>既定＝聡明さ防壁0.7・最大誘導50%・矛盾崩壊0.5/単位・露見逆効果1.5倍。</summary>
        public static DeceptionParams Default => new DeceptionParams(0.7f, 0.5f, 0.5f, 1.5f);
    }

    /// <summary>
    /// 戦略的欺瞞の純ロジック（孫子「兵者詭道也」＝戦は詭道・#1126）。偽情報や陽動で敵 AI の戦略認識を
    /// 歪め、戦力配置や進路の判断を誤らせる。信憑性は偽情報のもっともらしさと情報経路の信頼で決まり、
    /// 敵の聡明さ（情報力）が防壁になる＝賢い敵ほど嘘を見抜く。注いだバイアスは敵の戦力推定
    /// （<see cref="ReconRules"/> の EstimateStrength の roll バイアス）へ流し込む想定。偽情報は時間と観測で
    /// 崩れ（矛盾の蓄積）、防諜（<see cref="CounterIntelligenceRules"/> と対）に露見すると以後の偽情報が
    /// 一切信じられなくなる＝ばれた嘘は二度と使えない（信用の焼失）。
    /// 戦術の陽動（<see cref="FeintRules"/>）・世論向けの宣伝（<see cref="PropagandaRules"/>）とは別系統＝
    /// こちらは戦略 AI の認識操作。乱数は roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DeceptionRules
    {
        /// <summary>
        /// 偽情報の信憑性（0..1）＝もっともらしさ plausibility(0..1)×情報経路の信頼 channelTrust(0..1)
        /// ×（1−敵情報力 enemyIntelligence×聡明さ防壁）。もっともらしく信頼できる経路ほど信じられ、
        /// 敵が聡明なほど割り引かれる＝敵の聡明さが最後の防壁。
        /// </summary>
        public static float DeceptionCredibility(float plausibility, float channelTrust, float enemyIntelligence, DeceptionParams p)
        {
            float baseCred = Mathf.Clamp01(plausibility) * Mathf.Clamp01(channelTrust);
            float shield = 1f - Mathf.Clamp01(enemyIntelligence) * p.intelligenceShield;
            return Mathf.Clamp01(baseCred * shield);
        }

        public static float DeceptionCredibility(float plausibility, float channelTrust, float enemyIntelligence)
            => DeceptionCredibility(plausibility, channelTrust, enemyIntelligence, DeceptionParams.Default);

        /// <summary>
        /// 敵の認識に植え付ける誤差（実態とのズレ）＝信憑性×植え付ける誤差規模 magnitude。
        /// <see cref="ReconRules"/> の戦力推定 roll バイアスへ注入する想定（信じた分だけ実態から外れる）。
        /// </summary>
        public static float PerceptionBias(float credibility, float magnitude)
        {
            return Mathf.Clamp01(credibility) * Mathf.Max(0f, magnitude);
        }

        /// <summary>
        /// 偽目標へ敵戦力を誘導する効果（0..maxMisdirection）＝信憑性×囮の見かけ規模 decoyStrength(0..1)
        /// ×最大誘導率。信じられた囮ほど多くの敵を釣る＝本命を手薄にする。
        /// </summary>
        public static float MisdirectionEffect(float credibility, float decoyStrength, DeceptionParams p)
        {
            return Mathf.Clamp01(credibility) * Mathf.Clamp01(decoyStrength) * p.maxMisdirection;
        }

        public static float MisdirectionEffect(float credibility, float decoyStrength)
            => MisdirectionEffect(credibility, decoyStrength, DeceptionParams.Default);

        /// <summary>
        /// 矛盾の蓄積で崩れた1tick後の信憑性（0..1）＝矛盾証拠 contradictoryEvidence(0..1)×崩壊速度×dt で減衰。
        /// 偽情報は時間と観測で剥がれる＝矛盾が無ければ（0）持続するが、観測が積もるほど早く崩れる。
        /// </summary>
        public static float ConsistencyDecayTick(float credibility, float contradictoryEvidence, float dt, DeceptionParams p)
        {
            float decay = Mathf.Clamp01(contradictoryEvidence) * p.consistencyDecayRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(credibility) - decay);
        }

        public static float ConsistencyDecayTick(float credibility, float contradictoryEvidence, float dt)
            => ConsistencyDecayTick(credibility, contradictoryEvidence, dt, DeceptionParams.Default);

        /// <summary>
        /// 露見成功率（0..1）＝信憑性が低いほど・敵防諜 enemyCounterIntel(0..1) が強いほど露見しやすい
        /// ＝（1−信憑性）×防諜力。<see cref="CounterIntelligenceRules"/> の摘発と対。
        /// </summary>
        public static float ExposureChance(float credibility, float enemyCounterIntel)
        {
            return Mathf.Clamp01((1f - Mathf.Clamp01(credibility)) * Mathf.Clamp01(enemyCounterIntel));
        }

        /// <summary>露見判定（決定論）。roll∈[0,1) が露見成功率未満なら見破られた＝true。</summary>
        public static bool Exposure(float credibility, float enemyCounterIntel, float roll)
        {
            return roll < ExposureChance(credibility, enemyCounterIntel);
        }

        /// <summary>
        /// 露見した欺瞞の逆効果（信用の焼失）＝植え付けた誤差規模 magnitude×逆効果係数。
        /// ばれた嘘は以後の偽情報を一切信じさせない＝張った規模ぶんの信用を失う（詭道の代金）。
        /// 後続の <see cref="DeceptionCredibility"/> に対する将来ペナルティの大きさとして使う想定。
        /// </summary>
        public static float BacklashOnExposure(float magnitude, DeceptionParams p)
        {
            return Mathf.Max(0f, magnitude) * p.backlashScale;
        }

        public static float BacklashOnExposure(float magnitude)
            => BacklashOnExposure(magnitude, DeceptionParams.Default);
    }
}
