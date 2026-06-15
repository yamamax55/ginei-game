using UnityEngine;

namespace Ginei
{
    /// <summary>全体主義の指導者選別バイアスの調整係数（ハイエク「最悪の者が頂点に立つ」）。</summary>
    public readonly struct AuthoritarianSelectionParams
    {
        /// <summary>従順な大衆が扇動者に動員される基礎係数。</summary>
        public readonly float docileAppealScale;
        /// <summary>否定的結束（共通の敵）が生む結束の基礎係数。</summary>
        public readonly float negativeSolidarityScale;
        /// <summary>非情さ（良心の薄さ）が権力闘争で生む優位の基礎係数。</summary>
        public readonly float ruthlessnessScale;
        /// <summary>逆淘汰が指導層の質を削る速度（per dt・選別圧1のとき）。</summary>
        public readonly float adverseSelectionRate;

        public AuthoritarianSelectionParams(float docileAppealScale, float negativeSolidarityScale,
            float ruthlessnessScale, float adverseSelectionRate)
        {
            this.docileAppealScale = Mathf.Max(0f, docileAppealScale);
            this.negativeSolidarityScale = Mathf.Max(0f, negativeSolidarityScale);
            this.ruthlessnessScale = Mathf.Max(0f, ruthlessnessScale);
            this.adverseSelectionRate = Mathf.Max(0f, adverseSelectionRate);
        }

        /// <summary>既定＝従順動員0.5・否定的結束0.5・非情の優位0.6・逆淘汰速度0.05。</summary>
        public static AuthoritarianSelectionParams Default
            => new AuthoritarianSelectionParams(0.5f, 0.5f, 0.6f, 0.05f);
    }

