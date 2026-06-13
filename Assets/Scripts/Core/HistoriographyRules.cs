using UnityEngine;

namespace Ginei
{
    /// <summary>歴史叙述の調整係数（勝者が歴史を書く）。</summary>
    public readonly struct HistoriographyParams
    {
        /// <summary>政権の都合が公式評価を歪める最大幅（0..1。整合なら持ち上げ、敵対なら貶める）。</summary>
        public readonly float distortionStrength;
        /// <summary>政権交代時の再評価の進み（0..1。1未満＝旧叙述の慣性が残る＝真実には戻らない）。</summary>
        public readonly float revisionRate;
        /// <summary>真実の検証可能性が痩せる速さ（証人ゼロ時・per dt）。</summary>
        public readonly float erosionRate;
        /// <summary>検証可能性の維持に足りる生き証人の数（これ以上居れば痩せない）。</summary>
        public readonly int witnessSufficiency;
        /// <summary>公式圧力が歴史家の抵抗力を削る重み（0..1）。</summary>
        public readonly float suppressionWeight;

        public HistoriographyParams(float distortionStrength, float revisionRate, float erosionRate,
                                    int witnessSufficiency, float suppressionWeight)
        {
            this.distortionStrength = Mathf.Clamp01(distortionStrength);
            this.revisionRate = Mathf.Clamp01(revisionRate);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.witnessSufficiency = Mathf.Max(1, witnessSufficiency);
            this.suppressionWeight = Mathf.Clamp01(suppressionWeight);
        }

        /// <summary>既定＝歪み幅0.6・再評価率0.7・浸食0.1・証人充足10名・圧力重み0.5。</summary>
        public static HistoriographyParams Default => new HistoriographyParams(0.6f, 0.7f, 0.1f, 10, 0.5f);
    }

    /// <summary>
    /// 歴史叙述の純ロジック（勝者が歴史を書く）。人物の後世評価は実際の功罪（trueDeeds）でなく
    /// 現政権との整合（regimeAlignment）で歪められ、政権交代では新政権の都合で再び歪む＝真実には戻らない。
    /// 生き証人の退場で真実の検証可能性が痩せ、学問の自由だけが歪みを抑えて長期で真実へ収束させる。
    /// <see cref="ReputationRules"/>（存命中の名声＝戦歴で増減し本人に効く）とは別系統＝こちらは死後の評価戦
    /// （故人は反論できず、評価は政権の都合で書き換わる）。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HistoriographyRules
    {
        /// <summary>
        /// 公式評価（0..1）。実際の功罪 trueDeeds(0..1) を、現政権との整合 regimeAlignment(-1..1) が
        /// distortionStrength の幅で歪める：整合(＋)なら満点方向へ持ち上げ、敵対(−)なら零点方向へ貶める。
        /// 整合ゼロ（政権に無関係）の人物だけが真実どおりに書かれる。
        /// </summary>
        public static float OfficialVerdict(float trueDeeds, float regimeAlignment, HistoriographyParams p)
        {
            float deeds = Mathf.Clamp01(trueDeeds);
            float align = Mathf.Clamp(regimeAlignment, -1f, 1f);
            // 整合(＋)は 1 へ、敵対(−)は 0 へ、|align|×歪み幅ぶん引き寄せる（余地に比例＝クランプ切り捨て無し）。
            float target = align >= 0f ? 1f : 0f;
            return Mathf.Lerp(deeds, target, Mathf.Abs(align) * p.distortionStrength);
        }

        public static float OfficialVerdict(float trueDeeds, float regimeAlignment)
            => OfficialVerdict(trueDeeds, regimeAlignment, HistoriographyParams.Default);

        /// <summary>改竄の幅（0..1）＝公式評価と実際の功罪の乖離。大きいほど歴史が嘘をついている。</summary>
        public static float DistortionGap(float official, float trueDeeds)
            => Mathf.Abs(Mathf.Clamp01(official) - Mathf.Clamp01(trueDeeds));

        /// <summary>
        /// 政権交代の再評価（0..1）。旧公式評価 official から、新政権の都合の評価
        /// （<see cref="OfficialVerdict(float,float,HistoriographyParams)"/> with newAlignment）へ revisionRate ぶんだけ動く。
        /// 新政権も自分の都合で書く＋旧叙述の慣性が残るため、交代しても真実そのものには戻らない。
        /// </summary>
        public static float RevisionOnRegimeChange(float official, float trueDeeds, float newAlignment, HistoriographyParams p)
        {
            float from = Mathf.Clamp01(official);
            float to = OfficialVerdict(trueDeeds, newAlignment, p);
            return Mathf.Lerp(from, to, p.revisionRate);
        }

        public static float RevisionOnRegimeChange(float official, float trueDeeds, float newAlignment)
            => RevisionOnRegimeChange(official, trueDeeds, newAlignment, HistoriographyParams.Default);

        /// <summary>
        /// 真実の検証可能性の1tick更新（0..1）。生き証人が witnessSufficiency 名以上居る間は痩せず、
        /// 退場で減るほど erosionRate へ向けて速く痩せる（証人ゼロで最大速度）。一度痩せた検証可能性は戻らない。
        /// </summary>
        public static float TruthErosionTick(float verifiability, int survivingWitnesses, float dt, HistoriographyParams p)
        {
            float v = Mathf.Clamp01(verifiability);
            float shortage = 1f - Mathf.Clamp01((float)Mathf.Max(0, survivingWitnesses) / p.witnessSufficiency);
            return Mathf.Clamp01(v - p.erosionRate * shortage * Mathf.Max(0f, dt));
        }

        public static float TruthErosionTick(float verifiability, int survivingWitnesses, float dt)
            => TruthErosionTick(verifiability, survivingWitnesses, dt, HistoriographyParams.Default);

        /// <summary>
        /// 歴史家の抵抗力（0..1）。学問の自由 academicFreedom(0..1) が土台で、公式圧力 officialPressure(0..1) が
        /// suppressionWeight の重みでそれを削る。自由がゼロなら抵抗力もゼロ＝学問の自由だけが歪みを抑える。
        /// </summary>
        public static float HistorianIntegrity(float officialPressure, float academicFreedom, HistoriographyParams p)
        {
            float freedom = Mathf.Clamp01(academicFreedom);
            float pressure = Mathf.Clamp01(officialPressure);
            return Mathf.Clamp01(freedom * (1f - pressure * p.suppressionWeight));
        }

        public static float HistorianIntegrity(float officialPressure, float academicFreedom)
            => HistorianIntegrity(officialPressure, academicFreedom, HistoriographyParams.Default);

        /// <summary>
        /// 最終的な歴史の審判（0..1）。公式評価 official から実際の功罪 trueDeeds へ、
        /// 歴史家の抵抗力 integrity × 検証可能性 verifiability のぶんだけ収束する：
        /// 自由な学問（integrity=1）は長期で真実へ書き直し、統制下（integrity=0）は政権の都合のまま。
        /// 学問が自由でも証人と記録が失われていれば（verifiability 低）真実へは戻り切れない。
        /// </summary>
        public static float LongRunVerdict(float official, float trueDeeds, float integrity, float verifiability = 1f)
        {
            float from = Mathf.Clamp01(official);
            float to = Mathf.Clamp01(trueDeeds);
            float weight = Mathf.Clamp01(integrity) * Mathf.Clamp01(verifiability);
            return Mathf.Lerp(from, to, weight);
        }
    }
}
