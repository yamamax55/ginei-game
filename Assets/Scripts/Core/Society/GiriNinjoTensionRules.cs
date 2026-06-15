using UnityEngine;

namespace Ginei
{
    /// <summary>義理と人情の葛藤の調整係数（ルース・ベネディクト『菊と刀』・KIKU-3 #1838）。</summary>
    public readonly struct GiriNinjoParams
    {
        /// <summary>葛藤を板挟みと判定する閾値（葛藤の深さがこれを超えると IsTorn＝引き裂かれた状態）。</summary>
        public readonly float tornThreshold;
        /// <summary>選択の道徳的代償の幅（捨てた側の重みが最大このコスト＝情を裏切る/義務を欠く痛みの上限）。</summary>
        public readonly float moralCostWeight;
        /// <summary>誠実さが葛藤を内面の苦悩へ増幅する幅（誠実な人ほど苦しむ＝苦悩の上限の重み）。</summary>
        public readonly float stressWeight;
        /// <summary>義理を欠いた時の世間の制裁の幅（共同体が義理を重んじるほど人情選択への評価が下がる上限）。</summary>
        public readonly float judgmentWeight;

        public GiriNinjoParams(float tornThreshold, float moralCostWeight, float stressWeight, float judgmentWeight)
        {
            this.tornThreshold = Mathf.Clamp01(tornThreshold);
            this.moralCostWeight = Mathf.Clamp01(moralCostWeight);
            this.stressWeight = Mathf.Clamp01(stressWeight);
            this.judgmentWeight = Mathf.Clamp01(judgmentWeight);
        }

        /// <summary>既定＝板挟み閾値0.6・道徳的代償幅0.8・苦悩幅1.0・世間の制裁幅0.8。</summary>
        public static GiriNinjoParams Default => new GiriNinjoParams(0.6f, 0.8f, 1.0f, 0.8f);
    }

    /// <summary>
    /// 義理と人情の葛藤の純ロジック＝ルース・ベネディクト『菊と刀』の核心（KIKU-3 #1838）。
    /// <b>義理</b>＝社会的義務・面目・恩返しの務め（世間に対して負う負債）／<b>人情</b>＝人間的な情愛・
    /// 私的な感情（個人の心の動き）。両者が対立する局面で人物はどちらを取るかの板挟みになる
    /// （<see cref="TensionLevel"/>・拮抗するほど深い）。<b>義理は人目があるほど強まり</b>（<see cref="GiriPriority"/>）、
    /// <b>人情は私的な場ほど勝る</b>（<see cref="NinjoPriority"/>）。どちらを取るかは両者の綱引きで決まり
    /// （<see cref="ResolveConflict"/>・>0義理/&lt;0人情/0板挟み）、<b>義理を取れば情を裏切り、人情を取れば義務を欠く</b>
    /// （捨てた側の代償＝<see cref="MoralCostOfChoice"/>）。誠実な人ほど葛藤に苦しみ（<see cref="InnerConflictStress"/>）、
    /// 人情を取って義理を欠けば世間に咎められ（<see cref="SocialJudgment"/>）、両立不能な義理人情は悲劇を生む
    /// （<see cref="TragicOutcome"/>）＝物語の悲劇の核を式に出す。
    /// <b>PoliticalEthicsRules</b>（ウェーバー＝心情倫理vs責任倫理＝動機か結果か）とは別系統＝こちらは
    /// <b>義理（社会的義務）対人情（私的情愛）という日本的葛藤</b>を解く。同 EPIC KIKU の <b>GiriRules</b>
    /// （恩の負債＝義理の蓄積）の義理（義務の強さ）を入力に取りうる。<b>JusticeRules</b>（サンデル＝正義の天秤）とも別。
    /// 全入力クランプ・乱数なし・決定論・基準値非破壊（修正子を返す）。盤面非依存のplain引数。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GiriNinjoRules { }

