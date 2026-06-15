using UnityEngine;

namespace Ginei
{
    /// <summary>恐怖と憎悪の調整係数（マキャヴェッリ型）。</summary>
    public readonly struct FearVsHatredParams
    {
        /// <summary>規律ある強制力が恐れに寄与する係数（理由の分かる罰は恐れを生む）。</summary>
        public readonly float fearWeight;
        /// <summary>恣意性・略奪が憎悪に寄与する係数（私財と名誉の略奪は憎悪を生む）。</summary>
        public readonly float hatredWeight;
        /// <summary>恐れが統治の安定に転じる係数（恐れられる君主は侮られない）。</summary>
        public readonly float controlGain;
        /// <summary>憎悪が転覆・暗殺リスクに転じる係数（非線形＝憎まれると一気に狙われる）。</summary>
        public readonly float subversionGain;
        /// <summary>恐怖が残虐さで憎悪へ転じる境界の閾値（これを超えると恐怖が憎悪になる）。</summary>
        public readonly float hatredCrossThreshold;

        public FearVsHatredParams(float fearWeight, float hatredWeight, float controlGain,
                                  float subversionGain, float hatredCrossThreshold)
        {
            this.fearWeight = Mathf.Max(0f, fearWeight);
            this.hatredWeight = Mathf.Max(0f, hatredWeight);
            this.controlGain = Mathf.Max(0f, controlGain);
            this.subversionGain = Mathf.Max(0f, subversionGain);
            this.hatredCrossThreshold = Mathf.Clamp01(hatredCrossThreshold);
        }

        /// <summary>既定＝恐れ係数1.0・憎悪係数1.0・安定寄与0.6・転覆寄与1.0・越境閾値0.5。</summary>
        public static FearVsHatredParams Default => new FearVsHatredParams(1f, 1f, 0.6f, 1f, 0.5f);
    }

    /// <summary>
    /// 恐怖と憎悪の純ロジック（#1140・マキャヴェッリ『君主論』＝「君主は愛されるより恐れられる方が
    /// 安全だが、憎まれてはならない」）。規律ある強制力（予測可能な厳格さ・理由の分かる罰）は恐れを
    /// 生み統治を安定させるが、残虐な抑圧（恣意的な処断・私財や名誉の略奪）は憎悪を生み転覆・暗殺を招く。
    /// 恐怖と憎悪の境界は非線形＝やりすぎれば恐れが憎悪へ転じる。核は <see cref="FearWithoutHatred"/>
    /// （恐れられるが憎まれない理想領域）と <see cref="CrossingIntoHatred"/>（残虐さでの越境）。
    /// テロの媒体増幅（<see cref="TerrorRules"/>）・戒厳令（<see cref="MartialLawRules"/>）とは別系統＝
    /// 「恐れられても憎まれるな」の境界。同 EPIC MKV の占領統治（ConquestGovernanceRules）とも別＝
    /// こちらは君主の強制力の質（規律ある恐怖 vs 残虐な憎悪）に絞る。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FearVsHatredRules
    {
        /// <summary>
        /// 恐れの水準（0..1）＝規律ある強制力 disciplinedForce×予測可能性 predictability×係数。
        /// 理由が分かり予測できる罰は恐れを生むが憎まれない（マキャヴェッリの賢明な強制力）。
        /// </summary>
        public static float FearLevel(float disciplinedForce, float predictability, FearVsHatredParams p)
        {
            float f = Mathf.Clamp01(disciplinedForce) * Mathf.Clamp01(predictability) * p.fearWeight;
            return Mathf.Clamp01(f);
        }

        public static float FearLevel(float disciplinedForce, float predictability)
            => FearLevel(disciplinedForce, predictability, FearVsHatredParams.Default);

        /// <summary>
        /// 憎悪の水準（0..1）＝恣意性 arbitrariness×（私財・名誉の）略奪 plunder×係数。
        /// マキャヴェッリ＝財産と女に手を出すな＝恣意的な略奪こそ憎悪の源泉。
        /// </summary>
        public static float HatredLevel(float arbitrariness, float plunder, FearVsHatredParams p)
        {
            float h = Mathf.Clamp01(arbitrariness) * Mathf.Clamp01(plunder) * p.hatredWeight;
            return Mathf.Clamp01(h);
        }

        public static float HatredLevel(float arbitrariness, float plunder)
            => HatredLevel(arbitrariness, plunder, FearVsHatredParams.Default);

        /// <summary>
        /// 恐れが統治の安定に寄与する量（0..1）＝恐れ水準×安定寄与係数。
        /// 恐れられる君主は侮られない（愛されるより恐れられる方が安全）。実効値。
        /// </summary>
        public static float ControlFromFear(float fearLevel, FearVsHatredParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(fearLevel) * p.controlGain);
        }

