using UnityEngine;

namespace Ginei
{
    /// <summary>抑止の調整係数。</summary>
    public readonly struct DeterrenceParams
    {
        /// <summary>報復能力が満点(1.0)になる戦力比（自軍/相手）。これ未満は比例で割り引く。</summary>
        public readonly float forceRatioFullCapability;
        /// <summary>信憑性に占める「過去の実行実績」の重み。実績でしか買えない部分。</summary>
        public readonly float recordWeight;
        /// <summary>信憑性に占める「死活度」の重み（死活的利益ほど脅しは信じられる）。</summary>
        public readonly float stakesWeight;
        /// <summary>信憑性投資がコミットメントの罠（実行を強いられるリスク）へ転化する係数。</summary>
        public readonly float commitmentTrapScale;

        public DeterrenceParams(float forceRatioFullCapability, float recordWeight, float stakesWeight, float commitmentTrapScale)
        {
            this.forceRatioFullCapability = Mathf.Max(0.01f, forceRatioFullCapability);
            this.recordWeight = Mathf.Clamp01(recordWeight);
            this.stakesWeight = Mathf.Clamp01(stakesWeight);
            this.commitmentTrapScale = Mathf.Max(0f, commitmentTrapScale);
        }

        /// <summary>既定＝満点戦力比1.0・実績重み0.6・死活度重み0.4・罠係数0.5。</summary>
        public static DeterrenceParams Default => new DeterrenceParams(1f, 0.6f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 抑止の純ロジック（開戦判断への写像）。報復能力の顕示が相手に開戦を思いとどまらせる：
    /// 抑止力＝能力×信憑性の「積」であり、どちらかゼロなら無意味（撃ち返せない大艦隊も、
    /// 空脅しの前科がある国の恫喝も、相手の計算を変えない）。能力は戦力比×第二撃残存性
    /// （先制攻撃を吸収して撃ち返せるか）、信憑性は過去の実行実績と死活度でしか買えない。
    /// ただし信憑性への投資は退路を焼く＝本当に実行する羽目になるコミットメントの罠を伴う。
    /// 軍備蓄積の相互作用そのもの（ArmsRaceRules＝軍拡の螺旋）とは別系統＝こちらは
    /// 「持っている力が開戦の損得勘定をどう変えるか」を解く。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DeterrenceRules
    {
        /// <summary>
        /// 報復能力（0..1）＝Min(戦力比/満点比, 1)×第二撃残存性(0..1)。
        /// どれだけ戦力があっても先制で潰される（残存性0）なら報復は撃てない。
        /// </summary>
        public static float RetaliationCapability(float forceRatio, float secondStrikeSurvivability, DeterrenceParams p)
        {
            float force = Mathf.Clamp01(Mathf.Max(0f, forceRatio) / p.forceRatioFullCapability);
            return force * Mathf.Clamp01(secondStrikeSurvivability);
        }

        public static float RetaliationCapability(float forceRatio, float secondStrikeSurvivability)
            => RetaliationCapability(forceRatio, secondStrikeSurvivability, DeterrenceParams.Default);

        /// <summary>
        /// 信憑性（0..1）＝実行実績(0..1)×実績重み＋死活度(0..1)×死活度重み。
        /// 空脅しの前科は信用を殺し（実績0＝大半を失う）、死活的利益ほど「本当にやる」と信じられる。
        /// </summary>
        public static float Credibility(float pastFollowThrough, float stakes, DeterrenceParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(pastFollowThrough) * p.recordWeight +
                Mathf.Clamp01(stakes) * p.stakesWeight);
        }

        public static float Credibility(float pastFollowThrough, float stakes)
            => Credibility(pastFollowThrough, stakes, DeterrenceParams.Default);

        /// <summary>
        /// 抑止力（0..1）＝報復能力×信憑性。掛け算＝どちらかゼロなら抑止はゼロ
        /// （能力なき意志も、意志なき能力も、相手は恐れない）。
        /// </summary>
        public static float DeterrenceStrength(float capability, float credibility)
        {
            return Mathf.Clamp01(capability) * Mathf.Clamp01(credibility);
        }

        /// <summary>
        /// 開戦誘惑（-1..1）＝期待利得(0..1)−抑止力(0..1)。
        /// 抑止が利得を上回れば負＝攻める理由が消える。
        /// </summary>
        public static float AttackTemptation(float expectedGain, float deterrence)
        {
            return Mathf.Clamp01(expectedGain) - Mathf.Clamp01(deterrence);
        }

        /// <summary>抑止成立か＝開戦誘惑が閾値未満（閾値は-1..1にクランプ。既定0＝利得が抑止を超えない限り開戦しない）。</summary>
        public static bool IsDeterred(float temptation, float threshold)
        {
            return Mathf.Clamp(temptation, -1f, 1f) < Mathf.Clamp(threshold, -1f, 1f);
        }

        /// <summary>
        /// コミットメントの罠（0..1）＝信憑性への投資(0..1)×罠係数。
        /// 「必ず報復する」と縛るほど脅しは効くが、破られたとき本当に実行する羽目になる
        /// ＝退路を焼いた分だけ意図せぬ開戦リスクを抱える。
        /// </summary>
        public static float CommitmentTrap(float credibilityInvestment, DeterrenceParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(credibilityInvestment) * p.commitmentTrapScale);
        }

        public static float CommitmentTrap(float credibilityInvestment)
            => CommitmentTrap(credibilityInvestment, DeterrenceParams.Default);
    }
}
