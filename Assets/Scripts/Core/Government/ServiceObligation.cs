using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 服役義務の社会契約（純データ・STSH-3 #2360）。「社会は個人を守り、個人は兵役で社会を守る」
    /// という明示的な交換を勢力ごとに表す＝徴募政策（<see cref="RecruitmentPolicy"/>）・必要服役年数・
    /// 免除率の三点。<see cref="ServiceObligationRules"/> がこれを読んで兵力供給（<see cref="FleetPool"/>）と
    /// 合意コスト（<see cref="ConsentRules"/>）の綱引きを算出する。乱数なし・直列化可。
    /// </summary>
    [System.Serializable]
    public class ServiceObligation
    {
        /// <summary>勢力（兵力プール・統治合意の帰属先）。</summary>
        public Faction factionId;

        /// <summary>徴募政策（強制の度合い＝供給↑×合意↓のトレードオフを決める軸）。既定＝自発。</summary>
        public RecruitmentPolicy policy = RecruitmentPolicy.自発;

        /// <summary>必要服役年数（兵役の重さ。長いほど供給は増え合意コストも増す）。負は0。</summary>
        public float serviceYearsRequired = 2f;

        /// <summary>免除率（0..1＝徴兵対象から外れる人口割合。高いほど供給は細るが合意コストは和らぐ）。</summary>
        public float exemptionRate = 0f;

        public ServiceObligation() { }

        public ServiceObligation(Faction factionId, RecruitmentPolicy policy,
            float serviceYearsRequired, float exemptionRate)
        {
            this.factionId = factionId;
            this.policy = policy;
            this.serviceYearsRequired = Mathf.Max(0f, serviceYearsRequired);
            this.exemptionRate = Mathf.Clamp01(exemptionRate);
        }
    }
}
