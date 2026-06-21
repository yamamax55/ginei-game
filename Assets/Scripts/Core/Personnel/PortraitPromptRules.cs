using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 提督ポートレートの生成プロンプトを <see cref="AdmiralData"/> から組み立てる単一窓口（③アート一貫性）。
    /// 一貫性の核＝①全肖像で共通の <see cref="StylePreamble"/>（画風アンカー）②人物アーキタイプ→外見の固定写像
    /// ③名前から決定論シード（再生成しても同一人物）。ツール非依存（Gemini/Midjourney/SD いずれにも投げられる）。
    /// 詳細な運用は docs/ops/portrait-pipeline.md。純ロジック・test-first・基準値非破壊（読むだけ）。
    /// </summary>
    public static class PortraitPromptRules
    {
        /// <summary>全肖像で共通の画風アンカー（これを固定することで作品全体の絵柄が揃う）。</summary>
        public const string StylePreamble =
            "vibrant polished anime style, cel-shaded, clean crisp lineart, large expressive glossy eyes, " +
            "soft bright cinematic lighting, smooth detailed shading, visual-novel character art, " +
            "space-opera military officer, upper-body portrait, simple soft background, " +
            "consistent art direction, highly detailed, masterpiece";

        /// <summary>提督1人ぶんの生成プロンプト（英語・カンマ区切り）。null安全。</summary>
        public static string BuildPrompt(AdmiralData a)
        {
            if (a == null) return StylePreamble;

            var parts = new List<string> { StylePreamble };
            parts.Add("portrait of " + Subject(a));

            string faction = FactionLook(a.faction);
            if (!string.IsNullOrEmpty(faction)) parts.Add(faction);

            string rank = RankBearing(a.rankTier);
            if (!string.IsNullOrEmpty(rank)) parts.Add(rank);

            string arche = ArchetypeLook(a);
            if (!string.IsNullOrEmpty(arche)) parts.Add(arche);

            parts.Add(Expression(a));

            if (!string.IsNullOrEmpty(a.appearanceNote)) parts.Add(a.appearanceNote);

            return string.Join(", ", parts);
        }

        /// <summary>再生成しても同一人物を保つ決定論シード（portraitSeed 指定が優先、未指定は名前ハッシュ）。</summary>
        public static int DeriveSeed(AdmiralData a)
        {
            if (a == null) return 0;
            if (a.portraitSeed > 0) return a.portraitSeed;
            return StableHash(NameKey(a));
        }

        // ── 部品（決定論・基準値を読むだけ） ──

        private static string Subject(AdmiralData a)
        {
            string name = a.ShortName;
            if (string.IsNullOrEmpty(name)) name = a.admiralName;
            return string.IsNullOrEmpty(name) ? "a military officer" : name;
        }

        private static string NameKey(AdmiralData a)
        {
            string n = a.FullName;
            if (string.IsNullOrEmpty(n)) n = a.admiralName;
            return n ?? "";
        }

        private static string FactionLook(Faction f)
        {
            switch (f)
            {
                case Faction.帝国:
                    return "ornate imperial military dress uniform, high collar, gold braid and epaulettes, deep wine-red and black";
                case Faction.同盟:
                    return "practical republican navy uniform, navy blue with silver trim";
                default:
                    return "military officer's dress uniform";
            }
        }

        private static string RankBearing(int tier)
        {
            if (tier <= 0) return "";
            if (tier <= 5) return "young officer in late twenties, eager, few decorations";
            if (tier == 6) return "officer in their thirties, confident bearing";
            if (tier == 7) return "seasoned commander in their forties, composed, several medals";
            if (tier == 8) return "distinguished veteran admiral, many decorations, authoritative";
            if (tier == 9) return "senior admiral of great gravitas, ornate decorations";
            return "supreme commander, a marshal of imposing presence, lavish ceremonial decorations";
        }

        /// <summary>人物アーキタイプ→外見。優先順に最初の一致を1つ採用（焦点を絞る）。</summary>
        private static string ArchetypeLook(AdmiralData a)
        {
            if (a.isKaiser) return "strikingly handsome young man, golden-blond wavy hair, piercing ice-blue eyes, regal sovereign aura";
            if (a.isMagician) return "unassuming young man with messy dark hair, sleepy intelligent eyes, faint ironic smile, scholarly air";
            if (a.isRightHand) return "gentle young man, deep red hair, calm blue eyes, steadfast loyal demeanor";
            if (a.isWarSaint) return "tall imposing man, long flowing beard, narrow phoenix eyes, dignified ruddy complexion";
            if (a.isFierceGeneral) return "burly fierce warrior, bristling beard, glaring round eyes, thunderous presence";
            if (a.isVirtuousLord) return "benevolent dignified man, gentle kind eyes, long earlobes, an air of virtue";
            if (a.isGrandTactician) return "sharp-eyed master strategist, weathered campaigner, cunning and calm";
            if (a.isInnovator) return "youthful bold visionary, sharp confident gaze, unconventional flair";
            if (a.isRisingHero) return "energetic commoner-born officer, bright ambitious eyes, lively spirit";
            if (a.isPeerlessWarrior) return "valiant warrior, crimson-accented armor, fearless gaze";
            if (a.isThreeDayRuler) return "refined cultured officer, ambitious yet melancholy";
            if (a.isTurncoat) return "uneasy young officer, hesitant restless eyes";
            return "";
        }

        /// <summary>能力値から表情を1つ（しきい値85以上を「高い」とする・決定論）。</summary>
        private static string Expression(AdmiralData a)
        {
            const int High = 85;
            if (a.ambition >= High && a.humility < 50) return "proud, ambitious expression";
            if (a.attack >= High) return "bold, aggressive expression";
            if (a.intelligence >= High) return "shrewd, calculating expression";
            if (a.leadership >= High) return "commanding, confident expression";
            if (a.defense >= High) return "calm, steadfast expression";
            return "composed expression";
        }

        /// <summary>文字列の決定論ハッシュ（FNV-1a 32bit・正の int）。string.GetHashCode は環境依存ゆえ使わない。</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (s != null)
                    for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
                int v = (int)(h & 0x7fffffff); // 正に丸める
                return v == 0 ? 1 : v;         // 0 は未指定扱いと衝突するため避ける
            }
        }
    }
}
