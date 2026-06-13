using UnityEngine;

namespace Ginei
{
    /// <summary>連続運転（高炉・反応炉のような止め起こしに大コストがかかるプロセス）の調整係数。</summary>
    public readonly struct ContinuousOperationParams
    {
        /// <summary>連続性が最大のときの最低稼働率（turndown floor の上限＝連続炉はここまでしか絞れない）。</summary>
        public readonly float maxTurndownFloor;
        /// <summary>連続性が最大のときの停止コスト（炉が冷えて固まる＝再起動の莫大な費用）。</summary>
        public readonly float maxShutdownCost;
        /// <summary>連続性が最大のときの再起動所要時間。</summary>
        public readonly float maxRestartTime;
        /// <summary>戦時ロックイン係数（連続性×戦争継続時間に掛ける＝一度始めると止まらない強度）。</summary>
        public readonly float warLockInRate;

        public ContinuousOperationParams(float maxTurndownFloor, float maxShutdownCost,
                                         float maxRestartTime, float warLockInRate)
        {
            this.maxTurndownFloor = Mathf.Clamp01(maxTurndownFloor);
            this.maxShutdownCost = Mathf.Max(0f, maxShutdownCost);
            this.maxRestartTime = Mathf.Max(0f, maxRestartTime);
            this.warLockInRate = Mathf.Max(0f, warLockInRate);
        }

        /// <summary>既定＝最低稼働0.7・停止コスト100・再起動時間50・ロックイン0.02/単位時間。</summary>
        public static ContinuousOperationParams Default => new ContinuousOperationParams(0.7f, 100f, 50f, 0.02f);
    }

    /// <summary>
    /// 連続運転の硬直の純ロジック（#1115）。高炉・反応炉のような連続プロセスは止め起こしに大コストがかかり、
    /// 需要が減っても止められない＝戦時生産の硬直。連続性が高いほど（1に近いほど）最低稼働率は底上げされ
    /// （ゼロまで絞れない＝高炉は止められない）、停止コスト・再起動時間が跳ね上がる。需要が落ちても最低稼働ぶんは
    /// 作り続けて在庫の山になり、止めるより回し続ける方が安い領域では戦時硬直として操業が続く。
    /// 倍率/判定は生産係数に掛けて使う想定（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 動員水準そのものは <see cref="MobilizationRules"/>（国家の経済切替段階）、ロジスティック伝播は SpreadRules が担い、
    /// ここは個々のプラントの慣性（止められない硬直）だけを扱う＝別系統。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ContinuousOperationRules
    {
        /// <summary>
        /// 最低稼働率（turndown floor）。連続性 plantContinuity(0..1) に比例して底上げされる＝
        /// 連続プロセスはゼロまで絞れない（高炉は止められない）。0 のバッチ炉は 0、1 の連続炉は maxTurndownFloor。
        /// </summary>
        public static float TurndownFloor(float plantContinuity, ContinuousOperationParams p)
        {
            return Mathf.Clamp01(plantContinuity) * p.maxTurndownFloor;
        }

        public static float TurndownFloor(float plantContinuity) => TurndownFloor(plantContinuity, ContinuousOperationParams.Default);

        /// <summary>
        /// 停止コスト＝炉が冷えて固まる再起動費用。連続性が高いほど跳ね上がる（連続性²×maxShutdownCost＝
        /// 非線形＝連続炉ほど止めると重い）。
        /// </summary>
        public static float ShutdownCost(float plantContinuity, ContinuousOperationParams p)
        {
            float c = Mathf.Clamp01(plantContinuity);
            return c * c * p.maxShutdownCost;
        }

        public static float ShutdownCost(float plantContinuity) => ShutdownCost(plantContinuity, ContinuousOperationParams.Default);

        /// <summary>再起動所要時間。連続性に比例（連続炉ほど立ち上げが長い）。</summary>
        public static float RestartTime(float plantContinuity, ContinuousOperationParams p)
        {
            return Mathf.Clamp01(plantContinuity) * p.maxRestartTime;
        }

        public static float RestartTime(float plantContinuity) => RestartTime(plantContinuity, ContinuousOperationParams.Default);

        /// <summary>
        /// 硬直による過剰生産＝需要が落ちても最低稼働ぶんは作り続ける（在庫の山）。
        /// 実際の生産量は max(demand, capacity×turndownFloor)。需要がそれを下回ったぶんが過剰となる。
        /// 需要が最低稼働ライン以上なら過剰は 0。
        /// </summary>
        public static float OverproductionFromRigidity(float demand, float turndownFloor, float capacity)
        {
            float cap = Mathf.Max(0f, capacity);
            float floor = Mathf.Clamp01(turndownFloor);
            float d = Mathf.Clamp(demand, 0f, cap);
            float minOutput = cap * floor;
            float output = Mathf.Max(d, minOutput);
            return Mathf.Max(0f, output - d);
        }

        /// <summary>
        /// 操業判断＝回し続けるべきか（true=操業継続）。需要が落ちても、止めて再起動するコスト
        /// （停止コスト＋再起動時間×時間コスト）と、最低稼働ぶんを無駄に回す浪費（最低稼働率に比例）を比べ、
        /// 止めるより回す方が安い領域では操業を続ける＝戦時硬直の本体。需要が最低稼働を上回るなら当然操業。
        /// </summary>
        public static bool OperatingDecision(float demand, float turndownFloor, float shutdownCost, float restartTime)
        {
            float d = Mathf.Clamp01(demand);
            float floor = Mathf.Clamp01(turndownFloor);
            // 需要が最低稼働を上回る＝普通に操業
            if (d >= floor) return true;
            // 止めて再起動するトータルコスト（炉が冷えて固まる＋立ち上げ時間）
            float stopCost = Mathf.Max(0f, shutdownCost) + Mathf.Max(0f, restartTime);
            // 回し続ける浪費＝需要を下回って空回しする最低稼働ぶん
            float idleWaste = floor - d;
            // 止めるコストの方が高ければ回し続ける（戦時硬直）
            return stopCost >= idleWaste;
        }

        /// <summary>
        /// 戦時のロックイン強度（0..1）。連続運転は一度始めると戦争が終わるまで止まらない＝
        /// 連続性×戦争継続時間×warLockInRate が積み上がり、1 で完全ロックイン（需要が消えても止められない）。
        /// </summary>
        public static float WartimeLockIn(float plantContinuity, float warDuration, ContinuousOperationParams p)
        {
            float c = Mathf.Clamp01(plantContinuity);
            float dur = Mathf.Max(0f, warDuration);
            return Mathf.Clamp01(c * dur * p.warLockInRate);
        }

        public static float WartimeLockIn(float plantContinuity, float warDuration)
            => WartimeLockIn(plantContinuity, warDuration, ContinuousOperationParams.Default);
    }
}
