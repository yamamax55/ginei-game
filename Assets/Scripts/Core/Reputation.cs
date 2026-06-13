using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の名声（名将ぶり）の純データ（名声システム）。会戦の勝敗で増減し、士気・徴募・敵威圧へ波及する。
    /// renown は 0..1 に正規化（0＝無名、1＝伝説的名将）。基準能力（<see cref="AdmiralData"/>）とは別系統の
    /// 評判ステータスで、解決は <see cref="ReputationRules"/> が唯一の窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class Reputation
    {
        public float renown;   // 名声 0..1
        public int victories;  // 通算勝利数
        public int defeats;    // 通算敗北数

        public Reputation() { }

        public Reputation(float renown, int victories = 0, int defeats = 0)
        {
            this.renown = Mathf.Clamp01(renown);
            this.victories = Mathf.Max(0, victories);
            this.defeats = Mathf.Max(0, defeats);
        }

        /// <summary>戦歴総数（勝＋敗）。</summary>
        public int Battles => victories + defeats;
    }

    /// <summary>名声の調整係数（名声システム）。</summary>
    public readonly struct ReputationParams
    {
        /// <summary>勝利時の基礎名声増分。</summary>
        public readonly float winGain;
        /// <summary>敗北時の基礎名声減分。</summary>
        public readonly float lossErode;
        /// <summary>格上撃破ボーナス係数（敵が自軍より強いほど勝利の名声が増す）。</summary>
        public readonly float upsetMultiplier;
        /// <summary>平時の自然減衰率（無名へ向けて忘れられる速度・per dt）。</summary>
        public readonly float decayRate;
        /// <summary>名声→自軍士気ボーナスの最大幅。</summary>
        public readonly float moraleScale;
        /// <summary>名声→徴募ボーナスの最大幅。</summary>
        public readonly float recruitScale;
        /// <summary>名声→敵威圧（敵士気ペナルティ）の最大幅。</summary>
        public readonly float intimidationScale;

        public ReputationParams(float winGain, float lossErode, float upsetMultiplier, float decayRate,
                                float moraleScale, float recruitScale, float intimidationScale)
        {
            this.winGain = Mathf.Max(0f, winGain);
            this.lossErode = Mathf.Max(0f, lossErode);
            this.upsetMultiplier = Mathf.Max(0f, upsetMultiplier);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.moraleScale = Mathf.Max(0f, moraleScale);
            this.recruitScale = Mathf.Max(0f, recruitScale);
            this.intimidationScale = Mathf.Max(0f, intimidationScale);
        }

        /// <summary>既定＝勝0.1/敗0.08/格上×1.0/減衰0.02/士気0.2/徴募0.3/威圧0.2。</summary>
        public static ReputationParams Default => new ReputationParams(0.1f, 0.08f, 1f, 0.02f, 0.2f, 0.3f, 0.2f);
    }

    /// <summary>
    /// 名声の純ロジック（名将の評判システム）。会戦の勝敗で名声が増減し、格上を破るほど名声が跳ね、
    /// 敗北で削られる。名声は自軍の士気・徴募を底上げし、対面する敵の士気を削る（威圧）。平時は徐々に忘れられる。
    /// 乱数を持たず決定論。基準能力は非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReputationRules
    {
        /// <summary>
        /// 会戦後の名声（0..1）。won=true なら winGain に「格上ボーナス」を上乗せ（enemyStrengthRatio＝敵戦力/自軍戦力、
        /// 1超で格上）。won=false なら lossErode を引く。enemyStrengthRatio が大きい格上に勝つほど増分が大きい。
        /// </summary>
        public static float RenownAfterBattle(float current, bool won, float enemyStrengthRatio, ReputationParams p)
        {
            float r = Mathf.Clamp01(current);
            if (won)
            {
                // 格上（ratio>1）を破るほど上乗せ。ratio<=1 は据え置きの基礎増分。
                float upset = Mathf.Max(0f, enemyStrengthRatio - 1f) * p.upsetMultiplier;
                r += p.winGain * (1f + upset);
            }
            else
            {
                r -= p.lossErode;
            }
            return Mathf.Clamp01(r);
        }

        public static float RenownAfterBattle(float current, bool won, float enemyStrengthRatio)
            => RenownAfterBattle(current, won, enemyStrengthRatio, ReputationParams.Default);

        /// <summary>名声→自軍士気ボーナス（0..moraleScale）。名将の下では兵が奮い立つ。</summary>
        public static float MoraleBonus(float renown, ReputationParams p) => Mathf.Clamp01(renown) * p.moraleScale;
        public static float MoraleBonus(float renown) => MoraleBonus(renown, ReputationParams.Default);

        /// <summary>名声→徴募ボーナス（0..recruitScale）。名声は人を集める。</summary>
        public static float RecruitmentBonus(float renown, ReputationParams p) => Mathf.Clamp01(renown) * p.recruitScale;
        public static float RecruitmentBonus(float renown) => RecruitmentBonus(renown, ReputationParams.Default);

        /// <summary>名声→敵威圧（敵士気ペナルティ 0..intimidationScale）。名将と対峙する敵は怯む。</summary>
        public static float IntimidationFactor(float renown, ReputationParams p) => Mathf.Clamp01(renown) * p.intimidationScale;
        public static float IntimidationFactor(float renown) => IntimidationFactor(renown, ReputationParams.Default);

        /// <summary>平時の名声減衰。baseline（既定0＝無名）へ向けて decayRate×dt で漸減する。</summary>
        public static float Decay(float renown, float dt, ReputationParams p, float baseline = 0f)
        {
            float r = Mathf.Clamp01(renown);
            float bl = Mathf.Clamp01(baseline);
            return Mathf.MoveTowards(r, bl, p.decayRate * Mathf.Max(0f, dt));
        }

        public static float Decay(float renown, float dt) => Decay(renown, dt, ReputationParams.Default);
    }
}
