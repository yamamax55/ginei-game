using UnityEngine;

namespace Ginei
{
    /// <summary>人口局面（LIFE-3 #153）。生産年齢が厚い＝ボーナス／高齢化で従属が重い＝オーナス。</summary>
    public enum PopulationPhase { 人口ボーナス, 中立, 人口オーナス }

    /// <summary>
    /// 人口動態の純ロジック（LIFE-3 #153・唯一の窓口）。年齢コホート（<see cref="Population"/>）を出生・加齢・死亡で
    /// 更新し、<b>従属人口指数</b>＝(年少+高齢)/生産年齢 から<b>ボーナス/オーナス局面</b>を判定、生産(#93)・徴募(#96)・
    /// 安定度(#111)へ効く<b>産出係数</b>を返す（#106 パイプライン・実効値）。マクロな背景として効かせる（タイクン化回避）。test-first。
    /// </summary>
    public static class DemographicsRules
    {
        /// <summary>動態と局面しきい値の調整値。</summary>
        public readonly struct DemographicsParams
        {
            public readonly float bonusBelow;   // 従属指数がこれ未満＝人口ボーナス
            public readonly float onusAbove;     // 従属指数がこれ超＝人口オーナス
            public readonly float outputSwing;   // 局面が産出に与える振れ幅（±）

            public DemographicsParams(float bonusBelow, float onusAbove, float outputSwing)
            {
                this.bonusBelow = bonusBelow;
                this.onusAbove = onusAbove;
                this.outputSwing = Mathf.Max(0f, outputSwing);
            }

            /// <summary>既定＝従属0.5未満でボーナス・0.7超でオーナス・産出は±20%。</summary>
            public static DemographicsParams Default => new DemographicsParams(0.5f, 0.7f, 0.2f);
        }

        /// <summary>動態率の調整値（per-turn の割合）。</summary>
        public readonly struct VitalRates
        {
            public readonly float birthRate;     // 生産年齢に対する出生（→年少へ）
            public readonly float youthAging;     // 年少→生産年齢へ移る割合
            public readonly float workAging;      // 生産年齢→高齢へ移る割合
            public readonly float elderMortality; // 高齢の死亡割合

            public VitalRates(float birthRate, float youthAging, float workAging, float elderMortality)
            {
                this.birthRate = Mathf.Max(0f, birthRate);
                this.youthAging = Mathf.Clamp01(youthAging);
                this.workAging = Mathf.Clamp01(workAging);
                this.elderMortality = Mathf.Clamp01(elderMortality);
            }

            /// <summary>既定＝出生6%/年少→生産6.7%(15年で抜ける)/生産→高齢2%/高齢死亡8%。</summary>
            public static VitalRates Default => new VitalRates(0.06f, 1f / 15f, 0.02f, 0.08f);
        }

        /// <summary>従属人口指数＝(年少+高齢)/生産年齢（生産年齢0なら大きな値）。低いほど働き手が厚い。</summary>
        public static float DependencyRatio(Population p)
        {
            if (p == null) return 0f;
            if (p.working <= 0f) return p.Dependents > 0f ? 999f : 0f;
            return p.Dependents / p.working;
        }

        /// <summary>従属指数から局面を判定。</summary>
        public static PopulationPhase Phase(Population p, DemographicsParams prm)
        {
            float dr = DependencyRatio(p);
            if (dr < prm.bonusBelow) return PopulationPhase.人口ボーナス;
            if (dr > prm.onusAbove) return PopulationPhase.人口オーナス;
            return PopulationPhase.中立;
        }

        /// <summary>局面の産出係数（ボーナス＝1+swing／中立＝1／オーナス＝1-swing）。#106 で生産・徴募・税へ。</summary>
        public static float OutputFactor(Population p, DemographicsParams prm)
        {
            switch (Phase(p, prm))
            {
                case PopulationPhase.人口ボーナス: return 1f + prm.outputSwing;
                case PopulationPhase.人口オーナス: return 1f - prm.outputSwing;
                default: return 1f;
            }
        }

        /// <summary>
        /// 1ターン分の動態更新（出生→年少／年少→生産年齢→高齢の加齢／高齢の死亡）。<see cref="Population"/> を破壊的に更新。
        /// 大量増減（ベビーブーム/疫病/戦災 #116）は呼び出し側が率を差し替える。
        /// </summary>
        public static void Tick(Population p, VitalRates r)
        {
            if (p == null) return;
            float births = p.working * r.birthRate;
            float youthToWork = p.youth * r.youthAging;
            float workToElder = p.working * r.workAging;
            float elderDeaths = p.elderly * r.elderMortality;

            p.youth = Mathf.Max(0f, p.youth + births - youthToWork);
            p.working = Mathf.Max(0f, p.working + youthToWork - workToElder);
            p.elderly = Mathf.Max(0f, p.elderly + workToElder - elderDeaths);
        }
    }
}
