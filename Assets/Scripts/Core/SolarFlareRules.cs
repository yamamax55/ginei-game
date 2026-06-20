using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 恒星フレア／宇宙嵐パラメータ（突発イベントの係数）。基準値非破壊・全項目 Clamp。
    /// </summary>
    public readonly struct SolarFlareParams
    {
        /// <summary>恒星活動がフレア強度に効く重み。</summary>
        public readonly float activityWeight;
        /// <summary>恒星への近さがフレア強度を増幅する重み。</summary>
        public readonly float proximityWeight;
        /// <summary>探知精度が猶予を延ばす重み。</summary>
        public readonly float detectionWeight;
        /// <summary>距離が猶予を延ばす重み。</summary>
        public readonly float distanceWeight;
        /// <summary>到達猶予の最大秒（これで上限を切る）。</summary>
        public readonly float maxWarningSeconds;
        /// <summary>フレア強度→電磁パルス障害の係数。</summary>
        public readonly float empWeight;
        /// <summary>フレア強度→通信途絶の係数。</summary>
        public readonly float blackoutWeight;
        /// <summary>遮蔽物の陰による退避軽減の係数。</summary>
        public readonly float shelterWeight;
        /// <summary>奇襲好機（敵我の障害差）の係数。</summary>
        public readonly float exploitWeight;
        /// <summary>嵐の継続時間の最大秒（強度1.0で到達する上限）。</summary>
        public readonly float maxStormSeconds;
        /// <summary>システム危機と判定する電磁パルス障害のしきい値。</summary>
        public readonly float criticalThreshold;

        public SolarFlareParams(
            float activityWeight,
            float proximityWeight,
            float detectionWeight,
            float distanceWeight,
            float maxWarningSeconds,
            float empWeight,
            float blackoutWeight,
            float shelterWeight,
            float exploitWeight,
            float maxStormSeconds,
            float criticalThreshold)
        {
            this.activityWeight = Mathf.Clamp01(activityWeight);
            this.proximityWeight = Mathf.Clamp01(proximityWeight);
            this.detectionWeight = Mathf.Clamp01(detectionWeight);
            this.distanceWeight = Mathf.Clamp01(distanceWeight);
            this.maxWarningSeconds = Mathf.Max(0f, maxWarningSeconds);
            this.empWeight = Mathf.Clamp01(empWeight);
            this.blackoutWeight = Mathf.Clamp01(blackoutWeight);
            this.shelterWeight = Mathf.Clamp01(shelterWeight);
            this.exploitWeight = Mathf.Clamp01(exploitWeight);
            this.maxStormSeconds = Mathf.Max(0f, maxStormSeconds);
            this.criticalThreshold = Mathf.Clamp01(criticalThreshold);
        }

        public static SolarFlareParams Default =>
            new SolarFlareParams(
                activityWeight: 0.7f,
                proximityWeight: 0.5f,
                detectionWeight: 0.6f,
                distanceWeight: 0.5f,
                maxWarningSeconds: 120f,
                empWeight: 0.9f,
                blackoutWeight: 0.8f,
                shelterWeight: 0.8f,
                exploitWeight: 0.8f,
                maxStormSeconds: 90f,
                criticalThreshold: 0.6f);
    }

    /// <summary>
    /// 恒星フレア／宇宙嵐（突発的な高エネルギー現象）の純ロジック（盤面非依存・plain引数）。
    /// 活動的な恒星の近くでフレアが強く立ち上がり、探知精度と距離が到達までの猶予を稼ぐ。
    /// 到達すると電磁パルスでシステムが障害を起こし通信が途絶するが、遮蔽物の陰へ退避すれば軽減でき、
    /// 自軍が敵より耐性で勝るならフレアの混乱を衝く奇襲の好機にもなる。嵐は強度に比例して続く。
    /// 分担：<see cref="SolarFlareRules"/> は「突発的環境現象が会戦のシステム/通信/退避/奇襲に効くレイヤー」。
    /// 恒常的な地形効果（TerrainRules）や視界制限戦闘（NightBattleRules）とは別物＝ここは時限イベント。
    /// 実効値パターン（基準値を破壊せず障害量/猶予を返す）・乱数なし（決定論）。
    /// </summary>
    public static class SolarFlareRules
    {
        /// <summary>
        /// フレア強度（0..1）。恒星活動と恒星への近さの加重和。近いほど・活動的なほど強い。
        /// = clamp01(activity × activityWeight + proximity × proximityWeight)。
        /// </summary>
        public static float FlareIntensity(float stellarActivity, float proximityToStar, SolarFlareParams p)
        {
            float act = Mathf.Clamp01(stellarActivity);
            float prox = Mathf.Clamp01(proximityToStar);
            float raw = act * p.activityWeight + prox * p.proximityWeight;
            return Mathf.Clamp01(raw);
        }

        public static float FlareIntensity(float stellarActivity, float proximityToStar) =>
            FlareIntensity(stellarActivity, proximityToStar, SolarFlareParams.Default);

        /// <summary>
        /// 到達までの猶予（秒・0..maxWarningSeconds）。探知精度と距離が早期警戒を稼ぐ。
        /// = maxWarningSeconds × clamp01(detection × detectionWeight + flareDistance × distanceWeight)。
        /// </summary>
        public static float WarningTime(float detectionQuality, float flareDistance, SolarFlareParams p)
        {
            float det = Mathf.Clamp01(detectionQuality);
            float dist = Mathf.Clamp01(flareDistance);
            float norm = Mathf.Clamp01(det * p.detectionWeight + dist * p.distanceWeight);
            return p.maxWarningSeconds * norm;
        }

        public static float WarningTime(float detectionQuality, float flareDistance) =>
            WarningTime(detectionQuality, flareDistance, SolarFlareParams.Default);

        /// <summary>
        /// 電磁パルスによるシステム障害（0..1）。フレア強度 × (1 − 耐性)。耐性が高いほど障害は小さい。
        /// = clamp01(flareIntensity × empWeight × (1 − systemHardening))。
        /// </summary>
        public static float EmpDisruption(float flareIntensity, float systemHardening, SolarFlareParams p)
        {
            float fi = Mathf.Clamp01(flareIntensity);
            float hard = Mathf.Clamp01(systemHardening);
            return Mathf.Clamp01(fi * p.empWeight * (1f - hard));
        }

        public static float EmpDisruption(float flareIntensity, float systemHardening) =>
            EmpDisruption(flareIntensity, systemHardening, SolarFlareParams.Default);

        /// <summary>
        /// 通信途絶度（0..1）。フレア強度に比例して指揮通信が遮断される。
        /// = clamp01(flareIntensity × blackoutWeight)。
        /// </summary>
        public static float CommunicationBlackout(float flareIntensity, SolarFlareParams p)
        {
            float fi = Mathf.Clamp01(flareIntensity);
            return Mathf.Clamp01(fi * p.blackoutWeight);
        }

        public static float CommunicationBlackout(float flareIntensity) =>
            CommunicationBlackout(flareIntensity, SolarFlareParams.Default);

        /// <summary>
        /// 退避による軽減後の実効障害（0..1）。遮蔽物の陰に隠れるほどフレア障害を減じる（基準は非破壊）。
        /// = flareIntensity × (1 − coverProximity × shelterWeight)。
        /// </summary>
        public static float ShelterMitigation(float coverProximity, float flareIntensity, SolarFlareParams p)
        {
            float cover = Mathf.Clamp01(coverProximity);
            float fi = Mathf.Clamp01(flareIntensity);
            return Mathf.Clamp01(fi * (1f - cover * p.shelterWeight));
        }

        public static float ShelterMitigation(float coverProximity, float flareIntensity) =>
            ShelterMitigation(coverProximity, flareIntensity, SolarFlareParams.Default);

        /// <summary>
        /// フレアを衝く奇襲好機（-1..1）。敵が自軍より障害を受けるほど好機（正）、逆なら不利（負）。
        /// = clamp(empDisruption × (enemyHardening の脆さ − ownHardening の脆さ) × exploitWeight, -1, 1)。
        /// 脆さ = (1 − hardening)。敵が脆く自軍が固いほど大きい正値。
        /// </summary>
        public static float FlareExploitation(float empDisruption, float ownHardening, float enemyHardening, SolarFlareParams p)
        {
            float emp = Mathf.Clamp01(empDisruption);
            float ownFrail = 1f - Mathf.Clamp01(ownHardening);
            float enemyFrail = 1f - Mathf.Clamp01(enemyHardening);
            float advantage = enemyFrail - ownFrail; // -1..1（敵が脆いほど正）
            float raw = emp * advantage * p.exploitWeight;
            return Mathf.Clamp(raw, -1f, 1f);
        }

        public static float FlareExploitation(float empDisruption, float ownHardening, float enemyHardening) =>
            FlareExploitation(empDisruption, ownHardening, enemyHardening, SolarFlareParams.Default);

        /// <summary>
        /// 嵐の継続時間（秒・0..maxStormSeconds）。フレア強度に比例して荒天が続く。
        /// = maxStormSeconds × flareIntensity。
        /// </summary>
        public static float StormDuration(float flareIntensity, SolarFlareParams p)
        {
            float fi = Mathf.Clamp01(flareIntensity);
            return p.maxStormSeconds * fi;
        }

        public static float StormDuration(float flareIntensity) =>
            StormDuration(flareIntensity, SolarFlareParams.Default);

        /// <summary>
        /// システムが危機的か。電磁パルス障害がしきい値以上なら機能不全（指揮・火器管制が落ちる）と判定。
        /// </summary>
        public static bool IsSystemsCritical(float empDisruption, float threshold)
        {
            float emp = Mathf.Clamp01(empDisruption);
            return emp >= Mathf.Clamp01(threshold);
        }

        public static bool IsSystemsCritical(float empDisruption) =>
            IsSystemsCritical(empDisruption, SolarFlareParams.Default.criticalThreshold);
    }
}
