using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 諜報作戦ルール（純ロジック）。
    /// 工作員の技量と相手の防諜から作戦成功率を出し、得られる情報量（期待値）と
    /// 古びた情報の減衰を扱う。乱数を使わず決定論的（期待値ベース）。
    /// 既存の <see cref="EspionageRules"/>（諜報網）とは別系統の作戦単体ルール。
    /// </summary>
    public static class IntelOperationRules
    {
        /// <summary>諜報作戦算出のパラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>基礎成功率。</summary>
            public readonly float baseSuccess;
            /// <summary>工作員技量が成功率へ寄与する重み。</summary>
            public readonly float skillWeight;
            /// <summary>相手防諜が成功率を下げる重み。</summary>
            public readonly float counterWeight;
            /// <summary>1tick・単位dt あたりの情報減衰量。</summary>
            public readonly float decayPerTick;

            public Params(float baseSuccess, float skillWeight, float counterWeight, float decayPerTick)
            {
                this.baseSuccess = baseSuccess;
                this.skillWeight = skillWeight;
                this.counterWeight = counterWeight;
                this.decayPerTick = decayPerTick;
            }

            /// <summary>既定パラメータ。</summary>
            public static Params Default => new Params(0.2f, 0.6f, 0.5f, 0.1f);
        }

        /// <summary>
        /// 作戦成功率を算出。技量で上がり防諜で下がる。[0,1] にクランプ。
        /// </summary>
        public static float SuccessChance(float agentSkill01, float counterIntel01, Params p)
        {
            float skill = Mathf.Clamp01(agentSkill01);
            float counter = Mathf.Clamp01(counterIntel01);
            float chance = p.baseSuccess + skill * p.skillWeight - counter * p.counterWeight;
            return Mathf.Clamp01(chance);
        }

        /// <summary>SuccessChance の既定パラメータ版。</summary>
        public static float SuccessChance(float agentSkill01, float counterIntel01)
            => SuccessChance(agentSkill01, counterIntel01, Params.Default);

        /// <summary>
        /// 得られる情報量の期待値＝成功率×標的価値（負値は0扱い）。乱数なし。
        /// </summary>
        public static float IntelGain(float successChance01, float targetValue, Params p)
        {
            float chance = Mathf.Clamp01(successChance01);
            float value = Mathf.Max(0f, targetValue);
            return chance * value;
        }

        /// <summary>IntelGain の既定パラメータ版。</summary>
        public static float IntelGain(float successChance01, float targetValue)
            => IntelGain(successChance01, targetValue, Params.Default);

        /// <summary>
        /// 古びた情報を減衰させる。0 を下限とする。
        /// </summary>
        public static float DecayIntel(float currentIntel, float dt, Params p)
        {
            float step = p.decayPerTick * Mathf.Max(0f, dt);
            return Mathf.Max(0f, currentIntel - step);
        }

        /// <summary>DecayIntel の既定パラメータ版。</summary>
        public static float DecayIntel(float currentIntel, float dt)
            => DecayIntel(currentIntel, dt, Params.Default);
    }
}
