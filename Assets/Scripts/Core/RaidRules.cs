using UnityEngine;

namespace Ginei
{
    /// <summary>縦深襲撃の調整係数。</summary>
    public readonly struct RaidParams
    {
        /// <summary>縦深ペナルティ（0..1）。深さ1で到達率がこの分だけ落ちる。</summary>
        public readonly float depthPenalty;
        /// <summary>機動による哨戒網回避率（0..1）。機動1で哨戒の阻止力をこの分だけ無効化。</summary>
        public readonly float mobilityEvasion;
        /// <summary>警報後の帰還率ペナルティ（0..1）。警報が出ると帰還率が(1−これ)倍。</summary>
        public readonly float alertedExfilPenalty;
        /// <summary>警戒上昇速度（襲撃活動1のとき毎単位時間の警戒上昇量）。</summary>
        public readonly float alarmRise;
        /// <summary>警戒減衰速度（活動が無いとき毎単位時間の警戒低下量＝ほとぼりが冷める）。</summary>
        public readonly float alarmDecay;
        /// <summary>警報閾値（0..1）。警戒がこの値以上で「警報発令」＝以後の襲撃が通らない。</summary>
        public readonly float alertThreshold;

        public RaidParams(float depthPenalty, float mobilityEvasion, float alertedExfilPenalty,
            float alarmRise, float alarmDecay, float alertThreshold)
        {
            this.depthPenalty = Mathf.Clamp01(depthPenalty);
            this.mobilityEvasion = Mathf.Clamp01(mobilityEvasion);
            this.alertedExfilPenalty = Mathf.Clamp01(alertedExfilPenalty);
            this.alarmRise = Mathf.Max(0f, alarmRise);
            this.alarmDecay = Mathf.Max(0f, alarmDecay);
            this.alertThreshold = Mathf.Clamp01(alertThreshold);
        }

        /// <summary>既定＝縦深0.5・機動回避0.5・警報後0.5・警戒上昇0.2・減衰0.05・警報閾値0.5。</summary>
        public static RaidParams Default => new RaidParams(0.5f, 0.5f, 0.5f, 0.2f, 0.05f, 0.5f);
    }

