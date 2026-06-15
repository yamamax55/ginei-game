using UnityEngine;

namespace Ginei
{
    /// <summary>部族連合の亀裂（ガリア戦記型）の調整係数。</summary>
    public readonly struct CoalitionFaultlineParams
    {
        /// <summary>旧怨が亀裂に寄与する重み（過去の遺恨が連合を内側から割る）。</summary>
        public readonly float enmityWeight;
        /// <summary>利害対立が亀裂に寄与する重み（取り分・縄張りの食い違い）。</summary>
        public readonly float interestWeight;
        /// <summary>主導権争いが亀裂に寄与する重み（盟主の座を巡る角逐）。</summary>
        public readonly float rivalryWeight;
        /// <summary>亀裂が初期忠誠を蝕む強さ（亀裂の深い連合は出だしから忠誠が低い）。</summary>
        public readonly float loyaltyErosionWeight;
        /// <summary>共通の脅威が亀裂を抑えて結束させる重み（外敵が連合を固める）。</summary>
        public readonly float threatCohesionWeight;
        /// <summary>外圧が亀裂線に沿って連合を割る強さ（直接の圧力は亀裂を裂け目に変える）。</summary>
        public readonly float fracturePressureWeight;

        public CoalitionFaultlineParams(float enmityWeight, float interestWeight, float rivalryWeight,
            float loyaltyErosionWeight, float threatCohesionWeight, float fracturePressureWeight)
        {
            this.enmityWeight = Mathf.Clamp01(enmityWeight);
            this.interestWeight = Mathf.Clamp01(interestWeight);
            this.rivalryWeight = Mathf.Clamp01(rivalryWeight);
            this.loyaltyErosionWeight = Mathf.Clamp01(loyaltyErosionWeight);
            this.threatCohesionWeight = Mathf.Clamp01(threatCohesionWeight);
            this.fracturePressureWeight = Mathf.Clamp01(fracturePressureWeight);
        }

        /// <summary>既定＝旧怨0.4・利害0.35・主導権0.25（重み和=1の加重平均）・忠誠侵食0.6・脅威結束0.7・外圧分裂0.8
        /// （三要素を加重平均で亀裂スコアにし、亀裂が初期忠誠を下げ、共通の脅威が固め、直接の外圧が裂く）。</summary>
        public static CoalitionFaultlineParams Default
            => new CoalitionFaultlineParams(0.4f, 0.35f, 0.25f, 0.6f, 0.7f, 0.8f);
    }

