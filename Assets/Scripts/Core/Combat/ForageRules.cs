using UnityEngine;

namespace Ginei
{
    /// <summary>現地調達（糧を敵に因る）の調整係数。</summary>
    public readonly struct ForageParams
    {
        /// <summary>徴発で得られる物資の規模（最も豊かで敵対ゼロ・全力徴発のときの物資量）。</summary>
        public readonly float yieldScale;
        /// <summary>住民の敵対が徴発を妨げる強さ（敵地では隠匿・抵抗で集まりにくい。1で完全に殺ぐ）。</summary>
        public readonly float hostilityPenalty;
        /// <summary>徴発が生む恨みの最大量（最も敵対的な土地で全力徴発したとき）。</summary>
        public readonly float resentmentScale;
        /// <summary>友好的な土地でも徴発が残す最低限の恨み（取り立てそのものが反感を生む）。</summary>
        public readonly float baseResentment;
        /// <summary>絞り続けたとき星系が涸れる速さ（焦土化＝同じ土地は次第に痩せる）。</summary>
        public readonly float depletionRate;
        /// <summary>現地調達依存による機動の最大加速（輜重を引かない軍の速さの上限）。</summary>
        public readonly float mobilityBonusScale;
        /// <summary>豊かさ1の星系が養える兵力（大軍はこれを超えると1星系を食い潰す）。</summary>
        public readonly float sustainPerRichness;

        public ForageParams(float yieldScale, float hostilityPenalty, float resentmentScale,
            float baseResentment, float depletionRate, float mobilityBonusScale, float sustainPerRichness)
        {
            this.yieldScale = Mathf.Max(0f, yieldScale);
            this.hostilityPenalty = Mathf.Clamp01(hostilityPenalty);
            this.resentmentScale = Mathf.Max(0f, resentmentScale);
            this.baseResentment = Mathf.Clamp01(baseResentment);
            this.depletionRate = Mathf.Max(0f, depletionRate);
            this.mobilityBonusScale = Mathf.Max(0f, mobilityBonusScale);
            this.sustainPerRichness = Mathf.Max(0f, sustainPerRichness);
        }

        /// <summary>既定＝徴発規模100・敵対妨害0.8・恨み0.6・基礎恨み0.1・枯渇速度0.2・機動加速0.3・養兵量500。</summary>
        public static ForageParams Default => new ForageParams(100f, 0.8f, 0.6f, 0.1f, 0.2f, 0.3f, 500f);
    }

