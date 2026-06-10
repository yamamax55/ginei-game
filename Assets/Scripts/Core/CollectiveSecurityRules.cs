using UnityEngine;

namespace Ginei
{
    /// <summary>集団安全保障の調整係数。</summary>
    public readonly struct CollectiveSecurityParams
    {
        /// <summary>制裁参加意欲に占める「自己負担の軽さ」の重み（損が小さいほど参加する）。</summary>
        public readonly float selfCostWeight;
        /// <summary>制裁参加意欲に占める「脅威の近さ」の重み（火事が近いほど参加する）。</summary>
        public readonly float proximityWeight;
        /// <summary>集団対応が実効力を持つために必要な最小平均参加度（足並み閾値）。これ未満は無力。</summary>
        public readonly float participationFloor;
        /// <summary>信頼性が見過ごし（対応失敗）で崩れる速さ。満州事変型＝一度の不作為で建前が死ぬ。</summary>
        public readonly float credibilityErosionRate;
        /// <summary>信頼性が成功実績で回復する速さ（崩壊より遅い＝信は築くに難く壊すに易い）。</summary>
        public readonly float credibilityRecoveryRate;
        /// <summary>体制崩壊と判定する信頼性の下限（これ以下＋大国離脱で建前が死ぬ）。</summary>
        public readonly float collapseCredibilityThreshold;

        public CollectiveSecurityParams(float selfCostWeight, float proximityWeight, float participationFloor,
            float credibilityErosionRate, float credibilityRecoveryRate, float collapseCredibilityThreshold)
        {
            this.selfCostWeight = Mathf.Clamp01(selfCostWeight);
            this.proximityWeight = Mathf.Clamp01(proximityWeight);
            this.participationFloor = Mathf.Clamp01(participationFloor);
            this.credibilityErosionRate = Mathf.Max(0f, credibilityErosionRate);
            this.credibilityRecoveryRate = Mathf.Max(0f, credibilityRecoveryRate);
            this.collapseCredibilityThreshold = Mathf.Clamp01(collapseCredibilityThreshold);
        }

        /// <summary>
        /// 既定＝自己負担重み0.6・脅威の近さ重み0.4・足並み閾値0.5・信頼性崩壊速度0.3・
        /// 回復速度0.05（崩壊の1/6＝建前は壊れやすく直しにくい）・崩壊閾値0.3。
        /// </summary>
        public static CollectiveSecurityParams Default =>
            new CollectiveSecurityParams(0.6f, 0.4f, 0.5f, 0.3f, 0.05f, 0.3f);
    }

