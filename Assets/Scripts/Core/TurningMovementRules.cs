using UnityEngine;

namespace Ginei
{
    /// <summary>迂回機動の調整係数。ctor で全値をクランプする。</summary>
    public readonly struct TurningMovementParams
    {
        /// <summary>迂回コスト算出における余分距離の効き（0..1）。大きいほど遠回りのコストが重い。</summary>
        public readonly float detourCostWeight;
        /// <summary>引きずり出し圧力における敵の連絡線依存の効き（0..1）。補給線に依存する敵ほど効く。</summary>
        public readonly float dependencyWeight;
        /// <summary>迂回利得におけるコストの割引重み（0..1）。連絡線脅威から迂回コストを差し引く強さ。</summary>
        public readonly float advantageCostWeight;
        /// <summary>大回り適性の距離効果の非線形度（冪指数・1以上）。深く回るほど脅威が伸びる加速度。</summary>
        public readonly float wideExponent;
        /// <summary>迂回機動を実行可能と判定する利得の閾値（0..1）。これ以上で踏み切る。</summary>
        public readonly float viableThreshold;

        public TurningMovementParams(float detourCostWeight, float dependencyWeight,
            float advantageCostWeight, float wideExponent, float viableThreshold)
        {
            this.detourCostWeight = Mathf.Clamp01(detourCostWeight);
            this.dependencyWeight = Mathf.Clamp01(dependencyWeight);
            this.advantageCostWeight = Mathf.Clamp01(advantageCostWeight);
            this.wideExponent = Mathf.Max(1f, wideExponent);
            this.viableThreshold = Mathf.Clamp01(viableThreshold);
        }

        /// <summary>既定＝迂回コスト重み0.5／依存重み0.6／利得コスト割引0.5／大回り冪1.5／実行閾値0.4。</summary>
        public static TurningMovementParams Default =>
            new TurningMovementParams(0.5f, 0.6f, 0.5f, 1.5f, 0.4f);
    }

