using UnityEngine;

namespace Ginei
{
    /// <summary>捕獲側の処遇（LIFE-4 #154）。解放＝元勢力へ復帰／処断＝死亡(#152)へ合流／登用＝自陣営へ寝返り。</summary>
    public enum CaptiveDisposition { 解放, 処断, 登用 }

    /// <summary>
    /// 捕虜化・解放・処断の純ロジック（LIFE-4 #154・死亡 #152 と対の可逆ルート・唯一の窓口）。「殺すか・捕らえるか」の
    /// 分岐＝捕縛→拘留→処遇。死(#152)が不可逆で席が空くのに対し、捕虜は<b>生存・拘留中で席は空くが復帰しうる</b>
    /// （解放で復帰／処断で初めて死＝<see cref="LifecycleRules.Kill"/> へ合流／登用で自陣営へ）。処遇傾向は政体(#145/#117)で変わる。
    /// 乱数は呼び出し側が roll(0..1) を渡す＝決定論的にテストできる。test-first。
    /// </summary>
    public static class CaptivityRules
    {
        /// <summary>
        /// 捕虜化の確率（0..1）。退路を断たれ（ZOC包囲 #81）・指揮/士気が低いほど高い。係数 #106 想定。
        /// </summary>
        public static float CaptureChance(bool encircled, float commandFactor, float moraleFactor)
        {
            float baseChance = encircled ? 0.6f : 0.15f; // 包囲されると捕縛されやすい
            float escapeSkill = Mathf.Clamp01(commandFactor) * 0.5f + Mathf.Clamp01(moraleFactor) * 0.3f; // 指揮・士気で逃げ切る
            return Mathf.Clamp01(baseChance - escapeSkill * baseChance);
        }

        /// <summary>このとき捕虜化するか（roll が確率を下回れば捕縛）。</summary>
        public static bool IsCaptured(bool encircled, float commandFactor, float moraleFactor, float roll)
            => roll < CaptureChance(encircled, commandFactor, moraleFactor);

        /// <summary>人物を捕虜にする（自由→捕虜・捕獲勢力を記録）。既に捕虜/故人なら false。席の空席化は <see cref="VacancyRules"/>。</summary>
        public static bool Capture(Person person, Faction captor, int year)
        {
            if (person == null || person.IsDeceased || person.captiveStatus != CaptiveStatus.自由) return false;
            person.captiveStatus = CaptiveStatus.捕虜;
            person.heldBy = captor;
            return true;
        }

        /// <summary>解放する（捕虜→自由）。元勢力へ復帰＝再任用可。捕虜でなければ false。</summary>
        public static bool Release(Person person)
        {
            if (person == null || person.captiveStatus != CaptiveStatus.捕虜) return false;
            person.captiveStatus = CaptiveStatus.自由;
            person.heldBy = default;
            return true;
        }

        /// <summary>処断する（捕虜→処断済＋死亡＝#152 へ合流）。捕虜でなければ false。</summary>
        public static bool Execute(Person person, int year)
        {
            if (person == null || person.captiveStatus != CaptiveStatus.捕虜) return false;
            person.captiveStatus = CaptiveStatus.処断済;
            LifecycleRules.Kill(person, year);
            return true;
        }

        /// <summary>登用する（捕虜→自由＋自陣営へ寝返り）。成立条件は <see cref="RecruitChance"/> で別途判定。捕虜でなければ false。</summary>
        public static bool Recruit(Person person, Faction newFaction)
        {
            if (person == null || person.captiveStatus != CaptiveStatus.捕虜) return false;
            person.captiveStatus = CaptiveStatus.自由;
            person.faction = newFaction;
            person.heldBy = default;
            return true;
        }

        /// <summary>
        /// 登用（寝返り）の成立確率（0..1）。思想差が小さく・厚遇（処遇度）が高いほど成立しやすいが、基本は稀。
        /// </summary>
        public static float RecruitChance(float ideologyDistance, float treatment)
        {
            float d = Mathf.Clamp01(ideologyDistance);
            float t = Mathf.Clamp01(treatment);
            return Mathf.Clamp01((1f - d) * 0.4f * t); // 最大でも0.4＝離反は稀
        }

        /// <summary>政体（文民統制型 #145/#117）ごとの AI 既定処遇傾向。プレイヤーは手動選択。</summary>
        public static CaptiveDisposition DefaultDisposition(CivilianControlType control)
        {
            switch (control)
            {
                case CivilianControlType.君主統帥: return CaptiveDisposition.解放; // 王党派＝騎士道・厚遇
                case CivilianControlType.党軍:     return CaptiveDisposition.処断; // 共産＝粛清・処断
                case CivilianControlType.軍部優位: return CaptiveDisposition.解放; // 軍閥＝身代金/取引（解放）
                case CivilianControlType.文民統制: return CaptiveDisposition.解放; // 民主派＝捕虜の権利・裁判
                case CivilianControlType.未分化:   return CaptiveDisposition.処断; // 首長制＝戦利品的
                default: return CaptiveDisposition.解放;
            }
        }

        /// <summary>
        /// 処断の政治的代償（支持#113 等への負）。著名（fame 0..1）な提督ほど波紋が大きい。係数 #106 想定。
        /// </summary>
        public static float ExecutionSupportPenalty(float fame)
            => Mathf.Clamp01(fame) * 0.3f; // 著名提督の処断は支持を最大30%削る
    }
}
