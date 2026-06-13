using UnityEngine;

namespace Ginei
{
    /// <summary>徴募の調整係数（L-4 #96 人口/徴募）。</summary>
    public readonly struct ConscriptionParams
    {
        /// <summary>生産年齢人口から徴募できる上限割合（これ以上は社会が回らない）。</summary>
        public readonly float maxDraftRatio;
        /// <summary>徴募が産出を削る強さ（働き手を兵に取られた分の経済ペナルティ係数）。</summary>
        public readonly float outputPenaltyScale;
        /// <summary>徴募が世論支持を削る強さ（徴兵は嫌われる）。</summary>
        public readonly float supportPenaltyScale;
        /// <summary>兵1人あたりに必要な生産年齢人口（人口→兵力の変換率の逆数）。</summary>
        public readonly float popPerStrength;

        public ConscriptionParams(float maxDraftRatio, float outputPenaltyScale, float supportPenaltyScale, float popPerStrength)
        {
            this.maxDraftRatio = Mathf.Clamp01(maxDraftRatio);
            this.outputPenaltyScale = Mathf.Max(0f, outputPenaltyScale);
            this.supportPenaltyScale = Mathf.Max(0f, supportPenaltyScale);
            this.popPerStrength = Mathf.Max(0.0001f, popPerStrength);
        }

        /// <summary>既定＝徴募上限20%・産出ペナルティ1.0・支持ペナルティ0.5・1兵力=人口1。</summary>
        public static ConscriptionParams Default => new ConscriptionParams(0.2f, 1f, 0.5f, 1f);
    }

    /// <summary>
    /// 徴募の純ロジック（L-4 #96）。生産年齢人口（<see cref="Population.working"/>＝徴募源）から兵力を引き出す。
    /// 徴募には上限があり（社会が回らなくなる）、引き出した分だけ産出が落ち・世論支持が削れる＝
    /// 大動員は勝っても国を痩せさせる。人口の時間動態は <see cref="DemographicsRules"/>、艦の補充は
    /// <see cref="FleetPool"/> が担い、ここは「人口→兵力」の変換と代償のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ConscriptionRules
    {
        /// <summary>徴募可能な最大兵力＝生産年齢人口×上限割合÷兵1人あたり人口。</summary>
        public static float DraftCapacity(Population pop, ConscriptionParams p)
        {
            if (pop == null) return 0f;
            return Mathf.Max(0f, pop.working) * p.maxDraftRatio / p.popPerStrength;
        }

        public static float DraftCapacity(Population pop) => DraftCapacity(pop, ConscriptionParams.Default);

        /// <summary>
        /// 徴募の実行。requested 兵力を求め、上限内で実際に徴募できた兵力を返し、その分の生産年齢人口を
        /// <see cref="Population.working"/> から差し引く（人口は実際に減る＝代償が残る）。
        /// </summary>
        public static float Draft(Population pop, float requested, ConscriptionParams p)
        {
            if (pop == null) return 0f;
            float granted = Mathf.Clamp(Mathf.Max(0f, requested), 0f, DraftCapacity(pop, p));
            pop.working = Mathf.Max(0f, pop.working - granted * p.popPerStrength);
            return granted;
        }

        public static float Draft(Population pop, float requested) => Draft(pop, requested, ConscriptionParams.Default);

        /// <summary>
        /// 徴募済み割合（0..1）＝徴募で抜けた働き手 draftedPop ÷ 元の生産年齢人口。産出・支持ペナルティの入力。
        /// </summary>
        public static float DraftedFraction(float draftedPop, float originalWorking)
        {
            if (originalWorking <= 0f) return draftedPop > 0f ? 1f : 0f;
            return Mathf.Clamp01(Mathf.Max(0f, draftedPop) / originalWorking);
        }

        /// <summary>徴募の産出倍率（0..1）＝働き手が抜けた分だけ産出が落ちる。生産係数に掛けて使う（基準非破壊）。</summary>
        public static float OutputFactor(float draftedFraction, ConscriptionParams p)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(draftedFraction) * p.outputPenaltyScale);
        }

        public static float OutputFactor(float draftedFraction) => OutputFactor(draftedFraction, ConscriptionParams.Default);

        /// <summary>徴募の世論支持ペナルティ（0..supportPenaltyScale）＝徴兵が深いほど嫌われる。</summary>
        public static float SupportPenalty(float draftedFraction, ConscriptionParams p)
        {
            return Mathf.Clamp01(draftedFraction) * p.supportPenaltyScale;
        }

        public static float SupportPenalty(float draftedFraction) => SupportPenalty(draftedFraction, ConscriptionParams.Default);

        /// <summary>
        /// 復員。兵力 strength を生産年齢人口へ戻す（講和・解隊で働き手が帰ってくる）。戻した人口を返す。
        /// </summary>
        public static float Demobilize(Population pop, float strength, ConscriptionParams p)
        {
            if (pop == null) return 0f;
            float returned = Mathf.Max(0f, strength) * p.popPerStrength;
            pop.working += returned;
            return returned;
        }

        public static float Demobilize(Population pop, float strength) => Demobilize(pop, strength, ConscriptionParams.Default);
    }
}
