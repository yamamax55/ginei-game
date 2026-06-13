using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 制圧射撃のパラメータ（弾幕で敵の行動を抑えるための調整値）。
    /// 全フィールド public・ctor で Clamp（実効値パターン＝基準値を壊さない）。
    /// </summary>
    public readonly struct SuppressionFireParams
    {
        /// <summary>制圧度=1.0 に達する基準弾量（これ以上は頭打ち）。</summary>
        public readonly float referenceVolume;
        /// <summary>制圧度の上限（0..1）。</summary>
        public readonly float maxSuppression;
        /// <summary>制圧度→行動（反撃/前進）の鈍りスケール（0..1）。</summary>
        public readonly float actionPenaltyScale;
        /// <summary>制圧度→回避機動による陣形の乱れスケール（0..1）。指数で効きを曲げる。</summary>
        public readonly float maneuverScale;
        /// <summary>機動阻害の曲線指数（>1で高制圧時に急峻）。</summary>
        public readonly float maneuverExponent;
        /// <summary>弾量1あたり・秒あたりの弾薬消費。</summary>
        public readonly float ammoPerVolumeSecond;
        /// <summary>射撃が途切れたとき制圧が解ける速さ（毎秒）。</summary>
        public readonly float decayPerSecond;
        /// <summary>釘付け効果＝低士気の敵ほど抑えられる強さ（0..1）。</summary>
        public readonly float pinDownScale;
        /// <summary>援護射撃価値のスケール（味方機動×制圧度）。</summary>
        public readonly float coveringFireScale;

        public SuppressionFireParams(
            float referenceVolume,
            float maxSuppression,
            float actionPenaltyScale,
            float maneuverScale,
            float maneuverExponent,
            float ammoPerVolumeSecond,
            float decayPerSecond,
            float pinDownScale,
            float coveringFireScale)
        {
            this.referenceVolume = Mathf.Max(0.0001f, referenceVolume);
            this.maxSuppression = Mathf.Clamp01(maxSuppression);
            this.actionPenaltyScale = Mathf.Clamp01(actionPenaltyScale);
            this.maneuverScale = Mathf.Clamp01(maneuverScale);
            this.maneuverExponent = Mathf.Clamp(maneuverExponent, 0.1f, 8f);
            this.ammoPerVolumeSecond = Mathf.Max(0f, ammoPerVolumeSecond);
            this.decayPerSecond = Mathf.Max(0f, decayPerSecond);
            this.pinDownScale = Mathf.Clamp01(pinDownScale);
            this.coveringFireScale = Mathf.Max(0f, coveringFireScale);
        }

        /// <summary>既定値。</summary>
        public static SuppressionFireParams Default =>
            new SuppressionFireParams(
                referenceVolume: 100f,
                maxSuppression: 0.95f,
                actionPenaltyScale: 0.8f,
                maneuverScale: 0.6f,
                maneuverExponent: 1.5f,
                ammoPerVolumeSecond: 0.5f,
                decayPerSecond: 0.4f,
                pinDownScale: 0.5f,
                coveringFireScale: 1f);
    }

    /// <summary>
    /// 制圧射撃＝敵の行動を抑える弾幕（純ロジック・static・盤面非依存）。
    /// 損害を与えるためでなく、頭を上げさせない（回避機動・回頭を強いる）ための継続射撃。
    /// 制圧された敵は反撃・前進・陣形変更が鈍る。弾薬を消費し、効果は持続射撃で維持される。
    /// ・<see cref="WeaponTypeRules"/>（兵装の種別）とは別＝損害でなく行動抑制の効果。
    /// ・<see cref="MoraleShockRules"/>（士気衝撃）とは別＝物理的な弾幕による釘付け
    ///   （ここでは士気は釘付けされやすさの入力に使うだけで、士気そのものは変えない）。
    /// 実効値パターン：基準値（Params）は壊さず、ローカルで実効値を計算する。
    /// </summary>
    public static class SuppressionFireRules
    {
        // ---- 制圧度 ----

        /// <summary>弾量×精度で制圧度(0..1)を出す。弾量は referenceVolume で頭打ち。</summary>
        public static float SuppressionLevel(float volumeOfFire, float accuracy, SuppressionFireParams p)
        {
            float vol = Mathf.Clamp01(Mathf.Max(0f, volumeOfFire) / p.referenceVolume);
            float acc = Mathf.Clamp01(accuracy);
            return Mathf.Clamp(vol * acc * p.maxSuppression, 0f, p.maxSuppression);
        }

        public static float SuppressionLevel(float volumeOfFire, float accuracy) =>
            SuppressionLevel(volumeOfFire, accuracy, SuppressionFireParams.Default);

        // ---- 行動の鈍り ----

        /// <summary>制圧された敵の行動（反撃/前進）の鈍り（0..1・大きいほど鈍る）。</summary>
        public static float ActionPenalty(float suppressionLevel, SuppressionFireParams p)
        {
            float s = Mathf.Clamp01(suppressionLevel);
            return Mathf.Clamp01(s * p.actionPenaltyScale);
        }

        public static float ActionPenalty(float suppressionLevel) =>
            ActionPenalty(suppressionLevel, SuppressionFireParams.Default);

        // ---- 機動阻害（陣形の乱れ）----

        /// <summary>回避機動を強いられ陣形が乱れる度合い(0..1)。指数で高制圧時に急峻。</summary>
        public static float ManeuverImpairment(float suppressionLevel, SuppressionFireParams p)
        {
            float s = Mathf.Clamp01(suppressionLevel);
            float curved = Mathf.Pow(s, p.maneuverExponent);
            return Mathf.Clamp01(curved * p.maneuverScale);
        }

        public static float ManeuverImpairment(float suppressionLevel) =>
            ManeuverImpairment(suppressionLevel, SuppressionFireParams.Default);

        // ---- 弾薬消費 ----

        /// <summary>制圧射撃の弾薬消費（弾量×経過時間×単価）。</summary>
        public static float AmmoConsumption(float volumeOfFire, float dt, SuppressionFireParams p)
        {
            float vol = Mathf.Max(0f, volumeOfFire);
            float t = Mathf.Max(0f, dt);
            return vol * t * p.ammoPerVolumeSecond;
        }

        public static float AmmoConsumption(float volumeOfFire, float dt) =>
            AmmoConsumption(volumeOfFire, dt, SuppressionFireParams.Default);

        // ---- 制圧の解け（途切れると下がる）----

        /// <summary>射撃が途切れると制圧が解ける（線形減衰・0未満にならない）。</summary>
        public static float SuppressionDecay(float suppressionLevel, float dt, SuppressionFireParams p)
        {
            float s = Mathf.Clamp01(suppressionLevel);
            float t = Mathf.Max(0f, dt);
            return Mathf.Max(0f, s - p.decayPerSecond * t);
        }

        public static float SuppressionDecay(float suppressionLevel, float dt) =>
            SuppressionDecay(suppressionLevel, dt, SuppressionFireParams.Default);

        // ---- 釘付け（低士気ほど効く）----

        /// <summary>士気が低い敵ほど釘付けにされる(0..1)。enemyMorale は 0..1。</summary>
        public static float PinDownEffect(float suppressionLevel, float enemyMorale, SuppressionFireParams p)
        {
            float s = Mathf.Clamp01(suppressionLevel);
            float morale = Mathf.Clamp01(enemyMorale);
            // 低士気(=1-morale)ほど効きを底上げ：基準(1-scale)＋脆さぶん。
            float vulnerability = (1f - p.pinDownScale) + p.pinDownScale * (1f - morale);
            return Mathf.Clamp01(s * vulnerability);
        }

        public static float PinDownEffect(float suppressionLevel, float enemyMorale) =>
            PinDownEffect(suppressionLevel, enemyMorale, SuppressionFireParams.Default);

        // ---- 援護射撃価値 ----

        /// <summary>味方の機動を援護する価値（敵を抑えて味方を動かす）。friendlyManeuver は機動量。</summary>
        public static float CoveringFireValue(float suppressionLevel, float friendlyManeuver, SuppressionFireParams p)
        {
            float s = Mathf.Clamp01(suppressionLevel);
            float maneuver = Mathf.Max(0f, friendlyManeuver);
            return s * maneuver * p.coveringFireScale;
        }

        public static float CoveringFireValue(float suppressionLevel, float friendlyManeuver) =>
            CoveringFireValue(suppressionLevel, friendlyManeuver, SuppressionFireParams.Default);

        // ---- 制圧判定 ----

        /// <summary>制圧されているか（しきい値以上）。</summary>
        public static bool IsSuppressed(float suppressionLevel, float threshold)
        {
            return Mathf.Clamp01(suppressionLevel) >= Mathf.Clamp01(threshold);
        }

        public static bool IsSuppressed(float suppressionLevel) =>
            IsSuppressed(suppressionLevel, 0.5f);
    }
}
