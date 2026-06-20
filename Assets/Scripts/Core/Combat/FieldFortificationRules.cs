using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 野戦築城パラメータ（臨時の陣地構築）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。
    /// </summary>
    public readonly struct FieldFortificationParams
    {
        /// <summary>築城の手間スケール（努力×時間がこの値で築城度1.0に達する／所要時間の基準）。</summary>
        public readonly float constructionScale;
        /// <summary>陣地による防御倍率の最大上乗せ（1+gain が上限）。</summary>
        public readonly float defenseGain;
        /// <summary>遮蔽による被弾低減の最大割合（0..0.9）。</summary>
        public readonly float maxCover;
        /// <summary>陣地に固定されて失う機動の最大割合（0..1）。</summary>
        public readonly float maxMobilityLoss;
        /// <summary>事前照準した射撃帯（待ち伏せ）の最大上乗せ（1+gain が上限）。</summary>
        public readonly float killZoneGain;
        /// <summary>放棄した陣地が毎 dt で失われる速度（毎秒の築城度低下）。</summary>
        public readonly float decayRate;

        public FieldFortificationParams(
            float constructionScale,
            float defenseGain,
            float maxCover,
            float maxMobilityLoss,
            float killZoneGain,
            float decayRate)
        {
            this.constructionScale = Mathf.Max(0.01f, constructionScale);
            this.defenseGain = Mathf.Clamp(defenseGain, 0f, 2f);
            this.maxCover = Mathf.Clamp(maxCover, 0f, 0.9f);
            this.maxMobilityLoss = Mathf.Clamp01(maxMobilityLoss);
            this.killZoneGain = Mathf.Clamp(killZoneGain, 0f, 2f);
            this.decayRate = Mathf.Clamp(decayRate, 0f, 4f);
        }

        public static FieldFortificationParams Default => new FieldFortificationParams(
            constructionScale: 10f,
            defenseGain: 0.6f,
            maxCover: 0.5f,
            maxMobilityLoss: 0.7f,
            killZoneGain: 0.8f,
            decayRate: 0.1f);
    }

    /// <summary>
    /// 野戦築城ロジック＝防御側が時間と資材をかけて臨時の陣地（防御機雷網・偵察哨・遮蔽）を築き、
    /// 防御力・遮蔽・事前照準した射撃帯を得る代わりに機動を犠牲にする純数値モデル。
    /// 時間とのトレードオフ（築くほど固いが固定される／放棄すると価値が失われる）が核。
    ///
    /// 分担：恒久的な回廊要塞は <see cref="FortressRules"/>（既存）が担う＝本ルールはそれとは別＝
    /// 会戦中に張る「臨時の野戦築城」。工兵の能力・建設は <see cref="MilitaryEngineeringRules"/> と
    /// 連携するが別物（engineerCapacity をその出力として受けるだけ）。盤面非依存・plain 引数・
    /// 実効値パターン（基準値は非破壊・倍率/割合を返すのみ）。
    /// </summary>
    public static class FieldFortificationRules
    {
        /// <summary>
        /// 築城の進捗 0..1。constructionEffort（工兵投入量）×timeSpent（経過時間）が
        /// constructionScale に達すると 1.0。負入力は 0。
        /// </summary>
        public static float FortificationLevel(float constructionEffort, float timeSpent, FieldFortificationParams p)
        {
            float effort = Mathf.Max(0f, constructionEffort);
            float t = Mathf.Max(0f, timeSpent);
            return Mathf.Clamp01(effort * t / p.constructionScale);
        }

        public static float FortificationLevel(float constructionEffort, float timeSpent)
            => FortificationLevel(constructionEffort, timeSpent, FieldFortificationParams.Default);

        /// <summary>
        /// 陣地による防御倍率（1.0〜1+defenseGain）。被ダメージ計算へ乗算する実効値。
        /// fortificationLevel(0..1) を線形に defenseGain ぶん上乗せ。
        /// </summary>
        public static float DefenseBonus(float fortificationLevel, FieldFortificationParams p)
        {
            float lvl = Mathf.Clamp01(fortificationLevel);
            return 1f + lvl * p.defenseGain;
        }

        public static float DefenseBonus(float fortificationLevel)
            => DefenseBonus(fortificationLevel, FieldFortificationParams.Default);

        /// <summary>
        /// 遮蔽による被弾低減割合 0..maxCover（被ダメージにこのぶん乗算減）。
        /// fortificationLevel が高いほど遮蔽が効く。
        /// </summary>
        public static float CoverFromFire(float fortificationLevel, FieldFortificationParams p)
        {
            float lvl = Mathf.Clamp01(fortificationLevel);
            return lvl * p.maxCover;
        }

        public static float CoverFromFire(float fortificationLevel)
            => CoverFromFire(fortificationLevel, FieldFortificationParams.Default);

        /// <summary>
        /// 目標築城レベルまでの所要時間。fortificationTarget(0..1)×constructionScale を
        /// engineerCapacity（工兵能力）で割る。能力が高いほど早い。
        /// </summary>
        public static float ConstructionTime(float fortificationTarget, float engineerCapacity, FieldFortificationParams p)
        {
            float target = Mathf.Clamp01(fortificationTarget);
            float cap = Mathf.Max(0.01f, engineerCapacity);
            return target * p.constructionScale / cap;
        }

        public static float ConstructionTime(float fortificationTarget, float engineerCapacity)
            => ConstructionTime(fortificationTarget, engineerCapacity, FieldFortificationParams.Default);

        /// <summary>
        /// 陣地に固定されて失う機動の割合 0..maxMobilityLoss（実効機動へ (1-この値) を乗算）。
        /// 築くほど動けなくなる＝時間とのトレードオフの代償。
        /// </summary>
        public static float MobilitySacrifice(float fortificationLevel, FieldFortificationParams p)
        {
            float lvl = Mathf.Clamp01(fortificationLevel);
            return lvl * p.maxMobilityLoss;
        }

        public static float MobilitySacrifice(float fortificationLevel)
            => MobilitySacrifice(fortificationLevel, FieldFortificationParams.Default);

        /// <summary>
        /// 事前照準した射撃帯（待ち伏せ的優位）の攻撃倍率（1.0〜1+killZoneGain）。
        /// fortificationLevel と fieldOfFire(0..1＝射界の良さ)の積で効く。
        /// 整えた陣地に踏み込んだ敵へ一方的に火力を集められる。
        /// </summary>
        public static float PreparedKillZone(float fortificationLevel, float fieldOfFire, FieldFortificationParams p)
        {
            float lvl = Mathf.Clamp01(fortificationLevel);
            float fof = Mathf.Clamp01(fieldOfFire);
            return 1f + lvl * fof * p.killZoneGain;
        }

        public static float PreparedKillZone(float fortificationLevel, float fieldOfFire)
            => PreparedKillZone(fortificationLevel, fieldOfFire, FieldFortificationParams.Default);

        /// <summary>
        /// 放棄した陣地の経年劣化。abandoned=false なら現状維持、true なら dt 経過ぶん
        /// decayRate で築城度が下がる（0 下限）。陣地は留まらないと価値を失う。
        /// </summary>
        public static float EntrenchmentDecay(float fortificationLevel, bool abandoned, float dt, FieldFortificationParams p)
        {
            float lvl = Mathf.Clamp01(fortificationLevel);
            if (!abandoned) return lvl;
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(lvl - p.decayRate * t);
        }

        public static float EntrenchmentDecay(float fortificationLevel, bool abandoned, float dt)
            => EntrenchmentDecay(fortificationLevel, abandoned, dt, FieldFortificationParams.Default);

        /// <summary>
        /// 十分に築城できたか＝築城度が threshold 以上か。
        /// </summary>
        public static bool IsWellEntrenched(float fortificationLevel, float threshold)
        {
            return Mathf.Clamp01(fortificationLevel) >= threshold;
        }
    }
}