    /// <summary>
    /// 迂回機動の純ロジック＝ジョミニ『戦争概論』の戦略的迂回機動（turning movement・JOM-4・#1353）。敵前線を
    /// 正面から攻めず、<b>側面・後背へ大きく回り込んで敵の連絡線（補給・退路）を脅かし</b>、敵を堅固な陣地から
    /// 引きずり出す（dislodge）攻め手。迂回が深いほど敵連絡線への脅威は増すが、迂回中は自軍が晒され、迂回に
    /// 時間がかかれば敵が気づいて対応する。連絡線脅威と迂回コストのトレードオフで利得が決まる。
    /// <see cref="IndirectApproachRules"/>（最も予期されない経路の<b>心理的</b>評価）とは別＝こちらは敵の連絡線を
    /// <b>物理的に脅かす</b>迂回機動。<see cref="GalaxyPathfinder"/>（最短/Dijkstra 探索）とも別。
    /// <see cref="LineOfOperationsRules"/>（自軍作戦線の脆弱性＝<b>守り手</b>）とも別＝こちらは敵の作戦線を脅かす
    /// <b>攻め手</b>。<see cref="EncirclementRules"/>（包囲度）とも別＝こちらは<b>より大きく回り込む</b>。盤面非依存の
    /// plain 引数（距離・依存度等の連続値）で評価。倍率・圧力は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TurningMovementRules
    {
        /// <summary>
        /// 迂回コスト（0..1）＝正面経路に対する迂回経路の余分なコスト。directDistance（正面最短距離・0以下なら0）に
        /// 対する detourDistance（迂回距離）の超過分を比で取り、detourCostWeight で重み付けして0..1へ写す＝遠回り
        /// するほど時間と補給を食う。迂回が正面以下なら余分コストは0。
        /// </summary>
        public static float DetourCost(float directDistance, float detourDistance, TurningMovementParams p)
        {
            float dir = Mathf.Max(0f, directDistance);
            float det = Mathf.Max(0f, detourDistance);
            if (dir <= 0f) return 0f;
            float excessRatio = (det - dir) / dir; // 正面に対する超過比
            if (excessRatio <= 0f) return 0f;
            float raw = excessRatio / (excessRatio + 1f); // 超過比を0..1へ飽和
            return Mathf.Clamp01(p.detourCostWeight * raw);
        }

        /// <summary>既定係数での迂回コスト（0..1）。</summary>
        public static float DetourCost(float directDistance, float detourDistance)
            => DetourCost(directDistance, detourDistance, TurningMovementParams.Default);

        /// <summary>
        /// 連絡線脅威（0..1）＝迂回が敵の連絡線（後背）を脅かす度合い。detourReach（迂回の到達深度0..1＝後背に
        /// どれだけ届くか）と enemyRearExposure（敵後背の無防備さ0..1）の積＝深く回り込み、かつ敵の後背が手薄で
        /// あってこそ連絡線を脅かせる。どちらか0なら脅威0。
        /// </summary>
        public static float LineOfCommunicationThreat(float detourReach, float enemyRearExposure)
        {
            float reach = Mathf.Clamp01(detourReach);
            float exposure = Mathf.Clamp01(enemyRearExposure);
            return Mathf.Clamp01(reach * exposure);
        }

        /// <summary>
        /// 引きずり出し圧力（0..1）＝連絡線を脅かして敵を陣地から引きずり出す圧力。locThreat（連絡線脅威0..1）に、
        /// enemyDependencyOnLine（敵の連絡線への依存0..1）を dependencyWeight で混ぜた係数を掛ける＝補給線に
        /// 依存する敵ほど、連絡線を脅かされると陣地に居られず出てくる。自給自足の敵は脅威に動じにくい。
        /// </summary>
        public static float DislodgementPressure(float locThreat, float enemyDependencyOnLine, TurningMovementParams p)
        {
            float threat = Mathf.Clamp01(locThreat);
            float dep = Mathf.Clamp01(enemyDependencyOnLine);
            // 依存度を 1-w .. 1 の係数へ写す＝依存ゼロでも基礎圧は残り、依存が高いほど圧が増す。
            float depFactor = 1f - p.dependencyWeight + p.dependencyWeight * dep;
            return Mathf.Clamp01(threat * depFactor);
        }

        /// <summary>既定係数での引きずり出し圧力（0..1）。</summary>
        public static float DislodgementPressure(float locThreat, float enemyDependencyOnLine)
            => DislodgementPressure(locThreat, enemyDependencyOnLine, TurningMovementParams.Default);

        /// <summary>
        /// 迂回機動の利得（0..1）＝連絡線脅威と迂回コストのトレードオフ。locThreat（連絡線脅威0..1）から detourCost
        /// （迂回コスト0..1）を advantageCostWeight で割り引く＝敵連絡線を脅かせても、遠回りが過大なら差し引かれる。
        /// 最良の迂回機動は「敵連絡線を深く脅かし、かつ過大な遠回りでない」もの。
        /// </summary>
        public static float TurningAdvantage(float locThreat, float detourCost, TurningMovementParams p)
        {
            float threat = Mathf.Clamp01(locThreat);
            float cost = Mathf.Clamp01(detourCost);
            return Mathf.Clamp01(threat - p.advantageCostWeight * cost);
        }

        /// <summary>既定係数での迂回機動の利得（0..1）。</summary>
        public static float TurningAdvantage(float locThreat, float detourCost)
            => TurningAdvantage(locThreat, detourCost, TurningMovementParams.Default);

        /// <summary>
        /// 迂回中の脆弱性（0..1）＝迂回中に自軍が晒される脆さ。detourDistance が長いほど側面を晒す時間が伸びて
        /// 危険（距離を0..1へ飽和）で、ownScreenForce（自軍の掩護戦力0..1）が軽減する＝掩護が厚いほど安全に回れる。
        /// 掩護満点で脆弱性ゼロ、掩護ゼロで距離なりの脆弱性。
        /// </summary>
        public static float ExposureDuringDetour(float detourDistance, float ownScreenForce)
        {
            float det = Mathf.Max(0f, detourDistance);
            float raw = det / (det + 1f); // 迂回距離を0..1へ飽和
            float screen = Mathf.Clamp01(ownScreenForce);
            return Mathf.Clamp01(raw * (1f - screen));
        }

        /// <summary>
        /// 敵の反作用（0..1）＝敵が迂回に気づいて対応する反作用。enemyMobility（敵の機動力0..1）と detourTime
        /// （迂回に要する時間0..1）の積＝敵が速く、かつ迂回に時間がかかるほど、敵は迂回を察知して間に合ってしまう。
        /// 敵が鈍重か、迂回が一瞬なら反作用は小さい＝迂回が間に合う。
        /// </summary>
        public static float EnemyCounterReaction(float enemyMobility, float detourTime)
        {
            float mob = Mathf.Clamp01(enemyMobility);
            float time = Mathf.Clamp01(detourTime);
            return Mathf.Clamp01(mob * time);
        }

        /// <summary>
        /// 大回りvs小回りの適性（0..1）＝大きく回り込む（連絡線を深く脅かすが脆い）ほど高い値。detourDistance を冪で
        /// 非線形に効かせて0..1へ写し、ownForce（自軍戦力0..1）で底上げ＝大回りは深い脅威を生むが手薄になりやすく、
        /// 十分な戦力があってこそ大回りに耐えられる。戦力が乏しければ小回りが無難（低い値）。
        /// </summary>
        public static float WideVsCloseTurning(float detourDistance, float ownForce, TurningMovementParams p)
        {
            float det = Mathf.Max(0f, detourDistance);
            float raw = det / (det + 1f); // 迂回距離を0..1へ飽和
            float wide = Mathf.Pow(raw, 1f / p.wideExponent); // 冪で大回り傾向を強調
            float force = Mathf.Clamp01(ownForce);
            return Mathf.Clamp01(wide * force);
        }

        /// <summary>既定係数での大回りvs小回りの適性（0..1）。</summary>
        public static float WideVsCloseTurning(float detourDistance, float ownForce)
            => WideVsCloseTurning(detourDistance, ownForce, TurningMovementParams.Default);

        /// <summary>
        /// 迂回機動が実行可能か＝迂回利得が閾値超なら踏み切る。turningAdvantage が threshold 以上なら、連絡線脅威が
        /// 迂回コストを十分に上回る＝迂回機動を実行する価値があると判定する。
        /// </summary>
        public static bool IsTurningMovementViable(float turningAdvantage, float threshold)
        {
            return Mathf.Clamp01(turningAdvantage) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="TurningMovementParams.viableThreshold"/>）での実行可能判定。</summary>
        public static bool IsTurningMovementViable(float turningAdvantage)
            => IsTurningMovementViable(turningAdvantage, TurningMovementParams.Default.viableThreshold);
    }
}
