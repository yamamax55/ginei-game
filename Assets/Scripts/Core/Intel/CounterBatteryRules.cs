using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 対砲戦（カウンターバッテリー）の調整値。敵の火力源（主砲）そのものを狙い撃って削ぐための係数群。
    /// 全フィールド public・ctor で Clamp（実効値パターン＝基準値を壊さない）。
    /// </summary>
    public readonly struct CounterBatteryParams
    {
        /// <summary>敵砲の位置特定が頭打ちになる基準発砲量（これ以上は飽和）。</summary>
        public readonly float referenceFireVolume;
        /// <summary>位置特定の上限（0..1）。完全特定には届かせない＝撃ち返しは難しい。</summary>
        public readonly float maxLocalization;
        /// <summary>対砲射撃の命中の効きスケール（位置特定×射撃管制→命中）。0..1。</summary>
        public readonly float accuracyScale;
        /// <summary>火力抑制の効きスケール（命中→火力を削ぐ度合い）。0..1。</summary>
        public readonly float suppressionScale;
        /// <summary>持続射撃の蓄積指数（>1で長時間ほど急に効く＝撃ち続けてはじめて崩す）。</summary>
        public readonly float sustainExponent;
        /// <summary>逆探知され撃ち返されるリスクのスケール（自分の射線も露呈する）。0..1。</summary>
        public readonly float returnFireScale;
        /// <summary>砲制圧と判定する火力抑制のしきい値（0..1）。</summary>
        public readonly float suppressedThreshold;

        public CounterBatteryParams(
            float referenceFireVolume,
            float maxLocalization,
            float accuracyScale,
            float suppressionScale,
            float sustainExponent,
            float returnFireScale,
            float suppressedThreshold)
        {
            this.referenceFireVolume = Mathf.Max(0.0001f, referenceFireVolume);
            this.maxLocalization = Mathf.Clamp01(maxLocalization);
            this.accuracyScale = Mathf.Clamp01(accuracyScale);
            this.suppressionScale = Mathf.Clamp01(suppressionScale);
            this.sustainExponent = Mathf.Clamp(sustainExponent, 0.1f, 4f);
            this.returnFireScale = Mathf.Clamp01(returnFireScale);
            this.suppressedThreshold = Mathf.Clamp01(suppressedThreshold);
        }

        /// <summary>既定：基準発砲量100・位置特定上限0.9・命中0.8・抑制0.7・持続指数1.5・逆探知0.6・制圧しきい0.6。</summary>
        public static CounterBatteryParams Default => new CounterBatteryParams(
            DefaultReferenceFireVolume,
            DefaultMaxLocalization,
            DefaultAccuracyScale,
            DefaultSuppressionScale,
            DefaultSustainExponent,
            DefaultReturnFireScale,
            DefaultSuppressedThreshold);

        public const float DefaultReferenceFireVolume = 100f;
        public const float DefaultMaxLocalization = 0.9f;
        public const float DefaultAccuracyScale = 0.8f;
        public const float DefaultSuppressionScale = 0.7f;
        public const float DefaultSustainExponent = 1.5f;
        public const float DefaultReturnFireScale = 0.6f;
        public const float DefaultSuppressedThreshold = 0.6f;
    }

    /// <summary>
    /// 対砲戦＝敵の砲を狙い撃って火力を削ぐ純ロジック（盤面非依存・plain 引数・test-first）。
    /// 敵兵装そのもの（主砲＝火力源）を狙う＝発砲炎・射線から敵砲の位置を逆探知し、撃ち返して相手の火力を削る。
    /// 火力を削げば受ける被害は減るが、点目標の砲を狙うのは命中が難しく時間（持続射撃）がかかり、
    /// その間は艦体への直接打撃が減る（トレードオフ）。さらに撃ち返せばこちらの位置も逆探知されうる。
    /// <para>
    /// 分担：<see cref="SuppressionFireRules"/>（制圧射撃＝弾幕で敵の<b>行動</b>を抑える・釘付け）とは別＝
    /// 本ルールは火力<b>源そのものの破壊</b>（敵の発砲能力を恒久的に削ぐ）。
    /// <see cref="WeaponTypeRules"/>（兵装<b>種別</b>の特性）とも別＝本ルールは種別非依存で「砲を狙う」行為そのものを扱う。
    /// </para>
    /// 実効値パターン（基準火力は非破壊・対砲戦後の実効火力を導出するだけ）。各メソッドに Params 明示版＋Default 委譲版。
    /// </summary>
    public static class CounterBatteryRules
    {
        // --- 敵砲の位置特定（発砲量×探知） ---

        /// <summary>既定パラメータ版。</summary>
        public static float BatteryLocalization(float enemyFireVolume, float sensorQuality)
            => BatteryLocalization(enemyFireVolume, sensorQuality, CounterBatteryParams.Default);

        /// <summary>
        /// 敵の発砲量（発砲炎・射線）とこちらの探知性能から、敵砲の位置をどれだけ特定できたかを返す（0..maxLocalization）。
        /// 撃てば撃つほど・探知が良いほど絞れるが、上限で頭打ち＝完全特定には届かない。
        /// </summary>
        public static float BatteryLocalization(float enemyFireVolume, float sensorQuality, CounterBatteryParams p)
        {
            float volume = Mathf.Max(0f, enemyFireVolume);
            float sensor = Mathf.Clamp01(sensorQuality);
            float volumeFactor = volume / (volume + p.referenceFireVolume); // 0..1 飽和カーブ
            return Mathf.Clamp(volumeFactor * sensor, 0f, p.maxLocalization);
        }

        // --- 対砲射撃の命中（位置特定×射撃管制） ---

        /// <summary>既定パラメータ版。</summary>
        public static float CounterFireAccuracy(float localization, float ownFireControl)
            => CounterFireAccuracy(localization, ownFireControl, CounterBatteryParams.Default);

        /// <summary>
        /// 逆探知した位置特定度と自艦の射撃管制から、点目標（敵砲）への対砲射撃の命中を返す（0..1）。
        /// 位置が絞れていて射撃管制が良いほど当たるが、accuracyScale で全体を抑える＝砲狙いは元々当てにくい。
        /// </summary>
        public static float CounterFireAccuracy(float localization, float ownFireControl, CounterBatteryParams p)
        {
            float loc = Mathf.Clamp01(localization);
            float fc = Mathf.Clamp01(ownFireControl);
            return Mathf.Clamp01(loc * fc * p.accuracyScale);
        }

        // --- 火力抑制（命中×持続射撃の蓄積） ---

        /// <summary>既定パラメータ版。</summary>
        public static float FirepowerSuppression(float counterFireAccuracy, float sustainedFire)
            => FirepowerSuppression(counterFireAccuracy, sustainedFire, CounterBatteryParams.Default);

        /// <summary>
        /// 対砲射撃の命中と持続射撃（撃ち続けた度合い 0..1）から、敵火力を削げた度合いを返す（0..1）。
        /// 命中していても一撃では崩れず、撃ち続けて（sustain^exponent）はじめて蓄積して効く＝時間がかかる。
        /// </summary>
        public static float FirepowerSuppression(float counterFireAccuracy, float sustainedFire, CounterBatteryParams p)
        {
            float acc = Mathf.Clamp01(counterFireAccuracy);
            float sustain = Mathf.Clamp01(sustainedFire);
            float sustainFactor = Mathf.Pow(sustain, p.sustainExponent);
            return Mathf.Clamp01(acc * sustainFactor * p.suppressionScale);
        }

        // --- 対砲戦後に残る敵火力（実効値・基準非破壊） ---

        /// <summary>
        /// 基準の敵火力に火力抑制（0..1）を掛けて、対砲戦後に残る実効火力を返す（基準値は非破壊）。
        /// 抑制が大きいほど敵は撃てなくなる＝こちらの被害が減る。
        /// </summary>
        public static float EnemyFirepowerAfter(float enemyFirepower, float firepowerSuppression)
        {
            float baseFp = Mathf.Max(0f, enemyFirepower);
            float sup = Mathf.Clamp01(firepowerSuppression);
            return baseFp * (1f - sup);
        }

        // --- 撃ち合いの優劣（射撃管制の差・符号付き -1..1） ---

        /// <summary>
        /// 自他の射撃管制から、対砲戦の撃ち合いの優劣を返す（-1..1）。+1＝こちらが一方的に削る／-1＝削られる／0＝拮抗。
        /// </summary>
        public static float CounterBatteryDuel(float ownFireControl, float enemyFireControl)
        {
            float own = Mathf.Clamp01(ownFireControl);
            float enemy = Mathf.Clamp01(enemyFireControl);
            return Mathf.Clamp(own - enemy, -1f, 1f);
        }

        // --- 機会費用（砲狙い⇔艦体への直接打撃のトレードオフ・符号付き -1..1） ---

        /// <summary>
        /// 砲を狙う配分と艦体を直接叩く配分から、機会費用を符号付きで返す（-1..1）。
        /// +＝砲狙いに寄せた（艦体直撃が減る代わりに火力を削ぐ）／-＝艦体直撃に寄せた（火力源は残る）。
        /// </summary>
        public static float OpportunityCost(float focusOnBatteries, float focusOnHulls)
        {
            float bat = Mathf.Clamp01(focusOnBatteries);
            float hull = Mathf.Clamp01(focusOnHulls);
            return Mathf.Clamp(bat - hull, -1f, 1f);
        }

        // --- 撃ち返しリスク（位置特定×敵の応射） ---

        /// <summary>既定パラメータ版。</summary>
        public static float ReturnFireRisk(float localization, float enemyResponse)
            => ReturnFireRisk(localization, enemyResponse, CounterBatteryParams.Default);

        /// <summary>
        /// 自分が敵砲を逆探知して撃ち返すほど、自分の射線・発砲も露呈し、敵の応射能力次第でこちらの位置も特定される（0..1）。
        /// </summary>
        public static float ReturnFireRisk(float localization, float enemyResponse, CounterBatteryParams p)
        {
            float loc = Mathf.Clamp01(localization);
            float resp = Mathf.Clamp01(enemyResponse);
            return Mathf.Clamp01(loc * resp * p.returnFireScale);
        }

        // --- 砲制圧の判定（bool） ---

        /// <summary>既定パラメータ版。</summary>
        public static bool IsBatterySuppressed(float firepowerSuppression)
            => IsBatterySuppressed(firepowerSuppression, CounterBatteryParams.Default.suppressedThreshold);

        /// <summary>火力抑制がしきい値以上なら敵砲を制圧したとみなす。</summary>
        public static bool IsBatterySuppressed(float firepowerSuppression, float threshold)
        {
            float sup = Mathf.Clamp01(firepowerSuppression);
            float th = Mathf.Clamp01(threshold);
            return sup >= th;
        }
    }
}
