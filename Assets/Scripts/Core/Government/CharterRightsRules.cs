using UnityEngine;

namespace Ginei
{
    /// <summary>利権・特許状の種別（市場開設権／資源採掘権／通行税徴収権）。</summary>
    public enum CharterType
    {
        市場開設権,
        資源採掘権,
        通行税徴収権,
    }

    /// <summary>
    /// 利権と特許状の数値解決（#1093 Pillars of the Earth・純ロジック test-first）。利権を扱う唯一の窓口。
    /// 元首/政府がネームドや組織へ利権を特許状として与え、取り消し、複数主体が同一利権を争う＝聖俗の管轄争いをモデル化する。
    /// 「特許状は与えるは易く取り上げるは難し＝利権は怨恨の通貨」を核にする：旨味のある利権の授与は忠誠を生むが、
    /// 長く保有された既得権の取消は深い怨恨を残す（一度与えた利権は取り返しにくい）。
    /// 役割分担：王権制約一般＝<see cref="MagnaCartaRules"/>（憲章で王権そのものを縛る）／本クラスは個別利権の管轄争い。
    /// 独占の超過利潤は <see cref="MonopolyRules"/> と接続（排他的特許状＝市場の失敗のミクロ版）。
    /// 役職そのものの任命・台帳は <c>GovernmentRegistry</c>（本クラスは利権という権益のみ）。
    /// 乱数は持たない（決定論）。調整値は <see cref="CharterRightsParams"/> に集約（基準非破壊・実効値パターン）。
    /// </summary>
    public static class CharterRightsRules
    {
        /// <summary>
        /// 利権の経済価値（0..1）＝種別ごとの基準価値に経済活動度を乗じる。
        /// 市場が栄えるほど市場開設権は高価になる＝経済活動度 economicActivity(0..1) に比例。
        /// </summary>
        public static float CharterValue(CharterType type, float economicActivity, CharterRightsParams p)
        {
            float a = Mathf.Clamp01(economicActivity);
            float baseValue = type switch
            {
                CharterType.市場開設権 => p.MarketBaseValue,
                CharterType.資源採掘権 => p.MiningBaseValue,
                CharterType.通行税徴収権 => p.TollBaseValue,
                _ => p.MarketBaseValue,
            };
            return Mathf.Clamp01(baseValue * a);
        }

        /// <summary>利権の経済価値（既定パラメータ）。</summary>
        public static float CharterValue(CharterType type, float economicActivity)
            => CharterValue(type, economicActivity, CharterRightsParams.Default);

        /// <summary>
        /// 授与による忠誠ボーナス（0..1）＝旨味のある利権を与えれば恩義が生まれる（パトロネージ）。
        /// 利権価値が高いほど、また受け手の野心 granteeAmbition(0..1) が強いほど授与の効きが大きい
        /// （野心家ほど利権を喜ぶ＝買える忠誠）。
        /// </summary>
        public static float GrantLoyaltyBonus(float charterValue, float granteeAmbition, CharterRightsParams p)
        {
            float v = Mathf.Clamp01(charterValue);
            float am = Mathf.Clamp01(granteeAmbition);
            // 野心の重み付け：基礎＋野心ぶんで効きを増幅
            float ambitionScale = 1f + am * p.AmbitionGratitudeWeight;
            return Mathf.Clamp01(v * p.GrantLoyaltyScale * ambitionScale);
        }

        /// <summary>授与忠誠（既定パラメータ）。</summary>
        public static float GrantLoyaltyBonus(float charterValue, float granteeAmbition)
            => GrantLoyaltyBonus(charterValue, granteeAmbition, CharterRightsParams.Default);

