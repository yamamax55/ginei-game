using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 省庁ツリーと官僚配属の純ロジック（GOV-5 #158・唯一の窓口）。省 ⊃ 庁/局 ⊃ 課 の木構造（単一親・循環防止）と、
    /// 官僚の<b>配属/異動</b>（定員順守・単一所属）、臨時省庁の<b>新設/廃止</b>（廃止時に配属を再配置）、
    /// 縦割りの<b>横断政策フリクション</b>（関与省庁の省益の平均）を扱う。別レジストリを乱立させず `List&lt;Ministry&gt;` 上で動く
    /// （所有は政府状態が持つ＝軍の <see cref="OrderOfBattle"/> と対称の文民版）。test-first。
    /// </summary>
    public static class MinistryRules
    {
        /// <summary>id の省庁を返す（無ければ null）。</summary>
        public static Ministry Get(List<Ministry> list, int id)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return list[i];
            return null;
        }

        /// <summary>子省庁を親に付け替える（単一親・循環防止＝子の子孫に親を入れない）。</summary>
        public static bool AttachChild(List<Ministry> list, int parentId, int childId)
        {
            if (parentId == childId) return false;
            Ministry parent = Get(list, parentId);
            Ministry child = Get(list, childId);
            if (parent == null || child == null) return false;
            if (IsDescendant(list, childId, parentId)) return false; // 循環防止

            // 旧親から外す（単一親）
            if (child.parentId >= 0)
            {
                Ministry old = Get(list, child.parentId);
                if (old != null) old.childIds.Remove(childId);
            }
            child.parentId = parentId;
            if (!parent.childIds.Contains(childId)) parent.childIds.Add(childId);
            return true;
        }

        /// <summary>子省庁を親から切り離す（最上位＝親なしになる）。</summary>
        public static bool DetachChild(List<Ministry> list, int parentId, int childId)
        {
            Ministry parent = Get(list, parentId);
            Ministry child = Get(list, childId);
            if (parent == null || child == null) return false;
            if (child.parentId != parentId) return false;
            parent.childIds.Remove(childId);
            child.parentId = -1;
            return true;
        }

        /// <summary>官僚を省庁へ配属する（定員順守・単一所属＝他省庁からは外す）。</summary>
        public static bool AssignOfficial(List<Ministry> list, int ministryId, int officialId)
        {
            Ministry m = Get(list, ministryId);
            if (m == null || !m.HasVacancy) return false;
            if (m.staffIds.Contains(officialId)) return false;

            // 単一所属：他省庁から外す
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id != ministryId) list[i].staffIds.Remove(officialId);

            m.staffIds.Add(officialId);
            return true;
        }

        /// <summary>配属官僚を外す。</summary>
        public static bool RemoveOfficial(List<Ministry> list, int ministryId, int officialId)
        {
            Ministry m = Get(list, ministryId);
            if (m == null) return false;
            return m.staffIds.Remove(officialId);
        }

        /// <summary>省庁間の異動（from→to。空きが無ければ失敗し元のまま）。</summary>
        public static bool Transfer(List<Ministry> list, int fromId, int toId, int officialId)
        {
            Ministry from = Get(list, fromId);
            Ministry to = Get(list, toId);
            if (from == null || to == null || !to.HasVacancy) return false;
            if (!from.staffIds.Contains(officialId)) return false;
            return AssignOfficial(list, toId, officialId); // 単一所属で from から自動的に外れる
        }

        /// <summary>root 以下（自身含む）の全配属官僚を再帰収集（重複なし）。</summary>
        public static List<int> AllOfficialsUnder(List<Ministry> list, int rootId)
        {
            var result = new List<int>();
            CollectOfficials(list, rootId, result);
            return result;
        }

        /// <summary>root 以下の配属官僚総数。</summary>
        public static int CountStaffUnder(List<Ministry> list, int rootId)
            => AllOfficialsUnder(list, rootId).Count;

        /// <summary>臨時省庁を新設してリストへ追加する（戦時・危機の集中投下）。</summary>
        public static Ministry CreateTemporary(List<Ministry> list, int id, string name, OfficeDomain domain, int staffSlots = 8)
        {
            if (list == null || Get(list, id) != null) return null;
            var m = new Ministry(id, name, domain) { isTemporary = true, staffSlots = staffSlots };
            list.Add(m);
            return m;
        }

        /// <summary>省庁を廃止する（配属官僚を reassignToId へ再配置。-1 なら解任のみ）。子は最上位へ昇格。</summary>
        public static bool Dissolve(List<Ministry> list, int ministryId, int reassignToId = -1)
        {
            Ministry m = Get(list, ministryId);
            if (m == null) return false;

            // 配属官僚の再配置
            var staff = new List<int>(m.staffIds);
            m.staffIds.Clear();
            if (reassignToId >= 0)
                foreach (int officialId in staff) AssignOfficial(list, reassignToId, officialId);

            // 子を最上位へ
            foreach (int childId in new List<int>(m.childIds))
            {
                Ministry child = Get(list, childId);
                if (child != null) child.parentId = -1;
            }
            // 親から外す
            if (m.parentId >= 0)
            {
                Ministry parent = Get(list, m.parentId);
                if (parent != null) parent.childIds.Remove(ministryId);
            }
            list.Remove(m);
            return true;
        }

        /// <summary>
        /// 縦割りの横断政策フリクション＝関与する省庁の省益の平均（0..1）。高いほど横断政策に抵抗する。
        /// タイクン化を避けるため「省益の駆け引き」を1つの係数に集約（#106 想定）。
        /// </summary>
        public static float SectionalismFriction(List<Ministry> list, IEnumerable<int> involvedMinistryIds)
        {
            if (involvedMinistryIds == null) return 0f;
            float sum = 0f; int n = 0;
            foreach (int id in involvedMinistryIds)
            {
                Ministry m = Get(list, id);
                if (m == null) continue;
                sum += Mathf.Clamp01(m.institutionalInterest);
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        // --- 内部 ---

        private static void CollectOfficials(List<Ministry> list, int id, List<int> acc)
        {
            Ministry m = Get(list, id);
            if (m == null) return;
            for (int i = 0; i < m.staffIds.Count; i++)
                if (!acc.Contains(m.staffIds[i])) acc.Add(m.staffIds[i]);
            for (int i = 0; i < m.childIds.Count; i++)
                CollectOfficials(list, m.childIds[i], acc);
        }

        /// <summary>candidateId が rootId の子孫か（循環防止用）。</summary>
        private static bool IsDescendant(List<Ministry> list, int rootId, int candidateId)
        {
            Ministry root = Get(list, rootId);
            if (root == null) return false;
            for (int i = 0; i < root.childIds.Count; i++)
            {
                if (root.childIds[i] == candidateId) return true;
                if (IsDescendant(list, root.childIds[i], candidateId)) return true;
            }
            return false;
        }
    }
}
