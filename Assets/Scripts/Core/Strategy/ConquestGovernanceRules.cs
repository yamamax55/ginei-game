using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 征服地統治の三様（MKV-1 #1139・マキャヴェッリ『君主論』）。新たに獲得した領土を保持する三つの方法：
    /// 駆逐＝旧支配層を根絶やしにする／植民＝自ら移り住む・植民する／傀儡＝従来の法で自治させ傀儡を立てる。
    /// </summary>
    public enum ConquestStrategy { 駆逐, 植民, 傀儡 }

    /// <summary>
    /// 征服地統治の調整値（MKV-1 #1139）。各戦略の統合速度・裏切りリスク・初期コスト・恨みの重みを束ねる。
    /// </summary>
    public readonly struct ConquestGovernanceParams
    {
        // --- 統合速度（基準速度。文化的距離で割引く） ---
        /// <summary>駆逐の基準統合速度（抵抗を消すが恨みで遅れる）。</summary>
        public readonly float purgeIntegrationBase;
        /// <summary>植民の基準統合速度（確実だが緩やか）。</summary>
        public readonly float colonizeIntegrationBase;
        /// <summary>傀儡の基準統合速度（速いが上辺だけ）。</summary>
        public readonly float puppetIntegrationBase;

        // --- 裏切りリスク（基準値。現地勢力・恨みで増える） ---
        /// <summary>駆逐の基礎裏切りリスク（残党の恨み）。</summary>
        public readonly float purgeBetrayalBase;
        /// <summary>植民の基礎裏切りリスク（最も低い）。</summary>
        public readonly float colonizeBetrayalBase;
        /// <summary>傀儡の基礎裏切りリスク（現地勢力が残る＝最も高い）。</summary>
        public readonly float puppetBetrayalBase;

        // --- 初期コスト（領土規模に比例） ---
        /// <summary>駆逐の初期コスト係数（虐殺の汚点＋兵力）。</summary>
        public readonly float purgeCost;
        /// <summary>植民の初期コスト係数（入植者の供給）。</summary>
        public readonly float colonizeCost;
        /// <summary>傀儡の初期コスト係数（最も安い）。</summary>
        public readonly float puppetCost;

        // --- 恨み（残虐度に比例） ---
        /// <summary>駆逐の恨み係数（最大）。</summary>
        public readonly float purgeResentment;
        /// <summary>植民の恨み係数。</summary>
        public readonly float colonizeResentment;
        /// <summary>傀儡の恨み係数（最も低い＝従来の法で自治）。</summary>
        public readonly float puppetResentment;

        /// <summary>長期安定が統合速度へ寄る速さ（/戦略秒）。</summary>
        public readonly float stabilitySpeed;
        /// <summary>傀儡忠誠が利益と現地勢力の均衡へ寄る速さ（/戦略秒）。</summary>
        public readonly float loyaltySpeed;

        public ConquestGovernanceParams(
            float purgeIntegrationBase, float colonizeIntegrationBase, float puppetIntegrationBase,
            float purgeBetrayalBase, float colonizeBetrayalBase, float puppetBetrayalBase,
            float purgeCost, float colonizeCost, float puppetCost,
            float purgeResentment, float colonizeResentment, float puppetResentment,
            float stabilitySpeed, float loyaltySpeed)
        {
            this.purgeIntegrationBase = Mathf.Max(0f, purgeIntegrationBase);
            this.colonizeIntegrationBase = Mathf.Max(0f, colonizeIntegrationBase);
            this.puppetIntegrationBase = Mathf.Max(0f, puppetIntegrationBase);
            this.purgeBetrayalBase = Mathf.Clamp01(purgeBetrayalBase);
            this.colonizeBetrayalBase = Mathf.Clamp01(colonizeBetrayalBase);
            this.puppetBetrayalBase = Mathf.Clamp01(puppetBetrayalBase);
            this.purgeCost = Mathf.Max(0f, purgeCost);
            this.colonizeCost = Mathf.Max(0f, colonizeCost);
            this.puppetCost = Mathf.Max(0f, puppetCost);
            this.purgeResentment = Mathf.Clamp01(purgeResentment);
            this.colonizeResentment = Mathf.Clamp01(colonizeResentment);
            this.puppetResentment = Mathf.Clamp01(puppetResentment);
            this.stabilitySpeed = Mathf.Max(0f, stabilitySpeed);
            this.loyaltySpeed = Mathf.Max(0f, loyaltySpeed);
        }

        /// <summary>
        /// 既定：駆逐＝統合0.05・裏切り0.30・コスト1.0・恨み0.9／植民＝統合0.04・裏切り0.08・コスト0.8・恨み0.4／
        /// 傀儡＝統合0.07・裏切り0.40・コスト0.3・恨み0.2。安定速度6・忠誠速度4。
        /// 三様のトレードオフ（傀儡＝速いが脆い／植民＝確実だが高コスト／駆逐＝抵抗を消すが恨み）。
        /// </summary>
        public static ConquestGovernanceParams Default => new ConquestGovernanceParams(
            0.05f, 0.04f, 0.07f,
            0.30f, 0.08f, 0.40f,
            1.0f, 0.8f, 0.3f,
            0.9f, 0.4f, 0.2f,
            6f, 4f);
    }

    /// <summary>
    /// 征服地統治の数値解決（MKV-1 #1139・マキャヴェッリ『君主論』・純ロジック test-first）。
    /// 新たに獲得した領土を保持する三様＝<see cref="ConquestStrategy"/>{駆逐/植民/傀儡} の選択をモデル化する：
    /// 駆逐＝旧支配層を根絶やしにして抵抗を消す（速いが恨み・汚点）／植民＝自ら移り住んで地歩を固める（確実だが高コスト）／
    /// 傀儡＝従来の法で自治させ傀儡を立てる（安く速いが現地勢力が残り脆い）。各戦略は統合速度と裏切りリスクが
    /// トレードオフで、状況（文化的距離・自勢力／現地勢力）により最適が変わる（マキャヴェッリの状況判断）。
    /// 分担：<see cref="GovernanceRules"/>（占領後の安定度の時間収束）＝統治の継続運用、本クラス＝<b>征服直後の統治戦略の選択</b>。
    /// <see cref="ColonizationRules"/>（無人惑星への新規入植）とは別＝住民の居る征服地の保持。
    /// 恨みの汚点は AtrocityRules（駆逐の残虐行為）と連動、FearVsHatred は同 EPIC MKV の姉妹（恐怖と憎悪の使い分け）。
    /// 全入力クランプ・乱数なし決定論。
    /// </summary>
    public static class ConquestGovernanceRules
    {
        /// <summary>
        /// 戦略ごとの統合速度（0..基準）。文化的距離が大きいほど統合は遅れる。
        /// 駆逐＝抵抗を消すが恨みで遅れる／植民＝確実だが緩やか／傀儡＝速いが上辺だけ。
        /// </summary>
        public static float IntegrationSpeed(ConquestStrategy strategy, float culturalDistance)
            => IntegrationSpeed(strategy, culturalDistance, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="IntegrationSpeed(ConquestStrategy,float)"/>
        public static float IntegrationSpeed(ConquestStrategy strategy, float culturalDistance, ConquestGovernanceParams p)
        {
            float cd = Mathf.Clamp01(culturalDistance);
            float baseSpeed;
            switch (strategy)
            {
                case ConquestStrategy.駆逐:  baseSpeed = p.purgeIntegrationBase; break;
                case ConquestStrategy.植民:  baseSpeed = p.colonizeIntegrationBase; break;
                default:                     baseSpeed = p.puppetIntegrationBase; break; // 傀儡
            }
            // 文化的距離で割引（遠いほど統合が遅れる。最大で半減）。
            return baseSpeed * (1f - 0.5f * cd);
        }

        /// <summary>
        /// 戦略ごとの裏切りリスク（0..1）。傀儡は現地勢力が残るので高い・駆逐は残党の恨み・植民は低い。
        /// 現地勢力(localPower)と恨み(resentment)で上振れする。
        /// </summary>
        public static float BetrayalRisk(ConquestStrategy strategy, float localPower, float resentment)
            => BetrayalRisk(strategy, localPower, resentment, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="BetrayalRisk(ConquestStrategy,float,float)"/>
        public static float BetrayalRisk(ConquestStrategy strategy, float localPower, float resentment, ConquestGovernanceParams p)
        {
            float lp = Mathf.Clamp01(localPower);
            float r = Mathf.Clamp01(resentment);
            float baseRisk;
            float localWeight; // 現地勢力の効き（傀儡が最大＝現地が残る）
            switch (strategy)
            {
                case ConquestStrategy.駆逐:  baseRisk = p.purgeBetrayalBase;     localWeight = 0.2f; break; // 残党は弱い
                case ConquestStrategy.植民:  baseRisk = p.colonizeBetrayalBase;  localWeight = 0.3f; break;
                default:                     baseRisk = p.puppetBetrayalBase;    localWeight = 0.6f; break; // 傀儡＝現地勢力が決定的
            }
            // 基礎＋現地勢力＋恨み（恨みは一律 0.3 の重みで効く）。
            float risk = baseRisk + localWeight * lp + 0.3f * r;
            return Mathf.Clamp01(risk);
        }

        /// <summary>
        /// 戦略ごとの初期コスト（領土規模に比例）。駆逐＝虐殺の汚点＋兵力・植民＝入植者・傀儡＝安い。
        /// </summary>
        public static float UpfrontCost(ConquestStrategy strategy, float territorySize)
            => UpfrontCost(strategy, territorySize, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="UpfrontCost(ConquestStrategy,float)"/>
        public static float UpfrontCost(ConquestStrategy strategy, float territorySize, ConquestGovernanceParams p)
        {
            float ts = Mathf.Clamp01(territorySize);
            float coef;
            switch (strategy)
            {
                case ConquestStrategy.駆逐:  coef = p.purgeCost; break;
                case ConquestStrategy.植民:  coef = p.colonizeCost; break;
                default:                     coef = p.puppetCost; break; // 傀儡＝最も安い
            }
            return coef * ts;
        }

        /// <summary>
        /// 現地の恨み（0..1）。残虐度(brutality)に比例。駆逐が最大＝AtrocityRules（虐殺の汚点）連動・傀儡は低い。
        /// </summary>
        public static float Resentment(ConquestStrategy strategy, float brutality)
            => Resentment(strategy, brutality, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="Resentment(ConquestStrategy,float)"/>
        public static float Resentment(ConquestStrategy strategy, float brutality, ConquestGovernanceParams p)
        {
            float b = Mathf.Clamp01(brutality);
            float coef;
            switch (strategy)
            {
                case ConquestStrategy.駆逐:  coef = p.purgeResentment; break;
                case ConquestStrategy.植民:  coef = p.colonizeResentment; break;
                default:                     coef = p.puppetResentment; break; // 傀儡＝最も低い
            }
            return Mathf.Clamp01(coef * b);
        }

        /// <summary>
        /// 長期安定（0..1）を1tick更新する純関数。統合が進み裏切りリスクが下がるほど安定へ寄る。
        /// 目標＝統合速度の正規化（速いほど統合が進む前提）×(1−裏切りリスク)。dt 比例で目標へ収束。
        /// </summary>
        public static float LongTermStability(float current, float integrationSpeed, float betrayalRisk, float dt)
            => LongTermStability(current, integrationSpeed, betrayalRisk, dt, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="LongTermStability(float,float,float,float)"/>
        public static float LongTermStability(float current, float integrationSpeed, float betrayalRisk, float dt, ConquestGovernanceParams p)
        {
            float cur = Mathf.Clamp01(current);
            if (dt <= 0f) return cur;
            // 統合速度を 0..1 へ正規化（基準=植民の確実速度 0.05 を満点目安）。
            float integ = Mathf.Clamp01(integrationSpeed / 0.05f);
            float target = integ * (1f - Mathf.Clamp01(betrayalRisk));
            return Mathf.MoveTowards(cur, Mathf.Clamp01(target), p.stabilitySpeed * dt * 0.1f);
        }

        /// <summary>
        /// 傀儡政権の忠誠（0..1）を1tick更新する純関数。利益(benefit)で釣るが、現地勢力(localPower)が強いと離反へ。
        /// 目標＝利益−現地勢力の自立志向。利益が現地勢力を上回る間だけ忠誠を保てる（マキャヴェッリの傀儡の脆さ）。
        /// </summary>
        public static float PuppetLoyalty(float current, float localPower, float benefit, float dt)
            => PuppetLoyalty(current, localPower, benefit, dt, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="PuppetLoyalty(float,float,float,float)"/>
        public static float PuppetLoyalty(float current, float localPower, float benefit, float dt, ConquestGovernanceParams p)
        {
            float cur = Mathf.Clamp01(current);
            if (dt <= 0f) return cur;
            float lp = Mathf.Clamp01(localPower);
            float bf = Mathf.Clamp01(benefit);
            // 利益で釣り、現地勢力（自立志向）が引き下げる。
            float target = Mathf.Clamp01(bf - 0.6f * lp + 0.3f);
            return Mathf.MoveTowards(cur, target, p.loyaltySpeed * dt * 0.1f);
        }

        /// <summary>
        /// 状況に応じた最適戦略の推奨（マキャヴェッリの状況判断・MKV-1 #1139）。
        /// 文化的距離が遠い・現地勢力が強いほど傀儡は危険、自勢力が十分なら植民で確実に固める、
        /// 自勢力が乏しく抵抗が強いなら駆逐で根を断つ。各戦略の「裏切りリスクを抑えた実効統合度」で比較する。
        /// </summary>
        public static ConquestStrategy OptimalStrategy(float culturalDistance, float ownStrength, float localPower)
            => OptimalStrategy(culturalDistance, ownStrength, localPower, ConquestGovernanceParams.Default);

        /// <inheritdoc cref="OptimalStrategy(float,float,float)"/>
        public static ConquestStrategy OptimalStrategy(float culturalDistance, float ownStrength, float localPower, ConquestGovernanceParams p)
        {
            float cd = Mathf.Clamp01(culturalDistance);
            float os = Mathf.Clamp01(ownStrength);
            float lp = Mathf.Clamp01(localPower);

            // 各戦略の実効スコア＝統合速度×(1−裏切りリスク)。恨みは戦略別の既定残虐度から導く。
            float purgeRes = Resentment(ConquestStrategy.駆逐, 1f, p);   // 駆逐＝高残虐度想定
            float colRes   = Resentment(ConquestStrategy.植民, 0.3f, p);
            float pupRes   = Resentment(ConquestStrategy.傀儡, 0.2f, p);

            float purge = IntegrationSpeed(ConquestStrategy.駆逐, cd, p)
                          * (1f - BetrayalRisk(ConquestStrategy.駆逐, lp, purgeRes, p));
            float colonize = IntegrationSpeed(ConquestStrategy.植民, cd, p)
                          * (1f - BetrayalRisk(ConquestStrategy.植民, lp, colRes, p));
            float puppet = IntegrationSpeed(ConquestStrategy.傀儡, cd, p)
                          * (1f - BetrayalRisk(ConquestStrategy.傀儡, lp, pupRes, p));

            // 自勢力が乏しいと植民の入植者を割けない＝植民スコアを実勢力で割引く。
            colonize *= 0.4f + 0.6f * os;

            // 最大スコアの戦略を選ぶ（同点は傀儡＞植民＞駆逐の安価順）。
            if (purge >= colonize && purge >= puppet) return ConquestStrategy.駆逐;
            if (colonize >= puppet) return ConquestStrategy.植民;
            return ConquestStrategy.傀儡;
        }

        /// <summary>征服地が完全に統合されたか（長期安定がしきい値以上）。</summary>
        public static bool IsConsolidated(float longTermStability, float threshold)
            => Mathf.Clamp01(longTermStability) >= Mathf.Clamp01(threshold);
    }
}
