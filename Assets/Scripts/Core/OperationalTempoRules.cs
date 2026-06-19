using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 作戦テンポ（OODAループ）の調整値。意思決定サイクルの基準時間・複雑さの寄与・効率下限（ゼロ除算回避）・
    /// 速すぎるテンポの負担指数・敵テンポを上回る余裕。符号付き指標は -1..1。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct OperationalTempoParams
    {
        /// <summary>意思決定サイクルの基準所要時間（効率1・複雑さ0のときの素の1サイクル時間）。</summary>
        public readonly float baseCycleTime;
        /// <summary>状況の複雑さがサイクル時間を伸ばす寄与（複雑さあたりの増分倍率）。</summary>
        public readonly float complexityWeight;
        /// <summary>指揮効率の下限（これ未満として扱わない＝ゼロ除算・発散回避）。</summary>
        public readonly float minEfficiency;
        /// <summary>速すぎるテンポ→補給負担の指数（2.0＝二乗的に効く＝無理押しは急に苦しくなる）。</summary>
        public readonly float strainExponent;
        /// <summary>敵テンポを上回る余裕（最適テンポは敵テンポ×この比を狙う＝1.2＝2割速く回す）。</summary>
        public readonly float tempoMargin;

        public OperationalTempoParams(float baseCycleTime, float complexityWeight, float minEfficiency,
            float strainExponent, float tempoMargin)
        {
            this.baseCycleTime = Mathf.Max(0.01f, baseCycleTime);
            this.complexityWeight = Mathf.Clamp(complexityWeight, 0f, 4f);
            this.minEfficiency = Mathf.Clamp(minEfficiency, 0.01f, 1f);
            this.strainExponent = Mathf.Clamp(strainExponent, 0.5f, 4f);
            this.tempoMargin = Mathf.Clamp(tempoMargin, 1f, 3f);
        }

        /// <summary>既定：基準サイクル2.0・複雑さ寄与1.0・効率下限0.1・負担指数2.0（二乗）・テンポ余裕1.2。</summary>
        public static OperationalTempoParams Default => new OperationalTempoParams(
            DefaultBaseCycleTime, DefaultComplexityWeight, DefaultMinEfficiency,
            DefaultStrainExponent, DefaultTempoMargin);

        public const float DefaultBaseCycleTime = 2.0f;
        public const float DefaultComplexityWeight = 1.0f;
        public const float DefaultMinEfficiency = 0.1f;
        public const float DefaultStrainExponent = 2.0f;
        public const float DefaultTempoMargin = 1.2f;
    }

    /// <summary>
    /// 作戦テンポの純ロジック（唯一の窓口）＝<b>OODAループ（観察→判断→決定→行動）の速さで主導権を握る</b>。
    /// 敵より速く意思決定・行動を回す側が相手を後手に回らせ（混乱させ）主導権を握る。だが速すぎると消耗・補給が
    /// 追いつかない（トレードオフ）。テンポは「1サイクルの所要時間の逆数的な速さ＝小さいサイクル時間ほど速い」。
    /// <b>分担</b>：会戦の瞬間的テンポ（もしあれば <c>BattleTempoRules</c>）とは別＝こちらは<b>作戦・戦略レベル</b>の
    /// OODAサイクル。<see cref="TimeFlowRules"/>（暦の伸縮・自動スロー）とも別＝あちらは時間表示の圧縮、こちらは
    /// 意思決定速度の優劣。盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class OperationalTempoRules
    {
        /// <summary>既定パラメータで意思決定サイクルの所要時間。</summary>
        public static float DecisionCycleTime(float commandEfficiency, float situationComplexity)
            => DecisionCycleTime(commandEfficiency, situationComplexity, OperationalTempoParams.Default);

        /// <summary>
        /// 意思決定サイクル（OODA一周）の所要時間。指揮効率が高いほど短く、状況が複雑なほど長い。
        /// time＝基準×(1＋複雑さ寄与×複雑さ)÷効率。短いほど「テンポが速い」（後段の引数はこの time）。
        /// </summary>
        public static float DecisionCycleTime(float commandEfficiency, float situationComplexity, OperationalTempoParams p)
        {
            float eff = Mathf.Max(p.minEfficiency, Mathf.Clamp01(commandEfficiency));
            float complexity = Mathf.Max(0f, situationComplexity);
            return p.baseCycleTime * (1f + p.complexityWeight * complexity) / eff;
        }

        /// <summary>
        /// 敵より速いサイクルの優位（-1..1）。自サイクルが短い（速い）ほど正。
        /// adv＝(敵time−自time)/(敵time＋自time)。拮抗0／自分が倍速で約+0.33／敵が倍速で約-0.33。
        /// </summary>
        public static float TempoAdvantage(float ownCycleTime, float enemyCycleTime)
        {
            float own = Mathf.Max(0f, ownCycleTime);
            float enemy = Mathf.Max(0f, enemyCycleTime);
            float denom = own + enemy;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp((enemy - own) / denom, -1f, 1f);
        }

        /// <summary>
        /// 主導権の掌握度（0..1）。テンポ優位を 0..1 へ写す＝速い側が攻め手を握り、遅い側は受けに回る。
        /// seizure＝(adv＋1)/2。拮抗0.5／完全優位1.0／完全劣位0.0。
        /// </summary>
        public static float InitiativeSeizure(float tempoAdvantage)
        {
            float adv = Mathf.Clamp(tempoAdvantage, -1f, 1f);
            return Mathf.Clamp01((adv + 1f) * 0.5f);
        }

        /// <summary>
        /// 速いテンポで敵が後手に回り混乱する度合い（0..1）。自分が優位（adv&gt;0）なほど、かつ敵の適応力が低いほど大きい。
        /// disorientation＝max(0,adv)×(1−適応力)。劣位（adv≤0）では敵は混乱しない（0）。適応力1.0の敵は付いてくる。
        /// </summary>
        public static float EnemyDisorientation(float tempoAdvantage, float enemyAdaptability)
        {
            float adv = Mathf.Max(0f, Mathf.Clamp(tempoAdvantage, -1f, 1f));
            float adapt = Mathf.Clamp01(enemyAdaptability);
            return Mathf.Clamp01(adv * (1f - adapt));
        }

        /// <summary>既定パラメータで速すぎるテンポの補給/消耗負担。</summary>
        public static float SustainmentStrain(float tempo, float supplyCapacity)
            => SustainmentStrain(tempo, supplyCapacity, OperationalTempoParams.Default);

        /// <summary>
        /// テンポを上げすぎたときの補給/消耗負担（0..1）。テンポが補給能力に対して大きいほど負担が増える。
        /// strain＝clamp01(pow(テンポ/補給能力, 指数))。能力と等速で上限1（持続限界）、半分の速さなら指数2で0.25。
        /// 速さは諸刃＝主導権は握れても兵站が干上がる。
        /// </summary>
        public static float SustainmentStrain(float tempo, float supplyCapacity, OperationalTempoParams p)
        {
            float t = Mathf.Max(0f, tempo);
            float supply = Mathf.Max(0.01f, supplyCapacity);
            float ratio = t / supply;
            return Mathf.Clamp01(Mathf.Pow(ratio, p.strainExponent));
        }

        /// <summary>既定パラメータで持続可能かつ敵を上回る最適テンポ。</summary>
        public static float OptimalTempo(float supplyCapacity, float enemyTempo)
            => OptimalTempo(supplyCapacity, enemyTempo, OperationalTempoParams.Default);

        /// <summary>
        /// 持続可能かつ敵を上回る最適テンポ。敵テンポ×余裕（2割速く）を狙うが、補給能力を超えない（兵站が律速）。
        /// optimal＝min(補給能力, 敵テンポ×余裕)。補給が足りれば敵より速く、足りなければ補給能力で頭打ち
        /// （＝無理に速めると <see cref="SustainmentStrain"/> が跳ね上がる）。
        /// </summary>
        public static float OptimalTempo(float supplyCapacity, float enemyTempo, OperationalTempoParams p)
        {
            float supply = Mathf.Max(0f, supplyCapacity);
            float enemy = Mathf.Max(0f, enemyTempo);
            return Mathf.Min(supply, enemy * p.tempoMargin);
        }

        /// <summary>
        /// 連続行動で積み上がる勢い（0..1）。主導権を握ったまま手を緩めず畳みかけるほど勢いが増す。
        /// momentum＝主導権×(連続行動/(連続行動＋1))。1手目は0、手数が増すほど主導権の上限へ漸近。
        /// 後手に回れば（主導権0）どれだけ手数を重ねても勢いは出ない。
        /// </summary>
        public static float MomentumFromTempo(float initiativeSeizure, int consecutiveActions)
        {
            float init = Mathf.Clamp01(initiativeSeizure);
            int n = Mathf.Max(0, consecutiveActions);
            float buildUp = (float)n / (n + 1);
            return Mathf.Clamp01(init * buildUp);
        }

        /// <summary>
        /// 敵を出し抜いているか（bool）＝テンポ優位がしきい値以上。僅差では主導権は確かでない（既定0.1）。
        /// </summary>
        public static bool IsOutpacingEnemy(float tempoAdvantage, float threshold = 0.1f)
        {
            float adv = Mathf.Clamp(tempoAdvantage, -1f, 1f);
            return adv >= Mathf.Clamp(threshold, -1f, 1f);
        }
    }
}
