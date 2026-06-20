using UnityEngine;

namespace Ginei
{
    /// <summary>強襲揚陸の調整係数（軌道→地上）。降下後のまとまり残存・防御側の重み・第一波の致死性・勢いの下限。ctor で全値をクランプする。</summary>
    public readonly struct AmphibiousAssaultParams
    {
        /// <summary>まとまり（landingCohesion）が0でも残る最低戦力比（0..1・降下が散っても全滅はしない）。1.0なら常に満額。</summary>
        public readonly float minCohesionRetention;
        /// <summary>橋頭堡確保度で防御側戦力にかける重み（≥0・大きいほど守りが効く＝確保しにくい）。</summary>
        public readonly float defenderWeight;
        /// <summary>第一波の致死性係数（0..1・上陸直後の脆さ＝確保が薄いほど削られる）。</summary>
        public readonly float firstWaveLethality;
        /// <summary>勢い（AssaultMomentum）の下限係数（0..1・後続が止まっても橋頭堡ぶんの勢いは残る）。</summary>
        public readonly float momentumFloor;

        public AmphibiousAssaultParams(float minCohesionRetention, float defenderWeight,
                                       float firstWaveLethality, float momentumFloor)
        {
            this.minCohesionRetention = Mathf.Clamp01(minCohesionRetention);
            this.defenderWeight = Mathf.Max(0f, defenderWeight);
            this.firstWaveLethality = Mathf.Clamp01(firstWaveLethality);
            this.momentumFloor = Mathf.Clamp01(momentumFloor);
        }

        /// <summary>既定＝まとまり最低残存0.4／防御重み1.0／第一波致死性0.5／勢い下限0.3。</summary>
        public static AmphibiousAssaultParams Default
            => new AmphibiousAssaultParams(0.4f, 1f, 0.5f, 0.3f);
    }

