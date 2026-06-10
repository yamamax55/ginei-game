using UnityEngine;

namespace Ginei
{
    /// <summary>脱走の調整係数。</summary>
    public readonly struct DesertionParams
    {
        /// <summary>士気満タン・補給良好でも漏れる基礎脱走率（per dt）。</summary>
        public readonly float baseRate;
        /// <summary>低士気が脱走率に上乗せする最大幅（士気ゼロのとき）。</summary>
        public readonly float lowMoraleScale;
        /// <summary>補給切れの脱走倍率（飢えた兵は消える）。</summary>
        public readonly float noSupplyMultiplier;
        /// <summary>長期戦の摩耗が効き始める従軍期間。</summary>
        public readonly float fatigueOnset;
        /// <summary>従軍期間が脱走率に上乗せする増分（onset 超過1あたり）。</summary>
        public readonly float fatigueRate;
        /// <summary>「出血が止まらない」とみなす脱走率の閾値。</summary>
        public readonly float hemorrhageThreshold;

        public DesertionParams(float baseRate, float lowMoraleScale, float noSupplyMultiplier,
                               float fatigueOnset, float fatigueRate, float hemorrhageThreshold)
        {
            this.baseRate = Mathf.Max(0f, baseRate);
            this.lowMoraleScale = Mathf.Max(0f, lowMoraleScale);
            this.noSupplyMultiplier = Mathf.Max(1f, noSupplyMultiplier);
            this.fatigueOnset = Mathf.Max(0f, fatigueOnset);
            this.fatigueRate = Mathf.Max(0f, fatigueRate);
            this.hemorrhageThreshold = Mathf.Max(0f, hemorrhageThreshold);
        }

        /// <summary>既定＝基礎0.001・低士気幅0.01・補給切れ3倍・摩耗開始30・摩耗増0.0002・出血閾値0.01。</summary>
        public static DesertionParams Default => new DesertionParams(0.001f, 0.01f, 3f, 30f, 0.0002f, 0.01f);
    }

    /// <summary>
    /// 脱走の純ロジック（戦闘によらない無言の損耗）。兵は低士気・補給切れ・長期従軍で静かに消えていく＝
    /// 会戦で負けていなくても軍は痩せる。金銭での離反（<see cref="MercenaryRules"/>）・公然の抗命
    /// （<see cref="DisciplineRules"/>）とは別系統＝音もなく塒へ帰る兵。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DesertionRules
    {
        /// <summary>
        /// 脱走率（per dt）＝基礎＋低士気上乗せ（(1−morale)×幅）＋長期摩耗（onset 超過×増分）。
        /// 補給切れ（supplyOk=false）なら全体が倍率で跳ねる。
        /// </summary>
        public static float DesertionRate(float morale, bool supplyOk, float campaignDuration, DesertionParams p)
        {
            float rate = p.baseRate
                       + (1f - Mathf.Clamp01(morale)) * p.lowMoraleScale
                       + Mathf.Max(0f, campaignDuration - p.fatigueOnset) * p.fatigueRate;
            if (!supplyOk) rate *= p.noSupplyMultiplier;
            return rate;
        }

        public static float DesertionRate(float morale, bool supplyOk, float campaignDuration)
            => DesertionRate(morale, supplyOk, campaignDuration, DesertionParams.Default);

        /// <summary>1tick の脱走者数＝兵力×脱走率×dt（兵力は呼び出し側が減算する）。</summary>
        public static float DesertersTick(float strength, float morale, bool supplyOk, float campaignDuration, float dt, DesertionParams p)
        {
            float loss = Mathf.Max(0f, strength) * DesertionRate(morale, supplyOk, campaignDuration, p) * Mathf.Max(0f, dt);
            return Mathf.Min(loss, Mathf.Max(0f, strength)); // 兵力以上は消えない
        }

        public static float DesertersTick(float strength, float morale, bool supplyOk, float campaignDuration, float dt)
            => DesertersTick(strength, morale, supplyOk, campaignDuration, dt, DesertionParams.Default);

        /// <summary>出血が止まらない状態か＝脱走率が閾値以上（休ませる・帰すの判断材料）。</summary>
        public static bool IsHemorrhaging(float morale, bool supplyOk, float campaignDuration, DesertionParams p)
        {
            return DesertionRate(morale, supplyOk, campaignDuration, p) >= p.hemorrhageThreshold;
        }

        public static bool IsHemorrhaging(float morale, bool supplyOk, float campaignDuration)
            => IsHemorrhaging(morale, supplyOk, campaignDuration, DesertionParams.Default);
    }
}
