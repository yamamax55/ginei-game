using UnityEngine;

namespace Ginei
{
    /// <summary>無国籍（無権利者の創出）の調整係数。</summary>
    public readonly struct StatelessnessParams
    {
        /// <summary>無権利状態の基本規模（国籍剥奪1.0×法的保護0のときの上限）。</summary>
        public readonly float rightlessnessScale;
        /// <summary>法の保護の空白の最大値（無権利状態を保護の空白へ写す）。</summary>
        public readonly float protectionVoidScale;
        /// <summary>迫害が国籍剥奪を進める速度（無国籍者の増加・per dt）。</summary>
        public readonly float denationalizationRate;
        /// <summary>虐待への脆弱性の最大値（無権利×加害者の不処罰）。</summary>
        public readonly float abuseScale;
        /// <summary>過激化の温床の最大値（無国籍者×絶望）。</summary>
        public readonly float radicalizationScale;
        /// <summary>再統合コストの基本規模（法外人口を法の内へ戻す費用）。</summary>
        public readonly float reintegrationCostScale;

        public StatelessnessParams(float rightlessnessScale, float protectionVoidScale,
            float denationalizationRate, float abuseScale, float radicalizationScale,
            float reintegrationCostScale)
        {
            this.rightlessnessScale = Mathf.Max(0f, rightlessnessScale);
            this.protectionVoidScale = Mathf.Max(0f, protectionVoidScale);
            this.denationalizationRate = Mathf.Max(0f, denationalizationRate);
            this.abuseScale = Mathf.Max(0f, abuseScale);
            this.radicalizationScale = Mathf.Max(0f, radicalizationScale);
            this.reintegrationCostScale = Mathf.Max(0f, reintegrationCostScale);
        }

        /// <summary>既定＝無権利1.0・保護空白1.0・剥奪進行0.1・虐待0.9・過激化0.7・再統合費1.2。</summary>
        public static StatelessnessParams Default =>
            new StatelessnessParams(1.0f, 1.0f, 0.1f, 0.9f, 0.7f, 1.2f);
    }

    /// <summary>
    /// 無権利者の創出の純ロジック（TOTL-5 #1526・アーレント『全体主義の起原』参考）。
    /// 国家が国籍を剥奪すると、人は「権利を持つ権利（the right to have rights）」そのものを失い、
    /// 法の保護の外に置かれた法外の人口クラス＝無国籍者が生まれる。彼らは誰にも守られず
    /// （保護の空白＝何をされても訴えられない）、虐待され放題になり、行き場のない絶望が過激化の
    /// 温床になる（全体主義の前段）。**「国籍剥奪は権利を持つ権利を奪い、法の保護の外に置かれた
    /// 無国籍者は虐待され放題で過激化の温床になる」を式に出す**。
    /// 市民権の段階（<see cref="CitizenshipRules"/>＝二級市民の不満・法的地位の付与）／戦火の難民
    /// （<see cref="RefugeeRules"/>＝人の移動）とは別系統＝こちらは権利を持つ権利の剥奪（法外人口の創出）。
    /// 過激化は <see cref="SuperfluousnessRules"/>（余剰性・同EPIC TOTL＝不要とされた人口）へ接続し、
    /// 全体主義の機構（<see cref="TotalitarianRules"/>）の温床を成す。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class StatelessnessRules
    {
        /// <summary>
        /// 無権利状態（0..rightlessnessScale）＝国籍剥奪 citizenshipStripped(0..1) が進み、
        /// 法的保護 legalProtection(0..1) が薄いほど高い＝権利を持つ権利の喪失度。
        /// 剥奪×(1−保護)×規模＝剥奪されても保護が残れば緩むが、両方欠けると無権利に堕ちる。
        /// </summary>
        public static float RightlessnessLevel(float citizenshipStripped, float legalProtection, StatelessnessParams p)
        {
            float stripped = Mathf.Clamp01(citizenshipStripped);
            float unprotected = 1f - Mathf.Clamp01(legalProtection);
            return stripped * unprotected * p.rightlessnessScale;
        }

        public static float RightlessnessLevel(float citizenshipStripped, float legalProtection)
            => RightlessnessLevel(citizenshipStripped, legalProtection, StatelessnessParams.Default);

        /// <summary>
        /// 法外に置かれた人口の規模（0..1）＝国籍を剥がされた人口比 strippedShare(0..1)×総人口 totalPopulation(0..1)。
        /// 法の保護の外に立つ無国籍者の量。
        /// </summary>
        public static float StatelessPopulation(float strippedShare, float totalPopulation)
        {
            return Mathf.Clamp01(strippedShare) * Mathf.Clamp01(totalPopulation);
        }

        /// <summary>
        /// 法の保護の空白（0..protectionVoidScale）＝無権利状態 rightlessnessLevel(0..1) に比例。
        /// 誰も守らない＝何をされても訴えられない領域の広さ。
        /// </summary>
        public static float ProtectionVoid(float rightlessnessLevel, StatelessnessParams p)
        {
            return Mathf.Clamp01(rightlessnessLevel) * p.protectionVoidScale;
        }

        public static float ProtectionVoid(float rightlessnessLevel)
            => ProtectionVoid(rightlessnessLevel, StatelessnessParams.Default);

        /// <summary>
        /// 国籍剥奪の進行＝1tick後の無国籍者比（0..1）。迫害 persecution(0..1) が剥奪を進め、
        /// 無国籍者を増やす（全体主義の前段）。statelessShare + 迫害×剥奪速度×dt。
        /// </summary>
        public static float DenationalizationTick(float statelessShare, float persecution, float dt, StatelessnessParams p)
        {
            float grow = Mathf.Clamp01(persecution) * p.denationalizationRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(statelessShare) + grow);
        }

