using UnityEngine;

namespace Ginei
{
    /// <summary>心理戦（対敵）の調整係数。降伏勧告・宣伝放送・絶望感の演出で敵の戦意を挫く強さと、
    /// 敵の結束・大義への信念がそれに抗う度合いを決める。</summary>
    public readonly struct PsyOpsParams
    {
        /// <summary>宣伝の基礎到達効率（放送出力1で届く度合いのスケール）。</summary>
        public readonly float reachScale;
        /// <summary>士気侵食の鋭さ（届いた宣伝1あたり士気を削る強さ）。</summary>
        public readonly float erosionScale;
        /// <summary>投降誘発の強さ（侵食×絶望感に掛かる投降の鋭さ）。</summary>
        public readonly float surrenderScale;
        /// <summary>離反誘発の強さ（侵食に掛かる離反の鋭さ）。</summary>
        public readonly float defectionScale;
        /// <summary>戦意低下の合成重み（侵食と投降誘発を束ねる重み）。</summary>
        public readonly float willWeight;
        /// <summary>高圧の逆効果スケール（高圧的すぎると敵の結束を固める背水の陣効果の強さ）。</summary>
        public readonly float backfireScale;
        /// <summary>逆効果が始まる強要の閾値（これ以下の穏当な勧告は逆効果を生まない）。</summary>
        public readonly float coercionThreshold;

        public PsyOpsParams(float reachScale, float erosionScale, float surrenderScale,
                            float defectionScale, float willWeight, float backfireScale,
                            float coercionThreshold)
        {
            this.reachScale = Mathf.Clamp(reachScale, 0f, 2f);
            this.erosionScale = Mathf.Clamp(erosionScale, 0f, 2f);
            this.surrenderScale = Mathf.Clamp(surrenderScale, 0f, 2f);
            this.defectionScale = Mathf.Clamp(defectionScale, 0f, 2f);
            this.willWeight = Mathf.Clamp01(willWeight);
            this.backfireScale = Mathf.Clamp01(backfireScale);
            this.coercionThreshold = Mathf.Clamp01(coercionThreshold);
        }

        /// <summary>既定＝到達1.0・侵食0.8・投降0.7・離反0.5・戦意重み0.6・逆効果0.5・強要閾値0.6。</summary>
        public static PsyOpsParams Default =>
            new PsyOpsParams(1.0f, 0.8f, 0.7f, 0.5f, 0.6f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 心理戦（対敵）の純ロジック。戦わずして敵の戦意を挫く＝降伏勧告・包囲の絶望感の演出・宣伝放送で
    /// 敵兵の士気を削り、離反・投降を促す。だが効果は敵の結束・大義への信念に阻まれ、高圧的すぎる強要は
    /// かえって敵の結束を固める（背水の陣）。
    /// <para>
    /// 分担：<c>PropagandaRules</c>（自軍向け/汎用の宣伝効果）・<c>MoraleContagionRules</c>（勝敗の士気伝播）とは別＝
    /// こちらは<b>対敵の心理作戦</b>に特化し、<c>SurrenderRules</c>（投降の解決）の<b>前段</b>＝投降を「促す」度合いを出す。
    /// すべて盤面非依存の plain 引数・実効値パターン（基準値非破壊）・乱数なし。
    /// </para>
    /// </summary>
    public static class PsyOpsRules
    {
        // ---- 宣伝の到達（孤立した敵ほど届く）----

        /// <summary>宣伝が敵に届く度合い（0..1）。放送出力が高く、敵が孤立しているほど効く。</summary>
        public static float PropagandaReach(float broadcastPower, float enemyIsolation, PsyOpsParams p)
        {
            broadcastPower = Mathf.Clamp01(broadcastPower);
            enemyIsolation = Mathf.Clamp01(enemyIsolation);
            // 出力×到達効率 を孤立度で増幅（孤立0で半分・孤立1で満額）。
            float isolationGain = 0.5f + 0.5f * enemyIsolation;
            return Mathf.Clamp01(broadcastPower * p.reachScale * isolationGain);
        }

        public static float PropagandaReach(float broadcastPower, float enemyIsolation) =>
            PropagandaReach(broadcastPower, enemyIsolation, PsyOpsParams.Default);

        // ---- 士気の侵食（信念が固いと抗う）----

        /// <summary>敵士気を削る度合い（0..1）。届いた宣伝に比例し、敵の大義への信念が固いほど抗う。</summary>
        public static float MoraleErosion(float propagandaReach, float enemyConviction, PsyOpsParams p)
        {
            propagandaReach = Mathf.Clamp01(propagandaReach);
            enemyConviction = Mathf.Clamp01(enemyConviction);
            float resist = 1f - enemyConviction; // 信念1で完全に抗う。
            return Mathf.Clamp01(propagandaReach * p.erosionScale * resist);
        }

        public static float MoraleErosion(float propagandaReach, float enemyConviction) =>
            MoraleErosion(propagandaReach, enemyConviction, PsyOpsParams.Default);

        // ---- 投降の誘発（絶望感が強いほど）----

        /// <summary>投降を促す度合い（0..1）。士気侵食に絶望感を掛けて高める。</summary>
        public static float SurrenderInducement(float moraleErosion, float hopelessness, PsyOpsParams p)
        {
            moraleErosion = Mathf.Clamp01(moraleErosion);
            hopelessness = Mathf.Clamp01(hopelessness);
            return Mathf.Clamp01(moraleErosion * hopelessness * p.surrenderScale);
        }

        public static float SurrenderInducement(float moraleErosion, float hopelessness) =>
            SurrenderInducement(moraleErosion, hopelessness, PsyOpsParams.Default);

        // ---- 離反の誘発（忠誠が低いほど）----

        /// <summary>離反を誘発する度合い（0..1）。士気侵食に比例し、敵の忠誠が高いほど抑えられる。</summary>
        public static float DefectionRate(float moraleErosion, float enemyLoyalty, PsyOpsParams p)
        {
            moraleErosion = Mathf.Clamp01(moraleErosion);
            enemyLoyalty = Mathf.Clamp01(enemyLoyalty);
            float disloyal = 1f - enemyLoyalty; // 忠誠1で離反しない。
            return Mathf.Clamp01(moraleErosion * disloyal * p.defectionScale);
        }

        public static float DefectionRate(float moraleErosion, float enemyLoyalty) =>
            DefectionRate(moraleErosion, enemyLoyalty, PsyOpsParams.Default);

        // ---- 逆宣伝（敵の心理戦に抗う自軍の結束）----

        /// <summary>敵の心理戦が削る自軍士気を、自軍の結束で打ち消した残り（0..1）。結束が固いほど被害は小さい。</summary>
        public static float Counterpropaganda(float enemyMoraleOps, float ownCohesion)
        {
            enemyMoraleOps = Mathf.Clamp01(enemyMoraleOps);
            ownCohesion = Mathf.Clamp01(ownCohesion);
            return Mathf.Clamp01(enemyMoraleOps * (1f - ownCohesion));
        }

        // ---- 高圧の逆効果（背水の陣）----

        /// <summary>高圧的すぎる強要が逆に敵の結束を固める度合い（0..1）。閾値以下は0。</summary>
        public static float BackfireFromHarshness(float coercion, PsyOpsParams p)
        {
            coercion = Mathf.Clamp01(coercion);
            if (coercion <= p.coercionThreshold) return 0f;
            float denom = 1f - p.coercionThreshold;
            if (denom <= 0f) return 0f;
            float excess = (coercion - p.coercionThreshold) / denom; // 0..1
            return Mathf.Clamp01(excess * p.backfireScale);
        }

        public static float BackfireFromHarshness(float coercion) =>
            BackfireFromHarshness(coercion, PsyOpsParams.Default);

        // ---- 敵全体の戦意低下 ----

        /// <summary>敵全体の戦意低下（0..1）。士気侵食と投降誘発を重みで合成する。</summary>
        public static float WillToFightReduction(float moraleErosion, float surrenderInducement, PsyOpsParams p)
        {
            moraleErosion = Mathf.Clamp01(moraleErosion);
            surrenderInducement = Mathf.Clamp01(surrenderInducement);
            float w = Mathf.Clamp01(p.willWeight);
            return Mathf.Clamp01(moraleErosion * w + surrenderInducement * (1f - w));
        }

        public static float WillToFightReduction(float moraleErosion, float surrenderInducement) =>
            WillToFightReduction(moraleErosion, surrenderInducement, PsyOpsParams.Default);

        // ---- 戦意を挫いたか ----

        /// <summary>敵の戦意を挫いたか（士気侵食が閾値以上）。</summary>
        public static bool IsEnemyDemoralized(float moraleErosion, float threshold)
        {
            moraleErosion = Mathf.Clamp01(moraleErosion);
            threshold = Mathf.Clamp01(threshold);
            return moraleErosion >= threshold;
        }
    }
}
