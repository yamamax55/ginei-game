using UnityEngine;

namespace Ginei
{
    /// <summary>招安ジレンマの調整係数（SHZ-3 #1359・水滸伝＝梁山泊の招安と解体）。</summary>
    public readonly struct CooptionParams
    {
        /// <summary>官位の付与が招安の魅力に効く基準重み。</summary>
        public readonly float rankWeight;
        /// <summary>恩赦（罪を不問）の付与が招安の魅力に効く基準重み。</summary>
        public readonly float amnestyWeight;
        /// <summary>正統性の付与（賊から官軍へ）が招安の魅力に効く基準重み。</summary>
        public readonly float legitimacyWeight;
        /// <summary>体制に吸収されるほど元の結束が時間で薄れる基準速度（梁山泊の義が散る）。</summary>
        public readonly float driftRate;
        /// <summary>使い捨て（戦に使われ滅ぼされる）リスクの基準倍率＝水滸伝の悲劇。</summary>
        public readonly float disposalScale;
        /// <summary>体制に取り込まれたと判定する吸収度の閾値。</summary>
        public readonly float cooptedThreshold;

        public CooptionParams(float rankWeight, float amnestyWeight, float legitimacyWeight,
            float driftRate, float disposalScale, float cooptedThreshold)
        {
            this.rankWeight = Mathf.Clamp01(rankWeight);
            this.amnestyWeight = Mathf.Clamp01(amnestyWeight);
            this.legitimacyWeight = Mathf.Clamp01(legitimacyWeight);
            this.driftRate = Mathf.Max(0f, driftRate);
            this.disposalScale = Mathf.Clamp01(disposalScale);
            this.cooptedThreshold = Mathf.Clamp01(cooptedThreshold);
        }

        /// <summary>既定＝官位0.4・恩赦0.35・正統性0.25（合計1.0で魅力を正規化）・ドリフト速度0.3・使い捨て0.8・取り込み閾値0.6。</summary>
        public static CooptionParams Default =>
            new CooptionParams(0.4f, 0.35f, 0.25f, 0.3f, 0.8f, 0.6f);
    }

