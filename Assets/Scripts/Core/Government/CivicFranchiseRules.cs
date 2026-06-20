using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍功市民制（STSH-1 #2353・static Core・唯一の窓口）。「兵役を果たした者だけが参政権を持つ」を
    /// 純ロジックで表す＝完遂した役務（<see cref="ServiceRecord"/>）が<b>参政権</b>（投票・就任の権利）を授ける。
    /// 既存の全市民均等参加（<see cref="PartyRules"/>）に参政権の条件分岐を足し、フランチャイズ比率が
    /// <see cref="ConsentRules.ControlStrength"/>（実効統治力）と政治正統性に効くループを確立する。
    /// 役職就任の可否は <see cref="OfficeRules.CanHold"/> へ委譲（ここは「市民か」だけを足す）。
    /// <see cref="MeritRankRules"/>（軍内昇進）と異なり、これは<b>民政参加権</b>を扱う。test-first。
    /// </summary>
    public static class CivicFranchiseRules
    {
        /// <summary>参政権に必要な最低服役年数（これ未満は無資格）。</summary>
        public const int MinServiceYears = 2;
        /// <summary>フランチャイズ比率→正統性の最大寄与（比率1.0で +この値）。</summary>
        public const float LegitimacyWeight = 0.5f;
        /// <summary>サポート修正子の最大幅（比率0で −、比率1で +この振れ幅／2を中心0へ寄せる）。</summary>
        public const float SupportSwing = 0.2f;

        /// <summary>
        /// このレコードが参政権を満たすか（完遂した役務のみが市民権を授ける）。
        /// ＝名誉除隊かつ最低年数以上。null/不名誉/不足は false。
        /// </summary>
        public static bool IsEnfranchised(ServiceRecord rec)
        {
            if (rec == null) return false;
            if (!rec.honorableDischarge) return false;
            return rec.yearsServed >= MinServiceYears;
        }

        /// <summary>
        /// 参政権の付与を確定する（<see cref="ServiceRecord.isEnfranchised"/> を判定結果で更新し返す）。
        /// 服役政策が生成したレコードに対し呼ぶ supply 側の確定窓口。
        /// </summary>
        public static bool Enfranchise(ServiceRecord rec)
        {
            if (rec == null) return false;
            bool ok = IsEnfranchised(rec);
            rec.isEnfranchised = ok;
            return ok;
        }

        /// <summary>
        /// 指定勢力の有権者数（参政権を満たすレコード数）。null/空は0。
        /// </summary>
        public static int EnfranchisedCount(IEnumerable<ServiceRecord> records, int factionId)
        {
            if (records == null) return 0;
            int n = 0;
            foreach (ServiceRecord r in records)
            {
                if (r == null || r.factionId != factionId) continue;
                if (IsEnfranchised(r)) n++;
            }
            return n;
        }

        /// <summary>
        /// フランチャイズ比率＝有権者数／成人市民総数（0..1）。総数0以下は0（ゼロ除算回避）。
        /// 比率が高いほど広い参政＝民主的正統性が厚い。
        /// </summary>
        public static float FranchiseShare(int enfranchised, int totalCitizens)
        {
            if (totalCitizens <= 0) return 0f;
            int e = enfranchised < 0 ? 0 : enfranchised;
            return Mathf.Clamp01((float)e / totalCitizens);
        }

        /// <summary>records から比率を導く版（成人市民総数を別に与える）。</summary>
        public static float FranchiseShare(IEnumerable<ServiceRecord> records, int factionId, int totalCitizens)
            => FranchiseShare(EnfranchisedCount(records, factionId), totalCitizens);

        /// <summary>
        /// フランチャイズ比率→正統性寄与（0..<see cref="LegitimacyWeight"/>）。
        /// 参政が広いほど統治の正統性が上がる（<see cref="ConsentRules"/> の協力回復に効く想定）。
        /// </summary>
        public static float LegitimacyFromFranchise(float franchiseShare)
            => Mathf.Clamp01(franchiseShare) * LegitimacyWeight;

        /// <summary>
        /// 参政権保持者の市民票に対するサポート修正子（<see cref="PartyRules.Support"/> 連携用・−SupportSwing/2..+SupportSwing/2）。
        /// 比率0.5を中心（修正子0）に、広い参政で＋・狭い参政で−。クランプ済み。
        /// </summary>
        public static float SupportModifier(float franchiseShare)
        {
            float s = Mathf.Clamp01(franchiseShare);
            return (s - 0.5f) * SupportSwing;
        }

        /// <summary>
        /// 市民（参政権保持者）かつ役職資格を満たすか＝就任可否（OfficeRules へ委譲＋市民ゲート）。
        /// 非市民（未服役/不名誉/null レコード）は無条件で就けない（軍功市民制の核）。
        /// </summary>
        public static bool CanHoldOffice(ServiceRecord rec, ICharacter c, Office o)
        {
            if (!IsEnfranchised(rec)) return false;
            return OfficeRules.CanHold(c, o);
        }

        /// <summary>
        /// 投票資格＝参政権を満たすか（<see cref="IsEnfranchised"/> の別名・読み手の意図を明示）。
        /// </summary>
        public static bool CanVote(ServiceRecord rec) => IsEnfranchised(rec);
    }
}