    /// <summary>義理と人情の葛藤エンジン（『菊と刀』・KIKU-3 #1838）。<see cref="GiriNinjoRules"/> の説明を参照。</summary>
    public static class GiriNinjoTensionRules
    {
        /// <summary>
        /// 葛藤の深さ（0..1）。義理の引力(giriPull 0..1)と人情の引力(ninjoPull 0..1)が<b>両方強く拮抗する</b>ほど
        /// 葛藤が深い＝積（両方高くて初めて高い＝片方だけなら板挟みにならない）に、拮抗度（差が小さいほど1）を掛ける。
        /// どちらか一方が圧倒的なら迷いは無く葛藤は浅い。
        /// </summary>
        public static float TensionLevel(float giriPull, float ninjoPull, GiriNinjoParams p)
        {
            float g = Mathf.Clamp01(giriPull);
            float n = Mathf.Clamp01(ninjoPull);
            float balance = 1f - Mathf.Abs(g - n); // 拮抗度（差0で1・差1で0）
            return Mathf.Clamp01(g * n * balance);
        }

        public static float TensionLevel(float giriPull, float ninjoPull)
            => TensionLevel(giriPull, ninjoPull, GiriNinjoParams.Default);

        /// <summary>
        /// 義理の強さ（0..1）。負うべき義務(giriObligation 0..1)が、社会的に見られている度合い
        /// (socialVisibility 0..1)で底上げされる＝人目があるほど義理が勝つ（面目は世間の目で測られる）。
        /// 義務に「義務×可視性ぶんの上乗せ」を加える（可視性0なら義務そのまま・可視性1なら最大2倍を1にクランプ）。
        /// </summary>
        public static float GiriPriority(float giriObligation, float socialVisibility)
        {
            float obl = Mathf.Clamp01(giriObligation);
            float vis = Mathf.Clamp01(socialVisibility);
            return Mathf.Clamp01(obl * (1f + vis));
        }

        /// <summary>
        /// 人情の強さ（0..1）。情愛の絆(emotionalBond 0..1)が、私的な場である度合い(privateSetting 0..1)で
        /// 底上げされる＝人目の無い私的な場ほど情が勝る（情は世間から隠れた所で素直に出る）。
        /// 絆に「絆×私性ぶんの上乗せ」を加える（私性0なら絆そのまま・私性1なら最大2倍を1にクランプ）。
        /// </summary>
        public static float NinjoPriority(float emotionalBond, float privateSetting)
        {
            float bond = Mathf.Clamp01(emotionalBond);
            float priv = Mathf.Clamp01(privateSetting);
            return Mathf.Clamp01(bond * (1f + priv));
        }

        /// <summary>
        /// 葛藤の解決（−1人情〜+1義理）。義理の強さ(giriPriority 0..1)と人情の強さ(ninjoPriority 0..1)の綱引きを
        /// −1..+1 の軸へ写す。義理が勝てば +（義理を取る）、人情が勝てば −（人情を取る）、拮抗すれば 0（板挟み）。
        /// 両者の取り分を 0..1 で求め −1..+1 へ写す（両方0なら板挟み0）。
        /// </summary>
        public static float ResolveConflict(float giriPriority, float ninjoPriority)
        {
            float g = Mathf.Clamp01(giriPriority);
            float n = Mathf.Clamp01(ninjoPriority);
            float total = g + n;
            if (total <= 0f) return 0f; // どちらも引力が無い＝板挟み（中立0）
            float giriShare = g / total;
            return Mathf.Clamp(giriShare * 2f - 1f, -1f, 1f);
        }

        /// <summary>
        /// 選択の道徳的代償（0..1）。選んだ側(chosenSide 0..1)のために捨てた側(abandonedSide 0..1)が重いほど代償が大きい＝
        /// 「義理を取って情を裏切る／人情を取って義務を欠く」痛みは<b>捨てた側の重さ</b>で決まる（選んだ側ではなく）。
        /// 捨てた側の重み×（選択の強さ）×コスト幅＝強く選ぶほど捨てた側を深く切り捨てる。
        /// </summary>
        public static float MoralCostOfChoice(float chosenSide, float abandonedSide, GiriNinjoParams p)
        {
            float chosen = Mathf.Clamp01(chosenSide);
            float aband = Mathf.Clamp01(abandonedSide);
            return Mathf.Clamp01(aband * chosen * p.moralCostWeight);
        }

