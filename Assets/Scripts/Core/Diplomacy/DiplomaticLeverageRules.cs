using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 外交的てこ作用（レバレッジ）の調整係数。
    /// 軍事・経済・連合・弱みの利用・脅しの信憑性・行使コスト・引き出せる譲歩の各重みを決める。
    /// </summary>
    public readonly struct DiplomaticLeverageParams
    {
        /// <summary>軍事優位の寄与重み（軍事的てこの主軸）。</summary>
        public readonly float militaryWeight;
        /// <summary>近接の脅威の寄与重み（隣接＝脅威が現実味を帯びる）。</summary>
        public readonly float proximityWeight;
        /// <summary>経済てこの貿易依存の寄与重み（相手がこちらに依存するほど効く）。</summary>
        public readonly float dependencyWeight;
        /// <summary>連合てこの結束寄与重み（数だけでなく結束が要る）。</summary>
        public readonly float commitmentWeight;
        /// <summary>総合てこに占める軍事てこの比重（残りを経済・連合で分け合う）。</summary>
        public readonly float militaryShare;
        /// <summary>総合てこに占める経済てこの比重。</summary>
        public readonly float economicShare;
        /// <summary>てこ行使のエスカレーション・コスト係数（かけ過ぎると自分も傷つく）。</summary>
        public readonly float exertionCostWeight;

        public DiplomaticLeverageParams(float militaryWeight, float proximityWeight,
            float dependencyWeight, float commitmentWeight, float militaryShare,
            float economicShare, float exertionCostWeight)
        {
            this.militaryWeight = Mathf.Clamp01(militaryWeight);
            this.proximityWeight = Mathf.Clamp01(proximityWeight);
            this.dependencyWeight = Mathf.Clamp01(dependencyWeight);
            this.commitmentWeight = Mathf.Clamp01(commitmentWeight);
            this.militaryShare = Mathf.Clamp01(militaryShare);
            this.economicShare = Mathf.Clamp01(economicShare);
            this.exertionCostWeight = Mathf.Clamp01(exertionCostWeight);
        }

        /// <summary>既定係数（軍事0.6・近接0.4・依存0.7・結束0.6・軍事比重0.5・経済比重0.3〔連合0.2〕・行使コスト0.5）。</summary>
        public static DiplomaticLeverageParams Default =>
            new DiplomaticLeverageParams(0.6f, 0.4f, 0.7f, 0.6f, 0.5f, 0.3f, 0.5f);
    }

    /// <summary>
    /// 外交的てこ＝交渉カードと圧力の純ロジック。
    /// <b>外交交渉は力の非対称＝てこ（leverage）で動く</b>：軍事的威圧・経済的圧力・同盟による包囲・相手の弱みの利用が
    /// 圧力を生み、信頼性のある脅しが相手を譲歩させる。ただし<b>てこの行使にはコストが伴い</b>（かけ過ぎは反発・
    /// エスカレーションで自分も傷つく）、<b>相手の決意</b>がてこに抗う。
    /// <see cref="DeterrenceRules"/>（抑止＝現状維持の脅し）／<see cref="SanctionsRules"/>（制裁の実体）／
    /// <see cref="WarGoalRules"/>（戦争目標・厭戦）とは別＝こちらは<b>交渉での圧力の積み上げと譲歩の引き出し</b>の係数のみ。
    /// 乱数なし決定論・全入力クランプ・符号付き出力は-1..1。純ロジック（test-first）。
    /// </summary>
    public static class DiplomaticLeverageRules
    {
        /// <summary>
        /// 軍事的てこ 0..1＝軍事優位 × (近接の脅威で重み付け)。
        /// 軍事優位があっても遠ければ威圧は薄い＝近接の脅威がてこを現実味あるものにする。
        /// </summary>
        public static float MilitaryLeverage(float militaryAdvantage, float proximityThreat, DiplomaticLeverageParams p)
        {
            float adv = Mathf.Clamp01(militaryAdvantage);
            float prox = Mathf.Clamp01(proximityThreat);
            // 軍事優位を主軸に、近接の脅威で底上げ（近いほどてこが効く）。
            float reach = Mathf.Lerp(1f - p.proximityWeight, 1f, prox);
            return Mathf.Clamp01(p.militaryWeight * adv * reach);
        }

        /// <summary>既定係数版。</summary>
        public static float MilitaryLeverage(float militaryAdvantage, float proximityThreat)
            => MilitaryLeverage(militaryAdvantage, proximityThreat, DiplomaticLeverageParams.Default);

        /// <summary>
        /// 経済的てこ 0..1＝相手の貿易依存 × 制裁力。
        /// 相手がこちらに依存し（依存度大）、こちらに制裁を実行する力（制裁力大）があるほど経済圧力が効く＝
        /// <b>どちらか欠けると経済てこは効かない</b>（積で相乗）。
        /// </summary>
        public static float EconomicLeverage(float tradeDependency, float sanctionCapacity, DiplomaticLeverageParams p)
        {
            float dep = Mathf.Clamp01(tradeDependency);
            float cap = Mathf.Clamp01(sanctionCapacity);
            return Mathf.Clamp01(p.dependencyWeight * dep * cap);
        }

        /// <summary>既定係数版。</summary>
        public static float EconomicLeverage(float tradeDependency, float sanctionCapacity)
            => EconomicLeverage(tradeDependency, sanctionCapacity, DiplomaticLeverageParams.Default);

        /// <summary>
        /// 連合の包囲圧力 0..1＝同盟国数 × 結束。
        /// 数が多くても結束が緩ければ包囲は崩れる＝<b>数 × 結束</b>。同盟国数は逓減（最初の数国が効き、増えるほど限界効用減）。
        /// </summary>
        public static float CoalitionLeverage(int allyCount, float allyCommitment, DiplomaticLeverageParams p)
        {
            int n = Mathf.Max(0, allyCount);
            float commit = Mathf.Clamp01(allyCommitment);
            // 同盟国数を 0..1 へ逓減（4国で約0.67・多いほど飽和）。Sqrt で限界効用減。
            float mass = Mathf.Clamp01(Mathf.Sqrt(n) / (Mathf.Sqrt(n) + 2f));
            float commitFactor = Mathf.Lerp(1f - p.commitmentWeight, 1f, commit);
            return Mathf.Clamp01(mass * commitFactor);
        }

        /// <summary>既定係数版。</summary>
        public static float CoalitionLeverage(int allyCount, float allyCommitment)
            => CoalitionLeverage(allyCount, allyCommitment, DiplomaticLeverageParams.Default);

        /// <summary>
        /// 相手の弱みの利用 0..1＝相手の脆弱性 × 内部分裂。
        /// 弱みがあり（脆弱性大）、内部が割れている（分裂大）ほど付け込める＝<b>弱り目に祟り目</b>（積で相乗）。
        /// </summary>
        public static float ExploitWeakness(float targetVulnerability, float internalDivisions, DiplomaticLeverageParams p)
        {
            float vuln = Mathf.Clamp01(targetVulnerability);
            float div = Mathf.Clamp01(internalDivisions);
            // 脆弱性を主軸に、内部分裂で底上げ（割れているほど付け込める）。
            float split = Mathf.Lerp(0.5f, 1f, div);
            return Mathf.Clamp01(vuln * split);
        }

        /// <summary>既定係数版。</summary>
        public static float ExploitWeakness(float targetVulnerability, float internalDivisions)
            => ExploitWeakness(targetVulnerability, internalDivisions, DiplomaticLeverageParams.Default);

        /// <summary>
        /// 脅しの信憑性 0..1＝能力 × 決意 × 過去の実績。
        /// 実行できる能力・実行する決意・過去に実行した実績の三つが揃ってはじめて脅しは信じられる＝
        /// <b>口先だけの脅しは効かない</b>（積で相乗・どれか欠けるとブラフ扱い）。過去の実績は半分だけ寄与（無実績でも能力＋決意で半信）。
        /// </summary>
        public static float ThreatCredibility(float capability, float resolve, float pastBehavior, DiplomaticLeverageParams p)
        {
            float cap = Mathf.Clamp01(capability);
            float res = Mathf.Clamp01(resolve);
            float past = Mathf.Clamp01(pastBehavior);
            // 過去の実績は信を補強するが、無くても能力×決意で半分は通る。
            float track = Mathf.Lerp(0.5f, 1f, past);
            return Mathf.Clamp01(cap * res * track);
        }

        /// <summary>既定係数版。</summary>
        public static float ThreatCredibility(float capability, float resolve, float pastBehavior)
            => ThreatCredibility(capability, resolve, pastBehavior, DiplomaticLeverageParams.Default);

        /// <summary>
        /// 総合圧力 0..1＝軍事・経済・連合の各てこを比重で統合。
        /// 軍事を主軸に経済・連合で補う（militaryShare/economicShare＋残りが連合）。
        /// </summary>
        public static float TotalLeverage(float militaryLeverage, float economicLeverage, float coalitionLeverage, DiplomaticLeverageParams p)
        {
            float mil = Mathf.Clamp01(militaryLeverage);
            float eco = Mathf.Clamp01(economicLeverage);
            float coal = Mathf.Clamp01(coalitionLeverage);
            float mShare = Mathf.Clamp01(p.militaryShare);
            float eShare = Mathf.Clamp01(p.economicShare);
            // 連合の比重は残り（軍事・経済を引いた分。負にならないよう下限0）。
            float cShare = Mathf.Max(0f, 1f - mShare - eShare);
            return Mathf.Clamp01(mil * mShare + eco * eShare + coal * cShare);
        }

        /// <summary>既定係数版。</summary>
        public static float TotalLeverage(float militaryLeverage, float economicLeverage, float coalitionLeverage)
            => TotalLeverage(militaryLeverage, economicLeverage, coalitionLeverage, DiplomaticLeverageParams.Default);

        /// <summary>
        /// てこの行使コスト 0..1＝てこの行使 × エスカレーション・リスク。
        /// 圧力をかけるほど、そしてエスカレーションのリスクが高いほど自分も傷つく＝
        /// <b>圧力をかけ過ぎると自分も傷つく</b>（過剰なてこは反発・関係悪化・巻き添えを招く）。
        /// </summary>
        public static float ExertionCost(float totalLeverage, float escalationRisk, DiplomaticLeverageParams p)
        {
            float lev = Mathf.Clamp01(totalLeverage);
            float esc = Mathf.Clamp01(escalationRisk);
            // 行使量とエスカレーションの積にコスト係数を掛ける（両方が大きいときに跳ね上がる）。
            return Mathf.Clamp01(p.exertionCostWeight * lev * esc);
        }

        /// <summary>既定係数版。</summary>
        public static float ExertionCost(float totalLeverage, float escalationRisk)
            => ExertionCost(totalLeverage, escalationRisk, DiplomaticLeverageParams.Default);

        /// <summary>
        /// 引き出せる譲歩 0..1＝てこ × 信憑性 ÷ (てこ × 信憑性 + 相手の決意)。
        /// てこが大きく脅しが信じられるほど譲歩を引き出せるが、<b>相手の決意</b>が抵抗する＝
        /// 力の比（圧力 vs 決意）で譲歩が決まる（コンテスト関数・両者ゼロなら0）。
        /// </summary>
        public static float Concession(float totalLeverage, float threatCredibility, float targetResolve, DiplomaticLeverageParams p)
        {
            float lev = Mathf.Clamp01(totalLeverage);
            float cred = Mathf.Clamp01(threatCredibility);
            float resolve = Mathf.Clamp01(targetResolve);
            // 実効的な圧力＝てこ × 信憑性（信じられない脅しは圧力にならない）。
            float pressure = lev * cred;
            float denom = pressure + resolve;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(pressure / denom);
        }

        /// <summary>既定係数版。</summary>
        public static float Concession(float totalLeverage, float threatCredibility, float targetResolve)
            => Concession(totalLeverage, threatCredibility, targetResolve, DiplomaticLeverageParams.Default);
    }
}
