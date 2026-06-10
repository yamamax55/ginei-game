using UnityEngine;

namespace Ginei
{
    /// <summary>同盟の負担分担（集団防衛のただ乗り問題）の調整係数。</summary>
    public readonly struct BurdenSharingParams
    {
        /// <summary>盟主の公共財供給がただ乗り誘因へ効く重み（供給するほど小国は払わない＝集団行動の論理）。</summary>
        public readonly float freeRiderWeight;
        /// <summary>あるべき貢献における脅威の重み（脅威が高いほど応分負担を上げる）。</summary>
        public readonly float threatWeight;
        /// <summary>負担増の強制が主権感応度に乗って軋みを生む重み。</summary>
        public readonly float coercionWeight;

        public BurdenSharingParams(float freeRiderWeight, float threatWeight, float coercionWeight)
        {
            this.freeRiderWeight = Mathf.Clamp01(freeRiderWeight);
            this.threatWeight = Mathf.Clamp01(threatWeight);
            this.coercionWeight = Mathf.Clamp01(coercionWeight);
        }

        /// <summary>既定＝ただ乗り重み0.7・脅威重み0.5・強制重み0.8（盟主が供給するほど小国が払わず、強制は同盟を強く軋ませる）。</summary>
        public static BurdenSharingParams Default => new BurdenSharingParams(0.7f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 同盟の負担分担（集団防衛のただ乗り問題）の純ロジック＝平時の費用分担。
    /// 集団防衛＝公共財は供給されればただ乗りされる：盟主（大国）が背負うほど小国は払わず、
    /// 盟主は不足分を肩代わりし続けるか、応分負担を強制して同盟を軋ませるかの二択になる。
    /// 強制は応分の正論でも主権感応度の高い同盟国を傷つけ、結束を脆くする。
    /// <see cref="TreatyRules"/>（条約の締結・違反）・<see cref="LoyaltyRules"/>（会戦での静観・寝返り）とは別系統＝
    /// 平時の費用分担を扱う。同Wave並行の CollectiveSecurityRules（集団安全保障の発動・連鎖）とも分担し、
    /// ここは負担の配分と結束だけを扱う（平文言及のみ）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BurdenSharingRules
    {
        /// <summary>
        /// ただ乗りの誘因（0..1）＝盟主の公共財供給×freeRiderWeight×（1−自国の負担シェア）。
        /// 盟主が供給するほど・自国がまだ払っていないほど誘因が高い＝集団行動の論理。
        /// 既に応分を払っている国（allyShare→1）はただ乗りの余地が無い。
        /// </summary>
        public static float FreeRiderIncentive(float allyShare, float hegemonProvision, BurdenSharingParams p)
        {
            float share = Mathf.Clamp01(allyShare);
            float provision = Mathf.Clamp01(hegemonProvision);
            return Mathf.Clamp01(p.freeRiderWeight * provision * (1f - share));
        }

        public static float FreeRiderIncentive(float allyShare, float hegemonProvision)
            => FreeRiderIncentive(allyShare, hegemonProvision, BurdenSharingParams.Default);

        /// <summary>
        /// あるべき貢献（0..1）＝同盟国の国力に、脅威に応じた応分の上乗せをした水準。
        /// allyPower（力）に比例し、threatLevel が高いほど（threatWeight 経由で）求められる貢献が増える。
        /// 力ある国・脅威の高い局面ほど多く払うべき＝応分負担の規範値。
        /// </summary>
        public static float OptimalContribution(float allyPower, float threatLevel, BurdenSharingParams p)
        {
            float power = Mathf.Clamp01(allyPower);
            float threat = Mathf.Clamp01(threatLevel);
            return Mathf.Clamp01(power * (1f - p.threatWeight + p.threatWeight * threat));
        }

        public static float OptimalContribution(float allyPower, float threatLevel)
            => OptimalContribution(allyPower, threatLevel, BurdenSharingParams.Default);

        /// <summary>
        /// 実際の貢献（0..1）＝あるべき貢献を、ただ乗り誘因のぶんだけ目減りさせた値。
        /// 誘因が0なら規範どおり払い、1なら払わない＝公共財はただ乗りされる。
        /// </summary>
        public static float ActualContribution(float optimal, float freeRiderIncentive)
        {
            return Mathf.Clamp01(optimal) * (1f - Mathf.Clamp01(freeRiderIncentive));
        }

        /// <summary>
        /// 盟主の肩代わり（0以上）＝総所要のうち同盟国の実貢献合計で埋まらない不足分。
        /// 小国がただ乗りするほど不足が広がり、大国（盟主）が一手に背負う＝大国ほど割を食う。
        /// 充足していれば0（盟主の追加負担なし）。
        /// </summary>
        public static float HegemonOverpayment(float[] allyContributions, float totalNeed)
        {
            float need = Mathf.Max(0f, totalNeed);
            float sum = 0f;
            if (allyContributions != null)
            {
                for (int i = 0; i < allyContributions.Length; i++)
                {
                    sum += Mathf.Max(0f, allyContributions[i]);
                }
            }
            return Mathf.Max(0f, need - sum);
        }

        /// <summary>
        /// 負担増の強制が同盟へ与える軋み（0..1）＝要求した負担増×主権感応度×coercionWeight。
        /// 応分負担を正論で迫っても、主権意識の高い同盟国ほど関係が傷つく＝強制の代償。
        /// </summary>
        public static float CoercionStrain(float demandedIncrease, float allySovereigntySensitivity, BurdenSharingParams p)
        {
            float demand = Mathf.Clamp01(demandedIncrease);
            float sensitivity = Mathf.Clamp01(allySovereigntySensitivity);
            return Mathf.Clamp01(p.coercionWeight * demand * sensitivity);
        }

        public static float CoercionStrain(float demandedIncrease, float allySovereigntySensitivity)
            => CoercionStrain(demandedIncrease, allySovereigntySensitivity, BurdenSharingParams.Default);

        /// <summary>
        /// 同盟の結束（0..1）＝負担の公平感×（1−強制の軋み）。
        /// 負担が公平なら強く、応分を強制して軋ませれば脆い＝盟主は払い続けるか軋ませるかの二択。
        /// </summary>
        public static float AllianceCohesion(float burdenFairness, float coercionStrain)
        {
            return Mathf.Clamp01(burdenFairness) * (1f - Mathf.Clamp01(coercionStrain));
        }
    }
}