    /// <summary>
    /// 縦深襲撃の純ロジック。敵後方の補給拠点・造船所など<b>固定目標</b>への一撃離脱＝占領せず破壊して戻る。
    /// 回廊上の移動する輸送船団を狩る <see cref="CommerceRaidingRules"/>（通商破壊 L-3 #95）とは分担が別＝
    /// あちらは「船団 vs 護衛」の迎撃解決、こちらは「敵縦深への侵入→施設破壊→離脱」の損益。
    /// 核は「深く刺すほど抜けなくなる」＝帰還率は到達率より深さに対して厳しく落ち（二乗）、
    /// 警報後はさらに半減する。襲撃を反復すると敵の警戒が上がり以後の襲撃が通らない（味をしめた反復は逓減）。
    /// 乱数は roll∈[0,1) で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RaidRules
    {
        /// <summary>帰還率の機動下限係数＝鈍足部隊でも半分は抜けられる。</summary>
        public const float ExfilMobilityFloor = 0.5f;

        /// <summary>
        /// 目標到達率（0..1）＝（1−縦深ペナルティ×深さ）×（1−哨戒×（1−機動回避×機動））。
        /// 深いほど・哨戒網が濃いほど届かない。機動は哨戒の阻止をすり抜ける。
        /// </summary>
        public static float InfiltrationChance(float depth, float mobility, float enemyPicket, RaidParams p)
        {
            float depthFactor = 1f - p.depthPenalty * Mathf.Clamp01(depth);
            float picketBlock = Mathf.Clamp01(enemyPicket) * (1f - p.mobilityEvasion * Mathf.Clamp01(mobility));
            return Mathf.Clamp01(depthFactor * (1f - picketBlock));
        }

        public static float InfiltrationChance(float depth, float mobility, float enemyPicket)
            => InfiltrationChance(depth, mobility, enemyPicket, RaidParams.Default);

        /// <summary>到達判定。roll∈[0,1) が到達率未満なら目標到達＝true（決定論）。</summary>
        public static bool Infiltrates(float depth, float mobility, float enemyPicket, float roll, RaidParams p)
            => roll < InfiltrationChance(depth, mobility, enemyPicket, p);

        public static bool Infiltrates(float depth, float mobility, float enemyPicket, float roll)
            => Infiltrates(depth, mobility, enemyPicket, roll, RaidParams.Default);

        /// <summary>
        /// 目標破壊量＝襲撃戦力−目標防御（下回れば0＝守り切られる）。占領しない＝施設へのダメージのみ。
        /// </summary>
        public static float TargetDamage(float raidStrength, float targetDefense)
        {
            return Mathf.Max(0f, Mathf.Max(0f, raidStrength) - Mathf.Max(0f, targetDefense));
        }

        /// <summary>
        /// 帰還率（0..1）＝（1−縦深ペナルティ×深さ）<b>の二乗</b>×機動係数×警報係数。
        /// 帰路は往路より厳しい（敵中で背を向ける）＝深さの効きが到達率の二乗＝「深く刺すほど抜けなくなる」。
        /// 機動係数は <see cref="ExfilMobilityFloor"/>..1、警報後は（1−alertedExfilPenalty）倍。
        /// </summary>
        public static float ExfiltrationChance(float depth, float mobility, bool alerted, RaidParams p)
        {
            float depthFactor = 1f - p.depthPenalty * Mathf.Clamp01(depth);
            float mobilityFactor = Mathf.Lerp(ExfilMobilityFloor, 1f, Mathf.Clamp01(mobility));
            float alertFactor = alerted ? 1f - p.alertedExfilPenalty : 1f;
            return Mathf.Clamp01(depthFactor * depthFactor * mobilityFactor * alertFactor);
        }

        public static float ExfiltrationChance(float depth, float mobility, bool alerted)
            => ExfiltrationChance(depth, mobility, alerted, RaidParams.Default);

        /// <summary>帰還判定。roll∈[0,1) が帰還率未満なら離脱成功＝true（決定論）。</summary>
        public static bool Exfiltrates(float depth, float mobility, bool alerted, float roll, RaidParams p)
            => roll < ExfiltrationChance(depth, mobility, alerted, p);

        public static bool Exfiltrates(float depth, float mobility, bool alerted, float roll)
            => Exfiltrates(depth, mobility, alerted, roll, RaidParams.Default);

        /// <summary>
        /// 期待戦果＝到達率×破壊量−（1−帰還率）×部隊価値。深入りの損益をここで見える化する＝
        /// 浅い襲撃は堅実、深い襲撃は破壊が届いても部隊喪失リスクで赤字になりうる。
        /// </summary>
        public static float ExpectedValue(float depth, float mobility, float enemyPicket, bool alerted,
            float raidStrength, float targetDefense, float fleetValue, RaidParams p)
        {
            float reach = InfiltrationChance(depth, mobility, enemyPicket, p);
            float ret = ExfiltrationChance(depth, mobility, alerted, p);
            float damage = TargetDamage(raidStrength, targetDefense);
            return reach * damage - (1f - ret) * Mathf.Max(0f, fleetValue);
        }

        public static float ExpectedValue(float depth, float mobility, float enemyPicket, bool alerted,
            float raidStrength, float targetDefense, float fleetValue)
            => ExpectedValue(depth, mobility, enemyPicket, alerted, raidStrength, targetDefense, fleetValue,
                RaidParams.Default);

        /// <summary>
        /// 敵警戒レベルの時間積分（0..1）。襲撃活動 raidActivity(0..1) があるぶん上昇し、無いぶん減衰する＝
        /// 次警戒 = Clamp01(警戒 + (活動×alarmRise − (1−活動)×alarmDecay)×dt)。
        /// 襲撃が続くと警戒が上がり以後の襲撃が通らない（反復の逓減）。冷ますには手を止めるしかない。
        /// </summary>
        public static float AlarmLevelTick(float alarm, float raidActivity, float dt, RaidParams p)
        {
            float activity = Mathf.Clamp01(raidActivity);
            float delta = (activity * p.alarmRise - (1f - activity) * p.alarmDecay) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(alarm) + delta);
        }

        public static float AlarmLevelTick(float alarm, float raidActivity, float dt)
            => AlarmLevelTick(alarm, raidActivity, dt, RaidParams.Default);

        /// <summary>警報発令か＝警戒レベルが閾値以上。発令中は帰還率に警報ペナルティが乗る。</summary>
        public static bool IsAlerted(float alarm, RaidParams p)
            => Mathf.Clamp01(alarm) >= p.alertThreshold;

        public static bool IsAlerted(float alarm) => IsAlerted(alarm, RaidParams.Default);
    }
}