    /// <summary>
    /// 全体主義体制の指導者選別バイアスの純ロジック（HAYK-3 #1547・ハイエク『隷属への道』第10章
    /// 「なぜ最悪の者が頂点に立つのか＝The Worst Get on Top」）。三つのメカニズムで逆淘汰（最悪が上に立つ
    /// 選別圧）が起きる：①従順で無批判な大衆ほど扇動者に動員しやすい（批判精神が低いほど効く）、②共通の敵への
    /// 憎悪は肯定的価値よりも容易に多数を否定的に結束させる、③権力闘争において良心は障害＝非情な者ほど出世する。
    /// この選別圧が時間で指導層の質を下げ（良心ある者は脱落し非情が残る）、全体主義度が高い体制ほど良心が出世の
    /// 足枷になる。クーデターの成否（<see cref="CoupRules"/>）・政策としての粛清の損得（<see cref="PurgeRules"/>）・
    /// 計画化のドリフト（PlanningDriftRules＝同 EPIC HAYK・隷属への道の経済側）・親衛隊の問題（<see cref="PraetorianRules"/>）
    /// とは別系統＝「誰が上に立つか」の選別バイアスを扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AuthoritarianSelectionRules
    {
        /// <summary>
        /// ①従順な大衆への訴求力（0..1）＝従順 conformity(0..1)×（1−批判精神 criticalThinking(0..1)）×係数。
        /// 従順で無批判な大衆ほど扇動者に動員されやすい。批判精神が高い（1）大衆には効かない。
        /// </summary>
        public static float DocileMassAppeal(float conformity, float criticalThinking, AuthoritarianSelectionParams p)
        {
            return Mathf.Clamp01(conformity) * (1f - Mathf.Clamp01(criticalThinking)) * p.docileAppealScale;
        }

        public static float DocileMassAppeal(float conformity, float criticalThinking)
            => DocileMassAppeal(conformity, criticalThinking, AuthoritarianSelectionParams.Default);

        /// <summary>
        /// ②否定的結束（0..1）＝共通の敵 commonEnemy(0..1)×内集団の恐怖 inGroupFear(0..1)×係数。
        /// 何かに賛成して結束するより、共通の敵を憎んで否定的に結束する方が容易＝敵を作る結束は安上がり。
        /// </summary>
        public static float NegativeSolidarity(float commonEnemy, float inGroupFear, AuthoritarianSelectionParams p)
        {
            return Mathf.Clamp01(commonEnemy) * Mathf.Clamp01(inGroupFear) * p.negativeSolidarityScale;
        }

        public static float NegativeSolidarity(float commonEnemy, float inGroupFear)
            => NegativeSolidarity(commonEnemy, inGroupFear, AuthoritarianSelectionParams.Default);

        /// <summary>
        /// ③非情さの優位（0..1）＝（1−良心 scruples(0..1)）×権力の賭け金 powerStakes(0..1)×係数。
        /// 良心は権力闘争の障害＝賭け金が高いほど、躊躇しない非情な者が勝ち上がる。良心1の者は優位を得ない。
        /// </summary>
        public static float RuthlessnessAdvantage(float scruples, float powerStakes, AuthoritarianSelectionParams p)
        {
            return (1f - Mathf.Clamp01(scruples)) * Mathf.Clamp01(powerStakes) * p.ruthlessnessScale;
        }

        public static float RuthlessnessAdvantage(float scruples, float powerStakes)
            => RuthlessnessAdvantage(scruples, powerStakes, AuthoritarianSelectionParams.Default);

        /// <summary>
        /// 逆淘汰の選別圧（0..1）＝三メカニズム（従順動員・否定的結束・非情の優位）の平均。
        /// 三つが揃うほど「最悪が上へ」の圧力が強い。入力はそれぞれ DocileMassAppeal/NegativeSolidarity/
        /// RuthlessnessAdvantage の出力（0..1）を渡す。
        /// </summary>
        public static float SelectionBias(float docileAppeal, float negativeSolidarity, float ruthlessnessAdvantage)
        {
            float sum = Mathf.Clamp01(docileAppeal) + Mathf.Clamp01(negativeSolidarity) + Mathf.Clamp01(ruthlessnessAdvantage);
            return Mathf.Clamp01(sum / 3f);
        }

        /// <summary>
        /// 逆淘汰の進行（更新後の指導層の質 0..1）＝選別圧 selectionBias(0..1) に比例して質が下がる。
        /// 良心ある有能者は脱落し非情な者が残る＝放置すれば指導層の質は時間で劣化する。下限0。
        /// </summary>
        public static float AdverseSelectionTick(float leadershipQuality, float selectionBias, float dt,
            AuthoritarianSelectionParams p)
        {
            float q = Mathf.Clamp01(leadershipQuality);
            float drop = Mathf.Clamp01(selectionBias) * p.adverseSelectionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(q - drop);
        }

        public static float AdverseSelectionTick(float leadershipQuality, float selectionBias, float dt)
            => AdverseSelectionTick(leadershipQuality, selectionBias, dt, AuthoritarianSelectionParams.Default);

        /// <summary>
        /// 良心が出世の足枷になる度合い（0..1）＝良心 scruples(0..1)×全体主義度 regimeType(0..1)。
        /// 全体主義度が高い体制ほど、良心ある者ほど昇進で不利になる（足を引っ張られる）。
        /// 自由な体制（regimeType=0）では良心は足枷にならない。
        /// </summary>
        public static float MoralityAsLiability(float scruples, float regimeType)
        {
            return Mathf.Clamp01(scruples) * Mathf.Clamp01(regimeType);
        }

        /// <summary>
        /// 従順のカスケード（更新後の従順度 0..1）＝弾圧 repression(0..1) の下で従順が連鎖し批判が消える。
        /// 弾圧が強いほど従順は1へ近づく（沈黙が沈黙を呼ぶ）。下限は元の従順度（後退はしない）。
        /// </summary>
        public static float ConformityCascade(float conformity, float repression)
        {
            float c = Mathf.Clamp01(conformity);
            return Mathf.Clamp01(c + (1f - c) * Mathf.Clamp01(repression));
        }

        /// <summary>
        /// 最悪の者が頂点に立った判定＝指導層の非情さ leadershipRuthlessness(0..1) が閾値 threshold(0..1) 以上。
        /// 逆淘汰が進み非情さが閾値を超えたら「最悪が上に立った」とみなす。
        /// </summary>
        public static bool IsWorstOnTop(float leadershipRuthlessness, float threshold)
        {
            return Mathf.Clamp01(leadershipRuthlessness) >= Mathf.Clamp01(threshold);
        }
    }
}
