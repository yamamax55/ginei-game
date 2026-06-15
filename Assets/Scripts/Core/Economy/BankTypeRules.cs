using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 銀行の業態プロファイル（#2010 BTYP）。規模・営業地域の広さ・組織形態・リレーションシップバンキング強度・典型顧客規模。
    /// </summary>
    public readonly struct BankTypeProfile
    {
        /// <summary>規模（典型的な資本/預金の相対倍率。都銀＝大）。</summary>
        public readonly float scaleFactor;
        /// <summary>営業地域の広さ（0..1。全国=1・狭域=小）＝分散度。</summary>
        public readonly float areaReach;
        /// <summary>協同組織か（信金＝true・株式会社＝false）。</summary>
        public readonly bool isCooperative;
        /// <summary>リレーションシップバンキングの強さ（0..1。地域密着ほど高い）。</summary>
        public readonly float relationshipStrength;
        /// <summary>典型顧客の規模（大企業=大・零細=小）。</summary>
        public readonly float targetCustomerScale;

        public BankTypeProfile(float scaleFactor, float areaReach, bool isCooperative,
            float relationshipStrength, float targetCustomerScale)
        {
            this.scaleFactor = scaleFactor;
            this.areaReach = Mathf.Clamp01(areaReach);
            this.isCooperative = isCooperative;
            this.relationshipStrength = Mathf.Clamp01(relationshipStrength);
            this.targetCustomerScale = targetCustomerScale;
        }
    }

    /// <summary>
    /// 銀行の業態ロジック（#2010 BTYP・純ロジック・業態プロファイルの唯一の窓口）。既存 <see cref="BankRules"/> のバランスシート
    /// 機構（資産/自己資本比率/信用乗数/収益/流動性）は業態非依存で不変、その上に業態の特性を additive に足す：業態プロファイル
    /// （BTYP-1）／顧客層の適合度（BTYP-2）／リレーションシップバンキングの貸倒れ低減（BTYP-3）／信用金庫の協同組織性（BTYP-4）／
    /// 地域経済連動（BTYP-5）。内政（#109）・企業（#1022）・危機（#1939）へ接続（read-only/接続のみ）。マクロ近似（3種の一表）。test-first。
    /// </summary>
    public static class BankTypeRules
    {
        /// <summary>リレーションシップバンキングがデフォルトリスクを下げられる最大割合。</summary>
        public const float RelationshipMaxReduction = 0.5f;

        // ===== BTYP-1 業態の定義 =====

        /// <summary>業態プロファイル（一表）。都銀＝大規模/全国/大企業、地銀＝中規模/地域/中小、信金＝小規模/狭域/零細/協同組織。</summary>
        public static BankTypeProfile ProfileFor(BankType type)
        {
            switch (type)
            {
                case BankType.地方銀行: return new BankTypeProfile(2f, 0.4f, false, 0.6f, 2f);
                case BankType.信用金庫: return new BankTypeProfile(0.5f, 0.1f, true, 0.9f, 0.5f);
                default:               return new BankTypeProfile(10f, 1.0f, false, 0.2f, 10f); // 都市銀行
            }
        }

        /// <summary>協同組織か（信用金庫＝会員制の非営利）。</summary>
        public static bool IsCooperative(BankType type) => ProfileFor(type).isCooperative;

        // ===== BTYP-2 顧客層と貸出 =====

        /// <summary>典型顧客の規模（プロファイルの targetCustomerScale）。</summary>
        public static float IdealCustomerScale(BankType type) => ProfileFor(type).targetCustomerScale;

        /// <summary>
        /// 顧客適合度（0..1）＝顧客規模と業態の典型規模の近さ＝min/max（一致で1.0、かけ離れると小）。
        /// 都銀は大企業に1.0で零細に低・信金は逆＝それぞれの得意分野を表す。
        /// </summary>
        public static float CustomerFitFactor(BankType type, float customerScale)
        {
            float ideal = ProfileFor(type).targetCustomerScale;
            float c = Mathf.Max(0f, customerScale);
            if (ideal <= 0f || c <= 0f) return 0f;
            float lo = Mathf.Min(ideal, c), hi = Mathf.Max(ideal, c);
            return lo / hi;
        }

        // ===== BTYP-3 リレーションシップバンキング =====

        /// <summary>リレバンによるデフォルトリスク低減係数＝1−リレバン強度×最大削減（地域密着ほど情報優位で低く）。</summary>
        public static float RelationshipDefaultFactor(BankType type, float maxReduction)
            => 1f - ProfileFor(type).relationshipStrength * Mathf.Clamp01(maxReduction);

        /// <summary>実効デフォルトリスク＝基準リスク×リレバン低減係数（地銀/信金は情報優位で貸倒れが低い）。</summary>
        public static float EffectiveDefaultRisk(float baseRisk, BankType type, float maxReduction)
            => Mathf.Max(0f, baseRisk) * RelationshipDefaultFactor(type, maxReduction);

        // ===== BTYP-4 信用金庫の協同組織性 =====

        /// <summary>会員への出資配当＝協同組織なら余剰×配当率（信金は利益を会員へ還元）、株式会社は0。</summary>
        public static float MemberDividend(float surplus, float payoutRatio, bool isCooperative)
            => isCooperative ? Mathf.Max(0f, surplus) * Mathf.Clamp01(payoutRatio) : 0f;

        /// <summary>営業地域が制限されるか（信用金庫＝法律で区域制限）。</summary>
        public static bool IsAreaRestricted(BankType type) => ProfileFor(type).isCooperative;

        /// <summary>区域外貸出が許されるか（信金は不可＝会員のための金融機関）。</summary>
        public static bool OutOfAreaLendingAllowed(BankType type) => !IsAreaRestricted(type);

        // ===== BTYP-5 地域経済との連動 =====

        /// <summary>分散度＝営業地域の広さ（都銀=1で全国分散・信金=狭く集中）。</summary>
        public static float DiversificationFactor(BankType type) => ProfileFor(type).areaReach;

        /// <summary>
        /// 地域不況のデフォルト増幅＝1＋(1−営業地域の広さ)×地域ストレス。営業地域が狭いほど地域経済（#109）と共倒れ、
        /// 都銀（areaReach=1）は全国分散で不感（=1.0）。
        /// </summary>
        public static float RegionalDefaultMultiplier(BankType type, float localEconomyStress)
            => 1f + (1f - ProfileFor(type).areaReach) * Mathf.Max(0f, localEconomyStress);
    }
}
