using UnityEngine;

namespace Ginei
{
    /// <summary>ゲリラ戦（非正規戦）の継戦力学を調整する係数。全フィールド ctor で Clamp。</summary>
    public readonly struct GuerrillaWarfareParams
    {
        /// <summary>一撃離脱の戦果重み（0..1・機動×奇襲×敵の隙に掛ける最大効果）。</summary>
        public readonly float hitRunWeight;
        /// <summary>潜伏における地形の重み（0..1・残りが住民支持の寄与＝魚と水）。</summary>
        public readonly float terrainWeight;
        /// <summary>支持基盤における不満の重み（0..1）。</summary>
        public readonly float grievanceWeight;
        /// <summary>支持基盤におけるゲリラの規律の重み（0..1・略奪は民心を失う）。</summary>
        public readonly float conductWeight;
        /// <summary>占領者の残虐さが支持を押し上げる重み（0..1・暴虐は反乱の海を広げる）。</summary>
        public readonly float brutalityWeight;
        /// <summary>敵の消耗強要の重み（0..1・一撃離脱×作戦頻度に掛ける最大消耗）。</summary>
        public readonly float attritionWeight;
        /// <summary>掃討脆弱性の下限（0..1・潜伏と支持が完璧でも消えない最低限の発見リスク）。</summary>
        public readonly float minSweepVulnerability;
        /// <summary>継戦力における外部補給の最大上乗せ（0..1・国境を越えた武器/資金/聖域）。</summary>
        public readonly float externalSupplyWeight;
        /// <summary>厭戦強要の重み（0..1・敵消耗×長期化が占領者の戦意を削る最大）。</summary>
        public readonly float wearinessWeight;

        public GuerrillaWarfareParams(float hitRunWeight, float terrainWeight, float grievanceWeight,
            float conductWeight, float brutalityWeight, float attritionWeight, float minSweepVulnerability,
            float externalSupplyWeight, float wearinessWeight)
        {
            this.hitRunWeight = Mathf.Clamp01(hitRunWeight);
            this.terrainWeight = Mathf.Clamp01(terrainWeight);
            this.grievanceWeight = Mathf.Clamp01(grievanceWeight);
            this.conductWeight = Mathf.Clamp01(conductWeight);
            this.brutalityWeight = Mathf.Clamp01(brutalityWeight);
            this.attritionWeight = Mathf.Clamp01(attritionWeight);
            this.minSweepVulnerability = Mathf.Clamp01(minSweepVulnerability);
            this.externalSupplyWeight = Mathf.Clamp01(externalSupplyWeight);
            this.wearinessWeight = Mathf.Clamp01(wearinessWeight);
        }

        /// <summary>
        /// 既定＝一撃離脱重み1.0・地形重み0.5・不満重み0.6・規律重み0.4・暴虐重み0.5・
        /// 消耗重み0.8・掃討脆弱性下限0.1・外部補給0.4・厭戦0.7。
        /// </summary>
        public static GuerrillaWarfareParams Default =>
            new GuerrillaWarfareParams(1.0f, 0.5f, 0.6f, 0.4f, 0.5f, 0.8f, 0.1f, 0.4f, 0.7f);
    }