    /// <summary>
    /// 現地調達（糧を敵に因る）の純ロジック（#1128）。後方からの補給線に頼らず、進軍先の占領地・通過星系から
    /// 物資を徴発する＝補給線が伸びすぎる前線の自律調達。「糧を敵に因れば輜重は軽く軍は速いが、取り立てが
    /// 占領地を敵地に変える」を式に出す：①豊かで敵対の低い星系ほど多く集まり（<see cref="ForageYield"/>）、
    /// 現地調達ぶん後方輸送が要らなくなり（<see cref="SupplyLineRelief"/>＝<see cref="SupplyRules"/> への加算）、
    /// 軍は速くなる（<see cref="MobilitySpeedBonus"/>）が、②取り立てるほど住民の恨みが募り（<see cref="PopulationResentment"/>）、
    /// 同じ星系を絞り続ければ涸れる（<see cref="DepletionTick"/>）。大軍は1星系を食い潰す（<see cref="SustainabilityLimit"/>）。
    /// 後方からの補給線そのものは <see cref="SupplyRules"/>（L-2）が、徴発が生む占領不満の収束は
    /// <see cref="GovernanceRules"/>（占領統合・安定度）が扱う＝こちらは前線の自律調達の量と代償のみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ForageRules
    {
        /// <summary>
        /// 徴発で得られる物資＝徴発規模×星系の豊かさ×徴発努力×（1−敵対度×敵対妨害）。
        /// 豊かな星系ほど・敵対が低いほど・努力するほど集まる。敵地では隠匿と抵抗で痩せる。
        /// </summary>
        public static float ForageYield(float systemRichness, float foragingEffort, float hostilityLevel, ForageParams p)
        {
            float richness = Mathf.Clamp01(systemRichness);
            float effort = Mathf.Clamp01(foragingEffort);
            float hostility = Mathf.Clamp01(hostilityLevel);
            float cooperation = 1f - hostility * p.hostilityPenalty; // 敵対が徴発を妨げる
            return p.yieldScale * richness * effort * cooperation;
        }

        public static float ForageYield(float systemRichness, float foragingEffort, float hostilityLevel)
            => ForageYield(systemRichness, foragingEffort, hostilityLevel, ForageParams.Default);

        /// <summary>
        /// 補給線の負担軽減＝現地調達ぶん後方からの輸送が要らなくなる量（0..demand）。
        /// 前線需要 demand のうち徴発 forageYield で賄えた分を返す（需要を超えた徴発は余剰＝輸送軽減には効かない）。
        /// <see cref="SupplyRules.TickFront"/> の resupplyRate に加算する想定＝補給線が短くて済む。
        /// </summary>
        public static float SupplyLineRelief(float forageYield, float demand)
        {
            float d = Mathf.Max(0f, demand);
            return Mathf.Clamp(Mathf.Max(0f, forageYield), 0f, d);
        }

        /// <summary>
        /// 徴発が住民に生む恨み（0..resentmentScale）＝徴発努力×（基礎恨み＋（1−基礎恨み）×敵対度）。
        /// 取り立てそのものが基礎恨みを生み、敵対的な土地ほど深い＝<b>略奪は次の反乱を育てる</b>。
        /// 占領地の不満の実際の収束（統合・安定度への波及）は <see cref="GovernanceRules"/> が扱う。
        /// </summary>
        public static float PopulationResentment(float foragingEffort, float hostilityLevel, ForageParams p)
        {
            float effort = Mathf.Clamp01(foragingEffort);
            float hostility = Mathf.Clamp01(hostilityLevel);
            float severity = p.baseResentment + (1f - p.baseResentment) * hostility;
            return effort * severity * p.resentmentScale;
        }

        public static float PopulationResentment(float foragingEffort, float hostilityLevel)
            => PopulationResentment(foragingEffort, hostilityLevel, ForageParams.Default);

        /// <summary>
        /// 星系の枯渇（豊かさの低下量）＝豊かさ×徴発強度×枯渇速度×dt。
        /// 同じ星系を絞り続ければ涸れる＝焦土化。残った豊かさを返す（下限0）。
        /// </summary>
        public static float DepletionTick(float systemRichness, float foragingIntensity, float dt, ForageParams p)
        {
            float richness = Mathf.Clamp01(systemRichness);
            if (dt <= 0f) return richness;
            float intensity = Mathf.Clamp01(foragingIntensity);
            float drain = richness * intensity * p.depletionRate * dt;
            return Mathf.Max(0f, richness - drain);
        }

        public static float DepletionTick(float systemRichness, float foragingIntensity, float dt)
            => DepletionTick(systemRichness, foragingIntensity, dt, ForageParams.Default);

        /// <summary>
        /// 機動の速さの倍率＝1＋現地調達依存度×機動加速。重い輜重を引かない軍は速い（糧を敵に因る軍の利点）。
        /// 依存度0で1.0倍（補給線頼みで鈍重）、依存度1で 1＋mobilityBonusScale 倍。実効値パターン（基準速度は非破壊）。
        /// </summary>
        public static float MobilitySpeedBonus(float forageReliance, ForageParams p)
        {
            float reliance = Mathf.Clamp01(forageReliance);
            return 1f + reliance * p.mobilityBonusScale;
        }

        public static float MobilitySpeedBonus(float forageReliance)
            => MobilitySpeedBonus(forageReliance, ForageParams.Default);

        /// <summary>
        /// 持続可能な徴発の上限＝1星系が養える兵力（豊かさ×養兵量）。
        /// 軍規模 armySize がこれを超えると現地調達だけでは足りない＝補給線なき進撃の限界。
        /// 充足率（0..1）を <see cref="SustainabilityRatio"/> で見る。
        /// </summary>
        public static float SustainabilityLimit(float systemRichness, ForageParams p)
        {
            return Mathf.Clamp01(systemRichness) * p.sustainPerRichness;
        }

        public static float SustainabilityLimit(float systemRichness)
            => SustainabilityLimit(systemRichness, ForageParams.Default);

        /// <summary>
        /// 現地調達の充足率（0..1）＝養える兵力÷軍規模。1なら現地調達のみで賄える、
        /// 1未満なら不足分は後方補給線に頼るしかない（大軍は1星系を食い潰す）。
        /// </summary>
        public static float SustainabilityRatio(float systemRichness, float armySize, ForageParams p)
        {
            float army = Mathf.Max(0f, armySize);
            if (army <= 0f) return 1f; // 養うべき軍がなければ常に充足
            float limit = SustainabilityLimit(systemRichness, p);
            return Mathf.Clamp01(limit / army);
        }

        public static float SustainabilityRatio(float systemRichness, float armySize)
            => SustainabilityRatio(systemRichness, armySize, ForageParams.Default);
    }
}
