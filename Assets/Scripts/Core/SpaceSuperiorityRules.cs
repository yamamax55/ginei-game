using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 制宙権（宙域の支配権）の調整値。哨戒戦力比の効き（指数）・支配で得られる移動/偵察/補給の自由の効き・
    /// 係争中の消耗・哨戒を引いた時の支配減衰速度。
    /// </summary>
    public readonly struct SpaceSuperiorityParams
    {
        /// <summary>哨戒戦力差に対する支配度の効き（指数・大きいほど僅差で一方に振れる）。</summary>
        public readonly float controlExponent;
        /// <summary>支配度→移動/偵察/補給の自由の効き（0..1のクランプ後に乗算）。</summary>
        public readonly float advantageScale;
        /// <summary>係争中（支配度0付近）で双方が被る消耗の最大値。</summary>
        public readonly float contestedPenalty;
        /// <summary>哨戒を引いた時に支配が中立(0)へ薄れる毎秒の速度。</summary>
        public readonly float decayRate;

        public SpaceSuperiorityParams(float controlExponent, float advantageScale, float contestedPenalty, float decayRate)
        {
            this.controlExponent = Mathf.Max(0.01f, controlExponent);
            this.advantageScale = Mathf.Clamp01(advantageScale);
            this.contestedPenalty = Mathf.Clamp01(contestedPenalty);
            this.decayRate = Mathf.Max(0f, decayRate);
        }

        /// <summary>既定：指数1.0（線形）・優位の効き1.0・係争消耗0.3・減衰速度0.5/秒。</summary>
        public static SpaceSuperiorityParams Default
            => new SpaceSuperiorityParams(DefaultControlExponent, DefaultAdvantageScale, DefaultContestedPenalty, DefaultDecayRate);

        public const float DefaultControlExponent = 1.0f;
        public const float DefaultAdvantageScale = 1.0f;
        public const float DefaultContestedPenalty = 0.3f;
        public const float DefaultDecayRate = 0.5f;
    }

    /// <summary>
    /// 制宙権（Space Superiority＝宙域の支配権・制空権の宇宙版）の純ロジック。
    /// ある宙域を支配すると偵察・補給・移動の自由を得て、敵のそれを妨げる。制宙権は艦隊の哨戒戦力で決まり、
    /// 哨戒密度・存在が高い側が支配度を握り、奪い合いになる（係争中は双方が消耗する）。
    /// 支配度は -1..1 の符号付き（+1＝完全制宙／0＝係争中／-1＝敵が完全制宙）。
    ///
    /// <b>分担</b>：
    /// - <see cref="ZoneOfControl"/>（戦術的なZOC・既存）とは別＝こちらは<b>宙域レベルの制宙権</b>（戦略/作戦規模の支配）。
    /// - <see cref="PlanetSiegeRules"/>の制空権（惑星のorbitalDefense）とは別＝こちらは惑星でなく<b>宙域</b>の支配。
    /// 盤面非依存の plain 引数のみ（艦隊・シーンに触れない）。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class SpaceSuperiorityRules
    {
        /// <summary>既定パラメータで支配度を返す。</summary>
        public static float ControlLevel(float ownPatrolStrength, float enemyPatrolStrength)
            => ControlLevel(ownPatrolStrength, enemyPatrolStrength, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 双方の哨戒戦力比から宙域の支配度(-1..1)を返す。
        /// 比 own/(own+enemy) を0.5中心に符号付き(-1..1)へ写し、指数で効きを調整。
        /// 双方0＝係争中(0)、敵0＝+1、自0＝-1。
        /// </summary>
        public static float ControlLevel(float ownPatrolStrength, float enemyPatrolStrength, SpaceSuperiorityParams p)
        {
            float own = Mathf.Max(0f, ownPatrolStrength);
            float enemy = Mathf.Max(0f, enemyPatrolStrength);
            float total = own + enemy;
            if (total <= 0f) return 0f; // 双方不在＝係争中（真空）

            float share = own / total;        // 0..1（自勢力の哨戒シェア）
            float signed = share * 2f - 1f;    // -1..1（0.5で0＝拮抗）
            // 指数は大きさにだけ掛けて符号を保つ。
            float mag = Mathf.Pow(Mathf.Abs(signed), p.controlExponent);
            return Mathf.Clamp(Mathf.Sign(signed) * mag, -1f, 1f);
        }

        /// <summary>既定パラメータで支配側の移動の自由を返す。</summary>
        public static float FreedomOfMovement(float controlLevel)
            => FreedomOfMovement(controlLevel, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 制宙権を握る側（controlLevel&gt;0）が得る移動の自由(0..1)。中立0/劣勢側は0、優勢ほど1へ。
        /// </summary>
        public static float FreedomOfMovement(float controlLevel, SpaceSuperiorityParams p)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            return Mathf.Clamp01(Mathf.Max(0f, c) * p.advantageScale);
        }

        /// <summary>既定パラメータで敵の移動阻害度を返す。</summary>
        public static float EnemyMovementDenial(float controlLevel)
            => EnemyMovementDenial(controlLevel, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 制宙権を握る側が敵の移動を妨げる度合い(0..1)。自分の支配度ぶんだけ敵の自由を奪う。
        /// </summary>
        public static float EnemyMovementDenial(float controlLevel, SpaceSuperiorityParams p)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            return Mathf.Clamp01(Mathf.Max(0f, c) * p.advantageScale);
        }

        /// <summary>既定パラメータで偵察の優位を返す。</summary>
        public static float ReconAdvantage(float controlLevel)
            => ReconAdvantage(controlLevel, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 制宙権下での偵察の優位(0..1)。宙域を支配する側だけが索敵の自由を持つ（劣勢側は0）。
        /// </summary>
        public static float ReconAdvantage(float controlLevel, SpaceSuperiorityParams p)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            return Mathf.Clamp01(Mathf.Max(0f, c) * p.advantageScale);
        }

        /// <summary>既定パラメータで補給線の安全を返す。</summary>
        public static float SupplyProtection(float controlLevel)
            => SupplyProtection(controlLevel, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 制宙権で守る補給線の安全(0..1)。支配側は補給線が守られ(→1)、劣勢側は晒される(0)。
        /// </summary>
        public static float SupplyProtection(float controlLevel, SpaceSuperiorityParams p)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            return Mathf.Clamp01(Mathf.Max(0f, c) * p.advantageScale);
        }

        /// <summary>既定パラメータで係争消耗を返す。</summary>
        public static float ContestedZonePenalty(float controlLevel)
            => ContestedZonePenalty(controlLevel, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 係争中の宙域で双方が被る消耗(0..1)。支配度0付近（拮抗＝奪い合い）で最大 contestedPenalty、
        /// どちらかが完全制宙(|c|=1)に近づくほど0へ。
        /// </summary>
        public static float ContestedZonePenalty(float controlLevel, SpaceSuperiorityParams p)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            float contestedness = 1f - Mathf.Abs(c); // 0(完全支配)..1(拮抗)
            return Mathf.Clamp01(contestedness * p.contestedPenalty);
        }

        /// <summary>既定パラメータで支配減衰を返す。</summary>
        public static float ControlDecay(float controlLevel, bool patrolWithdrawn, float dt)
            => ControlDecay(controlLevel, patrolWithdrawn, dt, SpaceSuperiorityParams.Default);

        /// <summary>
        /// 哨戒を引く（patrolWithdrawn=true）と支配度が中立(0)へ薄れる。新しい支配度を返す。
        /// 哨戒が残っている間は維持（変化なし）。dt 秒ぶん decayRate で0へ寄せる。
        /// </summary>
        public static float ControlDecay(float controlLevel, bool patrolWithdrawn, float dt, SpaceSuperiorityParams p)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            if (!patrolWithdrawn) return c;
            float step = p.decayRate * Mathf.Max(0f, dt);
            return Mathf.MoveTowards(c, 0f, step);
        }

        /// <summary>
        /// 制宙権を握ったか（controlLevel が閾値 threshold 以上で true）。
        /// </summary>
        public static bool HasSpaceSuperiority(float controlLevel, float threshold)
        {
            float c = Mathf.Clamp(controlLevel, -1f, 1f);
            return c >= threshold;
        }
    }
}
