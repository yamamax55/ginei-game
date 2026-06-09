using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 外交状態（外交EPIC #189・DIP-1 の入口）。勢力ペアごとの<b>関係値(opinion)</b>と<b>外交状態(<see cref="DiplomaticStatus"/>)</b>を保持する純データ。
    /// 敵対判定の唯一の窓口 <see cref="FactionRelations.IsHostile(FactionData, Faction, FactionData, Faction)"/> を駆動する
    /// （交戦→敵対／同盟・不可侵・属国→非敵対／平時→従来の enum/FactionData 判定にフォールバック＝後方互換）。
    /// 状態遷移・opinion 修正子の計算は <see cref="DiplomacyRules"/> が唯一の窓口。直列化可（FND-2 #495 = JsonUtility）。test-first。
    /// </summary>
    [System.Serializable]
    public class DiplomacyState
    {
        /// <summary>勢力ペアの外交状態。平時＝条約無し（敵対は従来判定に委ねる）。</summary>
        public enum DiplomaticStatus
        {
            平時,    // 条約なし＝FactionRelations の従来判定にフォールバック
            同盟,    // 共同交戦・非敵対
            不可侵,  // 非敵対（協力義務は無し）
            属国,    // 従属関係・非敵対
            交戦,    // 宣戦布告済み＝敵対
        }

        /// <summary>1 ペア分のレコード（勢力名キー・正規化して保持）。</summary>
        [System.Serializable]
        public class Entry
        {
            public string factionA = "";
            public string factionB = "";
            public float opinion;                 // -100..100（負＝険悪／正＝良好）
            public DiplomaticStatus status = DiplomaticStatus.平時;
        }

        [Tooltip("勢力ペアごとの外交レコード")]
        public List<Entry> entries = new List<Entry>();

        // ===== キー正規化（無向ペア＝名前順で一意化） =====

        private static void Normalize(ref string a, ref string b)
        {
            if (string.CompareOrdinal(a, b) > 0) { var t = a; a = b; b = t; }
        }

        /// <summary>ペアのレコードを取得（create=true なら無ければ生成）。同名・空名は null。</summary>
        public Entry GetEntry(string a, string b, bool create = false)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b) return null;
            Normalize(ref a, ref b);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.factionA == a && e.factionB == b) return e;
            }
            if (!create) return null;
            var added = new Entry { factionA = a, factionB = b };
            entries.Add(added);
            return added;
        }

        /// <summary>ペアの外交状態（レコードが無ければ平時）。</summary>
        public DiplomaticStatus Status(string a, string b)
        {
            var e = GetEntry(a, b);
            return e != null ? e.status : DiplomaticStatus.平時;
        }

        /// <summary>ペアの関係値（レコードが無ければ 0）。</summary>
        public float Opinion(string a, string b)
        {
            var e = GetEntry(a, b);
            return e != null ? e.opinion : 0f;
        }
    }
}
