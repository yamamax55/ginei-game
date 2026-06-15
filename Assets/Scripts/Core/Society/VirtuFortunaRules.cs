using UnityEngine;

namespace Ginei
{
    /// <summary>ヴィルトゥーとフォルトゥーナ（力量と運命）の相互作用の調整係数。</summary>
    public readonly struct VirtuFortunaParams
    {
        /// <summary>運命（fortuna）が結果に効く重み＝人事の「半分」。残りが力量（virtù）の重み。</summary>
        public readonly float fortuneShare;
        /// <summary>力量（virtù）が結果に効く重み（fortuneShare の補完＝1−fortuneShare 相当）。</summary>
        public readonly float virtuShare;
        /// <summary>備え（堤防＝foresight）が運命の打撃をどれだけ和らげるか（0..1）。</summary>
        public readonly float preparednessDamping;
        /// <summary>運命の振れ幅の基準（安定な時代でもこれだけは偶然が残る・最小ボラティリティ）。</summary>
        public readonly float baseVolatility;
        /// <summary>不安定さが運命の振れをどれだけ増幅するか（不安定な時代ほど偶然が大きく振れる）。</summary>
        public readonly float instabilityAmplification;
        /// <summary>好機をつかむ最低成功率（力量0でもまぐれで掴む下限）。</summary>
        public readonly float seizeFloor;
        /// <summary>力量が運命をねじ伏せたと見なす既定の閾値。</summary>
        public readonly float masteryThreshold;

        public VirtuFortunaParams(float fortuneShare, float preparednessDamping,
                                  float baseVolatility, float instabilityAmplification,
                                  float seizeFloor, float masteryThreshold)
        {
            this.fortuneShare = Mathf.Clamp01(fortuneShare);
            this.virtuShare = 1f - this.fortuneShare;
            this.preparednessDamping = Mathf.Clamp01(preparednessDamping);
            this.baseVolatility = Mathf.Clamp01(baseVolatility);
            this.instabilityAmplification = Mathf.Max(0f, instabilityAmplification);
            this.seizeFloor = Mathf.Clamp01(seizeFloor);
            this.masteryThreshold = Mathf.Clamp01(masteryThreshold);
        }

        /// <summary>
        /// 既定＝運命の取り分0.5（人事の半分）・備えの緩和0.7・基準振れ0.2・不安定増幅0.6・
        /// 好機の下限0.1・運命制御の閾値0.5。
        /// </summary>
        public static VirtuFortunaParams Default => new VirtuFortunaParams(0.5f, 0.7f, 0.2f, 0.6f, 0.1f, 0.5f);
    }

