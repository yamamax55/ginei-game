using UnityEngine;

namespace Ginei
{
    /// <summary>擬制商品の種別（労働・土地・貨幣＝本来「売るために作られたもの」ではない）。</summary>
    public enum FictitiousCommodity
    {
        労働,
        土地,
        貨幣,
    }

    /// <summary>擬制商品ストレスの調整係数（POLA-3 #1596・ポランニー『大転換』の擬制商品）。</summary>
    public readonly struct FictitiousCommodityParams
    {
        /// <summary>労働の完全商品化が生むストレスの最大（人間の摩耗＝休息を奪われる）。</summary>
        public readonly float laborStressScale;
        /// <summary>土地の完全商品化が生むストレスの最大（自然・地域の破壊）。</summary>
        public readonly float landStressScale;
        /// <summary>貨幣の完全商品化が生むストレスの最大（金融不安定）。</summary>
        public readonly float moneyStressScale;
        /// <summary>商品化ストレス→社会の保護需要（二重運動）への感度。</summary>
        public readonly float protectionDemandSensitivity;
        /// <summary>商品化ストレスが社会の紐帯を解く速度（年あたり）。</summary>
        public readonly float unravelRate;
        /// <summary>脱商品化（保護）がストレスを和らげる強さ。</summary>
        public readonly float decommodificationScale;
        /// <summary>過剰商品化とみなす既定の商品化水準しきい値。</summary>
        public readonly float overcommodifyThreshold;

        public FictitiousCommodityParams(float laborStressScale, float landStressScale, float moneyStressScale,
                                         float protectionDemandSensitivity, float unravelRate,
                                         float decommodificationScale, float overcommodifyThreshold)
        {
            this.laborStressScale = Mathf.Max(0f, laborStressScale);
            this.landStressScale = Mathf.Max(0f, landStressScale);
            this.moneyStressScale = Mathf.Max(0f, moneyStressScale);
            this.protectionDemandSensitivity = Mathf.Max(0f, protectionDemandSensitivity);
            this.unravelRate = Mathf.Max(0f, unravelRate);
            this.decommodificationScale = Mathf.Clamp01(decommodificationScale);
            this.overcommodifyThreshold = Mathf.Clamp01(overcommodifyThreshold);
        }

        /// <summary>既定＝労働ストレス0.8・土地ストレス0.7・貨幣ストレス0.6・保護需要感度1.0・
        /// 紐帯崩し0.5・脱商品化緩和0.7・過剰商品化しきい値0.8。</summary>
        public static FictitiousCommodityParams Default =>
            new FictitiousCommodityParams(0.8f, 0.7f, 0.6f, 1.0f, 0.5f, 0.7f, 0.8f);
    }

