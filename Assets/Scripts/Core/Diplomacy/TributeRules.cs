using UnityEngine;

namespace Ginei
{
    /// <summary>朝貢・貢納（従属勢力からの貢納関係）の調整係数。</summary>
    public readonly struct TributeParams
    {
        /// <summary>要求の効き＝宗主の要求が貢納額に乗る強さ（0..1）。</summary>
        public readonly float demandWeight;
        /// <summary>庇護の効き＝宗主の力×脅威が庇護価値になる係数（0..1）。</summary>
        public readonly float protectionWeight;
        /// <summary>負担の効き＝貢納額/富が反発に効く強さ（0..1）。</summary>
        public readonly float burdenWeight;
        /// <summary>威信の効き＝朝貢国数×貢納が宗主威信になる係数（0..1）。</summary>
        public readonly float prestigeWeight;

        public TributeParams(float demandWeight, float protectionWeight, float burdenWeight, float prestigeWeight)
        {
            this.demandWeight = Mathf.Clamp01(demandWeight);
            this.protectionWeight = Mathf.Clamp01(protectionWeight);
            this.burdenWeight = Mathf.Clamp01(burdenWeight);
            this.prestigeWeight = Mathf.Clamp01(prestigeWeight);
        }

        /// <summary>既定＝要求0.6・庇護0.7・負担0.8・威信0.5。</summary>
        public static TributeParams Default => new TributeParams(0.6f, 0.7f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 朝貢・貢納（従属勢力からの貢納関係＝みかじめ）の純ロジック。従属国の富と宗主の要求と交渉力で貢納額が決まり、
    /// 宗主は見返りに庇護（外的脅威があるほど価値が上がる）を与える。貢納は従属国に負担をかけ、見返り（庇護）に
    /// 見合わなければ反発が募る。宗主は朝貢国の数と貢納で威信を得るが、宗主が弱体化し従属国が力を蓄えれば貢納拒否
    /// （離反）のリスクが高まる。庇護−反発−離反で貢納関係の安定が決まり、安定が閾値を割れば朝貢は破れる。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TributeRules
    {
        /// <summary>
        /// 貢納額（0以上）。従属国の富 × (宗主の要求×demandWeight) ÷ (1+交渉力)。
        /// 交渉力が高いほど値切れる＝貢納額は下がる。
        /// </summary>
        public static float TributeAmount(float vassalWealth, float overlordDemand, float vassalBargaining, TributeParams p)
        {
            float wealth = Mathf.Max(0f, vassalWealth);
            float demand = Mathf.Clamp01(overlordDemand);
            float bargain = Mathf.Clamp01(vassalBargaining);
            return wealth * demand * p.demandWeight / (1f + bargain);
        }

        public static float TributeAmount(float vassalWealth, float overlordDemand, float vassalBargaining)
            => TributeAmount(vassalWealth, overlordDemand, vassalBargaining, TributeParams.Default);

        /// <summary>
        /// 庇護の価値（0..1）。宗主の力 × 外的脅威 × protectionWeight。
        /// 脅威があるほど庇護が要る＝従属の見返りが大きくなる。
        /// </summary>
        public static float ProtectionValue(float overlordPower, float externalThreat, TributeParams p)
        {
            float power = Mathf.Clamp01(overlordPower);
            float threat = Mathf.Clamp01(externalThreat);
            return Mathf.Clamp01(power * threat * p.protectionWeight);
        }

        public static float ProtectionValue(float overlordPower, float externalThreat)
            => ProtectionValue(overlordPower, externalThreat, TributeParams.Default);

        /// <summary>
        /// 従属国の負担（0..1）。貢納額 / 従属国の富。富がゼロなら（取るものが無く）負担0。
        /// </summary>
        public static float TributeBurden(float tributeAmount, float vassalWealth, TributeParams p)
        {
            float wealth = Mathf.Max(0f, vassalWealth);
            if (wealth <= 0f) return 0f;
            float amount = Mathf.Max(0f, tributeAmount);
            return Mathf.Clamp01(amount / wealth);
        }

        public static float TributeBurden(float tributeAmount, float vassalWealth)
            => TributeBurden(tributeAmount, vassalWealth, TributeParams.Default);

        /// <summary>
        /// 従属国の反発（0..1）。負担×burdenWeight − 庇護価値。
        /// 払う負担が見返り（庇護）に見合わないほど不満が募る。下限0。
        /// </summary>
        public static float VassalResentment(float tributeBurden, float protectionValue, TributeParams p)
        {
            float burden = Mathf.Clamp01(tributeBurden);
            float protection = Mathf.Clamp01(protectionValue);
            return Mathf.Clamp01(burden * p.burdenWeight - protection);
        }

        public static float VassalResentment(float tributeBurden, float protectionValue)
            => VassalResentment(tributeBurden, protectionValue, TributeParams.Default);

        /// <summary>
        /// 宗主の威信（0以上）。朝貢国数 × 貢納額 × prestigeWeight。
        /// 多くの国を従え多く貢がせるほど威信が高まる。
        /// </summary>
        public static float OverlordPrestige(int tributeCount, float tributeAmount, TributeParams p)
        {
            int count = Mathf.Max(0, tributeCount);
            float amount = Mathf.Max(0f, tributeAmount);
            return count * amount * p.prestigeWeight;
        }

        public static float OverlordPrestige(int tributeCount, float tributeAmount)
            => OverlordPrestige(tributeCount, tributeAmount, TributeParams.Default);

        /// <summary>
        /// 貢納拒否（離反）のリスク（0..1）。反発 × 従属国の力 × 宗主の弱体化。
        /// 不満を抱えた強い従属国は、宗主が弱れば貢納を拒む。
        /// </summary>
        public static float DefianceRisk(float vassalResentment, float vassalStrength, float overlordWeakness, TributeParams p)
        {
            float resent = Mathf.Clamp01(vassalResentment);
            float strength = Mathf.Clamp01(vassalStrength);
            float weakness = Mathf.Clamp01(overlordWeakness);
            return Mathf.Clamp01(resent * strength * weakness);
        }

        public static float DefianceRisk(float vassalResentment, float vassalStrength, float overlordWeakness)
            => DefianceRisk(vassalResentment, vassalStrength, overlordWeakness, TributeParams.Default);

        /// <summary>
        /// 貢納関係の安定（-1..1）。庇護価値 − 反発 − 離反リスク。
        /// 庇護が見返りとして効けば安定し、反発と離反が勝てば崩れる。
        /// </summary>
        public static float TributeRelationStability(float protectionValue, float vassalResentment, float defianceRisk, TributeParams p)
        {
            float protection = Mathf.Clamp01(protectionValue);
            float resent = Mathf.Clamp01(vassalResentment);
            float defiance = Mathf.Clamp01(defianceRisk);
            return Mathf.Clamp(protection - resent - defiance, -1f, 1f);
        }

        public static float TributeRelationStability(float protectionValue, float vassalResentment, float defianceRisk)
            => TributeRelationStability(protectionValue, vassalResentment, defianceRisk, TributeParams.Default);

        /// <summary>貢納関係が維持されるか＝安定が閾値以上なら朝貢は続く。</summary>
        public static bool IsTributeSustained(float tributeRelationStability, float threshold)
        {
            float stability = Mathf.Clamp(tributeRelationStability, -1f, 1f);
            return stability >= threshold;
        }

        /// <summary>既定閾値0＝安定が非負なら維持。</summary>
        public static bool IsTributeSustained(float tributeRelationStability)
            => IsTributeSustained(tributeRelationStability, 0f);
    }
}
