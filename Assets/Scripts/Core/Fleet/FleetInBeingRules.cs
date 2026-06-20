using UnityEngine;

namespace Ginei
{
    /// <summary>現存艦隊（fleet in being）の調整係数。</summary>
    public readonly struct FleetInBeingParams
    {
        /// <summary>潜在脅威に占める機動（投射距離）の寄与重み（0..1）。残りは戦力そのもの。</summary>
        public readonly float mobilityWeight;
        /// <summary>分散強要が満点(1.0)になる「潜在脅威/敵総戦力」比。これ未満は比例で割り引く。</summary>
        public readonly float dispersionFullRatio;
        /// <summary>無活動で脅威が減衰する速さ（1単位時間あたりの減衰係数）。睨むだけでは軽視される。</summary>
        public readonly float passiveDecayRate;
        /// <summary>出撃の気配（陽動）が減衰分をどれだけ取り戻すかの上限係数（0..1）。</summary>
        public readonly float sortieRevivalScale;

        public FleetInBeingParams(float mobilityWeight, float dispersionFullRatio, float passiveDecayRate, float sortieRevivalScale)
        {
            this.mobilityWeight = Mathf.Clamp01(mobilityWeight);
            this.dispersionFullRatio = Mathf.Max(0.01f, dispersionFullRatio);
            this.passiveDecayRate = Mathf.Max(0f, passiveDecayRate);
            this.sortieRevivalScale = Mathf.Clamp01(sortieRevivalScale);
        }

        /// <summary>既定＝機動重み0.4・分散満点比1.0・無活動減衰0.1/時・出撃再活性上限0.8。</summary>
        public static FleetInBeingParams Default => new FleetInBeingParams(0.4f, 1f, 0.1f, 0.8f);
    }

