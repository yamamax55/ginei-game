using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 政党の純ロジック（GOV-6 #159・最小選挙＋官僚の役職上限）。民主国家では政府の長＝<b>最大支持の党の党首</b>
    /// （任命でなく党勢 #113 で首班が決まる最小選挙）。あわせて「<b>官僚はトップに就けず、大臣以上は政治家</b>」を、
    /// 政治職役職を政治任用専用（<see cref="Office.politicalAppointmentOnly"/>）へ昇格させる形で表す（資格判定は
    /// <see cref="OfficeRules.CanHold"/> が読む）。党制（一党/複数政党/無党）は政体 #117/#145 が決める前提。test-first。
    /// </summary>
    public static class PartyRules
    {
        /// <summary>最大支持の党（同点や不在は最初の党。空なら null）＝民主国家の与党。</summary>
        public static Party RulingParty(IEnumerable<Party> parties)
        {
            if (parties == null) return null;
            Party best = null;
            float bestVal = float.NegativeInfinity;
            foreach (Party p in parties)
            {
                if (p == null) continue;
                if (p.support > bestVal) { bestVal = p.support; best = p; }
            }
            return best;
        }

        /// <summary>与党党首＝政府の長（最小選挙＝党勢で首班決定）。Person.id（無ければ -1）。</summary>
        public static int Premier(IEnumerable<Party> parties)
        {
            Party ruling = RulingParty(parties);
            return ruling != null ? ruling.leaderId : -1;
        }

        /// <summary>
        /// 民主国家（文民統制）で職業官僚の役職上限を適用する：政治系所掌（内政/外交/財政/元首）で
        /// 必要階級が <paramref name="careerCeilingTier"/> を超える役職を<b>政治任用専用</b>へ昇格させる。
        /// ＝官僚は事務次官級（ceiling）まで、その上（大臣・元首）は政治家のみ。民主派以外では何もしない（後方互換）。
        /// </summary>
        public static void MarkDemocraticAppointments(IEnumerable<Office> offices, int careerCeilingTier, CivilianControlType control)
        {
            if (offices == null) return;
            if (control != CivilianControlType.文民統制) return; // 民主国家のみ
            foreach (Office o in offices)
            {
                if (o == null) continue;
                if (o.domain == OfficeDomain.軍事) continue; // 軍は別系統（文民統制下でも軍人）
                if (o.requiredTier > careerCeilingTier)
                    o.politicalAppointmentOnly = true;
            }
        }
    }
}