        public static float ControlFromFear(float fearLevel)
            => ControlFromFear(fearLevel, FearVsHatredParams.Default);

        /// <summary>
        /// 憎悪が生む転覆・暗殺リスク（0..1）＝憎悪水準を非線形（二乗）に増幅×係数。
        /// 憎まれる君主は狙われる＝低い憎悪は耐えられるが閾値を越えると一気に危険になる。
        /// </summary>
        public static float SubversionFromHatred(float hatredLevel, FearVsHatredParams p)
        {
            float h = Mathf.Clamp01(hatredLevel);
            return Mathf.Clamp01(h * h * p.subversionGain);
        }

        public static float SubversionFromHatred(float hatredLevel)
            => SubversionFromHatred(hatredLevel, FearVsHatredParams.Default);

        /// <summary>
        /// マキャヴェッリの理想（0..1）＝恐れられるが憎まれない領域＝規律ある強制力 disciplinedForce による
        /// 恐れから、恣意性 arbitrariness と略奪 plunder による憎悪を差し引く。規律はあるが略奪しない君主が
        /// 最大値を取る＝「恐れられるが憎まれない」。核となる指標。
        /// </summary>
        public static float FearWithoutHatred(float disciplinedForce, float arbitrariness, float plunder, FearVsHatredParams p)
        {
            float fear = FearLevel(disciplinedForce, 1f, p);
            float hatred = HatredLevel(arbitrariness, plunder, p);
            return Mathf.Clamp01(fear - hatred);
        }

        public static float FearWithoutHatred(float disciplinedForce, float arbitrariness, float plunder)
            => FearWithoutHatred(disciplinedForce, arbitrariness, plunder, FearVsHatredParams.Default);

        /// <summary>
        /// 恐怖が残虐さで憎悪へ転じる量（0..1・非線形の境界）。残虐さ brutality が閾値 threshold を
        /// 超えた超過分だけ、恐れ水準を憎悪へ流し込む＝やりすぎると恐怖が憎悪になる。閾値未満は転化なし
        /// （規律ある恐怖のまま）。threshold 省略時は Params の既定境界を使う。
        /// </summary>
        public static float CrossingIntoHatred(float fearLevel, float brutality, FearVsHatredParams p, float threshold = -1f)
        {
            float t = threshold < 0f ? p.hatredCrossThreshold : Mathf.Clamp01(threshold);
            float excess = Mathf.Max(0f, Mathf.Clamp01(brutality) - t);
            float span = Mathf.Max(0.0001f, 1f - t);
            float ratio = Mathf.Clamp01(excess / span); // 閾値超過の度合い（0..1）
            return Mathf.Clamp01(Mathf.Clamp01(fearLevel) * ratio);
        }

        public static float CrossingIntoHatred(float fearLevel, float brutality)
            => CrossingIntoHatred(fearLevel, brutality, FearVsHatredParams.Default);

        /// <summary>
        /// 純安全（-1..1）＝恐れの安定寄与 controlFromFear − 憎悪の転覆リスク subversionFromHatred。
        /// 正なら恐れが憎悪を上回り安全、負なら憎まれて危うい＝「恐れられても憎まれてはならない」の収支。
        /// </summary>
        public static float NetSecurity(float controlFromFear, float subversionFromHatred)
        {
            return Mathf.Clamp(Mathf.Clamp01(controlFromFear) - Mathf.Clamp01(subversionFromHatred), -1f, 1f);
        }

        /// <summary>憎まれて危険な状態か＝憎悪水準が閾値（既定0.5）を超えた。憎悪は転覆を招く一線。</summary>
        public static bool IsHated(float hatredLevel, float threshold = 0.5f)
        {
            return Mathf.Clamp01(hatredLevel) > Mathf.Clamp01(threshold);
        }
    }
}
