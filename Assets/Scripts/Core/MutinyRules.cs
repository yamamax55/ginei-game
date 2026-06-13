using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊反乱の調整係数。</summary>
    public readonly struct MutinyParams
    {
        /// <summary>不満の合成における待遇（俸給遅配）の重み。</summary>
        public readonly float payWeight;
        /// <summary>不満の合成における思想の乖離の重み。</summary>
        public readonly float ideologyWeight;
        /// <summary>不満の合成における敗勢（連敗）の重み。</summary>
        public readonly float defeatWeight;
        /// <summary>軍紀が反乱リスクを抑える強さ（0..1・1で完全に抑え込む）。</summary>
        public readonly float disciplineSuppression;
        /// <summary>カリスマ的首謀者がいるときのリスク倍率（不満に火をつける触媒）。</summary>
        public readonly float ringleaderBoost;
        /// <summary>反乱艦比率の伝播速度（per dt・不満1のとき）。</summary>
        public readonly float spreadRate;
        /// <summary>鎮圧が反乱艦比率を削る速度（per dt・鎮圧1のとき）。</summary>
        public readonly float suppressionRate;
        /// <summary>同士討ちで艦隊全体から失われる戦力割合の最大（両派拮抗時）。</summary>
        public readonly float fratricideLoss;
        /// <summary>鎮圧後に残る禍根の基礎係数（原因を除かない鎮圧でも必ず残る分）。</summary>
        public readonly float aftermathBase;
        /// <summary>処罰の苛烈さが禍根を上積みする係数。</summary>
        public readonly float aftermathScale;

        public MutinyParams(float payWeight, float ideologyWeight, float defeatWeight,
                            float disciplineSuppression, float ringleaderBoost,
                            float spreadRate, float suppressionRate, float fratricideLoss,
                            float aftermathBase, float aftermathScale)
        {
            this.payWeight = Mathf.Max(0f, payWeight);
            this.ideologyWeight = Mathf.Max(0f, ideologyWeight);
            this.defeatWeight = Mathf.Max(0f, defeatWeight);
            this.disciplineSuppression = Mathf.Clamp01(disciplineSuppression);
            this.ringleaderBoost = Mathf.Max(1f, ringleaderBoost);
            this.spreadRate = Mathf.Max(0f, spreadRate);
            this.suppressionRate = Mathf.Max(0f, suppressionRate);
            this.fratricideLoss = Mathf.Clamp01(fratricideLoss);
            this.aftermathBase = Mathf.Max(0f, aftermathBase);
            this.aftermathScale = Mathf.Max(0f, aftermathScale);
        }

        /// <summary>既定＝待遇0.4/思想0.3/敗勢0.3・軍紀抑止0.7・首謀者1.5倍・伝播0.4・鎮圧0.5・同士討ち0.3・禍根基礎0.2＋苛烈0.5。</summary>
        public static MutinyParams Default
            => new MutinyParams(0.4f, 0.3f, 0.3f, 0.7f, 1.5f, 0.4f, 0.5f, 0.3f, 0.2f, 0.5f);
    }

    /// <summary>反乱で割れた艦隊の内訳（戦力比）。</summary>
    public readonly struct MutinySplit
    {
        /// <summary>忠誠派の戦力比（0..1）。</summary>
        public readonly float loyalistShare;
        /// <summary>反乱派の戦力比（0..1）。</summary>
        public readonly float mutineerShare;
        /// <summary>同士討ちで艦隊全体から失われる戦力割合（両派が拮抗するほど大きい）。</summary>
        public readonly float internalAttrition;

        public MutinySplit(float loyalistShare, float mutineerShare, float internalAttrition)
        {
            this.loyalistShare = Mathf.Clamp01(loyalistShare);
            this.mutineerShare = Mathf.Clamp01(mutineerShare);
            this.internalAttrition = Mathf.Clamp01(internalAttrition);
        }
    }

    /// <summary>
    /// 艦隊反乱の純ロジック＝部隊単位の集団的な命令拒否・艦の乗っ取り。
    /// <see cref="DisciplineRules"/>（個別の抗命）と <see cref="CoupRules"/>（国家転覆）の**中間スケール**：
    /// 一人の抗命より大きく、政権打倒より小さい「艦隊が割れる」事態を扱う。
    /// 引き金は待遇（俸給遅配）・思想の乖離・敗勢の三因で、不満として蓄積する。軍紀は蓋になるが、
    /// カリスマ的首謀者が現れると蓋ごと跳ね上がる。発生後は不満を燃料に艦隊内へ伝播し、
    /// 艦隊は忠誠派と反乱派の同士討ちに割れて戦力を内側で焼く。そして核心＝
    /// **反乱は不満の在庫処分にすぎない**：鎮圧は在庫（反乱艦）を片づけるが在庫の原因（待遇・思想・敗勢）
    /// は除かれず、処罰が苛烈なほど次の不満の種（禍根）を自ら蒔く。
    /// 乱数は外から与える roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MutinyRules
    {
        /// <summary>
        /// 不満の合成（0..1）＝待遇 payArrears・思想乖離 ideologyGap・敗勢 losingStreak（各0..1）の加重和。
        /// 反乱は単因では起きにくく、三因が重なって閾を越える。
        /// </summary>
        public static float GrievanceAccumulation(float payArrears, float ideologyGap, float losingStreak, MutinyParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(payArrears) * p.payWeight
                                 + Mathf.Clamp01(ideologyGap) * p.ideologyWeight
                                 + Mathf.Clamp01(losingStreak) * p.defeatWeight);
        }

        public static float GrievanceAccumulation(float payArrears, float ideologyGap, float losingStreak)
            => GrievanceAccumulation(payArrears, ideologyGap, losingStreak, MutinyParams.Default);

        /// <summary>
        /// 反乱確率（0..1）＝不満×（1−軍紀×抑止係数）×首謀者倍率。
        /// 軍紀は蓋（高いほどリスクを削る）、カリスマ的首謀者は触媒（同じ不満でも跳ねる）。
        /// </summary>
        public static float MutinyRisk(float grievance, float discipline, bool charismaticRingleader, MutinyParams p)
        {
            float lid = 1f - Mathf.Clamp01(discipline) * p.disciplineSuppression;
            float boost = charismaticRingleader ? p.ringleaderBoost : 1f;
            return Mathf.Clamp01(Mathf.Clamp01(grievance) * lid * boost);
        }

        public static float MutinyRisk(float grievance, float discipline, bool charismaticRingleader)
            => MutinyRisk(grievance, discipline, charismaticRingleader, MutinyParams.Default);

        /// <summary>発生判定。roll∈[0,1) がリスク未満なら反乱勃発＝true（決定論）。</summary>
        public static bool Erupts(float risk, float roll)
        {
            return roll < Mathf.Clamp01(risk);
        }

        /// <summary>
        /// 反乱艦比率（0..1）の1tick後。伝播はロジスティック型＝不満を燃料に、既存の反乱核と未感染の
        /// 残余の積で広がる（核が無ければ広がらない＝勃発は <see cref="Erupts"/> が別途与える）。
        /// 鎮圧 suppression は比率に比例して削る。
        /// </summary>
        public static float SpreadTick(float mutinousShare, float grievance, float suppression, float dt, MutinyParams p)
        {
            float share = Mathf.Clamp01(mutinousShare);
            float d = Mathf.Max(0f, dt);
            float growth = p.spreadRate * Mathf.Clamp01(grievance) * share * (1f - share) * d;
            float decay = p.suppressionRate * Mathf.Clamp01(suppression) * share * d;
            return Mathf.Clamp01(share + growth - decay);
        }

        public static float SpreadTick(float mutinousShare, float grievance, float suppression, float dt)
            => SpreadTick(mutinousShare, grievance, suppression, dt, MutinyParams.Default);

        /// <summary>
        /// 忠誠派との分裂。艦隊は忠誠派（1−比率）と反乱派（比率）の戦力に割れ、
        /// 同士討ちの損耗は両派が拮抗（50:50）するほど大きい＝外敵に向くはずの戦力が内側で焼ける。
        /// </summary>
        public static MutinySplit LoyalistSplit(float mutinousShare, MutinyParams p)
        {
            float m = Mathf.Clamp01(mutinousShare);
            float loyal = 1f - m;
            float attrition = p.fratricideLoss * 2f * Mathf.Min(m, loyal);
            return new MutinySplit(loyal, m, attrition);
        }

        public static MutinySplit LoyalistSplit(float mutinousShare)
            => LoyalistSplit(mutinousShare, MutinyParams.Default);

        /// <summary>
        /// 鎮圧後の禍根（0..1）＝次の不満の種。規模×（基礎＋苛烈さ severity×係数）。
        /// 反乱は不満の在庫処分＝鎮圧しても在庫の原因（待遇・思想・敗勢）は残るため、
        /// 寛大でも規模に応じた残滓は消えず（基礎分）、処罰が苛烈なほど種は太る。
        /// </summary>
        public static float SuppressionAftermath(float mutinousShare, float severity, MutinyParams p)
        {
            float m = Mathf.Clamp01(mutinousShare);
            return Mathf.Clamp01(m * (p.aftermathBase + Mathf.Clamp01(severity) * p.aftermathScale));
        }

        public static float SuppressionAftermath(float mutinousShare, float severity)
            => SuppressionAftermath(mutinousShare, severity, MutinyParams.Default);
    }
}
