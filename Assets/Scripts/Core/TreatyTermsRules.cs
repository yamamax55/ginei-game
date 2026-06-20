using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 条約条項（講和条件）の純ロジック（外交EPIC #189 周辺・test-first）。
    /// 割譲・賠償・武装制限などの条件の<b>苛烈さ</b>(<see cref="TermsSeverity"/>)、敗者の<b>受容可能性</b>
    /// (<see cref="LoserAcceptability"/>)、過酷な条約が残す<b>遺恨</b>(<see cref="ResentmentSeeds"/>＝将来の再戦の種)、
    /// 勝者の<b>獲得</b>(<see cref="WinnerGains"/>)、履行の<b>強制力</b>(<see cref="EnforcementCapacity"/>)、条約の
    /// <b>安定性</b>(<see cref="TreatyStability"/>)と<b>改定圧力</b>(<see cref="RevisionPressure"/>)を扱う。
    /// 寛大な条約は安定し、苛烈な条約は遺恨を残して破棄圧力を生む（ヴェルサイユ型）。
    /// 細かい数値管理は持たず高位の決断（厳しく締めるか・手打ちにするか）と帰結に絞る（タイクン化回避）。
    /// すべて入力 clamp・決定論・実効値パターン。0..1 出力。
    /// </summary>
    public static class TreatyTermsRules
    {
        /// <summary>条約条項の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct TreatyTermsParams
        {
            public readonly float territorialWeight;   // 割譲が苛烈さへ寄与する重み
            public readonly float reparationsWeight;    // 賠償が苛烈さへ寄与する重み
            public readonly float armamentWeight;       // 武装制限が苛烈さへ寄与する重み
            public readonly float coercionWeight;       // 強制が受容可能性へ寄与する重み
            public readonly float humiliationWeight;    // 屈辱が遺恨へ寄与する重み
            public readonly float winnerPowerWeight;    // 勝者の力が強制力へ寄与する重み
            public readonly float monitoringWeight;     // 監視体制が強制力へ寄与する重み
            public readonly float resentmentWeight;     // 遺恨が安定性を削る重み

            public TreatyTermsParams(float territorialWeight, float reparationsWeight, float armamentWeight,
                float coercionWeight, float humiliationWeight, float winnerPowerWeight, float monitoringWeight,
                float resentmentWeight)
            {
                this.territorialWeight = Mathf.Max(0f, territorialWeight);
                this.reparationsWeight = Mathf.Max(0f, reparationsWeight);
                this.armamentWeight = Mathf.Max(0f, armamentWeight);
                this.coercionWeight = Mathf.Max(0f, coercionWeight);
                this.humiliationWeight = Mathf.Max(0f, humiliationWeight);
                this.winnerPowerWeight = Mathf.Max(0f, winnerPowerWeight);
                this.monitoringWeight = Mathf.Max(0f, monitoringWeight);
                this.resentmentWeight = Mathf.Max(0f, resentmentWeight);
            }

            /// <summary>既定＝割譲0.4/賠償0.3/武装0.3・強制0.5・屈辱0.6・勝者の力0.6/監視0.4・遺恨0.5。</summary>
            public static TreatyTermsParams Default => new TreatyTermsParams(
                0.4f, 0.3f, 0.3f,
                0.5f, 0.6f,
                0.6f, 0.4f,
                0.5f);
        }

        /// <summary>持続判定の既定閾値（安定が圧力をこれだけ上回れば持続）。</summary>
        public const float DefaultDurabilityThreshold = 0.5f;

        /// <summary>0..1 に丸める。</summary>
        public static float Clamp01(float v) => Mathf.Clamp01(v);

        /// <summary>
        /// 条項の苛烈さ（0..1）。割譲(territorialLoss 0..1)×割譲重み＋賠償(reparations 0..1)×賠償重み
        /// ＋武装制限(armamentLimits 0..1)×武装重みの加重平均。重み総和0なら0。
        /// </summary>
        public static float TermsSeverity(float territorialLoss, float reparations, float armamentLimits,
            TreatyTermsParams p)
        {
            float terr = Mathf.Clamp01(territorialLoss);
            float rep = Mathf.Clamp01(reparations);
            float arm = Mathf.Clamp01(armamentLimits);
            float weightSum = p.territorialWeight + p.reparationsWeight + p.armamentWeight;
            if (weightSum <= 0f) return 0f;
            float v = (terr * p.territorialWeight + rep * p.reparationsWeight + arm * p.armamentWeight) / weightSum;
            return Clamp01(v);
        }

        /// <summary>既定 Params で苛烈さ。</summary>
        public static float TermsSeverity(float territorialLoss, float reparations, float armamentLimits)
            => TermsSeverity(territorialLoss, reparations, armamentLimits, TreatyTermsParams.Default);

        /// <summary>
        /// 敗者の受容可能性（0..1）。苛烈でない（1-termsSeverity）ほど受け入れやすく、強制(coercionLevel 0..1)が
        /// 高いほど（呑まされて）受諾に傾く。寛大さと強制の加重平均。重み総和0なら寛大さのみ。
        /// </summary>
        public static float LoserAcceptability(float termsSeverity, float coercionLevel, TreatyTermsParams p)
        {
            float leniency = 1f - Mathf.Clamp01(termsSeverity);
            float coerce = Mathf.Clamp01(coercionLevel);
            float weightSum = 1f + p.coercionWeight; // 寛大さの重みは 1 固定
            float v = (leniency * 1f + coerce * p.coercionWeight) / weightSum;
            return Clamp01(v);
        }

        /// <summary>既定 Params で受容可能性。</summary>
        public static float LoserAcceptability(float termsSeverity, float coercionLevel)
            => LoserAcceptability(termsSeverity, coercionLevel, TreatyTermsParams.Default);

        /// <summary>
        /// 遺恨（将来の再戦の種 0..1）。苛烈さ(termsSeverity 0..1)×屈辱(nationalHumiliation 0..1)を核に、
        /// 屈辱重みで増幅。苛烈さが0なら遺恨なし＝0。
        /// </summary>
        public static float ResentmentSeeds(float termsSeverity, float nationalHumiliation, TreatyTermsParams p)
        {
            float sev = Mathf.Clamp01(termsSeverity);
            float hum = Mathf.Clamp01(nationalHumiliation);
            float v = sev * hum * (1f + p.humiliationWeight);
            return Clamp01(v);
        }

        /// <summary>既定 Params で遺恨。</summary>
        public static float ResentmentSeeds(float termsSeverity, float nationalHumiliation)
            => ResentmentSeeds(termsSeverity, nationalHumiliation, TreatyTermsParams.Default);

        /// <summary>
        /// 勝者の獲得（0..1）。割譲(territorialLoss 0..1)＋賠償(reparations 0..1)を、苛烈さと同じ重み比で加重平均。
        /// 敗者の苛烈さと表裏の関係（勝者にとっての取り分）。重み総和0なら0。
        /// </summary>
        public static float WinnerGains(float territorialLoss, float reparations, TreatyTermsParams p)
        {
            float terr = Mathf.Clamp01(territorialLoss);
            float rep = Mathf.Clamp01(reparations);
            float weightSum = p.territorialWeight + p.reparationsWeight;
            if (weightSum <= 0f) return 0f;
            float v = (terr * p.territorialWeight + rep * p.reparationsWeight) / weightSum;
            return Clamp01(v);
        }

        /// <summary>既定 Params で勝者の獲得。</summary>
        public static float WinnerGains(float territorialLoss, float reparations)
            => WinnerGains(territorialLoss, reparations, TreatyTermsParams.Default);

        /// <summary>
        /// 履行強制力（0..1）。勝者の力(winnerPower 0..1)×力重み＋監視体制(monitoringRegime 0..1)×監視重みの加重平均。
        /// 力があり監視が効くほど条約を守らせられる。重み総和0なら0。
        /// </summary>
        public static float EnforcementCapacity(float winnerPower, float monitoringRegime, TreatyTermsParams p)
        {
            float power = Mathf.Clamp01(winnerPower);
            float monitor = Mathf.Clamp01(monitoringRegime);
            float weightSum = p.winnerPowerWeight + p.monitoringWeight;
            if (weightSum <= 0f) return 0f;
            float v = (power * p.winnerPowerWeight + monitor * p.monitoringWeight) / weightSum;
            return Clamp01(v);
        }

        /// <summary>既定 Params で強制力。</summary>
        public static float EnforcementCapacity(float winnerPower, float monitoringRegime)
            => EnforcementCapacity(winnerPower, monitoringRegime, TreatyTermsParams.Default);

        /// <summary>
        /// 条約の安定性（0..1）。敗者の受容(loserAcceptability)と強制力(enforcementCapacity)の平均から、
        /// 遺恨(resentmentSeeds)×遺恨重みを差し引く。受容され強制でき遺恨が薄いほど安定。
        /// </summary>
        public static float TreatyStability(float loserAcceptability, float enforcementCapacity,
            float resentmentSeeds, TreatyTermsParams p)
        {
            float accept = Mathf.Clamp01(loserAcceptability);
            float enforce = Mathf.Clamp01(enforcementCapacity);
            float resent = Mathf.Clamp01(resentmentSeeds);
            float baseStability = (accept + enforce) * 0.5f;
            float v = baseStability - resent * p.resentmentWeight;
            return Clamp01(v);
        }

        /// <summary>既定 Params で安定性。</summary>
        public static float TreatyStability(float loserAcceptability, float enforcementCapacity,
            float resentmentSeeds)
            => TreatyStability(loserAcceptability, enforcementCapacity, resentmentSeeds, TreatyTermsParams.Default);

        /// <summary>
        /// 条約改定（破棄）の圧力（0..1）。遺恨(resentmentSeeds 0..1)×力の変化(shiftingPowerBalance 0..1＝
        /// 敗者側へ力が傾くほど大)。遺恨があり力関係が動くほど現状打破の圧力が高まる。
        /// </summary>
        public static float RevisionPressure(float resentmentSeeds, float shiftingPowerBalance, TreatyTermsParams p)
        {
            float resent = Mathf.Clamp01(resentmentSeeds);
            float shift = Mathf.Clamp01(shiftingPowerBalance);
            // p は将来の調整余地のため受けるが、現状は遺恨×力変化の積で表す。
            float v = resent * shift;
            return Clamp01(v);
        }

        /// <summary>既定 Params で改定圧力。</summary>
        public static float RevisionPressure(float resentmentSeeds, float shiftingPowerBalance)
            => RevisionPressure(resentmentSeeds, shiftingPowerBalance, TreatyTermsParams.Default);

        /// <summary>
        /// 条約が持続するか。安定性(treatyStability)が改定圧力(revisionPressure)を threshold ぶん上回れば true。
        /// </summary>
        public static bool IsTreatyDurable(float treatyStability, float revisionPressure, float threshold)
        {
            float stability = Mathf.Clamp01(treatyStability);
            float pressure = Mathf.Clamp01(revisionPressure);
            return stability - pressure >= threshold;
        }

        /// <summary>既定閾値で持続判定。</summary>
        public static bool IsTreatyDurable(float treatyStability, float revisionPressure)
            => IsTreatyDurable(treatyStability, revisionPressure, DefaultDurabilityThreshold);
    }
}