    /// <summary>
    /// 招安（しょうあん）ジレンマの純ロジック（SHZ-3 #1359・水滸伝＝宋江と梁山泊の招安）。
    /// **体制（官）が反乱勢力・義賊を武力でなく懐柔して取り込む（co-option）＝官位・恩赦・正統性を
    /// 与えて体制側に組み込む。義賊側は受諾するか拒否するかのジレンマを負う（受諾閾値）。受諾すると
    /// 体制に吸収されて元の結束（梁山泊の義）が次第に薄れ、独立性を失い、最後は使い捨てにされて
    /// 解体する（水滸伝の悲劇＝招安後、方臘討伐の戦に使われ滅ぼされる）**。
    /// 功臣（既に体制内）の処遇ジレンマは <see cref="MeritRetentionRules"/>、捕虜の登用（個人の寝返り）は
    /// <see cref="CaptivityRules"/> が担い、義賊そのものの結束・存続は <see cref="OutlawOrganizationRules"/>
    /// （同EPIC SHZ）、賊の側の対抗的正統性（替天行道）は <see cref="CounterLegitimacyRules"/>（同EPIC）が
    /// 担う。ここは**体制が義賊集団を懐柔して取り込み、取り込みが結束を解体させる過程に特化**する。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CooptionRules
    {
        /// <summary>
        /// 体制が出す招安の魅力（0..1）＝官位 officialRank(0..1)・恩赦 amnesty(0..1)・正統性 legitimacyGranted(0..1)を
        /// それぞれの重みで合成（既定重みは合計1.0で正規化）。出世・赦免・賊から官への正統化が揃うほど
        /// 招安は魅力的になる（宋江を動かした「忠義」の大義名分＝正統性の付与）。
        /// </summary>
        public static float CooptionOffer(float officialRank, float amnesty, float legitimacyGranted, CooptionParams p)
        {
            float r = Mathf.Clamp01(officialRank) * p.rankWeight;
            float a = Mathf.Clamp01(amnesty) * p.amnestyWeight;
            float l = Mathf.Clamp01(legitimacyGranted) * p.legitimacyWeight;
            return Mathf.Clamp01(r + a + l);
        }

        public static float CooptionOffer(float officialRank, float amnesty, float legitimacyGranted)
            => CooptionOffer(officialRank, amnesty, legitimacyGranted, CooptionParams.Default);

        /// <summary>
        /// 義賊が招安を受諾する閾値（0..1・低いほど受諾しやすい）＝疲弊 rebelExhaustion(0..1)が高く、頭領の
        /// 出世欲 leaderAmbition(0..1)が高いほど閾値が下がり、思想の純度 ideologicalPurity(0..1)が高いほど閾値が
        /// 上がる（純粋な反体制ほど招安を拒む＝魯智深・武松ら反対派／李逵の反発）。閾値が低いほど低い魅力でも転ぶ。
        /// </summary>
        public static float AcceptanceThreshold(float rebelExhaustion, float leaderAmbition, float ideologicalPurity)
        {
            float exhaustion = Mathf.Clamp01(rebelExhaustion);
            float ambition = Mathf.Clamp01(leaderAmbition);
            float purity = Mathf.Clamp01(ideologicalPurity);
            // 純度が高いほど高い閾値、疲弊と出世欲が閾値を引き下げる。
            float threshold = purity - 0.5f * exhaustion - 0.5f * ambition;
            return Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 招安を受諾するか＝魅力 cooptionOffer が受諾閾値 acceptanceThreshold を超えれば受諾（宋江の選択）。
        /// 純粋な反体制（高い閾値）には高い魅力が要り、疲弊し出世を望む集団（低い閾値）は容易に転ぶ。
        /// </summary>
        public static bool AcceptCooption(float cooptionOffer, float acceptanceThreshold)
        {
            return Mathf.Clamp01(cooptionOffer) > Mathf.Clamp01(acceptanceThreshold);
        }

        /// <summary>
        /// 体制に吸収されるほど元の結束（梁山泊の義）が時間で薄れる＝吸収度 absorptionLevel(0..1)×driftRate×dt
        /// ぶん cohesion を下げて返す。官軍の一部隊として体制に組み込まれるほど、同志は散り、義兄弟の絆は薄れる
        /// （招安後、好漢たちが各地へ配属され梁山泊が空になる）。下限0でクランプ。
        /// </summary>
        public static float CohesionDriftTick(float cohesion, float absorptionLevel, float dt, CooptionParams p)
        {
            float c = Mathf.Clamp01(cohesion);
            float absorb = Mathf.Clamp01(absorptionLevel);
            float decay = absorb * p.driftRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(c - decay);
        }

        public static float CohesionDriftTick(float cohesion, float absorptionLevel, float dt)
            => CohesionDriftTick(cohesion, absorptionLevel, dt, CooptionParams.Default);

        /// <summary>
        /// 義賊が体制の道具に組み込まれていく度合い（吸収度 0..1）＝結束の喪失 cohesionDrift(0..1・元の結束が
        /// どれだけ失われたか)と体制の統制 institutionalControl(0..1)の積。義が薄れ、かつ体制が強く統制するほど、
        /// 独立した一勢力でなく官の手駒（独立性の喪失）になる＝替天行道の旗が降りる。
        /// </summary>
        public static float AbsorptionIntoSystem(float cohesionDrift, float institutionalControl)
        {
            return Mathf.Clamp01(Mathf.Clamp01(cohesionDrift) * Mathf.Clamp01(institutionalControl));
        }

        /// <summary>
        /// 吸収された後、体制に使い捨てにされるリスク（0..1）＝水滸伝の悲劇。吸収度 absorptionLevel が高い（独立性を
        /// 失い断れない）ほど、また体制の信義 regimeTrustworthiness(0..1)が低いほど高い＝信用できない体制ほど、
        /// 取り込んだ賊を消耗戦（方臘討伐）に投入して滅ぼす。disposalScale で全体の強度を調整。
        /// </summary>
        public static float DisposableAfterUse(float absorptionLevel, float regimeTrustworthiness, CooptionParams p)
        {
            float absorb = Mathf.Clamp01(absorptionLevel);
            float distrust = 1f - Mathf.Clamp01(regimeTrustworthiness);
            return Mathf.Clamp01(absorb * distrust * p.disposalScale);
        }

        public static float DisposableAfterUse(float absorptionLevel, float regimeTrustworthiness)
            => DisposableAfterUse(absorptionLevel, regimeTrustworthiness, CooptionParams.Default);

        /// <summary>
        /// 招安後の二重忠誠の曖昧さ（0..1）＝元の大義 originalCause(0..1)と新しい主君（体制）の間で揺れる度合い。
        /// 吸収が中途半端（absorptionLevel が半ば）で、なお元の義が残るほど忠誠は最も曖昧になる＝完全に体制化
        /// すれば迷いはなく、完全に賊のままでも迷いはない。中間で板挟みが最大（招安後の好漢の鬱屈）。
        /// </summary>
        public static float LoyaltyAmbiguity(float absorptionLevel, float originalCause)
        {
            float absorb = Mathf.Clamp01(absorptionLevel);
            float cause = Mathf.Clamp01(originalCause);
            // 吸収度が0.5で最大になる山型（4x(1-x)）に、残る元の義を掛ける＝板挟みの強さ。
            float split = 4f * absorb * (1f - absorb);
            return Mathf.Clamp01(split * cause);
        }

        /// <summary>
        /// 義賊が体制に取り込まれた判定＝吸収度 absorptionLevel が閾値を超えたら招安が成立（もはや独立勢力でなく
        /// 官の一部隊）。閾値を超えた集団は、使い捨てのリスクに身を晒すことになる。
        /// </summary>
        public static bool IsCoopted(float absorptionLevel, float threshold)
        {
            return Mathf.Clamp01(absorptionLevel) > Mathf.Clamp01(threshold);
        }

        public static bool IsCoopted(float absorptionLevel)
            => IsCoopted(absorptionLevel, CooptionParams.Default.cooptedThreshold);
    }
}