    /// <summary>
    /// ゲリラ戦（非正規戦）の継戦力学を扱う純ロジック。占領地で正規軍に劣る勢力が
    /// 一撃離脱・潜伏・住民の支持基盤（魚と水）・敵への消耗強要・報復掃討への耐性・
    /// 長期戦による厭戦強要で戦い続ける力を表す。
    /// <para>
    /// 分担：<see cref="InsurgencyRules"/> は外部支援された反乱の発生・組織化を扱う。
    /// <see cref="RaidingPartyRules"/> は襲撃隊（一回の襲撃の損害）を扱う。
    /// 本クラスは「非正規戦をどれだけ続けられるか」＝継戦力学に責任を持つ（別系統）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論。LINQ/Log/Exp 不使用。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GuerrillaWarfareRules
    {
        /// <summary>
        /// 一撃離脱の戦果（0..1）＝機動 mobility×奇襲 surprise×敵の隙 enemyVulnerability に重みを掛ける。
        /// 速く動き、不意を突き、敵が手薄なときに最大効果＝どれか欠ければ戦果は薄れる（積で効く）。
        /// </summary>
        public static float HitAndRunEffect(float mobility, float surprise, float enemyVulnerability, GuerrillaWarfareParams p)
        {
            float t = Mathf.Clamp01(mobility) * Mathf.Clamp01(surprise) * Mathf.Clamp01(enemyVulnerability);
            return Mathf.Clamp01(p.hitRunWeight * t);
        }

        public static float HitAndRunEffect(float mobility, float surprise, float enemyVulnerability)
            => HitAndRunEffect(mobility, surprise, enemyVulnerability, GuerrillaWarfareParams.Default);

        /// <summary>
        /// 潜伏度（0..1）＝地形の遮蔽 terrainCover×terrainWeight ＋ 住民支持 populationSupport×(1-terrainWeight)。
        /// 山岳/森林/市街は身を隠し、住民が匿えば隠れられる＝魚は水中に隠れる（地形と民心の加重平均）。
        /// </summary>
        public static float Concealment(float terrainCover, float populationSupport, GuerrillaWarfareParams p)
        {
            float t = Mathf.Clamp01(terrainCover) * p.terrainWeight
                + Mathf.Clamp01(populationSupport) * (1f - p.terrainWeight);
            return Mathf.Clamp01(t);
        }

        public static float Concealment(float terrainCover, float populationSupport)
            => Concealment(terrainCover, populationSupport, GuerrillaWarfareParams.Default);

        /// <summary>
        /// 住民の支持基盤（0..1）＝不満 grievance×規律 guerrillaConduct（略奪すれば民心を失う）に、
        /// 占領者の残虐さ occupierBrutality の押し上げを加える。
        /// 不満があってもゲリラが規律を欠けば支持は得られず、占領者の暴虐は支持を底上げする（反乱の海）。
        /// </summary>
        public static float PopularSupportBase(float grievance, float guerrillaConduct, float occupierBrutality, GuerrillaWarfareParams p)
        {
            float earned = Mathf.Clamp01(grievance) * p.grievanceWeight
                * Mathf.Lerp(1f - p.conductWeight, 1f, Mathf.Clamp01(guerrillaConduct));
            float pushed = Mathf.Clamp01(occupierBrutality) * p.brutalityWeight;
            return Mathf.Clamp01(earned + pushed);
        }

        public static float PopularSupportBase(float grievance, float guerrillaConduct, float occupierBrutality)
            => PopularSupportBase(grievance, guerrillaConduct, occupierBrutality, GuerrillaWarfareParams.Default);

        /// <summary>
        /// 敵への消耗強要（0..1）＝一撃離脱の戦果 hitAndRunEffect×作戦頻度 operationTempo×重み。
        /// 効果的な一撃離脱を高頻度で繰り返すほど、正規軍にじわじわ出血を強いる（数多の小傷で大樹を倒す）。
        /// </summary>
        public static float EnemyAttrition(float hitAndRunEffect, float operationTempo, GuerrillaWarfareParams p)
        {
            float t = Mathf.Clamp01(hitAndRunEffect) * Mathf.Clamp01(operationTempo);
            return Mathf.Clamp01(p.attritionWeight * t);
        }

        public static float EnemyAttrition(float hitAndRunEffect, float operationTempo)
            => EnemyAttrition(hitAndRunEffect, operationTempo, GuerrillaWarfareParams.Default);

        /// <summary>
        /// 掃討に対する脆弱性（minSweepVulnerability..1）＝潜伏 concealment と住民支持 popularSupport が高いほど低い。
        /// 隠れ、住民が密告しないほど報復掃討で見つからない＝脆弱性は下がる（下限まで・完全には消えない）。
        /// </summary>
        public static float SweepVulnerability(float concealment, float popularSupport, GuerrillaWarfareParams p)
        {
            // 潜伏と支持の高い方ではなく平均で守りを測る（両方が要る）。
            float shield = (Mathf.Clamp01(concealment) + Mathf.Clamp01(popularSupport)) * 0.5f;
            return Mathf.Lerp(1f, p.minSweepVulnerability, shield);
        }

        public static float SweepVulnerability(float concealment, float popularSupport)
            => SweepVulnerability(concealment, popularSupport, GuerrillaWarfareParams.Default);

        /// <summary>
        /// 継戦力（0..1）＝住民支持 popularSupport×潜伏 concealment（魚と水の積）に、
        /// 外部補給 externalSupply の上乗せを加える。
        /// 支持と潜伏のどちらかが崩れれば継戦力は痩せ、国境を越えた補給がそれを底上げする。
        /// </summary>
        public static float Sustainability(float popularSupport, float concealment, float externalSupply, GuerrillaWarfareParams p)
        {
            float core = Mathf.Clamp01(popularSupport) * Mathf.Clamp01(concealment);
            float supply = Mathf.Clamp01(externalSupply) * p.externalSupplyWeight;
            return Mathf.Clamp01(core + supply);
        }

        public static float Sustainability(float popularSupport, float concealment, float externalSupply)
            => Sustainability(popularSupport, concealment, externalSupply, GuerrillaWarfareParams.Default);

        /// <summary>
        /// 占領者への厭戦強要（0..1）＝敵消耗 enemyAttrition×長期化 conflictDuration×重み。
        /// 出血が長引くほど占領者の世論は撤退へ傾く＝長期戦は弱者の武器（消耗と時間の積）。
        /// </summary>
        public static float WarWeariness(float enemyAttrition, float conflictDuration, GuerrillaWarfareParams p)
        {
            float t = Mathf.Clamp01(enemyAttrition) * Mathf.Clamp01(conflictDuration);
            return Mathf.Clamp01(p.wearinessWeight * t);
        }

        public static float WarWeariness(float enemyAttrition, float conflictDuration)
            => WarWeariness(enemyAttrition, conflictDuration, GuerrillaWarfareParams.Default);

        /// <summary>
        /// ゲリラ戦が成立するか＝継戦力 sustainability が高く、掃討脆弱性 sweepVulnerability が低い。
        /// 成立度＝sustainability×(1-sweepVulnerability) が threshold 以上なら継戦可能（生き延びて戦い続けられる）。
        /// </summary>
        public static bool IsGuerrillaViable(float sustainability, float sweepVulnerability, float threshold)
        {
            float viability = Mathf.Clamp01(sustainability) * (1f - Mathf.Clamp01(sweepVulnerability));
            return viability >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値 0.25 で成立判定。</summary>
        public static bool IsGuerrillaViable(float sustainability, float sweepVulnerability)
            => IsGuerrillaViable(sustainability, sweepVulnerability, 0.25f);
    }
}
