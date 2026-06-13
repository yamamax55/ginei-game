using UnityEngine;

namespace Ginei
{
    /// <summary>市民権の調整係数。</summary>
    public readonly struct CitizenshipParams
    {
        /// <summary>二級市民の不満の基本規模（排除人口比1.0・経過0年のときの値）。</summary>
        public readonly float grievanceScale;
        /// <summary>排除1年あたりの不満の複利率（世代を跨ぐほど恨みが膨らむ）。</summary>
        public readonly float grievanceCompoundRate;
        /// <summary>市民権拡大の統合効果の最大値（ローマの市民権モデル＝包摂は最強の同化）。</summary>
        public readonly float integrationScale;
        /// <summary>既得市民の反発の最大値（急拡大×特権の希釈）。</summary>
        public readonly float backlashScale;
        /// <summary>無権利層の体制外化リスクの最大値。</summary>
        public readonly float statelessRiskScale;

        public CitizenshipParams(float grievanceScale, float grievanceCompoundRate,
            float integrationScale, float backlashScale, float statelessRiskScale)
        {
            this.grievanceScale = Mathf.Max(0f, grievanceScale);
            this.grievanceCompoundRate = Mathf.Clamp(grievanceCompoundRate, 0f, 1f);
            this.integrationScale = Mathf.Max(0f, integrationScale);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.statelessRiskScale = Mathf.Max(0f, statelessRiskScale);
        }

        /// <summary>既定＝不満0.3・複利0.02/年・統合0.5・反発0.4・体制外化0.6。</summary>
        public static CitizenshipParams Default => new CitizenshipParams(0.3f, 0.02f, 0.5f, 0.4f, 0.6f);
    }

    /// <summary>
    /// 市民権の純ロジック（参政権・公職資格の範囲＝法的地位の付与）。市民権の拡大は統合を進め
    /// （ローマの市民権モデル＝包摂は最強の同化）、二級市民の存在は排除の長さで複利の恨みを残す。
    /// **「市民権は壁ではなく門にせよ」＝門を閉ざせば火種（不満・体制外化）、門を開けば統合、
    /// ただし開き方（速さ×既得特権）次第で旧市民が反発する＝門の開き方が国の形**。
    /// 民族の文化的同化（<see cref="CultureRules"/>＝アイデンティティの変化）とは別系統＝
    /// こちらは法が与える地位の力学。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CitizenshipRules
    {
        /// <summary>
        /// 参政権を持つ人口割合（0..1）＝市民権保有率 citizenShare の正規化窓口。
        /// 門の開き具合そのもの（1−これ＝二級市民・無権利層の割合）。
        /// </summary>
        public static float EnfranchisedShare(float citizenShare)
        {
            return Mathf.Clamp01(citizenShare);
        }

        /// <summary>
        /// 二級市民の不満（0..1）＝排除人口比 excludedShare(0..1)×基本規模×複利
        /// (1+複利率)^排除年数 exclusionDuration。排除が世代を跨ぐほど恨みは利息を生み、
        /// 上限1で飽和する（生まれながらの二級市民は生まれながらに憤る）。
        /// </summary>
        public static float SecondClassGrievance(float excludedShare, float exclusionDuration, CitizenshipParams p)
        {
            float compound = Mathf.Pow(1f + p.grievanceCompoundRate, Mathf.Max(0f, exclusionDuration));
            return Mathf.Clamp01(Mathf.Clamp01(excludedShare) * p.grievanceScale * compound);
        }

        public static float SecondClassGrievance(float excludedShare, float exclusionDuration)
            => SecondClassGrievance(excludedShare, exclusionDuration, CitizenshipParams.Default);

        /// <summary>
        /// 市民権拡大の統合効果（0..integrationScale）＝新規に参政権を得た人口比
        /// newlyEnfranchised(0..1)×規模。権利を与えられた者は体制の当事者になる＝
        /// 包摂は征服より強い同化（ローマの市民権モデル）。
        /// </summary>
        public static float IntegrationGain(float newlyEnfranchised, CitizenshipParams p)
        {
            return Mathf.Clamp01(newlyEnfranchised) * p.integrationScale;
        }

        public static float IntegrationGain(float newlyEnfranchised)
            => IntegrationGain(newlyEnfranchised, CitizenshipParams.Default);

        /// <summary>
        /// 既得市民の反発（0..backlashScale）＝拡大速度 expansionSpeed(0..1)×既得特権の大きさ
        /// oldPrivilege(0..1)×規模。特権が大きいほど希釈の痛みは強く、急に開くほど反発する＝
        /// 門は徐々に開けよ（速度0なら反発なし）。
        /// </summary>
        public static float OldCitizenBacklash(float expansionSpeed, float oldPrivilege, CitizenshipParams p)
        {
            return Mathf.Clamp01(expansionSpeed) * Mathf.Clamp01(oldPrivilege) * p.backlashScale;
        }

        public static float OldCitizenBacklash(float expansionSpeed, float oldPrivilege)
            => OldCitizenBacklash(expansionSpeed, oldPrivilege, CitizenshipParams.Default);

        /// <summary>
        /// 従軍による市民権取得の進捗（0..1）＝従軍年数 serviceYears／必要年数 threshold。
        /// 1.0 で取得（血の代価＝帝国補助軍モデル：戦って国の一員になる門）。
        /// threshold が0以下なら即時取得＝1。
        /// </summary>
        public static float MilitaryServicePath(float serviceYears, float threshold)
        {
            if (threshold <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Max(0f, serviceYears) / threshold);
        }

        /// <summary>
        /// 無権利層の体制外化リスク（0..statelessRiskScale）＝排除人口比の二乗×規模。
        /// 失うものがない者は怖いものもない＝小さな排除は管理できるが、
        /// 大きな無権利層は超線形に体制の外で結束する。
        /// </summary>
        public static float StatelessRisk(float excludedShare, CitizenshipParams p)
        {
            float x = Mathf.Clamp01(excludedShare);
            return x * x * p.statelessRiskScale;
        }

        public static float StatelessRisk(float excludedShare)
            => StatelessRisk(excludedShare, CitizenshipParams.Default);

        /// <summary>
        /// 門の開き方の純効果＝統合効果−既得反発−二級市民の不満−体制外化リスク。
        /// 正なら開門が引き合う：閉ざせば（newly=0・excluded大）負に沈み、
        /// 開けば（excluded→0）統合だけが残る＝壁ではなく門にせよ、を式で出す。
        /// </summary>
        public static float NetOpennessEffect(float newlyEnfranchised, float expansionSpeed, float oldPrivilege,
            float excludedShare, float exclusionDuration, CitizenshipParams p)
        {
            return IntegrationGain(newlyEnfranchised, p)
                 - OldCitizenBacklash(expansionSpeed, oldPrivilege, p)
                 - SecondClassGrievance(excludedShare, exclusionDuration, p)
                 - StatelessRisk(excludedShare, p);
        }

        public static float NetOpennessEffect(float newlyEnfranchised, float expansionSpeed, float oldPrivilege,
            float excludedShare, float exclusionDuration)
            => NetOpennessEffect(newlyEnfranchised, expansionSpeed, oldPrivilege,
                excludedShare, exclusionDuration, CitizenshipParams.Default);
    }
}
