using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 自動解決した会戦の結果（TIME-4 #950）。勝者・勝者残存兵力・所要時間。
    /// 観戦会戦と同じ game-time を消費できるよう durationSeconds を持つ。
    /// </summary>
    public readonly struct AutoBattleResult
    {
        /// <summary>攻撃側が勝ったか（膠着打ち切り時は兵力多い側を勝者とする）。</summary>
        public readonly bool attackerWon;
        /// <summary>勝者の残存兵力（切り上げ。非負）。</summary>
        public readonly int survivorStrength;
        /// <summary>会戦に要した game-time 秒（>0）。</summary>
        public readonly double durationSeconds;

        public AutoBattleResult(bool attackerWon, int survivorStrength, double durationSeconds)
        {
            this.attackerWon = attackerWon;
            this.survivorStrength = survivorStrength;
            this.durationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// 自動解決シミュの調整値（TIME-4 #950）。マジックナンバーを集約し .Default を持つ。
    /// </summary>
    public readonly struct AutoBattleParams
    {
        /// <summary>基準損耗レート（/秒）。大きいほど早く決着。</summary>
        public readonly float attritionRate;
        /// <summary>無限ループ防止の所要時間上限（秒）。超過＝膠着で打ち切り。</summary>
        public readonly float maxDuration;
        /// <summary>積分刻み（秒）。小さいほど Lanchester 近似精度が上がる。</summary>
        public readonly float dtStep;

        public AutoBattleParams(float attritionRate, float maxDuration, float dtStep)
        {
            this.attritionRate = attritionRate;
            this.maxDuration = maxDuration;
            this.dtStep = dtStep;
        }

        /// <summary>既定値：損耗0.01/秒・上限600秒・刻み1秒。</summary>
        public static AutoBattleParams Default => new AutoBattleParams(DefaultAttritionRate, DefaultMaxDuration, DefaultDtStep);

        public const float DefaultAttritionRate = 0.01f;
        public const float DefaultMaxDuration = 600f;
        public const float DefaultDtStep = 1f;
    }

    /// <summary>
    /// 自動会戦シミュ（TIME-4 #950・純ロジック）。兵力即時比較ではなく時間刻みの簡易戦術シミュで
    /// 会戦の所要時間と結果を算出する唯一の窓口。Lanchester 二乗則型の損耗を dtStep で積分する。
    /// power は提督攻撃力等（既定1.0）。将来 #106 CombatModifiers と整合（ここでは plain float）。
    /// </summary>
    public static class AutoBattleSim
    {
        /// <summary>兵力の絶対下限（これ以下で全滅扱い＝決着）。</summary>
        private const float EliminationThreshold = 0f;
        /// <summary>所要時間の下限（最低1刻みは戦うため durationSeconds は常に>0）。</summary>
        private const double MinDurationSeconds = 1e-6;

        /// <summary>
        /// 2艦隊の会戦を時間刻みで自動解決する。各 dtStep で Lanchester 二乗則型の損耗を積分：
        /// A -= defenderPower×attritionRate×D×dt／D -= attackerPower×attritionRate×A×dt（A,D は現残存）。
        /// どちらかが0以下になった時点で終了し、勝者・残存（切り上げ）・経過 durationSeconds を返す。
        /// maxDuration で打ち切り＝膠着は兵力多い側勝ち（同数は防衛側＝attackerWon=false）。
        /// </summary>
        public static AutoBattleResult Resolve(int attackerStrength, int defenderStrength,
            float attackerPower, float defenderPower, AutoBattleParams p)
        {
            // 兵力は非負クランプ（負の入力が敵を強化する非物理を防ぐ）。
            float a = Mathf.Max(0, attackerStrength);
            float d = Mathf.Max(0, defenderStrength);

            // power は非負クランプ（負＝回復は禁止）。
            float ap = Mathf.Max(0f, attackerPower);
            float dp = Mathf.Max(0f, defenderPower);

            // 調整値は非負／正へクランプ（dtStep<=0 は無限ループになるため最小値で守る）。
            float rate = Mathf.Max(0f, p.attritionRate);
            float maxDur = Mathf.Max(0f, p.maxDuration);
            float dt = p.dtStep > 0f ? p.dtStep : AutoBattleParams.DefaultDtStep;

            // どちらかが初期から全滅＝即決着。
            if (a <= EliminationThreshold || d <= EliminationThreshold)
                return DecideEndState(a, d, dt);

            double elapsed = 0.0;
            while (a > EliminationThreshold && d > EliminationThreshold && elapsed < maxDur)
            {
                // 同時に現残存から減算（Lanchester 二乗則）。
                float da = dp * rate * d * dt;
                float dd = ap * rate * a * dt;
                a -= da;
                d -= dd;
                elapsed += dt;
            }

            // 経過は最低1刻み分（決着・打ち切りいずれも>0）。
            double duration = elapsed > 0.0 ? elapsed : System.Math.Max(MinDurationSeconds, dt);
            return BuildResult(a, d, duration);
        }

        /// <summary>既定係数で会戦を解決する簡易版（power 既定1.0）。</summary>
        public static AutoBattleResult Resolve(int attackerStrength, int defenderStrength)
            => Resolve(attackerStrength, defenderStrength, 1f, 1f, AutoBattleParams.Default);

        /// <summary>既定 power で係数だけ指定する版。</summary>
        public static AutoBattleResult Resolve(int attackerStrength, int defenderStrength, AutoBattleParams p)
            => Resolve(attackerStrength, defenderStrength, 1f, 1f, p);

        /// <summary>開始時点でどちらかが全滅していた場合の即決着。</summary>
        private static AutoBattleResult DecideEndState(float a, float d, float dt)
        {
            double duration = System.Math.Max(MinDurationSeconds, dt);
            return BuildResult(a, d, duration);
        }

        /// <summary>
        /// 残存兵力から勝者・残存（切り上げ・非負）・所要時間を組み立てる。
        /// 攻撃側残存が防衛側残存より多ければ攻撃側勝利、同数は防衛側勝利（守り切る）。
        /// </summary>
        private static AutoBattleResult BuildResult(float a, float d, double duration)
        {
            float ca = Mathf.Max(0f, a);
            float cd = Mathf.Max(0f, d);
            bool attackerWon = ca > cd;
            float winnerStrength = attackerWon ? ca : cd;
            // 切り上げ＝1隻でも残れば生存（相打ち付近で0は0のまま）。
            int survivor = Mathf.Max(0, Mathf.CeilToInt(winnerStrength));
            return new AutoBattleResult(attackerWon, survivor, duration);
        }
    }
}
