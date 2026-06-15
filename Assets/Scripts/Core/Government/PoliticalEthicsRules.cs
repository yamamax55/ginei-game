using UnityEngine;

namespace Ginei
{
    /// <summary>政治家の倫理類型（ウェーバー『職業としての政治』・WEBR-2 #1528）。心情倫理＝動機の純粋さ・原則を貫く／責任倫理＝行為の結果に責任を負う／均衡＝両者の成熟した調和。</summary>
    public enum PoliticalEthicsType { 心情倫理, 責任倫理, 均衡 }

    /// <summary>心情倫理vs責任倫理の調整係数（ウェーバー『職業としての政治』・WEBR-2 #1528）。</summary>
    public readonly struct PoliticalEthicsParams
    {
        /// <summary>心情倫理/責任倫理を弁別する倫理軸の閾値（−1..+1 の軸でこの絶対値を超えると一方に倒す＝内側は均衡）。</summary>
        public readonly float typeThreshold;
        /// <summary>原則を貫いた帰結が招く災いのコスト幅（純粋さ×悪い帰結が最大このコスト＝無責任の罠の重み）。</summary>
        public readonly float principleCostWeight;
        /// <summary>結果を追って原則を捨てる魂の摩耗の幅（責任倫理×原則放棄が最大この摩耗＝マキャヴェリズムへの堕落）。</summary>
        public readonly float erosionWeight;
        /// <summary>無責任な理想主義と判定する「心情の純粋さ−結果責任」の差の閾値（これを超えて純粋さが勝てば無責任）。</summary>
        public readonly float irresponsibilityThreshold;

        public PoliticalEthicsParams(float typeThreshold, float principleCostWeight, float erosionWeight, float irresponsibilityThreshold)
        {
            this.typeThreshold = Mathf.Clamp01(typeThreshold);
            this.principleCostWeight = Mathf.Clamp01(principleCostWeight);
            this.erosionWeight = Mathf.Clamp01(erosionWeight);
            this.irresponsibilityThreshold = Mathf.Clamp01(irresponsibilityThreshold);
        }

        /// <summary>既定＝類型閾値0.3・原則コスト幅0.8・摩耗幅0.8・無責任判定の差閾値0.4。</summary>
        public static PoliticalEthicsParams Default => new PoliticalEthicsParams(0.3f, 0.8f, 0.8f, 0.4f);
    }

