using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 特技・戦法のカタログ（本作オリジナル＝信長の野望の特技／三国志の個性・戦法を参考にした固有名の一覧）。
    /// 「何があるか」の唯一の真実の源。提督はここの id を <see cref="AdmiralData.talents"/> で所持し、
    /// 効果量は <see cref="TalentRules"/> が素養と格で解く（数式はここに持たない＝カタログは名前と分類だけ）。
    /// デザイナーは <see cref="Register"/> で固有特技を増やせる。test-first。
    /// </summary>
    public static class TalentCatalog
    {
        private static readonly Dictionary<string, TalentDef> byId = BuildDefaults();
        private static List<TalentDef> orderedCache;

        private static Dictionary<string, TalentDef> BuildDefaults()
        {
            var list = new List<TalentDef>
            {
                // ── 武勇（攻撃が冴え）──
                new TalentDef("鬼神",   "鬼神",     TalentAspect.武勇, TalentKind.特性, TalentEffect.攻撃強化, 0.15f, SkillCondition.常時,   "比類なき闘将。常に与ダメージが高い。"),
                new TalentDef("背水",   "背水之陣", TalentAspect.武勇, TalentKind.特性, TalentEffect.攻撃強化, 0.25f, SkillCondition.劣勢時, "窮地で猛る。劣勢時に与ダメージが跳ね上がる。"),
                new TalentDef("急襲",   "電光奇襲", TalentAspect.武勇, TalentKind.特性, TalentEffect.側背強化, 0.20f, SkillCondition.側背面時, "側背を取るや一撃必殺。側背面の与ダメージ増。"),
                new TalentDef("連弩",   "連弩斉射", TalentAspect.武勇, TalentKind.戦法, TalentEffect.砲撃戦法, 0.30f, SkillCondition.常時,   "一斉砲撃を強化する短時間の猛射。"),
                new TalentDef("突貫",   "猪突猛進", TalentAspect.武勇, TalentKind.戦法, TalentEffect.突撃戦法, 0.25f, SkillCondition.常時,   "突撃の威力と速度をさらに高める。"),

                // ── 知略（情報が冴え）──
                new TalentDef("看破",   "看破",     TalentAspect.知略, TalentKind.特性, TalentEffect.索敵強化, 6f,    SkillCondition.常時,   "敵の動きを読み、索敵範囲が広がり不意打ちを受けにくい。"),
                new TalentDef("神算",   "神算鬼謀", TalentAspect.知略, TalentKind.特性, TalentEffect.火力集中, 0.20f, SkillCondition.交戦時, "局所火力を集中させる用兵の妙（ランチェスター集中強化）。"),
                new TalentDef("奇道",   "奇道",     TalentAspect.知略, TalentKind.特性, TalentEffect.奇襲,     0.20f, SkillCondition.常時,   "潜伏からの不意打ちが冴える（索敵 #2180／特殊作戦）。"),
                new TalentDef("火計",   "業火之計", TalentAspect.知略, TalentKind.戦法, TalentEffect.範囲攻撃戦法, 0.40f, SkillCondition.常時, "範囲に火を放つ。広範囲の敵へ大ダメージ。"),
                new TalentDef("雷撃",   "天雷召喚", TalentAspect.知略, TalentKind.戦法, TalentEffect.範囲攻撃戦法, 0.55f, SkillCondition.常時, "落雷の如き一撃。神算の極み（高格）。"),

                // ── 統率（統率が冴え）──
                new TalentDef("鉄壁",   "鉄壁",     TalentAspect.統率, TalentKind.特性, TalentEffect.防御強化, 0.15f, SkillCondition.常時,   "陣を崩さず、被ダメージを抑える。"),
                new TalentDef("不動",   "泰山不動", TalentAspect.統率, TalentKind.特性, TalentEffect.士気維持, 12f,   SkillCondition.交戦時, "交戦下でも士気が崩れにくい。"),
                new TalentDef("用兵",   "用兵如神", TalentAspect.統率, TalentKind.特性, TalentEffect.火力集中, 0.15f, SkillCondition.常時,   "兵を巧みに集中運用する（局所火力集中）。"),
                new TalentDef("疾風",   "疾風迅雷", TalentAspect.統率, TalentKind.特性, TalentEffect.機動強化, 0.15f, SkillCondition.常時,   "部隊運動が速い。機動が上がる。"),
                new TalentDef("鼓舞",   "鼓舞激励", TalentAspect.統率, TalentKind.戦法, TalentEffect.鼓舞戦法, 0.30f, SkillCondition.常時,   "全軍を鼓舞し士気を一気に回復させる。"),

                // ── 政務（運営が冴え・マクロ）──
                new TalentDef("兵站",   "兵站名人", TalentAspect.政務, TalentKind.特性, TalentEffect.兵站, 0.20f, SkillCondition.常時, "補給を絶やさぬ手腕。前線の継戦を支える（#2049）。"),
                new TalentDef("治世",   "治世能臣", TalentAspect.政務, TalentKind.特性, TalentEffect.内政, 0.15f, SkillCondition.常時, "統治が安定する。占領地の安定度が上がる（#109）。"),
                new TalentDef("富国",   "富国之才", TalentAspect.政務, TalentKind.特性, TalentEffect.経済, 0.20f, SkillCondition.常時, "産業を興す。所領の産出が増える（#93）。"),
            };
            var map = new Dictionary<string, TalentDef>();
            foreach (var d in list) map[d.id] = d;
            return map;
        }

        /// <summary>id で特技定義を解決（無ければ null）。</summary>
        public static TalentDef Get(string id)
            => (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var d)) ? d : null;

        /// <summary>所持特技（defId）から定義を解決（無ければ null）。</summary>
        public static TalentDef Get(Talent t) => t == null ? null : Get(t.defId);

        /// <summary>カタログの全特技（登録順は不定だが安定列挙のため id でソート）。</summary>
        public static IReadOnlyList<TalentDef> All
        {
            get
            {
                if (orderedCache == null || orderedCache.Count != byId.Count)
                {
                    orderedCache = new List<TalentDef>(byId.Values);
                    orderedCache.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                }
                return orderedCache;
            }
        }

        /// <summary>カタログ件数。</summary>
        public static int Count => byId.Count;

        /// <summary>固有特技を登録／上書き（デザイナー拡張）。</summary>
        public static void Register(TalentDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            byId[def.id] = def;
            orderedCache = null;
        }

        /// <summary>既定カタログへ戻す（テスト・戦役の作り直し）。</summary>
        public static void ResetToDefaults()
        {
            byId.Clear();
            foreach (var kv in BuildDefaults()) byId[kv.Key] = kv.Value;
            orderedCache = null;
        }
    }
}
