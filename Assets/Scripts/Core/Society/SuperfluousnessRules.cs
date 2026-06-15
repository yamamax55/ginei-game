using UnityEngine;

namespace Ginei
{
    /// <summary>余剰性（superfluousness）の調整係数（TOTL-4 #1524・アーレント型）。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct SuperfluousnessParams
    {
        /// <summary>失業の重み（余剰割合の合成で経済的不要を測る比重）。</summary>
        public readonly float unemploymentWeight;
        /// <summary>没落の重み（中間層からの転落＝社会的地位の喪失の比重）。</summary>
        public readonly float declassedWeight;
        /// <summary>根なし化の重み（地縁・職縁を失い帰属を持たない比重）。</summary>
        public readonly float rootlessWeight;
        /// <summary>承認の欠如が「不要だという感覚」を深める利得（承認0で最大化）。</summary>
        public readonly float redundancyDeepenWeight;
        /// <summary>余剰人口が運動に吸収される基準率（運動の訴求1のとき）。</summary>
        public readonly float absorptionWeight;
        /// <summary>使い捨ての常態化が進む速度（per dt・非人間化最大のとき）。</summary>
        public readonly float normalizeRate;
        /// <summary>意味ある役割による余剰性の解消速度（per dt・意味ある仕事最大のとき）。</summary>
        public readonly float reintegrationRate;
        /// <summary>大量余剰（運動の温床）判定の既定しきい値。</summary>
        public readonly float massThreshold;

        public SuperfluousnessParams(float unemploymentWeight, float declassedWeight, float rootlessWeight,
                                     float redundancyDeepenWeight, float absorptionWeight,
                                     float normalizeRate, float reintegrationRate, float massThreshold)
        {
            this.unemploymentWeight = Mathf.Max(0f, unemploymentWeight);
            this.declassedWeight = Mathf.Max(0f, declassedWeight);
            this.rootlessWeight = Mathf.Max(0f, rootlessWeight);
            this.redundancyDeepenWeight = Mathf.Clamp01(redundancyDeepenWeight);
            this.absorptionWeight = Mathf.Max(0f, absorptionWeight);
            this.normalizeRate = Mathf.Max(0f, normalizeRate);
            this.reintegrationRate = Mathf.Max(0f, reintegrationRate);
            this.massThreshold = Mathf.Clamp01(massThreshold);
        }

        /// <summary>既定＝失業0.4/没落0.35/根なし0.25（合成重み和=1）・承認欠如利得0.5・吸収0.7・常態化0.2・再統合0.3・大量余剰0.5。</summary>
        public static SuperfluousnessParams Default =>
            new SuperfluousnessParams(0.4f, 0.35f, 0.25f, 0.5f, 0.7f, 0.2f, 0.3f, 0.5f);
    }