    /// <summary>
    /// ヴィルトゥーとフォルトゥーナの純ロジック（MKV-4 #1142・マキャヴェッリ『君主論』）。
    /// 「フォルトゥーナ（運命・偶然）は人事の半分を支配するが、残りの半分は力量（ヴィルトゥー＝果断・適応・備え）に
    /// 委ねられる」＝運命は準備した者に味方し、果断な者は運命をねじ伏せる。「運命は女神＝果敢に立ち向かう者に従う」
    /// 「運命は備えなき所を襲う＝堤防を築け」を式に出す。統治者の適応力（virtù）と外的偶然（fortuna）の相互作用が
    /// 結果を決める（virtù×fortuna の統治修正子）。
    /// 分担：<see cref="GrowthRules"/>＝経験による能力成長（上り坂）／<see cref="SenescenceRules"/>＝加齢の衰え（下り坂）／
    /// <see cref="ReadinessRules"/>＝即応態勢（物理的な備え・疲労）。本クラスは別＝力量と運命の相互作用そのもの
    /// （備えと果断が運命をねじ伏せ、時代への適応が成否を分ける統治修正子）。
    /// 全入力クランプ（fortuna は -1..1）・乱数は roll 引数で決定論・基準非破壊。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class VirtuFortunaRules
    {
        /// <summary>
        /// 結果修正子（0..1＋運命分の振れ＝おおむね 0..1 強）。「運命の半分＋力量の半分」を式に出す：
        /// 運命寄与＝fortuneShare×(fortuna を 0..1 へ写した値・負の運命は0方向＝凶事)、
        /// 力量寄与＝virtuShare×virtu。運命は虚runt（負の fortuna）にも振れる。
        /// 運命は人事の半分を支配するが、残り半分は力量が握る。
        /// </summary>
        public static float OutcomeModifier(float virtu, float fortuna, VirtuFortunaParams p)
        {
            float v = Mathf.Clamp01(virtu);
            float f = Mathf.Clamp(fortuna, -1f, 1f);
            // fortuna(-1..1) を 0..1 の寄与へ：+1で満額、-1で0（凶運）。
            float fortuneContribution = (f + 1f) * 0.5f;
            return p.fortuneShare * fortuneContribution + p.virtuShare * v;
        }

        public static float OutcomeModifier(float virtu, float fortuna)
            => OutcomeModifier(virtu, fortuna, VirtuFortunaParams.Default);

        /// <summary>
        /// 備え（堤防＝foresight）が運命の打撃を和らげる（実効的な負の運命の緩和倍率 0..1）。
        /// 備えと力量が高いほど、悪い運命の被害が軽くなる＝「運命は備えなき所を襲う」。
        /// 緩和率＝preparednessDamping×(foresight と virtu の平均)。返り値は「残る打撃の割合」＝1−緩和率。
        /// </summary>
        public static float FortunePreparedness(float virtu, float foresight, VirtuFortunaParams p)
        {
            float v = Mathf.Clamp01(virtu);
            float fs = Mathf.Clamp01(foresight);
            float defense = (v + fs) * 0.5f;          // 備えと力量の総合（0..1）
            float mitigation = p.preparednessDamping * defense; // 緩和率（0..preparednessDamping）
            return Mathf.Clamp01(1f - mitigation);    // 残る打撃の割合
        }

        public static float FortunePreparedness(float virtu, float foresight)
            => FortunePreparedness(virtu, foresight, VirtuFortunaParams.Default);

        /// <summary>
        /// 好機（運命の女神）を果断につかむ（決定論 roll∈[0,1)）。成功率＝seizeFloor..1 の範囲で
        /// 力量×好機の大きさに比例＝力量が高く好機が熟すほど確実に掴む。roll が成功率未満なら true。
        /// 「運命は果敢に立ち向かう者に従う」。
        /// </summary>
        public static bool SeizeOpportunity(float virtu, float opportunity, float roll, VirtuFortunaParams p)
        {
            float chance = SeizeChance(virtu, opportunity, p);
            return roll < chance;
        }

        public static bool SeizeOpportunity(float virtu, float opportunity, float roll)
            => SeizeOpportunity(virtu, opportunity, roll, VirtuFortunaParams.Default);

        /// <summary>好機をつかむ成功率（seizeFloor..1）。力量×好機の大きさで上がる。</summary>
        public static float SeizeChance(float virtu, float opportunity, VirtuFortunaParams p)
        {
            float v = Mathf.Clamp01(virtu);
            float o = Mathf.Clamp01(opportunity);
            return Mathf.Clamp01(p.seizeFloor + (1f - p.seizeFloor) * v * o);
        }

        public static float SeizeChance(float virtu, float opportunity)
            => SeizeChance(virtu, opportunity, VirtuFortunaParams.Default);

        /// <summary>
        /// 運命の振れ幅（ボラティリティ・0..1）＝不安定な時代ほど偶然が大きく振れる。
        /// 安定（stability=1）なら baseVolatility まで縮み、不安定（=0）なら baseVolatility に増幅分が乗る。
        /// </summary>
        public static float FortuneVolatility(float stability, VirtuFortunaParams p)
        {
            float s = Mathf.Clamp01(stability);
            float instability = 1f - s;
            return Mathf.Clamp01(p.baseVolatility + p.instabilityAmplification * instability);
        }

        public static float FortuneVolatility(float stability)
            => FortuneVolatility(stability, VirtuFortunaParams.Default);

        /// <summary>
        /// 時代の変化への適応力（0..1）。マキャヴェッリ＝慎重な者も果断な者も、時代（situationChange）が
        /// 変われば手法が合わなくなる。変化が大きいほど、力量の高い者しか追従できない＝
        /// 適応度＝virtu×(1−変化幅)＋virtu²×変化幅（変化が大きい局面では力量の二乗が効く＝凡庸は脱落）。
        /// </summary>
        public static float AdaptationToTimes(float virtu, float situationChange, VirtuFortunaParams p)
        {
            float v = Mathf.Clamp01(virtu);
            float c = Mathf.Clamp01(situationChange);
            float steady = v;          // 変化のない時代＝力量どおり
            float turbulent = v * v;   // 激変期＝二乗で厳しく（高力量のみ追従）
            return Mathf.Clamp01(steady * (1f - c) + turbulent * c);
        }

        public static float AdaptationToTimes(float virtu, float situationChange)
            => AdaptationToTimes(virtu, situationChange, VirtuFortunaParams.Default);

        /// <summary>
        /// 果断さ（boldness）と慎重さ（1−boldness）の、時代との相性に応じた報酬（-1..1）。
        /// 運が向く（fortuneFavorability&gt;0）局面では果断が報われ、逆風（&lt;0）では慎重が守る。
        /// ＝果断の報酬は boldness×favorability、慎重の守りは (1−boldness)×(−favorability の正の分)。
        /// 「果断は運が向く時に報われ、慎重は逆風で守る」（時代との相性）。
        /// </summary>
        public static float BoldnessVsCaution(float boldness, float fortuneFavorability)
        {
            float b = Mathf.Clamp01(boldness);
            float fav = Mathf.Clamp(fortuneFavorability, -1f, 1f);
            float boldReward = b * fav;                       // 順風で+、逆風で−
            float cautionShelter = (1f - b) * Mathf.Max(0f, -fav); // 逆風でのみ守りが効く
            return Mathf.Clamp(boldReward + cautionShelter, -1f, 1f);
        }

        /// <summary>
        /// 悪い運命（負の fortuna）を力量で跳ね返す回復力（0..1）。fortuna が正なら被害なし＝1.0。
        /// 負の運命の打撃ぶんを力量で取り戻す＝回復力＝1−打撃×(1−virtu)。
        /// 力量が高いほど、同じ凶運でも立て直しが効く。
        /// </summary>
        public static float ResilienceToMisfortune(float virtu, float fortuna, VirtuFortunaParams p)
        {
            float v = Mathf.Clamp01(virtu);
            float f = Mathf.Clamp(fortuna, -1f, 1f);
            if (f >= 0f) return 1f;          // 運が向いていれば回復の必要なし
            float blow = -f;                  // 凶運の打撃（0..1）
            return Mathf.Clamp01(1f - blow * (1f - v));
        }

        public static float ResilienceToMisfortune(float virtu, float fortuna)
            => ResilienceToMisfortune(virtu, fortuna, VirtuFortunaParams.Default);

        /// <summary>
        /// 力量が運命をねじ伏せた判定。結果修正子（<see cref="OutcomeModifier"/>）が threshold 以上なら true。
        /// 凶運（負の fortuna）でも、十分な力量があれば修正子が閾値を超え得る＝「果断な者は運命をねじ伏せる」。
        /// </summary>
        public static bool IsFortuneMastered(float virtu, float fortuna, float threshold, VirtuFortunaParams p)
        {
            float t = Mathf.Clamp01(threshold);
            return OutcomeModifier(virtu, fortuna, p) >= t;
        }

        public static bool IsFortuneMastered(float virtu, float fortuna, float threshold)
            => IsFortuneMastered(virtu, fortuna, threshold, VirtuFortunaParams.Default);

        public static bool IsFortuneMastered(float virtu, float fortuna)
            => IsFortuneMastered(virtu, fortuna, VirtuFortunaParams.Default.masteryThreshold, VirtuFortunaParams.Default);
    }
}
