using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の能力データを保持する ScriptableObject。
    /// 銀河英雄伝説IV EX の能力値を踏襲します。
    ///
    /// 参謀（最大3名）を付けると、各能力を「参謀の最高値×staffBonusRatio」で補完した
    /// 実効能力を算出します（基準値=public フィールドは書き換えない＝実効値パターン）。
    /// ゲーム側は必ず Effectivexxx ゲッターを参照して実効能力を反映すること。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAdmiral", menuName = "Ginei/Admiral Data")]
    public class AdmiralData : ScriptableObject
    {
        [Header("基本情報")]
        public string admiralName = "提督名";
        public Faction faction;

        [Header("能力値 (0-100)")]
        [Tooltip("兵力上限・士気に影響")]
        public int leadership = 80;    // 統率

        [Tooltip("攻撃力に影響")]
        public int attack = 80;        // 攻撃

        [Tooltip("被ダメージ軽減に影響")]
        public int defense = 80;       // 防御

        [Tooltip("移動速度・回頭速度に影響")]
        public int mobility = 80;      // 機動

        [Tooltip("補給・コスト等に影響（将来用）")]
        public int operation = 80;     // 運営

        [Tooltip("索敵・回避等に影響（将来用）")]
        public int intelligence = 80;  // 情報

        [Header("艦隊設定")]
        [Tooltip("この提督が率いる際の基準兵力")]
        public int baseStrength = 10000;

        [Header("参謀（最大3名・能力補完）")]
        [Tooltip("能力を補完する参謀（最大3名・提督データを流用）。各能力は参謀の最高値×staffBonusRatio だけ底上げされる")]
        public AdmiralData[] staffOfficers = new AdmiralData[0];

        [Tooltip("参謀の能力が実効能力に寄与する割合 (0〜1)。例:0.2 なら参謀の能力の20%を補完")]
        [Range(0f, 1f)]
        public float staffBonusRatio = 0.2f;

        /// <summary>参謀の最大人数。</summary>
        public const int MaxStaff = 3;

        /// <summary>能力値の上限（補完してもこれを超えない）。</summary>
        public const int MaxStatValue = 100;

        // ===== 実効能力（基準値＋参謀補完。基準フィールドは非破壊）=====
        public int EffectiveLeadership   => ComputeEffective(leadership,   s => s.leadership);
        public int EffectiveAttack       => ComputeEffective(attack,       s => s.attack);
        public int EffectiveDefense      => ComputeEffective(defense,      s => s.defense);
        public int EffectiveMobility     => ComputeEffective(mobility,     s => s.mobility);
        public int EffectiveOperation    => ComputeEffective(operation,    s => s.operation);
        public int EffectiveIntelligence => ComputeEffective(intelligence, s => s.intelligence);

        /// <summary>
        /// 基準値に「参謀（最大MaxStaff名）の当該能力の最高値×staffBonusRatio」を加えた実効値を返す。
        /// 上限は MaxStatValue。基準フィールドは変更しない（実効値パターン）。
        /// </summary>
        private int ComputeEffective(int baseValue, Func<AdmiralData, int> selector)
        {
            int best = 0;
            int counted = 0;
            if (staffOfficers != null)
            {
                for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
                {
                    AdmiralData s = staffOfficers[i];
                    if (s == null || s == this) continue; // 空き枠・自己参照は無視
                    counted++;
                    int v = selector(s);
                    if (v > best) best = v;
                }
            }
            int bonus = Mathf.RoundToInt(best * staffBonusRatio);
            return Mathf.Clamp(baseValue + bonus, 0, MaxStatValue);
        }

        /// <summary>有効な参謀（非null）が1名以上いるか。</summary>
        public bool HasStaff
        {
            get
            {
                if (staffOfficers == null) return false;
                int counted = 0;
                for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
                {
                    AdmiralData s = staffOfficers[i];
                    if (s == null || s == this) continue;
                    return true;
                }
                return false;
            }
        }

        /// <summary>参謀名を「、」区切りで返す（最大MaxStaff名・HUD表示用）。参謀が無ければ空文字。</summary>
        public string GetStaffNames()
        {
            if (staffOfficers == null) return string.Empty;
            string result = string.Empty;
            int counted = 0;
            for (int i = 0; i < staffOfficers.Length && counted < MaxStaff; i++)
            {
                AdmiralData s = staffOfficers[i];
                if (s == null || s == this) continue;
                if (counted > 0) result += "、";
                result += s.admiralName;
                counted++;
            }
            return result;
        }
    }
}
