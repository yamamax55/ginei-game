using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資源カルテル（戦略資源の供給支配と価格操作）の調整値。資源の重要度の効き・生産調整のてこ・
    /// 抜け駆けの誘惑の効き・加盟数で結束が崩れる速さ・武器化の効き・需要側対抗の備蓄/代替の効き。
    /// </summary>
    public readonly struct ResourceCartelParams
    {
        /// <summary>資源の重要度→市場支配力の効き（0..1。0で重要度無視・1で完全反映）。</summary>
        public readonly float criticalityWeight;
        /// <summary>生産調整→価格つり上げのてこ（>1で減産が価格に強く効く）。</summary>
        public readonly float cutLeverage;
        /// <summary>1加盟あたり結束が崩れる係数（大きいほど多人数で崩れやすい）。</summary>
        public readonly float defectionPerMember;
        /// <summary>武器化（禁輸）の威力の効き（市場支配×政治意志への倍率）。</summary>
        public readonly float weaponizationWeight;
        /// <summary>需要側対抗のうち代替技術の効き（残りが備蓄の効き＝合算1.0想定）。</summary>
        public readonly float substitutionWeight;

        public ResourceCartelParams(float criticalityWeight, float cutLeverage, float defectionPerMember,
            float weaponizationWeight, float substitutionWeight)
        {
            this.criticalityWeight = Mathf.Clamp01(criticalityWeight);
            this.cutLeverage = Mathf.Max(0f, cutLeverage);
            this.defectionPerMember = Mathf.Max(0f, defectionPerMember);
            this.weaponizationWeight = Mathf.Clamp01(weaponizationWeight);
            this.substitutionWeight = Mathf.Clamp01(substitutionWeight);
        }

        /// <summary>既定：重要度の効き0.5・減産てこ1.5・1加盟0.1崩し・武器化1.0・代替の効き0.6。</summary>
        public static ResourceCartelParams Default
            => new ResourceCartelParams(DefaultCriticalityWeight, DefaultCutLeverage, DefaultDefectionPerMember,
                DefaultWeaponizationWeight, DefaultSubstitutionWeight);

        public const float DefaultCriticalityWeight = 0.5f;
        public const float DefaultCutLeverage = 1.5f;
        public const float DefaultDefectionPerMember = 0.1f;
        public const float DefaultWeaponizationWeight = 1.0f;
        public const float DefaultSubstitutionWeight = 0.6f;
    }

    /// <summary>
    /// 資源カルテル（戦略資源＝レアメタル・燃料の供給支配と価格操作）の純ロジック（唯一の窓口）。
    /// カルテルの市場シェアと資源の重要度で市場支配力を持ち、減産で価格をつり上げる。だが高価格は加盟国の
    /// <b>抜け駆け（裏切り）</b>を誘い、加盟が多いほど結束は崩れやすい（OPEC のジレンマ）。市場支配は禁輸＝
    /// <b>資源の武器化</b>に転用でき、需要側は<b>備蓄・代替技術</b>でカルテルの効力を削ぐ。
    ///
    /// <b>分担</b>：
    /// - <see cref="StrategicResourceRules"/>（戦略資源の産出・偏在 #178）とは別＝こちらは<b>供給支配と価格操作の力学</b>。
    /// - <see cref="StrategicResourceStockpile"/> 等の備蓄ストアは触らない（plain 引数のみ・盤面非依存）。
    /// 符号付き出力は-1..1にクランプ。引数は全てクランプ。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class ResourceCartelRules
    {
        /// <summary>既定パラメータで市場支配力を返す。</summary>
        public static float MarketControl(float cartelShare, float resourceCriticality)
            => MarketControl(cartelShare, resourceCriticality, ResourceCartelParams.Default);

        /// <summary>
        /// 市場支配力(0..1)。カルテルの市場シェア(share)に資源の重要度(criticality)を掛けて増幅する。
        /// 重要度が低い資源は支配しても旨味が薄く（criticalityWeight ぶん割り引く）、不可欠な資源ほど支配が効く。
        /// </summary>
        public static float MarketControl(float cartelShare, float resourceCriticality, ResourceCartelParams p)
        {
            float share = Mathf.Clamp01(cartelShare);
            float criticality = Mathf.Clamp01(resourceCriticality);
            // 重要度の効き：criticality=0 で (1-weight) 倍、=1 で 1.0 倍。
            float critFactor = (1f - p.criticalityWeight) + p.criticalityWeight * criticality;
            return Mathf.Clamp01(share * critFactor);
        }

        /// <summary>既定パラメータで価格つり上げを返す。</summary>
        public static float PriceManipulation(float marketControl, float productionCut)
            => PriceManipulation(marketControl, productionCut, ResourceCartelParams.Default);

        /// <summary>
        /// 価格つり上げ(0..1)。市場支配(control)があるほど・減産(productionCut)するほど価格が跳ね上がる。
        /// 支配がなければ減産しても無視されて効かない（積）。cutLeverage で減産のてこを効かせる。
        /// </summary>
        public static float PriceManipulation(float marketControl, float productionCut, ResourceCartelParams p)
        {
            float control = Mathf.Clamp01(marketControl);
            float cut = Mathf.Clamp01(productionCut);
            return Mathf.Clamp01(control * cut * p.cutLeverage);
        }

        /// <summary>既定パラメータで抜け駆けの誘惑を返す。</summary>
        public static float CheatingIncentive(float priceManipulation, float individualQuota)
            => CheatingIncentive(priceManipulation, individualQuota, ResourceCartelParams.Default);

        /// <summary>
        /// 抜け駆けの誘惑(0..1)。価格が高い(priceManipulation)ほど・自分の割当(quota)が小さいほど、こっそり多く売りたい。
        /// 割当が小さい加盟ほど高値で増産したい誘惑が大きい＝(1-quota) を掛ける。割当満杯(quota=1)なら誘惑ゼロ。
        /// </summary>
        public static float CheatingIncentive(float priceManipulation, float individualQuota, ResourceCartelParams p)
        {
            float price = Mathf.Clamp01(priceManipulation);
            float quota = Mathf.Clamp01(individualQuota);
            return Mathf.Clamp01(price * (1f - quota));
        }

        /// <summary>既定パラメータでカルテルの結束を返す。</summary>
        public static float CartelCohesion(int memberCount, float sharedInterest, float enforcementPower)
            => CartelCohesion(memberCount, sharedInterest, enforcementPower, ResourceCartelParams.Default);

        /// <summary>
        /// カルテルの結束(0..1)。共通利益(sharedInterest)と統制力(enforcementPower)が高いほど固いが、
        /// 加盟数(memberCount)が多いほど崩れやすい（合意形成と監視のコスト＝defectionPerMember×(members-1) ぶん減衰）。
        /// 加盟1で減衰なし・大所帯ほど結束は脆い。
        /// </summary>
        public static float CartelCohesion(int memberCount, float sharedInterest, float enforcementPower, ResourceCartelParams p)
        {
            int members = memberCount < 1 ? 1 : memberCount;
            float shared = Mathf.Clamp01(sharedInterest);
            float enforcement = Mathf.Clamp01(enforcementPower);
            // 加盟が多いほど結束が崩れる：members=1 で1.0、増えるほど0へ。
            float sizeFactor = Mathf.Clamp01(1f - p.defectionPerMember * (members - 1));
            return Mathf.Clamp01(shared * enforcement * sizeFactor);
        }

        /// <summary>既定パラメータで裏切りリスクを返す。</summary>
        public static float CheatingRisk(float cheatingIncentive, float cartelCohesion)
            => CheatingRisk(cheatingIncentive, cartelCohesion, ResourceCartelParams.Default);

        /// <summary>
        /// 裏切りリスク(0..1)。抜け駆けの誘惑(incentive)が高く・結束(cohesion)が弱いほど、加盟国が割当を破る。
        /// 結束が完璧(cohesion=1)なら誘惑があってもリスクゼロ＝(1-cohesion) を掛ける。
        /// </summary>
        public static float CheatingRisk(float cheatingIncentive, float cartelCohesion, ResourceCartelParams p)
        {
            float incentive = Mathf.Clamp01(cheatingIncentive);
            float cohesion = Mathf.Clamp01(cartelCohesion);
            return Mathf.Clamp01(incentive * (1f - cohesion));
        }

        /// <summary>既定パラメータで資源の武器化を返す。</summary>
        public static float ResourceWeaponization(float marketControl, float politicalWill)
            => ResourceWeaponization(marketControl, politicalWill, ResourceCartelParams.Default);

        /// <summary>
        /// 資源の武器化＝禁輸の威力(0..1)。市場支配(control)があり・禁輸を断行する政治意志(politicalWill)があるほど強い。
        /// 支配なき禁輸は迂回されて無力・意志なき支配は禁輸を使えない（積）。weaponizationWeight で全体の効きを調整。
        /// </summary>
        public static float ResourceWeaponization(float marketControl, float politicalWill, ResourceCartelParams p)
        {
            float control = Mathf.Clamp01(marketControl);
            float will = Mathf.Clamp01(politicalWill);
            return Mathf.Clamp01(control * will * p.weaponizationWeight);
        }

        /// <summary>既定パラメータで需要側の対抗を返す。</summary>
        public static float DemandResponse(float buyerStockpiles, float substitutionTech)
            => DemandResponse(buyerStockpiles, substitutionTech, ResourceCartelParams.Default);

        /// <summary>
        /// 需要側の対抗(0..1)。買い手の備蓄(buyerStockpiles)で時間を稼ぎ、代替技術(substitutionTech)で依存を断つ。
        /// substitutionWeight ぶん代替・残りを備蓄の加重平均＝どちらか一方でも対抗できる（カルテルの効力を削ぐ）。
        /// </summary>
        public static float DemandResponse(float buyerStockpiles, float substitutionTech, ResourceCartelParams p)
        {
            float stockpiles = Mathf.Clamp01(buyerStockpiles);
            float substitution = Mathf.Clamp01(substitutionTech);
            float w = p.substitutionWeight;
            return Mathf.Clamp01(substitution * w + stockpiles * (1f - w));
        }

        /// <summary>
        /// カルテルの実効力(-1..1)。市場支配(control)から裏切りリスク(cheatingRisk)と需要側の対抗(demandResponse)を差し引く。
        /// 正＝価格・供給を握れる／負＝裏切りと需要対抗に押し負け、もはやカルテルとして機能しない。Params 不要（純粋な差分）。
        /// </summary>
        public static float CartelPower(float marketControl, float cheatingRisk, float demandResponse)
        {
            float control = Mathf.Clamp01(marketControl);
            float risk = Mathf.Clamp01(cheatingRisk);
            float demand = Mathf.Clamp01(demandResponse);
            return Mathf.Clamp(control - risk - demand, -1f, 1f);
        }
    }
}