        public static float MoralCostOfChoice(float chosenSide, float abandonedSide)
            => MoralCostOfChoice(chosenSide, abandonedSide, GiriNinjoParams.Default);

        /// <summary>
        /// 内面の苦悩（0..1）。葛藤の深さ(tensionLevel 0..1)が、その人の誠実さ(integrityValue 0..1)で増幅される＝
        /// <b>誠実な人ほど苦しむ</b>（義理にも情にも真剣に応えようとするから板挟みが心を引き裂く）。
        /// 葛藤×誠実さ×苦悩幅（不誠実な人は同じ葛藤でも痛まない）。
        /// </summary>
        public static float InnerConflictStress(float tensionLevel, float integrityValue, GiriNinjoParams p)
        {
            float tension = Mathf.Clamp01(tensionLevel);
            float integrity = Mathf.Clamp01(integrityValue);
            return Mathf.Clamp01(tension * integrity * p.stressWeight);
        }

        public static float InnerConflictStress(float tensionLevel, float integrityValue)
            => InnerConflictStress(tensionLevel, integrityValue, GiriNinjoParams.Default);

        /// <summary>
        /// 世間の評価（−1非難〜0中立）。人情を取って義理を欠いた度合い(choseNinjoOverGiri 0..1)が、共同体が義理を
        /// 重んじる度合い(communityValues 0..1)で咎められる＝義理を欠くほど・共同体が義理を重んじるほど評価が下がる。
        /// 負の値（非難）で返す＝0は無風・−1は最大の社会的制裁（義理を重んじる世間で情に走った代償）。
        /// </summary>
        public static float SocialJudgment(float choseNinjoOverGiri, float communityValues, GiriNinjoParams p)
        {
            float chose = Mathf.Clamp01(choseNinjoOverGiri);
            float values = Mathf.Clamp01(communityValues);
            return Mathf.Clamp(-(chose * values * p.judgmentWeight), -1f, 0f);
        }

        public static float SocialJudgment(float choseNinjoOverGiri, float communityValues)
            => SocialJudgment(choseNinjoOverGiri, communityValues, GiriNinjoParams.Default);

        /// <summary>
        /// 悲劇の度合い（0..1）。葛藤の深さ(tensionLevel 0..1)と、義理と人情の両立不能さ(irreconcilability 0..1)の積＝
        /// <b>深い葛藤かつ両立の道が無い</b>ほど悲劇が大きい（どちらを選んでも取り返しがつかない＝物語の悲劇の核）。
        /// 両立可能なら（折衷の道があれば）葛藤が深くても悲劇にはならない＝両立不能さが0なら悲劇0。
        /// </summary>
        public static float TragicOutcome(float tensionLevel, float irreconcilability)
        {
            float tension = Mathf.Clamp01(tensionLevel);
            float irr = Mathf.Clamp01(irreconcilability);
            return Mathf.Clamp01(tension * irr);
        }

        /// <summary>
        /// 板挟み状態の判定。葛藤の深さ(tensionLevel 0..1)が閾値(threshold)を超えるとき true＝
        /// どちらも切り捨てられず引き裂かれた状態（義理にも人情にも引かれて身動きが取れない）。
        /// </summary>
        public static bool IsTorn(float tensionLevel, float threshold)
        {
            float tension = Mathf.Clamp01(tensionLevel);
            float th = Mathf.Clamp01(threshold);
            return tension > th;
        }

        public static bool IsTorn(float tensionLevel)
            => IsTorn(tensionLevel, GiriNinjoParams.Default.tornThreshold);
    }
}
