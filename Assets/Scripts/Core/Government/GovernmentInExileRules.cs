using UnityEngine;

namespace Ginei
{
    /// <summary>亡命政権の調整係数。</summary>
    public readonly struct ExileParams
    {
        /// <summary>亡命直後に持ち出せる正統性の割合（落ち延びた時点で目減りする）。</summary>
        public readonly float carryOverRatio;
        /// <summary>残存正統性が時間で痩せる速度（per dt・忘れられていく）。</summary>
        public readonly float decayRate;
        /// <summary>承認国1つあたりの減衰緩和（国際承認が忘却を遅らせる）。</summary>
        public readonly float recognitionShield;
        /// <summary>「忘れられた」とみなす正統性の閾値（これ未満は帰還の旗にならない）。</summary>
        public readonly float forgottenThreshold;

        public ExileParams(float carryOverRatio, float decayRate, float recognitionShield, float forgottenThreshold)
        {
            this.carryOverRatio = Mathf.Clamp01(carryOverRatio);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.recognitionShield = Mathf.Clamp01(recognitionShield);
            this.forgottenThreshold = Mathf.Clamp01(forgottenThreshold);
        }

        /// <summary>既定＝持ち出し70%・減衰0.02・承認緩和0.2/国・忘却閾値0.15。</summary>
        public static ExileParams Default => new ExileParams(0.7f, 0.02f, 0.2f, 0.15f);
    }

    /// <summary>
    /// 亡命政権の純ロジック（ゴールデンバウム残党／合法政府の脱出型）。領土を失っても正統性の残滓は
    /// 持ち出せるが、時間とともに忘れられて痩せる＝亡命政権は時限の旗。国際承認が忘却を遅らせ、
    /// 残存正統性が占領地レジスタンスへの支援力と帰還の見込みを決める。本国の喪失感（故地の不安定）が
    /// 高いうちが勝負＝安定統治が完成すれば旗は立たない。版図の一体化（<see cref="LogisticsRules"/>）
    /// とは別系統＝領土なき正統性の管理。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GovernmentInExileRules
    {
        /// <summary>亡命時に持ち出せる正統性＝本国の正統性×持ち出し率（落ち延びた時点で目減り）。</summary>
        public static float InitialExileLegitimacy(float homelandLegitimacy, ExileParams p)
        {
            return Mathf.Clamp01(homelandLegitimacy) * p.carryOverRatio;
        }

        public static float InitialExileLegitimacy(float homelandLegitimacy)
            => InitialExileLegitimacy(homelandLegitimacy, ExileParams.Default);

        /// <summary>
        /// 残存正統性の1tick後の値（0..1）。減衰は承認国数 recognitions で緩む
        /// （実効減衰＝基礎×(1−緩和×承認数)、下限0＝十分な承認は忘却を止める）。
        /// </summary>
        public static float LegitimacyTick(float exileLegitimacy, int recognitions, float dt, ExileParams p)
        {
            float shield = Mathf.Clamp01(p.recognitionShield * Mathf.Max(0, recognitions));
            float effectiveDecay = p.decayRate * (1f - shield);
            return Mathf.Clamp01(Mathf.Clamp01(exileLegitimacy) - effectiveDecay * Mathf.Max(0f, dt));
        }

        public static float LegitimacyTick(float exileLegitimacy, int recognitions, float dt)
            => LegitimacyTick(exileLegitimacy, recognitions, dt, ExileParams.Default);

        /// <summary>
        /// 占領地レジスタンスへの支援力（0..1）＝残存正統性×故地の不満 homelandUnrest(0..1)。
        /// 旗が立っていても故地が満足していれば呼応する者はいない。
        /// </summary>
        public static float ResistanceSupport(float exileLegitimacy, float homelandUnrest)
        {
            return Mathf.Clamp01(exileLegitimacy) * Mathf.Clamp01(homelandUnrest);
        }

        /// <summary>忘れられたか＝残存正統性が閾値未満（もはや帰還の旗にならない）。</summary>
        public static bool IsForgotten(float exileLegitimacy, ExileParams p)
        {
            return Mathf.Clamp01(exileLegitimacy) < p.forgottenThreshold;
        }

        public static bool IsForgotten(float exileLegitimacy) => IsForgotten(exileLegitimacy, ExileParams.Default);

        /// <summary>
        /// 帰還が現実的か＝忘れられておらず、故地の不満が新支配の安定を上回っている
        /// （homelandUnrest &gt; occupierStability）。安定統治の完成が亡命政権の死。
        /// </summary>
        public static bool ReturnViable(float exileLegitimacy, float homelandUnrest, float occupierStability, ExileParams p)
        {
            if (IsForgotten(exileLegitimacy, p)) return false;
            return Mathf.Clamp01(homelandUnrest) > Mathf.Clamp01(occupierStability);
        }

        public static bool ReturnViable(float exileLegitimacy, float homelandUnrest, float occupierStability)
            => ReturnViable(exileLegitimacy, homelandUnrest, occupierStability, ExileParams.Default);
    }
}
