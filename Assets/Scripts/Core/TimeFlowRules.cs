using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 暦時間の流速＝自動スロー（Paradox 風）の純ロジック（TIME-7 #959）。戦略マップ平時は暦を速く流し（年が分単位）、
    /// 「観るべき瞬間」（会戦の生起・前線突入など salient）では実時間へ自動減速する。暦の累積秒を進める<b>レート倍率</b>を
    /// 返す唯一の窓口。実時間アクション（艦隊移動・自動解決）の速さとは独立＝暦だけ伸縮する。純ロジック・test-first。
    /// </summary>
    public static class TimeFlowRules
    {
        /// <summary>暦流速の調整値（平時の圧縮倍率・観戦時の倍率・遷移の滑らかさ）。</summary>
        public readonly struct TimeFlowParams
        {
            /// <summary>平時：暦が実時間の何倍で流れるか（&gt;1 で圧縮＝速い）。</summary>
            public readonly float fastCompression;
            /// <summary>観るべき瞬間：ほぼ実時間（=1）。0以上。</summary>
            public readonly float slowCompression;
            /// <summary>1秒あたりの倍率変化（MoveTowards・急変を避ける）。</summary>
            public readonly float easeRate;

            public TimeFlowParams(float fastCompression, float slowCompression, float easeRate)
            {
                this.fastCompression = Mathf.Max(1f, fastCompression);
                this.slowCompression = Mathf.Max(0f, slowCompression);
                this.easeRate = Mathf.Max(0f, easeRate);
            }

            /// <summary>既定：平時30倍（1日=60sなら実2秒で1日＝1年≈12分）・観戦時1倍（実時間）・遷移8/秒。</summary>
            public static TimeFlowParams Default => new TimeFlowParams(30f, 1f, 8f);
        }

        /// <summary>観るべき瞬間（salient）なら slow、平時なら fast の<b>目標</b>倍率を返す。</summary>
        public static float TargetCompression(bool salient, TimeFlowParams p)
            => salient ? p.slowCompression : p.fastCompression;

        /// <summary>
        /// 現在倍率を目標へ滑らかに寄せる（MoveTowards・dt 基準＝フレームレート非依存）。dt&lt;=0 は現状維持。
        /// 急に暦が飛ぶ/止まるのを避け、減速・再加速をなめらかにする。
        /// </summary>
        public static float Ease(float current, float target, TimeFlowParams p, float dt)
        {
            if (dt <= 0f) return current;
            return Mathf.MoveTowards(current, target, p.easeRate * dt);
        }
    }
}
