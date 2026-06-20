using UnityEngine;

namespace Ginei
{
    /// <summary>大事業の工期×品質トレードオフの調整係数（#1091）。既定値は <see cref="Default"/> に集約（マジックナンバー禁止）。</summary>
    public readonly struct QualityScheduleParams
    {
        /// <summary>工期優先(scheduleFocus=1)で進捗倍率がどこまで上がるか＝最大加速。1+これ倍まで速くなる。</summary>
        public readonly float maxSpeedup;
        /// <summary>工期優先(scheduleFocus=1)で構造品質がどこまで落ちるか＝急造の品質ペナルティ（最大）。</summary>
        public readonly float rushQualityPenalty;
        /// <summary>職人の腕(craftsmanship=1)が品質をどこまで底上げするか＝腕で急造を補う最大幅。</summary>
        public readonly float craftQualityBonus;
        /// <summary>低品質×危険曝露で跳ねる事故確率の上限係数。最悪条件(品質0×曝露1)での事故確率の上限。</summary>
        public readonly float maxAccidentChance;
        /// <summary>事故時の進捗後退の基準（低品質・既進捗が大きいほど大きく戻る）。低品質×進捗に掛かる最大後退率。</summary>
        public readonly float maxSetback;
        /// <summary>完成後の耐久性の下限＝急造でも残る最低耐久（脆さの底）。品質1で耐久1。</summary>
        public readonly float minDurability;

        public QualityScheduleParams(float maxSpeedup, float rushQualityPenalty, float craftQualityBonus, float maxAccidentChance, float maxSetback, float minDurability)
        {
            this.maxSpeedup = Mathf.Max(0f, maxSpeedup);
            this.rushQualityPenalty = Mathf.Clamp01(rushQualityPenalty);
            this.craftQualityBonus = Mathf.Clamp01(craftQualityBonus);
            this.maxAccidentChance = Mathf.Clamp01(maxAccidentChance);
            this.maxSetback = Mathf.Clamp01(maxSetback);
            this.minDurability = Mathf.Clamp01(minDurability);
        }

        public static QualityScheduleParams Default => new QualityScheduleParams(
            maxSpeedup: 0.6f,           // 工期全振りで最大1.6倍速
            rushQualityPenalty: 0.5f,   // 工期全振りで品質-0.5
            craftQualityBonus: 0.3f,    // 名工で品質+0.3
            maxAccidentChance: 0.6f,    // 最悪条件で事故確率0.6
            maxSetback: 0.5f,           // 最悪で進捗の半分を失う
            minDurability: 0.4f);       // 急造でも耐久0.4は残る
    }

    /// <summary>
    /// 大事業建造の「工期 vs 品質」トレードオフの純ロジック（#1091・Pillars of the Earth・唯一の窓口・test-first）。
    /// 方針レバー scheduleFocus(0..1=工期優先度) を <see cref="MegaProjectRules"/>（PIL-1 #1090＝事業基盤）の建設へ接続する：
    /// <see cref="ScheduleSpeedup"/> を <see cref="MegaProjectRules.ProgressTick"/> の funding に乗算＝工期優先ほど速い。
    /// だが急ぐほど構造品質(<see cref="StructuralQuality"/>)が落ち、低品質ほど事故イベント（崩落/火災/襲撃）が起きやすく
    /// （<see cref="AccidentChance"/>）、起きたとき大きく進捗が戻る（<see cref="SetbackSeverity"/>＝数年分を失う）。
    /// 事故の発火そのもの（イベント提示・効果適用）は <see cref="EventEngine"/>（#116 事故イベント）が担い、ここは確率/後退量/耐久の
    /// 算出のみ。確率は事前に見せず roll で解決＝決断→創発的帰結（結果プレビュー禁止）。乱数は roll 引数で決定論（Core層・Game型不参照）。
    /// </summary>
    public static class QualityScheduleRules
    {
        /// <summary>
        /// 方針レバーによる進捗倍率（工期優先ほど速い）。scheduleFocus=0で1.0倍、1で 1+maxSpeedup 倍。
        /// 配線時は <see cref="MegaProjectRules.ProgressTick"/> の funding に乗算する想定＝急造は速い（が脆い）。
        /// </summary>
        public static float ScheduleSpeedup(float scheduleFocus, QualityScheduleParams p)
        {
            float s = Mathf.Clamp01(scheduleFocus);
            return 1f + s * p.maxSpeedup;
        }

