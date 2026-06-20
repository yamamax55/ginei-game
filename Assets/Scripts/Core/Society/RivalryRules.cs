using UnityEngine;

namespace Ginei
{
    /// <summary>宿敵関係の調整係数（宿敵システム）。</summary>
    public readonly struct RivalryParams
    {
        /// <summary>対戦1回あたりの強度寄与（互角度1のとき。回数×これで飽和へ向かう）。</summary>
        public readonly float intensityPerEncounter;
        /// <summary>宿敵強度→経験成長倍率の最大上乗せ幅（強度1で 1+この値 倍）。</summary>
        public readonly float growthScale;
        /// <summary>宿敵と対面した会戦の集中ボーナスの最大幅。</summary>
        public readonly float focusScale;
        /// <summary>宿敵喪失時の空虚の深さ（強度1でこの値＝最も深い喪失）。</summary>
        public readonly float voidScale;
        /// <summary>空虚の自然回復速度（per dt。時間だけが張りの喪失を癒す）。</summary>
        public readonly float voidRecoveryRate;

        public RivalryParams(float intensityPerEncounter, float growthScale, float focusScale,
                             float voidScale, float voidRecoveryRate)
        {
            this.intensityPerEncounter = Mathf.Max(0f, intensityPerEncounter);
            this.growthScale = Mathf.Max(0f, growthScale);
            this.focusScale = Mathf.Max(0f, focusScale);
            this.voidScale = Mathf.Max(0f, voidScale);
            this.voidRecoveryRate = Mathf.Max(0f, voidRecoveryRate);
        }

        /// <summary>既定＝対戦寄与0.1/成長幅0.5/集中幅0.2/空虚深度0.8/回復0.05。</summary>
        public static RivalryParams Default => new RivalryParams(0.1f, 0.5f, 0.2f, 0.8f, 0.05f);
    }

    /// <summary>
    /// 宿敵（ヤン vs ラインハルト型）の純ロジック。好敵手の存在が互いの成長・士気を引き上げ、
    /// 宿敵の死は勝者から張り（目標）を奪う。「互角の敵だけが人を磨く」＝強度は対戦回数×互角度で育ち、
    /// 一方的な相手（互角度0）は何度戦っても宿敵にならない。「宿敵の死は勝者から目標を奪う」＝強い宿敵ほど
    /// 喪失の空虚が深く、時間でしか癒えない。<see cref="GrowthRules"/>（経験→能力の成長曲線）とは別系統＝
    /// こちらは関係性ボーナスで、<see cref="GrowthMultiplier"/> を獲得経験に掛ける想定（基準値非破壊＝実効値パターン）。
    /// 乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RivalryRules
    {
        /// <summary>
        /// 宿敵関係の強度（0..1）。対戦回数×互角度（closenessOfMatches 0..1＝戦績がどれだけ拮抗しているか）で育つ。
        /// 互角度0（一方的に勝つ／負けるだけの相手）は回数を重ねても 0＝宿敵にならない。
        /// 回数は intensityPerEncounter ぶんずつ積み上がり 1 で飽和する。
        /// </summary>
        public static float RivalryIntensity(int encounterCount, float closenessOfMatches, RivalryParams p)
        {
            float encounters = Mathf.Clamp01(Mathf.Max(0, encounterCount) * p.intensityPerEncounter);
            return encounters * Mathf.Clamp01(closenessOfMatches);
        }

        public static float RivalryIntensity(int encounterCount, float closenessOfMatches)
            => RivalryIntensity(encounterCount, closenessOfMatches, RivalryParams.Default);

        /// <summary>
        /// 宿敵強度→経験成長の倍率（1..1+growthScale）。互角の敵を持つ者は学びが速い。
        /// <see cref="GrowthRules"/> の獲得経験に掛ける想定（基準経験・基準能力は変えない）。
        /// </summary>
        public static float GrowthMultiplier(float intensity, RivalryParams p)
            => 1f + Mathf.Clamp01(intensity) * p.growthScale;

        public static float GrowthMultiplier(float intensity) => GrowthMultiplier(intensity, RivalryParams.Default);

        /// <summary>
        /// 宿敵と対面した会戦のみの集中ボーナス（0..focusScale）。宿敵が戦場にいない会戦では 0＝
        /// 関係性ボーナスは「相手がそこにいる」ときだけ最大に燃える。
        /// </summary>
        public static float FocusBonus(float intensity, bool facingRival, RivalryParams p)
            => facingRival ? Mathf.Clamp01(intensity) * p.focusScale : 0f;

        public static float FocusBonus(float intensity, bool facingRival)
            => FocusBonus(intensity, facingRival, RivalryParams.Default);

        /// <summary>
        /// 宿敵喪失の空虚（0..voidScale）。強い宿敵ほど死後の空虚が深い＝勝者から目標を奪う。
        /// 士気・成長への長期ペナルティとして消費側が減算/除算に使う想定（基準値非破壊）。
        /// </summary>
        public static float VoidAfterDeath(float intensity, RivalryParams p)
            => Mathf.Clamp01(intensity) * p.voidScale;

        public static float VoidAfterDeath(float intensity) => VoidAfterDeath(intensity, RivalryParams.Default);

        /// <summary>空虚の緩やかな回復。0 へ向けて voidRecoveryRate×dt で漸減する（負の dt は 0 扱い）。</summary>
        public static float VoidRecoveryTick(float currentVoid, float dt, RivalryParams p)
            => Mathf.MoveTowards(Mathf.Clamp01(currentVoid), 0f, p.voidRecoveryRate * Mathf.Max(0f, dt));

        public static float VoidRecoveryTick(float currentVoid, float dt)
            => VoidRecoveryTick(currentVoid, dt, RivalryParams.Default);
    }
}