        /// <summary>
        /// 取消の怨恨（0..1）＝長く持った既得権を取り上げると深い恨みが残る。
        /// 利権価値が高いほど、保有期間 grantHeld(0..1＝既得権の定着度) が長いほど怨恨は跳ね上がる
        /// （保有が長いほど「自分のもの」になる＝取り上げは裏切りに映る）。授与忠誠より係数を重くし、
        /// 「与えるは易く取り上げるは難し＝利権は怨恨の通貨」を式に出す（非対称）。
        /// </summary>
        public static float RevocationResentment(float charterValue, float grantHeld, CharterRightsParams p)
        {
            float v = Mathf.Clamp01(charterValue);
            float held = Mathf.Clamp01(grantHeld);
            // 既得権化＝基礎＋保有期間ぶんで怨恨を増幅（長期保有ほど深く恨む）
            float entrenchScale = p.RevocationBase + held * p.EntrenchmentWeight;
            return Mathf.Clamp01(v * entrenchScale);
        }

        /// <summary>取消の怨恨（既定パラメータ）。</summary>
        public static float RevocationResentment(float charterValue, float grantHeld)
            => RevocationResentment(charterValue, grantHeld, CharterRightsParams.Default);

        /// <summary>
        /// 管轄争いの激しさ（0..1）＝複数主体が同一利権を請求するほど争いは激化する
        /// （宗教組織 vs 貴族 vs 政府部局）。請求者が 1 以下なら争い無し、増えるほど飽和しながら上昇し、
        /// 価値の高い利権ほど争奪は熾烈になる（価値で乗じる）。
        /// </summary>
        public static float DisputeIntensity(int claimantCount, float charterValue, CharterRightsParams p)
        {
            if (claimantCount <= 1) return 0f;
            float v = Mathf.Clamp01(charterValue);
            // 競合者数を飽和曲線へ：余剰請求者(count-1)を半飽和定数で写像
            float rivals = claimantCount - 1;
            float contest = rivals / (rivals + p.DisputeHalfSaturation);
            return Mathf.Clamp01(contest * v);
        }

        /// <summary>管轄争いの激しさ（既定パラメータ）。</summary>
        public static float DisputeIntensity(int claimantCount, float charterValue)
            => DisputeIntensity(claimantCount, charterValue, CharterRightsParams.Default);

        /// <summary>
        /// 独占利権の上がり（超過利潤、0..1）＝排他的な特許状ほど超過利潤を生む。
        /// 排他性 exclusivity(0..1) が高いほど競争が消えてレントが膨らむ（<see cref="MonopolyRules"/> の市場版）。
        /// 価値ゼロ・排他性ゼロなら超過利潤も無い。
        /// </summary>
        public static float MonopolyRent(float charterValue, float exclusivity, CharterRightsParams p)
        {
            float v = Mathf.Clamp01(charterValue);
            float ex = Mathf.Clamp01(exclusivity);
            // 排他性は二乗で効かせる＝完全排他に近づくほど超過利潤が跳ねる（独占の非線形）
            return Mathf.Clamp01(v * ex * ex * p.MonopolyRentScale);
        }

        /// <summary>独占利権の上がり（既定パラメータ）。</summary>
        public static float MonopolyRent(float charterValue, float exclusivity)
            => MonopolyRent(charterValue, exclusivity, CharterRightsParams.Default);

        /// <summary>
        /// 聖俗の管轄争い（0..1）＝教会と世俗権力が同じ利権を主張するときの紛争度。
        /// 双方の請求 secularClaim/religiousClaim(0..1) がともに強いときに最大化し（両者譲らぬ管轄争い）、
        /// 片方の請求が弱ければ争いにならない＝積で効かせる（どちらか欠ければ管轄は確定する）。
        /// </summary>
        public static float JurisdictionConflict(float secularClaim, float religiousClaim, CharterRightsParams p)
        {
            float s = Mathf.Clamp01(secularClaim);
            float r = Mathf.Clamp01(religiousClaim);
            // 双方の請求が拮抗するほど熾烈＝積に均衡度を乗じる（一方的請求は確定して争わない）
            float overlap = s * r;
            float balance = 1f - Mathf.Abs(s - r); // 拮抗で1・偏りで低下
            return Mathf.Clamp01(overlap * balance * p.JurisdictionScale);
        }

