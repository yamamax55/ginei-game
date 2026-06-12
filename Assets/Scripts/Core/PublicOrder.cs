using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 治安の状態（LAW-3・#2126・純データ）。犯罪率・取締り力・公共秩序（各0..1）。
    /// </summary>
    public class PublicOrder
    {
        public float crimeRate;   // 犯罪率 0..1
        public float enforcement; // 取締り力（警察力）0..1
        public float orderLevel;  // 公共秩序 0..1（派生・キャッシュ）

        public PublicOrder() { }
        public PublicOrder(float enforcement) { this.enforcement = Mathf.Clamp01(enforcement); }
    }

    /// <summary>
    /// 犯罪の純ロジック（LAW-3・#2126）。犯罪は失業#110・貧困（1−生活水準#181）・格差#917 が押し上げ、取締りで抑制される。
    /// 個体犯罪を追わず集約（惑星×犯罪圧力）。test-first。
    /// </summary>
    public static class CrimeRules
    {
        /// <summary>犯罪圧力の重み（失業/貧困/格差）。</summary>
        public struct CrimeParams
        {
            public float unemploymentWeight;
            public float povertyWeight;
            public float inequalityWeight;

            public CrimeParams(float unemploymentWeight, float povertyWeight, float inequalityWeight)
            {
                this.unemploymentWeight = unemploymentWeight;
                this.povertyWeight = povertyWeight;
                this.inequalityWeight = inequalityWeight;
            }

            /// <summary>既定＝失業0.4/貧困0.4/格差0.2。</summary>
            public static CrimeParams Default => new CrimeParams(0.4f, 0.4f, 0.2f);
        }

        /// <summary>犯罪圧力（0..1）＝失業×重み＋貧困×重み＋格差×重み。</summary>
        public static float CrimePressure(float unemployment, float poverty, float inequality, CrimeParams p)
        {
            float v = Mathf.Clamp01(unemployment) * p.unemploymentWeight
                    + Mathf.Clamp01(poverty) * p.povertyWeight
                    + Mathf.Clamp01(inequality) * p.inequalityWeight;
            return Mathf.Clamp01(v);
        }

        /// <summary>実効犯罪＝犯罪圧力×(1−取締り×抑制係数)（取締りで犯罪が減る）。</summary>
        public static float EffectiveCrime(float crimePressure, float enforcement, float suppressionFactor)
            => Mathf.Clamp01(Mathf.Clamp01(crimePressure) * (1f - Mathf.Clamp01(enforcement) * Mathf.Clamp01(suppressionFactor)));
    }
}
