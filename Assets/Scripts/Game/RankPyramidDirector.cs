using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 階級ピラミッドの Game 層配線（TKO-13 #2490・Strategy 自動生成）。勢力ごとの階級別人数（<see cref="MilitaryRankRegistry"/>）を
    /// 初期ピラミッドで種付けし、年境界（<see cref="GameClock.ElapsedSeconds"/>÷年秒で自前検出＝`GalaxyView` 非編集）ごとに
    /// <see cref="RankFlowRules.TickYear"/>（徴募→損耗→定員空きへ昇進）で回す。<see cref="CanPromote"/> は「その階級に定員空きがあるか」＝
    /// 個人昇進（<see cref="ProtagonistCareerDirector"/>）の<b>定員ゲート</b>＝上ほど狭き門。数式・状態遷移は Core 窓口へ委譲（配線のみ・additive）。
    /// </summary>
    public class RankPyramidDirector : MonoBehaviour
    {
        [Header("ピラミッド（調整値）")]
        [Tooltip("1勢力あたりの初期総兵力（名目・人数）")]
        public int nominalForcePerFaction = 120000;
        [Tooltip("初期分布を理想定員の何割で種付けするか（<1で各階級に空きを残す＝昇進余地）")]
        [Range(0.3f, 1f)] public float seedFill = 0.8f;
        [Tooltip("年あたりの新兵流入（最下級へ）")]
        public int annualIntake = 7000;

        private const float SecondsPerYear = 60f * 30f * 12f; // 年秒（既定 DateParams）
        public static RankPyramidDirector Instance { get; private set; }

        private static readonly RankDistributionRules.PyramidParams Pyramid = RankDistributionRules.PyramidParams.Default;
        private static readonly RankFlowRules.FlowParams Flow = RankFlowRules.FlowParams.Default;

        private int lastYear;
        private bool seeded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy") return;
            if (Object.FindAnyObjectByType<RankPyramidDirector>() != null) return;
            new GameObject("RankPyramidDirector").AddComponent<RankPyramidDirector>();
        }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (StrategySession.Campaign?.states == null) return;
            if (!seeded) { Seed(); seeded = true; }

            GameClock clock = StrategySession.Clock;
            if (clock == null) return;
            int nowYear = Mathf.FloorToInt((float)clock.ElapsedSeconds / SecondsPerYear);
            int guard = 0;
            while (lastYear < nowYear && guard++ < 8)
            {
                lastYear++;
                TickAll();
            }
        }

        private void Seed()
        {
            var states = StrategySession.Campaign.states;
            int[] target = RankDistributionRules.PyramidTarget(Mathf.Max(0, nominalForcePerFaction), Pyramid);
            for (int s = 0; s < states.Count; s++)
            {
                var st = states[s];
                if (st == null) continue;
                var d = MilitaryRankRegistry.Get(st.faction);
                if (RankDistributionRules.TotalForce(d) > 0) continue; // 既に種付け済み
                for (int i = 0; i < target.Length; i++)
                    d.Set((MilitaryGrade)i, Mathf.RoundToInt(target[i] * seedFill));
            }
            GameClock clock = StrategySession.Clock;
            lastYear = clock != null ? Mathf.FloorToInt((float)clock.ElapsedSeconds / SecondsPerYear) : 0;
        }

        private void TickAll()
        {
            var factions = new List<Faction>(MilitaryRankRegistry.Factions);
            for (int i = 0; i < factions.Count; i++)
                RankFlowRules.TickYear(MilitaryRankRegistry.Get(factions[i]), annualIntake, Pyramid, Flow);
        }

        /// <summary>
        /// その勢力でその階級 tier（少尉1〜元帥10）へ昇進する定員空きがあるか＝個人昇進のゲート。
        /// 各階級に最低1枠（最上位も1人ぶん）。分布未整備（総数0）なら律速しない（true）。
        /// </summary>
        public bool CanPromote(Faction faction, int toTier)
        {
            var d = MilitaryRankRegistry.Get(faction);
            int total = RankDistributionRules.TotalForce(d);
            if (total <= 0) return true;
            int[] target = RankDistributionRules.PyramidTarget(total, Pyramid);
            MilitaryGrade grade = RankDistributionRules.GradeForOfficerTier(toTier);
            int cap = Mathf.Max(1, target[(int)grade]); // 各階級に最低1枠＝最上位も到達可
            return d.Get(grade) < cap;
        }
    }
}
