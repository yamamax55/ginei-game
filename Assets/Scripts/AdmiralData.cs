using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の能力データを保持する ScriptableObject。
    /// 銀河英雄伝説IV EX の能力値を踏襲します。
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
    }
}