        /// <summary>聖俗の管轄争い（既定パラメータ）。</summary>
        public static float JurisdictionConflict(float secularClaim, float religiousClaim)
            => JurisdictionConflict(secularClaim, religiousClaim, CharterRightsParams.Default);
    }

    /// <summary>
    /// CharterRightsRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 「与えるは易く取り上げるは難し」を係数で固定する＝取消怨恨の基礎・既得権重みは授与忠誠より重い（非対称）。
    /// ctor で全入力をクランプ（価値/重みは [0,1]、半飽和定数は下限ガード）。
    /// </summary>
    public readonly struct CharterRightsParams
    {
        /// <summary>市場開設権の基準経済価値（0..1）。</summary>
        public readonly float MarketBaseValue;
        /// <summary>資源採掘権の基準経済価値（0..1）。</summary>
        public readonly float MiningBaseValue;
        /// <summary>通行税徴収権の基準経済価値（0..1）。</summary>
        public readonly float TollBaseValue;
        /// <summary>授与忠誠の係数（利権価値→忠誠）。</summary>
        public readonly float GrantLoyaltyScale;
        /// <summary>授与忠誠における野心の感謝増幅（野心家ほど利権を喜ぶ）。</summary>
        public readonly float AmbitionGratitudeWeight;
        /// <summary>取消怨恨の基礎係数（保有ゼロでも生じる最低限の恨み）。</summary>
        public readonly float RevocationBase;
        /// <summary>取消怨恨における既得権（保有期間）の重み（長期保有ほど深く恨む）。</summary>
        public readonly float EntrenchmentWeight;
        /// <summary>管轄争いの半飽和定数（余剰請求者がこの数で争いが半分に達する）。</summary>
        public readonly float DisputeHalfSaturation;
        /// <summary>独占利権の超過利潤係数。</summary>
        public readonly float MonopolyRentScale;
        /// <summary>聖俗の管轄争いの係数。</summary>
        public readonly float JurisdictionScale;

        public CharterRightsParams(
            float marketBaseValue, float miningBaseValue, float tollBaseValue,
            float grantLoyaltyScale, float ambitionGratitudeWeight,
            float revocationBase, float entrenchmentWeight,
            float disputeHalfSaturation, float monopolyRentScale, float jurisdictionScale)
        {
            MarketBaseValue = Mathf.Clamp01(marketBaseValue);
            MiningBaseValue = Mathf.Clamp01(miningBaseValue);
            TollBaseValue = Mathf.Clamp01(tollBaseValue);
            GrantLoyaltyScale = Mathf.Clamp01(grantLoyaltyScale);
            AmbitionGratitudeWeight = Mathf.Clamp01(ambitionGratitudeWeight);
            RevocationBase = Mathf.Clamp01(revocationBase);
            EntrenchmentWeight = Mathf.Clamp01(entrenchmentWeight);
            DisputeHalfSaturation = Mathf.Max(0.0001f, disputeHalfSaturation);
            MonopolyRentScale = Mathf.Clamp01(monopolyRentScale);
            JurisdictionScale = Mathf.Clamp01(jurisdictionScale);
        }

        /// <summary>
        /// 既定（市場0.9/採掘0.7/通行税0.6・授与忠誠0.5＋野心増幅0.4・取消怨恨 基礎0.4＋既得権重み0.6・
        /// 争い半飽和2・独占係数1.0・聖俗係数1.0）。
        /// 取消怨恨の最大係数(0.4+0.6=1.0)は授与忠誠の最大係数(0.5×1.4=0.7)を上回る＝
        /// 「与えるは易く取り上げるは難し＝利権は怨恨の通貨」を非対称に表す。
        /// </summary>
        public static CharterRightsParams Default => new CharterRightsParams(
            marketBaseValue: 0.9f, miningBaseValue: 0.7f, tollBaseValue: 0.6f,
            grantLoyaltyScale: 0.5f, ambitionGratitudeWeight: 0.4f,
            revocationBase: 0.4f, entrenchmentWeight: 0.6f,
            disputeHalfSaturation: 2f, monopolyRentScale: 1f, jurisdictionScale: 1f);
    }
}
