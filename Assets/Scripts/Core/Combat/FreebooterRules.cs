using UnityEngine;

namespace Ginei
{
    /// <summary>無頼の冒険者（独立傭兵／宇宙海賊）の調整係数。</summary>
    public readonly struct FreebooterParams
    {
        /// <summary>能力値の正規化基準（prowess/experience を 0..1 へ割る分母）。</summary>
        public readonly float statMax;
        /// <summary>技量の全体倍率（腕一本の評価をどれだけ重く見るか）。</summary>
        public readonly float skillScale;
        /// <summary>仕事の食いつきでリスクが分母にどれだけ効くか（大きいほど危険な仕事を嫌う）。</summary>
        public readonly float riskWeight;
        /// <summary>評判における武勇伝の寄与（雇われやすさ）。</summary>
        public readonly float deedsWeight;
        /// <summary>評判における悪名の寄与（名は売れるが警戒される＝両刃）。</summary>
        public readonly float infamyWeight;
        /// <summary>信頼性の下限（信条ゼロ・前金ゼロでも腕は貸すが信用は最低これだけ）。</summary>
        public readonly float minReliability;
        /// <summary>前金が信頼性を底上げする強さ（前金を積めば動く）。</summary>
        public readonly float upfrontWeight;
        /// <summary>無頼の使い勝手で裏切りリスクを引く強さ（信義のなさのペナルティ）。</summary>
        public readonly float betrayalPenalty;

        public FreebooterParams(float statMax, float skillScale, float riskWeight, float deedsWeight,
                                float infamyWeight, float minReliability, float upfrontWeight, float betrayalPenalty)
        {
            this.statMax = Mathf.Max(1e-4f, statMax);
            this.skillScale = Mathf.Max(0f, skillScale);
            this.riskWeight = Mathf.Max(0f, riskWeight);
            this.deedsWeight = Mathf.Max(0f, deedsWeight);
            this.infamyWeight = Mathf.Max(0f, infamyWeight);
            this.minReliability = Mathf.Clamp01(minReliability);
            this.upfrontWeight = Mathf.Clamp01(upfrontWeight);
            this.betrayalPenalty = Mathf.Max(0f, betrayalPenalty);
        }

        /// <summary>既定＝能力基準100・技量倍率1・リスク重み1・武勇伝0.6/悪名0.4・信頼下限0.2・前金0.5・裏切り1。</summary>
        public static FreebooterParams Default => new FreebooterParams(100f, 1f, 1f, 0.6f, 0.4f, 0.2f, 0.5f, 1f);
    }

