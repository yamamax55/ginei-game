using UnityEngine;

namespace Ginei
{
    /// <summary>会戦の三択結果＝勝利／敗北／膠着（勝敗でない第三の結果）。</summary>
    public enum BattleResult { 勝利, 敗北, 膠着 }

    /// <summary>膠着戦況（塹壕戦）の調整係数。</summary>
    public readonly struct StalemateParams
    {
        /// <summary>決着とみなす攻守の戦力差（これ未満の差は膠着へ寄る）。</summary>
        public readonly float decisiveMargin;
        /// <summary>膠着が成立する膠着尤度の閾値（これ以上で動かない＝デッドロック既定）。</summary>
        public readonly float deadlockThreshold;
        /// <summary>膠着中に両軍が消耗する基礎速度（per dt・最大膠着強度時）。</summary>
        public readonly float attritionRate;
        /// <summary>長い膠着が国力を削る消耗戦コストの速度（per dt）。</summary>
        public readonly float warCostRate;
        /// <summary>決着なき膠着が士気を蝕む侵食速度（per dt）。</summary>
        public readonly float moraleErosionRate;

        public StalemateParams(float decisiveMargin, float deadlockThreshold,
            float attritionRate, float warCostRate, float moraleErosionRate)
        {
            this.decisiveMargin = Mathf.Clamp01(decisiveMargin);
            this.deadlockThreshold = Mathf.Clamp01(deadlockThreshold);
            this.attritionRate = Mathf.Max(0f, attritionRate);
            this.warCostRate = Mathf.Max(0f, warCostRate);
            this.moraleErosionRate = Mathf.Max(0f, moraleErosionRate);
        }

        /// <summary>既定＝決着差0.2・膠着閾値0.6・消耗0.15/dt・消耗戦コスト0.1/dt・士気侵食0.2/dt。</summary>
        public static StalemateParams Default => new StalemateParams(0.2f, 0.6f, 0.15f, 0.1f, 0.2f);
    }

    /// <summary>
    /// 膠着戦況の純ロジック（レマルク『西部戦線異状なし』型・塹壕戦・RMK-3 #1408）。会戦には
    /// 勝利／敗北だけでなく「勝敗でない第三の結果＝拮抗（膠着・stalemate）」がある＝戦力が均衡し
    /// 攻撃側も防御側も決定的勝利を得られないと、塹壕戦のように戦線が動かず消耗だけが続く＝西部戦線の
    /// 数年に及ぶ膠着。決着がつかず両軍が血を流し続け、士気と国力を勝者なきまま削る。新技術（戦車）・
    /// 側面機動・新鋭予備兵力が膠着を打開する鍵となる。
    /// <para>分担：<see cref="PursuitRules"/>（決着後の追撃損害）・<see cref="AutoBattleSim"/>（自動解決の
    /// 勝敗決着＝Core）とは別＝こちらは決着しない第三の結果（塹壕戦の膠着）を扱う。膠着中の疲弊感は
    /// <see cref="CombatFatigueRules"/>（疲弊・同EPIC RMK）、計画が実行で削られる摩擦は
    /// <see cref="FrictionRules"/>（生成済み）に委ね、ここは「拮抗＝膠着になり戦線が動かず消耗が続く」
    /// 写像のみを担う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。</para>
    /// </summary>
    public static class StalemateRules
    {
        /// <summary>
        /// 膠着の起こりやすさ（0..1）＝戦力が拮抗し防御有利なほど膠着が起きやすい。forceBalance は
        /// 0.5 で最も拮抗（互角）・0/1 で一方的＝拮抗度＝1−|forceBalance−0.5|×2。これに防御有利を掛け、
        /// 「互角かつ守りが堅いと戦線が動かない」を出す。
        /// </summary>
        public static float StalemateLikelihood(float forceBalance, float defensiveAdvantage, StalemateParams p)
        {
            float fb = Mathf.Clamp01(forceBalance);
            float parity = 1f - Mathf.Abs(fb - 0.5f) * 2f; // 0.5で1・両端で0
            return Mathf.Clamp01(parity * Mathf.Clamp01(defensiveAdvantage));
        }

        public static float StalemateLikelihood(float forceBalance, float defensiveAdvantage)
            => StalemateLikelihood(forceBalance, defensiveAdvantage, StalemateParams.Default);

        /// <summary>
        /// 会戦の三択結果＝攻撃側／防御側の実効戦力差で決まる。防御有利は防御側戦力を底上げする
        /// （守りは堅い）。決定的差（decisiveMargin 超）があれば勝利／敗北、拮抗（差が小さい）なら膠着＝
        /// 「決定的勝利が得られないと勝敗でない第三の結果＝膠着」。これが核。
        /// </summary>
        public static BattleResult ResolveBattle(float attackerStrength, float defenderStrength,
            float defensiveAdvantage, StalemateParams p)
        {
            float atk = Mathf.Clamp01(attackerStrength);
            float def = Mathf.Clamp01(defenderStrength);
            // 防御有利＝防御側を最大2倍まで底上げ（守りが堅い＝攻めは決まりにくい）。
            float effectiveDef = def * (1f + Mathf.Clamp01(defensiveAdvantage));
            float diff = atk - effectiveDef;
            if (diff > p.decisiveMargin) return BattleResult.勝利;   // 攻撃側の決定的優位
            if (diff < -p.decisiveMargin) return BattleResult.敗北;  // 防御側の決定的優位
            return BattleResult.膠着;                                 // 拮抗＝決着せず塹壕戦へ
        }

