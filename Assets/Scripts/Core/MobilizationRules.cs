using UnityEngine;

namespace Ginei
{
    /// <summary>動員水準（戦時経済への切替段階）。序数が高いほど軍事へ傾く。</summary>
    public enum MobilizationLevel
    {
        平時,       // 0 通常経済
        部分動員,   // 1 軍需へ一部転換
        総力戦      // 2 国家のすべてを戦争へ
    }

    /// <summary>戦時動員の調整係数。</summary>
    public readonly struct MobilizationParams
    {
        /// <summary>部分動員の軍事生産倍率。</summary>
        public readonly float partialMilitaryFactor;
        /// <summary>総力戦の軍事生産倍率。</summary>
        public readonly float totalMilitaryFactor;
        /// <summary>部分動員の民需生産倍率（軍へ回した分だけ落ちる）。</summary>
        public readonly float partialCivilianFactor;
        /// <summary>総力戦の民需生産倍率。</summary>
        public readonly float totalCivilianFactor;
        /// <summary>総力戦の過熱が始まる継続時間（これを超えると軍事生産も摩耗で落ち始める）。</summary>
        public readonly float overheatTime;
        /// <summary>過熱による軍事生産の減衰率（超過時間あたり）。</summary>
        public readonly float overheatDecay;
        /// <summary>動員水準1段あたりの世論支持低下（per dt）。</summary>
        public readonly float supportDrainPerLevel;

        public MobilizationParams(float partialMilitaryFactor, float totalMilitaryFactor,
                                  float partialCivilianFactor, float totalCivilianFactor,
                                  float overheatTime, float overheatDecay, float supportDrainPerLevel)
        {
            this.partialMilitaryFactor = Mathf.Max(1f, partialMilitaryFactor);
            this.totalMilitaryFactor = Mathf.Max(this.partialMilitaryFactor, totalMilitaryFactor);
            this.partialCivilianFactor = Mathf.Clamp01(partialCivilianFactor);
            this.totalCivilianFactor = Mathf.Clamp(totalCivilianFactor, 0f, this.partialCivilianFactor);
            this.overheatTime = Mathf.Max(0f, overheatTime);
            this.overheatDecay = Mathf.Max(0f, overheatDecay);
            this.supportDrainPerLevel = Mathf.Max(0f, supportDrainPerLevel);
        }

        /// <summary>既定＝部分1.5/総力2.5・民需0.8/0.4・過熱開始100・減衰0.01・支持低下0.01/段。</summary>
        public static MobilizationParams Default => new MobilizationParams(1.5f, 2.5f, 0.8f, 0.4f, 100f, 0.01f, 0.01f);
    }

    /// <summary>
    /// 戦時動員の純ロジック。動員水準を上げるほど軍事生産は跳ねるが、民需が削れ・支持が漏れ・
    /// 総力戦を長く続けると経済が過熱して軍事生産すら摩耗で落ちていく＝総力戦は短期決戦でしか引き合わない。
    /// 倍率は生産係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MobilizationRules
    {
        /// <summary>動員水準の軍事生産倍率（過熱前の基準値）。</summary>
        public static float MilitaryFactor(MobilizationLevel level, MobilizationParams p)
        {
            switch (level)
            {
                case MobilizationLevel.部分動員: return p.partialMilitaryFactor;
                case MobilizationLevel.総力戦:   return p.totalMilitaryFactor;
                default:                          return 1f;
            }
        }

        public static float MilitaryFactor(MobilizationLevel level) => MilitaryFactor(level, MobilizationParams.Default);

        /// <summary>動員水準の民需生産倍率（軍へ回した分だけ落ちる）。</summary>
        public static float CivilianFactor(MobilizationLevel level, MobilizationParams p)
        {
            switch (level)
            {
                case MobilizationLevel.部分動員: return p.partialCivilianFactor;
                case MobilizationLevel.総力戦:   return p.totalCivilianFactor;
                default:                          return 1f;
            }
        }

        public static float CivilianFactor(MobilizationLevel level) => CivilianFactor(level, MobilizationParams.Default);

        /// <summary>
        /// 過熱を含む実効軍事倍率。総力戦が overheatTime を超えて続くと、超過時間×overheatDecay で
        /// 軍事倍率が摩耗していく（下限1＝平時相当までは落ちる）。総力戦以外は過熱しない。
        /// </summary>
        public static float EffectiveMilitaryFactor(MobilizationLevel level, float sustainedTime, MobilizationParams p)
        {
            float baseFactor = MilitaryFactor(level, p);
            if (level != MobilizationLevel.総力戦) return baseFactor;
            float overtime = Mathf.Max(0f, sustainedTime - p.overheatTime);
            return Mathf.Max(1f, baseFactor - overtime * p.overheatDecay);
        }

        public static float EffectiveMilitaryFactor(MobilizationLevel level, float sustainedTime)
            => EffectiveMilitaryFactor(level, sustainedTime, MobilizationParams.Default);

        /// <summary>経済が過熱中か＝総力戦が overheatTime を超えて続いている。</summary>
        public static bool IsOverheating(MobilizationLevel level, float sustainedTime, MobilizationParams p)
        {
            return level == MobilizationLevel.総力戦 && sustainedTime > p.overheatTime;
        }

        public static bool IsOverheating(MobilizationLevel level, float sustainedTime)
            => IsOverheating(level, sustainedTime, MobilizationParams.Default);

        /// <summary>動員の世論支持低下量（per dt）＝水準の段数×supportDrainPerLevel×dt。平時は0。</summary>
        public static float SupportDrain(MobilizationLevel level, float dt, MobilizationParams p)
        {
            return (int)level * p.supportDrainPerLevel * Mathf.Max(0f, dt);
        }

        public static float SupportDrain(MobilizationLevel level, float dt) => SupportDrain(level, dt, MobilizationParams.Default);
    }
}
