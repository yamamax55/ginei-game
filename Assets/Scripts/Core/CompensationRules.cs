using UnityEngine;

namespace Ginei
{
    /// <summary>報酬・賞罰の種別（功への賞／過への罰）。</summary>
    public enum RewardType
    {
        金銭,
        昇進,
        授爵,
        恩賞,
        叱責,
        降格,
        処罰,
    }

    /// <summary>信賞必罰の調整係数。</summary>
    public readonly struct CompensationParams
    {
        /// <summary>賞（プラス側）の士気・忠誠への基準効果。</summary>
        public readonly float rewardScale;
        /// <summary>罰（マイナス側）の士気・忠誠への基準効果（賞より重く効く＝損失回避）。</summary>
        public readonly float punishScale;
        /// <summary>不当（釣り合わぬ賞罰）が逆効果へ転じる度合い。</summary>
        public readonly float injusticePenalty;
        /// <summary>報酬価値が半減する累計授与数（乱発のインフレ半減期）。</summary>
        public readonly float inflationHalfLife;
        /// <summary>罰の見せしめ効果（可視性が他者を律する基準）。</summary>
        public readonly float deterrenceScale;

        public CompensationParams(float rewardScale, float punishScale, float injusticePenalty, float inflationHalfLife, float deterrenceScale)
        {
            this.rewardScale = Mathf.Max(0f, rewardScale);
            this.punishScale = Mathf.Max(0f, punishScale);
            this.injusticePenalty = Mathf.Max(0f, injusticePenalty);
            this.inflationHalfLife = Mathf.Max(1f, inflationHalfLife);
            this.deterrenceScale = Mathf.Max(0f, deterrenceScale);
        }

        /// <summary>既定＝賞0.3・罰0.4・不当ペナルティ1.5・インフレ半減数20・見せしめ0.25。</summary>
        public static CompensationParams Default => new CompensationParams(0.3f, 0.4f, 1.5f, 20f, 0.25f);
    }

