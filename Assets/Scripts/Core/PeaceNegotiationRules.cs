using UnityEngine;

namespace Ginei
{
    /// <summary>講和交渉の調整値。マジックナンバー禁止＝集約。ctor で全 Clamp。</summary>
    public readonly struct PeaceNegotiationParams
    {
        public readonly float wearinessWeight;     // 厭戦が交渉動機/譲歩へ寄与する重み
        public readonly float stalemateWeight;     // 膠着が交渉動機へ寄与する重み
        public readonly float strainWeight;        // 経済疲弊が交渉動機へ寄与する重み
        public readonly float leverageGain;        // てこ(leverage)が要求の強さを押し上げる係数
        public readonly float pressureWeight;      // 国内圧力が譲歩へ寄与する重み
        public readonly float faceWeight;          // 威信×国内政治が面子の要求へ寄与する重み
        public readonly float distrustWeight;      // 相互不信が決裂リスクへ寄与する重み

        public PeaceNegotiationParams(float wearinessWeight, float stalemateWeight, float strainWeight,
            float leverageGain, float pressureWeight, float faceWeight, float distrustWeight)
        {
            this.wearinessWeight = Mathf.Clamp01(wearinessWeight);
            this.stalemateWeight = Mathf.Clamp01(stalemateWeight);
            this.strainWeight = Mathf.Clamp01(strainWeight);
            this.leverageGain = Mathf.Clamp(leverageGain, 0f, 2f);
            this.pressureWeight = Mathf.Clamp01(pressureWeight);
            this.faceWeight = Mathf.Clamp01(faceWeight);
            this.distrustWeight = Mathf.Clamp01(distrustWeight);
        }

        /// <summary>既定値（厭戦0.5/膠着0.3/疲弊0.2・てこ0.5・国内圧力0.4・面子0.6・不信0.5）。</summary>
        public static PeaceNegotiationParams Default =>
            new PeaceNegotiationParams(0.5f, 0.3f, 0.2f, 0.5f, 0.4f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 講和交渉の純ロジック（外交EPIC #189・<b>戦争終結の交渉力学に特化</b>・唯一の窓口）。
    /// 交渉のテーブルにつく動機（厭戦×膠着×経済疲弊）、戦況の優劣とてこで決まる要求の強さ、厭戦と国内圧力による
    /// 譲歩の用意、威信・国内政治が課す面子（メンツを潰せない）、A要求とB譲歩の重なる合意可能域（ZOPA）、
    /// 要求の隔たりと相互不信が招く決裂、そして講和に到達できるかの天秤を扱う。
    /// <see cref="WarTerminationRules"/>（戦争終結の機運・引き際）とは別＝こちらは<b>交渉そのもの</b>の力学。
    /// <see cref="DiplomacyRules"/>（外交状態遷移）とも別。盤面非依存の plain 引数・乱数なし・入力クランプ・
    /// 実効値パターン。各メソッドに Params 明示版＋Default 委譲版。test-first。
    /// </summary>
    public static class PeaceNegotiationRules
    {
        // ── 交渉への動機 ──────────────────────────────────────
        /// <summary>交渉への動機＝厭戦・膠着・経済疲弊の加重和（0..1）。三者が揃うほどテーブルにつく。Params 明示版。</summary>
        public static float NegotiationIncentive(float warWeariness, float stalemate, float economicStrain,
            PeaceNegotiationParams p)
        {
            float ww = Mathf.Clamp01(warWeariness);
            float st = Mathf.Clamp01(stalemate);
            float es = Mathf.Clamp01(economicStrain);
            float w = ww * p.wearinessWeight + st * p.stalemateWeight + es * p.strainWeight;
            return Mathf.Clamp01(w);
        }

        /// <summary>交渉への動機（Default 委譲版）。</summary>
        public static float NegotiationIncentive(float warWeariness, float stalemate, float economicStrain) =>
            NegotiationIncentive(warWeariness, stalemate, economicStrain, PeaceNegotiationParams.Default);

        // ── 要求の強さ ────────────────────────────────────────
        /// <summary>要求の強さ＝戦況優位を土台に、てこ(leverage)が上乗せ（0..1）。優位なほど強く要求できる。Params 明示版。</summary>
        public static float DemandStrength(float battlefieldPosition, float leverage, PeaceNegotiationParams p)
        {
            float bp = Mathf.Clamp01(battlefieldPosition);
            float lv = Mathf.Clamp01(leverage);
            // 戦況優位が土台、てこが拍車。てこは battlefieldPosition を leverageGain ぶん押し上げる。
            float d = bp * (1f + lv * p.leverageGain);
            return Mathf.Clamp01(d);
        }

        /// <summary>要求の強さ（Default 委譲版）。</summary>
        public static float DemandStrength(float battlefieldPosition, float leverage) =>
            DemandStrength(battlefieldPosition, leverage, PeaceNegotiationParams.Default);

        // ── 譲歩の用意 ────────────────────────────────────────
        /// <summary>譲歩の用意＝厭戦と国内圧力で高まる（0..1）。疲れて国内が和平を求めるほど折れる。Params 明示版。</summary>
        public static float ConcessionWillingness(float warWeariness, float domesticPressure, PeaceNegotiationParams p)
        {
            float ww = Mathf.Clamp01(warWeariness);
            float dp = Mathf.Clamp01(domesticPressure);
            float c = ww * p.wearinessWeight + dp * p.pressureWeight;
            return Mathf.Clamp01(c);
        }

        /// <summary>譲歩の用意（Default 委譲版）。</summary>
        public static float ConcessionWillingness(float warWeariness, float domesticPressure) =>
            ConcessionWillingness(warWeariness, domesticPressure, PeaceNegotiationParams.Default);

        // ── 面子の要求 ────────────────────────────────────────
        /// <summary>面子の要求＝威信×国内政治（幾何平均×重み・0..1）。威信が高く国内が見ているほどメンツを潰せない。Params 明示版。</summary>
        public static float FaceSavingNeed(float prestige, float domesticPolitics, PeaceNegotiationParams p)
        {
            float pr = Mathf.Clamp01(prestige);
            float dp = Mathf.Clamp01(domesticPolitics);
            // 幾何平均＝どちらか低ければ面子は崩れる（威信があっても国内が無関心なら譲れる、逆も然り）。
            float fn = Mathf.Sqrt(pr * dp) * p.faceWeight;
            return Mathf.Clamp01(fn);
        }

        /// <summary>面子の要求（Default 委譲版）。</summary>
        public static float FaceSavingNeed(float prestige, float domesticPolitics) =>
            FaceSavingNeed(prestige, domesticPolitics, PeaceNegotiationParams.Default);

        // ── 合意可能域（ZOPA） ────────────────────────────────
        /// <summary>合意可能域＝A の要求と B の譲歩の重なり（0..1）。譲歩が要求に届くほど域が開く。Params 明示版。</summary>
        public static float ZoneOfAgreement(float demandStrengthA, float concessionWillingnessB,
            PeaceNegotiationParams p)
        {
            float da = Mathf.Clamp01(demandStrengthA);
            float cb = Mathf.Clamp01(concessionWillingnessB);
            // 譲歩が要求を満たすぶんだけ域が開く。譲歩が要求に満たなければ域は閉じる（負はゼロ）。
            float overlap = cb - da;
            return Mathf.Clamp01(Mathf.Max(0f, overlap));
        }

        /// <summary>合意可能域（Default 委譲版）。</summary>
        public static float ZoneOfAgreement(float demandStrengthA, float concessionWillingnessB) =>
            ZoneOfAgreement(demandStrengthA, concessionWillingnessB, PeaceNegotiationParams.Default);

        // ── 合意の魅力 ────────────────────────────────────────
        /// <summary>合意の魅力＝合意可能域×(1-面子の制約)（0..1）。面子が強いほど呑みにくい。Params 明示版（p は予約・将来拡張用）。</summary>
        public static float DealAttractiveness(float zoneOfAgreement, float faceSavingNeed, PeaceNegotiationParams p)
        {
            float z = Mathf.Clamp01(zoneOfAgreement);
            float f = Mathf.Clamp01(faceSavingNeed);
            // 合意域があっても面子が制約として削る。面子1.0なら魅力ゼロ＝呑めない。
            float v = z * (1f - f);
            return Mathf.Clamp01(v);
        }

        /// <summary>合意の魅力（Default 委譲版）。</summary>
        public static float DealAttractiveness(float zoneOfAgreement, float faceSavingNeed) =>
            DealAttractiveness(zoneOfAgreement, faceSavingNeed, PeaceNegotiationParams.Default);

        // ── 決裂リスク ────────────────────────────────────────
        /// <summary>決裂リスク＝要求の隔たり×相互不信（0..1）。隔たりが大きく信頼がないほど卓は壊れる。Params 明示版。</summary>
        public static float BreakdownRisk(float demandGap, float mutualDistrust, PeaceNegotiationParams p)
        {
            float dg = Mathf.Clamp01(demandGap);
            float md = Mathf.Clamp01(mutualDistrust);
            // 隔たりが土台、不信が増幅。不信が distrustWeight ぶん隔たりを膨らませる。
            float r = dg * (1f + md * p.distrustWeight);
            return Mathf.Clamp01(r);
        }

        /// <summary>決裂リスク（Default 委譲版）。</summary>
        public static float BreakdownRisk(float demandGap, float mutualDistrust) =>
            BreakdownRisk(demandGap, mutualDistrust, PeaceNegotiationParams.Default);

        // ── 講和到達の判定 ─────────────────────────────────────
        /// <summary>講和に到達できるか＝決裂リスクで割り引いた合意の魅力が閾値を超えるか。Params 不要の閾値版。</summary>
        public static bool IsPeaceReachable(float dealAttractiveness, float breakdownRisk, float threshold)
        {
            float da = Mathf.Clamp01(dealAttractiveness);
            float br = Mathf.Clamp01(breakdownRisk);
            float th = Mathf.Clamp01(threshold);
            // 決裂リスクが魅力を削る＝リスク1.0なら到達不能。
            float effective = da * (1f - br);
            return effective >= th;
        }

        /// <summary>講和に到達できるか（既定閾値 0.3 委譲版）。</summary>
        public static bool IsPeaceReachable(float dealAttractiveness, float breakdownRisk) =>
            IsPeaceReachable(dealAttractiveness, breakdownRisk, DefaultReachThreshold);

        /// <summary>講和到達の既定閾値。</summary>
        public const float DefaultReachThreshold = 0.3f;
    }
}
