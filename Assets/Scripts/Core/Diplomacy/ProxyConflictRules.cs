using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 代理戦争。ライバルに対抗して第三者（代理）を支援する。
    /// 支援は代理を増強するが、エスカレーション（直接戦争への巻き込まれ）と
    /// 露見（後ろ盾の正体が暴かれる）のリスクを伴う。
    /// 純ロジック・決定論・クランプ済み（外部ゲーム型に依存しない）。
    /// </summary>
    public static class ProxyConflictRules
    {
        /// <summary>代理戦争計算のパラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>支援が代理の戦力に与える効果係数。</summary>
            public readonly float supportEffect;
            /// <summary>支援1単位あたりのエスカレーション係数。</summary>
            public readonly float escalationPerSupport;
            /// <summary>支援1単位あたりの露見係数。</summary>
            public readonly float exposurePerSupport;

            public Params(float supportEffect, float escalationPerSupport, float exposurePerSupport)
            {
                this.supportEffect = supportEffect;
                this.escalationPerSupport = escalationPerSupport;
                this.exposurePerSupport = exposurePerSupport;
            }

            /// <summary>既定値（支援は等倍、リスク係数は控えめ）。</summary>
            public static Params Default => new Params(1f, 0.5f, 0.5f);
        }

        /// <summary>
        /// 代理の戦力増強（>=0）。支援量に比例する。
        /// </summary>
        public static float ProxyStrengthBoost(float support01, Params p)
        {
            float s = Mathf.Clamp01(support01);
            return Mathf.Max(0f, s * p.supportEffect);
        }

        /// <summary>代理の戦力増強（既定パラメータ）。</summary>
        public static float ProxyStrengthBoost(float support01)
            => ProxyStrengthBoost(support01, Params.Default);

        /// <summary>
        /// エスカレーションのリスク（0..1）。支援量とライバルの認知度に比例する。
        /// </summary>
        public static float EscalationRisk(float support01, float rivalAwareness01, Params p)
        {
            float s = Mathf.Clamp01(support01);
            float a = Mathf.Clamp01(rivalAwareness01);
            return Mathf.Clamp01(s * p.escalationPerSupport * a);
        }

        /// <summary>エスカレーションのリスク（既定パラメータ）。</summary>
        public static float EscalationRisk(float support01, float rivalAwareness01)
            => EscalationRisk(support01, rivalAwareness01, Params.Default);

        /// <summary>
        /// 露見のリスク（0..1＝後ろ盾の正体が暴かれる確率）。支援量と相手の防諜力に比例する。
        /// </summary>
        public static float ExposureRisk(float support01, float counterIntel01, Params p)
        {
            float s = Mathf.Clamp01(support01);
            float ci = Mathf.Clamp01(counterIntel01);
            return Mathf.Clamp01(s * p.exposurePerSupport * ci);
        }

        /// <summary>露見のリスク（既定パラメータ）。</summary>
        public static float ExposureRisk(float support01, float counterIntel01)
            => ExposureRisk(support01, counterIntel01, Params.Default);
    }
}
