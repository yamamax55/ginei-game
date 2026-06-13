using UnityEngine;

namespace Ginei
{
    /// <summary>テュケー（運命・偶然）への制度的耐性の調整係数。</summary>
    public readonly struct TycheParams
    {
        /// <summary>制度の質が運命耐性へどれだけ効くか（質1で耐性がこの上限まで届く）。</summary>
        public readonly float resilienceFromQuality;
        /// <summary>運命耐性の下限（最低限の制度でも残る基礎耐性＝完全には脆くない）。</summary>
        public readonly float resilienceFloor;
        /// <summary>負のイベント（災厄）を運命耐性がどれだけ和らげるか（緩和の最大割合）。</summary>
        public readonly float misfortuneDamping;
        /// <summary>脆弱な制度が一度の不運で崩壊すると見なす運命耐性の既定閾値。</summary>
        public readonly float collapseThreshold;
        /// <summary>逆境を糧にできる制度の質の分岐点（これ以上で強化・未満で弱化）。</summary>
        public readonly float adversityPivot;
        /// <summary>逆境を糧にしたとき・逆に弱ったときの効果の最大幅。</summary>
        public readonly float adversityGain;
        /// <summary>運命の振れ幅の基準（穏やかな時代でもこれだけは偶然が残る・最小ボラティリティ）。</summary>
        public readonly float baseVolatility;
        /// <summary>時代の激動がどれだけ運命の振れを増幅するか。</summary>
        public readonly float turbulenceAmplification;
        /// <summary>打撃からの回復速度の基準（制度の質に比例して立ち直る・単位時間あたり）。</summary>
        public readonly float recoveryRate;
        /// <summary>運命に強い堅固な国家と見なす運命耐性の既定閾値。</summary>
        public readonly float resilientThreshold;

        public TycheParams(float resilienceFromQuality, float resilienceFloor, float misfortuneDamping,
                           float collapseThreshold, float adversityPivot, float adversityGain,
                           float baseVolatility, float turbulenceAmplification,
                           float recoveryRate, float resilientThreshold)
        {
            this.resilienceFromQuality = Mathf.Clamp01(resilienceFromQuality);
            this.resilienceFloor = Mathf.Clamp01(resilienceFloor);
            this.misfortuneDamping = Mathf.Clamp01(misfortuneDamping);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
            this.adversityPivot = Mathf.Clamp01(adversityPivot);
            this.adversityGain = Mathf.Clamp01(adversityGain);
            this.baseVolatility = Mathf.Clamp01(baseVolatility);
            this.turbulenceAmplification = Mathf.Max(0f, turbulenceAmplification);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.resilientThreshold = Mathf.Clamp01(resilientThreshold);
        }

        /// <summary>
        /// 既定＝質→耐性0.8・基礎耐性0.1・災厄緩和0.7・崩壊閾値0.3・逆境分岐0.5・逆境効果幅0.3・
        /// 基準振れ0.15・激動増幅0.7・回復速度0.5・堅固判定閾値0.6。
        /// </summary>
        public static TycheParams Default =>
            new TycheParams(0.8f, 0.1f, 0.7f, 0.3f, 0.5f, 0.3f, 0.15f, 0.7f, 0.5f, 0.6f);
    }

