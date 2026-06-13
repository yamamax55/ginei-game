using UnityEngine;

namespace Ginei
{
    /// <summary>練度段階（経験の蓄積で上がる艦隊の熟練度）。序数が高いほど精強。</summary>
    public enum ExperienceLevel
    {
        新兵,   // 0 訓練されたばかり
        一般,   // 1 実戦を知る標準
        精鋭,   // 2 数次の会戦を生き残った
        古参    // 3 歴戦＝最精強
    }

    /// <summary>練度の調整係数。</summary>
    public readonly struct VeterancyParams
    {
        /// <summary>一般に上がる経験値の閾値。</summary>
        public readonly float regularXp;
        /// <summary>精鋭に上がる経験値の閾値。</summary>
        public readonly float veteranXp;
        /// <summary>古参に上がる経験値の閾値。</summary>
        public readonly float eliteXp;
        /// <summary>古参（経験値最大）での戦闘倍率ボーナス（1+これが上限）。</summary>
        public readonly float maxCombatBonus;
        /// <summary>1回の会戦で得る基礎経験値（激しさ intensity 0..1 を掛ける）。</summary>
        public readonly float xpPerBattle;

        public VeterancyParams(float regularXp, float veteranXp, float eliteXp, float maxCombatBonus, float xpPerBattle)
        {
            this.regularXp = Mathf.Max(0f, regularXp);
            this.veteranXp = Mathf.Max(this.regularXp, veteranXp);
            this.eliteXp = Mathf.Max(this.veteranXp, eliteXp);
            this.maxCombatBonus = Mathf.Max(0f, maxCombatBonus);
            this.xpPerBattle = Mathf.Max(0f, xpPerBattle);
        }

        /// <summary>既定＝一般10/精鋭30/古参60・最大ボーナス+30%・1会戦10xp。</summary>
        public static VeterancyParams Default => new VeterancyParams(10f, 30f, 60f, 0.3f, 10f);
    }

    /// <summary>
    /// 練度の純ロジック。会戦を重ねるほど経験値が積み上がり（激戦ほど多く）、練度段階と戦闘倍率が上がる。
    /// だが損耗を新兵で補充すると練度は希釈される（加重平均）＝歴戦の艦隊は数字以上に強く、
    /// 替えが利かない。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class VeterancyRules
    {
        /// <summary>経験値→練度段階。閾値は <see cref="VeterancyParams"/>。</summary>
        public static ExperienceLevel LevelOf(float xp, VeterancyParams p)
        {
            float x = Mathf.Max(0f, xp);
            if (x >= p.eliteXp) return ExperienceLevel.古参;
            if (x >= p.veteranXp) return ExperienceLevel.精鋭;
            if (x >= p.regularXp) return ExperienceLevel.一般;
            return ExperienceLevel.新兵;
        }

        public static ExperienceLevel LevelOf(float xp) => LevelOf(xp, VeterancyParams.Default);

        /// <summary>
        /// 会戦後の経験値。激しさ intensity(0..1) に比例して xpPerBattle を積む（生き残った者だけが学ぶ）。
        /// </summary>
        public static float GainFromBattle(float xp, float intensity, VeterancyParams p)
        {
            return Mathf.Max(0f, xp) + p.xpPerBattle * Mathf.Clamp01(intensity);
        }

        public static float GainFromBattle(float xp, float intensity) => GainFromBattle(xp, intensity, VeterancyParams.Default);

        /// <summary>
        /// 練度の戦闘倍率（1..1+maxCombatBonus）。経験値が eliteXp で上限に達する線形カーブ。
        /// 基準ダメージ・防御に掛けて使う（基準非破壊）。
        /// </summary>
        public static float CombatFactor(float xp, VeterancyParams p)
        {
            if (p.eliteXp <= 0f) return 1f + p.maxCombatBonus;
            float t = Mathf.Clamp01(Mathf.Max(0f, xp) / p.eliteXp);
            return 1f + p.maxCombatBonus * t;
        }

        public static float CombatFactor(float xp) => CombatFactor(xp, VeterancyParams.Default);

        /// <summary>
        /// 補充による練度の希釈＝残存（経験値 xp）と新兵（経験値 rookieXp、通常0）の兵力加重平均。
        /// 大損耗を新兵で埋めるほど練度は落ちる＝歴戦の中身は買い戻せない。
        /// </summary>
        public static float DiluteOnReinforce(float xp, float survivorStrength, float reinforcementStrength, float rookieXp = 0f)
        {
            float s = Mathf.Max(0f, survivorStrength);
            float r = Mathf.Max(0f, reinforcementStrength);
            if (s + r <= 0f) return Mathf.Max(0f, xp);
            return (Mathf.Max(0f, xp) * s + Mathf.Max(0f, rookieXp) * r) / (s + r);
        }
    }
}
