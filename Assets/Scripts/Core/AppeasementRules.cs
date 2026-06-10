using UnityEngine;

namespace Ginei
{
    /// <summary>宥和政策の調整係数。</summary>
    public readonly struct AppeasementParams
    {
        /// <summary>譲歩が現状維持国の満足（緊張低下）へ変換される係数。</summary>
        public readonly float satiationScale;
        /// <summary>譲歩が拡張主義国の食欲（次の要求の燃料）へ変換される係数。</summary>
        public readonly float appetiteScale;
        /// <summary>要求水準の学習速度（「譲る相手だ」と学んだ拡張主義者が要求を吊り上げる速さ／秒）。</summary>
        public readonly float demandLearnRate;
        /// <summary>見ている同盟国1国あたりの信用毀損の重み。</summary>
        public readonly float reputationCostPerAlly;
        /// <summary>性格の読み違い（過小評価）が代償へ転化する係数。</summary>
        public readonly float misjudgmentScale;

        public AppeasementParams(float satiationScale, float appetiteScale, float demandLearnRate, float reputationCostPerAlly, float misjudgmentScale)
        {
            this.satiationScale = Mathf.Max(0f, satiationScale);
            this.appetiteScale = Mathf.Max(0f, appetiteScale);
            this.demandLearnRate = Mathf.Max(0f, demandLearnRate);
            this.reputationCostPerAlly = Mathf.Max(0f, reputationCostPerAlly);
            this.misjudgmentScale = Mathf.Max(0f, misjudgmentScale);
        }

        /// <summary>既定＝満足係数1.0・食欲係数1.0・学習速度0.5・同盟国毀損0.1/国・読み違い係数1.0。</summary>
        public static AppeasementParams Default => new AppeasementParams(1f, 1f, 0.5f, 0.1f, 1f);
    }

    /// <summary>
    /// 宥和政策の純ロジック（ミュンヘンの教訓）。譲歩が平和を買うか侵略の食欲を育てるかは
    /// 「相手の性格」で決まる：現状維持国（revisionism低）への譲歩は満足を生み緊張を下げるが、
    /// 拡張主義国（revisionism高）への同じ譲歩は食欲を育て、次はもっと要求される
    /// （SatiationEffect と AppetiteGrowth の対比が核＝同じ行為が相手次第で逆に効く）。
    /// 罪は宥和そのものではなく相手を見誤ること＝現状維持と信じて拡張主義者に譲るのが最悪
    /// （MisjudgmentCost）。さらに同盟国が見ている前での譲歩は安全保障の信用を削る（ReputationCost）。
    /// DiplomacyRules（外交状態の遷移と opinion 修正子）とは別系統＝こちらは
    /// 「譲歩という行為を相手がどう学習するか」を解く。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AppeasementRules
    {
        /// <summary>
        /// 満足化効果（0..1）＝譲歩(0..1)×(1−拡張主義度(0..1))×満足係数。
        /// 現状維持国への譲歩は不満の種を取り除き緊張を下げる＝平和は買える。
        /// 拡張主義者には満足は生まれない（同じ譲歩は AppetiteGrowth へ流れる）。
        /// </summary>
        public static float SatiationEffect(float concession, float revisionism, AppeasementParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(concession) * (1f - Mathf.Clamp01(revisionism)) * p.satiationScale);
        }

        public static float SatiationEffect(float concession, float revisionism)
            => SatiationEffect(concession, revisionism, AppeasementParams.Default);

        /// <summary>
        /// 食欲成長（0..1）＝譲歩(0..1)×拡張主義度(0..1)×食欲係数。
        /// 拡張主義国への譲歩は「押せば取れる」という確信を育てる＝平和を買ったつもりが
        /// 次の侵略の頭金になる。現状維持国では育たない（SatiationEffect と鏡像）。
        /// </summary>
        public static float AppetiteGrowth(float concession, float revisionism, AppeasementParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(concession) * Mathf.Clamp01(revisionism) * p.appetiteScale);
        }

        public static float AppetiteGrowth(float concession, float revisionism)
            => AppetiteGrowth(concession, revisionism, AppeasementParams.Default);

        /// <summary>
        /// 要求水準の時間発展（0..1）＝現在の要求＋譲歩の累積(0..1)×拡張主義度(0..1)×学習速度×dt。
        /// 「譲る相手だ」と学んだ拡張主義者は次をもっと要求する＝要求は譲歩の履歴で増殖する。
        /// 現状維持国（revisionism=0）は何度譲られても要求を吊り上げない。
        /// </summary>
        public static float DemandTick(float demand, float concessionHistory, float revisionism, float dt, AppeasementParams p)
        {
            float growth = Mathf.Clamp01(concessionHistory) * Mathf.Clamp01(revisionism) * p.demandLearnRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(demand) + growth);
        }

        public static float DemandTick(float demand, float concessionHistory, float revisionism, float dt)
            => DemandTick(demand, concessionHistory, revisionism, dt, AppeasementParams.Default);

        /// <summary>
        /// 評判コスト（0..1）＝譲歩(0..1)×Min(見ている同盟国数×1国あたり毀損, 1)。
        /// 同盟国が見ている前での譲歩は「この国の安全保障の約束は破られる」という学習を
        /// 第三者に与え、securityGuarantee の信用を削る。誰も見ていなければ無料。
        /// </summary>
        public static float ReputationCost(float concession, int alliesWatching, AppeasementParams p)
        {
            float exposure = Mathf.Min(1f, Mathf.Max(0, alliesWatching) * p.reputationCostPerAlly);
            return Mathf.Clamp01(Mathf.Clamp01(concession) * exposure);
        }

        public static float ReputationCost(float concession, int alliesWatching)
            => ReputationCost(concession, alliesWatching, AppeasementParams.Default);

        /// <summary>
        /// 読み違いの代償（0..1）＝譲歩(0..1)×Max(0, 真の拡張主義度−信じた拡張主義度)×読み違い係数。
        /// ミュンヘンの教訓＝罪は宥和そのものでなく相手の見誤り：性格を正しく読んでいれば
        /// 譲歩しても代償ゼロ（過大評価＝警戒しすぎも破滅にはならない）。最悪は
        /// 「現状維持と信じて（believed=0）本物の拡張主義者（true=1）に全面譲歩」＝代償最大。
        /// </summary>
        public static float MisjudgmentCost(float concession, float trueRevisionism, float believedRevisionism, AppeasementParams p)
        {
            float underestimate = Mathf.Max(0f, Mathf.Clamp01(trueRevisionism) - Mathf.Clamp01(believedRevisionism));
            return Mathf.Clamp01(Mathf.Clamp01(concession) * underestimate * p.misjudgmentScale);
        }

        public static float MisjudgmentCost(float concession, float trueRevisionism, float believedRevisionism)
            => MisjudgmentCost(concession, trueRevisionism, believedRevisionism, AppeasementParams.Default);
    }
}