    /// <summary>
    /// テュケー（運命・偶然）への制度的耐性の純ロジック（POLY-3 #1448・ポリュビオス『歴史』）。
    /// 「テュケー（運命・偶然）＝歴史を動かす予測不能な力。だが優れた制度を持つ国家（ローマ）はテュケーの打撃を
    /// 吸収して立ち直り、脆弱な国家は一度の不運で崩れる＝制度の質が偶然の災厄への耐性を決める。堅固な制度は逆境を
    /// 糧にする」を式に出す。制度品質×Tyche係数がイベント効果を変調する。
    /// 分担：<see cref="VirtuFortunaRules"/>＝個人の力量と運命（マキャヴェッリ＝統治者の適応力が偶然をねじ伏せる）。
    /// 本クラスは別＝制度の質が偶然の打撃を吸収する（運命耐性＝制度の頑健性）。
    /// <see cref="EventRules"/>/<see cref="EventEngine"/>＝イベント効果の変調先（<see cref="EventEffectModulation"/> が
    /// 生のイベント効果係数を制度耐性で変調する窓口）／<see cref="TimeFlowRules"/>＝激動度の入力源（時代の趨勢）／
    /// <see cref="InstitutionalCorrectionRules"/>＝制度の自己修正（頑健性の出所・本クラスはその質を耐性へ写すのみ）。
    /// 全入力クランプ（magnitude は -1..1）・乱数なし決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TycheRules
    {
        /// <summary>
        /// 運命耐性（0..1）＝制度の質が高いほど運命の打撃への耐性が高い。堅固な制度はテュケーを吸収する。
        /// 耐性＝resilienceFloor..1 の範囲で制度品質に比例（基礎耐性＋質×伸び）。
        /// </summary>
        public static float FortuneResilience(float institutionalQuality, TycheParams p)
        {
            float q = Mathf.Clamp01(institutionalQuality);
            float span = Mathf.Clamp01(p.resilienceFromQuality);
            return Mathf.Clamp01(p.resilienceFloor + span * q);
        }

        public static float FortuneResilience(float institutionalQuality)
            => FortuneResilience(institutionalQuality, TycheParams.Default);

        /// <summary>
        /// イベント効果の変調（変調後の magnitude・おおむね -1..1）＝<see cref="EventEngine"/> への変調窓口。
        /// 制度耐性が負のイベント（災厄）を和らげ、正のイベント（好運）はそのまま活かす。
        /// 災厄：magnitude×(1−misfortuneDamping×fortuneResilience)＝耐性が高いほど打撃が軽い。
        /// 好運：magnitude のまま（制度は好機を削らない）。「優れた制度はテュケーの打撃を吸収する」。
        /// </summary>
        public static float EventEffectModulation(float rawEventMagnitude, float fortuneResilience, TycheParams p)
        {
            float m = Mathf.Clamp(rawEventMagnitude, -1f, 1f);
            float r = Mathf.Clamp01(fortuneResilience);
            if (m >= 0f) return m;                       // 好運はそのまま活かす
            float mitigation = p.misfortuneDamping * r;  // 災厄の緩和率（0..misfortuneDamping）
            return m * (1f - mitigation);                // 負の打撃を耐性ぶん和らげる
        }

        public static float EventEffectModulation(float rawEventMagnitude, float fortuneResilience)
            => EventEffectModulation(rawEventMagnitude, fortuneResilience, TycheParams.Default);

        /// <summary>
        /// 不運の衝撃を制度が吸収する量（0..shock）＝脆い国は吸収できず崩れる。
        /// 吸収量＝shock×misfortuneDamping×FortuneResilience(質)。残る打撃は shock−吸収量。
        /// </summary>
        public static float MisfortuneAbsorption(float shock, float institutionalQuality, TycheParams p)
        {
            float s = Mathf.Clamp01(shock);
            float r = FortuneResilience(institutionalQuality, p);
            return Mathf.Clamp(s * p.misfortuneDamping * r, 0f, s);
        }

        public static float MisfortuneAbsorption(float shock, float institutionalQuality)
            => MisfortuneAbsorption(shock, institutionalQuality, TycheParams.Default);

        /// <summary>
        /// 脆弱な制度が一度の不運で崩壊するリスク（0..1）。残存打撃（shock−吸収量）が運命耐性を上回るほど高い。
        /// 耐性が threshold 未満（脆弱）でなければリスクは生じない＝堅固な制度は一度の不運では崩れない。
        /// リスク＝Clamp01((残存打撃−耐性)/(1−耐性))。「脆弱な国家は一度の不運で崩れる」。
        /// </summary>
        public static float FragileStateCollapse(float institutionalQuality, float shock, float threshold, TycheParams p)
        {
            float r = FortuneResilience(institutionalQuality, p);
            float t = Mathf.Clamp01(threshold);
            if (r >= t) return 0f;                        // 堅固な制度は崩れない
            float s = Mathf.Clamp01(shock);
            float residual = s - MisfortuneAbsorption(s, institutionalQuality, p); // 吸収しきれない打撃
            if (residual <= r) return 0f;                 // 打撃が耐性内なら持ちこたえる
            float denom = Mathf.Max(0.0001f, 1f - r);
            return Mathf.Clamp01((residual - r) / denom);
        }

        public static float FragileStateCollapse(float institutionalQuality, float shock, float threshold)
            => FragileStateCollapse(institutionalQuality, shock, threshold, TycheParams.Default);

        public static float FragileStateCollapse(float institutionalQuality, float shock)
            => FragileStateCollapse(institutionalQuality, shock, TycheParams.Default.collapseThreshold, TycheParams.Default);

        /// <summary>
        /// 逆境を制度の質へ与える効果（-adversityGain..+adversityGain）＝堅固な制度は逆境を糧にして強くなり、
        /// 脆弱な制度は逆境で逆に弱る。質が adversityPivot を超える分だけ正（鍛えられる）・下回る分だけ負（蝕まれる）。
        /// 効果＝adversityGain×adversity×((質−pivot)/分岐幅)。「堅固な制度は逆境を糧にする」「危機が制度を鍛える」。
        /// </summary>
        public static float AdversityIntoStrength(float institutionalQuality, float adversity, TycheParams p)
        {
            float q = Mathf.Clamp01(institutionalQuality);
            float a = Mathf.Clamp01(adversity);
            // 質を pivot 中心の -1..1 へ写す（pivot で0・質1で正・質0で負）。
            float aboveRange = Mathf.Max(0.0001f, 1f - p.adversityPivot);
            float belowRange = Mathf.Max(0.0001f, p.adversityPivot);
            float signed = q >= p.adversityPivot
                ? (q - p.adversityPivot) / aboveRange
                : (q - p.adversityPivot) / belowRange;
            float effect = p.adversityGain * a * signed;
            return Mathf.Clamp(effect, -p.adversityGain, p.adversityGain);
        }

        public static float AdversityIntoStrength(float institutionalQuality, float adversity)
            => AdversityIntoStrength(institutionalQuality, adversity, TycheParams.Default);

        /// <summary>
        /// 運命の振れ幅（ボラティリティ・0..1）＝時代の激動度が運命の振れ幅を決める。
        /// 穏やかな時代（turbulence=0）なら baseVolatility まで縮み、激動（=1）なら増幅分が乗る。
        /// </summary>
        public static float TycheVolatility(float historicalTurbulence, TycheParams p)
        {
            float t = Mathf.Clamp01(historicalTurbulence);
            return Mathf.Clamp01(p.baseVolatility + p.turbulenceAmplification * t);
        }

        public static float TycheVolatility(float historicalTurbulence)
            => TycheVolatility(historicalTurbulence, TycheParams.Default);

        /// <summary>
        /// 打撃からの回復進捗（0..1・1で完全回復）＝制度の質が高いほど速く立ち直る。
        /// 進捗＝Clamp01(recoveryRate×制度品質×dt)。「優れた制度は打撃を吸収して立ち直る」。
        /// </summary>
        public static float RecoverySpeed(float institutionalQuality, float dt, TycheParams p)
        {
            float q = Mathf.Clamp01(institutionalQuality);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(p.recoveryRate * q * t);
        }

        public static float RecoverySpeed(float institutionalQuality, float dt)
            => RecoverySpeed(institutionalQuality, dt, TycheParams.Default);

        /// <summary>
        /// 運命に強い堅固な国家の判定。運命耐性が threshold 以上なら true＝テュケーの打撃を吸収できる国家。
        /// </summary>
        public static bool IsResilientToFortune(float fortuneResilience, float threshold)
        {
            float r = Mathf.Clamp01(fortuneResilience);
            float t = Mathf.Clamp01(threshold);
            return r >= t;
        }

        public static bool IsResilientToFortune(float fortuneResilience)
            => IsResilientToFortune(fortuneResilience, TycheParams.Default.resilientThreshold);
    }
}
