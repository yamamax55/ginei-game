using UnityEngine;

namespace Ginei
{
    /// <summary>反体制・異論（言論/思想の体制批判）の調整係数（発生・地下化・殉教・良心・亡命・圧力の重み）。</summary>
    public readonly struct DissidentParams
    {
        /// <summary>異論発生の重み（不満×言論の自由×知識層に掛ける）。</summary>
        public readonly float emergenceWeight;
        /// <summary>地下化の重み（異論×弾圧に掛ける＝弾圧されると潜る）。</summary>
        public readonly float undergroundWeight;
        /// <summary>殉教共感の重み（弾圧×知名度に掛ける＝弾圧の逆説）。</summary>
        public readonly float martyrWeight;
        /// <summary>知識人の良心の重み（知識層×体制の不正の幾何平均に掛ける）。</summary>
        public readonly float conscienceWeight;
        /// <summary>亡命の声の重み（亡命×国際的注目の幾何平均に掛ける）。</summary>
        public readonly float exileWeight;
        /// <summary>体制圧力のうち殉教共感の寄与（共感/良心/亡命の合計＝1）。</summary>
        public readonly float pressureMartyrWeight;
        /// <summary>体制圧力のうち知識人の良心の寄与。</summary>
        public readonly float pressureConscienceWeight;
        /// <summary>体制圧力のうち亡命の声の寄与。</summary>
        public readonly float pressureExileWeight;
        /// <summary>弾圧正味効果で殉教共感を差し引く重み（強硬の逆効果）。</summary>
        public readonly float suppressionBacklashWeight;

        public DissidentParams(float emergenceWeight, float undergroundWeight, float martyrWeight,
            float conscienceWeight, float exileWeight,
            float pressureMartyrWeight, float pressureConscienceWeight, float pressureExileWeight,
            float suppressionBacklashWeight)
        {
            this.emergenceWeight = Mathf.Clamp01(emergenceWeight);
            this.undergroundWeight = Mathf.Clamp01(undergroundWeight);
            this.martyrWeight = Mathf.Clamp01(martyrWeight);
            this.conscienceWeight = Mathf.Clamp01(conscienceWeight);
            this.exileWeight = Mathf.Clamp01(exileWeight);
            this.pressureMartyrWeight = Mathf.Clamp01(pressureMartyrWeight);
            this.pressureConscienceWeight = Mathf.Clamp01(pressureConscienceWeight);
            this.pressureExileWeight = Mathf.Clamp01(pressureExileWeight);
            this.suppressionBacklashWeight = Mathf.Clamp01(suppressionBacklashWeight);
        }

        /// <summary>
        /// 既定＝発生重み1.0・地下化重み1.0・殉教重み1.0・良心重み1.0・亡命重み1.0・
        /// 圧力配分（共感0.4／良心0.35／亡命0.25）・弾圧逆効果重み1.0。
        /// </summary>
        public static DissidentParams Default =>
            new DissidentParams(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.4f, 0.35f, 0.25f, 1.0f);
    }