    /// <summary>
    /// 報酬・授爵・賞罰の純ロジック（#996・信賞必罰）。功績への報酬（金銭・昇進・授爵・恩賞）と
    /// 過失への罰（叱責・降格・処罰）が士気と忠誠を動かす。**功に見合う賞は士気を上げ、過に見合う罰は
    /// 規律を生むが、釣り合わぬ賞罰（過大な賞・冤罪の罰）は逆に士気を削る**。
    /// 報酬の経済層を束ねる窓口：勲章の希少性インフレは <see cref="HonorsRules"/>（名誉の通貨）、
    /// 軍功爵位の実利身分は <see cref="MeritRankRules"/>（戦功→爵位）、ここが算出する士気/忠誠効果は
    /// <see cref="LoyaltyRules"/>（忠誠・寝返り）の loyalty へ、罰の規律側は <see cref="DisciplineRules"/>
    /// （軍紀・査問）と対をなす。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CompensationRules
    {
        /// <summary>賞（プラス効果）の種別か＝金銭/昇進/授爵/恩賞。</summary>
        public static bool IsReward(RewardType type)
        {
            return type == RewardType.金銭 || type == RewardType.昇進
                || type == RewardType.授爵 || type == RewardType.恩賞;
        }

        /// <summary>罰（マイナス効果）の種別か＝叱責/降格/処罰。</summary>
        public static bool IsPunishment(RewardType type) => !IsReward(type);

        /// <summary>
        /// 士気への効果（信賞必罰）。賞は magnitude×rewardScale だけ士気を上げ、罰は
        /// magnitude×punishScale だけ士気を下げるのが基準。ただし deservedness（功績との釣り合い）が
        /// 低いほど効果は反転＝**不当な賞は鼻白ませ士気を削り、冤罪の罰は士気をさらに大きく削る**。
        /// 釣り合い係数 (2×deservedness−1)∈[-1,1]：満点で満額の賞/正当な罰、0.5で無効、0で逆符号。
        /// </summary>
        public static float MoraleEffect(RewardType type, float magnitude, float deservedness)
        {
            return MoraleEffect(type, magnitude, deservedness, CompensationParams.Default);
        }

        public static float MoraleEffect(RewardType type, float magnitude, float deservedness, CompensationParams p)
        {
            float m = Mathf.Clamp01(magnitude);
            float d = Mathf.Clamp01(deservedness);
            // 釣り合い：1.0=完全に妥当→正の効果、0.0=完全に不当→逆効果。
            float justice = 2f * d - 1f;
            if (IsReward(type))
            {
                // 賞：妥当なら士気↑、不当（過大な賞）なら逆効果＝injusticePenalty で増幅して削る。
                float gain = m * p.rewardScale * justice;
                return justice >= 0f ? gain : gain * p.injusticePenalty;
            }
            // 罰：妥当な罰は規律として士気の毀損が小さい（justice≥0で軽い負）、
            // 冤罪（justice<0）は injusticePenalty で増幅した大きな負。
            float loss = -m * p.punishScale;
            return justice >= 0f ? loss * (1f - justice) : loss * (1f + (-justice) * p.injusticePenalty);
        }

        /// <summary>
        /// 忠誠への効果（<see cref="LoyaltyRules"/> の loyalty へ）。士気効果と同型だが、忠誠は賞に
        /// より敏感で罰に粘る（恩は忠誠を厚くし、正当な罰は忠誠を大きくは損なわない）。
        /// 賞は 1.0 倍、罰は 0.7 倍に減衰した士気効果を返す。
        /// </summary>
        public static float LoyaltyEffect(RewardType type, float magnitude, float deservedness)
        {
            return LoyaltyEffect(type, magnitude, deservedness, CompensationParams.Default);
        }

        public static float LoyaltyEffect(RewardType type, float magnitude, float deservedness, CompensationParams p)
        {
            float baseEffect = MoraleEffect(type, magnitude, deservedness, p);
            return IsReward(type) ? baseEffect : baseEffect * 0.7f;
        }

        /// <summary>
        /// 公平感（0..1）＝与えた報酬が功績に釣り合っているか。過小も過大も不満＝
        /// rewardGiven が rewardDeserved に一致すると 1.0、乖離が大きいほど下がる。
        /// 相対誤差 |given−deserved|/max(deserved,ε) を 1 から引く（クランプ）。
        /// </summary>
        public static float FairnessPerception(float rewardGiven, float rewardDeserved)
        {
            float given = Mathf.Max(0f, rewardGiven);
            float deserved = Mathf.Max(0f, rewardDeserved);
            float denom = Mathf.Max(deserved, 0.0001f);
            float relativeError = Mathf.Abs(given - deserved) / denom;
            return Mathf.Clamp01(1f - relativeError);
        }

        /// <summary>
        /// 報酬のインフレ（現在価値 0..1）＝乱発すると価値が下がる（<see cref="HonorsRules"/> と同型の
        /// 半減期カーブ）。rewardScarcity（希少性 0..1）が高いほどインフレに強い＝実効半減数を
        /// 引き伸ばす。累計報酬数 cumulativeRewards が実効半減数で価値半減（1/(1+n/half)）。
        /// </summary>
        public static float RewardInflation(int cumulativeRewards, float rewardScarcity)
        {
            return RewardInflation(cumulativeRewards, rewardScarcity, CompensationParams.Default);
        }

        public static float RewardInflation(int cumulativeRewards, float rewardScarcity, CompensationParams p)
        {
            float n = Mathf.Max(0, cumulativeRewards);
            float scarcity = Mathf.Clamp01(rewardScarcity);
            // 希少性が高いほど半減数を伸ばす（最大2倍＝価値が落ちにくい）。
            float effectiveHalf = p.inflationHalfLife * (1f + scarcity);
            return 1f / (1f + n / effectiveHalf);
        }

        /// <summary>
        /// 罰の抑止力（0..1）＝見せしめの処罰は他者を律する（信賞必罰の罰側）。
        /// 重い罰（severity）かつ可視（visibility）なほど抑止力が高い＝陰で罰しても誰も学ばない。
        /// 罰でない種別は抑止力0。基本式＝severity×visibility×deterrenceScale を 0..1 にクランプ。
        /// </summary>
        public static float PunishmentDeterrence(RewardType type, float severity, float visibility)
        {
            return PunishmentDeterrence(type, severity, visibility, CompensationParams.Default);
        }

        public static float PunishmentDeterrence(RewardType type, float severity, float visibility, CompensationParams p)
        {
            if (!IsPunishment(type)) return 0f;
            float s = Mathf.Clamp01(severity);
            float v = Mathf.Clamp01(visibility);
            // 処罰は降格・叱責より重い見せしめになる（種別の重み）。
            float typeWeight = type == RewardType.処罰 ? 1f : (type == RewardType.降格 ? 0.7f : 0.4f);
            return Mathf.Clamp01(s * v * typeWeight * (1f + p.deterrenceScale));
        }

        /// <summary>
        /// 信賞必罰の度合い（0..1）＝功には賞・過には罰が一貫して釣り合っているか（組織の規律の源泉）。
        /// reward（与えた賞罰の符号付き量）と merit（功績の符号付き量＝正=功・負=過）の符号と大きさが
        /// 一致するほど 1.0、ちぐはぐ（功に罰・過に賞・釣り合わぬ大小）ほど 0 に近づく。
        /// </summary>
        public static float MeritRewardBalance(float reward, float merit)
        {
            float r = Mathf.Clamp(reward, -1f, 1f);
            float m = Mathf.Clamp(merit, -1f, 1f);
            // 符号が逆（功に罰・過に賞）なら大きく減点。
            if (r * m < 0f)
            {
                return Mathf.Clamp01(1f - (Mathf.Abs(r) + Mathf.Abs(m)) * 0.5f);
            }
            // 同符号（無功無賞も含む）＝大小の釣り合いを見る。乖離が小さいほど一貫。
            float mismatch = Mathf.Abs(r - m);
            return Mathf.Clamp01(1f - mismatch);
        }
    }
}