    /// <summary>
    /// 余剰性（superfluousness）の純ロジック（TOTL-4 #1524・ハンナ・アーレント『全体主義の起原』型）。
    /// 近代社会が生み出す「不要とされた人々（使い捨ての人口）」＝失業・没落・根なし化した大衆が、
    /// 自分が世界に不要だと感じるとき、全体主義運動が彼らを<b>燃料</b>として吸収する。核は
    /// 「不要とされた者ほど運動に意味を見出す」＝<see cref="MovementAbsorption"/> で余剰人口の運動吸収率が上がり、
    /// 無意味感が（虚構の）意味への飢え（<see cref="MeaningHunger"/>）を生み、それが運動の動員燃料
    /// （<see cref="MobilizationFuel"/>＝<see cref="TotalitarianRules"/> への入力）になる。処方は意味ある役割
    /// （雇用・包摂）による再統合（<see cref="ReintegrationViaPurpose"/>）。
    /// 人口コホートの動態は <see cref="DemographicsRules"/>、全体主義の自己強化ループ本体は <see cref="TotalitarianRules"/>（生成済み）、
    /// 法的に権利を奪われた無権利者は <see cref="StatelessnessRules"/>（同 EPIC TOTL）、意味の喪失＝末人は <see cref="HopeRules"/> が担う。
    /// 全入力 0..1 にクランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SuperfluousnessRules
    {
        /// <summary>
        /// 「不要とされた人口」の割合(0..1)＝失業・没落・根なし化の加重合成。経済的に職を失い（失業）、
        /// 社会的地位から転落し（没落）、帰属を失った（根なし化）ほど大きい。重みは合成で正規化する。
        /// </summary>
        public static float SuperfluousShare(float unemployment, float declassed, float rootlessness, SuperfluousnessParams p)
        {
            float u = Mathf.Clamp01(unemployment);
            float d = Mathf.Clamp01(declassed);
            float r = Mathf.Clamp01(rootlessness);
            float wSum = p.unemploymentWeight + p.declassedWeight + p.rootlessWeight;
            if (wSum <= 0f) return 0f;
            float share = (p.unemploymentWeight * u + p.declassedWeight * d + p.rootlessWeight * r) / wSum;
            return Mathf.Clamp01(share);
        }

        public static float SuperfluousShare(float unemployment, float declassed, float rootlessness)
            => SuperfluousShare(unemployment, declassed, rootlessness, SuperfluousnessParams.Default);

        /// <summary>
        /// 自分が世界に不要だという感覚(0..1)。余剰割合が大きいほど深く、社会的承認 socialRecognition の
        /// 欠如がそれをさらに深める（誰にも必要とされていないという実感）。承認1なら深化分は消える。
        /// </summary>
        public static float FeelingOfRedundancy(float superfluousShare, float socialRecognition, SuperfluousnessParams p)
        {
            float s = Mathf.Clamp01(superfluousShare);
            float lack = 1f - Mathf.Clamp01(socialRecognition);
            // 余剰そのもの＋承認の欠如が余剰分を増幅する（承認が支えれば余剰でも不要感は浅い）。
            return Mathf.Clamp01(s + s * lack * p.redundancyDeepenWeight);
        }

        public static float FeelingOfRedundancy(float superfluousShare, float socialRecognition)
            => FeelingOfRedundancy(superfluousShare, socialRecognition, SuperfluousnessParams.Default);

        /// <summary>
        /// 余剰人口が運動に吸収される率(0..1)＝余剰割合 × 運動の訴求 movementAppeal × 吸収基準率。
        /// 不要とされた者ほど運動に意味（自分が世界史の担い手だという虚構）を見出して吸い込まれる。
        /// 両者が揃って初めて吸収が回る（どちらか0なら0）。
        /// </summary>
        public static float MovementAbsorption(float superfluousShare, float movementAppeal, SuperfluousnessParams p)
        {
            float s = Mathf.Clamp01(superfluousShare);
            float a = Mathf.Clamp01(movementAppeal);
            return Mathf.Clamp01(s * a * p.absorptionWeight);
        }

        public static float MovementAbsorption(float superfluousShare, float movementAppeal)
            => MovementAbsorption(superfluousShare, movementAppeal, SuperfluousnessParams.Default);

        /// <summary>
        /// 吸収された余剰人口が運動の動員燃料になる量(0..1)＝<see cref="TotalitarianRules"/> への入力。
        /// 吸収率がそのまま運動の燃料量（動員力）に直結する＝使い捨て人口が運動を駆動する。
        /// </summary>
        public static float MobilizationFuel(float movementAbsorption)
        {
            return Mathf.Clamp01(movementAbsorption);
        }

        /// <summary>
        /// 「人を使い捨てにできる」という感覚の常態化の1tick後の値(0..1)＝強制収容所の論理。
        /// 余剰割合が大きく、非人間化 dehumanization が進むほど、人間を余計者として処分できるという
        /// 感覚が社会に定着していく（両者の積ぶんずつ進む）。
        /// </summary>
        public static float DisposabilityNormalization(float current, float superfluousShare, float dehumanization, float dt, SuperfluousnessParams p)
        {
            float d = Mathf.Max(0f, dt);
            float drive = p.normalizeRate * Mathf.Clamp01(superfluousShare) * Mathf.Clamp01(dehumanization) * d;
            return Mathf.Clamp01(Mathf.Clamp01(current) + drive);
        }

        public static float DisposabilityNormalization(float current, float superfluousShare, float dehumanization, float dt)
            => DisposabilityNormalization(current, superfluousShare, dehumanization, dt, SuperfluousnessParams.Default);

        /// <summary>
        /// （虚構の）意味への飢え(0..1)＝無意味感がそのまま意味への渇望を生む。不要だという感覚が
        /// 深いほど、運動が与える偽の意味（敵の名指し・歴史の使命）への飢えが強い＝燃えやすい。
        /// </summary>
        public static float MeaningHunger(float feelingOfRedundancy)
        {
            return Mathf.Clamp01(feelingOfRedundancy);
        }

        /// <summary>
        /// 意味ある役割（雇用・包摂）による余剰性の解消の1tick後の値(0..1)。意味ある仕事 meaningfulWork を
        /// 与えるほど余剰割合が下がる＝不要とされた者に世界での居場所を返す処方（運動の燃料を断つ）。
        /// </summary>
        public static float ReintegrationViaPurpose(float superfluousShare, float meaningfulWork, float dt, SuperfluousnessParams p)
        {
            float d = Mathf.Max(0f, dt);
            float relief = p.reintegrationRate * Mathf.Clamp01(meaningfulWork) * d;
            return Mathf.Clamp01(Mathf.Clamp01(superfluousShare) - relief);
        }

        public static float ReintegrationViaPurpose(float superfluousShare, float meaningfulWork, float dt)
            => ReintegrationViaPurpose(superfluousShare, meaningfulWork, dt, SuperfluousnessParams.Default);

        /// <summary>大量の余剰人口が運動の温床になったか＝余剰割合がしきい値以上（使い捨て人口の臨界）。</summary>
        public static bool IsMassSuperfluity(float superfluousShare, float threshold)
        {
            return Mathf.Clamp01(superfluousShare) >= Mathf.Clamp01(threshold);
        }

        public static bool IsMassSuperfluity(float superfluousShare)
            => IsMassSuperfluity(superfluousShare, SuperfluousnessParams.Default.massThreshold);
    }
}
