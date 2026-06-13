using UnityEngine;

namespace Ginei
{
    /// <summary>安全保障のジレンマの調整係数。</summary>
    public readonly struct SecurityDilemmaParams
    {
        /// <summary>螺旋tickで自衛軍備が増減する基礎速度（per dt）。</summary>
        public readonly float spiralGain;
        /// <summary>攻撃有利(offense-defense balance)が脅威認識を増幅する幅（1なら攻撃有利最大で脅威2倍）。</summary>
        public readonly float offenseAdvantageScale;
        /// <summary>偶発(accident)が誰も望まない戦争へ寄与する重み（残りは螺旋強度）。</summary>
        public readonly float accidentWeight;
        /// <summary>信頼醸成措置（透明性＋自制）が螺旋を緩める最大係数（1なら完全な信頼で螺旋消失）。</summary>
        public readonly float trustReliefScale;

        public SecurityDilemmaParams(float spiralGain, float offenseAdvantageScale,
            float accidentWeight, float trustReliefScale)
        {
            this.spiralGain = Mathf.Max(0f, spiralGain);
            this.offenseAdvantageScale = Mathf.Max(0f, offenseAdvantageScale);
            this.accidentWeight = Mathf.Clamp01(accidentWeight);
            this.trustReliefScale = Mathf.Clamp01(trustReliefScale);
        }

        /// <summary>既定＝螺旋速度0.5・攻撃有利増幅1・偶発重み0.3・信頼緩和1。</summary>
        public static SecurityDilemmaParams Default => new SecurityDilemmaParams(0.5f, 1f, 0.3f, 1f);
    }

    /// <summary>
    /// 安全保障のジレンマの純ロジック（#1461・ホッブズ的アナーキー）。国家Aが純粋に防衛目的で軍備を
    /// 増強しても、それが攻撃能力と区別できない（攻防の区別不能性）ため隣国Bは脅威と受け取り対抗軍備する。
    /// 結果、双方の安全を求める努力が相互の不安全を生み「誰も望まない戦争」へ向かう＝構造的猜疑の螺旋。
    /// 核は「攻防の区別不能性 ambiguity」と「脅威認識の構造」であり、量の反応そのものを解く
    /// <see cref="ArmsRaceRules"/>（リチャードソン型＝量の螺旋）とは別系統：こちらは防衛動機が
    /// なぜ猜疑を生むかの構造（攻撃有利な技術・地勢ほど深刻／兵器が防御的と区別できるほど緩む）を扱う。
    /// 抑止（<see cref="DeterrenceRules"/>＝戦力差が開戦を防ぐ効用）の前段＝なぜ軍備が積み上がるかの側、
    /// アナーキーの構造コスト（AnarchyCostRules・同EPIC LEVI＝主権なき秩序の費用）と対をなし、
    /// 危機が梯子を昇る過程（<see cref="EscalationRules"/>）の起点に当たる。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SecurityDilemmaRules
    {
        /// <summary>
        /// 受け取る脅威（0..1）＝隣国の軍備(0..1)×攻防の区別不能性(0..1)。
        /// 隣国が防衛のつもりでも、攻撃能力と区別できない（ambiguity が高い）ほど脅威に見える。
        /// 区別不能性が0なら（純粋に防御的と分かれば）どれだけ軍備しても脅威ゼロ。
        /// </summary>
        public static float PerceivedThreat(float neighborArmament, float offenseDefenseAmbiguity)
        {
            return Mathf.Clamp01(neighborArmament) * Mathf.Clamp01(offenseDefenseAmbiguity);
        }

        /// <summary>
        /// 防衛的軍備（0..1）＝受け取った脅威(0..1)×（1−自国の安全感(0..1)）。
        /// 脅威を感じ、かつ安全に不足を覚えるほど自衛のため軍備する（純粋に守るつもりでも）。
        /// 既に十分安全(ownSecurity=1)なら軍備しない。
        /// </summary>
        public static float DefensiveBuildup(float perceivedThreat, float ownSecurity)
        {
            return Mathf.Clamp01(perceivedThreat) * (1f - Mathf.Clamp01(ownSecurity));
        }

        /// <summary>
        /// 螺旋的増大の1tick後（buildupA, buildupB のタプル）。互いの自衛軍備が相手の脅威認識を高め、
        /// 相手はさらに自衛軍備する＝安全を求めて不安全になる正のフィードバック。
        /// 各陣営は「相手の現在の軍備」を脅威として読み（防衛動機を信じられない）、螺旋速度で漸増する。
        /// </summary>
        public static (float buildupA, float buildupB) SpiralEscalation(
            float buildupA, float buildupB, float dt, SecurityDilemmaParams p)
        {
            float a = Mathf.Clamp01(buildupA);
            float b = Mathf.Clamp01(buildupB);
            float step = Mathf.Max(0f, dt);

            // 相手の軍備を脅威と読み、自衛のため軍備を上積みする＝相手が防衛のつもりでも
            // 攻防が区別できない（構造的猜疑）ので、双方が相手の軍備に比例して競り上がる正のフィードバック。
            float newA = a + b * p.spiralGain * step;
            float newB = b + a * p.spiralGain * step;
            return (Mathf.Clamp01(newA), Mathf.Clamp01(newB));
        }

        public static (float buildupA, float buildupB) SpiralEscalation(
            float buildupA, float buildupB, float dt)
            => SpiralEscalation(buildupA, buildupB, dt, SecurityDilemmaParams.Default);

        /// <summary>
        /// 攻防バランス（0..1）＝攻撃有利度(0..1)を増幅係数で写したジレンマ深刻度。
        /// 攻撃有利な技術・地勢ほど（先制が得なら）猜疑が増し、ジレンマが深刻になる。
        /// 防御有利(offenseAdvantage=0)なら深刻度ゼロ＝守る側が報われるので軍備が脅威にならない。
        /// </summary>
        public static float OffenseDefenseBalance(float offenseAdvantage, SecurityDilemmaParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(offenseAdvantage) * p.offenseAdvantageScale);
        }

