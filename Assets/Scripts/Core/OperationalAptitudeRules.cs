using UnityEngine;

namespace Ginei
{
    /// <summary>戦闘類型＝提督の適性を測る戦場の種類（#1063）。</summary>
    public enum CombatType
    {
        /// <summary>遭遇戦（艦隊同士の野戦・会戦）。</summary>
        遭遇戦,
        /// <summary>拠点侵攻（要塞・惑星への攻め手）。</summary>
        拠点侵攻,
        /// <summary>拠点防衛（要塞・惑星に拠る守り手）。</summary>
        拠点防衛
    }

    /// <summary>作戦適性の等級（S=最高〜E=最低）。得意な戦場では別人のように強い。</summary>
    public enum AptitudeGrade
    {
        S,
        A,
        B,
        C,
        D,
        E
    }

    /// <summary>作戦適性の調整係数。</summary>
    public readonly struct OperationalAptitudeParams
    {
        /// <summary>S適性の能力倍率（1超＝得意な戦場では能力が大幅増）。</summary>
        public readonly float gradeSMultiplier;
        /// <summary>E適性の能力倍率（1未満＝苦手な戦場では能力が大幅減）。</summary>
        public readonly float gradeEMultiplier;
        /// <summary>不適合の罰の最大値（最も苦手な戦場へ投入した損失の上限）。</summary>
        public readonly float maxMismatchPenalty;

        public OperationalAptitudeParams(float gradeSMultiplier, float gradeEMultiplier, float maxMismatchPenalty)
        {
            this.gradeSMultiplier = Mathf.Max(1f, gradeSMultiplier);
            this.gradeEMultiplier = Mathf.Clamp(gradeEMultiplier, 0f, 1f);
            this.maxMismatchPenalty = Mathf.Clamp01(maxMismatchPenalty);
        }

        /// <summary>既定＝S倍率1.5・E倍率0.6・不適合罰の上限0.5。</summary>
        public static OperationalAptitudeParams Default => new OperationalAptitudeParams(1.5f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 作戦適性の純ロジック（#1063 Almagest）。提督ごとに戦闘類型（遭遇戦・拠点侵攻・拠点防衛）への
    /// 得手不得手がS〜Eの等級で決まり、得意な戦場では能力が増し苦手では落ちる＝適材適所が戦いを決める
    /// （守りの名手を攻めに使うな）。等級は基準能力に掛ける倍率を返すだけで、基準能力そのものは変えない
    /// （実効値パターン・基準非破壊）。
    /// 分担：<see cref="AdmiralData"/> が能力（統率/攻撃/防御…の基準値）、<see cref="AdmiralSkillRules"/> が
    /// 条件付きパッシブスキルの修正子、<see cref="TerrainRules"/> が宙域地形そのものの環境倍率を担う。
    /// ここは「提督×戦闘類型」の適性等級だけを扱う（地形の物理特性ではなく将の得手不得手）。
    /// 乱数なし決定論・全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OperationalAptitudeRules
    {
        /// <summary>戦闘類型の数（適性配列の長さ）。</summary>
        public const int CombatTypeCount = 3;

        /// <summary>等級の段階数（S〜Eの6段階）。</summary>
        public const int GradeCount = 6;

        /// <summary>
        /// 適性等級の能力倍率（S=大幅増・E=大幅減）。
        /// S→gradeSMultiplier、E→gradeEMultiplier の間を等級ぶん等間隔に線形補間し、C（中庸）が約1.0倍になる。
        /// 得意な戦場では別人のように強く、苦手な戦場では凡将に堕ちる。
        /// </summary>
        public static float AptitudeMultiplier(AptitudeGrade grade, OperationalAptitudeParams p)
        {
            // S=0 .. E=5 を t∈[0,1] に写像（0=最高/1=最低）。
            int idx = (int)grade;
            if (idx < 0) idx = 0;
            if (idx > GradeCount - 1) idx = GradeCount - 1;
            float t = (float)idx / (GradeCount - 1);
            return Mathf.Lerp(p.gradeSMultiplier, p.gradeEMultiplier, t);
        }

        public static float AptitudeMultiplier(AptitudeGrade grade)
            => AptitudeMultiplier(grade, OperationalAptitudeParams.Default);

        /// <summary>
        /// 適性込みの実効能力＝基準能力(0..100)×適性倍率（実効値パターン・基準非破壊）。
        /// 攻城の名手(S)も野戦が苦手(E)なら、同じ基準能力でも野戦では凡庸に振る舞う。上限は <see cref="AdmiralData.MaxStatValue"/>。
        /// </summary>
        public static float EffectivePerformance(float baseAbility, AptitudeGrade grade, OperationalAptitudeParams p)
        {
            float ability = Mathf.Clamp(baseAbility, 0f, AdmiralData.MaxStatValue);
            return Mathf.Clamp(ability * AptitudeMultiplier(grade, p), 0f, AdmiralData.MaxStatValue);
        }

        public static float EffectivePerformance(float baseAbility, AptitudeGrade grade)
            => EffectivePerformance(baseAbility, grade, OperationalAptitudeParams.Default);

        /// <summary>
        /// 適性スコア(0..1)からS〜E等級へ（高いほどS）。
        /// [0,1] を6段に等分し、1.0付近=S、0.0付近=E。閾値駆動で決定論。
        /// </summary>
        public static AptitudeGrade GradeFromScore(float aptitudeScore)
        {
            float s = Mathf.Clamp01(aptitudeScore);
            // 等間隔のバケット（高スコアほど上位等級）。
            // s>=5/6→S, >=4/6→A, >=3/6→B, >=2/6→C, >=1/6→D, それ未満→E。
            int bucket = (int)((1f - s) * GradeCount);
            if (bucket < 0) bucket = 0;
            if (bucket > GradeCount - 1) bucket = GradeCount - 1;
            return (AptitudeGrade)bucket;
        }

        /// <summary>
        /// 最も得意な戦闘類型（grades[i] が <see cref="CombatType"/> i に対応）＝この提督をどこで使うか（適材適所）。
        /// 同等級なら添字の若い類型（遭遇戦＞拠点侵攻＞拠点防衛）を優先。null/空は遭遇戦を既定で返す。
        /// </summary>
        public static CombatType BestCombatType(AptitudeGrade[] grades)
        {
            if (grades == null || grades.Length == 0)
                return CombatType.遭遇戦;

            int bestIdx = 0;
            AptitudeGrade best = grades[0]; // 数値が小さいほど上位等級（S=0）。
            for (int i = 1; i < grades.Length && i < CombatTypeCount; i++)
            {
                if ((int)grades[i] < (int)best)
                {
                    best = grades[i];
                    bestIdx = i;
                }
            }
            return (CombatType)bestIdx;
        }

        /// <summary>
        /// 不適合の罰(0..maxMismatchPenalty)＝苦手な戦場へ投入した損失（守りの名将を攻めに使う愚）。
        /// 配属した戦闘類型に対する等級が低い（E寄り）ほど罰が大きく、S/A の得意分野なら罰ゼロ。
        /// 返り値は「能力からの差し引き割合」＝EffectivePerformance と独立に減点係数として使える。
        /// </summary>
        public static float MismatchPenalty(CombatType assignedType, AptitudeGrade gradeForType, OperationalAptitudeParams p)
        {
            // S=0..E=5 を罰の度合いへ。B(=2) を境に、それより悪い等級でのみ罰が立つ。
            int idx = (int)gradeForType;
            if (idx < 0) idx = 0;
            if (idx > GradeCount - 1) idx = GradeCount - 1;

            // B(2)以下は得意〜中庸＝罰ゼロ。C(3)以降が不適合。
            const int neutralIdx = 2;
            if (idx <= neutralIdx)
                return 0f;

            // C..E を 0..1 へ写像（E で最大罰）。
            float severity = (float)(idx - neutralIdx) / (GradeCount - 1 - neutralIdx);
            return Mathf.Clamp01(severity) * p.maxMismatchPenalty;
        }

        public static float MismatchPenalty(CombatType assignedType, AptitudeGrade gradeForType)
            => MismatchPenalty(assignedType, gradeForType, OperationalAptitudeParams.Default);

        /// <summary>
        /// 適性のぶつかり合い＝攻める側の適性 vs 守る側の適性（攻防の差が戦闘を左右する）。
        /// 戦闘類型に応じて「攻め側の侵攻適性倍率 ÷ 守り側の防衛適性倍率」を返す：
        /// 1超＝攻め手有利、1未満＝守り手有利。拠点侵攻なら攻め=拠点侵攻適性/守り=拠点防衛適性で対比し、
        /// 遭遇戦は双方の遭遇戦適性で対比する（守り手に防衛特化がいれば攻め手は跳ね返される）。
        /// </summary>
        public static float TerrainMatchBonus(AptitudeGrade commanderGrade, AptitudeGrade defenderGrade, CombatType type, OperationalAptitudeParams p)
        {
            float attackerFactor = AptitudeMultiplier(commanderGrade, p);
            float defenderFactor = AptitudeMultiplier(defenderGrade, p);
            // ゼロ割回避（E倍率が0でも安全に最大優位へ）。
            if (defenderFactor <= 0f)
                return p.gradeSMultiplier / Mathf.Max(p.gradeEMultiplier, 0.01f);
            return attackerFactor / defenderFactor;
        }

        public static float TerrainMatchBonus(AptitudeGrade commanderGrade, AptitudeGrade defenderGrade, CombatType type)
            => TerrainMatchBonus(commanderGrade, defenderGrade, type, OperationalAptitudeParams.Default);
    }
}
