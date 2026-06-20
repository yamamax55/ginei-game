using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦で得る経験の<b>種別</b>（ADM 育成弧・#提督育成）。同じ「経験値」でも、どう戦ったかで
    /// <b>どの能力が伸びるか</b>が変わる＝攻めて勝てば攻撃、守り抜けば防御、機動戦・追撃なら機動、
    /// 大艦隊を率いれば統率が主に育つ。配分は <see cref="AdmiralDevelopmentRules"/> が決め、伸び方の
    /// 飽和カーブ自体は <see cref="GrowthRules"/> に委譲する（数式を二重実装しない）。
    /// </summary>
    public enum CombatExperienceKind
    {
        攻勢, // 接近・打撃戦（攻撃中心に伸びる）
        防勢, // 防御・籠城・受け止め（防御中心）
        機動, // 機動戦・追撃・離脱（機動中心）
        統御  // 大艦隊指揮・連携（統率中心）
    }

    /// <summary>
    /// 提督が伸ばす4つの能力面（実効値パターンで <c>AdmiralData</c> の基準に乗せる）。
    /// <see cref="AdmiralData.attack"/>/<see cref="AdmiralData.defense"/>/<see cref="AdmiralData.mobility"/>/
    /// <see cref="AdmiralData.leadership"/> に対応する。
    /// </summary>
    public enum DevelopmentFacet { 攻撃, 防御, 機動, 統率 }

    /// <summary>
    /// 提督の<b>能力面ごと</b>の成長状態（育成弧・純データ）。
    /// 単一スカラの <see cref="Growth"/> が「総経験→全能力一律ボーナス」だったのに対し、
    /// こちらは攻撃/防御/機動/統率に<b>別々の経験</b>を蓄える＝偏った戦歴が偏った提督を作る。
    /// 各面は内部的に <see cref="Growth"/> を1つずつ持ち、伸び方の型（<see cref="GrowthArchetype"/>）は共有する
    /// （飽和カーブ・天井は <see cref="GrowthRules"/> に委譲）。基準能力は <c>AdmiralData</c> 側＝ここは非破壊の上乗せ。
    /// </summary>
    [System.Serializable]
    public class AdmiralDevelopment
    {
        /// <summary>攻撃面の経験蓄積。</summary>
        public Growth attack;
        /// <summary>防御面の経験蓄積。</summary>
        public Growth defense;
        /// <summary>機動面の経験蓄積。</summary>
        public Growth mobility;
        /// <summary>統率面の経験蓄積。</summary>
        public Growth leadership;

        public AdmiralDevelopment() : this(GrowthArchetype.叩き上げ) { }

        /// <summary>全面を同一アーキタイプ・経験0で初期化する。</summary>
        public AdmiralDevelopment(GrowthArchetype archetype)
        {
            attack = new Growth(archetype);
            defense = new Growth(archetype);
            mobility = new Growth(archetype);
            leadership = new Growth(archetype);
        }

        /// <summary>指定面の <see cref="Growth"/> を返す（null安全＝面が未初期化なら新規生成）。</summary>
        public Growth GetFacet(DevelopmentFacet facet)
        {
            switch (facet)
            {
                case DevelopmentFacet.攻撃: return attack ?? (attack = new Growth());
                case DevelopmentFacet.防御: return defense ?? (defense = new Growth());
                case DevelopmentFacet.機動: return mobility ?? (mobility = new Growth());
                case DevelopmentFacet.統率: return leadership ?? (leadership = new Growth());
                default:                    return attack ?? (attack = new Growth());
            }
        }
    }

    /// <summary>
    /// 提督の<b>育成弧</b>（どんな戦歴がどの能力を伸ばすか）の唯一の窓口（純ロジック・test-first）。
    /// <para>
    /// 既存の <see cref="GrowthRules"/>（経験→飽和ボーナス）・<see cref="BattleMetaRules.ExperienceFromBattle"/>
    /// （戦果→総経験量）の<b>上に薄く乗る</b>レイヤー。1回の会戦で得た総経験を、戦い方（<see cref="CombatExperienceKind"/>）
    /// に応じて4能力面へ<b>配分</b>するだけで、経験の蓄積（<see cref="GrowthRules.GainExperience"/>）と
    /// ボーナス算出（<see cref="GrowthRules.EffectiveStatBonus"/>）の数式は委譲する＝二重実装しない。
    /// </para>
    /// 実効値パターン：<c>AdmiralData</c> の基準フィールドは書き換えず、面ごとの <see cref="Growth"/> にだけ積む。
    /// すべて static・決定論・null安全。調整値は <see cref="DevelopmentParams"/>。
    /// </summary>
    public static class AdmiralDevelopmentRules
    {
        /// <summary>育成弧の調整値（経験配分の偏り・滲み出し）。</summary>
        public readonly struct DevelopmentParams
        {
            /// <summary>主能力（その戦い方が主に伸ばす面）へ向かう経験の比率（0..1）。残りは他3面へ均等に滲む。</summary>
            public readonly float primaryShare;

            public DevelopmentParams(float primaryShare)
            {
                // 1/4（完全均等＝偏りなし）以上 1.0（主のみ）以下に丸める。
                this.primaryShare = Mathf.Clamp(primaryShare, 0.25f, 1f);
            }

            /// <summary>既定＝主能力に7割、残り3割を他3面へ滲ませる（偏りつつも総合力も少し伸びる）。</summary>
            public static DevelopmentParams Default => new DevelopmentParams(0.7f);
        }

        /// <summary>戦い方が主に伸ばす能力面（攻勢→攻撃／防勢→防御／機動→機動／統御→統率）。</summary>
        public static DevelopmentFacet PrimaryFacet(CombatExperienceKind kind)
        {
            switch (kind)
            {
                case CombatExperienceKind.攻勢: return DevelopmentFacet.攻撃;
                case CombatExperienceKind.防勢: return DevelopmentFacet.防御;
                case CombatExperienceKind.機動: return DevelopmentFacet.機動;
                case CombatExperienceKind.統御: return DevelopmentFacet.統率;
                default:                        return DevelopmentFacet.統率;
            }
        }

        /// <summary>
        /// 指定の戦い方で総経験 <paramref name="totalExperience"/> を得たとき、各能力面へ配分される経験量を返す
        /// （索引 = <see cref="DevelopmentFacet"/> のキャスト＝攻撃0/防御1/機動2/統率3）。
        /// 主能力に <paramref name="p"/>.primaryShare、残りを他3面へ均等。配分の総和は totalExperience を保つ（漏らさない）。
        /// 負の総経験は0扱い。<see cref="GrowthRules"/> へ積む前段の純配分のみ（蓄積はしない）。
        /// </summary>
        public static float[] DistributeExperience(float totalExperience, CombatExperienceKind kind, DevelopmentParams p)
        {
            float total = Mathf.Max(0f, totalExperience);
            var result = new float[4];
            int primary = (int)PrimaryFacet(kind);

            float primaryAmount = total * p.primaryShare;
            float spillEach = (total - primaryAmount) / 3f; // 残りを他3面へ均等
            for (int i = 0; i < 4; i++) result[i] = (i == primary) ? primaryAmount : spillEach;
            return result;
        }

        /// <summary>既定パラメータ版の <see cref="DistributeExperience(float, CombatExperienceKind, DevelopmentParams)"/>。</summary>
        public static float[] DistributeExperience(float totalExperience, CombatExperienceKind kind)
            => DistributeExperience(totalExperience, kind, DevelopmentParams.Default);

        /// <summary>
        /// 1回の会戦の戦果（<paramref name="damageDealt"/>/<paramref name="kills"/>/<paramref name="isWinner"/>）と
        /// 戦い方 <paramref name="kind"/> から、育成弧へ経験を積む。
        /// 総経験量は <see cref="BattleMetaRules.ExperienceFromBattle"/> に委譲し、<see cref="DistributeExperience"/> で配分、
        /// 各面の蓄積は <see cref="GrowthRules.GainExperience"/>（dt=1）に委譲する＝数式を二重実装しない。
        /// <paramref name="dev"/> が null なら何もしない（null安全）。基準能力は非破壊。
        /// </summary>
        public static void ApplyBattleExperience(AdmiralDevelopment dev, int damageDealt, int kills, bool isWinner,
                                                 CombatExperienceKind kind, DevelopmentParams p)
        {
            if (dev == null) return;
            float total = BattleMetaRules.ExperienceFromBattle(damageDealt, kills, isWinner);
            float[] dist = DistributeExperience(total, kind, p);
            for (int i = 0; i < 4; i++)
            {
                Growth g = dev.GetFacet((DevelopmentFacet)i);
                // amount をそのまま渡し dt=1。成長速度はアーキタイプ（GrowthRules）が掛ける。
                GrowthRules.GainExperience(g, dist[i], dt: 1f);
            }
        }

        /// <summary>既定パラメータ版の <see cref="ApplyBattleExperience(AdmiralDevelopment, int, int, bool, CombatExperienceKind, DevelopmentParams)"/>。</summary>
        public static void ApplyBattleExperience(AdmiralDevelopment dev, int damageDealt, int kills, bool isWinner, CombatExperienceKind kind)
            => ApplyBattleExperience(dev, damageDealt, kills, isWinner, kind, DevelopmentParams.Default);

        /// <summary>
        /// 指定面の経験から乗る実効能力ボーナス分（基準 stat に加算する量・0以上）。
        /// 算出は <see cref="GrowthRules.EffectiveStatBonus"/> に委譲する（飽和カーブ・天井・上限抑制を再実装しない）。
        /// <paramref name="dev"/> が null なら 0。
        /// </summary>
        public static int FacetBonus(AdmiralDevelopment dev, DevelopmentFacet facet, int baseStat)
        {
            if (dev == null) return 0;
            return GrowthRules.EffectiveStatBonus(dev.GetFacet(facet), baseStat);
        }

        /// <summary>
        /// 提督の指定面の成長後能力（基準＋面ごとの経験ボーナス・実効値）。
        /// 基準は <c>AdmiralData</c> の対応フィールド（参謀補完・軍神限界突破は別系統）。
        /// <paramref name="admiral"/> が null なら 0、<paramref name="dev"/> が null なら基準そのまま。
        /// 単一スカラの <see cref="AdmiralGrowthRules.GrownStat(int, Growth)"/> の<b>能力面別</b>版。
        /// </summary>
        public static int GrownFacetStat(AdmiralData admiral, AdmiralDevelopment dev, DevelopmentFacet facet)
        {
            if (admiral == null) return 0;
            int baseStat = BaseStatOf(admiral, facet);
            if (dev == null) return Mathf.Clamp(baseStat, 0, AdmiralData.MaxStatValue);
            int bonus = FacetBonus(dev, facet, baseStat);
            return Mathf.Clamp(baseStat + bonus, 0, AdmiralData.MaxStatValue);
        }

        /// <summary>能力面に対応する <c>AdmiralData</c> の基準フィールドを引く（攻撃/防御/機動/統率）。</summary>
        public static int BaseStatOf(AdmiralData admiral, DevelopmentFacet facet)
        {
            if (admiral == null) return 0;
            switch (facet)
            {
                case DevelopmentFacet.攻撃: return admiral.attack;
                case DevelopmentFacet.防御: return admiral.defense;
                case DevelopmentFacet.機動: return admiral.mobility;
                case DevelopmentFacet.統率: return admiral.leadership;
                default:                    return admiral.leadership;
            }
        }
    }
}
