using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>家禄と役高のペア（#1975 足し高の制・純データ）。ある人物をある役職に就けるときの世襲俸禄と役職俸禄。</summary>
    public readonly struct TashidakaAppointment
    {
        /// <summary>家禄（世襲の俸禄＝恒久）。</summary>
        public readonly float hereditaryStipend;

        /// <summary>役高（役職が要求する俸禄）。</summary>
        public readonly float officeStipend;

        public TashidakaAppointment(float hereditaryStipend, float officeStipend)
        {
            this.hereditaryStipend = hereditaryStipend;
            this.officeStipend = officeStipend;
        }
    }

    /// <summary>
    /// 足し高の制のロジック（#1975・純ロジック・唯一の窓口）。徳川吉宗の制度を参考に、<b>家禄（世襲＝恒久）</b>と
    /// <b>役高（役職が要求する俸禄）</b>を分け、家禄が役高に届かない人材でも<b>在職中だけ不足分（足し高）を国庫が補填</b>して
    /// 高い役職に就ける。退任すれば足し高は消え家禄に戻る（家禄を恒久的に上げない＝実効値パターン・基準非破壊）。
    /// 俸禄の壁を外して<b>低禄の俊英を実力で登用</b>でき（適材適所 #866・席次vs実力 #155）、登用コストを在職期間に限定する。
    /// 俸給 <see cref="WageRules"/>(#1969)・役職 <see cref="Office"/>(GOV-1)・財政(#163)へ接続（read-only/接続のみ）。test-first。
    /// </summary>
    public static class TashidakaRules
    {
        // ===== 足し高と実効俸禄 =====

        /// <summary>足し高＝役高−家禄（在職中だけ補填する不足分。家禄が役高以上なら0）。</summary>
        public static float Supplement(float hereditaryStipend, float officeStipend)
            => Mathf.Max(0f, officeStipend - hereditaryStipend);

        /// <summary>在職中の実効俸禄＝家禄＋足し高＝max(家禄, 役高)（役職に見合う俸禄を受け取る）。</summary>
        public static float EffectiveStipend(float hereditaryStipend, float officeStipend)
            => Mathf.Max(0f, hereditaryStipend) + Supplement(hereditaryStipend, officeStipend);

        /// <summary>退任後の俸禄＝家禄に戻る（足し高は消える＝家禄を恒久的に上げない・基準非破壊）。</summary>
        public static float RevertedStipend(float hereditaryStipend)
            => Mathf.Max(0f, hereditaryStipend);

        // ===== 就任資格（俸禄の壁） =====

        /// <summary>旧制：家禄が役高に達していないと就任できない（俸禄の壁＝低禄は高位に就けない）。</summary>
        public static bool CanServeTraditional(float hereditaryStipend, float officeStipend)
            => hereditaryStipend >= officeStipend;

        /// <summary>足し高の制：家禄に関わらず就任できる（俸禄の壁を撤廃＝就任ゲートは実力#155/#866へ移る）。</summary>
        public static bool CanServeWithTashidaka(float hereditaryStipend, float officeStipend)
            => true;

        /// <summary>旧制で家禄ゆえに排除される人材か（低禄の俊英＝足し高の制が救う対象）。</summary>
        public static bool ExcludedByStipend(float hereditaryStipend, float officeStipend)
            => hereditaryStipend < officeStipend;

        // ===== 国庫の負担と節約 =====

        /// <summary>在職期間ぶんの足し高負担＝足し高×在職年数（在職中だけ払う）。</summary>
        public static float TenureSupplementCost(TashidakaAppointment a, float tenureYears)
            => Supplement(a.hereditaryStipend, a.officeStipend) * Mathf.Max(0f, tenureYears);

        /// <summary>恒久昇禄にした場合の費用＝足し高×期間（退任後・世襲後もずっと払い続ける比較対象）。</summary>
        public static float PermanentRaiseCost(TashidakaAppointment a, float horizonYears)
            => Supplement(a.hereditaryStipend, a.officeStipend) * Mathf.Max(0f, horizonYears);

        /// <summary>
        /// 足し高の制が国庫に与える節約＝恒久昇禄費−在職補填費＝足し高×max(0, 期間−在職)。
        /// 退任後の負担を負わないぶんだけ安い（人材登用コストを在職に限定する財政の妙）。
        /// </summary>
        public static float Savings(TashidakaAppointment a, float tenureYears, float horizonYears)
            => Supplement(a.hereditaryStipend, a.officeStipend) * Mathf.Max(0f, horizonYears - tenureYears);

        /// <summary>複数任用の足し高総額（国庫の人件費＝歳出 #163 に乗る）。</summary>
        public static float TotalSupplementCost(IReadOnlyList<TashidakaAppointment> appointments)
        {
            if (appointments == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < appointments.Count; i++)
                sum += Supplement(appointments[i].hereditaryStipend, appointments[i].officeStipend);
            return sum;
        }

        // ===== 階級版（俸給 #1969 と接続） =====

        /// <summary>役高＝役職の必要階級から金額化（<see cref="WageRules.RankBasePay"/> #1969）。</summary>
        public static float OfficeStipend(int officeTier, PayScale scale)
            => WageRules.RankBasePay(officeTier, scale);

        /// <summary>家禄＝人物の世襲階級から金額化（<see cref="WageRules.RankBasePay"/>）。</summary>
        public static float HereditaryStipend(int hereditaryTier, PayScale scale)
            => WageRules.RankBasePay(hereditaryTier, scale);

        /// <summary>足し高（階級版）＝役職階級と世襲階級の俸禄差。低い家禄tierで高い役職tierに就く不足を補う。</summary>
        public static float SupplementForTier(int hereditaryTier, int officeTier, PayScale scale)
            => Supplement(HereditaryStipend(hereditaryTier, scale), OfficeStipend(officeTier, scale));
    }
}