        public static float ScheduleSpeedup(float scheduleFocus)
            => ScheduleSpeedup(scheduleFocus, QualityScheduleParams.Default);

        /// <summary>
        /// 構造品質(0..1)＝急ぐほど落ち、職人の腕が補う（速さと品質はトレードオフ）。
        /// 基準1.0 − scheduleFocus×rushQualityPenalty ＋ craftsmanship×craftQualityBonus を 0..1 にクランプ。
        /// 工期全振りでも名工なら品質を取り戻せる＝腕が急造の代金を肩代わりする。
        /// </summary>
        public static float StructuralQuality(float scheduleFocus, float craftsmanship, QualityScheduleParams p)
        {
            float s = Mathf.Clamp01(scheduleFocus);
            float c = Mathf.Clamp01(craftsmanship);
            float q = 1f - s * p.rushQualityPenalty + c * p.craftQualityBonus;
            return Mathf.Clamp01(q);
        }

        public static float StructuralQuality(float scheduleFocus, float craftsmanship)
            => StructuralQuality(scheduleFocus, craftsmanship, QualityScheduleParams.Default);

        /// <summary>
        /// 事故確率（崩落/火災/襲撃）＝低品質×危険曝露で跳ねる。(1-品質)×曝露×maxAccidentChance。
        /// 高品質・低曝露なら 0 近く、低品質×高曝露で上限へ。確率は提示せず <see cref="AccidentOccurs"/> の roll で解決する。
        /// </summary>
        public static float AccidentChance(float structuralQuality, float hazardExposure, QualityScheduleParams p)
        {
            float q = Mathf.Clamp01(structuralQuality);
            float h = Mathf.Clamp01(hazardExposure);
            return (1f - q) * h * p.maxAccidentChance;
        }

        public static float AccidentChance(float structuralQuality, float hazardExposure)
            => AccidentChance(structuralQuality, hazardExposure, QualityScheduleParams.Default);

        /// <summary>事故判定（決定論）。roll∈[0,1) が事故確率未満なら事故発生＝true。</summary>
        public static bool AccidentOccurs(float accidentChance, float roll)
            => Mathf.Clamp01(roll) < Mathf.Clamp01(accidentChance);

        /// <summary>
        /// 事故時の進捗後退率(0..1)＝失う進捗の割合。低品質の建造ほど崩れたとき大きく戻る（数年分を失う）。
        /// (1-品質)×progressSoFar×maxSetback ＝品質が高ければ崩れても局所的、低品質は積み上げを大きく失う。
        /// 配線側は progress -= progress×戻り率（あるいは絶対量 progress×戻り率）として使う。
        /// </summary>
        public static float SetbackSeverity(float structuralQuality, float progressSoFar, QualityScheduleParams p)
        {
            float q = Mathf.Clamp01(structuralQuality);
            float prog = Mathf.Clamp01(progressSoFar);
            return (1f - q) * prog * p.maxSetback;
        }

        public static float SetbackSeverity(float structuralQuality, float progressSoFar)
            => SetbackSeverity(structuralQuality, progressSoFar, QualityScheduleParams.Default);

        /// <summary>
        /// 完成後の長期耐久性(0..1)＝急造の建物は完成後も脆い。品質1で1.0、品質0で minDurability。
        /// 配線時は完成効果や被ダメ軽減・経年（ShipAgingRules 流儀）への係数として使う想定。
        /// </summary>
        public static float LongTermDurability(float structuralQuality, QualityScheduleParams p)
        {
            float q = Mathf.Clamp01(structuralQuality);
            return Mathf.Lerp(p.minDurability, 1f, q);
        }

        public static float LongTermDurability(float structuralQuality)
            => LongTermDurability(structuralQuality, QualityScheduleParams.Default);
    }
}