    /// <summary>
    /// 無頼の冒険者の純ロジック。国家に属さない独立傭兵・宇宙海賊（フリーブーター）の個人活動を解決する＝
    /// 腕一本の技量と評判、報酬目当ての気まぐれな食いつき（割の良い危険な仕事ほど請ける）、前金次第の信頼性と
    /// 信義のなさ（より高い報酬で裏切る）、犯した罪と当局の追跡によるお尋ね者の度合い、足のつかない汚れ仕事の
    /// 請負価値（国家が使い捨てる）。能力値（prowess/experience/deeds/infamy）は 0..statMax 想定で正規化、
    /// 確率的な係数（risk/loyalty/reward 等）は 0..1 想定。乱数なし・決定論・実効値パターン（基準値非破壊）。
    /// 正規軍に雇われる傭兵団 <see cref="MercenaryRules"/>・公認の私掠 <see cref="PrivateerRules"/>・
    /// 無主の海賊 <see cref="PiracyRules"/> とは別系統（こちらは一匹狼の個人＝足のつかない汚れ仕事の請負人）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FreebooterRules
    {
        /// <summary>
        /// 無頼の技量（0..skillScale）＝個人の腕 individualProwess と経験 experience の幾何平均×倍率。
        /// 腕と場数のどちらかが欠ければ崩れる（幾何平均＝一匹狼は片輪では食えない）。
        /// </summary>
        public static float MercenarySkill(float individualProwess, float experience, FreebooterParams p)
        {
            float pr = Mathf.Clamp01(Mathf.Max(0f, individualProwess) / p.statMax);
            float ex = Mathf.Clamp01(Mathf.Max(0f, experience) / p.statMax);
            return Mathf.Sqrt(pr * ex) * p.skillScale;
        }

        public static float MercenarySkill(float individualProwess, float experience)
            => MercenarySkill(individualProwess, experience, FreebooterParams.Default);

        /// <summary>
        /// 仕事への食いつき（割の良さ・0..1）＝報酬 rewardOffered(0..1) ÷ (1 + リスク riskLevel(0..1)×riskWeight)。
        /// 報酬が高く危険が低い「割の良い仕事」ほど食いつく＝気まぐれな働きの動機。
        /// </summary>
        public static float JobOpportunism(float rewardOffered, float riskLevel, FreebooterParams p)
        {
            float r = Mathf.Clamp01(rewardOffered);
            float risk = Mathf.Clamp01(riskLevel);
            return Mathf.Clamp01(r / (1f + risk * p.riskWeight));
        }

        public static float JobOpportunism(float rewardOffered, float riskLevel)
            => JobOpportunism(rewardOffered, riskLevel, FreebooterParams.Default);

        /// <summary>
        /// 評判（0..1・両刃）＝武勇伝 notableDeeds×deedsWeight ＋ 悪名 infamy×infamyWeight（ともに正規化）。
        /// 名が売れれば仕事は来るが、悪名は雇い主に警戒もされる＝腕一本の評判は諸刃。
        /// </summary>
        public static float Reputation(float notableDeeds, float infamy, FreebooterParams p)
        {
            float deeds = Mathf.Clamp01(Mathf.Max(0f, notableDeeds) / p.statMax);
            float inf = Mathf.Clamp01(Mathf.Max(0f, infamy) / p.statMax);
            return Mathf.Clamp01(deeds * p.deedsWeight + inf * p.infamyWeight);
        }

        public static float Reputation(float notableDeeds, float infamy)
            => Reputation(notableDeeds, infamy, FreebooterParams.Default);

        /// <summary>
        /// 信頼性（minReliability..1）＝個人の信条 personalCode(0..1) を土台に、前金 paymentUpfront(0..1) で底上げ。
        /// 信条ある者は前金なしでも一定は守るが、無頼でも前金を積めば動く（前金が信用を買う）。
        /// </summary>
        public static float Reliability(float personalCode, float paymentUpfront, FreebooterParams p)
        {
            float code = Mathf.Lerp(p.minReliability, 1f, Mathf.Clamp01(personalCode));
            float boost = p.upfrontWeight * Mathf.Clamp01(paymentUpfront) * (1f - code);
            return Mathf.Clamp01(code + boost);
        }

        public static float Reliability(float personalCode, float paymentUpfront)
            => Reliability(personalCode, paymentUpfront, FreebooterParams.Default);

        /// <summary>
        /// 裏切りリスク（0..1）＝(1−信義 loyalty(0..1)) × より高い報酬 higherBidder(0..1)。
        /// 信義の薄い者ほど、より高く買う者が現れると寝返る＝雇うときの最大の地雷。
        /// </summary>
        public static float BetrayalRisk(float loyalty, float higherBidder, FreebooterParams p)
        {
            float distrust = 1f - Mathf.Clamp01(loyalty);
            return Mathf.Clamp01(distrust * Mathf.Clamp01(higherBidder));
        }

        public static float BetrayalRisk(float loyalty, float higherBidder)
            => BetrayalRisk(loyalty, higherBidder, FreebooterParams.Default);

        /// <summary>
        /// お尋ね者の度合い（0..1）＝犯した罪 crimesCommitted(0..1) × 当局の追跡 authorityPursuit(0..1)。
        /// 罪を重ねても誰も追わなければ野放し、追跡が厳しくても罪がなければ手配されない＝両者の積。
        /// </summary>
        public static float Outlawry(float crimesCommitted, float authorityPursuit, FreebooterParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(crimesCommitted) * Mathf.Clamp01(authorityPursuit));
        }

        public static float Outlawry(float crimesCommitted, float authorityPursuit)
            => Outlawry(crimesCommitted, authorityPursuit, FreebooterParams.Default);

        /// <summary>
        /// 汚れ仕事の請負価値（0..1）＝無法度 outlawry(0..1) × 足のつかなさ untraceability(0..1)。
        /// 国家にとって、罪を重ねた者ほど後ろ暗い仕事を厭わず、足がつかないほど使い捨てに都合がよい
        /// ＝デニアブルな（否認できる）汚れ仕事の担い手としての価値。
        /// </summary>
        public static float DeniableOperations(float outlawry, float untraceability, FreebooterParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(outlawry) * Mathf.Clamp01(untraceability));
        }

        public static float DeniableOperations(float outlawry, float untraceability)
            => DeniableOperations(outlawry, untraceability, FreebooterParams.Default);

        /// <summary>
        /// 無頼の使い勝手（-1..1）＝技量 mercenarySkill × 信頼性 reliability − 裏切りリスク betrayalRisk×betrayalPenalty。
        /// 腕が立ち約束を守るほど高く、信義のなさ（裏切り）が差し引く＝雇うべきか否かの総合評価。
        /// 負値＝裏切りリスクが価値を食い潰した「雇うほど損」な無頼。
        /// </summary>
        public static float FreebooterUtility(float mercenarySkill, float reliability, float betrayalRisk, FreebooterParams p)
        {
            float skill = Mathf.Max(0f, mercenarySkill);
            float rel = Mathf.Clamp01(reliability);
            float betray = Mathf.Clamp01(betrayalRisk);
            float u = skill * rel - betray * p.betrayalPenalty;
            return Mathf.Clamp(u, -1f, 1f);
        }

        public static float FreebooterUtility(float mercenarySkill, float reliability, float betrayalRisk)
            => FreebooterUtility(mercenarySkill, reliability, betrayalRisk, FreebooterParams.Default);
    }
}
