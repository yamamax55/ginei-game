using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊決戦主義ドクトリンの調整係数。ctor で全値をクランプする。</summary>
    public readonly struct DecisiveBattleDoctrineParams
    {
        /// <summary>決戦を求める誘因における「戦力優位」の寄与（重み）。</summary>
        public readonly float advantageWeight;
        /// <summary>決戦を求める誘因における「時間圧力」の寄与（重み）＝持久戦不利なら速戦を求める。</summary>
        public readonly float pressureWeight;
        /// <summary>決戦の博打性の鋭さ（指数）。戦力投入×不確実が大きいほど博打性が跳ね上がる。</summary>
        public readonly float gambleSharpness;
        /// <summary>決戦勝利の戦略効果の係数（敵主力撃滅×戦争疲弊に掛ける利得倍率の上限寄与）。</summary>
        public readonly float payoffScale;
        /// <summary>壊滅的敗北リスクが妥当性を引き下げる重み（リスク回避度）。</summary>
        public readonly float defeatPenaltyWeight;

        public DecisiveBattleDoctrineParams(float advantageWeight, float pressureWeight,
            float gambleSharpness, float payoffScale, float defeatPenaltyWeight)
        {
            this.advantageWeight = Mathf.Max(0f, advantageWeight);
            this.pressureWeight = Mathf.Max(0f, pressureWeight);
            this.gambleSharpness = Mathf.Max(0.01f, gambleSharpness);
            this.payoffScale = Mathf.Clamp01(payoffScale);
            this.defeatPenaltyWeight = Mathf.Max(0f, defeatPenaltyWeight);
        }

        /// <summary>既定＝優位重み0.6・圧力重み0.4／博打鋭さ1.5／勝利効果係数1.0／敗北ペナルティ重み1.0。</summary>
        public static DecisiveBattleDoctrineParams Default =>
            new DecisiveBattleDoctrineParams(0.6f, 0.4f, 1.5f, 1.0f, 1.0f);
    }

    /// <summary>
    /// 艦隊決戦主義（decisive battle doctrine）の純ロジック＝<b>主力決戦で戦争を一気に決する思想</b>。
    /// 一大会戦で雌雄を決すれば長い消耗戦を避けて短期に決着できる一方、敗れれば主力を一挙に失い壊滅する
    /// （マハン的な艦隊決戦／日本海海戦の思想）。決戦を求める誘因（戦力優位×時間圧力）、主力の集結
    /// （集中度×動員完了）、決戦の機会（敵の隙×即応）、漸減作戦＝決戦回避（敵の増勢×自軍の持久）との対比、
    /// 決戦の博打性（戦力投入×不確実）、決戦勝利の戦略効果（敵主力撃滅×戦争疲弊）、壊滅的敗北リスク
    /// （博打性×代替不能）、ドクトリンの妥当性（機会×勝利効果−壊滅リスク）を算定する。
    /// <see cref="DecisiveBattleWindowRules"/>（決戦の好機の窓＝いつ叩くか）とは別＝こちらは「一大会戦に賭ける
    /// 思想そのものの是非」を評価する。<see cref="AttritionExchangeRules"/>（消耗交換の収支）とも別。
    /// 倍率・スコアは基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DecisiveBattleDoctrineRules
    {
        /// <summary>
        /// 決戦を求める誘因（0..1）＝戦力優位と時間圧力で一大会戦を志向する強さ。strengthAdvantage（戦力優位
        /// 0..1）と timesPressure（時間圧力0..1＝持久すれば不利になる度＝速戦を迫られる度）を重み付き平均する
        /// （重み合計で正規化、合計0なら0）。優位を持ち・かつ時間が味方しないほど「今こそ決戦」を強く求める。
        /// </summary>
        public static float DecisiveBattleSeek(float strengthAdvantage, float timesPressure,
            DecisiveBattleDoctrineParams p)
        {
            float adv = Mathf.Clamp01(strengthAdvantage);
            float pres = Mathf.Clamp01(timesPressure);
            float weightSum = p.advantageWeight + p.pressureWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01((p.advantageWeight * adv + p.pressureWeight * pres) / weightSum);
        }

        /// <summary>既定係数での決戦を求める誘因（0..1）。</summary>
        public static float DecisiveBattleSeek(float strengthAdvantage, float timesPressure)
            => DecisiveBattleSeek(strengthAdvantage, timesPressure, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 主力の集結（0..1）＝決戦に主力を集中できているか。concentrationRatio（戦力の集中度0..1＝分散せず一点に
        /// 集めた度）× mobilizationCompleteness（動員完了度0..1）の積＝戦力を一所に集め・かつ動員が整ってこそ
        /// 主力決戦に臨める。どちらか一方でも0なら集結0＝集中していても動員未了なら・動員済でも分散なら主力たり得ない。
        /// </summary>
        public static float ForceMassing(float concentrationRatio, float mobilizationCompleteness,
            DecisiveBattleDoctrineParams p)
        {
            float conc = Mathf.Clamp01(concentrationRatio);
            float mob = Mathf.Clamp01(mobilizationCompleteness);
            return Mathf.Clamp01(conc * mob);
        }

        /// <summary>既定係数での主力の集結（0..1）。</summary>
        public static float ForceMassing(float concentrationRatio, float mobilizationCompleteness)
            => ForceMassing(concentrationRatio, mobilizationCompleteness, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 決戦の機会（0..1）＝敵を捕捉して一大会戦に持ち込めるか。enemyExposure（敵の隙0..1＝身を晒し決戦に
        /// 引き込める度）× ownReadiness（自軍の即応0..1＝好機に即応できる態勢）の積＝敵が隙を見せ・かつこちらが
        /// 即応できる瞬間にだけ会戦が成立する。どちらか一方でも0なら機会0＝隙があっても即応できねば・即応できても
        /// 敵が応じねば決戦は起きない。
        /// </summary>
        public static float BattleOpportunity(float enemyExposure, float ownReadiness,
            DecisiveBattleDoctrineParams p)
        {
            float exp = Mathf.Clamp01(enemyExposure);
            float rdy = Mathf.Clamp01(ownReadiness);
            return Mathf.Clamp01(exp * rdy);
        }

        /// <summary>既定係数での決戦の機会（0..1）。</summary>
        public static float BattleOpportunity(float enemyExposure, float ownReadiness)
            => BattleOpportunity(enemyExposure, ownReadiness, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 漸減作戦の妥当性（0..1）＝決戦を避け敵を漸減させる選択の正しさ（決戦主義の対極）。enemyStrengthGrowth
        /// （敵の増勢0..1＝放置すると敵が強くなる度）と ownStayingPower（自軍の持久0..1＝長期戦に耐える力）の
        /// 幾何平均＝敵が増勢し<b>かつ</b>こちらに持久力があるほど、決戦を急がず漸減で削るのが妥当。幾何平均ゆえ
        /// どちらか一方が乏しければ妥当性は崩れる（敵が増勢でも持久できねば漸減は破綻・持久できても敵が増勢せねば
        /// 漸減の旨味は薄い）。決戦を求める誘因 <see cref="DecisiveBattleSeek"/> と綱引きになる。
        /// </summary>
        public static float AttritionAlternative(float enemyStrengthGrowth, float ownStayingPower,
            DecisiveBattleDoctrineParams p)
        {
            float grow = Mathf.Clamp01(enemyStrengthGrowth);
            float stay = Mathf.Clamp01(ownStayingPower);
            return Mathf.Clamp01(Mathf.Sqrt(grow * stay));
        }

        /// <summary>既定係数での漸減作戦の妥当性（0..1）。</summary>
        public static float AttritionAlternative(float enemyStrengthGrowth, float ownStayingPower)
            => AttritionAlternative(enemyStrengthGrowth, ownStayingPower, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 決戦の博打性（0..1）＝一会戦に賭ける危うさ。forceCommitment（戦力投入0..1＝主力をどれだけ賭けるか）×
        /// outcomeUncertainty（結果の不確実0..1）を掛け、博打鋭さ（指数）で強調する＝主力を多く賭け・かつ結果が
        /// 読めないほど博打性が跳ね上がる。投入が無ければ（0）・結果が確実なら（0）博打ではない＝決戦は本質的に
        /// 一発勝負の賭けである。
        /// </summary>
        public static float BattleGambleRisk(float forceCommitment, float outcomeUncertainty,
            DecisiveBattleDoctrineParams p)
        {
            float commit = Mathf.Clamp01(forceCommitment);
            float unc = Mathf.Clamp01(outcomeUncertainty);
            float baseRisk = commit * unc;
            return Mathf.Clamp01(Mathf.Pow(baseRisk, p.gambleSharpness));
        }

        /// <summary>既定係数での決戦の博打性（0..1）。</summary>
        public static float BattleGambleRisk(float forceCommitment, float outcomeUncertainty)
            => BattleGambleRisk(forceCommitment, outcomeUncertainty, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 決戦勝利の戦略効果（0..1）＝一大会戦に勝てば戦争を一気に決する利得。enemyMainForceDestroyed（敵主力
        /// 撃滅0..1＝相手の主力をどれだけ壊滅させたか）× warExhaustion（戦争疲弊0..1＝相手がもう続けられない度）に
        /// 勝利効果係数を掛ける＝敵主力を撃滅し・かつ相手が疲弊しきっているほど、戦争そのものを終わらせる
        /// 決定的効果になる。主力を取り逃がせば（0）・敵に余力があれば（0）勝っても戦略的決着には至らない。
        /// </summary>
        public static float DecisiveVictoryPayoff(float enemyMainForceDestroyed, float warExhaustion,
            DecisiveBattleDoctrineParams p)
        {
            float destroyed = Mathf.Clamp01(enemyMainForceDestroyed);
            float exhaust = Mathf.Clamp01(warExhaustion);
            return Mathf.Clamp01(destroyed * exhaust * p.payoffScale);
        }

        /// <summary>既定係数での決戦勝利の戦略効果（0..1）。</summary>
        public static float DecisiveVictoryPayoff(float enemyMainForceDestroyed, float warExhaustion)
            => DecisiveVictoryPayoff(enemyMainForceDestroyed, warExhaustion, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 壊滅的敗北リスク（0..1）＝決戦に敗れたとき主力を一挙に失う取り返しのつかなさ。battleGambleRisk
        /// （決戦の博打性0..1）× irreplaceability（戦力の代替不能0..1＝失っても補充できない度）の積＝賭けが大きく・
        /// かつ失った戦力を二度と取り戻せないほど壊滅は致命的になる。博打性が無ければ（0）・いくらでも補充できれば
        /// （irreplaceability 0）敗れても壊滅には至らない＝決戦主義の最大の弱点（一度の敗北で国が傾く）。
        /// </summary>
        public static float CatastrophicDefeatRisk(float battleGambleRisk, float irreplaceability,
            DecisiveBattleDoctrineParams p)
        {
            float gamble = Mathf.Clamp01(battleGambleRisk);
            float irr = Mathf.Clamp01(irreplaceability);
            return Mathf.Clamp01(gamble * irr);
        }

        /// <summary>既定係数での壊滅的敗北リスク（0..1）。</summary>
        public static float CatastrophicDefeatRisk(float battleGambleRisk, float irreplaceability)
            => CatastrophicDefeatRisk(battleGambleRisk, irreplaceability, DecisiveBattleDoctrineParams.Default);

        /// <summary>
        /// 決戦ドクトリンの妥当性（-1..1）＝艦隊決戦主義を選ぶべきかの総合判断。battleOpportunity（決戦の機会0..1）×
        /// decisiveVictoryPayoff（勝利効果0..1）の積（＝機が熟し勝てば見返りが大きい＝攻め時）から、
        /// catastrophicDefeatRisk（壊滅リスク0..1）に敗北ペナルティ重みを掛けたものを引く＝<b>機会×勝利効果 −
        /// 壊滅リスク</b>。正なら一大会戦に賭ける価値あり、負なら決戦は割に合わず漸減・温存が賢明。
        /// 好機があり勝てば決定的だがリスクの小さい状況で最も高く、機会が乏しく壊滅リスクが大きい状況で最も低い。
        /// </summary>
        public static float DoctrineSoundness(float battleOpportunity, float decisiveVictoryPayoff,
            float catastrophicDefeatRisk, DecisiveBattleDoctrineParams p)
        {
            float opp = Mathf.Clamp01(battleOpportunity);
            float payoff = Mathf.Clamp01(decisiveVictoryPayoff);
            float defeat = Mathf.Clamp01(catastrophicDefeatRisk);
            float upside = opp * payoff;
            float downside = defeat * p.defeatPenaltyWeight;
            return Mathf.Clamp(upside - downside, -1f, 1f);
        }

        /// <summary>既定係数での決戦ドクトリンの妥当性（-1..1）。</summary>
        public static float DoctrineSoundness(float battleOpportunity, float decisiveVictoryPayoff,
            float catastrophicDefeatRisk)
            => DoctrineSoundness(battleOpportunity, decisiveVictoryPayoff, catastrophicDefeatRisk,
                DecisiveBattleDoctrineParams.Default);
    }
}
