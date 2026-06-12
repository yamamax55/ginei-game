using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP の年齢コホート（LIFE-3 #153・人口ピラミッドの器）。3区分＝年少(0-14)/生産年齢(15-64)/高齢(65+)。
    /// 出生・加齢・死亡で時間更新し、従属人口指数から<b>人口ボーナス/オーナス</b>を生む。動態の解決は
    /// <see cref="DemographicsRules"/> が唯一の窓口。所有勢力は星系/勢力側が持つ（ここには持たない）。純データ。
    /// </summary>
    [System.Serializable]
    public class Population
    {
        /// <summary>年少人口（0-14・従属）。</summary>
        public float youth;

        /// <summary>生産年齢人口（15-64・働き手・徴募源）。</summary>
        public float working;

        /// <summary>高齢人口（65+・従属）。</summary>
        public float elderly;

        /// <summary>女性の割合（0..1・マクロな男女比。既定0.5＝均衡）。男性割合は 1-femaleShare。解決は <see cref="SexRules"/>。</summary>
        public float femaleShare = SexRules.BalancedFemaleShare;

        public Population() { }

        public Population(float youth, float working, float elderly)
        {
            this.youth = youth;
            this.working = working;
            this.elderly = elderly;
        }

        /// <summary>総人口。</summary>
        public float Total => youth + working + elderly;

        /// <summary>従属人口（年少＋高齢）。</summary>
        public float Dependents => youth + elderly;

        /// <summary>女性の総数（総人口×女性割合）。</summary>
        public float Females => Total * Mathf.Clamp01(femaleShare);

        /// <summary>男性の総数（総人口×男性割合）。</summary>
        public float Males => Total * (1f - Mathf.Clamp01(femaleShare));
    }
}
