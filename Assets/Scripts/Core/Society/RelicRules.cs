using UnityEngine;

namespace Ginei
{
    /// <summary>遺失技術（地球時代・前文明の遺産発掘）の調整係数。</summary>
    public readonly struct RelicParams
    {
        /// <summary>発掘成功率の上限（最豊の遺跡×全力の考古でこの率。一点物は簡単には出ない）。</summary>
        public readonly float maxExcavationChance;
        /// <summary>格1の遺物の技術ブースト基準値（自前研究1世代ぶんの目安）。</summary>
        public readonly float baseTechValue;
        /// <summary>格が1上がるごとの技術価値の倍率（格で跳ねる＝高位遺物は数世代先）。</summary>
        public readonly float tierGrowth;
        /// <summary>遺物の格の上限（これ以上はクランプ）。</summary>
        public readonly int maxTier;
        /// <summary>解析の基準速度（科学水準1.0での理解進捗/時間）。</summary>
        public readonly float analysisRate;
        /// <summary>解析に必要な最低科学水準（これ以下では理解が一切進まない＝宝の持ち腐れ）。</summary>
        public readonly float minScienceLevel;
        /// <summary>独占の自然漏出速度（諜報ゼロでも漏れる＝独占は時限・<see cref="InnovationDiffusionRules"/> と同型の運命）。</summary>
        public readonly float baseErosionRate;
        /// <summary>外国諜報が漏出に足す重み（spies=1 で自然漏出＋これ）。</summary>
        public readonly float spyErosionWeight;
        /// <summary>公開の名声スケール（技術価値→名声への変換率）。</summary>
        public readonly float prestigeScale;
        /// <summary>暴走リスクの上限（最高位の遺物×理解ゼロの最悪ケース）。</summary>
        public readonly float maxCurseRisk;

        public RelicParams(float maxExcavationChance, float baseTechValue, float tierGrowth, int maxTier,
            float analysisRate, float minScienceLevel, float baseErosionRate, float spyErosionWeight,
            float prestigeScale, float maxCurseRisk)
        {
            this.maxExcavationChance = Mathf.Clamp01(maxExcavationChance);
            this.baseTechValue = Mathf.Max(0f, baseTechValue);
            // 1未満を許すと「高位遺物ほど価値が低い」逆転になるため下限を切る。
            this.tierGrowth = Mathf.Max(1f, tierGrowth);
            this.maxTier = Mathf.Max(1, maxTier);
            this.analysisRate = Mathf.Max(0f, analysisRate);
            this.minScienceLevel = Mathf.Clamp(minScienceLevel, 0f, 0.99f);
            this.baseErosionRate = Mathf.Max(0f, baseErosionRate);
            this.spyErosionWeight = Mathf.Max(0f, spyErosionWeight);
            this.prestigeScale = Mathf.Max(0f, prestigeScale);
            this.maxCurseRisk = Mathf.Clamp01(maxCurseRisk);
        }

        /// <summary>既定＝発掘上限0.6・基準価値0.2・格倍率1.8・格上限5・解析速度0.1・必要科学0.3・自然漏出0.01・諜報重み0.05・名声スケール0.5・暴走上限0.5。</summary>
        public static RelicParams Default
            => new RelicParams(0.6f, 0.2f, 1.8f, 5, 0.1f, 0.3f, 0.01f, 0.05f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 遺失技術の純ロジック。地球時代・前文明の遺産発掘＝一点物の技術ブーストと、独占か公開かの選択。
    /// 発掘（遺跡の豊かさ×考古の努力）で遺物が出るが、解析は<b>自国の科学水準が低いと一切進まない</b>＝
    /// 理解できない遺物は飾り（宝の持ち腐れ）。独占は理解した分だけの優位を生むが、機密は諜報ゼロでも
    /// 必ず漏れる（<see cref="InnovationDiffusionRules"/> と同型の運命＝独占は時限）。公開は人類への貢献として
    /// 名声（正統性・外交ボーナス）を返し、未理解のまま高位遺物を振るえば暴走リスク＝分不相応な力は
    /// 持ち主を焼く。「遺物は鏡＝拾った文明の格を映す」＝同じ遺物でも、解析できる科学・守れる防諜・
    /// 律せる理解が無い文明には利得どころか災いになる。分担＝<see cref="ResearchRules"/> は自前研究
    /// （研究力からの産出）、<see cref="DisclosureRules"/> は物語の開示（秘史の演出）で、ここは
    /// <b>発掘の利得</b>のみを扱う。乱数なし＝判定は外から与える roll で決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RelicRules
    {
        /// <summary>
        /// 発掘成功率（0..1）＝上限×遺跡の豊かさ×考古の努力。掘らなければ（effort=0）何も出ず、
        /// 不毛の地（richness=0）からも出ない。最豊×全力でも上限止まり＝一点物は簡単には出ない。
        /// </summary>
        public static float ExcavationChance(float siteRichness, float archaeologyEffort, RelicParams p)
        {
            float rich = Mathf.Clamp01(siteRichness);
            float effort = Mathf.Clamp01(archaeologyEffort);
            return Mathf.Clamp01(p.maxExcavationChance * rich * effort);
        }

        public static float ExcavationChance(float siteRichness, float archaeologyEffort)
            => ExcavationChance(siteRichness, archaeologyEffort, RelicParams.Default);

        /// <summary>発掘の決定論判定（roll は [0,1) を外から与える＝同じ入力なら同じ結果）。</summary>
        public static bool Excavates(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 遺物の技術価値＝一点物のブースト量（基準値×倍率^(格-1)）。格で指数的に跳ねる＝
        /// 高位遺物は現代技術の数世代先で、1を超えうる（自前研究では届かない領域）。
        /// 格0以下は0（ガラクタ）、格上限でクランプ。
        /// </summary>
        public static float RelicTechValue(int relicTier, RelicParams p)
        {
            if (relicTier <= 0) return 0f;
            int tier = Mathf.Min(relicTier, p.maxTier);
            return p.baseTechValue * Mathf.Pow(p.tierGrowth, tier - 1);
        }

        public static float RelicTechValue(int relicTier)
            => RelicTechValue(relicTier, RelicParams.Default);

        /// <summary>
        /// 解析（リバースエンジニアリング）の1tick後の理解度（0..1）。自国の科学水準が
        /// minScienceLevel 以下なら<b>一切進まない</b>＝理解できない遺物は飾り（宝の持ち腐れ）。
        /// 閾値を超えた分に比例して速くなる＝遺物は鏡、読める文明にだけ価値を返す。
        /// </summary>
        public static float ReverseEngineeringTick(float understanding, float scienceLevel, float dt, RelicParams p)
        {
            float u = Mathf.Clamp01(understanding);
            float sci = Mathf.Clamp01(scienceLevel);
            if (sci <= p.minScienceLevel) return u; // 科学不足＝飾り
            float scienceFactor = (sci - p.minScienceLevel) / (1f - p.minScienceLevel);
            return Mathf.Clamp01(u + p.analysisRate * scienceFactor * Mathf.Max(0f, dt));
        }

        public static float ReverseEngineeringTick(float understanding, float scienceLevel, float dt)
            => ReverseEngineeringTick(understanding, scienceLevel, dt, RelicParams.Default);

        /// <summary>
        /// 独占の優位＝技術価値×理解度。抱え込んでも理解した分しか力にならない＝
        /// 未解析の独占は優位ゼロのまま漏出リスクだけを負う。
        /// </summary>
        public static float MonopolyAdvantage(float techValue, float understanding)
        {
            return Mathf.Max(0f, techValue) * Mathf.Clamp01(understanding);
        }

        /// <summary>
        /// 独占の機密度の1tick後の値（0..1）。自然漏出（諜報ゼロでも漏れる）＋外国諜報の重みで
        /// 必ず減る＝<see cref="InnovationDiffusionRules"/> と同型の運命（独占は時限）。0で下限クランプ。
        /// </summary>
        public static float SecrecyErosionTick(float secrecy, float foreignSpies, float dt, RelicParams p)
        {
            float sec = Mathf.Clamp01(secrecy);
            float spies = Mathf.Clamp01(foreignSpies);
            float rate = p.baseErosionRate + p.spyErosionWeight * spies;
            return Mathf.Clamp01(sec - rate * Mathf.Max(0f, dt));
        }

        public static float SecrecyErosionTick(float secrecy, float foreignSpies, float dt)
            => SecrecyErosionTick(secrecy, foreignSpies, dt, RelicParams.Default);

        /// <summary>
        /// 公開の名声（0..1）＝技術価値×名声スケール。人類への貢献として正統性・外交ボーナスに
        /// 充てる想定。優位は失うが名声は理解度に依らない＝読めない遺物でも公開なら価値が出る。
        /// </summary>
        public static float PublicationPrestige(float techValue, RelicParams p)
        {
            return Mathf.Clamp01(p.prestigeScale * Mathf.Max(0f, techValue));
        }

        public static float PublicationPrestige(float techValue)
            => PublicationPrestige(techValue, RelicParams.Default);

        /// <summary>
        /// 未理解の高位遺物の暴走リスク（0..1）＝上限×（格/格上限）×（1−理解度）。
        /// 分不相応な力は持ち主を焼く＝低位の遺物や完全理解ならゼロ、最高位×無理解で最悪。
        /// 判定は <see cref="Excavates(float, float)"/> と同じく roll との比較で決定論に行う想定。
        /// </summary>
        public static float CurseRisk(int relicTier, float understanding, RelicParams p)
        {
            if (relicTier <= 0) return 0f;
            float tierRatio = Mathf.Min(relicTier, p.maxTier) / (float)p.maxTier;
            return Mathf.Clamp01(p.maxCurseRisk * tierRatio * (1f - Mathf.Clamp01(understanding)));
        }

        public static float CurseRisk(int relicTier, float understanding)
            => CurseRisk(relicTier, understanding, RelicParams.Default);
    }
}
