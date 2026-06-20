using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍事請負将軍の政治力を回す調整値（ヴァレンシュタイン型・#1424）。
    /// </summary>
    public readonly struct KriegsherrParams
    {
        /// <summary>このリスク以上で「国家を脅かす強大化家臣（over-mighty subject）」と判定する閾値（0..1）。</summary>
        public readonly float overMightyThreshold;

        /// <summary>契約自治（私的所有）が政治力へ効く重み＝私兵化が発言力に転じる強さ。</summary>
        public readonly float autonomyWeight;

        public KriegsherrParams(float overMightyThreshold, float autonomyWeight)
        {
            this.overMightyThreshold = Mathf.Clamp01(overMightyThreshold);
            this.autonomyWeight = Mathf.Clamp01(autonomyWeight);
        }

        /// <summary>既定＝強大化家臣の閾値0.5／自治の政治力寄与0.6。</summary>
        public static KriegsherrParams Default => new KriegsherrParams(0.5f, 0.6f);
    }

    /// <summary>
    /// 軍事請負将軍（Kriegsherr）の純ロジック＝三十年戦争のヴァレンシュタイン型（#1424）。将軍は
    /// 皇帝に金で雇われるのでなく、<b>私的な信用・融資（財務レバレッジ）で大軍を編成して「所有」</b>し、
    /// その軍事力を背景に皇帝へ政治的要求（領地・地位・自治）を突きつける。軍が私兵化すると将軍が国家を脅かす。
    /// 私的融資→軍の所有→政治的要求の連鎖を式に出す。乱数なし・全入力クランプの決定論。test-first。
    /// <para><see cref="MercenaryRules"/>（傭兵の給与忠誠＝金で雇う戦力）とは別＝こちらは将軍自身が借金で軍を起こし所有する。
    /// <see cref="PraetorianRules"/>（君主直属の近衛の政治力＝守る者が支配する）とも別＝こちらは<b>私兵を持つ将軍</b>の政治力。
    /// <see cref="KontributionRules"/>（軍が占領地から自活し財政独立する＝軍税徴発）と接続＝
    /// <see cref="StateThreat"/> が「軍が国家を超える」局面を受ける。一般のクーデター解決は <see cref="CoupRules"/> が別途担う。</para>
    /// </summary>
    public static class KriegsherrRules
    {
        /// <summary>
        /// 財務レバレッジ（0..1）＝私的信用×期待される略奪収益で起こせる軍の規模（<b>借金で軍を起こす</b>）。
        /// 信用も略奪期待も欠ければ軍は起こせない＝両者の積。
        /// </summary>
        public static float FinancialLeverage(float privateCredit, float expectedPlunder)
            => Mathf.Clamp01(privateCredit) * Mathf.Clamp01(expectedPlunder);

        /// <summary>
        /// 軍の私的所有度（0..1）＝財務レバレッジ×契約自治＝将軍が軍を私的に所有する度合い
        /// （皇帝でなく将軍に忠誠＝私兵化）。自前で起こし自治を握るほど軍は将軍のものになる。
        /// </summary>
        public static float ArmyOwnership(float financialLeverage, float contractAutonomy)
            => Mathf.Clamp01(financialLeverage) * Mathf.Clamp01(contractAutonomy);

        /// <summary>
        /// 将軍の政治的発言力（0..1）＝私兵を背景に要求を突きつける力。私的所有度（皇帝でなく将軍の軍）と
        /// 軍規模（数の威圧）の積＝所有していても小勢なら威圧にならず、大軍でも皇帝の軍なら要求にならない。
        /// </summary>
        public static float PoliticalLeverage(float armyOwnership, float armySize)
            => Mathf.Clamp01(armyOwnership) * Mathf.Clamp01(armySize);

        /// <summary>
        /// 私兵将軍が国家（皇帝）を脅かすリスク（0..1）＝将軍の政治力×主権の弱さ。<b>軍が国家を超える</b>＝
        /// <see cref="KontributionRules.FiscalIndependenceFromState"/>（軍の財政独立）と接続。
        /// 主権が強ければ強大な将軍も御せ、弱ければ僅かな私兵でも脅威になる。
        /// </summary>
        public static float StateThreat(float politicalLeverage, float sovereignWeakness)
            => Mathf.Clamp01(politicalLeverage) * Mathf.Clamp01(sovereignWeakness);

        /// <summary>
        /// 債務不履行リスク（0..1）＝期待した略奪が得られないと借金が返せず軍が崩壊する（<b>私兵経済の脆さ</b>）。
        /// レバレッジが高いほど（借金が大きいほど）、略奪不足（plunderShortfall）が深いほど危うい＝両者の積。
        /// </summary>
        public static float DebtServiceRisk(float financialLeverage, float plunderShortfall)
            => Mathf.Clamp01(financialLeverage) * Mathf.Clamp01(plunderShortfall);

        /// <summary>
        /// 将軍への忠誠（0..1）＝兵が皇帝でなく将軍に忠誠を尽くす度合い（<b>私兵の論理</b>）。
        /// 私的所有度×給与の信頼性＝将軍が起こし将軍が払う軍ほど将軍に従う。
        /// </summary>
        public static float LoyaltyToGeneral(float armyOwnership, float paymentReliability)
            => Mathf.Clamp01(armyOwnership) * Mathf.Clamp01(paymentReliability);

        /// <summary>
        /// 解任の困難さ（0..1）＝強大化した私兵将軍は解任しにくい（<b>暗殺するしかない</b>＝ヴァレンシュタインの末路）。
        /// 政治力がそのまま解任への抵抗になる。
        /// </summary>
        public static float DismissalDifficulty(float politicalLeverage)
            => Mathf.Clamp01(politicalLeverage);

        /// <summary>国家を脅かすほど強大化した家臣（over-mighty subject）か＝国家への脅威が閾値以上。</summary>
        public static bool IsOverMightySubject(float stateThreat, float threshold)
            => Mathf.Clamp01(stateThreat) >= Mathf.Clamp01(threshold);

        /// <summary>既定パラメータ版（閾値に <see cref="KriegsherrParams.Default"/> を使う）。</summary>
        public static bool IsOverMightySubject(float stateThreat)
            => IsOverMightySubject(stateThreat, KriegsherrParams.Default.overMightyThreshold);
    }
}
