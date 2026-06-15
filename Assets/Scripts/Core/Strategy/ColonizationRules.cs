using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 入植のミッション状態（#129）。入植艦が対象星系へ向かい、到着後に成立まで進捗する。純データ。
    /// 入植艦は成立で使い切り（呼び出し側＝戦略レイヤーが艦を消費する。純ロジックは艦を持たない）。
    /// </summary>
    public class ColonyMission
    {
        public int targetSystemId;
        public Faction faction;
        public FactionData factionData;

        /// <summary>入植元の政体思想（住民思想 <see cref="Province.nativeIdeology"/> として発生する）。</summary>
        public string colonistIdeology = "";

        /// <summary>成立までの経過（0..buildTime）。</summary>
        public float progress;

        public ColonyMission() { }

        public ColonyMission(int targetSystemId, Faction faction, FactionData factionData = null, string colonistIdeology = "")
        {
            this.targetSystemId = targetSystemId;
            this.faction = faction;
            this.factionData = factionData;
            this.colonistIdeology = colonistIdeology ?? "";
        }
    }

    /// <summary>
    /// 入植の純ロジック（#129・唯一の窓口）。未入植かつ居住可能で探索済みの星系へ入植し、<b>1惑星→銀河へ拡張</b>（#117）する。
    /// 入植成立で星系を自勢力支配に変え、内政対象（<see cref="Province"/> #109）を生成する＝入植直後は小規模・未統合・低安定
    /// （<see cref="GovernanceRules.OnOccupied"/> の初期化窓口を通す＝時間で統合・安定）。住民思想は入植元の政体に従って発生。
    /// 居住性タイプ（L-1）・距離/ZOC 制約・探索（G-2 #119）は呼び出し側が事実を渡す（依存を作らない）。test-first。
    /// </summary>
    public static class ColonizationRules
    {
        /// <summary>入植の調整値。</summary>
        public readonly struct ColonizationParams
        {
            /// <summary>到着後、入植成立までの時間（戦略秒/ターン）。</summary>
            public readonly float buildTime;
            /// <summary>入植直後の初期人口（小規模）。</summary>
            public readonly float initialPopulation;

            public ColonizationParams(float buildTime, float initialPopulation)
            {
                this.buildTime = Mathf.Max(0f, buildTime);
                this.initialPopulation = Mathf.Max(0f, initialPopulation);
            }

            /// <summary>既定＝成立まで30・初期人口20（小規模）。</summary>
            public static ColonizationParams Default => new ColonizationParams(30f, 20f);
        }

        /// <summary>
        /// その星系に入植できるか（#129）。未入植（<see cref="StarSystem.isColonized"/>=false）かつ居住可能かつ探索済み。
        /// 探索済み判定は G-2 #119 が未実装のため <paramref name="explored"/> で受け取る。
        /// </summary>
        public static bool CanColonize(StarSystem target, bool explored)
        {
            return target != null && explored && target.habitable && !target.isColonized;
        }

        /// <summary>入植ミッションを dt 進める（到着後の成立進捗）。</summary>
        public static void Tick(ColonyMission mission, float dt, ColonizationParams p)
        {
            if (mission == null || dt <= 0f) return;
            mission.progress = Mathf.Min(p.buildTime, mission.progress + dt);
        }

        /// <summary>入植が成立したか（進捗が buildTime に達した）。</summary>
        public static bool IsComplete(ColonyMission mission, ColonizationParams p)
            => mission != null && mission.progress >= p.buildTime;

        /// <summary>
        /// 入植を成立させる：星系を入植勢力の支配に変え、内政対象 <see cref="Province"/> を生成して返す。
        /// 既に入植済みの星系は不可（null）。初期状態は <see cref="GovernanceRules.OnOccupied"/> で未統合・低安定にする。
        /// 住民思想は <see cref="ColonyMission.colonistIdeology"/>（入植元の政体）として発生。
        /// </summary>
        public static Province Establish(StarSystem target, ColonyMission mission, ColonizationParams p)
        {
            if (target == null || mission == null) return null;
            if (target.isColonized || !target.habitable) return null; // 二重入植・不毛は不可

            target.owner = mission.faction;
            target.ownerData = mission.factionData;
            target.isColonized = true;

            var province = new Province(target.id, mission.colonistIdeology, p.initialPopulation);
            GovernanceRules.OnOccupied(province); // 入植直後＝未統合・低安定（占領と同じ初期化窓口・時間で統合）
            return province;
        }

        /// <summary>
        /// 入植を即時成立させる簡易窓口（条件を満たす前提）。未入植・居住可能なら星系を支配化し Province を返す。
        /// </summary>
        public static Province Colonize(StarSystem target, Faction faction, FactionData factionData, string colonistIdeology, ColonizationParams p)
        {
            if (!CanColonize(target, explored: true)) return null;
            var mission = new ColonyMission(target.id, faction, factionData, colonistIdeology) { progress = p.buildTime };
            return Establish(target, mission, p);
        }
    }
}
