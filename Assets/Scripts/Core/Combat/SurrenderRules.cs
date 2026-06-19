using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 降伏判断の調整値（#降伏）。絶望度の重み配分と、降伏成立のしきい値を持つ。
    /// 値は ctor で全クランプ＝不正値で挙動が崩れない。
    /// </summary>
    public readonly struct SurrenderParams
    {
        /// <summary>絶望度における兵力比の重み（劣勢ほど絶望）。</summary>
        public readonly float ratioWeight;
        /// <summary>絶望度における包囲度の重み（退路がないほど絶望）。</summary>
        public readonly float encirclementWeight;
        /// <summary>絶望度における補給枯渇の重み（弾薬・燃料が尽きるほど絶望）。</summary>
        public readonly float supplyWeight;
        /// <summary>降伏が成立する正味傾きの既定しきい値（0..1）。</summary>
        public readonly float surrenderThreshold;

        public SurrenderParams(float ratioWeight, float encirclementWeight, float supplyWeight, float surrenderThreshold)
        {
            // 重みは非負にクランプし合計1へ正規化（合計0なら均等配分）
            float r = Mathf.Max(0f, ratioWeight);
            float e = Mathf.Max(0f, encirclementWeight);
            float s = Mathf.Max(0f, supplyWeight);
            float sum = r + e + s;
            if (sum <= 0f)
            {
                r = e = s = 1f;
                sum = 3f;
            }
            this.ratioWeight = r / sum;
            this.encirclementWeight = e / sum;
            this.supplyWeight = s / sum;
            this.surrenderThreshold = Mathf.Clamp01(surrenderThreshold);
        }

        /// <summary>既定：兵力比0.5・包囲0.3・補給0.2、成立しきい値0.5。</summary>
        public static SurrenderParams Default
            => new SurrenderParams(DefaultRatioWeight, DefaultEncirclementWeight, DefaultSupplyWeight, DefaultSurrenderThreshold);

        public const float DefaultRatioWeight = 0.5f;
        public const float DefaultEncirclementWeight = 0.3f;
        public const float DefaultSupplyWeight = 0.2f;
        public const float DefaultSurrenderThreshold = 0.5f;
    }

    /// <summary>
    /// 降伏（勝ち目のない部隊が抵抗をやめる・名誉と実利の判断）の純ロジック（#降伏）。
    /// 包囲され勝ち目を失った部隊は、兵の命を助けるため降伏を選びうる。
    /// 一方で<b>名誉・大義・指揮官の意地</b>（背水・死兵）は抵抗を続けさせ、
    /// 敵の<b>助命・寛大な降伏条件</b>（QuarterOffered）は降伏を促す。
    /// 絶望度（勝ち目のなさ）→降伏への傾き／それに抗う名誉の抵抗／助命の誘因を合成し、
    /// 正味の降伏確率としきい値で<b>降伏するか否か</b>を決める。
    ///
    /// 分担：<see cref="PsyOpsRules"/>（心理戦）の<b>帰結</b>＝降伏の意思決定をここで判断する。
    /// <see cref="CaptivityRules"/>（捕虜処遇）の<b>前段</b>＝降伏そのもの（捕虜が発生する瞬間）を担う。
    /// <see cref="SutegamariRules"/>（敗走時の殿・死兵）とは別物＝あちらは「逃がすための死兵」、
    /// こちらは「抵抗をやめて生き延びる」選択。盤面非依存・plain引数・乱数なし・test-first。
    /// </summary>
    public static class SurrenderRules
    {
        // ───────── 絶望度（勝ち目のなさ） ─────────

        /// <summary>既定パラメータ版。</summary>
        public static float HopelessnessLevel(float strengthRatio, float encirclement, float supplyExhaustion)
            => HopelessnessLevel(strengthRatio, encirclement, supplyExhaustion, SurrenderParams.Default);

        /// <summary>
        /// 絶望度（0..1）＝勝ち目のなさ。兵力劣勢・包囲・補給枯渇の加重平均。
        /// <paramref name="strengthRatio"/>＝自軍/敵軍の兵力比（小さいほど劣勢＝絶望）。
        /// 比1.0以上で兵力面の絶望は0、比0で1。包囲度・補給枯渇は 0..1。
        /// </summary>
        public static float HopelessnessLevel(float strengthRatio, float encirclement, float supplyExhaustion, SurrenderParams p)
        {
            // 兵力劣勢度：比1.0で0、比0で1（互角以上は絶望せず）
            float ratio = Mathf.Max(0f, strengthRatio);
            float inferiority = Mathf.Clamp01(1f - Mathf.Min(1f, ratio));
            float enc = Mathf.Clamp01(encirclement);
            float sup = Mathf.Clamp01(supplyExhaustion);

            return Mathf.Clamp01(
                inferiority * p.ratioWeight
                + enc * p.encirclementWeight
                + sup * p.supplyWeight);
        }

        // ───────── 降伏への傾き ─────────

        /// <summary>
        /// 降伏への傾き（0..1）＝絶望度が高く、兵の士気が低いほど降りやすい。
        /// 士気が高い部隊は絶望してもなお戦う＝降伏傾きを抑える。入力 troopMorale は 0..100。
        /// </summary>
        public static float SurrenderInclination(float hopelessnessLevel, float troopMorale)
        {
            float hope = Mathf.Clamp01(hopelessnessLevel);
            float morale = Mathf.Clamp01(troopMorale / 100f);
            // 士気が高いほど降伏傾きを割り引く（士気1.0で半減、0で素通し）
            return Mathf.Clamp01(hope * (1f - 0.5f * morale));
        }

        // ───────── 名誉の抵抗 ─────────

        /// <summary>
        /// 名誉・信念による降伏拒否（0..1）＝指揮官の意地(commanderPride)と大義への確信(causeConviction)。
        /// どちらか一方でも高ければ抵抗は強い＝<b>大きい方</b>を採り、もう一方が補強する。入力は 0..100。
        /// </summary>
        public static float HonorResistance(float commanderPride, float causeConviction)
        {
            float pride = Mathf.Clamp01(commanderPride / 100f);
            float cause = Mathf.Clamp01(causeConviction / 100f);
            // 主因＝高い方、従因＝低い方を控えめに加算（上限1）
            float major = Mathf.Max(pride, cause);
            float minor = Mathf.Min(pride, cause);
            return Mathf.Clamp01(major + minor * (1f - major) * 0.5f);
        }

        // ───────── 助命・寛大な条件 ─────────

        /// <summary>
        /// 助命・寛大な降伏条件の誘因（0..1）＝敵将の信義(captorReputation)と提示された条件(surrenderTerms)。
        /// 信義に厚い相手・寛大な条件ほど降伏しやすい。両者を要する（幾何平均＝片方が劣悪なら降りにくい）。入力は 0..100。
        /// </summary>
        public static float QuarterOffered(float captorReputation, float surrenderTerms)
        {
            float rep = Mathf.Clamp01(captorReputation / 100f);
            float terms = Mathf.Clamp01(surrenderTerms / 100f);
            return Mathf.Sqrt(Mathf.Max(0f, rep * terms));
        }

        // ───────── 正味の降伏傾き ─────────

        /// <summary>
        /// 降伏する正味の傾き（0..1）。降伏傾き(surrenderInclination)から名誉の抵抗を引き、助命の誘因で底上げ。
        /// 助命は残余（まだ降っていない分）に対して働く＝寛大な条件が抵抗を和らげる。
        /// </summary>
        public static float NetSurrenderProbability(float surrenderInclination, float honorResistance, float quarterOffered)
        {
            float incl = Mathf.Clamp01(surrenderInclination);
            float honor = Mathf.Clamp01(honorResistance);
            float quarter = Mathf.Clamp01(quarterOffered);

            // 名誉が傾きを削る（残った傾きが基礎）
            float baseProb = Mathf.Clamp01(incl * (1f - honor));
            // 助命は残余分を誘い込む（baseProb から 1 への引き上げ）
            return Mathf.Clamp01(baseProb + (1f - baseProb) * quarter);
        }

        // ───────── 背水・死兵 ─────────

        /// <summary>
        /// 絶望してもなお降らず死兵となる度合い（0..1・背水の陣）。名誉の抵抗が高く、絶望が深いほど高い。
        /// 「もはや勝てぬが屈しもしない」＝玉砕の覚悟。両者の<b>幾何平均</b>＝名誉だけ／絶望だけでは死兵にならない。
        /// </summary>
        public static float LastStandResolve(float honorResistance, float hopelessnessLevel)
        {
            float honor = Mathf.Clamp01(honorResistance);
            float hope = Mathf.Clamp01(hopelessnessLevel);
            return Mathf.Sqrt(Mathf.Max(0f, honor * hope));
        }

        // ───────── 捕虜獲得 ─────────

        /// <summary>
        /// 降伏により得る捕虜数。降伏した部隊の兵力(surrenderingStrength)に、捕獲側の制圧度(captorControl 0..1)を乗ずる。
        /// 制圧が緩いと混乱で取り逃がす＝完全制圧で全数。負値は0に丸める。
        /// </summary>
        public static int PrisonersTaken(int surrenderingStrength, float captorControl)
        {
            int strength = Mathf.Max(0, surrenderingStrength);
            float control = Mathf.Clamp01(captorControl);
            return Mathf.RoundToInt(strength * control);
        }

        // ───────── 降伏判定 ─────────

        /// <summary>既定しきい値版。</summary>
        public static bool DoesUnitSurrender(float netSurrenderProbability)
            => DoesUnitSurrender(netSurrenderProbability, SurrenderParams.DefaultSurrenderThreshold);

        /// <summary>部隊が降伏するか＝正味の降伏傾きがしきい値以上（決定論・乱数なし）。</summary>
        public static bool DoesUnitSurrender(float netSurrenderProbability, float threshold)
            => Mathf.Clamp01(netSurrenderProbability) >= Mathf.Clamp01(threshold);
    }
}