    /// <summary>
    /// 集団安全保障の純ロジック（国際連盟型＝侵略者への全員制裁の建前）。中核は
    /// 「全員で守る約束は誰も守らない」＝個々の損得勘定が集団の建前を殺す失敗モデル：
    /// 各国の制裁参加意欲は自分の被る損（交易依存）と脅威の遠さで決まり、対岸の火事
    /// （遠くて高くつく制裁）には誰も加わらない。足並みが揃わなければ集団対応は無力となり、
    /// 一度の見過ごしが信頼性を崩し（満州事変型）、信頼性なき建前は侵略を抑止できない。
    /// 公共財ゆえに各自が抜けたがり（フリーライダー）、大国の離脱が連鎖して体制は死ぬ。
    /// DiplomacyRules（二国間の opinion／同盟・交戦）とは別系統＝こちらは多国間の約束の脆さを解く。
    /// BurdenSharingRules（同Wave＝負担の分担そのものの公平配分）とも別＝こちらは「分担を渋る
    /// 各国の離脱が集団の実効力をどう殺すか」を解く。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CollectiveSecurityRules
    {
        /// <summary>
        /// 制裁参加意欲（0..1）＝自己負担の軽さ(1−交易依存)×自己負担重み＋脅威の近さ(1−脅威の遠さ)×近さ重み。
        /// 自分が損し（依存が高い）、脅威が遠いほど参加しない＝対岸の火事。
        /// </summary>
        public static float SanctionParticipation(float memberTradeExposure, float aggressorDistance, CollectiveSecurityParams p)
        {
            float cheapness = 1f - Mathf.Clamp01(memberTradeExposure);
            float nearness = 1f - Mathf.Clamp01(aggressorDistance);
            return Mathf.Clamp01(cheapness * p.selfCostWeight + nearness * p.proximityWeight);
        }

        public static float SanctionParticipation(float memberTradeExposure, float aggressorDistance)
            => SanctionParticipation(memberTradeExposure, aggressorDistance, CollectiveSecurityParams.Default);

        /// <summary>
        /// 集団対応の実効力（0..1）＝平均参加度。ただし足並み閾値未満は0（揃わなければ無力＝
        /// 半端な参加は集団行動として成立しない）。null/空は0。
        /// </summary>
        public static float CollectiveResponse(float[] participations, CollectiveSecurityParams p)
        {
            if (participations == null || participations.Length == 0)
                return 0f;
            float sum = 0f;
            for (int i = 0; i < participations.Length; i++)
                sum += Mathf.Clamp01(participations[i]);
            float avg = sum / participations.Length;
            return avg < p.participationFloor ? 0f : avg;
        }

        public static float CollectiveResponse(float[] participations)
            => CollectiveResponse(participations, CollectiveSecurityParams.Default);

        /// <summary>
        /// 集団安保の信頼性更新（0..1）。過去の対応成功度(0..1)が高ければ回復、低ければ崩壊。
        /// 崩壊は回復より速い（一度の見過ごしで建前が崩れる＝満州事変型）。dtで時間追従。
        /// </summary>
        public static float CredibilityTick(float credibility, float pastResponseSuccess, float dt, CollectiveSecurityParams p)
        {
            float c = Mathf.Clamp01(credibility);
            float success = Mathf.Clamp01(pastResponseSuccess);
            float step = Mathf.Max(0f, dt);
            // 成功度0.5を分岐点に、上は回復・下は崩壊（崩壊の傾きが急）。
            float drive = success - 0.5f;
            float delta = drive >= 0f
                ? drive * 2f * p.credibilityRecoveryRate * step
                : drive * 2f * p.credibilityErosionRate * step;
            return Mathf.Clamp01(c + delta);
        }

        public static float CredibilityTick(float credibility, float pastResponseSuccess, float dt)
            => CredibilityTick(credibility, pastResponseSuccess, dt, CollectiveSecurityParams.Default);

        /// <summary>
        /// 侵略抑止力（0..1）＝信頼性×集団対応の実効力。掛け算＝建前（信頼性）だけでも
        /// 実力（足並み）だけでも抑止できない。どちらかゼロなら侵略は止まらない。
        /// </summary>
        public static float DeterrenceValue(float credibility, float collectiveResponse)
        {
            return Mathf.Clamp01(credibility) * Mathf.Clamp01(collectiveResponse);
        }

        /// <summary>
        /// 離脱の誘因（0..1）＝自分の制裁コスト(0..1)−自分が得る集団的便益(0..1)、負はクランプ。
        /// 公共財ゆえコスト＞便益なら抜けたがる（フリーライダー＝守らせて自分は払わない）。
        /// </summary>
        public static float FreeRiderDefection(float memberCost, float collectiveBenefit)
        {
            return Mathf.Clamp01(Mathf.Clamp01(memberCost) - Mathf.Clamp01(collectiveBenefit));
        }

        /// <summary>
        /// 体制の崩壊判定。信頼性が崩壊閾値以下、かつ大国の離脱が1件以上で true
        /// ＝建前が痩せたところに大国が抜けると連鎖して死ぬ（信頼性が健在なら離脱があっても持ちこたえる）。
        /// </summary>
        public static bool SystemCollapse(float credibility, int majorDefections, CollectiveSecurityParams p)
        {
            return Mathf.Clamp01(credibility) <= p.collapseCredibilityThreshold && Mathf.Max(0, majorDefections) >= 1;
        }

        public static bool SystemCollapse(float credibility, int majorDefections)
            => SystemCollapse(credibility, majorDefections, CollectiveSecurityParams.Default);
    }
}