    /// <summary>
    /// 現存艦隊戦略（fleet in being・LDH-4）の純ロジック。攻撃しない存在脅威＝艦隊が「在る」だけで
    /// 敵の機動を縛る。無傷の艦隊が存在する限り、敵はそれに備えて戦力を割かねばならず（分散強要）、
    /// 自由に動けない。潜在脅威は戦力×機動で投射され、敵総戦力に対する比で分散強要へ写る。
    /// ただし動かずにいると脅威は時間で減衰し（睨むだけでは軽視される）、時々の出撃の気配で再活性する。
    /// 現存艦隊戦略は艦隊を失えば全ての縛りが消えるリスク（決戦回避＝温存が前提）を伴うが、
    /// 戦わず敵を縛れれば費用対効果は高い。盤面非依存の plain 引数・乱数なし・決定論。
    /// 分担：FleetDoctrineRules（現存艦隊を含む4ドクトリンのAI行動重み）とは別＝こちらは
    /// 現存艦隊の「分散強要」の数値モデルに特化／DeterrenceRules（報復能力×信憑性の抑止）とは別＝
    /// 戦わず縛る機動的抑止／RivalryRules 等の関係性とも別。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FleetInBeingRules
    {
        /// <summary>
        /// 潜在脅威（0..1）＝戦力(0..1)を、機動(0..1)で「投射距離」分だけ底上げした値。
        /// ＝戦力×((1−機動重み)＋機動重み×機動)。戦わずとも、強く・遠くまで届く艦隊ほど脅威が大きい。
        /// </summary>
        public static float LatentThreat(float fleetStrength, float mobility, FleetInBeingParams p)
        {
            float s = Mathf.Clamp01(fleetStrength);
            float m = Mathf.Clamp01(mobility);
            float projection = (1f - p.mobilityWeight) + p.mobilityWeight * m;
            return Mathf.Clamp01(s * projection);
        }

        public static float LatentThreat(float fleetStrength, float mobility)
            => LatentThreat(fleetStrength, mobility, FleetInBeingParams.Default);

        /// <summary>
        /// 分散強要（0..1）＝敵が備えのために割かねばならない戦力割合。
        /// ＝Min(潜在脅威/(敵総戦力×満点比), 1)。敵が小さいほど、同じ脅威でも縛りは強い。
        /// 敵総戦力0以下なら縛る相手がいない＝0。
        /// </summary>
        public static float ForcedDispersion(float latentThreat, float enemyTotalForce, FleetInBeingParams p)
        {
            float enemy = Mathf.Max(0f, enemyTotalForce);
            if (enemy <= 0f) return 0f;
            float threat = Mathf.Clamp01(latentThreat);
            return Mathf.Clamp01(threat / (enemy * p.dispersionFullRatio));
        }

        public static float ForcedDispersion(float latentThreat, float enemyTotalForce)
            => ForcedDispersion(latentThreat, enemyTotalForce, FleetInBeingParams.Default);

        /// <summary>敵の行動の自由（0..1）＝1−分散強要。縛られるほど低い（自由に動けない）。</summary>
        public static float EnemyFreedomOfAction(float forcedDispersion)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(forcedDispersion));
        }

        /// <summary>
        /// 無活動による脅威減衰後の潜在脅威（0..1）＝潜在脅威×(1−減衰率×無活動時間)（下限0）。
        /// 動かずにいると「いざとなれば出てこない」と見なされ、時間で軽視される。
        /// </summary>
        public static float ThreatDecayIfPassive(float latentThreat, float inactivityTime, FleetInBeingParams p)
        {
            float threat = Mathf.Clamp01(latentThreat);
            float t = Mathf.Max(0f, inactivityTime);
            float factor = Mathf.Clamp01(1f - p.passiveDecayRate * t);
            return Mathf.Clamp01(threat * factor);
        }

        public static float ThreatDecayIfPassive(float latentThreat, float inactivityTime)
            => ThreatDecayIfPassive(latentThreat, inactivityTime, FleetInBeingParams.Default);

        /// <summary>
        /// 出撃の気配による脅威再活性（0..1）＝減衰後の脅威＋失われた余地×出撃陽動度×再活性上限。
        /// 時々動いて存在を思い出させると、軽視された脅威が（上限まで）取り戻される。
        /// 元の潜在脅威を超えては戻らない（再活性は減衰の埋め戻し）。
        /// </summary>
        public static float SortieThreatRevival(float threatDecay, float sortieFeint, FleetInBeingParams p)
        {
            float decayed = Mathf.Clamp01(threatDecay);
            float feint = Mathf.Clamp01(sortieFeint);
            // 失われた余地＝(1−減衰後)。ただし減衰後が低いほど取り戻せる余地が大きい。
            float lost = 1f - decayed;
            float revived = decayed + lost * feint * p.sortieRevivalScale;
            return Mathf.Clamp01(revived);
        }

        public static float SortieThreatRevival(float threatDecay, float sortieFeint)
            => SortieThreatRevival(threatDecay, sortieFeint, FleetInBeingParams.Default);

        /// <summary>
        /// 撃破リスク（0..1）＝敵集中度(0..1)×(1−自軍戦力(0..1))。
        /// 現存艦隊戦略は艦隊を失えば全ての縛りが消える＝敵が戦力を集中し、自軍が弱るほど高い。
        /// 強大な無傷の艦隊（戦力1.0）はそもそも捕捉撃破されにくい。
        /// </summary>
        public static float RiskOfDestruction(float fleetStrength, float enemyConcentration)
        {
            float survivability = 1f - Mathf.Clamp01(fleetStrength);
            return Mathf.Clamp01(Mathf.Clamp01(enemyConcentration) * survivability);
        }

        /// <summary>
        /// 費用対効果＝分散強要(0..1)/維持費。戦わず敵を多く縛るほど、維持費が安いほど高い。
        /// 維持費0以下は不定なので、ごく小さな下限でクランプ（無償の縛りは過大評価を避ける）。
        /// </summary>
        public static float CostEffectiveness(float forcedDispersion, float ownUpkeep)
        {
            float upkeep = Mathf.Max(0.01f, ownUpkeep);
            return Mathf.Clamp01(forcedDispersion) / upkeep;
        }

        /// <summary>現存艦隊戦略が機能しているか＝分散強要が閾値超（閾値は0..1にクランプ）。</summary>
        public static bool IsEffectiveFleetInBeing(float forcedDispersion, float threshold)
        {
            return Mathf.Clamp01(forcedDispersion) > Mathf.Clamp01(threshold);
        }
    }
}
