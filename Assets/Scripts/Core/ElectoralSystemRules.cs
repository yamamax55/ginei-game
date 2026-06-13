namespace Ginei
{
    /// <summary>
    /// 政体（<see cref="GovernmentForm"/> #117）が指導者・代表の<b>選び方</b>を決める純ロジック（選挙システム基盤・唯一の窓口）。
    /// <b>寡頭制（共産の集団指導・首長制の長老会）は少数による合議</b>（<see cref="CouncilRules"/>）、
    /// <b>民主政治（立憲君主制/共和制）は選挙</b>（<see cref="ElectionRules"/>＝党内/惑星/星系/勢力の各層 <see cref="ElectionTier"/>）、
    /// 君主制は世襲、指導者独裁は指名。民主か否かは既存 <see cref="GovernmentFormRules.IsDemocratic"/> を流用（二重定義しない）。test-first。
    /// </summary>
    public static class ElectoralSystemRules
    {
        /// <summary>民主政治で有効な選挙の層（党内/惑星/星系/勢力）。</summary>
        static readonly ElectionTier[] DemocraticTiers =
            { ElectionTier.党内, ElectionTier.惑星, ElectionTier.星系, ElectionTier.勢力 };

        static readonly ElectionTier[] NoTiers = new ElectionTier[0];

        /// <summary>政体 → 指導者の選び方。</summary>
        public static LeaderSelectionMode ModeFor(GovernmentForm form)
        {
            if (GovernmentFormRules.IsDemocratic(form)) return LeaderSelectionMode.選挙; // 立憲君主制/共和制
            switch (form)
            {
                case GovernmentForm.共産主義: return LeaderSelectionMode.合議; // 党中央の集団指導＝寡頭の合議
                case GovernmentForm.首長制:   return LeaderSelectionMode.合議; // 長老会＝部族の寡頭
                case GovernmentForm.指導者独裁: return LeaderSelectionMode.指名; // 個人独裁＝後継指名/簒奪
                default:                       return LeaderSelectionMode.世襲; // 君主制
            }
        }

        /// <summary>民主政治か（選挙で選ぶ）＝<see cref="GovernmentFormRules.IsDemocratic"/> と一致。</summary>
        public static bool IsElectoral(GovernmentForm form) => ModeFor(form) == LeaderSelectionMode.選挙;

        /// <summary>寡頭制か（少数による合議で選ぶ＝共産の集団指導/首長制の長老会）。</summary>
        public static bool IsOligarchic(GovernmentForm form) => ModeFor(form) == LeaderSelectionMode.合議;

        /// <summary>
        /// その政体で有効な選挙の層。民主政治は党内/惑星/星系/勢力の四層、それ以外は無し（選挙が存在しない）。
        /// 返り値は読み取り専用想定（共有配列を返す＝確保しない）。
        /// </summary>
        public static ElectionTier[] ActiveElectionTiers(GovernmentForm form)
            => IsElectoral(form) ? DemocraticTiers : NoTiers;

        /// <summary>その政体にこの選挙層が存在するか。</summary>
        public static bool HasTier(GovernmentForm form, ElectionTier tier)
        {
            if (!IsElectoral(form)) return false;
            return tier == ElectionTier.党内 || tier == ElectionTier.惑星
                || tier == ElectionTier.星系 || tier == ElectionTier.勢力;
        }
    }
}
