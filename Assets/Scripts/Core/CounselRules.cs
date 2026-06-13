using UnityEngine;

namespace Ginei
{
    /// <summary>献策の質（参謀の知略×状況で決まる策の冴え）。</summary>
    public enum CounselQuality
    {
        愚策,
        凡策,
        良策,
        神算
    }

    /// <summary>献策システム（#1104）の調整値。</summary>
    public readonly struct CounselParams
    {
        /// <summary>採択確率に効く君主の英明さの重み。</summary>
        public readonly float wisdomWeight;
        /// <summary>採択確率に効く参謀の信頼の重み。</summary>
        public readonly float trustWeight;
        /// <summary>良策採択の成功ボーナス（帰結修正子の振れ幅）。</summary>
        public readonly float adoptBonus;
        /// <summary>信用更新の速さ（的中/外れ1回あたりの寄与）。</summary>
        public readonly float credibilityRate;

        public CounselParams(float wisdomWeight, float trustWeight, float adoptBonus, float credibilityRate)
        {
            this.wisdomWeight = Mathf.Max(0f, wisdomWeight);
            this.trustWeight = Mathf.Max(0f, trustWeight);
            this.adoptBonus = Mathf.Max(0f, adoptBonus);
            this.credibilityRate = Mathf.Max(0f, credibilityRate);
        }

        /// <summary>既定＝英明重み0.6・信頼重み0.4・採択ボーナス0.4・信用更新速度0.2。</summary>
        public static CounselParams Default => new CounselParams(0.6f, 0.4f, 0.4f, 0.2f);
    }

    /// <summary>
    /// 献策システム（#1104・三国志演義型）の純ロジック・唯一の窓口。参謀が策を献じ、君主が採るか退けるかで帰結が変わる。
    /// 良策を採れば成功・退ければ機会損失（袁紹が田豊の策を退けた型）。「良策も君主が採らねば意味がない＝献策は提案と採否の
    /// 二人三脚」を式に出す。能力補完（参謀が提督の運営・情報を底上げ）は `CommandStaffRules` の担当、提督の知略の出所は
    /// `AdmiralData`（intelligence）、不満が嵩じた参謀の離反そのものの判定は `LoyaltyRules` の担当＝本クラスは
    /// 「策の提案と採否の力学」だけを扱う（信頼の更新・不満の蓄積まで＝離反の発火は LoyaltyRules へ渡す）。
    /// 全入力クランプ・乱数は roll 引数で決定論。基準値非破壊（実効値パターン）。test-first。
    /// </summary>
    public static class CounselRules
    {
        // ===== 献策の質 =====

        /// <summary>
        /// 献策の質＝参謀の知略×状況の有利さ（同じ参謀でも状況で策の冴えが変わる）。
        /// 知略・状況とも0..1にクランプ。積が高いほど上位の策へ段階化。
        /// </summary>
        public static CounselQuality StratagemQuality(float counselorIntelligence, float situationFavorability)
        {
            float intel = Mathf.Clamp01(counselorIntelligence);
            float favor = Mathf.Clamp01(situationFavorability);
            float score = intel * favor;
            if (score >= 0.7f) return CounselQuality.神算;
            if (score >= 0.45f) return CounselQuality.良策;
            if (score >= 0.2f) return CounselQuality.凡策;
            return CounselQuality.愚策;
        }

        /// <summary>策の質を0..1の数値へ（愚策0/凡策1/良策2/神算3 を /3 で正規化）。</summary>
        public static float QualityValue(CounselQuality quality)
            => Mathf.Clamp01((int)quality / 3f);

        // ===== 採否（提案と採否の二人三脚） =====

        /// <summary>
        /// 採択される確率＝策の質×（君主の英明×wisdomWeight＋参謀の信頼×trustWeight）。
        /// 良策でも暗愚な君主なら退けられる・信頼ある参謀の策は通りやすい（田豊型）。0..1にクランプ。
        /// </summary>
        public static float AdoptionLikelihood(CounselQuality counselQuality, float rulerWisdom, float counselorTrust)
            => AdoptionLikelihood(counselQuality, rulerWisdom, counselorTrust, CounselParams.Default);

        /// <summary>採択確率（Params指定）。</summary>
        public static float AdoptionLikelihood(CounselQuality counselQuality, float rulerWisdom, float counselorTrust, CounselParams prm)
        {
            float wisdom = Mathf.Clamp01(rulerWisdom);
            float trust = Mathf.Clamp01(counselorTrust);
            float quality = QualityValue(counselQuality);
            // 受容力＝英明×重み＋信頼×重み（重み合計で正規化）。質が高いほど受け入れやすいが、暗愚は良策も退ける。
            float weightSum = Mathf.Max(0.0001f, prm.wisdomWeight + prm.trustWeight);
            float receptivity = (wisdom * prm.wisdomWeight + trust * prm.trustWeight) / weightSum;
            // 質と受容力の積＝良策も君主次第・暗君は良策を退ける。
            return Mathf.Clamp01(quality * receptivity);
        }

