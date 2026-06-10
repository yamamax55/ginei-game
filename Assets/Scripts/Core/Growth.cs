using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の成長アーキタイプ（#537-543）。経験の蓄積を実効能力ボーナスへ写す純データ。
    /// 「どう伸びるか」をアーキタイプで表す＝首席型は早咲き・在野俊英型は高天井で出世遅・
    /// 老練型は晩成・叩き上げは初期低で希少。基準能力は持たず experience だけ蓄える（実効値パターン）。
    /// </summary>
    public enum GrowthArchetype
    {
        首席型,     // 初期補正が高く昇進も早い（士官学校トップ）
        在野俊英型, // 高天井だが昇進適性が低い（出世が遅い俊英）
        老練型,     // 晩成。成長は遅いがピーク・天井が高く長期で高位
        叩き上げ    // 初期は低く成長も控えめ（希少な現場上がり）
    }

    /// <summary>
    /// 提督の成長状態の純データ。累積経験 <see cref="experience"/> と成長アーキタイプを持つ。
    /// 基準能力は別（<c>AdmiralData</c>）に持ち、ここはその上に乗る経験だけを蓄える（基準非破壊）。
    /// </summary>
    [System.Serializable]
    public class Growth
    {
        /// <summary>累積経験（0以上。会戦などで時間発展して増える）。</summary>
        public float experience = 0f;

        /// <summary>成長アーキタイプ（伸び方の型）。</summary>
        public GrowthArchetype archetype = GrowthArchetype.叩き上げ;

        public Growth() { }

        public Growth(GrowthArchetype archetype, float experience = 0f)
        {
            this.archetype = archetype;
            this.experience = Mathf.Max(0f, experience);
        }
    }
}