    /// <summary>
    /// 心情倫理vs責任倫理の純ロジック＝マックス・ウェーバー『職業としての政治』の核心（WEBR-2 #1528）。
    /// <b>心情倫理(Gesinnungsethik)</b>＝動機の純粋さ・原則を貫くこと／<b>責任倫理(Verantwortungsethik)</b>＝
    /// 行為の結果に責任を負うこと。政治家の意思決定を「動機か結果か」の軸（<see cref="EthicsOrientation"/>）で捉え、
    /// 心情倫理/責任倫理/均衡を弁別する（<see cref="TypeOf"/>）。<b>純粋な信念だけでは無責任になりうる</b>
    /// （原則を貫いた結果の悪い帰結＝<see cref="PrincipleCost"/>・無責任の罠／<see cref="IsIrresponsibleIdealism"/>）が、
    /// <b>結果のみを追えば原則を失う</b>（魂の摩耗＝<see cref="PragmaticErosion"/>・マキャヴェリズムへの堕落）。
    /// ウェーバーの理想は両方を備えた<b>成熟した判断</b>（情熱＝心情と責任感のバランス＝<see cref="MatureJudgment"/>）＝
    /// 「政治には動機の純粋さと結果への責任の両方が要り、片方だけでは無責任か原則喪失になる」を式に出す。
    /// <b>MoralStyleRules</b>（スミス三徳＝慎慮・仁愛・正義による統治スタイル）/<b>JusticeRules</b>（サンデル＝5つの
    /// 正義観の是認）とは別系統＝こちらは<b>政治家の倫理類型（心情倫理vs責任倫理＝動機か結果か）</b>を解く。
    /// 同 EPIC の <b>PoliticalVocationRules</b>（政治の職業化＝召命・情熱・距離・決断力の職業適性）とも別＝こちらは倫理の二類型に特化。
    /// 全入力クランプ・乱数なし・決定論・基準値非破壊（修正子を返す）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PoliticalEthicsRules
    {
        /// <summary>
        /// 倫理の軸（−1心情〜+1責任）。動機重視(convictionWeight 0..1)か結果重視(consequenceWeight 0..1)かの綱引き。
        /// 結果重視が勝てば +（責任倫理寄り）、動機重視が勝てば −（心情倫理寄り）、拮抗すれば 0（均衡）。
        /// 両者の差を（差＝0..1）で正規化して −1..+1 へ写す（両方0なら中立0）。
        /// </summary>
        public static float EthicsOrientation(float convictionWeight, float consequenceWeight)
        {
            float conv = Mathf.Clamp01(convictionWeight);
            float cons = Mathf.Clamp01(consequenceWeight);
            float total = conv + cons;
            if (total <= 0f) return 0f; // どちらの倫理も無い＝均衡（中立）
            // 結果重視の取り分を 0..1 で求め、−1..+1 の軸へ写す（0.5で均衡＝0）。
            float consequenceShare = cons / total;
            return Mathf.Clamp(consequenceShare * 2f - 1f, -1f, 1f);
        }

        /// <summary>
        /// 倫理類型の弁別。軸(ethicsOrientation −1..+1)の絶対値が閾値(threshold)以下なら<b>均衡</b>、
        /// 正側に振れれば<b>責任倫理</b>、負側に振れれば<b>心情倫理</b>。
        /// </summary>
        public static PoliticalEthicsType TypeOf(float ethicsOrientation, float threshold)
        {
            float o = Mathf.Clamp(ethicsOrientation, -1f, 1f);
            float th = Mathf.Clamp01(threshold);
            if (Mathf.Abs(o) <= th) return PoliticalEthicsType.均衡;
            return o > 0f ? PoliticalEthicsType.責任倫理 : PoliticalEthicsType.心情倫理;
        }

        public static PoliticalEthicsType TypeOf(float ethicsOrientation)
            => TypeOf(ethicsOrientation, PoliticalEthicsParams.Default.typeThreshold);

        /// <summary>
        /// 心情の純粋さ（0..1）。原則を曲げない＝妥協しないほど高い。原則固守(principleAdherence 0..1)を、
        /// 妥協(compromise 0..1)で割り引く＝原則を守りつつ妥協が無いほど純粋（信念を貫く心情倫理の強さ）。
        /// </summary>
        public static float ConvictionPurity(float principleAdherence, float compromise)
        {
            float adh = Mathf.Clamp01(principleAdherence);
            float comp = Mathf.Clamp01(compromise);
            return Mathf.Clamp01(adh * (1f - comp));
        }

        /// <summary>
        /// 責任倫理の強さ（0..1）。結果を見通し責任を負う＝結果の見通し(outcomeForesight 0..1)と
        /// 説明責任を負う度合い(accountability 0..1)の積（両方が要る＝片方が欠ければ責任倫理は成立しない）。
        /// </summary>
        public static float ConsequenceResponsibility(float outcomeForesight, float accountability)
        {
            float fore = Mathf.Clamp01(outcomeForesight);
            float acc = Mathf.Clamp01(accountability);
            return Mathf.Clamp01(fore * acc);
        }

        /// <summary>
        /// 原則を貫いた結果の悪い帰結のコスト（0..1）＝<b>無責任の罠</b>。心情の純粋さ(convictionPurity 0..1)が
        /// 現実の悪い帰結(badOutcome 0..1)を招くほどコストが大きい＝純粋さ×悪い帰結×コスト幅。
        /// 「純粋な信念だけでは無責任になりうる」を式に出す（ウェーバーの心情倫理への警告）。
        /// </summary>
        public static float PrincipleCost(float convictionPurity, float badOutcome, PoliticalEthicsParams p)
        {
            float pur = Mathf.Clamp01(convictionPurity);
            float bad = Mathf.Clamp01(badOutcome);
            return Mathf.Clamp01(pur * bad * p.principleCostWeight);
        }

        public static float PrincipleCost(float convictionPurity, float badOutcome)
            => PrincipleCost(convictionPurity, badOutcome, PoliticalEthicsParams.Default);

        /// <summary>
        /// 結果を追って原則を捨てる魂の摩耗（0..1）＝<b>マキャヴェリズムへの堕落</b>。責任倫理の強さ
        /// (consequenceResponsibility 0..1)が原則の放棄(principleAbandonment 0..1)を伴うほど魂が摩耗する＝
        /// 責任×原則放棄×摩耗幅。「結果のみを追えば原則を失う」を式に出す（ウェーバーの責任倫理への警告）。
        /// </summary>
        public static float PragmaticErosion(float consequenceResponsibility, float principleAbandonment, PoliticalEthicsParams p)
        {
            float resp = Mathf.Clamp01(consequenceResponsibility);
            float aband = Mathf.Clamp01(principleAbandonment);
            return Mathf.Clamp01(resp * aband * p.erosionWeight);
        }

        public static float PragmaticErosion(float consequenceResponsibility, float principleAbandonment)
            => PragmaticErosion(consequenceResponsibility, principleAbandonment, PoliticalEthicsParams.Default);

        /// <summary>
        /// 成熟した政治的判断（0..1）＝ウェーバーの理想。情熱（心情＝conviction 0..1）と責任感（responsibility 0..1）の
        /// <b>両方</b>が要る＝幾何平均（積の平方根）で、片方が欠ければ成熟は成立しない（どちらか0なら0）。
        /// 両者が高く揃って初めて高い＝心情の情熱と結果への責任を兼ね備えた成熟。
        /// </summary>
        public static float MatureJudgment(float conviction, float responsibility)
        {
            float conv = Mathf.Clamp01(conviction);
            float resp = Mathf.Clamp01(responsibility);
            return Mathf.Clamp01(Mathf.Pow(conv * resp, 0.5f));
        }

        /// <summary>
        /// 状況の重さに応じた倫理の使い分け（−1心情〜+1責任）。平時(situationGravity 0低)は原則（心情倫理）を許し、
        /// 危機(situationGravity 1高)ほど結果（責任倫理）へ倒すべき＝倫理軸を状況の重さで責任側へバイアスする。
        /// 元の軸(ethicsOrientation −1..+1)を、状況の重さに比例して +1（責任）方向へ引き寄せる。
        /// </summary>
        public static float ProportionToReality(float ethicsOrientation, float situationGravity)
        {
            float o = Mathf.Clamp(ethicsOrientation, -1f, 1f);
            float g = Mathf.Clamp01(situationGravity);
            // 状況が重いほど責任倫理(+1)へ寄せる＝gで o と +1 を線形補間。
            return Mathf.Clamp(Mathf.Lerp(o, 1f, g), -1f, 1f);
        }

        /// <summary>
        /// 結果を顧みない無責任な理想主義の判定。心情の純粋さ(convictionPurity 0..1)が結果責任
        /// (consequenceResponsibility 0..1)を閾値(threshold)を超えて上回るとき true＝純粋さばかりで結果に責任を
        /// 負わない無責任な理想主義（ウェーバーの戒める心情倫理の暴走）。
        /// </summary>
        public static bool IsIrresponsibleIdealism(float convictionPurity, float consequenceResponsibility, float threshold)
        {
            float pur = Mathf.Clamp01(convictionPurity);
            float resp = Mathf.Clamp01(consequenceResponsibility);
            float th = Mathf.Clamp01(threshold);
            return (pur - resp) > th;
        }

        public static bool IsIrresponsibleIdealism(float convictionPurity, float consequenceResponsibility)
            => IsIrresponsibleIdealism(convictionPurity, consequenceResponsibility, PoliticalEthicsParams.Default.irresponsibilityThreshold);
    }
}
