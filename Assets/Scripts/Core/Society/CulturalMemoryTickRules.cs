using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 「集合記憶×市民宗教」の薄いオーケストレータ（合成層・純ロジック test-first）。
    /// 勢力レベルの <see cref="CulturalMemoryState"/>（信奉/真摯さ/終戦からの年月/戦争の苛烈さ）を、既存の未配線 Core
    /// （<see cref="CivicFaithRules"/>＝市民宗教、<see cref="GenerationalMemoryRules"/>＝戦争記憶の世代風化）へ橋渡しして
    /// 年次収束させ、民心（支持）への背景modifier・占領同化・実効開戦閾値を返す唯一の窓口。数式は委譲先へ委ね二重実装しない。
    /// 結束/安定度を扱う <see cref="CultureSynthesisTickRules"/> とは別物＝こちらは「情緒的紐帯（支持）」と「忘却（開戦の敷居）」の合成。
    /// 決定論・bounded・null安全。基準値は非破壊（read-only で利用、状態は <see cref="CulturalMemoryState"/> のみ更新）。
    /// </summary>
    public static class CulturalMemoryTickRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        /// <summary>市民宗教（情緒的紐帯）が民心（支持）へ与える加点の振れ幅（±この値）。bounded。</summary>
        public const float SupportModifierScale = 10f;
        /// <summary>占領同化速度の基準（情緒的紐帯が強いほど被支配民を速く同化させる）。</summary>
        public const float AssimilationBaseRate = 0.04f;

        /// <summary>
        /// 勢力の市民宗教と集合記憶を1年ぶん進める（収束更新）。
        /// 信奉は <see cref="CivicFaithRules.ManufacturedFaithTick"/> で包摂＝国策の推進力として製造され、
        /// 真摯さは <see cref="CivicFaithRules.RitualizationDrift"/> で形骸化し、終戦からの年月は dt ぶん積む（記憶の風化）。
        /// 数式は委譲先のみ＝ここでは合成と収束の配線に徹する。
        /// </summary>
        /// <param name="s">対象の合成状態。null なら何もしない。</param>
        /// <param name="legitimacy">勢力の正統性(0..1)。現状は将来用（紐帯の健全さの背景・収束には未使用＝後方互換のため引数で受ける）。</param>
        /// <param name="inclusiveness">統治の包摂度(0..1)＝収奪0↔包摂1。市民宗教の製造推進力。</param>
        /// <param name="dt">経過年（通常1）。0以下なら何もしない。</param>
        public static void TickYear(CulturalMemoryState s, float legitimacy, float inclusiveness, float dt)
        {
            if (s == null || dt <= 0f) return;

            float inc = Mathf.Clamp01(inclusiveness);

            // 市民宗教＝包摂を国策の推進力として信奉を製造（CivicFaithRules へ委譲・天井あり）。
            s.devotion = CivicFaithRules.ManufacturedFaithTick(s.devotion, inc, dt);

            // 真摯さは信奉（外形の活力とみなす）で形骸化（CivicFaithRules へ委譲）。
            s.sincerity = CivicFaithRules.RitualizationDrift(s.sincerity, s.devotion, dt);

            // 終戦からの年月を積む＝集合記憶が風化する（GenerationalMemoryRules が消費）。
            s.yearsSinceWar = Mathf.Max(0f, s.yearsSinceWar + dt);
        }

        /// <summary>
        /// 市民宗教（情緒的紐帯）が民心（支持）へ与える背景加点（±・bounded）。read-only（状態を変更しない）。
        /// 政治的結束（信奉×真摯さ＝形骸化を割り引いた実効紐帯・<see cref="CivicFaithRules.CivicCohesion"/> へ委譲）が
        /// 高い（1.0）と +<see cref="SupportModifierScale"/>、低い（0.0）と −<see cref="SupportModifierScale"/>、
        /// 中庸で 0＝紐帯の薄い社会は民心を失い、厚い社会は支持を集める。null なら 0（modifier 無効＝未配線を明示）。
        /// </summary>
        /// <param name="s">対象の合成状態。null なら 0。</param>
        public static float SupportModifier(CulturalMemoryState s)
        {
            if (s == null) return 0f;
            // 信奉×真摯さ（形骸化を割り引いた実効紐帯）を CivicFaithRules へ委譲。
            float civicCohesion = CivicFaithRules.CivicCohesion(s.devotion, s.sincerity);
            // 0..1 を −1..+1 へ写して振れ幅を掛ける＝中庸で 0・両端で ±Scale。
            return (Mathf.Clamp01(civicCohesion) * 2f - 1f) * SupportModifierScale;
        }

        /// <summary>
        /// 占領地の同化速度(/年・0..)。read-only。情緒的紐帯（市民宗教の実効結束）が強いほど被支配民を速く同化させる。
        /// 統合が進む(integration 高)ほど同化の余地は小さくなる（残りの未統合ぶんに比例）。
        /// 速度の単位係数は <see cref="CultureRules"/> 系の同化速度（<see cref="CultureParams.assimilationSpeed"/>）と整合させる。
        /// </summary>
        /// <param name="s">対象の合成状態。null なら 0（同化なし）。</param>
        /// <param name="integration">占領統合度(0=占領直後 .. 1=完全統合)。残りの未統合ぶんに比例して同化が進む。</param>
        public static float AssimilationRate(CulturalMemoryState s, float integration)
        {
            if (s == null) return 0f;
            float bond = Mathf.Clamp01(CivicFaithRules.CivicCohesion(s.devotion, s.sincerity));
            float headroom = 1f - Mathf.Clamp01(integration); // 未統合ぶん＝同化の余地
            // 単位速度は CultureRules 系の同化速度（CultureParams.Default.assimilationSpeed）に整合。
            float unitSpeed = CultureParams.Default.assimilationSpeed;
            float rate = unitSpeed * bond * headroom;
            return Mathf.Max(0f, rate);
        }

        /// <summary>
        /// 集合記憶の強度(0..1)＝半減期カーブ（<see cref="GenerationalMemoryRules.MemoryIntensity"/> へ委譲）。read-only。
        /// 終戦からの年月が経つほど薄れる＝「平和の最大の敵は平和の長さ」。null なら 0。
        /// </summary>
        public static float MemoryIntensity(CulturalMemoryState s)
        {
            if (s == null) return 0f;
            return GenerationalMemoryRules.MemoryIntensity(s.yearsSinceWar, s.warSeverity);
        }

        /// <summary>
        /// 実効開戦閾値＝基準閾値×記憶修正（<see cref="GenerationalMemoryRules.EffectiveWarThreshold"/> へ委譲）。read-only。
        /// 終戦から年月が経つほど単調に下がり、基準値へ漸近する（基準値は非破壊＝実効値パターン）。null なら基準値そのまま。
        /// </summary>
        /// <param name="s">対象の合成状態。null なら baseThreshold をそのまま返す。</param>
        /// <param name="baseThreshold">勢力の基準開戦閾値。</param>
        public static float EffectiveWarThreshold(CulturalMemoryState s, float baseThreshold)
        {
            if (s == null) return baseThreshold;
            return GenerationalMemoryRules.EffectiveWarThreshold(baseThreshold, s.yearsSinceWar, s.warSeverity);
        }
    }
}
