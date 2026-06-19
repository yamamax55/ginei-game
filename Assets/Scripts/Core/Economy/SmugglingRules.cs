using UnityEngine;

namespace Ginei
{
    /// <summary>密貿易（フェザーン型の闇取引）の調整係数。封鎖・禁輸を破って禁制品を売買する側の数値モデル。</summary>
    public readonly struct SmugglingParams
    {
        /// <summary>禁制品の利幅（リスクプレミアム）の基礎ぶん（希少性/禁輸が0でもこれだけ乗る）。</summary>
        public readonly float baseMargin;
        /// <summary>希少性×禁輸の厳しさが利幅に効く幅。</summary>
        public readonly float premiumScale;
        /// <summary>摘発確率の基礎倍率（哨戒密度×非隠密に乗る）。</summary>
        public readonly float interdictScale;
        /// <summary>賄賂見逃しの効き幅（賄賂×腐敗に乗る）。</summary>
        public readonly float bribeScale;
        /// <summary>押収損失の基礎倍率（実効摘発×積荷価値に乗る）。</summary>
        public readonly float seizureScale;

        public SmugglingParams(float baseMargin, float premiumScale, float interdictScale,
            float bribeScale, float seizureScale)
        {
            this.baseMargin = Mathf.Clamp(baseMargin, 0f, 1f);
            this.premiumScale = Mathf.Clamp(premiumScale, 0f, 2f);
            this.interdictScale = Mathf.Clamp(interdictScale, 0f, 2f);
            this.bribeScale = Mathf.Clamp(bribeScale, 0f, 2f);
            this.seizureScale = Mathf.Clamp(seizureScale, 0f, 2f);
        }

        /// <summary>既定＝基礎利幅0.2・利幅幅1.0・摘発1.0・賄賂1.0・押収1.0。</summary>
        public static SmugglingParams Default => new SmugglingParams(0.2f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 密貿易（フェザーン型の密輸）の純ロジック。封鎖・禁輸を破って禁制品を闇取引する側の数値モデル。
    /// 密輸ルートの隠密性、禁制品の利幅（リスクプレミアム）、検問の摘発確率、賄賂による見逃し、押収の損失、
    /// 密輸組織の信頼性、密輸の期待利益を計算する。
    ///
    /// <para>分担：<see cref="BlockadeRunningRules"/>（封鎖を強行突破して補給/脱出する側）・
    /// <see cref="CommerceRaidingRules"/>（通商破壊＝船団を狩る側）とは別＝本ルールは禁制品を「闇取引する」側＝
    /// ルート隠密・賄賂・押収・利幅の経済モデル。盤面非依存の plain 引数・乱数なし・決定論。
    /// 入力は 0..100 のスケール（能力/度合い）か正の量（価値）。純ロジック（非 MonoBehaviour・test-first）。</para>
    /// </summary>
    public static class SmugglingRules
    {
        /// <summary>能力スケール 0..100 を 0..1 へ。</summary>
        static float Norm(float stat) => Mathf.Clamp01(Mathf.Clamp(stat, 0f, 100f) / 100f);

        /// <summary>
        /// 密輸ルートの隠密性（0..1）。ルート知識 routeKnowledge と回避技術 evasionSkill（各0..100）の
        /// 幾何平均＝どちらかが低いと隠密性は崩れる。両方高いほど検問に捕まりにくい。
        /// </summary>
        public static float RouteConcealment(float routeKnowledge, float evasionSkill, SmugglingParams p)
        {
            float rk = Norm(routeKnowledge);
            float es = Norm(evasionSkill);
            return Mathf.Clamp01(Mathf.Sqrt(rk * es));
        }

        public static float RouteConcealment(float routeKnowledge, float evasionSkill)
            => RouteConcealment(routeKnowledge, evasionSkill, SmugglingParams.Default);

        /// <summary>
        /// 禁制品の利幅＝リスクプレミアム（0..1）。希少性 scarcity と禁輸の厳しさ embargoSeverity（各0..100）が
        /// 高いほど闇値が跳ね上がる。基礎利幅 baseMargin に「希少性×禁輸」ぶんを premiumScale で乗せて積む。
        /// </summary>
        public static float ContrabandMargin(float scarcity, float embargoSeverity, SmugglingParams p)
        {
            float sc = Norm(scarcity);
            float es = Norm(embargoSeverity);
            float margin = p.baseMargin + p.premiumScale * sc * es;
            return Mathf.Clamp01(margin);
        }

        public static float ContrabandMargin(float scarcity, float embargoSeverity)
            => ContrabandMargin(scarcity, embargoSeverity, SmugglingParams.Default);

        /// <summary>
        /// 検問の摘発確率（0..1）。哨戒密度 patrolDensity（0..100）が高いほど上がり、
        /// ルート隠密性 routeConcealment（0..1）が高いほど (1-隠密) で下がる＝隠れるほど捕まらない。
        /// </summary>
        public static float InterdictionChance(float patrolDensity, float routeConcealment, SmugglingParams p)
        {
            float pd = Norm(patrolDensity);
            float conceal = Mathf.Clamp01(routeConcealment);
            float chance = p.interdictScale * pd * (1f - conceal);
            return Mathf.Clamp01(chance);
        }

        public static float InterdictionChance(float patrolDensity, float routeConcealment)
            => InterdictionChance(patrolDensity, routeConcealment, SmugglingParams.Default);

        /// <summary>
        /// 賄賂による見逃しの成功度（0..1）。賄賂額 bribeAmount（0..100 規模）と官憲の腐敗 officialCorruption
        /// （0..100）が両方高いほど見逃される。清廉な官憲（腐敗0）には賄賂が通じない。
        /// </summary>
        public static float BribeEvasion(float bribeAmount, float officialCorruption, SmugglingParams p)
        {
            float bribe = Norm(bribeAmount);
            float corruption = Norm(officialCorruption);
            float evasion = p.bribeScale * bribe * corruption;
            return Mathf.Clamp01(evasion);
        }

        public static float BribeEvasion(float bribeAmount, float officialCorruption)
            => BribeEvasion(bribeAmount, officialCorruption, SmugglingParams.Default);

        /// <summary>
        /// 実効摘発率（0..1）。素の摘発確率 interdictionChance を、賄賂見逃し bribeEvasion のぶん (1-見逃し) で
        /// 割り引く＝捕まりかけても賄賂が通れば見逃される。
        /// </summary>
        public static float EffectiveInterdiction(float interdictionChance, float bribeEvasion, SmugglingParams p)
        {
            float ic = Mathf.Clamp01(interdictionChance);
            float be = Mathf.Clamp01(bribeEvasion);
            return Mathf.Clamp01(ic * (1f - be));
        }

        public static float EffectiveInterdiction(float interdictionChance, float bribeEvasion)
            => EffectiveInterdiction(interdictionChance, bribeEvasion, SmugglingParams.Default);

        /// <summary>
        /// 押収による損失（積荷価値の損耗量）。実効摘発率 effectiveInterdiction × 積荷価値 cargoValue に
        /// seizureScale を乗じる。摘発されなければ損失ゼロ。
        /// </summary>
        public static float SeizureLoss(float effectiveInterdiction, float cargoValue, SmugglingParams p)
        {
            float ei = Mathf.Clamp01(effectiveInterdiction);
            float value = Mathf.Max(0f, cargoValue);
            return ei * value * p.seizureScale;
        }

        public static float SeizureLoss(float effectiveInterdiction, float cargoValue)
            => SeizureLoss(effectiveInterdiction, cargoValue, SmugglingParams.Default);

        /// <summary>
        /// 密輸組織の信頼性（0..1）。評判 reputation と組織安定 networkStability（各0..100）の幾何平均＝
        /// どちらかが低いと信頼は崩れる（裏切り/手抜き/足がつく）。両方高いほど取引が約束どおり履行される。
        /// </summary>
        public static float SmugglerReliability(float reputation, float networkStability, SmugglingParams p)
        {
            float rep = Norm(reputation);
            float stab = Norm(networkStability);
            return Mathf.Clamp01(Mathf.Sqrt(rep * stab));
        }

        public static float SmugglerReliability(float reputation, float networkStability)
            => SmugglerReliability(reputation, networkStability, SmugglingParams.Default);

        /// <summary>
        /// 密輸の期待利益（密輸の旨味）。利幅 contrabandMargin × 積荷価値 cargoValue に、
        /// 実効摘発率 effectiveInterdiction のぶん (1-摘発) を掛けて押収リスクを織り込む。
        /// 利幅が厚く、隠密・賄賂で摘発を抑えるほど旨味が出る。
        /// </summary>
        public static float ExpectedProfit(float contrabandMargin, float cargoValue, float effectiveInterdiction, SmugglingParams p)
        {
            float margin = Mathf.Clamp01(contrabandMargin);
            float value = Mathf.Max(0f, cargoValue);
            float ei = Mathf.Clamp01(effectiveInterdiction);
            return margin * value * (1f - ei);
        }

        public static float ExpectedProfit(float contrabandMargin, float cargoValue, float effectiveInterdiction)
            => ExpectedProfit(contrabandMargin, cargoValue, effectiveInterdiction, SmugglingParams.Default);
    }
}
