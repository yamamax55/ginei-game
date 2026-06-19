using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軌道インフラ（宇宙港・軌道造船所・軌道居住区・防衛プラットフォーム）の調整係数。
    /// 建設の進捗、施設の機能（交易ハブ・建艦・居住）、軌道防衛、相乗効果、被攻撃時の脆弱性、軌道混雑を司る。
    /// </summary>
    public readonly struct OrbitalInfrastructureParams
    {
        /// <summary>建設進捗の全体スケール（資材×労働/複雑さに掛ける）。</summary>
        public readonly float constructionScale;
        /// <summary>宇宙港の混雑飽和係数（交通量がこの倍率を超えると処理量が頭打ち＝発着待ち）。</summary>
        public readonly float portCongestionScale;
        /// <summary>軌道造船所の産出スケール（造船能力×サプライチェーンに掛ける）。</summary>
        public readonly float shipyardOutputScale;
        /// <summary>軌道居住区の収容スケール（与圧容積×生命維持に掛ける）。</summary>
        public readonly float habitatScale;
        /// <summary>惑星シールドが軌道防衛を底上げする係数（地表シールドが軌道施設を傘下に守る）。</summary>
        public readonly float shieldDefenseBonus;
        /// <summary>施設の相乗強度（施設が集まり統合度が高いほど複合効率が上がる）。</summary>
        public readonly float synergyScale;
        /// <summary>軌道施設の素の脆さ（防御が無いと施設が多いほど一撃で崩れる＝軌道は脆い）。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>インフラが稼働可能と見なす建設進捗の既定閾値（0..1）。</summary>
        public readonly float operationalThreshold;

        public OrbitalInfrastructureParams(float constructionScale, float portCongestionScale,
                                           float shipyardOutputScale, float habitatScale,
                                           float shieldDefenseBonus, float synergyScale,
                                           float vulnerabilityScale, float operationalThreshold)
        {
            this.constructionScale = Mathf.Max(0f, constructionScale);
            this.portCongestionScale = Mathf.Max(0.0001f, portCongestionScale);
            this.shipyardOutputScale = Mathf.Max(0f, shipyardOutputScale);
            this.habitatScale = Mathf.Max(0f, habitatScale);
            this.shieldDefenseBonus = Mathf.Max(0f, shieldDefenseBonus);
            this.synergyScale = Mathf.Max(0f, synergyScale);
            this.vulnerabilityScale = Mathf.Clamp01(vulnerabilityScale);
            this.operationalThreshold = Mathf.Clamp01(operationalThreshold);
        }

        /// <summary>
        /// 既定＝建設スケール1.0・混雑飽和2.0・造船産出1.0・居住1.0・シールド底上げ0.5・相乗0.25・
        /// 素の脆さ1.0・稼働閾値0.8。建設は資材と軌道労働の積を複雑さで割る、宇宙港は交通が接岸能力の
        /// 2倍を超えると頭打ち、軌道施設は防御が無いと脆い（脆さ1.0）。
        /// </summary>
        public static OrbitalInfrastructureParams Default
            => new OrbitalInfrastructureParams(1f, 2f, 1f, 1f, 0.5f, 0.25f, 1f, 0.8f);
    }

    /// <summary>
    /// 軌道インフラの純ロジック（宇宙港・軌道造船所・軌道居住区・防衛プラットフォームの建設と機能）。
    /// 軌道上に築くインフラ＝建設は資材と軌道労働で進み複雑さで遅れる、宇宙港は接岸能力で処理するが
    /// 交通が増えると混雑で頭打ち、軌道造船所はサプライチェーンが細ると産出が落ち、軌道居住区は生命維持に依る。
    /// 施設が集まり統合されると相乗効果（複合施設の効率）が出るが、軌道施設は脆く防御が無いと被攻撃で崩れる。
    /// 占領・攻城の防御側（層別迎撃）は <see cref="PlanetaryDefenseRules"/>、要塞の点防御は <see cref="FortressRules"/>、
    /// 造船パイプライン本体は <see cref="ShipyardRules"/> が担う（別系統）。本ルールは軌道<b>インフラ</b>の建設と機能に特化。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OrbitalInfrastructureRules
    {
        /// <summary>
        /// 建設進捗（0..1）。資材×軌道労働を複雑さ（+1）で割り、スケールを掛けてクランプ＝
        /// 資材と労働が揃うほど速く、複雑な施設ほど遅れる。複雑さが大きいと分母が膨らみ進捗が鈍る。
        /// 資材/労働ゼロは進捗ゼロ。
        /// </summary>
        public static float ConstructionProgress(float materialsDelivered, float orbitalLabor, float complexity,
                                                 OrbitalInfrastructureParams p)
        {
            float materials = Mathf.Max(0f, materialsDelivered);
            float labor = Mathf.Max(0f, orbitalLabor);
            float cplx = Mathf.Max(0f, complexity);
            float raw = (materials * labor) / (1f + cplx);
            return Mathf.Clamp01(raw * p.constructionScale);
        }

        public static float ConstructionProgress(float materialsDelivered, float orbitalLabor, float complexity)
            => ConstructionProgress(materialsDelivered, orbitalLabor, complexity, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// 宇宙港の処理量（≧0）。接岸能力で捌くが、交通量が接岸能力×混雑飽和係数を超えると頭打ち
        /// ＝発着待ちで処理が増えない。交通が能力以下なら交通そのまま（過剰能力は遊ぶ）、
        /// 能力～飽和点の間は接岸能力で捌き、それ以上は接岸能力×混雑飽和係数で頭打ち。
        /// </summary>
        public static float PortThroughput(float dockingCapacity, float trafficVolume, OrbitalInfrastructureParams p)
        {
            float capacity = Mathf.Max(0f, dockingCapacity);
            float traffic = Mathf.Max(0f, trafficVolume);
            float ceiling = capacity * p.portCongestionScale; // 混雑で捌ける上限
            // 交通が能力以下＝交通そのまま、能力超は能力で捌くが上限(ceiling)で頭打ち。
            float served = Mathf.Min(traffic, capacity);
            float overflow = Mathf.Max(0f, traffic - capacity);
            float congested = Mathf.Min(overflow, ceiling - capacity); // 飽和点までは追加で捌ける
            return served + congested;
        }

        public static float PortThroughput(float dockingCapacity, float trafficVolume)
            => PortThroughput(dockingCapacity, trafficVolume, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// 軌道造船所の産出（≧0）。造船能力×サプライチェーン（0..1）×スケール＝部材供給が細ると
        /// 能力があっても産出が落ちる（サプライチェーン0で停止）。
        /// </summary>
        public static float OrbitalShipyardOutput(float shipyardCapacity, float supplyChain, OrbitalInfrastructureParams p)
        {
            float capacity = Mathf.Max(0f, shipyardCapacity);
            float supply = Mathf.Clamp01(supplyChain);
            return capacity * supply * p.shipyardOutputScale;
        }

        public static float OrbitalShipyardOutput(float shipyardCapacity, float supplyChain)
            => OrbitalShipyardOutput(shipyardCapacity, supplyChain, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// 軌道居住区の収容力（≧0）。与圧容積×生命維持（0..1）×スケール＝容積が大きく生命維持が
        /// 健全なほど多く住める。生命維持が落ちると（事故・損傷）収容力が落ちる（0で居住不能）。
        /// </summary>
        public static float HabitatCapacity(float pressurizedVolume, float lifeSupport, OrbitalInfrastructureParams p)
        {
            float volume = Mathf.Max(0f, pressurizedVolume);
            float life = Mathf.Clamp01(lifeSupport);
            return volume * life * p.habitatScale;
        }

        public static float HabitatCapacity(float pressurizedVolume, float lifeSupport)
            => HabitatCapacity(pressurizedVolume, lifeSupport, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// 軌道防衛（≧0）。防衛プラットフォーム×(1+惑星シールド×シールド底上げ係数)＝
        /// 地表の惑星シールド（0..1）が軌道施設を傘下に守る（プラットフォームの実効を底上げ）。
        /// プラットフォームが無ければ0（シールドだけでは軌道を守れない）。
        /// </summary>
        public static float OrbitalDefense(float defensePlatforms, float planetaryShield, OrbitalInfrastructureParams p)
        {
            float platforms = Mathf.Max(0f, defensePlatforms);
            float shield = Mathf.Clamp01(planetaryShield);
            return platforms * (1f + shield * p.shieldDefenseBonus);
        }

        public static float OrbitalDefense(float defensePlatforms, float planetaryShield)
            => OrbitalDefense(defensePlatforms, planetaryShield, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// 施設の相乗効果倍率（≧1）。施設数（2施設以上から効く）と統合度（0..1）に応じて複合効率が上がる
        /// ＝施設が孤立せず統合されるほど効率が良い。1施設のみ/統合0は相乗なし（1.0）。
        /// 倍率＝1 + 相乗強度×統合度×(施設数-1)。
        /// </summary>
        public static float Synergy(int facilityCount, float integration, OrbitalInfrastructureParams p)
        {
            int count = Mathf.Max(0, facilityCount);
            if (count < 2) return 1f; // 単独では相乗なし
            float integ = Mathf.Clamp01(integration);
            return 1f + p.synergyScale * integ * (count - 1);
        }

        public static float Synergy(int facilityCount, float integration)
            => Synergy(facilityCount, integration, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// 被攻撃時の脆弱性（0..1）。施設数が多いほど（標的が多い）、軌道防衛が低いほど脆い＝
        /// 軌道施設は脆く防御が無いと一撃で崩れる。脆さ＝素の脆さ×(1-clamp01(防御))×(施設数の集中度)。
        /// 防御が満タンなら脆さ0（守り切る）、防御ゼロかつ多数施設なら素の脆さに近づく。
        /// orbitalDefense は 0..1 に正規化して受ける想定（呼び出し側で <see cref="OrbitalDefense"/> を正規化）。
        /// </summary>
        public static float VulnerabilityToAttack(int facilityCount, float orbitalDefense, OrbitalInfrastructureParams p)
        {
            int count = Mathf.Max(0, facilityCount);
            if (count <= 0) return 0f; // 施設が無ければ失う物も無い
            float defense = Mathf.Clamp01(orbitalDefense);
            // 施設が多いほど標的が増える＝集中度 1-1/count（1施設で0、無限で1へ漸近）。
            float concentration = 1f - 1f / count;
            float raw = p.vulnerabilityScale * (1f - defense) * concentration;
            return Mathf.Clamp01(raw);
        }

        public static float VulnerabilityToAttack(int facilityCount, float orbitalDefense)
            => VulnerabilityToAttack(facilityCount, orbitalDefense, OrbitalInfrastructureParams.Default);

        /// <summary>
        /// インフラが稼働可能か（建設進捗が閾値以上か）。閾値未満は建設中で機能しない。
        /// </summary>
        public static bool IsInfrastructureOperational(float constructionProgress, float threshold)
            => Mathf.Clamp01(constructionProgress) >= Mathf.Clamp01(threshold);

        public static bool IsInfrastructureOperational(float constructionProgress)
            => IsInfrastructureOperational(constructionProgress, OrbitalInfrastructureParams.Default.operationalThreshold);
    }
}
