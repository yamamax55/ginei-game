using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 考第（こうだい）＝官僚の勤務評定の九等（史実：唐令を承けた日本の律令〔考課令〕の九等考第「上上〜下下」を参考）。
    /// 上上が最上・下下が最下。<see cref="MeritEvaluationRules"/> が能（実務）・徳（清廉）・績（勤続）から付ける。
    /// 中中を可もなく不可もない平均とし、上系で昇進・下系で降格の圧力がかかる。
    /// </summary>
    public enum MeritRating
    {
        上上, 上中, 上下,
        中上, 中中, 中下,
        下上, 下中, 下下
    }

    /// <summary>
    /// 官僚の考課記録（純データ・官僚制基盤）。一人の官吏が考課（勤務評定）を重ねた履歴を保持する。
    /// 単発の評定が <see cref="MeritRating"/>、その累積がここ＝昇進・降格・俸給は累積で決まる
    /// （史実：年次の小考を重ね、四年の大考で進退を定める）。状態は <see cref="MeritEvaluationRules"/> が更新する。
    /// 史実の宦官（去勢官）の登用経路は倫理的観点から本システムでは採用せず、宮廷腐敗・側近政治は
    /// <see cref="integrity"/>（清廉度）と勢力レベルの <see cref="Regime"/> 腐敗で表現する。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class OfficialMerit
    {
        public int personId;
        public int evaluations;        // 受けた考課の回数
        public float cumulativeScore;  // 考第スコアの累計（上上=9 … 下下=1）
        public int consecutiveTop;     // 連続して上系（上上/上中/上下）を取った回数
        public int consecutivePoor;    // 連続して下系（下上/下中/下下）を取った回数
        public float integrity = 0.7f; // 清廉度 0..1（徳。汚職で下がり、考第と昇進に効く）
        public MeritRating lastRating = MeritRating.中中; // 直近の考第

        public OfficialMerit() { }

        public OfficialMerit(int personId, float integrity = 0.7f)
        {
            this.personId = personId;
            this.integrity = Mathf.Clamp01(integrity);
        }

        /// <summary>考第スコアの平均（評定が無ければ 0）。上上=9 … 下下=1 の平均＝官歴の総合評価。</summary>
        public float AverageScore => evaluations > 0 ? cumulativeScore / evaluations : 0f;

        /// <summary>一度でも考課を受けたか。</summary>
        public bool HasRecord => evaluations > 0;
    }
}
