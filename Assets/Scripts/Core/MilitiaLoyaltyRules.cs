using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 徴募源（兵の集め方）。マキャヴェッリ『ディスコルシ』『戦術論』の軸＝
    /// 市民軍（祖国のために戦う最も信頼できる兵）／志願兵（自発・中高い忠誠）／徴集兵（強制・中程度）／傭兵（金で動き危機に逃げる）。
    /// </summary>
    public enum RecruitmentSource { 市民軍, 志願兵, 徴集兵, 傭兵 }

    /// <summary>市民軍忠誠の調整係数（DISC-2 #1483）。</summary>
    public readonly struct MilitiaLoyaltyParams
    {
        /// <summary>逆境（敗勢・苦境）が忠誠を削る基礎の強さ（耐性が低いほど深く効く）。</summary>
        public readonly float adversityHit;
        /// <summary>傭兵の給与未払い（payArrears）が離反を押し上げる強さ。</summary>
        public readonly float arrearsDefectScale;
        /// <summary>祖国の脅威が市民軍の戦意を押し上げる強さ。</summary>
        public readonly float patriotismScale;
        /// <summary>傭兵がより良い条件で寝返る性向（betterOffer×これだけ背信確率）。</summary>
        public readonly float perfidyScale;
        /// <summary>信頼できる軍とみなす逆境耐性の閾値。</summary>
        public readonly float reliableThreshold;

        public MilitiaLoyaltyParams(float adversityHit, float arrearsDefectScale, float patriotismScale, float perfidyScale, float reliableThreshold)
        {
            this.adversityHit = Mathf.Max(0f, adversityHit);
            this.arrearsDefectScale = Mathf.Max(0f, arrearsDefectScale);
            this.patriotismScale = Mathf.Max(0f, patriotismScale);
            this.perfidyScale = Mathf.Max(0f, perfidyScale);
            this.reliableThreshold = Mathf.Clamp01(reliableThreshold);
        }

        /// <summary>既定＝逆境減0.6・未払い離反0.5・愛国0.6・背信0.8・信頼閾値0.5。</summary>
        public static MilitiaLoyaltyParams Default => new MilitiaLoyaltyParams(0.6f, 0.5f, 0.6f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 市民軍・傭兵の忠誠軸の純ロジック（DISC-2 #1483・マキャヴェッリ『ディスコルシ』『戦術論』）。
    /// 「自国の市民軍こそ最も信頼できる＝傭兵は金で動き危機に逃げ、援軍は他国に従属する。市民は祖国のために戦うので逆境に強い」
    /// を式に出す＝徴募源（市民軍／志願兵／徴集兵／傭兵）が逆境時の離反確率の差を生む。市民軍は逆境に強く傭兵は逃げる。
    /// 給与と離反の動態は <see cref="MercenaryRules"/>、人口コストを伴う徴募は <see cref="ConscriptionRules"/>、
    /// 集団反乱は <see cref="MutinyRules"/>、無言の損耗（脱走）は <see cref="DesertionRules"/> が担い、
    /// ここは「徴募源そのものが生む逆境忠誠の差」のみを扱う。乱数は外から渡す roll で決定論（または確率を返す）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MilitiaLoyaltyRules
    {
        /// <summary>
        /// 徴募源ごとの基礎忠誠（0..1）。市民軍＝高い（祖国のため）・志願兵＝中高（自発）・徴集兵＝中（強制）・傭兵＝低い（金次第）。
        /// </summary>
        public static float BaseLoyalty(RecruitmentSource source)
        {
            switch (source)
            {
                case RecruitmentSource.市民軍: return 0.9f;
                case RecruitmentSource.志願兵: return 0.75f;
                case RecruitmentSource.徴集兵: return 0.55f;
                case RecruitmentSource.傭兵: return 0.35f;
                default: return 0.5f;
            }
        }

        /// <summary>
        /// 逆境（adversity 0..1＝敗勢・苦境）での踏みとどまり（0..1）＝逆境耐性。基礎忠誠が高いほど逆境で削られにくい。
        /// 市民軍は祖国のため逆境に強く残り、傭兵は早く崩れて逃げる。
        /// </summary>
        public static float AdversityResilience(RecruitmentSource source, float adversity, MilitiaLoyaltyParams p)
        {
            float a = Mathf.Clamp01(adversity);
            float baseL = BaseLoyalty(source);
            // 逆境の打撃は (1−基礎忠誠) に比例＝信頼できる兵ほど削られにくい。
            float erosion = a * p.adversityHit * (1f - baseL);
            return Mathf.Clamp01(baseL - erosion);
        }

        public static float AdversityResilience(RecruitmentSource source, float adversity)
            => AdversityResilience(source, adversity, MilitiaLoyaltyParams.Default);

        /// <summary>
        /// 逆境時の離反確率（0..1）。逆境耐性が低いほど跳ね上がる。傭兵は給与未払い（payArrears 0..1）でさらに上がり、
        /// 市民軍は耐性が高く低く抑えられる。離反の素地＝(1−耐性)、傭兵のみ未払いが加算で効く。
        /// </summary>
        public static float DefectionProbability(RecruitmentSource source, float adversity, float payArrears, MilitiaLoyaltyParams p)
        {
            float resilience = AdversityResilience(source, adversity, p);
            float chance = 1f - resilience; // 耐性が低いほど離反しやすい
            if (source == RecruitmentSource.傭兵)
            {
                // 傭兵は未払いと逆境で離反が跳ね上がる（金で繋いだ忠誠は払えなければ切れる）。
                chance += Mathf.Clamp01(payArrears) * p.arrearsDefectScale;
            }
            return Mathf.Clamp01(chance);
        }

        public static float DefectionProbability(RecruitmentSource source, float adversity, float payArrears)
            => DefectionProbability(source, adversity, payArrears, MilitiaLoyaltyParams.Default);

        /// <summary>離反判定。roll∈[0,1) が離反確率未満なら離反＝true（決定論）。</summary>
        public static bool WillDefect(RecruitmentSource source, float adversity, float payArrears, float roll, MilitiaLoyaltyParams p)
        {
            return roll < DefectionProbability(source, adversity, payArrears, p);
        }

        public static bool WillDefect(RecruitmentSource source, float adversity, float payArrears, float roll)
            => WillDefect(source, adversity, payArrears, roll, MilitiaLoyaltyParams.Default);

        /// <summary>
        /// 愛国的動機による戦意ボーナス（0..1）。祖国が脅かされるほど（homelandThreat 0..1）市民軍の戦意が上がる＝防衛戦で真価。
        /// 志願兵は一部受け、徴集兵はわずか、傭兵は祖国と無関係＝0。
        /// </summary>
        public static float PatrioticMotivation(RecruitmentSource source, float homelandThreat, MilitiaLoyaltyParams p)
        {
            float threat = Mathf.Clamp01(homelandThreat);
            float affinity;
            switch (source)
            {
                case RecruitmentSource.市民軍: affinity = 1f; break;
                case RecruitmentSource.志願兵: affinity = 0.6f; break;
                case RecruitmentSource.徴集兵: affinity = 0.25f; break;
                case RecruitmentSource.傭兵: affinity = 0f; break; // 金で雇われた兵に祖国はない
                default: affinity = 0f; break;
            }
            return Mathf.Clamp01(threat * affinity * p.patriotismScale);
        }

        public static float PatrioticMotivation(RecruitmentSource source, float homelandThreat)
            => PatrioticMotivation(source, homelandThreat, MilitiaLoyaltyParams.Default);

        /// <summary>
        /// 徴募源のコスト効率（高いほど有利）。傭兵は平時に解散できる（peacetime＝true で軽い）が信頼できず、
        /// 市民軍は常在ながら安く（市民が自弁で武装＝祖国の負担が軽い）。戦時は傭兵の維持費が重くのしかかる。
        /// </summary>
        public static float CostEffectiveness(RecruitmentSource source, bool peacetime)
        {
            switch (source)
            {
                case RecruitmentSource.市民軍: return peacetime ? 0.9f : 0.95f; // 常在だが安く戦時も効率高い
                case RecruitmentSource.志願兵: return peacetime ? 0.7f : 0.8f;
                case RecruitmentSource.徴集兵: return peacetime ? 0.6f : 0.65f;
                case RecruitmentSource.傭兵: return peacetime ? 0.85f : 0.4f;  // 平時は解散でき身軽、戦時は維持費で効率激落
                default: return 0.5f;
            }
        }

        /// <summary>
        /// 公徳心の絆（0..1）。市民軍は公徳心（civicSpirit 0..1）と結びつき軍と国家が一体化する（マキャヴェッリの理想）。
        /// 志願兵は一部、徴集兵・傭兵はほぼ無関係＝公徳心は市民軍でこそ戦力に転じる。
        /// </summary>
        public static float CivicVirtueBond(RecruitmentSource source, float civicSpirit)
        {
            float c = Mathf.Clamp01(civicSpirit);
            switch (source)
            {
                case RecruitmentSource.市民軍: return c;          // 公徳心がそのまま絆になる
                case RecruitmentSource.志願兵: return c * 0.5f;
                case RecruitmentSource.徴集兵: return c * 0.15f;
                case RecruitmentSource.傭兵: return 0f;           // 金の兵に公徳心は宿らない
                default: return 0f;
            }
        }

        /// <summary>
        /// 傭兵の背信確率（0..1）。傭兵はより良い条件（betterOffer 0..1）で寝返る＝金の論理（<see cref="MercenaryRules"/> と整合）。
        /// 市民軍・志願兵・徴集兵は祖国に縛られ、好条件でも寝返らない＝0。
        /// </summary>
        public static float MercenaryPerfidy(RecruitmentSource source, float betterOffer, MilitiaLoyaltyParams p)
        {
            if (source != RecruitmentSource.傭兵) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(betterOffer) * p.perfidyScale);
        }

        public static float MercenaryPerfidy(RecruitmentSource source, float betterOffer)
            => MercenaryPerfidy(source, betterOffer, MilitiaLoyaltyParams.Default);

        /// <summary>
        /// 逆境に耐える信頼できる軍か＝逆境耐性が閾値以上。市民軍は満たしやすく、傭兵は逆境で崩れて満たせない。
        /// </summary>
        public static bool IsReliableForce(float adversityResilience, float threshold)
        {
            return Mathf.Clamp01(adversityResilience) >= Mathf.Clamp01(threshold);
        }

        public static bool IsReliableForce(float adversityResilience)
            => IsReliableForce(adversityResilience, MilitiaLoyaltyParams.Default.reliableThreshold);
    }
}
