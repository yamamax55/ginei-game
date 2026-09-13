using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>決裁の執行が実際にどうなったか。</summary>
    public enum PetitionActionOutcome
    {
        /// <summary>実行できた（盤面が動いた）。</summary>
        実行,
        /// <summary>対象が見つからない（攻める相手・守る惑星・交戦中の相手が居ない）。</summary>
        対象なし,
        /// <summary>資源が足りない（国庫・造船所）。</summary>
        資源不足,
        /// <summary>使える艦隊が無い（全部が移動中・交戦中）。</summary>
        手が空いていない,
        /// <summary>相手が拒んだ（講和）。</summary>
        相手が拒否,
        /// <summary>盤面が無い／効果キーが未対応。</summary>
        対象外,
    }

    /// <summary>
    /// 決裁を執行した結果（決裁後に画面へ出す「実際に何が起きたか」）。
    /// <b>承認と執行成功は別</b>＝承認しても <see cref="ok"/> が false になりうる。
    /// </summary>
    public readonly struct PetitionActionResult
    {
        public readonly PetitionActionOutcome outcome;
        /// <summary>1行の説明（実際の変更値・投入艦隊・命令先・外交結果、または実行できない理由）。</summary>
        public readonly string detail;
        /// <summary>動いた量（発注隻数・投入艦隊数・防御回復量など。表示用・意味は効果ごと）。</summary>
        public readonly float amount;
        /// <summary>国庫から実際に引かれた額。</summary>
        public readonly float spent;

        public PetitionActionResult(PetitionActionOutcome outcome, string detail, float amount = 0f, float spent = 0f)
        {
            this.outcome = outcome;
            this.detail = detail ?? "";
            this.amount = amount;
            this.spent = spent;
        }

        /// <summary>盤面が実際に動いたか。</summary>
        public bool ok => outcome == PetitionActionOutcome.実行;

        public static PetitionActionResult Fail(PetitionActionOutcome outcome, string detail)
            => new PetitionActionResult(outcome, detail);
    }

    /// <summary>
    /// 決裁の執行が触れる盤面（Core だけで完結する参照の束）。
    /// Game 層はこれを組んで <see cref="PetitionActionRules"/> に渡す＝ルール側が MonoBehaviour を知らない。
    /// </summary>
    public sealed class PetitionActionContext
    {
        public CampaignState campaign;
        public GalaxyMap map;
        public StrategicFleetRegistry fleets;
        public Faction faction;
        /// <summary>この勢力の造船所（動員の発注先）。空なら発注できない。</summary>
        public List<Shipyard> shipyards;
        /// <summary>外交状態（講和の反映先）。null なら講和は実行できない。</summary>
        public DiplomacyState diplomacy;
        /// <summary>参謀本部の実力 0..1（攻勢の計画に使う）。</summary>
        public float staffCompetence = 0.5f;

        /// <summary>
        /// 講和の受諾判定に戦争台帳（<see cref="WarLedger"/>）の厭戦を使うか。
        /// false なら受諾度を判定できないものとして<b>拒否側へ倒す</b>（勝手に停戦させない）。
        /// </summary>
        public bool useWarLedger = true;

        /// <summary>この勢力の国家状態（無ければ null）。</summary>
        public FactionState State()
        {
            if (campaign == null || campaign.states == null) return null;
            for (int i = 0; i < campaign.states.Count; i++)
                if (campaign.states[i] != null && campaign.states[i].faction == faction) return campaign.states[i];
            return null;
        }
    }

    /// <summary>
    /// 決裁の<b>実際のゲームへの接続</b>（作業票③）。
    ///
    /// <b>これが要る理由</b>：従来の <see cref="PetitionEffects"/> は <see cref="FactionState"/> しか触れないので、
    /// 「全軍動員」も「大攻勢」も国庫と民心が動くだけだった（艦は増えず、誰も出撃せず、戦争も終わらない）。
    /// カードの文面と実効果が食い違うので、<b>盤面まで届く執行</b>をここに置く。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item><b>架空の増艦をしない</b>。動員は既存の建艦パイプライン（造船所へ発注→完成で
    ///   <see cref="ShipyardRules.CommissionToPool"/>）に乗せ、国庫から代金を払う。</item>
    ///   <item>攻勢は既存の作戦計画（<see cref="MissionCommandRules.PlanMission"/>）と移動命令
    ///   （<see cref="StrategicFleet.WarpTo"/>）を使う＝新しい命令系統を作らない。</item>
    ///   <item>防衛は実戦に効く既存の守備要素（<see cref="Planet.orbitalDefense"/>）へ積む。</item>
    ///   <item>講和は外交状態（<see cref="DiplomacyRules.MakePeace"/>）を実際に平時へ戻す。
    ///   相手が受け入れなければ<b>失敗として返す</b>。</item>
    ///   <item>対象不在・艦隊が出払っている・国庫不足・相手の拒否は<b>無効果の成功にしない</b>
    ///   ＝理由を返して画面に出す。</item>
    /// </list>
    ///
    /// 乱数なし・決定論（受諾判定も既存ルールの数値で決める）。
    /// </summary>
    public static class PetitionActionRules
    {
        /// <summary>動員1回で発注する艦の数（満額）。magnitude で割り引く。</summary>
        public const int MobilizeOrdersFull = 4;

        /// <summary>攻勢1回で動員する艦隊数の上限（満額）。</summary>
        public const int OffensiveFleetsFull = 3;

        /// <summary>防衛1回で回復する制空値（満額）。</summary>
        public const float DefendRestoreFull = 40f;

        /// <summary>防衛1回の費用（満額・国庫）。</summary>
        public const float DefendCostFull = 30f;

        /// <summary>講和を相手が受け入れる厭戦の閾値（これ以上なら受諾）。</summary>
        public const float PeaceAcceptThreshold = 0.5f;

        /// <summary>この効果キーが盤面まで届く執行を持つか。</summary>
        public static bool IsActionKey(string effectKey)
        {
            switch (effectKey)
            {
                case "mil.mobilize":
                case "mil.offensive":
                case "mil.defend":
                case "diplo.ceasefire":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 効果キーに応じて盤面を動かす。<paramref name="magnitude"/> は官僚の執行忠実度（0..1）。
        /// 盤面まで届く効果でなければ <see cref="PetitionActionOutcome.対象外"/>。
        /// 対象を名指ししない旧経路（<see cref="PetitionTarget.None"/>）は執行時に対象を選ぶ。
        /// </summary>
        public static PetitionActionResult Execute(string effectKey, PetitionActionContext ctx, float magnitude)
            => Execute(effectKey, ctx, magnitude, PetitionTarget.None);

        /// <summary>
        /// 提案の時点で名指しした対象（<paramref name="target"/>）を使って執行する。
        ///
        /// ★<b>承認した内容と違うことをしない</b>：対象が失われていたら<b>別の対象へ振り替えず</b>
        /// 「対象なし」で失敗を返す。決裁前に見せた対象と実際に動く対象を必ず一致させる。
        /// </summary>
        public static PetitionActionResult Execute(string effectKey, PetitionActionContext ctx,
                                                   float magnitude, in PetitionTarget target)
        {
            if (ctx == null) return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "盤面がありません");
            magnitude = Mathf.Clamp01(magnitude);

            switch (effectKey)
            {
                case "mil.mobilize": return Mobilize(ctx, magnitude);
                case "mil.offensive": return Offensive(ctx, magnitude, target);
                case "mil.defend": return Defend(ctx, magnitude, target);
                case "diplo.ceasefire": return Ceasefire(ctx, magnitude, target);
                default:
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "この決裁に盤面の執行はありません");
            }
        }

        // ===== 提案の時点で対象を決める（決裁前に見せるため） =====

        /// <summary>
        /// その効果キーの対象を<b>いま</b>選ぶ（提案の時点で固定する用）。
        /// 選べなければ <see cref="PetitionTarget.None"/>＝対象不在なので、そもそも建白しない判断ができる。
        /// </summary>
        public static PetitionTarget PlanTarget(string effectKey, PetitionActionContext ctx)
        {
            if (ctx == null) return PetitionTarget.None;
            switch (effectKey)
            {
                case "mil.offensive":
                    {
                        int id = PickOffensiveTarget(ctx, IdleFleets(ctx));
                        StarSystem s = id >= 0 && ctx.map != null ? ctx.map.GetSystem(id) : null;
                        return s == null ? PetitionTarget.None
                            : new PetitionTarget(PetitionTargetKind.星系, s.id, SystemName(s));
                    }
                case "mil.defend":
                    {
                        StarSystem s = PickWeakestPlanetSystem(ctx);
                        return s == null ? PetitionTarget.None
                            : new PetitionTarget(PetitionTargetKind.惑星, s.id, SystemName(s));
                    }
                case "diplo.ceasefire":
                    {
                        string enemy = ctx.diplomacy != null ? FindWarOpponent(ctx, ctx.faction.ToString()) : "";
                        if (string.IsNullOrEmpty(enemy)) return PetitionTarget.None;
                        return new PetitionTarget(PetitionTargetKind.勢力, FactionIndexOf(enemy), enemy);
                    }
                default:
                    return PetitionTarget.None;   // 税・動員は勢力全体に効く（名指ししない）
            }
        }

        private static int FactionIndexOf(string name)
        {
            var all = (Faction[])System.Enum.GetValues(typeof(Faction));
            for (int i = 0; i < all.Length; i++) if (all[i].ToString() == name) return (int)all[i];
            return 0;
        }

        private static string SystemName(StarSystem s)
            => s == null ? "" : (string.IsNullOrEmpty(s.systemName) ? ("#" + s.id) : s.systemName);

        // ===== 動員＝造船所へ実際に発注する（架空の増艦をしない） =====

        /// <summary>
        /// 国庫の範囲で造船所へ建艦を発注する。完成すれば既存の経路で艦艇プールへ入る
        /// （<see cref="ShipyardRules.CommissionToPool"/>）＝<b>その場で艦は増えない</b>。
        /// 造船所が無い／国庫が足りなければ失敗として返す。
        /// </summary>
        public static PetitionActionResult Mobilize(PetitionActionContext ctx, float magnitude)
        {
            FactionState fs = ctx.State();
            if (fs == null) return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "勢力の国家状態がありません");
            if (ctx.shipyards == null || ctx.shipyards.Count == 0)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "発注できる造船所がありません");

            int want = Mathf.Max(1, Mathf.RoundToInt(MobilizeOrdersFull * magnitude));
            float unit = ShipyardRules.Cost(ShipClass.巡航艦, ShipRole.戦闘艦);
            if (unit <= 0f) unit = 1f;

            int placed = 0;
            float spent = 0f;
            int yardIndex = 0;
            for (int i = 0; i < want; i++)
            {
                // 国庫で払えるぶんだけ発注する（足りなければそこで打ち切り＝ツケで作らない）。
                if (fs.treasury < spent + unit) break;

                Shipyard yard = NextYard(ctx.shipyards, ref yardIndex);
                if (yard == null) break;
                if (ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦) == null) break;

                placed++;
                spent += unit;
            }

            if (placed == 0)
                return PetitionActionResult.Fail(PetitionActionOutcome.資源不足,
                    $"国庫が足りず発注できません（1隻 {unit:0} / 国庫 {fs.treasury:0}）");

            fs.treasury -= spent;
            return new PetitionActionResult(PetitionActionOutcome.実行,
                $"造船所へ {placed} 隻を発注（国庫 -{spent:0}）。完成した艦から艦艇プールへ入ります",
                placed, spent);
        }

        private static Shipyard NextYard(List<Shipyard> yards, ref int index)
        {
            for (int n = 0; n < yards.Count; n++)
            {
                Shipyard y = yards[index % yards.Count];
                index++;
                if (y != null) return y;
            }
            return null;
        }

        // ===== 攻勢＝実在の艦隊に実在の目標へ進軍を命じる =====

        /// <summary>
        /// 敵星系を1つ選び、手の空いている自軍艦隊に進軍を命じる。
        /// 計画は既存の <see cref="MissionCommandRules.PlanMission"/>、移動は <see cref="StrategicFleet.WarpTo"/>
        /// ＝別の命令系統を作らない。手が空いている艦隊が無ければ失敗。
        /// </summary>
        public static PetitionActionResult Offensive(PetitionActionContext ctx, float magnitude)
            => Offensive(ctx, magnitude, PetitionTarget.None);

        /// <summary><inheritdoc cref="Offensive(PetitionActionContext,float)"/></summary>
        public static PetitionActionResult Offensive(PetitionActionContext ctx, float magnitude,
                                                     in PetitionTarget target)
        {
            if (ctx.map == null || ctx.fleets == null || ctx.fleets.fleets == null)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "盤面がありません");

            List<StrategicFleet> idle = IdleFleets(ctx);
            if (idle.Count == 0)
                return PetitionActionResult.Fail(PetitionActionOutcome.手が空いていない,
                    "動かせる艦隊がありません（全隊が交戦中か移動中）");

            // ★提案時に名指しした星系があれば<b>それだけ</b>を使う（別の対象へ勝手に振り替えない）。
            int targetId;
            if (target.HasTarget && target.kind == PetitionTargetKind.星系)
            {
                StarSystem named = ctx.map.GetSystem(target.id);
                if (named == null)
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし,
                        $"提案した目標（{target.name}）が盤面にありません");
                if (!FactionRelations.IsHostile(null, ctx.faction, named.ownerData, named.owner))
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし,
                        $"提案した目標（{target.name}）はもう敵地ではありません");
                targetId = named.id;
            }
            else
            {
                targetId = PickOffensiveTarget(ctx, idle);
                if (targetId < 0)
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "攻める先の敵星系がありません");
            }

            int limit = Mathf.Clamp(Mathf.RoundToInt(OffensiveFleetsFull * magnitude), 1, idle.Count);
            idle.Sort((a, b) => b.strength.CompareTo(a.strength));   // 大きい隊から（決定論）

            int sent = 0;
            var names = new List<int>();
            for (int i = 0; i < idle.Count && sent < limit; i++)
            {
                if (!idle[i].WarpTo(ctx.map, targetId)) continue;
                names.Add(idle[i].id);
                sent++;
            }
            if (sent == 0)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "その星系へ至る経路がありません");

            StarSystem hit = ctx.map.GetSystem(targetId);
            string tname = SystemName(hit);
            return new PetitionActionResult(PetitionActionOutcome.実行,
                $"{tname} へ {sent} 隊が進撃（{FleetList(names)}）", sent);
        }

        /// <summary>手の空いている自軍艦隊（停泊中・非交戦・増援航行中でない）。</summary>
        private static List<StrategicFleet> IdleFleets(PetitionActionContext ctx)
        {
            var idle = new List<StrategicFleet>();
            if (ctx?.fleets?.fleets == null) return idle;
            for (int i = 0; i < ctx.fleets.fleets.Count; i++)
            {
                StrategicFleet f = ctx.fleets.fleets[i];
                if (f == null || f.faction != ctx.faction) continue;
                if (f.engaged || f.IsOnCorridor || f.warpingAsReinforcement) continue;
                if (f.strength <= 0) continue;
                idle.Add(f);
            }
            return idle;
        }

        /// <summary>いちばん制空が削られている自勢力の惑星がある星系（無ければ null）。</summary>
        private static StarSystem PickWeakestPlanetSystem(PetitionActionContext ctx)
        {
            if (ctx?.map?.systems == null) return null;
            StarSystem worst = null;
            float worstGap = 0f;
            for (int i = 0; i < ctx.map.systems.Count; i++)
            {
                StarSystem s = ctx.map.systems[i];
                if (s?.planet == null || s.planet.owner != ctx.faction) continue;
                float gap = s.planet.maxOrbitalDefense - s.planet.orbitalDefense;
                if (gap <= 0.01f) continue;
                if (gap > worstGap) { worstGap = gap; worst = s; }
            }
            return worst;
        }

        /// <summary>
        /// 攻める先＝手の空いている艦隊のいずれかから<b>経路がある</b>敵対星系のうち、最も近いもの。
        /// 経路が無い相手は選ばない（無効果の成功を作らない）。
        /// </summary>
        private static int PickOffensiveTarget(PetitionActionContext ctx, List<StrategicFleet> idle)
        {
            int best = -1;
            int bestHops = int.MaxValue;
            if (ctx.map.systems == null) return -1;

            for (int i = 0; i < ctx.map.systems.Count; i++)
            {
                StarSystem s = ctx.map.systems[i];
                if (s == null) continue;
                if (!FactionRelations.IsHostile(null, ctx.faction, s.ownerData, s.owner)) continue;

                for (int k = 0; k < idle.Count; k++)
                {
                    List<int> path = GalaxyPathfinder.FindPath(ctx.map, idle[k].currentSystemId, s.id);
                    if (path == null || path.Count < 2) continue;
                    int hops = path.Count - 1;
                    if (hops < bestHops || (hops == bestHops && s.id < best)) { bestHops = hops; best = s.id; }
                }
            }
            return best;
        }

        private static string FleetList(List<int> ids)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append('、');
                sb.Append('第').Append(ids[i]).Append("艦隊");
            }
            return sb.ToString();
        }

        // ===== 防衛＝実戦に効く守備（制空値）を積む =====

        /// <summary>
        /// 自勢力の惑星のうち<b>最も削られている</b>ものの制空値を回復する（上限まで）。
        /// これは攻城戦で実際に消費される値（<see cref="Planet.orbitalDefense"/>）＝表示だけの強化ではない。
        /// 国庫が足りない／削られている惑星が無ければ失敗。
        /// </summary>
        public static PetitionActionResult Defend(PetitionActionContext ctx, float magnitude)
            => Defend(ctx, magnitude, PetitionTarget.None);

        /// <summary><inheritdoc cref="Defend(PetitionActionContext,float)"/></summary>
        public static PetitionActionResult Defend(PetitionActionContext ctx, float magnitude,
                                                  in PetitionTarget target)
        {
            FactionState fs = ctx.State();
            if (fs == null || ctx.map == null || ctx.map.systems == null)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "盤面がありません");

            StarSystem worstSystem;
            if (target.HasTarget && target.kind == PetitionTargetKind.惑星)
            {
                // ★提案時に名指しした惑星だけを固める（別の惑星へ勝手に振り替えない）。
                worstSystem = ctx.map.GetSystem(target.id);
                if (worstSystem?.planet == null || worstSystem.planet.owner != ctx.faction)
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし,
                        $"提案した惑星（{target.name}）はもう自勢力の守備対象ではありません");
                if (worstSystem.planet.maxOrbitalDefense - worstSystem.planet.orbitalDefense <= 0.01f)
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし,
                        $"提案した惑星（{target.name}）の制空はすでに満ちています");
            }
            else worstSystem = PickWeakestPlanetSystem(ctx);

            Planet worst = worstSystem?.planet;
            if (worst == null)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "守りを固める余地のある惑星がありません");
            float worstGap = worst.maxOrbitalDefense - worst.orbitalDefense;

            float cost = DefendCostFull * magnitude;
            if (fs.treasury < cost)
                return PetitionActionResult.Fail(PetitionActionOutcome.資源不足,
                    $"国庫が足りません（必要 {cost:0} / 国庫 {fs.treasury:0}）");

            float restore = Mathf.Min(worstGap, DefendRestoreFull * magnitude);
            worst.orbitalDefense = Mathf.Min(worst.maxOrbitalDefense, worst.orbitalDefense + restore);
            fs.treasury -= cost;

            string name = worstSystem != null && !string.IsNullOrEmpty(worstSystem.systemName)
                ? worstSystem.systemName : ("#" + worst.systemId);
            return new PetitionActionResult(PetitionActionOutcome.実行,
                $"{name} の制空を +{restore:0} 回復（{worst.orbitalDefense:0}/{worst.maxOrbitalDefense:0}・国庫 -{cost:0}）",
                restore, cost);
        }

        // ===== 講和＝外交状態を実際に平時へ戻す（相手が受ければ） =====

        /// <summary>
        /// 交戦中の相手と講和する。相手の受諾は厭戦（<see cref="WarState.PeaceAcceptanceFor"/>）で決め、
        /// 足りなければ<b>拒否として失敗を返す</b>（勝手に停戦させない）。
        /// </summary>
        public static PetitionActionResult Ceasefire(PetitionActionContext ctx, float magnitude)
            => Ceasefire(ctx, magnitude, PetitionTarget.None);

        /// <summary><inheritdoc cref="Ceasefire(PetitionActionContext,float)"/></summary>
        public static PetitionActionResult Ceasefire(PetitionActionContext ctx, float magnitude,
                                                     in PetitionTarget target)
        {
            if (ctx.diplomacy == null)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "外交状態がありません");

            string me = ctx.faction.ToString();
            string enemy;
            if (target.HasTarget && target.kind == PetitionTargetKind.勢力)
            {
                // ★提案時に名指しした相手とだけ講和する（別の相手へ勝手に振り替えない）。
                enemy = target.name;
                if (string.IsNullOrEmpty(enemy) || enemy == me)
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "提案した講和相手が不正です");
                if (ctx.diplomacy.Status(me, enemy) != DiplomacyState.DiplomaticStatus.交戦)
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象なし,
                        $"提案した相手（{enemy}）とはもう交戦していません");
            }
            else enemy = FindWarOpponent(ctx, me);

            if (string.IsNullOrEmpty(enemy))
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "交戦中の相手がいません");

            // 相手の受諾（厭戦が閾値以上なら受ける）。台帳が無ければ受諾度を判定できないので拒否側に倒す。
            float acceptance = PeaceAcceptanceOfOpponent(ctx, me, enemy);
            if (acceptance < PeaceAcceptThreshold)
                return PetitionActionResult.Fail(PetitionActionOutcome.相手が拒否,
                    $"{enemy} は講和を拒みました（受諾度 {acceptance:0.00} < {PeaceAcceptThreshold:0.00}）");

            if (!DiplomacyRules.MakePeace(ctx.diplomacy, me, enemy))
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "交戦状態ではありません");

            return new PetitionActionResult(PetitionActionOutcome.実行,
                $"{enemy} と講和が成立（交戦 → 平時）", 1f);
        }

        /// <summary>交戦中の相手を1つ返す（決定論＝勢力名の並び順）。無ければ空文字。</summary>
        private static string FindWarOpponent(PetitionActionContext ctx, string me)
        {
            var names = new List<string>();
            var all = (Faction[])System.Enum.GetValues(typeof(Faction));
            for (int i = 0; i < all.Length; i++)
            {
                string other = all[i].ToString();
                if (other == me) continue;
                names.Add(other);
            }
            names.Sort(string.CompareOrdinal);

            for (int i = 0; i < names.Count; i++)
                if (ctx.diplomacy.Status(me, names[i]) == DiplomacyState.DiplomaticStatus.交戦) return names[i];
            return "";
        }

        /// <summary>相手側の講和受諾度（戦争台帳があれば厭戦から、無ければ 0＝拒否側）。</summary>
        private static float PeaceAcceptanceOfOpponent(PetitionActionContext ctx, string me, string enemy)
        {
            if (!ctx.useWarLedger) return 0f;
            WarState w = WarLedger.Get(me, enemy);
            if (w == null) return 0f;
            bool opponentIsA = string.CompareOrdinal(w.factionA, enemy) == 0;
            return WarStateRules.PeaceAcceptanceFor(w, opponentIsA, WarGoalRules.WarGoalParams.Default);
        }
    }
}
