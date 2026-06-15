using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の成長ロジックの純ロジック（#537-543）。経験を時間発展で蓄え、アーキタイプ別の成長曲線で
    /// 実効能力ボーナスへ写す＝早咲き／晩成／高天井／希少を1つの数式で表す。基準能力は変えない（実効値パターン）。
    /// 上限は <see cref="AdmiralData.MaxStatValue"/>。乱数は引かない（経験は roll でなく時間で決まる決定論）。test-first。
    /// </summary>
    public static class GrowthRules
    {
        /// <summary>能力値の上限（<c>AdmiralData</c> 不在環境でも自立するためのフォールバック）。</summary>
        public const int StatCeiling = AdmiralData.MaxStatValue;

        /// <summary>経験→ボーナス変換のスケール（experience この値ぶんで飽和カーブが進む）。</summary>
        public const float ExperienceScale = 100f;

        /// <summary>アーキタイプ別の成長曲線パラメータ。</summary>
        public readonly struct GrowthParams
        {
            public readonly float initialBonus;       // 経験0でも乗る初期補正
            public readonly float speed;              // 経験の獲得倍率（成長速度）
            public readonly float peak;               // 飽和カーブの到達ボーナス（ピーク）
            public readonly int ceiling;              // この型が出せるボーナス天井
            public readonly float promotionAptitude;  // 昇進のしやすさ（0..1。低いほど出世が遅い）

            public GrowthParams(float initialBonus, float speed, float peak, int ceiling, float promotionAptitude)
            {
                this.initialBonus = Mathf.Max(0f, initialBonus);
                this.speed = Mathf.Max(0f, speed);
                this.peak = Mathf.Max(0f, peak);
                this.ceiling = Mathf.Max(0, ceiling);
                this.promotionAptitude = Mathf.Clamp01(promotionAptitude);
            }

            /// <summary>既定＝叩き上げ相当（初期低・成長控えめ・天井低・昇進並）。</summary>
            public static GrowthParams Default => new GrowthParams(2f, 0.8f, 20f, 25, 0.5f);
        }

        /// <summary>アーキタイプ別の成長曲線パラメータを引く。</summary>
        public static GrowthParams ForArchetype(GrowthArchetype archetype)
        {
            switch (archetype)
            {
                // 初期高・昇進早。ピークはそこそこで頭打ち（エリートの伸び悩み）。
                case GrowthArchetype.首席型:     return new GrowthParams(12f, 1.3f, 25f, 30, 0.9f);
                // 高天井だが昇進適性が低い（出世が遅い在野の俊英）。
                case GrowthArchetype.在野俊英型: return new GrowthParams(4f, 1.0f, 38f, 45, 0.3f);
                // 晩成。speed は遅いが peak/ceiling が高く長期で高位に届く。
                case GrowthArchetype.老練型:     return new GrowthParams(3f, 0.6f, 40f, 50, 0.6f);
                // 初期低・希少（成長も控えめ・天井低）。
                case GrowthArchetype.叩き上げ:   return GrowthParams.Default;
                default:                          return GrowthParams.Default;
            }
        }

        /// <summary>
        /// 経験を時間発展で加算する（基準非破壊＝<paramref name="growth"/>.experience のみ更新）。
        /// 増分＝<paramref name="amount"/>×成長速度×<paramref name="dt"/>。負の引数は0扱い、経験は0未満にならない。
        /// </summary>
        public static void GainExperience(Growth growth, float amount, float dt)
        {
            if (growth == null) return;
            float a = Mathf.Max(0f, amount);
            float d = Mathf.Max(0f, dt);
            GrowthParams p = ForArchetype(growth.archetype);
            growth.experience = Mathf.Max(0f, growth.experience + a * p.speed * d);
        }

        /// <summary>
        /// 経験から乗る実効能力ボーナス分（基準statに加算する量）を返す。
        /// 初期補正＋飽和カーブ（経験で peak へ漸近）をアーキタイプ天井でクランプし、
        /// さらに base+bonus が <see cref="AdmiralData.MaxStatValue"/> を超えないよう抑える。基準値は変えない。
        /// </summary>
        public static int EffectiveStatBonus(Growth growth, int baseStat)
            => EffectiveStatBonus(growth, baseStat, StatCeiling);

        /// <summary>
        /// 上限を指定して実効能力ボーナス分を返す（<see cref="TenchijinRules"/> の軍神＝限界突破用）。
        /// 通常は <see cref="StatCeiling"/>（=100）。天地人が揃った軍神型はこれが100超になり、base+bonus が100を超えられる。
        /// アーキタイプ天井（p.ceiling）は据え置き＝伸びしろの形は変えず、到達できる上限だけを上げる。基準非破壊。
        /// <paramref name="statCeiling"/> が StatCeiling 未満でも下限は StatCeiling（限界突破専用＝従来挙動を弱めない）。
        /// </summary>
        public static int EffectiveStatBonus(Growth growth, int baseStat, int statCeiling)
        {
            if (growth == null) return 0;
            GrowthParams p = ForArchetype(growth.archetype);

            // 飽和カーブ：experience→0 で 0、∞ で peak へ漸近（決定論）。
            float exp = Mathf.Max(0f, growth.experience);
            float saturation = exp / (exp + ExperienceScale); // 0..1
            float raw = p.initialBonus + p.peak * saturation;

            // アーキタイプ天井でクランプ。
            int bonus = Mathf.Clamp(Mathf.RoundToInt(raw), 0, p.ceiling);

            // base+bonus が上限を超えないよう抑える（基準非破壊）。下限は従来上限。
            int ceiling = Mathf.Max(StatCeiling, statCeiling);
            int b = Mathf.Clamp(baseStat, 0, ceiling);
            int allowed = ceiling - b;
            return Mathf.Clamp(bonus, 0, allowed);
        }
    }
}