    /// <summary>
    /// 体制に異を唱える反体制派・異論者（言論・思想の体制批判）の純ロジック。
    /// 異論の発生、地下活動への移行、弾圧と殉教（弾圧が共感を生む逆説）、知識人の良心、
    /// 亡命と国外からの批判、体制への漸進的圧力、そして弾圧の正味効果と封じ込めの可否を扱う。
    /// <para>
    /// 分担：<see cref="ResistanceMovementRules"/> は武力抵抗組織（地下細胞・破壊活動）の存続力学、
    /// 本クラスは言論・思想の反体制＝弾圧してもむしろ共感と良心と亡命の声で体制が漸進的に蝕まれる逆説に特化する。
    /// </para>
    /// 全入力クランプ・乱数なし決定論・実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DissidentRules
    {
        /// <summary>
        /// 異論の発生（0..1）＝不満 grievance×言論の自由 freedomOfExpression×知識層 intellectualBase×重み。
        /// 不満があり、語れる自由があり、語る知識層がいて初めて異論が生まれる（どれか欠ければ声にならない）。
        /// </summary>
        public static float DissentEmergence(float grievance, float freedomOfExpression, float intellectualBase, DissidentParams p)
        {
            float drive = Mathf.Clamp01(grievance) * Mathf.Clamp01(freedomOfExpression) * Mathf.Clamp01(intellectualBase);
            return Mathf.Clamp01(drive * p.emergenceWeight);
        }

        public static float DissentEmergence(float grievance, float freedomOfExpression, float intellectualBase)
            => DissentEmergence(grievance, freedomOfExpression, intellectualBase, DissidentParams.Default);

        /// <summary>
        /// 地下活動への移行（0..1）＝異論 dissentEmergence×弾圧 repression×重み。
        /// 公然と語れぬほど（弾圧が強いほど）異論は地下に潜る＝弾圧0なら潜る必要がない。
        /// </summary>
        public static float Underground(float dissentEmergence, float repression, DissidentParams p)
        {
            float t = Mathf.Clamp01(dissentEmergence) * Mathf.Clamp01(repression);
            return Mathf.Clamp01(t * p.undergroundWeight);
        }

        public static float Underground(float dissentEmergence, float repression)
            => Underground(dissentEmergence, repression, DissidentParams.Default);

        /// <summary>
        /// 殉教の共感（0..1）＝弾圧 repression×異論者の知名度 dissidentVisibility×重み（弾圧の逆説）。
        /// 名の知れた異論者を弾圧するほど殉教者として共感を呼ぶ＝無名なら弾圧しても響かない。
        /// </summary>
        public static float MartyrSympathy(float repression, float dissidentVisibility, DissidentParams p)
        {
            float t = Mathf.Clamp01(repression) * Mathf.Clamp01(dissidentVisibility);
            return Mathf.Clamp01(t * p.martyrWeight);
        }

        public static float MartyrSympathy(float repression, float dissidentVisibility)
            => MartyrSympathy(repression, dissidentVisibility, DissidentParams.Default);

        /// <summary>
        /// 知識人の良心の覚醒（0..1）＝知識層 intellectualBase×体制の不正 regimeInjustice の幾何平均×重み。
        /// 不正が露わでも語る知識層がいなければ覚醒せず、知識層がいても不正が無ければ覚醒しない（どちらか0なら不発）。
        /// </summary>
        public static float IntellectualConscience(float intellectualBase, float regimeInjustice, DissidentParams p)
        {
            float ib = Mathf.Clamp01(intellectualBase);
            float inj = Mathf.Clamp01(regimeInjustice);
            float geo = Mathf.Sqrt(ib * inj);
            return Mathf.Clamp01(geo * p.conscienceWeight);
        }

        public static float IntellectualConscience(float intellectualBase, float regimeInjustice)
            => IntellectualConscience(intellectualBase, regimeInjustice, DissidentParams.Default);

        /// <summary>
        /// 国外からの批判の声（0..1）＝亡命 emigration×国際的注目 internationalAttention の幾何平均×重み。
        /// 亡命者がいても世界が無関心なら声は届かず、注目があっても語る亡命者が無ければ声にならない（どちらか0なら届かない）。
        /// </summary>
        public static float ExileVoice(float emigration, float internationalAttention, DissidentParams p)
        {
            float em = Mathf.Clamp01(emigration);
            float att = Mathf.Clamp01(internationalAttention);
            float geo = Mathf.Sqrt(em * att);
            return Mathf.Clamp01(geo * p.exileWeight);
        }

        public static float ExileVoice(float emigration, float internationalAttention)
            => ExileVoice(emigration, internationalAttention, DissidentParams.Default);

        /// <summary>
        /// 体制への漸進的圧力（0..1）＝殉教共感 martyrSympathy×良心 intellectualConscience×亡命の声 exileVoice の重み付き和。
        /// 内の共感・内の良心・外の声が積み上がって体制をじわじわ蝕む（一気にではなく漸進）。
        /// </summary>
        public static float RegimePressure(float martyrSympathy, float intellectualConscience, float exileVoice, DissidentParams p)
        {
            float pressure = Mathf.Clamp01(martyrSympathy) * p.pressureMartyrWeight
                           + Mathf.Clamp01(intellectualConscience) * p.pressureConscienceWeight
                           + Mathf.Clamp01(exileVoice) * p.pressureExileWeight;
            return Mathf.Clamp01(pressure);
        }

        public static float RegimePressure(float martyrSympathy, float intellectualConscience, float exileVoice)
            => RegimePressure(martyrSympathy, intellectualConscience, exileVoice, DissidentParams.Default);

        /// <summary>
        /// 弾圧の正味効果（-1..1）＝弾圧 repression − 殉教共感 martyrSympathy×逆効果重み。
        /// 弾圧は表向き声を封じるが、殉教共感が上回れば正味は負＝強硬がかえって体制を損なう逆説。
        /// </summary>
        public static float SuppressionEffect(float repression, float martyrSympathy, DissidentParams p)
        {
            float net = Mathf.Clamp01(repression) - Mathf.Clamp01(martyrSympathy) * p.suppressionBacklashWeight;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float SuppressionEffect(float repression, float martyrSympathy)
            => SuppressionEffect(repression, martyrSympathy, DissidentParams.Default);

        /// <summary>既定の封じ込め閾値（弾圧の正味効果から体制圧力を引いた余裕がこれ以上なら異論を抑え込めている）。</summary>
        public const float DefaultContainmentThreshold = 0.1f;

        /// <summary>
        /// 異論を抑え込めているか＝弾圧の正味効果 suppressionEffect − 体制圧力 regimePressure が threshold 以上。
        /// 正味効果が圧力を上回って初めて封じ込め＝逆効果（正味負）や圧力増大は封じ込めを破る。
        /// </summary>
        public static bool IsDissentContained(float suppressionEffect, float regimePressure, float threshold)
        {
            float margin = Mathf.Clamp(suppressionEffect, -1f, 1f) - Mathf.Clamp01(regimePressure);
            return margin >= Mathf.Clamp(threshold, -1f, 1f);
        }

        public static bool IsDissentContained(float suppressionEffect, float regimePressure)
            => IsDissentContained(suppressionEffect, regimePressure, DefaultContainmentThreshold);
    }
}
