using UnityEngine;

namespace Ginei
{
    /// <summary>柔弱（じゅうじゃく）ドクトリンの調整係数（老子型・上善は水の如し）。</summary>
    public readonly struct WaterDoctrineParams
    {
        /// <summary>圧力に対する短期の屈服度＝柔軟1のときどれだけ退くか（水は低きに流れる＝短期劣後）。</summary>
        public readonly float yieldScale;
        /// <summary>柔軟さが生む長期回復/秒の基礎（柔軟1のとき）。しなやかなものは折れず時間で戻る。</summary>
        public readonly float recoveryRate;
        /// <summary>柔軟＋持続が硬い守りに浸透する強さ（点滴石を穿つ）。</summary>
        public readonly float penetrationScale;
        /// <summary>柔弱ドクトリンが与える士気の床の上限＝柔軟1のときの最低士気（押されても折れない）。</summary>
        public readonly float moraleFloorCap;
        /// <summary>剛強がこの累積ストレスで折れる閾値（柔軟はこれを超えても折れない＝非対称）。</summary>
        public readonly float breakThreshold;

        public WaterDoctrineParams(float yieldScale, float recoveryRate, float penetrationScale,
            float moraleFloorCap, float breakThreshold)
        {
            this.yieldScale = Mathf.Max(0f, yieldScale);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.penetrationScale = Mathf.Max(0f, penetrationScale);
            this.moraleFloorCap = Mathf.Clamp01(moraleFloorCap);
            this.breakThreshold = Mathf.Clamp01(breakThreshold);
        }

        /// <summary>
        /// 既定＝屈服0.6・回復0.1/秒・浸透0.5・士気の床上限0.5・折れ閾値0.7。
        /// 柔軟は短期に最大6割退くが、長期は回復0.1で戻り、剛強は累積ストレス0.7で砕けるが柔軟は折れない。
        /// </summary>
        public static WaterDoctrineParams Default => new WaterDoctrineParams(0.6f, 0.1f, 0.5f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 柔弱（じゅうじゃく）ドクトリンの純ロジック（LAOZ-4 #1558・老子型＝上善は水の如し／柔弱は剛強に勝つ）。
    /// 水のように柔らかく低きにつく姿勢は、<b>短期には劣位</b>（圧力に押されれば退く＝水は低きに流れる）だが、
    /// <b>長期には驚異的な回復力と浸透力</b>で剛を制す＝硬く強いものは折れ、柔らかいものはしなって生き残る
    /// （点滴石を穿つ・しなやかなものは折れない）。短期の劣後と長期の回復・浸透の非対称が核。
    /// <see cref="FleetMorale"/>（士気そのものの係数算出＝Game層）／<see cref="WuWeiRules"/>（無為＝為さずして為す・同EPIC LAOZ）／
    /// <see cref="ReversalRules"/>（反者道之動＝極まれば反転する・同EPIC LAOZ）とは分担し、ここは
    /// <b>柔よく剛を制す＝短期劣後・長期の回復力と浸透力</b>のみを扱う（係数算出のみ・基準非破壊）。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WaterDoctrineRules
    {
        /// <summary>
        /// 短期の戦果（0..1＝押されてどれだけ残るか）＝1 − 柔軟×圧力×屈服度。柔軟なほど圧力に押されて短期は退く
        /// （水は低きに流れる＝短期劣後）。圧力0なら退かず1。剛強（柔軟0）は短期に退かないが、それは折れる前兆。
        /// </summary>
        public static float ShortTermYield(float flexibility, float pressure, WaterDoctrineParams p)
        {
            float flex = Mathf.Clamp01(flexibility);
            float pres = Mathf.Clamp01(pressure);
            return Mathf.Clamp01(1f - flex * pres * p.yieldScale);
        }

        public static float ShortTermYield(float flexibility, float pressure)
            => ShortTermYield(flexibility, pressure, WaterDoctrineParams.Default);

        /// <summary>
        /// 長期の回復量（0..1＝dtで戻る量）＝柔軟×回復率×dt。柔らかいものは折れず、時間とともに元に戻り浸透する
        /// ＝しなやかさが長期の強さ。柔軟0（剛強）は回復しない＝折れたら戻らない。
        /// </summary>
        public static float LongTermRecovery(float flexibility, float dt, WaterDoctrineParams p)
        {
            float flex = Mathf.Clamp01(flexibility);
            float step = Mathf.Max(0f, dt);
            return Mathf.Clamp01(flex * p.recoveryRate * step);
        }

        public static float LongTermRecovery(float flexibility, float dt)
            => LongTermRecovery(flexibility, dt, WaterDoctrineParams.Default);

        /// <summary>
        /// 回復の非対称（0..1）＝柔軟は折れずに戻り、剛強は折れたら戻らない。柔軟分（flexibility）は満額回復見込み、
        /// 剛強分（rigidity）は折れて戻らない＝回復見込みを差し引く。柔よく＝flexibility×(1−rigidity)。
        /// 硬いものは砕けて回復できないという老子の非対称を一本の式に出す。
        /// </summary>
        public static float RecoveryAsymmetry(float flexibility, float rigidity)
        {
            float flex = Mathf.Clamp01(flexibility);
            float rig = Mathf.Clamp01(rigidity);
            return Mathf.Clamp01(flex * (1f - rig));
        }

        /// <summary>
        /// 柔が剛に長期で勝る度合い（0..1）＝柔軟×相手の剛強度。相手が硬いほど（折れやすいほど）柔らかさが
        /// 長期で勝つ＝水が岩を穿つ。相手が柔らかければ（opponentRigidity小）勝ち目は小さい（しなる者同士は穿てない）。
        /// </summary>
        public static float SoftOvercomesHard(float flexibility, float opponentRigidity)
        {
            float flex = Mathf.Clamp01(flexibility);
            float rig = Mathf.Clamp01(opponentRigidity);
            return Mathf.Clamp01(flex * rig);
        }

        /// <summary>
        /// 浸透力（0..1）＝柔軟×持続×浸透係数。柔らかさと持続が硬い守りにしみ込む＝点滴石を穿つ。
        /// 持続が無ければ（一撃では）穿てず、柔軟が無ければ硬く弾かれる＝両方が要る。
        /// </summary>
        public static float PenetrationForce(float flexibility, float persistence, WaterDoctrineParams p)
        {
            float flex = Mathf.Clamp01(flexibility);
            float pers = Mathf.Clamp01(persistence);
            return Mathf.Clamp01(flex * pers * p.penetrationScale);
        }

        public static float PenetrationForce(float flexibility, float persistence)
            => PenetrationForce(flexibility, persistence, WaterDoctrineParams.Default);

        /// <summary>
        /// 柔弱ドクトリンが与える士気の床（0..1＝押されても下回らない最低士気・実効値）＝柔軟×床上限。
        /// 柔らかいものは折れない＝どれだけ押されても士気はこの床まで＝崩れずに残る。基準士気は変えず床だけ与える
        /// （実効値パターン＝呼び出し側で max(現士気, 床) を取る想定）。
        /// </summary>
        public static float ResilientMoraleFloor(float flexibility, WaterDoctrineParams p)
        {
            float flex = Mathf.Clamp01(flexibility);
            return Mathf.Clamp01(flex * p.moraleFloorCap);
        }

        public static float ResilientMoraleFloor(float flexibility)
            => ResilientMoraleFloor(flexibility, WaterDoctrineParams.Default);

        /// <summary>
        /// 適応的な受け流し（0..1＝脅威を柔で逃がした後の残存脅威）＝脅威×(1−柔軟)。硬く正面から受けず、
        /// 柔らかく受け流すほど（flexibility大）残る脅威は小さい＝剛で受けず柔で流す。柔軟1なら脅威を完全に流し去る。
        /// </summary>
        public static float AdaptiveResponse(float threat, float flexibility)
        {
            float t = Mathf.Clamp01(threat);
            float flex = Mathf.Clamp01(flexibility);
            return Mathf.Clamp01(t * (1f - flex));
        }

        /// <summary>
        /// しなやかで折れない判定（true＝剛強なら砕けるストレス下でも折れない）。累積ストレスが折れ閾値を超えても、
        /// 柔軟が高ければ折れずに耐える＝実効的な折れ閾値を柔軟ぶん引き上げる（柔軟1なら決して折れない）。
        /// 剛強（柔軟0）は閾値超過で折れる＝硬いものは砕ける。
        /// </summary>
        public static bool IsUnbreakable(float flexibility, float accumulatedStress, WaterDoctrineParams p)
        {
            float flex = Mathf.Clamp01(flexibility);
            float stress = Mathf.Clamp01(accumulatedStress);
            // 柔軟ぶん閾値を押し上げる：柔軟1なら実効閾値1を超えられず常に折れない。
            float effectiveThreshold = Mathf.Clamp01(p.breakThreshold + flex * (1f - p.breakThreshold));
            return stress <= effectiveThreshold;
        }

        public static bool IsUnbreakable(float flexibility, float accumulatedStress)
            => IsUnbreakable(flexibility, accumulatedStress, WaterDoctrineParams.Default);
    }
}