    /// <summary>
    /// 部族連合の亀裂（ガリア戦記型・GAL-1 #1343）の純ロジック＝多部族の連合が内に抱える構造的な亀裂。
    /// 旧怨（過去の遺恨）・利害対立（取り分や縄張りの食い違い）・主導権争い（盟主の座を巡る角逐）が
    /// 連合内部に亀裂を刻み、その亀裂が連合への初期忠誠を蝕む＝カエサルはガリア諸部族の不和につけ込んだ。
    /// 共通の脅威（外敵）が強いほど亀裂を抑えて結束を保つ（外敵が連合を固める）が、亀裂線に沿った直接の
    /// 外圧（脅威でなく狙い撃ちの圧力）は逆に連合を割る。盟主部族の支配が強すぎると他部族の反感を招く。
    /// <see cref="AllianceDivergenceRules"/>（連合の戦後目標乖離＝勝利が近づくと内ゲバ・スペイン内戦型）
    /// とは別系統＝こちらは部族連合内の構造的亀裂と、それが決める初期忠誠を扱う（戦後でなく出だし）。
    /// <see cref="LoyaltyRules"/>（関ヶ原型＝戦う前の寝返りカスケード）とは別＝こちらは亀裂が初期忠誠そのものを
    /// 決める前段（DivideRules ＝分割操作の入力源）。盤面非依存の plain 引数・乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CoalitionFaultlineRules
    {
        /// <summary>
        /// 亀裂スコア（0..1）＝旧怨・利害対立・主導権争いの加重平均。三つの不和の源を重みで束ねて
        /// 連合内部の亀裂の深さを返す＝どれか一つでも高いと亀裂が生じ、全部高ければ深く割れている。
        /// 重み和が0なら0（退化）。
        /// </summary>
        public static float FaultlineScore(float historicalEnmity, float interestConflict,
            float leadershipRivalry, CoalitionFaultlineParams p)
        {
            float enmity = Mathf.Clamp01(historicalEnmity);
            float interest = Mathf.Clamp01(interestConflict);
            float rivalry = Mathf.Clamp01(leadershipRivalry);
            float wSum = p.enmityWeight + p.interestWeight + p.rivalryWeight;
            if (wSum <= 0f) return 0f;
            float weighted = enmity * p.enmityWeight + interest * p.interestWeight + rivalry * p.rivalryWeight;
            return Mathf.Clamp01(weighted / wSum);
        }

        public static float FaultlineScore(float historicalEnmity, float interestConflict, float leadershipRivalry)
            => FaultlineScore(historicalEnmity, interestConflict, leadershipRivalry, CoalitionFaultlineParams.Default);

        /// <summary>
        /// 初期忠誠の修正子（-1..0）＝亀裂が連合への出だしの忠誠を蝕む下げ幅。亀裂×loyaltyErosionWeight を
        /// 負で返す＝亀裂の深い連合は寄せ集めゆえ最初から忠誠が低い（カエサルが切り崩す余地）。
        /// </summary>
        public static float InitialLoyaltyModifier(float faultlineScore, CoalitionFaultlineParams p)
        {
            float fault = Mathf.Clamp01(faultlineScore);
            return Mathf.Clamp(-fault * p.loyaltyErosionWeight, -1f, 0f);
        }

        public static float InitialLoyaltyModifier(float faultlineScore)
            => InitialLoyaltyModifier(faultlineScore, CoalitionFaultlineParams.Default);

        /// <summary>
        /// 部族の結束（0..1）＝共通の脅威が亀裂を抑えて結束を保つ。亀裂のぶん基礎結束(1−亀裂)を下げつつ、
        /// 共通の脅威×threatCohesionWeight ぶんを上乗せして引き戻す＝外敵が強いほど内部の亀裂を呑み込んで
        /// 連合がまとまる（脅威が去れば亀裂が表に出る）。
        /// </summary>
        public static float TribalCohesion(float sharedThreat, float faultlineScore, CoalitionFaultlineParams p)
        {
            float threat = Mathf.Clamp01(sharedThreat);
            float fault = Mathf.Clamp01(faultlineScore);
            float baseCohesion = 1f - fault;
            // 共通の脅威が亀裂ぶんを引き戻す＝外敵の強さに応じて亀裂を打ち消す。
            float threatPull = threat * p.threatCohesionWeight * fault;
            return Mathf.Clamp01(baseCohesion + threatPull);
        }

        public static float TribalCohesion(float sharedThreat, float faultlineScore)
            => TribalCohesion(sharedThreat, faultlineScore, CoalitionFaultlineParams.Default);

        /// <summary>
        /// 離反のしやすさ（0..1）＝亀裂×部族の自立性。亀裂が深く、かつ各部族が自立的（中央への従属が弱い）
        /// ほど離反しやすい＝両者の積（亀裂があっても従属的な部族は留まり、自立的でも亀裂がなければ留まる）。
        /// </summary>
        public static float DefectionSusceptibility(float faultlineScore, float tribalAutonomy)
        {
            return Mathf.Clamp01(Mathf.Clamp01(faultlineScore) * Mathf.Clamp01(tribalAutonomy));
        }

        /// <summary>
        /// 盟主への反感（0..1）＝盟主部族の支配が強すぎると他部族の反感を招く。支配度の二乗で返す＝
        /// 緩やかな主導は許容されるが、突出した支配は不釣り合いに大きな反感を生む（非線形）。
        /// </summary>
        public static float HegemonResentment(float leadTribeDominance)
        {
            float d = Mathf.Clamp01(leadTribeDominance);
            return Mathf.Clamp01(d * d);
        }

        /// <summary>
        /// 外圧下の分裂（0..1）＝亀裂線に沿った直接の外圧が連合を割る。亀裂×外圧×fracturePressureWeight＝
        /// 共通の脅威（結束を固める）と違い、亀裂を狙い撃つ直接の圧力（離間・買収・各個撃破）は裂け目を
        /// 押し広げる＝両者の積（亀裂がなければ割れず、外圧がなければ割れない）。
        /// </summary>
        public static float FractureUnderPressure(float faultlineScore, float externalPressure, CoalitionFaultlineParams p)
        {
            float fault = Mathf.Clamp01(faultlineScore);
            float pressure = Mathf.Clamp01(externalPressure);
            return Mathf.Clamp01(fault * pressure * p.fracturePressureWeight);
        }

        public static float FractureUnderPressure(float faultlineScore, float externalPressure)
            => FractureUnderPressure(faultlineScore, externalPressure, CoalitionFaultlineParams.Default);

        /// <summary>
        /// 連合の頑健性（0..1）＝部族の結束×（共通の脅威で底上げ）。結束が高く、共通の脅威が連合を
        /// 結びつけているほど頑健＝結束 + (1−結束)×脅威 で、脅威が結束の隙間を埋める（外敵が結束の足りない
        /// 連合を支える）。
        /// </summary>
        public static float CoalitionResilience(float tribalCohesion, float sharedThreat)
        {
            float cohesion = Mathf.Clamp01(tribalCohesion);
            float threat = Mathf.Clamp01(sharedThreat);
            return Mathf.Clamp01(cohesion + (1f - cohesion) * threat);
        }

        /// <summary>
        /// 割れやすい連合か＝亀裂スコアが閾値を超えるか。閾値超の連合は離反・分裂工作に脆く、
        /// カエサルのような切り崩しの標的になる（亀裂が浅ければ堅い）。
        /// </summary>
        public static bool IsFracturedCoalition(float faultlineScore, float threshold)
        {
            return Mathf.Clamp01(faultlineScore) > Mathf.Clamp01(threshold);
        }
    }
}
