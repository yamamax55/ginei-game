using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 斉射タイミングのパラメータ（一斉集中射撃＝サルヴォの調整値）。
    /// 全フィールド public・ctor で Clamp（実効値パターン＝基準値を壊さない）。
    /// </summary>
    public readonly struct SalvoTimingParams
    {
        /// <summary>斉射密度=1.0 に達する基準同時砲数（これ以上は頭打ち）。</summary>
        public readonly float referenceGuns;
        /// <summary>斉射密度の上限（0..1）。</summary>
        public readonly float maxConcentration;
        /// <summary>同時砲数の効きを曲げる指数（>1で多砲時に急峻・<1で逓減）。</summary>
        public readonly float concentrationExponent;
        /// <summary>密度→飽和度の窓口係数（敵迎撃に対する相対的な圧の強さ）。</summary>
        public readonly float saturationScale;
        /// <summary>飽和を抜けて命中する弾のスケール（0..1）。</summary>
        public readonly float penetrationScale;
        /// <summary>再装填中の無防備スケール（0..1）。</summary>
        public readonly float reloadVulnScale;
        /// <summary>無防備=最大になる基準再装填時間（秒・これ以上は頭打ち）。</summary>
        public readonly float referenceReload;
        /// <summary>一斉集中(alpha)←→持続射撃のトレードオフの効きスケール。</summary>
        public readonly float alphaBias;
        /// <summary>窓=1.0 になる基準シールド回復時間（秒）。回復が遅いほど窓が広い。</summary>
        public readonly float referenceShieldRecharge;

        public SalvoTimingParams(
            float referenceGuns,
            float maxConcentration,
            float concentrationExponent,
            float saturationScale,
            float penetrationScale,
            float reloadVulnScale,
            float referenceReload,
            float alphaBias,
            float referenceShieldRecharge)
        {
            this.referenceGuns = Mathf.Max(0.0001f, referenceGuns);
            this.maxConcentration = Mathf.Clamp01(maxConcentration);
            this.concentrationExponent = Mathf.Clamp(concentrationExponent, 0.1f, 8f);
            this.saturationScale = Mathf.Max(0.0001f, saturationScale);
            this.penetrationScale = Mathf.Clamp01(penetrationScale);
            this.reloadVulnScale = Mathf.Clamp01(reloadVulnScale);
            this.referenceReload = Mathf.Max(0.0001f, referenceReload);
            this.alphaBias = Mathf.Max(0f, alphaBias);
            this.referenceShieldRecharge = Mathf.Max(0.0001f, referenceShieldRecharge);
        }

        /// <summary>既定値。</summary>
        public static SalvoTimingParams Default =>
            new SalvoTimingParams(
                referenceGuns: 200f,
                maxConcentration: 1f,
                concentrationExponent: 0.7f,
                saturationScale: 1f,
                penetrationScale: 0.8f,
                reloadVulnScale: 0.5f,
                referenceReload: 4f,
                alphaBias: 1f,
                referenceShieldRecharge: 3f);
    }

    /// <summary>
    /// 斉射のタイミング＝一斉集中射撃（サルヴォ）で防御を飽和させる（純ロジック・static・盤面非依存）。
    /// バラバラに撃つより、全砲を同時に斉射して一点に集中すると、敵の迎撃（点防御/シールド）を
    /// 飽和させて貫ける。だが斉射の後は次弾までの再装填で無防備になる。
    /// ＝「一斉集中(alpha) か 持続射撃(sustained) か」のタイミングの判断に特化する。
    /// ・<see cref="SuppressionFireRules"/>（制圧射撃）とは別＝あちらは行動抑制の弾幕、
    ///   こちらは防御飽和のための同時集中とその裏の無防備。
    /// ・<see cref="WeaponTypeRules"/>（兵装の種別）とは別＝兵器の種類でなく撃つタイミングの話。
    /// 実効値パターン：基準値（Params）は壊さず、ローカルで実効値を計算する。盤面非依存の plain 引数。
    /// </summary>
    public static class SalvoTimingRules
    {
        // ---- 斉射密度（同時に撃てる砲数×射撃規律）----

        /// <summary>同時砲数×射撃規律で斉射の密度(0..1)を出す。砲数は referenceGuns で頭打ち。</summary>
        public static float SalvoConcentration(float participatingGuns, float fireDiscipline, SalvoTimingParams p)
        {
            float guns = Mathf.Clamp01(Mathf.Max(0f, participatingGuns) / p.referenceGuns);
            float curved = Mathf.Pow(guns, p.concentrationExponent);
            float discipline = Mathf.Clamp01(fireDiscipline);
            return Mathf.Clamp(curved * discipline * p.maxConcentration, 0f, p.maxConcentration);
        }

        public static float SalvoConcentration(float participatingGuns, float fireDiscipline) =>
            SalvoConcentration(participatingGuns, fireDiscipline, SalvoTimingParams.Default);

        // ---- 防御飽和（敵迎撃を飽和させる度合い）----

        /// <summary>斉射密度で敵の点防御を飽和させる度合い(0..1)。
        /// 密度が敵迎撃に対して大きいほど飽和（密度/(密度+迎撃) 形式）。</summary>
        public static float DefenseSaturation(float salvoConcentration, float enemyPointDefense, SalvoTimingParams p)
        {
            float conc = Mathf.Clamp01(salvoConcentration) * p.saturationScale;
            float pd = Mathf.Max(0f, enemyPointDefense);
            float denom = conc + pd;
            if (denom <= 0.0001f) return 0f;
            return Mathf.Clamp01(conc / denom);
        }

        public static float DefenseSaturation(float salvoConcentration, float enemyPointDefense) =>
            DefenseSaturation(salvoConcentration, enemyPointDefense, SalvoTimingParams.Default);

        // ---- 貫通命中（飽和を抜けて当たる弾）----

        /// <summary>飽和を抜けて命中する弾(0..1)＝密度×飽和度×貫通スケール。</summary>
        public static float PenetratingHits(float salvoConcentration, float defenseSaturation, SalvoTimingParams p)
        {
            float conc = Mathf.Clamp01(salvoConcentration);
            float sat = Mathf.Clamp01(defenseSaturation);
            return Mathf.Clamp01(conc * sat * p.penetrationScale);
        }

        public static float PenetratingHits(float salvoConcentration, float defenseSaturation) =>
            PenetratingHits(salvoConcentration, defenseSaturation, SalvoTimingParams.Default);

        // ---- 再装填の無防備 ----

        /// <summary>斉射後の再装填中の無防備(0..1)＝密度が高い（撃ち切った）ほど・再装填が長いほど無防備。</summary>
        public static float ReloadVulnerability(float salvoConcentration, float reloadTime, SalvoTimingParams p)
        {
            float conc = Mathf.Clamp01(salvoConcentration);
            float t = Mathf.Clamp01(Mathf.Max(0f, reloadTime) / p.referenceReload);
            return Mathf.Clamp01(conc * t * p.reloadVulnScale);
        }

        public static float ReloadVulnerability(float salvoConcentration, float reloadTime) =>
            ReloadVulnerability(salvoConcentration, reloadTime, SalvoTimingParams.Default);

        // ---- 一斉集中 vs 持続射撃（符号付き -1..1）----

        /// <summary>一斉集中(alpha=+1)か持続射撃(sustained=-1)かのトレードオフ(-1..1)。
        /// 斉射密度が持続火力を上回るほど +（alpha 寄り）、下回るほど −（持続寄り）、拮抗で 0。</summary>
        public static float SustainedVsAlpha(float salvoConcentration, float continuousFire, SalvoTimingParams p)
        {
            float conc = Mathf.Clamp01(salvoConcentration);
            float cont = Mathf.Clamp01(continuousFire);
            return Mathf.Clamp((conc - cont) * p.alphaBias, -1f, 1f);
        }

        public static float SustainedVsAlpha(float salvoConcentration, float continuousFire) =>
            SustainedVsAlpha(salvoConcentration, continuousFire, SalvoTimingParams.Default);

        // ---- タイミング窓（敵シールド回復前に当てる窓）----

        /// <summary>敵シールド回復前に次斉射を当てられる窓(0..1)。回復が遅い（時間が長い）ほど窓が広い。</summary>
        public static float TimingWindow(float enemyShieldRecharge, SalvoTimingParams p)
        {
            float recharge = Mathf.Max(0f, enemyShieldRecharge);
            return Mathf.Clamp01(recharge / p.referenceShieldRecharge);
        }

        public static float TimingWindow(float enemyShieldRecharge) =>
            TimingWindow(enemyShieldRecharge, SalvoTimingParams.Default);

        // ---- 斉射の正味効果（無防備を差し引く）----

        /// <summary>斉射の正味効果(0..1)＝貫通命中から再装填の無防備ぶんを差し引く。</summary>
        public static float VolleyEffectiveness(float penetratingHits, float reloadVulnerability)
        {
            float hits = Mathf.Clamp01(penetratingHits);
            float vuln = Mathf.Clamp01(reloadVulnerability);
            return Mathf.Clamp01(hits * (1f - vuln));
        }

        // ---- 飽和判定 ----

        /// <summary>防御を飽和させたか（しきい値以上）。</summary>
        public static bool IsDefenseSaturated(float defenseSaturation, float threshold)
        {
            return Mathf.Clamp01(defenseSaturation) >= Mathf.Clamp01(threshold);
        }

        public static bool IsDefenseSaturated(float defenseSaturation) =>
            IsDefenseSaturated(defenseSaturation, 0.5f);
    }
}