        public static float OffenseDefenseBalance(float offenseAdvantage)
            => OffenseDefenseBalance(offenseAdvantage, SecurityDilemmaParams.Default);

        /// <summary>
        /// 区別可能性によるジレンマ緩和倍率（0..1）＝1−兵器の防御的明確さ(0..1)。
        /// 兵器が防御的と明確に区別できる（weaponType=1）ほど攻防が見分けられて安心＝倍率0でジレンマ消失。
        /// 区別できない（weaponType=0）なら緩和なし＝倍率1でジレンマがそのまま残る。
        /// </summary>
        public static float Distinguishability(float weaponType)
        {
            return 1f - Mathf.Clamp01(weaponType);
        }

        /// <summary>
        /// 誰も望まない戦争の確率（0..1）＝螺旋強度(0..1)×(1−偶発重み)＋偶発(0..1)×偶発重み。
        /// 誰も戦争を望まないのに、自衛軍備の螺旋が高まり、偶発的事件が引き金を引いて戦争に至る。
        /// 螺旋がゼロでも偶発だけで一定の確率が残り、螺旋が高いほど偶発が戦争へ転びやすい。
        /// </summary>
        public static float UnwantedWar(float spiralIntensity, float accident, SecurityDilemmaParams p)
        {
            float spiral = Mathf.Clamp01(spiralIntensity);
            float acc = Mathf.Clamp01(accident);
            return Mathf.Clamp01(spiral * (1f - p.accidentWeight) + acc * p.accidentWeight);
        }

        public static float UnwantedWar(float spiralIntensity, float accident)
            => UnwantedWar(spiralIntensity, accident, SecurityDilemmaParams.Default);

        /// <summary>
        /// 信頼醸成措置による緩和倍率（0..1）＝1−（透明性(0..1)＋自制(0..1)）/2×緩和係数。
        /// 透明性（査察・情報公開）と自制（軍縮）が猜疑の螺旋を緩める＝倍率を下げる。
        /// 双方とも満点なら倍率は1−trustReliefScale（既定で0＝螺旋を完全に止める）。
        /// </summary>
        public static float TrustBuildingMeasures(float transparency, float restraint, SecurityDilemmaParams p)
        {
            float trust = (Mathf.Clamp01(transparency) + Mathf.Clamp01(restraint)) * 0.5f;
            return Mathf.Clamp01(1f - trust * p.trustReliefScale);
        }

        public static float TrustBuildingMeasures(float transparency, float restraint)
            => TrustBuildingMeasures(transparency, restraint, SecurityDilemmaParams.Default);

        /// <summary>
        /// 安全保障の螺旋に陥ったか＝螺旋強度が閾値以上（閾値は0..1にクランプ）。
        /// 双方の自衛努力が相互不安全を生む正のフィードバックに突入した判定。
        /// </summary>
        public static bool IsSecuritySpiral(float spiralIntensity, float threshold)
        {
            return Mathf.Clamp01(spiralIntensity) >= Mathf.Clamp01(threshold);
        }
    }
}
