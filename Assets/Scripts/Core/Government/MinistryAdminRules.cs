using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 省庁（<see cref="Ministry"/> ツリー）の行政効率→内政寄与の純ロジック（日本の律令制・官僚制基盤・配線ロジック）。
    /// 省の実力＝配下に配属された官僚（文民）の文才の平均×定員充足率。その寄与は<b>名実の乖離</b>で
    /// 朝廷の権威（<see cref="RitsuryoFormalizationRules.OfficeAuthorityFactor"/>）ぶん減衰する＝権威を失った朝廷の
    /// 省庁は人を揃えても実効を持たない。宰相（<see cref="AdministrationRules"/>）の機構版。基準値非破壊・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MinistryAdminRules
    {
        /// <summary>省庁効率の調整値。</summary>
        public readonly struct MinistryParams
        {
            public readonly float maxBonus; // 満充足・満権威・満能力での最大寄与

            public MinistryParams(float maxBonus) { this.maxBonus = maxBonus; }

            /// <summary>既定＝最大+8（安定度ポイント相当）。</summary>
            public static MinistryParams Default => new MinistryParams(8f);
        }

        /// <summary>root 配下（root＋子孫）の定員合計。</summary>
        public static int SlotsUnder(Ministry root, List<Ministry> tree)
        {
            if (root == null) return 0;
            int total = Mathf.Max(0, root.staffSlots);
            if (root.childIds != null)
                for (int i = 0; i < root.childIds.Count; i++)
                    total += SlotsUnder(MinistryRules.Get(tree, root.childIds[i]), tree);
            return total;
        }

        /// <summary>
        /// 省庁の行政効率（0..1）＝配下官僚の平均文才(0..1) × 定員充足率(配属数/定員)。官僚不在は0。
        /// </summary>
        public static float StaffingEfficiency(Ministry root, List<Ministry> tree, Func<int, Person> personOf)
        {
            if (root == null) return 0f;
            List<int> officials = MinistryRules.AllOfficialsUnder(tree, root.id);
            if (officials == null || officials.Count == 0) return 0f;

            float sum = 0f; int n = 0;
            for (int i = 0; i < officials.Count; i++)
            {
                Person p = personOf != null ? personOf(officials[i]) : null;
                if (p == null) continue;
                sum += Mathf.Clamp01(p.CivilAptitude / 100f);
                n++;
            }
            if (n == 0) return 0f;
            float avg = sum / n;

            int slots = SlotsUnder(root, tree);
            float fill = slots > 0 ? Mathf.Clamp01((float)officials.Count / slots) : 0f;
            return Mathf.Clamp01(avg) * fill;
        }

        /// <summary>
        /// 省庁の内政寄与＝行政効率 × 朝廷の権威（名実の乖離で減衰） × 上限。権威0で寄与0（機構が形骸化）。
        /// </summary>
        public static float AdministrativeBonus(Ministry root, List<Ministry> tree, Func<int, Person> personOf,
                                                float courtAuthority, MinistryParams p)
            => StaffingEfficiency(root, tree, personOf)
               * RitsuryoFormalizationRules.OfficeAuthorityFactor(courtAuthority) * p.maxBonus;
    }
}
