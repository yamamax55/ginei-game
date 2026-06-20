using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 親衛隊（近衛）の両刃の純ロジック（プラエトリアニ型・唯一の窓口）。君主直属の精鋭は
    /// <b>守護者にして簒奪者</b>＝厚遇するほど政治力を持ち皇帝を作り替える者になり、冷遇すれば守りが薄い。
    /// 厚遇のジレンマ＝<b>忠誠と危険が同時に育つ</b>（ローマ近衛が皇帝を競売にかけた構図）を式に出す。
    /// 乱数なし・全入力クランプの決定論。test-first。
    /// <para><see cref="CivilianControlRules"/>（軍全体の文民統制＝軍と政府のどちらが上か）とは別。
    /// <see cref="CoupRules"/>（一般のクーデター解決）とも別＝こちらは<b>君主直属の近衛</b>という特殊問題
    /// （身辺安全と簒奪リスクが同じ精鋭から生まれる）に限る。</para>
    /// </summary>
    public static class PraetorianRules
    {
        /// <summary>近衛の両刃を回す調整値。</summary>
        public readonly struct PraetorianParams
        {
            /// <summary>厚遇（特権）が忠誠を上げる速さ。</summary>
            public readonly float pamperGain;

            /// <summary>厚遇が政治力（近さ）を太らせる速さ＝甘やかすほど発言力が増す副作用。</summary>
            public readonly float leverageCreep;

            /// <summary>厚遇による忠誠の自然減衰（恩は風化する）。</summary>
            public readonly float loyaltyDecay;

            /// <summary>このリスク以上で簒奪・廃立が現実化する閾値（0..1）。</summary>
            public readonly float kingmakerThreshold;

            public PraetorianParams(float pamperGain, float leverageCreep, float loyaltyDecay, float kingmakerThreshold)
            {
                this.pamperGain = Mathf.Max(0f, pamperGain);
                this.leverageCreep = Mathf.Max(0f, leverageCreep);
                this.loyaltyDecay = Mathf.Max(0f, loyaltyDecay);
                this.kingmakerThreshold = Mathf.Clamp01(kingmakerThreshold);
            }

            /// <summary>既定＝厚遇0.15／政治力増0.10／減衰0.02／簒奪閾値0.6。</summary>
            public static PraetorianParams Default => new PraetorianParams(0.15f, 0.10f, 0.02f, 0.6f);
        }

        /// <summary>
        /// 君主の身辺安全（0..1）＝守護者としての価値。精鋭の強さ×忠誠（強くても不忠なら守らない）。
        /// </summary>
        public static float ProtectionStrength(float guardStrength, float loyalty)
            => Mathf.Clamp01(guardStrength) * Mathf.Clamp01(loyalty);

        /// <summary>
        /// 近衛の政治力（0..1）＝君主に近く強いほど発言力を持つ（<b>守る者が支配する</b>）。
        /// 強さと近さの積＝どちらか欠ければ政治力にならない。
        /// </summary>
        public static float PoliticalLeverage(float guardStrength, float proximity)
            => Mathf.Clamp01(guardStrength) * Mathf.Clamp01(proximity);

        /// <summary>
        /// 簒奪・廃立リスク（0..1）＝強い政治力＋低忠誠＝皇帝を作り替える者
        /// （ローマ近衛が皇帝を競売にかけた）。政治力に不忠分を掛ける＝忠誠1なら理屈の上ではリスク0。
        /// </summary>
        public static float KingmakerRisk(float politicalLeverage, float loyalty)
            => Mathf.Clamp01(politicalLeverage) * (1f - Mathf.Clamp01(loyalty));

        /// <summary>簒奪・廃立が現実化するか（リスクが閾値以上）。</summary>
        public static bool WouldDepose(float politicalLeverage, float loyalty, PraetorianParams prm)
            => KingmakerRisk(politicalLeverage, loyalty) >= prm.kingmakerThreshold;

        /// <summary>既定パラメータ版。</summary>
        public static bool WouldDepose(float politicalLeverage, float loyalty)
            => WouldDepose(politicalLeverage, loyalty, PraetorianParams.Default);

        /// <summary>
        /// 厚遇による忠誠の更新（厚遇のジレンマの核）。特権で忠誠は上がる（pamperGain×privileges）が、
        /// <b>恩は風化する</b>（loyaltyDecay）。次の<see cref="LeverageCreep"/>と対で見ること＝
        /// 同じ厚遇が政治力も育てる＝忠誠と危険が同時に育つ。
        /// </summary>
        public static float PamperingTick(float loyalty, float privileges, float dt, PraetorianParams prm)
        {
            float l = Mathf.Clamp01(loyalty);
            float p = Mathf.Clamp01(privileges);
            float t = Mathf.Max(0f, dt);
            float next = l + (prm.pamperGain * p - prm.loyaltyDecay) * t;
            return Mathf.Clamp01(next);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float PamperingTick(float loyalty, float privileges, float dt)
            => PamperingTick(loyalty, privileges, dt, PraetorianParams.Default);

        /// <summary>
        /// 厚遇が近衛の政治力（近さ）を太らせる更新（ジレンマの裏面）。<see cref="PamperingTick"/>と
        /// <b>同じ特権</b>を入力に取り＝甘やかすほど発言力が増す＝忠誠と危険が同時に育つ。減衰なし（既得は手放さない）。
        /// </summary>
        public static float LeverageCreep(float proximity, float privileges, float dt, PraetorianParams prm)
        {
            float prox = Mathf.Clamp01(proximity);
            float p = Mathf.Clamp01(privileges);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(prox + prm.leverageCreep * p * t);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float LeverageCreep(float proximity, float privileges, float dt)
            => LeverageCreep(proximity, privileges, dt, PraetorianParams.Default);

        /// <summary>
        /// 冷遇の脆弱性（0..1）＝近衛を弱めれば暗殺・クーデターに無防備（投資が薄いほど高い）。
        /// 簒奪リスクを下げる代わりに身辺が危うくなる＝ジレンマのもう一方の角。
        /// </summary>
        public static float NeglectVulnerability(float guardInvestment)
            => 1f - Mathf.Clamp01(guardInvestment);

        /// <summary>
        /// 最適な近衛規模（0..1）＝外部脅威と内部信頼の綱引き。外部脅威が高いほど強い近衛が要り、
        /// 内部信頼が低い（簒奪を恐れる）ほど近衛を絞る。<b>強すぎても弱すぎても危ない谷</b>＝
        /// 両者の中庸（平均）に落とす。
        /// </summary>
        public static float OptimalGuardStrength(float externalThreat, float internalTrust)
        {
            float threat = Mathf.Clamp01(externalThreat);
            float trust = Mathf.Clamp01(internalTrust);
            return Mathf.Clamp01((threat + trust) * 0.5f);
        }
    }
}