    /// <summary>
    /// 擬制商品ストレスの純ロジック（POLA-3 #1596・ポランニー『大転換』の<b>擬制商品</b> fictitious commodity）。
    /// 労働・土地・貨幣は本来「売るために作られたもの」ではないのに市場で商品として扱われる＝
    /// その完全商品化が固有の社会ストレスを生む：労働を商品にすれば人間が摩耗し（休息が要る）、
    /// 土地を商品にすれば自然と地域が壊れ、貨幣を商品にすれば金融が不安定になる。
    /// 「商品化の度合いが高いほど人・自然・金融に固有のストレスが生まれ、それが社会の保護需要
    /// （二重運動）を呼ぶ」を式にする。財の需給そのものは <see cref="MarketRules"/>、土地という資産の
    /// 再分配（意欲と効率の交換）は <see cref="LandReformRules"/> が扱い、ここは擬制商品（労働/土地/貨幣）の
    /// 完全商品化が生む固有の制度リスクのみを扱う。生まれたストレス→保護の積み上がり＝二重運動の
    /// <b>保護側ラチェット</b>は <see cref="SocialProtectionRules"/>（同EPIC POLA・こちらが入力 marketPressure/dislocation の源）。
    /// 係数は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。全入力クランプ。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FictitiousCommodityRules
    {
        /// <summary>
        /// 種別ごとの社会ストレス（0..1）。商品化の度合い（commodificationLevel 0..1）に種別の感度を掛けて出す
        /// ＝労働（人間の摩耗）・土地（自然・地域の破壊）・貨幣（金融不安定）で固有の重みを持つ。
        /// 完全商品化（1.0）ほどストレスが大きく、非商品化（0）ならゼロ。
        /// </summary>
        public static float CommodificationStress(float commodificationLevel, FictitiousCommodity kind,
                                                  FictitiousCommodityParams p)
        {
            float level = Mathf.Clamp01(commodificationLevel);
            float scale = KindScale(kind, p);
            return Mathf.Clamp01(scale * level);
        }

        public static float CommodificationStress(float commodificationLevel, FictitiousCommodity kind)
            => CommodificationStress(commodificationLevel, kind, FictitiousCommodityParams.Default);

        /// <summary>
        /// 労働の完全商品化が人を摩耗させる量（0..1）。労働の商品化（laborCommodification 0..1）と
        /// 労働強度（workIntensity 0..1）の積に労働ストレス感度を掛ける＝労働力を売り物として
        /// 強く使うほど人間が摩耗する（人間は休息が要る＝機械ではない）。どちらかゼロなら摩耗しない。
        /// </summary>
        public static float LaborWear(float laborCommodification, float workIntensity, FictitiousCommodityParams p)
        {
            float comm = Mathf.Clamp01(laborCommodification);
            float intensity = Mathf.Clamp01(workIntensity);
            return Mathf.Clamp01(p.laborStressScale * comm * intensity);
        }

        public static float LaborWear(float laborCommodification, float workIntensity)
            => LaborWear(laborCommodification, workIntensity, FictitiousCommodityParams.Default);

        /// <summary>
        /// 土地の商品化が自然・地域を壊す量（0..1）。土地の商品化（landCommodification 0..1）と
        /// 収奪度（exploitation 0..1）の積に土地ストレス感度を掛ける＝土地を売り物として収奪的に
        /// 使うほど自然と地域が壊れる（土地は人がつくったものではない）。どちらかゼロなら破壊されない。
        /// </summary>
        public static float LandDegradation(float landCommodification, float exploitation, FictitiousCommodityParams p)
        {
            float comm = Mathf.Clamp01(landCommodification);
            float exploit = Mathf.Clamp01(exploitation);
            return Mathf.Clamp01(p.landStressScale * comm * exploit);
        }

        public static float LandDegradation(float landCommodification, float exploitation)
            => LandDegradation(landCommodification, exploitation, FictitiousCommodityParams.Default);

        /// <summary>
        /// 貨幣の商品化が生む金融不安定（0..1）。貨幣の商品化（moneyCommodification 0..1）と
        /// 投機度（speculation 0..1）の積に貨幣ストレス感度を掛ける＝貨幣を売り物として投機的に
        /// 扱うほど金融が不安定になる（貨幣は購買力の象徴で生産物ではない）。どちらかゼロなら不安定化しない。
        /// </summary>
        public static float MoneyInstability(float moneyCommodification, float speculation, FictitiousCommodityParams p)
        {
            float comm = Mathf.Clamp01(moneyCommodification);
            float spec = Mathf.Clamp01(speculation);
            return Mathf.Clamp01(p.moneyStressScale * comm * spec);
        }

        public static float MoneyInstability(float moneyCommodification, float speculation)
            => MoneyInstability(moneyCommodification, speculation, FictitiousCommodityParams.Default);

        /// <summary>
        /// 擬制商品ストレスが呼ぶ社会の保護需要（0..1＝二重運動の反作用）。総ストレス（totalStress 0..1）に
        /// 感度を掛ける＝商品化が人・自然・金融を脅かすほど社会は自己防衛（保護）を求める。
        /// <see cref="SocialProtectionRules.ProtectionDemand"/> への入力（市場圧力×不安定化の源）。
        /// </summary>
        public static float ProtectionDemandFromStress(float totalStress, FictitiousCommodityParams p)
        {
            float stress = Mathf.Clamp01(totalStress);
            return Mathf.Clamp01(p.protectionDemandSensitivity * stress);
        }

        public static float ProtectionDemandFromStress(float totalStress)
            => ProtectionDemandFromStress(totalStress, FictitiousCommodityParams.Default);

        /// <summary>
        /// 商品化ストレスが社会の紐帯を解く（1tick後の社会の紐帯 0..1）。商品化ストレス（commodificationStress 0..1）が
        /// 大きいほど紐帯（socialFabric 0..1）が速く解ける＝市場化の圧力が共同体・人間関係を侵食する。
        /// dt は年単位。ストレスがゼロなら紐帯は減らない。
        /// </summary>
        public static float SocialUnravelingTick(float socialFabric, float commodificationStress, float dt,
                                                 FictitiousCommodityParams p)
        {
            float fabric = Mathf.Clamp01(socialFabric);
            float stress = Mathf.Clamp01(commodificationStress);
            float drop = p.unravelRate * stress * Mathf.Max(0f, dt);
            return Mathf.Clamp01(fabric - drop);
        }

        public static float SocialUnravelingTick(float socialFabric, float commodificationStress, float dt)
            => SocialUnravelingTick(socialFabric, commodificationStress, dt, FictitiousCommodityParams.Default);

        /// <summary>
        /// 脱商品化（保護）がストレスを和らげた後の実効ストレス（0..1）。保護水準（protectionLevel 0..1）が
        /// 高いほどそのストレスを軽減する＝労働規制・土地保全・金融規制など脱商品化が固有ストレスを吸収する
        /// （種別の感度に応じて緩和量が決まる）。保護ゼロなら緩和なし、完全保護でも種別感度ぶんまで。
        /// </summary>
        public static float DecommodificationRelief(float commodificationStress, float protectionLevel,
                                                    FictitiousCommodity kind, FictitiousCommodityParams p)
        {
            float stress = Mathf.Clamp01(commodificationStress);
            float prot = Mathf.Clamp01(protectionLevel);
            float relief = p.decommodificationScale * KindScale(kind, p) * prot; // 種別ごとに緩和の効きが違う
            return Mathf.Clamp01(stress * (1f - Mathf.Clamp01(relief)));
        }

        public static float DecommodificationRelief(float commodificationStress, float protectionLevel,
                                                    FictitiousCommodity kind)
            => DecommodificationRelief(commodificationStress, protectionLevel, kind, FictitiousCommodityParams.Default);

        /// <summary>
        /// 過剰商品化か（商品化水準がしきい値を超えたか）。閾値を超えると固有ストレスが社会の耐性を上回り
        /// 保護需要（二重運動）が政治的に噴き出す＝完全商品化は社会の反作用を呼ぶ
        /// （既定しきい値は <see cref="FictitiousCommodityParams"/>）。
        /// </summary>
        public static bool IsOvercommodified(float commodificationLevel, float threshold)
            => Mathf.Clamp01(commodificationLevel) >= Mathf.Clamp01(threshold);

        public static bool IsOvercommodified(float commodificationLevel)
            => IsOvercommodified(commodificationLevel, FictitiousCommodityParams.Default.overcommodifyThreshold);

        /// <summary>種別ごとのストレス感度を返す（労働＝人間の摩耗が最も重い）。</summary>
        private static float KindScale(FictitiousCommodity kind, FictitiousCommodityParams p)
        {
            switch (kind)
            {
                case FictitiousCommodity.労働: return p.laborStressScale;
                case FictitiousCommodity.土地: return p.landStressScale;
                case FictitiousCommodity.貨幣: return p.moneyStressScale;
                default: return p.laborStressScale;
            }
        }
    }
}
