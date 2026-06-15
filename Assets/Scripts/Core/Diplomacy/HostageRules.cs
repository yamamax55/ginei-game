using UnityEngine;

namespace Ginei
{
    /// <summary>人質外交の調整係数。</summary>
    public readonly struct HostageParams
    {
        /// <summary>階級tier1段あたりの価値倍率の伸び（高位ほど高価）。</summary>
        public readonly float tierValueStep;
        /// <summary>無位（tier0相当）の基準価値。</summary>
        public readonly float baseValue;
        /// <summary>君主との血縁近さ(0..1)が満点のとき価値に上乗せされる倍率（1で価値2倍相当）。</summary>
        public readonly float bloodTieWeight;
        /// <summary>非情さ0でも引き出せる譲歩の下限割合（温厚な保持者は脅しに信憑性が無く満額は取れない）。</summary>
        public readonly float ruthlessnessFloor;
        /// <summary>処刑の外聞コスト倍率（人質価値×これ＝高価値ほど野蛮に見える）。</summary>
        public readonly float executionCostFactor;
        /// <summary>価値の風化速度（1秒あたり。世論が忘れる＝価値は時間で逓減）。</summary>
        public readonly float decayRate;

        public HostageParams(float tierValueStep, float baseValue, float bloodTieWeight,
            float ruthlessnessFloor, float executionCostFactor, float decayRate)
        {
            this.tierValueStep = Mathf.Max(0f, tierValueStep);
            this.baseValue = Mathf.Max(0.0001f, baseValue);
            this.bloodTieWeight = Mathf.Max(0f, bloodTieWeight);
            this.ruthlessnessFloor = Mathf.Clamp01(ruthlessnessFloor);
            this.executionCostFactor = Mathf.Max(0f, executionCostFactor);
            this.decayRate = Mathf.Max(0f, decayRate);
        }

        /// <summary>既定＝tier1段で+50%・基準価値1・血縁満点で+200%・非情さ下限50%・処刑コスト1.5倍・風化0.1/秒。</summary>
        public static HostageParams Default => new HostageParams(0.5f, 1f, 2f, 0.5f, 1.5f, 0.1f);
    }

    /// <summary>
    /// 人質外交の純ロジック。要人の身柄が交渉材料になる＝価値は階級（#14 tier）と君主との血縁で決まり、
    /// 保持しているだけで相手に譲歩を呑ませられる。処刑すれば交渉材料を全て失い、外聞（高価値ほど野蛮に
    /// 見える評判コスト）まで失う＝「生かしてこそ価値」を式に出す。価値は時間とともに風化する（世論が忘れる）。
    /// 個々の身柄の状態遷移（捕縛/解放/処断/登用）は <see cref="CaptivityRules"/>、捕虜の数×質の等価交換は
    /// <see cref="PrisonerExchangeRules"/> へ委譲し、ここは人質を「握って交渉する」力学のみを扱う。
    /// 乱数なし・決定論（救出は roll を渡す）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HostageRules
    {
        /// <summary>
        /// 人質の交渉材料としての価値＝基準×（1＋tier×段差）×（1＋血縁×血縁倍率）。
        /// tier は階級序列（#14、無位=0扱い）、bloodTie は君主との血縁近さ(0..1、1=君主の実子級)。
        /// </summary>
        public static float HostageValue(int rankTier, float bloodTie, HostageParams p)
        {
            float rankFactor = 1f + Mathf.Max(0, rankTier) * p.tierValueStep;
            float bloodFactor = 1f + Mathf.Clamp01(bloodTie) * p.bloodTieWeight;
            return p.baseValue * rankFactor * bloodFactor;
        }

        public static float HostageValue(int rankTier, float bloodTie)
            => HostageValue(rankTier, bloodTie, HostageParams.Default);

        /// <summary>
        /// 相手に呑ませられる譲歩量＝価値×（下限＋(1−下限)×非情さ）。
        /// holderRuthlessness(0..1)＝保持者の非情さ。温厚な保持者は「本当に手にかける」脅しに
        /// 信憑性が無く満額は引き出せない（下限割合まで逓減）。
        /// </summary>
        public static float ConcessionPressure(float value, float holderRuthlessness, HostageParams p)
        {
            float v = Mathf.Max(0f, value);
            float credibility = p.ruthlessnessFloor + (1f - p.ruthlessnessFloor) * Mathf.Clamp01(holderRuthlessness);
            return v * credibility;
        }

        public static float ConcessionPressure(float value, float holderRuthlessness)
            => ConcessionPressure(value, holderRuthlessness, HostageParams.Default);

        /// <summary>
        /// 処刑の外聞コスト＝価値×コスト倍率。高価値の人質（高位・君主の血縁）ほど手にかければ野蛮に見える。
        /// さらに交渉材料そのもの（<see cref="ExecutionLeverageLoss"/>＝価値全額）も同時に失う＝総損失は常に
        /// 保持し続ける価値を上回る。処刑の実行（死亡処理・支持低下）は <see cref="CaptivityRules.Execute"/> 側。
        /// </summary>
        public static float ExecutionCost(float value, HostageParams p)
        {
            return Mathf.Max(0f, value) * p.executionCostFactor;
        }

        public static float ExecutionCost(float value) => ExecutionCost(value, HostageParams.Default);

        /// <summary>処刑で失う交渉力＝人質価値の全額（殺せば材料はゼロになる＝生かしてこそ価値）。</summary>
        public static float ExecutionLeverageLoss(float value) => Mathf.Max(0f, value);

        /// <summary>
        /// 救出作戦の成功率(0..1)＝精鋭度×（1−警備強度）。警備が完全(1)なら0、精鋭でなければ届かない。
        /// security(0..1)＝保持側の警備強度、eliteForce(0..1)＝救出部隊の精鋭度。
        /// </summary>
        public static float RescueChance(float security, float eliteForce, HostageParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(eliteForce) * (1f - Mathf.Clamp01(security)));
        }

        public static float RescueChance(float security, float eliteForce)
            => RescueChance(security, eliteForce, HostageParams.Default);

        /// <summary>このとき救出に成功するか（roll が成功率を下回れば成功）。乱数は roll を渡す決定論。</summary>
        public static bool RescueSucceeds(float security, float eliteForce, float roll, HostageParams p)
            => roll < RescueChance(security, eliteForce, p);

        public static bool RescueSucceeds(float security, float eliteForce, float roll)
            => RescueSucceeds(security, eliteForce, roll, HostageParams.Default);

        /// <summary>
        /// 時間経過後の価値＝価値÷（1＋風化速度×dt）。人質の鮮度は時間とともに落ちる（世論が忘れ、
        /// 相手も諦めがつく）。dt&lt;=0 は変化なし。0 へ漸近し負にはならない。
        /// </summary>
        public static float ValueDecay(float value, float dt, HostageParams p)
        {
            float v = Mathf.Max(0f, value);
            float t = Mathf.Max(0f, dt);
            return v / (1f + p.decayRate * t);
        }

        public static float ValueDecay(float value, float dt) => ValueDecay(value, dt, HostageParams.Default);
    }
}