        public static BattleResult ResolveBattle(float attackerStrength, float defenderStrength, float defensiveAdvantage)
            => ResolveBattle(attackerStrength, defenderStrength, defensiveAdvantage, StalemateParams.Default);

        /// <summary>
        /// 膠着中の相互消耗（両軍が等しく失う戦力割合・per dt）＝膠着強度×消耗速度×dt。決着なき血の代償＝
        /// 戦線は動かずとも消耗だけが続く。
        /// </summary>
        public static float MutualAttrition(float stalemateIntensity, float dt, StalemateParams p)
        {
            return Mathf.Clamp01(stalemateIntensity) * p.attritionRate * Mathf.Max(0f, dt);
        }

        public static float MutualAttrition(float stalemateIntensity, float dt)
            => MutualAttrition(stalemateIntensity, dt, StalemateParams.Default);

        /// <summary>
        /// 戦線の固着度（0..1）＝膠着尤度と地形要塞化が高いほど戦線が動かない＝
        /// stalemateLikelihood×(0.5＋terrainEntrenchment×0.5)＝塹壕・要塞化が固着を深める（要塞化ゼロでも
        /// 膠着尤度の半分は動かない）。
        /// </summary>
        public static float FrontStagnation(float stalemateLikelihood, float terrainEntrenchment, StalemateParams p)
        {
            float entrench = 0.5f + Mathf.Clamp01(terrainEntrenchment) * 0.5f;
            return Mathf.Clamp01(Mathf.Clamp01(stalemateLikelihood) * entrench);
        }

        public static float FrontStagnation(float stalemateLikelihood, float terrainEntrenchment)
            => FrontStagnation(stalemateLikelihood, terrainEntrenchment, StalemateParams.Default);

        /// <summary>
        /// 膠着を打開する力（0..1）＝新技術（戦車）・側面機動・新鋭予備兵力のいずれかが効く＝
        /// 1−(1−newTechnology)(1−flankingManeuver)(1−freshReserves)＝どれか一つでも揃えば膠着を破る方へ
        /// 働き、三つ揃えば確実に打開する。
        /// </summary>
        public static float StalemateBreaker(float newTechnology, float flankingManeuver, float freshReserves, StalemateParams p)
        {
            float tech = Mathf.Clamp01(newTechnology);
            float flank = Mathf.Clamp01(flankingManeuver);
            float reserves = Mathf.Clamp01(freshReserves);
            return Mathf.Clamp01(1f - (1f - tech) * (1f - flank) * (1f - reserves));
        }

        public static float StalemateBreaker(float newTechnology, float flankingManeuver, float freshReserves)
            => StalemateBreaker(newTechnology, flankingManeuver, freshReserves, StalemateParams.Default);

        /// <summary>
        /// 消耗戦のコスト（双方の国力を削る割合・per dt）＝膠着の長さ×消耗戦コスト速度×dt＝
        /// 長く膠着するほど勝者なきまま双方が疲弊する。
        /// </summary>
        public static float WarOfAttritionCost(float stalemateDuration, float dt, StalemateParams p)
        {
            return Mathf.Clamp01(stalemateDuration) * p.warCostRate * Mathf.Max(0f, dt);
        }

        public static float WarOfAttritionCost(float stalemateDuration, float dt)
            => WarOfAttritionCost(stalemateDuration, dt, StalemateParams.Default);

        /// <summary>
        /// 膠着下の士気（0..1）＝決着なき膠着が士気を蝕む＝膠着の長さ×士気侵食速度×dt 分だけ低下＝
        /// 「何のために死ぬのか」という厭戦。膠着が長引くほど士気が削れる。
        /// </summary>
        public static float MoraleUnderStalemate(float morale, float stalemateDuration, float dt, StalemateParams p)
        {
            float erosion = Mathf.Clamp01(stalemateDuration) * p.moraleErosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(morale) - erosion);
        }

        public static float MoraleUnderStalemate(float morale, float stalemateDuration, float dt)
            => MoraleUnderStalemate(morale, stalemateDuration, dt, StalemateParams.Default);

        /// <summary>
        /// デッドロック判定＝膠着尤度が閾値以上なら戦線が膠着して動かない（塹壕戦が固着する）。
        /// </summary>
        public static bool IsDeadlock(float stalemateLikelihood, float threshold)
        {
            return Mathf.Clamp01(stalemateLikelihood) >= Mathf.Clamp01(threshold);
        }

        public static bool IsDeadlock(float stalemateLikelihood)
            => IsDeadlock(stalemateLikelihood, StalemateParams.Default.deadlockThreshold);
    }
}