        public static float DenationalizationTick(float statelessShare, float persecution, float dt)
            => DenationalizationTick(statelessShare, persecution, dt, StatelessnessParams.Default);

        /// <summary>
        /// 虐待への脆弱性（0..abuseScale）＝無権利状態 rightlessnessLevel(0..1)×加害者の不処罰
        /// oppressorImpunity(0..1)×規模。保護がないゆえ、無権利者は虐待され放題になる
        /// （訴える先がない者は守られない）。
        /// </summary>
        public static float VulnerabilityToAbuse(float rightlessnessLevel, float oppressorImpunity, StatelessnessParams p)
        {
            return Mathf.Clamp01(rightlessnessLevel) * Mathf.Clamp01(oppressorImpunity) * p.abuseScale;
        }

        public static float VulnerabilityToAbuse(float rightlessnessLevel, float oppressorImpunity)
            => VulnerabilityToAbuse(rightlessnessLevel, oppressorImpunity, StatelessnessParams.Default);

        /// <summary>
        /// 過激化の温床（0..radicalizationScale）＝無国籍者比 statelessShare(0..1)×絶望 despair(0..1)×規模。
        /// 行き場のない無国籍者が過激化・運動の温床になる（<see cref="SuperfluousnessRules"/> と接続）。
        /// </summary>
        public static float RadicalizationBreedingGround(float statelessShare, float despair, StatelessnessParams p)
        {
            return Mathf.Clamp01(statelessShare) * Mathf.Clamp01(despair) * p.radicalizationScale;
        }

        public static float RadicalizationBreedingGround(float statelessShare, float despair)
            => RadicalizationBreedingGround(statelessShare, despair, StatelessnessParams.Default);

        /// <summary>
        /// 無国籍者を法的に再統合するコスト（0..）＝無国籍者比 statelessShare(0..1)×規模÷受け入れ能力。
        /// 受け入れ能力 hostCapacity(0..1) が薄いほど費用は嵩む（能力0は最大コスト）。
        /// 法外人口を法の内へ戻す費用＝剥がすのは一瞬、戻すのは高い。
        /// </summary>
        public static float ReintegrationCost(float statelessShare, float hostCapacity, StatelessnessParams p)
        {
            float share = Mathf.Clamp01(statelessShare);
            float capacity = Mathf.Clamp01(hostCapacity);
            float denom = Mathf.Max(0.1f, capacity);
            return share * p.reintegrationCostScale / denom;
        }

        public static float ReintegrationCost(float statelessShare, float hostCapacity)
            => ReintegrationCost(statelessShare, hostCapacity, StatelessnessParams.Default);

        /// <summary>
        /// 権利を持つ権利の崩壊判定＝無権利状態 rightlessnessLevel(0..1) が閾値 threshold 以上。
        /// 法の保護が崩れ、法外人口クラスが固定化した状態（全体主義の温床が成立）。
        /// </summary>
        public static bool IsRightsCollapse(float rightlessnessLevel, float threshold)
        {
            return Mathf.Clamp01(rightlessnessLevel) >= threshold;
        }
    }
}
