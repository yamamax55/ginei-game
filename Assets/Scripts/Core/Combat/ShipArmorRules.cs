using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦の装甲（armor）パラメータ。傾斜装甲・貫通競合・部位差・剥離の調整値。
    /// ctor で全 Clamp。Default は既定値。
    /// </summary>
    public readonly struct ShipArmorParams
    {
        public readonly float slopeBonus;        // 傾斜（避弾経始）による実効厚の最大増加倍率（1.0で増加なし）
        public readonly float reductionCap;      // 装甲による被ダメ軽減の上限（0..1）
        public readonly float facingSpread;      // 部位差の幅（正面厚く背面薄い・0..1）
        public readonly float overPenThreshold;  // 過貫通とみなす 貫通力/実効装甲 の倍率閾値（≧1）
        public readonly float overPenCap;        // 過貫通時のダメージ頭打ち倍率（0..1）
        public readonly float ablationPerHit;    // 被弾1回あたりの装甲剥離率（0..1）
        public readonly float shatterAngle;      // この正規化角度より浅いと弾きが起きうる（0..1）
        public readonly float shatterPenFloor;   // この貫通力未満だと浅角で弾かれやすい

        public ShipArmorParams(float slopeBonus, float reductionCap, float facingSpread,
            float overPenThreshold, float overPenCap, float ablationPerHit,
            float shatterAngle, float shatterPenFloor)
        {
            this.slopeBonus = Mathf.Max(1f, slopeBonus);
            this.reductionCap = Mathf.Clamp01(reductionCap);
            this.facingSpread = Mathf.Clamp01(facingSpread);
            this.overPenThreshold = Mathf.Max(1f, overPenThreshold);
            this.overPenCap = Mathf.Clamp01(overPenCap);
            this.ablationPerHit = Mathf.Clamp01(ablationPerHit);
            this.shatterAngle = Mathf.Clamp01(shatterAngle);
            this.shatterPenFloor = Mathf.Max(0f, shatterPenFloor);
        }

        public static ShipArmorParams Default => new ShipArmorParams(
            slopeBonus: 1.5f,
            reductionCap: 0.85f,
            facingSpread: 0.5f,
            overPenThreshold: 2.0f,
            overPenCap: 0.6f,
            ablationPerHit: 0.05f,
            shatterAngle: 0.3f,
            shatterPenFloor: 30f);
    }

    /// <summary>
    /// 艦の装甲＝防御力と貫通のモデル（純ロジック）。装甲厚の減衰・傾斜装甲（避弾経始）・
    /// 貫通力と装甲の競合・部位差（正面厚く側背薄い）・過貫通・装甲剥離の累積劣化を扱う。
    /// FleetStrength.TakeDamage（既存の被ダメ軽減）とは別系統＝こちらは装甲そのものの貫通モデル。
    /// 決定論（乱数は roll 引数のみ）。入力 clamp。Mathf の Log/Exp は使わない（Lerp で近似）。test-first。
    /// </summary>
    public static class ShipArmorRules
    {
        // ---- 実効装甲（傾斜＝避弾経始） ----

        /// <summary>公称装甲を命中角度で実効厚へ。angle は 0..1 正規化（0=直角命中/1=極浅角）。
        /// 浅いほど装甲を斜めに貫くため実効厚が増す（cos近似を Lerp で）。</summary>
        public static float EffectiveArmor(float nominalArmor, float impactAngle, ShipArmorParams p)
        {
            float armor = Mathf.Max(0f, nominalArmor);
            float a = Mathf.Clamp01(impactAngle);
            // 直角(0)=1.0倍、極浅角(1)=slopeBonus倍 を線形近似（傾斜で実効厚増）
            float factor = Mathf.Lerp(1f, p.slopeBonus, a);
            return armor * factor;
        }

        public static float EffectiveArmor(float nominalArmor, float impactAngle)
            => EffectiveArmor(nominalArmor, impactAngle, ShipArmorParams.Default);

        // ---- 貫通確率 ----

        /// <summary>貫通確率＝貫通力/(貫通力+実効装甲)。拮抗で0.5。0..1。</summary>
        public static float PenetrationChance(float penetration, float effectiveArmor, ShipArmorParams p)
        {
            float pen = Mathf.Max(0f, penetration);
            float armor = Mathf.Max(0f, effectiveArmor);
            float denom = pen + armor;
            if (denom <= 0f) return 0f; // 両方0は貫通不能扱い
            return Mathf.Clamp01(pen / denom);
        }

        public static float PenetrationChance(float penetration, float effectiveArmor)
            => PenetrationChance(penetration, effectiveArmor, ShipArmorParams.Default);

        // ---- 装甲による被ダメ軽減 ----

        /// <summary>装甲が貫通力を上回るほど被ダメを軽減（最大 reductionCap）。
        /// 軽減率＝装甲/(装甲+貫通力) を cap で頭打ち。0..cap。</summary>
        public static float DamageReduction(float effectiveArmor, float penetration, ShipArmorParams p)
        {
            float armor = Mathf.Max(0f, effectiveArmor);
            float pen = Mathf.Max(0f, penetration);
            float denom = armor + pen;
            if (denom <= 0f) return 0f;
            float raw = armor / denom;
            return Mathf.Clamp(raw, 0f, p.reductionCap);
        }

        public static float DamageReduction(float effectiveArmor, float penetration)
            => DamageReduction(effectiveArmor, penetration, ShipArmorParams.Default);

        // ---- 部位差（正面/側面/背面） ----

        /// <summary>命中部位で装甲厚を変える。hitFacing は 0..1（0=正面/0.5=側面/1=背面）。
        /// 正面は厚く（×(1+spread)）、背面は薄く（×(1-spread)）、側面は基準。</summary>
        public static float FacingArmor(float baseArmor, float hitFacing, ShipArmorParams p)
        {
            float armor = Mathf.Max(0f, baseArmor);
            float f = Mathf.Clamp01(hitFacing);
            // f=0→+spread、f=0.5→0、f=1→-spread の対称線形
            float factor = Mathf.Lerp(1f + p.facingSpread, 1f - p.facingSpread, f);
            return armor * Mathf.Max(0f, factor);
        }

        public static float FacingArmor(float baseArmor, float hitFacing)
            => FacingArmor(baseArmor, hitFacing, ShipArmorParams.Default);

        // ---- 過貫通 ----

        /// <summary>貫通力が実効装甲を大きく上回ると過貫通＝弾が突き抜けダメージが頭打ち（overPenCap）。
        /// 過貫通でないなら1.0（フルダメージ）。0..1。</summary>
        public static float OverPenetration(float penetration, float effectiveArmor, ShipArmorParams p)
        {
            float pen = Mathf.Max(0f, penetration);
            float armor = Mathf.Max(0f, effectiveArmor);
            if (armor <= 0f) return p.overPenCap; // 装甲皆無は素通り（頭打ち）
            float ratio = pen / armor;
            return ratio >= p.overPenThreshold ? p.overPenCap : 1f;
        }

        public static float OverPenetration(float penetration, float effectiveArmor)
            => OverPenetration(penetration, effectiveArmor, ShipArmorParams.Default);

        // ---- 装甲剥離（累積劣化） ----

        /// <summary>被弾累積で装甲が剥離・劣化した残存装甲。hitsTaken 回ぶん ablationPerHit を線形に削る（下限0）。</summary>
        public static float ArmorAblation(float currentArmor, int hitsTaken, ShipArmorParams p)
        {
            float armor = Mathf.Max(0f, currentArmor);
            int hits = (int)Mathf.Max(0f, hitsTaken);
            float loss = Mathf.Clamp01(p.ablationPerHit * hits);
            return armor * (1f - loss);
        }

        public static float ArmorAblation(float currentArmor, int hitsTaken)
            => ArmorAblation(currentArmor, hitsTaken, ShipArmorParams.Default);

        // ---- 弾き（shatter） ----

        /// <summary>浅い角度×低貫通で弾が滑る（弾き）か。impactAngle 0..1（1=極浅角）・penetration。
        /// 浅角（≧1-shatterAngle …の補集合）かつ貫通力が shatterPenFloor 未満なら true。</summary>
        public static bool ShellShatter(float impactAngle, float penetration, ShipArmorParams p)
        {
            float a = Mathf.Clamp01(impactAngle);
            float pen = Mathf.Max(0f, penetration);
            bool shallow = a >= (1f - p.shatterAngle); // 浅角域
            bool weak = pen < p.shatterPenFloor;
            return shallow && weak;
        }

        public static bool ShellShatter(float impactAngle, float penetration)
            => ShellShatter(impactAngle, penetration, ShipArmorParams.Default);

        // ---- 貫通判定（決定論） ----

        /// <summary>貫通したか。roll∈[0,1] を注入（roll ≤ 貫通確率 で貫通）。決定論。</summary>
        public static bool IsPenetrated(float penetrationChance, float roll)
            => Mathf.Clamp01(roll) <= Mathf.Clamp01(penetrationChance);
    }
}