    /// <summary>
    /// 強襲揚陸の純ロジック（軌道から地上へ部隊を降ろし橋頭堡を築く・唯一の窓口）。
    /// <b>制宙権下で地上軍を惑星表面へ降下させ、敵の防御陣地に強襲上陸する</b>。
    /// 降下中は無防備（<see cref="DescentVulnerability"/>＝制宙権で軽減）、上陸直後は脆い（<see cref="FirstWaveLosses"/>）。
    /// 橋頭堡（<see cref="BeachheadEstablishment"/>）を確保し後続（<see cref="FollowUpRate"/>）を流し込んで勢い（<see cref="AssaultMomentum"/>）を作る。
    /// <see cref="PlanetSiegeRules"/>（惑星攻城の制空権・侵略・既存）とは別＝こちらは<b>軌道→地上の強襲上陸プロセス</b>そのもの。
    /// BridgeheadRules（宙域の橋頭堡・あれば）とは別＝こちらは<b>惑星表面の上陸地点</b>。
    /// 盤面（StrategicFleet/Planet 等）に依存せず plain 引数で受ける。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AmphibiousAssaultRules
    {
        /// <summary>
        /// 降下後にまとまった戦力（≥0）＝降ろした部隊 droppedTroops のうち、まとまり landingCohesion で残る実効戦力。
        /// まとまり1.0で満額、0.0でも <see cref="AmphibiousAssaultParams.minCohesionRetention"/> ぶんは残る（散っても全滅しない）。
        /// </summary>
        public static float LandingForce(float droppedTroops, float landingCohesion, AmphibiousAssaultParams p)
        {
            float troops = Mathf.Max(0f, droppedTroops);
            float cohesion = Mathf.Clamp01(landingCohesion);
            float retention = Mathf.Lerp(p.minCohesionRetention, 1f, cohesion);
            return troops * retention;
        }

        /// <summary>既定係数での降下戦力（≥0）。</summary>
        public static float LandingForce(float droppedTroops, float landingCohesion)
            => LandingForce(droppedTroops, landingCohesion, AmphibiousAssaultParams.Default);

        /// <summary>
        /// 降下中の脆さ（0..1・1=丸裸）＝敵の対空 enemyAirDefense が削るが、制宙権 orbitalSuperiority で軽減される。
        /// 制宙権1.0なら降下は安全（0）、制宙権0なら敵対空がそのまま脅威になる。両入力はクランプ。
        /// </summary>
        public static float DescentVulnerability(float orbitalSuperiority, float enemyAirDefense)
        {
            float orbital = Mathf.Clamp01(orbitalSuperiority);
            float airDef = Mathf.Clamp01(enemyAirDefense);
            return Mathf.Clamp01(airDef * (1f - orbital)); // 制宙権で対空を抑え込む
        }

        /// <summary>
        /// 上陸地点の確保度（0..1）＝降下戦力 landingForce と防御側 defenderStrength の押し合い（ロジスティック比）。
        /// landingForce が大きいほど1へ、防御が厚いほど0へ。防御重みは <see cref="AmphibiousAssaultParams.defenderWeight"/>。
        /// 両者0なら確保なし（0）。
        /// </summary>
        public static float BeachheadEstablishment(float landingForce, float defenderStrength, AmphibiousAssaultParams p)
        {
            float force = Mathf.Max(0f, landingForce);
            float defender = Mathf.Max(0f, defenderStrength) * p.defenderWeight;
            float denom = force + defender;
            if (denom <= 0f) return 0f; // 双方ゼロ＝確保なし
            return Mathf.Clamp01(force / denom);
        }

        /// <summary>既定係数での橋頭堡確保度（0..1）。</summary>
        public static float BeachheadEstablishment(float landingForce, float defenderStrength)
            => BeachheadEstablishment(landingForce, defenderStrength, AmphibiousAssaultParams.Default);

        /// <summary>
        /// 第一波の損害（0..1・上陸直後の脆さ）＝防御側 defenderStrength が薄い橋頭堡 beachheadEstablishment を削る。
        /// 確保度が高いほど損害は小さく、防御が厚いほど大きい。致死性は <see cref="AmphibiousAssaultParams.firstWaveLethality"/>。
        /// </summary>
        public static float FirstWaveLosses(float defenderStrength, float beachheadEstablishment, AmphibiousAssaultParams p)
        {
            float defender = Mathf.Clamp01(defenderStrength);
            float exposure = 1f - Mathf.Clamp01(beachheadEstablishment); // 確保が薄いほど無防備
            return Mathf.Clamp01(defender * exposure * p.firstWaveLethality);
        }

        /// <summary>既定係数での第一波損害（0..1）。</summary>
        public static float FirstWaveLosses(float defenderStrength, float beachheadEstablishment)
            => FirstWaveLosses(defenderStrength, beachheadEstablishment, AmphibiousAssaultParams.Default);

        /// <summary>
        /// 後続部隊の降下速度（0..1）＝制宙権 orbitalSuperiority と降下能力 dropCapacity の積。
        /// 制宙権を握っているほど・揚陸艇の輸送力が高いほど後続を速く流し込める。両入力はクランプ。
        /// </summary>
        public static float FollowUpRate(float orbitalSuperiority, float dropCapacity)
        {
            float orbital = Mathf.Clamp01(orbitalSuperiority);
            float capacity = Mathf.Clamp01(dropCapacity);
            return Mathf.Clamp01(orbital * capacity);
        }

        /// <summary>
        /// 敵が上陸点へ戦力を集める反応（0..1）＝上陸の探知度 landingDetection と防御側の機動 defenderMobility の積。
        /// 早く見つかり・敵が機動的なほど反応集中が速い（橋頭堡を潰しに来る）。両入力はクランプ。
        /// </summary>
        public static float DefenderCounterconcentration(float landingDetection, float defenderMobility)
        {
            float detection = Mathf.Clamp01(landingDetection);
            float mobility = Mathf.Clamp01(defenderMobility);
            return Mathf.Clamp01(detection * mobility);
        }

        /// <summary>
        /// 上陸の勢い（0..1）＝橋頭堡 beachheadEstablishment を土台に後続 followUpRate が押し上げる。
        /// 後続が0でも橋頭堡ぶんの勢いは <see cref="AmphibiousAssaultParams.momentumFloor"/> を下限に残る。
        /// </summary>
        public static float AssaultMomentum(float beachheadEstablishment, float followUpRate, AmphibiousAssaultParams p)
        {
            float beachhead = Mathf.Clamp01(beachheadEstablishment);
            float followUp = Mathf.Clamp01(followUpRate);
            float push = Mathf.Lerp(p.momentumFloor, 1f, followUp); // 後続が勢いを底上げ
            return Mathf.Clamp01(beachhead * push);
        }

        /// <summary>既定係数での上陸の勢い（0..1）。</summary>
        public static float AssaultMomentum(float beachheadEstablishment, float followUpRate)
            => AssaultMomentum(beachheadEstablishment, followUpRate, AmphibiousAssaultParams.Default);

        /// <summary>
        /// 橋頭堡を確保したか＝確保度 beachheadEstablishment が閾値 threshold 以上なら true（上陸成功）。
        /// threshold はクランプ。確保度が閾値未満なら橋頭堡は不安定で押し返されうる。
        /// </summary>
        public static bool IsBeachheadHeld(float beachheadEstablishment, float threshold)
        {
            float beachhead = Mathf.Clamp01(beachheadEstablishment);
            float thr = Mathf.Clamp01(threshold);
            return beachhead >= thr;
        }
    }
}
