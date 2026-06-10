using UnityEngine;

namespace Ginei
{
    /// <summary>戦犯裁判の調整係数。</summary>
    public readonly struct TribunalParams
    {
        /// <summary>勝者の罪を裁かない法廷（勝者の裁き）が正統性に受ける割引率（0..1・残存率）。</summary>
        public readonly float victorJusticeDiscount;
        /// <summary>公正な裁きが被害社会に与える区切りの最大効果。</summary>
        public readonly float maxClosure;
        /// <summary>苛烈な量刑が殉教者を生むリスクの最大値。</summary>
        public readonly float maxMartyrdom;
        /// <summary>不処罰（大罪に寛大）が積む不満の最大量。</summary>
        public readonly float impunityScale;
        /// <summary>真実和解型（処罰より真相究明）が開く和解の最大効果。</summary>
        public readonly float reconciliationScale;

        public TribunalParams(float victorJusticeDiscount, float maxClosure, float maxMartyrdom,
                              float impunityScale, float reconciliationScale)
        {
            this.victorJusticeDiscount = Mathf.Clamp01(victorJusticeDiscount);
            this.maxClosure = Mathf.Max(0f, maxClosure);
            this.maxMartyrdom = Mathf.Clamp01(maxMartyrdom);
            this.impunityScale = Mathf.Max(0f, impunityScale);
            this.reconciliationScale = Mathf.Max(0f, reconciliationScale);
        }

        /// <summary>既定＝勝者裁き割引0.5・区切り0.5・殉教上限0.6・不処罰不満0.4・和解0.5。</summary>
        public static TribunalParams Default => new TribunalParams(0.5f, 0.5f, 0.6f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 戦犯裁判の純ロジック＝<see cref="AtrocityRules"/>（罪そのもの）の後段＝裁きの政治。
    /// 戦後の勝者の裁きは正義の執行と報復の間で揺れる：過酷なら遺恨（殉教者）を残し、寛大なら
    /// 不処罰の不満（大罪に寛大は被害者を二度殺す）を積む。**裁きの目的は復讐でなく区切り**＝
    /// 区切りの効果は「裁きの正統性×有罪率」で、勝者の罪だけ裁かれない法廷（復讐の劇場）は
    /// どれだけ裁いても過去を閉じきれない。人望ある被告の処刑は <see cref="MartyrdomRules"/>
    /// の死者の力を起動する逆効果。処罰の代わりに真相究明で閉じる真実和解型（南ア型）も選べる。
    /// 赦す側の力学（<see cref="AmnestyRules"/>）と対。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TribunalRules
    {
        /// <summary>
        /// 裁きの正統性（0..1）＝手続きの公正 dueProcess(0..1)。ただし勝者の罪を裁かない法廷
        /// （victorOnlyJustice）は victorJusticeDiscount まで割り引かれる＝勝者だけ裁かれない法廷は
        /// 正義でなく復讐の劇場と見られる。
        /// </summary>
        public static float PerceivedFairness(float dueProcess, bool victorOnlyJustice, TribunalParams p)
        {
            float fairness = Mathf.Clamp01(dueProcess);
            return victorOnlyJustice ? fairness * p.victorJusticeDiscount : fairness;
        }

        public static float PerceivedFairness(float dueProcess, bool victorOnlyJustice)
            => PerceivedFairness(dueProcess, victorOnlyJustice, TribunalParams.Default);

        /// <summary>
        /// 被害社会の区切り（0..maxClosure）＝裁きの正統性 fairness(0..1)×有罪率 convictionRate(0..1)。
        /// 裁きの目的は復讐でなく区切り＝不公正な法廷は全員を有罪にしても過去を閉じられない
        /// （fairness がゲート）。
        /// </summary>
        public static float ClosureEffect(float fairness, float convictionRate, TribunalParams p)
        {
            return Mathf.Clamp01(fairness) * Mathf.Clamp01(convictionRate) * p.maxClosure;
        }

        public static float ClosureEffect(float fairness, float convictionRate)
            => ClosureEffect(fairness, convictionRate, TribunalParams.Default);

        /// <summary>
        /// 殉教リスク（0..maxMartyrdom）＝量刑の苛烈さ severity(0..1)×被告の人望 defendantPopularity(0..1)。
        /// 人望ある被告の処刑は死者の力（<see cref="MartyrdomRules"/>）を起動する逆効果＝
        /// 無名の小悪を吊るすのは安いが、英雄を吊るすと遺恨が立つ。
        /// </summary>
        public static float MartyrdomRisk(float severity, float defendantPopularity, TribunalParams p)
        {
            return Mathf.Clamp01(severity) * Mathf.Clamp01(defendantPopularity) * p.maxMartyrdom;
        }

        public static float MartyrdomRisk(float severity, float defendantPopularity)
            => MartyrdomRisk(severity, defendantPopularity, TribunalParams.Default);

        /// <summary>
        /// 不処罰の不満（0..impunityScale）＝（1−有罪率）×罪の規模 atrocityScale(0..1)。
        /// 大罪に寛大は被害者を二度殺す＝罪が大きいほど無罪放免は許されない。
        /// 罪の規模は <see cref="AtrocityRules"/> の scale をそのまま入力できる。
        /// </summary>
        public static float ImpunityGrievance(float convictionRate, float atrocityScale, TribunalParams p)
        {
            return (1f - Mathf.Clamp01(convictionRate)) * Mathf.Clamp01(atrocityScale) * p.impunityScale;
        }

        public static float ImpunityGrievance(float convictionRate, float atrocityScale)
            => ImpunityGrievance(convictionRate, atrocityScale, TribunalParams.Default);

        /// <summary>
        /// 真実和解型の効果（0..reconciliationScale）＝裁きの正統性 fairness(0..1)×真相究明 truthDisclosure(0..1)。
        /// 処罰より真相究明で過去を閉じる南ア型の選択肢＝真実が語られ・場が公正であるほど和解が進む。
        /// 不公正な場での「真実」は告白ショーにしかならない（fairness がゲート）。
        /// </summary>
        public static float ReconciliationPath(float fairness, float truthDisclosure, TribunalParams p)
        {
            return Mathf.Clamp01(fairness) * Mathf.Clamp01(truthDisclosure) * p.reconciliationScale;
        }

        public static float ReconciliationPath(float fairness, float truthDisclosure)
            => ReconciliationPath(fairness, truthDisclosure, TribunalParams.Default);
    }
}
