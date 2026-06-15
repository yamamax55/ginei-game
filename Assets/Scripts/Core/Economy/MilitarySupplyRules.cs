using UnityEngine;

namespace Ginei
{
    /// <summary>部隊の活動状態（MILSUP-1・#2049）。待機＝糧食中心／移動＝燃料消費／交戦＝弾薬消費。</summary>
    public enum MilitaryActivity { 待機, 移動, 交戦 }

    /// <summary>
    /// 軍需物資カテゴリと需要原単位（MILSUP-1・#2049・純ロジック・唯一の窓口）。
    /// 既存 <see cref="ResourceType"/>{物資/弾薬/燃料} を軍需物資として流用。兵力あたりの原単位は<b>活動状態</b>で変わる
    /// （弾薬は交戦で激増・燃料は移動で増・物資は常時の糧食）。集約・lookup。test-first。
    /// </summary>
    public static class MilitarySupplyRules
    {
        // 兵力あたりの消費原単位 [ResourceType 物資/弾薬/燃料][MilitaryActivity 待機/移動/交戦]。唯一の出所。
        private static readonly float[][] rates =
        {
            //          待機    移動    交戦
            new[] { 0.10f, 0.10f, 0.12f }, // 物資（糧食）＝常時ほぼ一定
            new[] { 0.02f, 0.05f, 0.50f }, // 弾薬＝交戦で激増
            new[] { 0.05f, 0.30f, 0.15f }, // 燃料＝移動で増
        };

        /// <summary>兵力あたりの消費原単位（カテゴリ×活動）。</summary>
        public static float UpkeepRate(ResourceType type, MilitaryActivity activity)
            => rates[(int)type][(int)activity];

        /// <summary>1部隊の需要＝兵力×原単位（カテゴリ×活動）。</summary>
        public static float Upkeep(float strength, ResourceType type, MilitaryActivity activity)
            => Mathf.Max(0f, strength) * UpkeepRate(type, activity);
    }
}
