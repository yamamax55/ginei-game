using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP の技能ストック（SKILL-1・#2034・純データ）。惑星×職業（少数6種<see cref="Occupation"/>）の<b>集約</b>熟練度（0..1）。
    /// 個体粒度へ降りない（pop個別の技能シミュは作らない＝集約・タイクン回避）。基準＝教育普及/質（<see cref="SkillEducationRules"/>）。
    /// 形成は教育・職業訓練・OJT・リスキリング（#2034）が動かす。Province には任意で保持（null=既定で見積り＝後方互換）。
    /// </summary>
    [System.Serializable]
    public class SkillStock
    {
        public const int Count = 6; // Occupation の要素数
        public float[] levels = new float[Count]; // 職業別の熟練度（0..1）

        public SkillStock() { }

        public SkillStock(float[] src)
        {
            levels = new float[Count];
            if (src != null)
                for (int i = 0; i < Count && i < src.Length; i++) levels[i] = Mathf.Clamp01(src[i]);
        }

        public float Level(Occupation o) => levels[(int)o];

        public void SetLevel(Occupation o, float v) => levels[(int)o] = Mathf.Clamp01(v);

        /// <summary>就労シェア重みで加重した平均熟練度（惑星の総合技能・観測/集計用）。</summary>
        public float WeightedAverage(Workforce w)
        {
            if (w == null) return 0f;
            float sum = 0f, tot = 0f;
            for (int i = 0; i < Count; i++)
            {
                if (i == (int)Occupation.無職) continue;
                sum += levels[i] * w.shares[i];
                tot += w.shares[i];
            }
            return tot <= 0f ? 0f : Mathf.Clamp01(sum / tot);
        }
    }
}