        /// <summary>採択判定（roll∈[0,1)・決定論）。roll が採択確率未満なら採択。</summary>
        public static bool IsAdopted(CounselQuality counselQuality, float rulerWisdom, float counselorTrust, float roll)
            => IsAdopted(counselQuality, rulerWisdom, counselorTrust, roll, CounselParams.Default);

        /// <summary>採択判定（Params指定・roll決定論）。</summary>
        public static bool IsAdopted(CounselQuality counselQuality, float rulerWisdom, float counselorTrust, float roll, CounselParams prm)
            => roll < AdoptionLikelihood(counselQuality, rulerWisdom, counselorTrust, prm);

        // ===== 帰結 =====

        /// <summary>
        /// 帰結修正子（成功率などへ掛ける倍率、基準1.0）。良策・神算を採択＝成功率↑、愚策採択＝失敗（↓）、
        /// 採らなければ策は帰結に効かない（良策却下＝機会損失は MissedOpportunityCost が別途量る）。
        /// </summary>
        public static float OutcomeModifier(CounselQuality counselQuality, bool adopted)
            => OutcomeModifier(counselQuality, adopted, CounselParams.Default);

        /// <summary>帰結修正子（Params指定）。</summary>
        public static float OutcomeModifier(CounselQuality counselQuality, bool adopted, CounselParams prm)
        {
            if (!adopted) return 1f; // 退けた策は帰結に効かない（機会損失は別計上）
            // 良策ほど正・愚策は負へ。質0..1を中心0.5で±へ写し、ボーナス幅を掛ける。
            float quality = QualityValue(counselQuality);
            float delta = (quality - 0.5f) * 2f * prm.adoptBonus; // 愚策=-adoptBonus … 神算=+adoptBonus
            return Mathf.Max(0f, 1f + delta);
        }

        /// <summary>
        /// 献策無視の代償＝良策・神算を退けたときの機会損失（後悔の大きさ）。採択したら0、退けたら策の質に比例。
        /// 神算を退けるほど損失が大きい。0..1。
        /// </summary>
        public static float MissedOpportunityCost(CounselQuality counselQuality, bool adopted)
        {
            if (adopted) return 0f;
            // 良策以上の却下のみ後悔が立つ（凡策以下は退けても惜しくない）。
            float quality = QualityValue(counselQuality);
            float regret = quality - QualityValue(CounselQuality.凡策); // 良策で正・凡策以下で0以下
            return Mathf.Clamp01(regret);
        }

        // ===== 参謀の信用（実績が発言力を作る） =====

        /// <summary>
        /// 参謀の信用を更新する＝献策が的中すれば信頼が増し、外れれば減る（実績が次の発言力を作る）。
        /// dt で時間積分・0..1にクランプ。`AdoptionLikelihood` の counselorTrust にこの値を渡す想定。
        /// </summary>
        public static float CounselorCredibilityTick(float credibility, bool adviceWasCorrect, float dt)
            => CounselorCredibilityTick(credibility, adviceWasCorrect, dt, CounselParams.Default);

        /// <summary>参謀の信用更新（Params指定）。</summary>
        public static float CounselorCredibilityTick(float credibility, bool adviceWasCorrect, float dt, CounselParams prm)
        {
            float cred = Mathf.Clamp01(credibility);
            float step = prm.credibilityRate * Mathf.Max(0f, dt);
            cred += adviceWasCorrect ? step : -step;
            return Mathf.Clamp01(cred);
        }

        // ===== 参謀の不満（良策を退けられ続けると去る・寝返る） =====

        /// <summary>
        /// 参謀の不満＝良策を退けられた質×却下の累積回数（陳宮の離反型）。良策・神算を重ねて退けられるほど嵩む。
        /// 凡策以下の却下は不満を生まない。0..1（離反の発火そのものは `LoyaltyRules` へ渡す＝ここは不満値だけ量る）。
        /// </summary>
        public static float RejectionFrustration(CounselQuality counselQuality, int rejectionCount)
        {
            int count = Mathf.Max(0, rejectionCount);
            // 良策以上の却下だけが不満の源（機会損失と同じ閾）。
            float regret = MissedOpportunityCost(counselQuality, false);
            if (regret <= 0f || count <= 0) return 0f;
            // 回数で逓増だが飽和（退けられ続けるほど嵩むが上限あり）。
            float pile = 1f - Mathf.Pow(0.6f, count); // 1回0.4…多回で→1へ漸近
            return Mathf.Clamp01(regret * pile);
        }
    }
}
