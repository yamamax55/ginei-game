using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>同一回廊で敵対艦隊が遭遇した組（会戦トリガー・C-3への布石）。</summary>
    public readonly struct FleetEncounter
    {
        public readonly StrategicFleet a;
        public readonly StrategicFleet b;
        public FleetEncounter(StrategicFleet a, StrategicFleet b) { this.a = a; this.b = b; }
    }

    /// <summary>回廊戦闘の結果（C-3）。攻撃側勝利かと、勝者の残存兵力。</summary>
    public readonly struct CorridorBattleResult
    {
        public readonly bool attackerWon;
        public readonly int survivorStrength;
        public CorridorBattleResult(bool attackerWon, int survivorStrength)
        { this.attackerWon = attackerWon; this.survivorStrength = survivorStrength; }
    }

    /// <summary>
    /// 自動解決した回廊会戦の結末（通知・ピン留め用）。発生回廊（無向の {min,max} 星系）・勝者/敗者勢力・勝者残存兵力。
    /// </summary>
    public readonly struct EncounterOutcome
    {
        public readonly int sysMin;
        public readonly int sysMax;
        public readonly Faction winner;
        public readonly Faction loser;
        public readonly int survivorStrength;
        public EncounterOutcome(int sysMin, int sysMax, Faction winner, Faction loser, int survivorStrength)
        {
            this.sysMin = sysMin;
            this.sysMax = sysMax;
            this.winner = winner;
            this.loser = loser;
            this.survivorStrength = survivorStrength;
        }
    }

    /// <summary>回廊要塞への力攻めの結末（#40）。要塞を陥落させたかと、攻撃側の残存兵力。</summary>
    public readonly struct FortressAssaultResult
    {
        public readonly bool captured;        // 要塞を陥落（制圧）させたか
        public readonly int attackerSurvivor; // 攻撃側の残存兵力
        public FortressAssaultResult(bool captured, int attackerSurvivor)
        {
            this.captured = captured;
            this.attackerSurvivor = attackerSurvivor;
        }
    }

    /// <summary>
    /// 戦略マップの判定ルール（C-1 #34 仕上げ）。会戦トリガー（回廊での敵対遭遇）と
    /// 星系の占領（所有フリップ）の唯一の窓口。敵対判定は必ず FactionRelations 経由。純ロジック。
    /// </summary>
    public static class StrategyRules
    {
        /// <summary>
        /// レジストリ内で「同じ回廊上にいる敵対艦隊の組」をすべて返す（＝回廊会戦の発火条件）。
        /// 両者とも移動中・同一回廊・FactionRelations で敵対、を満たす組のみ。
        /// </summary>
        public static List<FleetEncounter> FindEncounters(StrategicFleetRegistry reg)
        {
            var result = new List<FleetEncounter>();
            if (reg == null) return result;

            var fleets = reg.fleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet a = fleets[i];
                if (a == null || !a.IsOnCorridor) continue;
                for (int j = i + 1; j < fleets.Count; j++)
                {
                    StrategicFleet b = fleets[j];
                    if (b == null || !b.IsOnCorridor) continue;
                    if (!a.IsOnSameCorridor(b)) continue;
                    if (!FactionRelations.IsHostile(null, a.faction, null, b.faction)) continue;
                    result.Add(new FleetEncounter(a, b));
                }
            }
            return result;
        }

        /// <summary>レジストリ内に回廊会戦の発火条件があるか。</summary>
        public static bool AnyEncounter(StrategicFleetRegistry reg) => FindEncounters(reg).Count > 0;

        /// <summary>
        /// 回廊内で実際に接触した最初の敵対艦隊ペアを返す（実会戦の起動条件・C-3）。
        /// 抽象解決ではなく実会戦へ渡したい時に使う。
        /// </summary>
        public static bool TryFindCollision(StrategicFleetRegistry reg, out StrategicFleet a, out StrategicFleet b)
        {
            a = null; b = null;
            if (reg == null) return false;
            foreach (var e in FindEncounters(reg))
            {
                if (e.a == null || e.b == null) continue;
                if (FleetsCollided(e.a, e.b)) { a = e.a; b = e.b; return true; }
            }
            return false;
        }

        /// <summary>
        /// 接触済み（FleetsCollided）の敵対ペアをすべて返す（＝今まさに回廊で交戦している組）。
        /// 二層遷移（C-2 #586）の「交戦中の回廊」検出に使う。
        /// </summary>
        public static List<FleetEncounter> CollidedEncounters(StrategicFleetRegistry reg)
        {
            var list = new List<FleetEncounter>();
            if (reg == null) return list;
            foreach (var e in FindEncounters(reg))
                if (e.a != null && e.b != null && FleetsCollided(e.a, e.b))
                    list.Add(e);
            return list;
        }

        /// <summary>
        /// 接触した敵対ペアを「交戦中（engaged）」にして回廊上で固着させる（C-2 #586）。
        /// 固着した艦は Tick で前進しないため、回廊上に交戦中の回廊として留まる
        /// ＝プレイヤーが潜行（ダブルクリック）するか自動解決するまで動かない。
        /// 新たに固着させたペア数を返す。
        /// </summary>
        public static int BeginEngagements(StrategicFleetRegistry reg)
        {
            int n = 0;
            foreach (var e in CollidedEncounters(reg))
            {
                if (!e.a.engaged || !e.b.engaged) n++;
                e.a.engaged = true;
                e.b.engaged = true;
            }
            return n;
        }

        /// <summary>
        /// 指定回廊（星系 sysA–sysB のエッジ）上で交戦中の敵対ペアを1組返す（C-2 #586 潜行先の特定）。
        /// 見つかれば true＋a/b。回廊の向きは無向（{min,max} 一致で判定）。
        /// </summary>
        public static bool TryGetEngagementOnCorridor(StrategicFleetRegistry reg, int sysA, int sysB,
            out StrategicFleet a, out StrategicFleet b)
        {
            a = null; b = null;
            if (reg == null) return false;
            int min = Mathf.Min(sysA, sysB), max = Mathf.Max(sysA, sysB);
            foreach (var e in CollidedEncounters(reg))
            {
                int eMin = Mathf.Min(e.a.currentSystemId, e.a.destinationSystemId);
                int eMax = Mathf.Max(e.a.currentSystemId, e.a.destinationSystemId);
                if (eMin == min && eMax == max) { a = e.a; b = e.b; return true; }
            }
            return false;
        }

        /// <summary>
        /// 指定回廊（星系 sysA–sysB のエッジ）上で交戦中の<b>全</b>艦隊を2陣営に分けて集める（複数艦隊の会戦・
        /// 接敵内容を会戦へ正しく渡す）。最初に見つかった艦隊を陣営Aの基準とし、それと敵対する艦隊を陣営Bへ、
        /// 非敵対（味方）を陣営Aへ振り分ける。両陣営に1隊以上いれば true。敵対判定は <see cref="FactionRelations"/>。
        /// </summary>
        public static bool GatherEngagementOnCorridor(StrategicFleetRegistry reg, int sysA, int sysB,
            List<StrategicFleet> sideA, List<StrategicFleet> sideB)
        {
            if (sideA == null || sideB == null) return false;
            sideA.Clear(); sideB.Clear();
            if (reg == null || reg.fleets == null) return false;
            int min = Mathf.Min(sysA, sysB), max = Mathf.Max(sysA, sysB);

            StrategicFleet anchor = null;
            foreach (var f in reg.fleets)
            {
                if (f == null || !f.engaged) continue;
                int fMin = Mathf.Min(f.currentSystemId, f.destinationSystemId);
                int fMax = Mathf.Max(f.currentSystemId, f.destinationSystemId);
                if (fMin != min || fMax != max) continue;
                if (anchor == null) anchor = f;
                if (FactionRelations.IsHostile(null, anchor.faction, null, f.faction)) sideB.Add(f);
                else sideA.Add(f);
            }
            return sideA.Count > 0 && sideB.Count > 0;
        }

        /// <summary>
        /// 複数艦隊の会戦（<see cref="BattleHandoff.IsMultiFleet"/>）の結果を戦略レジストリへ反映する。
        /// 勝者側の各艦隊は<b>生存兵力を原戦力比で按分</b>して残し（交戦固着を解除）、敗者側は盤面から除去する。
        /// 反映したら true（Handoff は消す）。
        /// </summary>
        public static bool ApplyMultiHandoffResult(StrategicFleetRegistry reg)
        {
            if (reg == null) return false;
            bool aWon = BattleHandoff.sideAWon;
            int survivor = Mathf.Max(1, BattleHandoff.survivorStrength);
            var entries = BattleHandoff.fleets;

            int winnerTotal = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].sideA == aWon) winnerTotal += Mathf.Max(0, entries[i].strategicStrength);
            if (winnerTotal <= 0) winnerTotal = 1;

            for (int i = 0; i < entries.Count; i++)
            {
                var hf = entries[i];
                StrategicFleet sf = reg.GetFleet(hf.fleetId);
                if (sf == null) continue;
                if (hf.sideA == aWon)
                {
                    int share = Mathf.Max(1, Mathf.RoundToInt(survivor * (Mathf.Max(0, hf.strategicStrength) / (float)winnerTotal)));
                    sf.strength = share;
                    sf.engaged = false;
                }
                else
                {
                    reg.Remove(sf);
                }
            }
            BattleHandoff.Clear();
            return true;
        }

        // ===== 星系上（惑星上）での接敵＝fleet-vs-fleet（回廊の星系版）=====

        /// <summary>
        /// 同一星系に停泊中の敵対艦隊を「交戦中（engaged）」にする＝<b>惑星上などで接敵したら会戦が起きる</b>
        /// （回廊の <see cref="BeginEngagements"/> の星系版）。両者とも停泊中（!IsOnCorridor）で同じ星系・敵対の
        /// ペアを engaged にする。新たに固着させたペア数を返す。停泊艦隊どうしは位置接触判定を要さない（同星系＝接触）。
        /// </summary>
        public static int BeginSystemEngagements(StrategicFleetRegistry reg)
        {
            int n = 0;
            if (reg == null || reg.fleets == null) return 0;
            var fleets = reg.fleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet a = fleets[i];
                if (a == null || a.IsOnCorridor) continue;
                for (int j = i + 1; j < fleets.Count; j++)
                {
                    StrategicFleet b = fleets[j];
                    if (b == null || b.IsOnCorridor) continue;
                    if (a.currentSystemId != b.currentSystemId) continue;
                    if (!FactionRelations.IsHostile(null, a.faction, null, b.faction)) continue;
                    if (!a.engaged || !b.engaged) n++;
                    a.engaged = true; b.engaged = true;
                }
            }
            return n;
        }

        /// <summary>指定星系で交戦中の停泊艦隊を2陣営へ集める（惑星上の会戦・潜行先の特定）。敵対判定で分割。</summary>
        public static bool GatherEngagementAtSystem(StrategicFleetRegistry reg, int systemId,
            List<StrategicFleet> sideA, List<StrategicFleet> sideB)
        {
            if (sideA == null || sideB == null) return false;
            sideA.Clear(); sideB.Clear();
            if (reg == null || reg.fleets == null) return false;
            StrategicFleet anchor = null;
            foreach (var f in reg.fleets)
            {
                if (f == null || f.IsOnCorridor || !f.engaged) continue;
                if (f.currentSystemId != systemId) continue;
                if (anchor == null) anchor = f;
                if (FactionRelations.IsHostile(null, anchor.faction, null, f.faction)) sideB.Add(f);
                else sideA.Add(f);
            }
            return sideA.Count > 0 && sideB.Count > 0;
        }

        /// <summary>
        /// 星系上の交戦（停泊艦隊どうし）を陣営合計戦力で自動解決する（回廊の <see cref="ResolveEncounters"/> の星系版）。
        /// 勝者側は生存兵力を原戦力比で按分して残し（固着解除）、敗者側は除去する。解決した星系数を返す。
        /// </summary>
        public static int ResolveSystemEncounters(StrategicFleetRegistry reg, IList<EncounterOutcome> outcomes,
            System.Func<StrategicFleet, float> factorOf = null)
        {
            if (reg == null || reg.fleets == null) return 0;
            int count = 0;

            var systems = new List<int>();
            foreach (var f in reg.fleets)
                if (f != null && f.engaged && !f.IsOnCorridor && !systems.Contains(f.currentSystemId))
                    systems.Add(f.currentSystemId);

            var sideA = new List<StrategicFleet>();
            var sideB = new List<StrategicFleet>();
            for (int s = 0; s < systems.Count; s++)
            {
                if (!GatherEngagementAtSystem(reg, systems[s], sideA, sideB)) continue;

                int aSum = 0, bSum = 0; float aFac = 0f, bFac = 0f;
                for (int i = 0; i < sideA.Count; i++) { aSum += sideA[i].strength; aFac += factorOf != null ? factorOf(sideA[i]) : 1f; }
                for (int i = 0; i < sideB.Count; i++) { bSum += sideB[i].strength; bFac += factorOf != null ? factorOf(sideB[i]) : 1f; }
                aFac = sideA.Count > 0 ? aFac / sideA.Count : 1f;
                bFac = sideB.Count > 0 ? bFac / sideB.Count : 1f;

                CorridorBattleResult r = ResolveCorridorBattle(aSum, bSum, aFac, bFac);
                bool aWon = r.attackerWon;
                var winners = aWon ? sideA : sideB;
                var losers = aWon ? sideB : sideA;
                int winnerTotal = (aWon ? aSum : bSum);
                if (winnerTotal <= 0) winnerTotal = 1;
                int survivor = Mathf.Max(1, r.survivorStrength);

                Faction winFaction = winners.Count > 0 ? winners[0].faction : Faction.同盟;
                Faction loseFaction = losers.Count > 0 ? losers[0].faction : Faction.帝国;

                for (int i = 0; i < losers.Count; i++) reg.Remove(losers[i]);
                for (int i = 0; i < winners.Count; i++)
                {
                    StrategicFleet w = winners[i];
                    w.strength = Mathf.Max(1, Mathf.RoundToInt(survivor * (w.strength / (float)winnerTotal)));
                    w.engaged = false;
                }
                if (outcomes != null)
                    outcomes.Add(new EncounterOutcome(systems[s], systems[s], winFaction, loseFaction, survivor));
                count++;
            }
            return count;
        }

        /// <summary>
        /// 回廊が前線（FTL不可）か。両端の星系を所有する勢力が敵対していれば true。
        /// 自勢力↔敵対勢力をつなぐ回廊はワープで通り抜けられず、回廊内で会戦になる（C-3 で起動予定）。
        /// 敵対判定は FactionRelations 経由（所有者の enum 比較＝後方互換）。
        /// </summary>
        public static bool IsFtlBlocked(GalaxyMap map, Corridor c)
        {
            if (map == null || c == null) return false;
            StarSystem a = map.GetSystem(c.aId);
            StarSystem b = map.GetSystem(c.bId);
            if (a == null || b == null) return false;
            return FactionRelations.IsHostile(null, a.owner, null, b.owner);
        }

        // ===== 回廊要塞＝戦略ノード（#40 C-7）=====

        /// <summary>
        /// 回廊が要塞で封鎖されているか（#40）。要塞が健在（<see cref="FortressRules.BlocksPassage"/>）で、
        /// 通行しようとする勢力が要塞所有者に敵対していれば封鎖＝通過には撃破/制圧が要る（迂回不可）。
        /// 要塞なし（フェザーン型の通商回廊）・要塞所有者と非敵対なら自由通過（後方互換）。
        /// </summary>
        public static bool IsFortressBlocked(Corridor c, Faction viewerFaction)
        {
            if (c == null || c.fortress == null) return false;
            if (!FortressRules.BlocksPassage(c.fortress)) return false;
            return FactionRelations.IsHostile(null, viewerFaction, null, c.fortress.owner);
        }

        /// <summary>
        /// 回廊要塞への力攻めを決定論で自動解決する（#40・抽象版）。攻撃兵力が実効防御の assaultRatio 倍以上なら
        /// <b>陥落</b>（守備0・通過開放・所有を攻撃側へ移転）＝攻撃側は実効防御ぶん消耗して残存。満たなければ
        /// <b>撃退</b>（難攻不落）＝攻撃側は守備の反撃で損害を受け、要塞は守備を一部削られるが健在（繰り返しの
        /// 消耗＝兵糧攻めの素地）。潜行（実会戦）でも自動解決でもこの窓口で要塞状態を更新する。
        /// </summary>
        public static FortressAssaultResult AssaultFortress(Fortress f, Faction attacker, int attackerStrength)
            => AssaultFortress(f, attacker, attackerStrength, FortressParams.Default);

        /// <summary><inheritdoc cref="AssaultFortress(Fortress,Faction,int)"/></summary>
        public static FortressAssaultResult AssaultFortress(Fortress f, Faction attacker, int attackerStrength, FortressParams p)
        {
            int atk = attackerStrength > 0 ? attackerStrength : 0;
            if (f == null) return new FortressAssaultResult(true, atk); // 要塞なし＝素通り（防御的フォールバック）

            float def = FortressRules.EffectiveDefense(f, p);
            if (FortressRules.CaptureFeasibleByForce(f, atk, p))
            {
                // 力攻め成功：実効防御ぶん消耗して占領。要塞は陥落（守備0・通過開放・所有移転）。
                int survivor = Mathf.Max(1, atk - Mathf.RoundToInt(def));
                f.garrisonStrength = 0f;
                f.controlsCorridor = false;
                f.owner = attacker;
                return new FortressAssaultResult(true, survivor);
            }

            // 撃退：難攻不落。攻撃側は反撃で損害（実効防御の一部）、要塞は守備を一部削られるが健在。
            int loss = Mathf.RoundToInt(Mathf.Min(atk, def * 0.5f));
            int attackerSurvivor = Mathf.Max(0, atk - loss);
            f.garrisonStrength = Mathf.Max(1f, f.garrisonStrength - atk * 0.1f); // 繰り返しで徐々に削れる素地
            return new FortressAssaultResult(false, attackerSurvivor);
        }

        /// <summary>
        /// 戦術潜行（実会戦＝FortressUnit 戦・#76-78）の決着を戦略の回廊要塞へ反映する（#40 次スライス）。
        /// 自動解決の <see cref="AssaultFortress"/> が力攻め比で決めるのに対し、こちらは<b>戦術会戦の帰結</b>
        /// （プレイヤーがコア破壊／砲台沈黙まで攻略できたか）を直接反映する唯一の窓口。乱数なし・決定論。
        /// <list type="bullet">
        /// <item><b>制圧</b>（captured）：守備0・通過開放・所有を攻撃側へ移転（陥落）。</item>
        /// <item><b>撃退</b>（!captured）：要塞は健在のまま、戦術会戦で受けた打撃ぶんシールドが
        /// <paramref name="repulseShieldLoss"/> 目減りする＝再潜行/兵糧攻めで段階的に脆くなる素地を残す。</item>
        /// </list>
        /// 攻撃側艦隊の残存兵力は呼び出し側（GalaxyView）が会戦の生存戦力から按分する（ここでは要塞だけを更新）。
        /// </summary>
        public static void ApplyFortressBattleResult(Fortress f, Faction attacker, bool captured, float repulseShieldLoss = 0.2f)
        {
            if (f == null) return;
            if (captured)
            {
                f.garrisonStrength = 0f;
                f.controlsCorridor = false;
                f.owner = attacker;
                return;
            }
            // 撃退：要塞は持ちこたえるが、潜行で叩かれたぶんシールドが削れる（次の攻略が楽になる＝段階攻略）。
            f.shieldIntegrity = FortressRules.ShieldAfterHit(f.shieldIntegrity, Mathf.Max(0f, repulseShieldLoss));
        }

        /// <summary>兵糧攻めの守備漸減の既定：1日あたり守備の3%＋下限50（必ず0へ到達して開城する＝難攻不落も封鎖で落ちる）。</summary>
        public const float FortressStarveDailyFraction = 0.03f;
        public const float FortressStarveDailyFloor = 50f;

        /// <summary>
        /// 兵糧攻め（補給遮断）で要塞守備を1tick分すり減らす（#40 戦略・銀英伝＝難攻不落も封鎖で落とす）。
        /// 力攻めでは落ちない要塞も、補給線を断って攻囲を続ければ守備が尽きて<b>開城</b>する＝正面でなく策略・
        /// 持久で落とす道。besieged（補給遮断下で攻囲継続中）でなければ何もしない。守備が0に達したら
        /// controlsCorridor を解いて回廊を開ける（所有は不変＝占領は別途・停泊で発生）。落城したら true。
        /// 乱数なし・決定論。日次Tick（<paramref name="days"/>=1）から呼ぶ。
        /// </summary>
        public static bool TickFortressSiegeAttrition(Fortress f, bool besieged, float days)
            => TickFortressSiegeAttrition(f, besieged, days, FortressStarveDailyFraction, FortressStarveDailyFloor);

        /// <summary><inheritdoc cref="TickFortressSiegeAttrition(Fortress,bool,float)"/></summary>
        public static bool TickFortressSiegeAttrition(Fortress f, bool besieged, float days, float dailyFraction, float dailyFloor)
        {
            if (f == null || !besieged || f.garrisonStrength <= 0f) return false;
            float d = Mathf.Max(0f, days);
            float loss = (Mathf.Max(0f, dailyFraction) * f.garrisonStrength + Mathf.Max(0f, dailyFloor)) * d;
            f.garrisonStrength = Mathf.Max(0f, f.garrisonStrength - loss);
            if (f.garrisonStrength <= 0f)
            {
                f.controlsCorridor = false; // 兵糧尽きて開城＝回廊が開く（所有は停泊占領で別途移る）
                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定星系の占領を解決する。停泊中の艦隊が1勢力のみで、その勢力が現所有者と敵対していれば
        /// 所有権をその勢力へ移す。複数勢力が居る（＝戦闘案件）／無人／同一勢力なら何もしない。
        /// フリップしたら true。
        /// </summary>
        public static bool ResolveOccupation(GalaxyMap map, StrategicFleetRegistry reg, int systemId)
        {
            if (map == null || reg == null) return false;
            StarSystem sys = map.GetSystem(systemId);
            if (sys == null) return false;

            // 防衛惑星（制空権持ち）は停泊だけでは落ちない＝攻城(TickSieges)で占領する（#131）。
            if (sys.planet != null) return false;

            List<StrategicFleet> present = reg.FleetsAt(systemId);
            if (present.Count == 0) return false;

            // 在席勢力が1つに揃っているか（揃っていなければ占領未確定）
            Faction occupier = present[0].faction;
            for (int i = 1; i < present.Count; i++)
                if (present[i].faction != occupier) return false;

            // 現所有者と敵対していれば占領（同一勢力なら据え置き）
            if (FactionRelations.IsHostile(null, sys.owner, null, occupier))
            {
                sys.owner = occupier;
                return true;
            }
            return false;
        }

        /// <summary>全星系の占領を解決し、所有が変わった星系数を返す。</summary>
        public static int ResolveAllOccupations(GalaxyMap map, StrategicFleetRegistry reg)
        {
            if (map == null || reg == null) return 0;
            int flips = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s != null && ResolveOccupation(map, reg, s.id)) flips++;
            }
            return flips;
        }

        // ===== 惑星攻城（#131 PB-3〜7 の戦略配線）=====

        /// <summary>
        /// 防衛惑星(StarSystem.planet)の攻城を deltaTime 進める（#131）。各星系について停泊中の艦隊を
        /// 攻撃側(planet.owner と敵対)／防衛側(非敵対)に分け、<b>防衛側が居らず攻撃側が単一勢力</b>のとき
        /// だけ、その合計兵力を S-AV戦力として <see cref="PlanetSiegeRules.Tick"/> で
        /// 制空権制圧→侵略→占領を進める（係争中＝両勢力停泊は宇宙の会戦が先＝攻城は進めない）。
        /// 占領したら StarSystem.owner も攻撃側へ同期する。占領した星系数を返す。回廊上の艦は停泊に数えない。
        /// </summary>
        public static int TickSieges(GalaxyMap map, StrategicFleetRegistry reg, float deltaTime, SiegeParams prm)
        {
            if (map == null || reg == null || deltaTime <= 0f) return 0;
            int captures = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem sys = map.systems[i];
                if (sys == null || sys.planet == null) continue;
                Planet planet = sys.planet;

                List<StrategicFleet> present = reg.FleetsAt(sys.id);
                Faction attackerFaction = planet.owner;
                bool hasAttacker = false, mixedAttackers = false, defendersPresent = false;
                int attackerStrength = 0;
                for (int k = 0; k < present.Count; k++)
                {
                    StrategicFleet f = present[k];
                    if (f == null) continue;
                    if (FactionRelations.IsHostile(null, f.faction, null, planet.owner))
                    {
                        if (!hasAttacker) { attackerFaction = f.faction; hasAttacker = true; }
                        else if (attackerFaction != f.faction) mixedAttackers = true;
                        attackerStrength += f.strength;
                    }
                    else defendersPresent = true;
                }

                float sav = (hasAttacker && !mixedAttackers && !defendersPresent) ? attackerStrength : 0f;
                SiegeTickResult r = PlanetSiegeRules.Tick(planet, attackerFaction, sav, deltaTime, prm);
                if (r.captured)
                {
                    // 占領：星系所有を同期し、新所有者が制空権を再建（侵略値リセット＝再び攻城が要る）。
                    sys.owner = planet.owner;
                    planet.orbitalDefense = planet.maxOrbitalDefense;
                    planet.invasionProgress = 0f;
                    captures++;
                }
            }
            return captures;
        }

        /// <summary>既定係数で攻城を進める簡易版。</summary>
        public static int TickSieges(GalaxyMap map, StrategicFleetRegistry reg, float deltaTime)
            => TickSieges(map, reg, deltaTime, SiegeParams.Default);

        // ===== 回廊戦闘（C-3 #36）=====

        /// <summary>
        /// 兵力差で回廊戦闘を解決する（抽象版）。攻撃側は優勢（攻撃兵力＞防衛兵力）で勝利し、
        /// 勝者の残存兵力＝勝者総兵力−敗者総兵力。同数は防衛側が守り切る（攻撃側敗北）。
        /// ※将来ここを実際の戦術エンジン（BattleSetup）起動の結果に差し替える。
        /// </summary>
        public static CorridorBattleResult ResolveCorridorBattle(int attackerStrength, int defenderStrength)
        {
            // 兵力は非負へクランプ（負の兵力が「敵を強化する」非物理な結果を防ぐ）
            int a = attackerStrength > 0 ? attackerStrength : 0;
            int d = defenderStrength > 0 ? defenderStrength : 0;
            bool aWon = a > d;
            int survivor = aWon ? a - d : d - a;
            return new CorridorBattleResult(aWon, survivor);
        }

        /// <summary>
        /// 実効戦力倍率（補給/技術/軍の質）を織り込む版。**勝敗は実効戦力で決まり、残存兵力は生兵力で算出**する
        /// （倍率1.0で従来と完全一致＝後方互換）。倍率は <see cref="FleetCombatStrengthRules"/> で合成して渡す。
        /// </summary>
        public static CorridorBattleResult ResolveCorridorBattle(int attackerStrength, int defenderStrength, float attackerFactor, float defenderFactor)
        {
            int a = attackerStrength > 0 ? attackerStrength : 0;
            int d = defenderStrength > 0 ? defenderStrength : 0;
            float af = attackerFactor > 0f ? attackerFactor : 0f;
            float df = defenderFactor > 0f ? defenderFactor : 0f;
            float aEff = a * af, dEff = d * df;
            bool aWon = aEff > dEff;                       // 勝敗は実効戦力で
            int winnerRaw = aWon ? a : d;
            float effWin = aWon ? aEff : dEff, effLose = aWon ? dEff : aEff;
            int survivor = effWin > 0f ? Mathf.RoundToInt(winnerRaw * Mathf.Clamp01((effWin - effLose) / effWin)) : winnerRaw; // 残存は生兵力で
            return new CorridorBattleResult(aWon, survivor);
        }

        /// <summary>
        /// 同一回廊上で出会った敵対艦隊を戦闘で解決する（回廊内で味方と敵がぶつかる＝戦闘開始・C-3）。
        /// 兵力差で勝敗（ResolveCorridorBattle）。敗者は除去、勝者は残存兵力で進行を続ける（相打ちは両方除去）。
        /// 解決した戦闘数を返す。毎フレーム呼ぶ想定（Tick の後）。
        /// </summary>
        public static int ResolveEncounters(StrategicFleetRegistry reg) => ResolveEncounters(reg, null);

        /// <summary>
        /// <see cref="ResolveEncounters(StrategicFleetRegistry)"/> の結末収集版。解決した各会戦の結末
        /// （発生回廊・勝者/敗者・勝者残存兵力）を <paramref name="outcomes"/> に追加する（通知・ピン留め用・null可）。
        /// </summary>
        public static int ResolveEncounters(StrategicFleetRegistry reg, IList<EncounterOutcome> outcomes,
            System.Func<StrategicFleet, float> factorOf = null)
        {
            if (reg == null) return 0;
            int count = 0;
            foreach (var e in FindEncounters(reg))
            {
                StrategicFleet a = e.a, b = e.b;
                if (a == null || b == null) continue;
                if (reg.GetFleet(a.id) == null || reg.GetFleet(b.id) == null) continue; // 既に除去済み
                if (!FleetsCollided(a, b)) continue; // 回廊内でまだ接触していない（複数艦が同じ回廊に居られる）

                // 実効戦力倍率（補給/技術/軍の質）が与えられれば勝敗へ織り込む（null=従来＝倍率1.0）。
                float fa = factorOf != null ? factorOf(a) : 1f;
                float fb = factorOf != null ? factorOf(b) : 1f;
                CorridorBattleResult r = ResolveCorridorBattle(a.strength, b.strength, fa, fb);
                if (outcomes != null)
                {
                    StrategicFleet w = r.attackerWon ? a : b;
                    StrategicFleet l = r.attackerWon ? b : a;
                    int mn = Mathf.Min(a.currentSystemId, a.destinationSystemId);
                    int mx = Mathf.Max(a.currentSystemId, a.destinationSystemId);
                    outcomes.Add(new EncounterOutcome(mn, mx, w.faction, l.faction, r.survivorStrength));
                }
                ApplyBattleResult(reg, a, b, r);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 戦闘結果（勝者・残存兵力）を2艦隊 a/b に適用する（抽象解決・実会戦どちらの結果でも共通）。
        /// attackerWon=true なら a が勝者。敗者は除去、勝者は残存兵力へ、相打ち（残存0）は両除去。
        /// </summary>
        public static void ApplyBattleResult(StrategicFleetRegistry reg, StrategicFleet a, StrategicFleet b, CorridorBattleResult r)
            => ApplyBattleResult(reg, a, b, r, 0);

        /// <summary>
        /// 敗北の代償つき版（#戦闘ドクトリン Stage4）。<paramref name="loserSurvivor"/> が正なら<b>敗者を除去せず
        /// 原隊へ帰す</b>（撤退して生き延びた＝盤面に残し再建可能に）＝整然と退いた軍はプールでなく盤面戦力だけ
        /// 失う。あわせて敗者に「士気・練度低下＋将帥の声望/政治的代償」を科す：兵力は撤退損耗ぶん削り（残った
        /// 兵力を <see cref="CorpsRetreatRules.SurvivorsAfterWithdrawal"/> で目減りさせ）、声望/政治の代償は敗者の
        /// 勢力/軍団名を添えて <see cref="NotificationCenter"/>（人事/政治）へ通知する（StrategicFleet に声望/練度の
        /// 直列化フィールドは無いため、数値台帳でなく通知で表現する＝設計どおり）。
        /// <paramref name="loserSurvivor"/>=0（既定）なら従来どおり敗者を除去する（完全後方互換）。
        /// </summary>
        public static void ApplyBattleResult(StrategicFleetRegistry reg, StrategicFleet a, StrategicFleet b,
            CorridorBattleResult r, int loserSurvivor)
        {
            if (reg == null || a == null || b == null) return;
            StrategicFleet winner = r.attackerWon ? a : b;
            StrategicFleet loser = r.attackerWon ? b : a;

            if (loserSurvivor > 0)
            {
                // 撤退して生き延びた敗者：盤面に残し原隊へ帰す（再建可能）。敗北の代償を科す。
                ApplyDefeatPenalty(loser, loserSurvivor, winner.strength);
            }
            else
            {
                reg.Remove(loser); // 完敗（生存兵力なし）＝盤面から除去（従来動作）
            }

            winner.strength = r.survivorStrength;
            winner.engaged = false; // 決着＝交戦固着を解除し前進を再開（抽象・実会戦どちらの結果でも）
            if (winner.strength <= 0) reg.Remove(winner); // 相打ち
        }

        /// <summary>
        /// 撤退して生き延びた敗者へ敗北の代償（#戦闘ドクトリン Stage4）を科す：撤退損耗で兵力を目減りさせ
        /// （<see cref="CorpsRetreatRules.SurvivorsAfterWithdrawal"/>・交戦固着は解除して原隊へ帰す）、将帥の声望/
        /// 政治的代償は敗者の勢力/軍団名を添えて通知する（数値台帳でなく通知で表現＝StrategicFleet に声望/士気/
        /// 練度の直列化フィールドが無い設計に合わせる）。敗北の度合いは勝者比から推定し、完敗ほど損耗・代償が重い。
        /// </summary>
        private static void ApplyDefeatPenalty(StrategicFleet loser, int survivor, int winnerStrength)
        {
            if (loser == null) return;
            int initial = Mathf.Max(1, loser.strength); // 戦闘前の兵力（呼び出し時点でまだ更新前）
            float severity = CorpsRetreatRules.DefeatSeverity(survivor, initial, winnerStrength);

            // 撤退に伴う追加損耗（しんがり・落伍）。最低1で原隊へ帰す（全滅させない）。
            loser.strength = CorpsRetreatRules.SurvivorsAfterWithdrawal(survivor, severity);
            loser.engaged = false; // 交戦固着を解いて原隊（後方）へ退ける

            // 将帥の声望・政治的代償＝敗者の勢力/軍団を名指しで通知（人事/政治）。
            string who = !string.IsNullOrEmpty(loser.corpsName) ? $"{loser.faction}・{loser.corpsName}" : loser.faction.ToString();
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                $"{who} は会戦に敗れ後退（士気・練度低下、指揮官の声望に傷）");
            if (severity >= 0.6f) // 完敗は政治的な責任問題に波及
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                    $"{who} の敗北で指揮官の政治的立場が揺らぐ");
        }

        /// <summary>
        /// 敗者が撤退して生き延びる場合に原隊へ帰す残存兵力の既定割合（#戦闘ドクトリン Stage4）。
        /// 実会戦で整然と退いた敗者は全滅でなく一部が後方へ生還する＝この割合×戦闘前兵力を生存とみなす。
        /// </summary>
        public const float DefeatedRetreatSurvivorRatio = 0.5f;

        /// <summary>
        /// 実会戦（Battleシーン）から戻った結果（BattleHandoff）を戦略レジストリへ反映する（C-3）。
        /// 予約があり結果が書き込まれていれば、A/B の艦隊IDを引いて ApplyBattleResult を適用し、Handoff を消す。
        /// 反映したら true。<b>敗者は除去（従来動作・完全後方互換）。</b>撤退した敗者を原隊へ帰したい場合は
        /// <see cref="ApplyHandoffResultWithRetreat"/> を使う（#戦闘ドクトリン Stage4・opt-in）。
        /// </summary>
        public static bool ApplyHandoffResult(StrategicFleetRegistry reg) => ApplyHandoffResult(reg, false);

        /// <summary>
        /// #戦闘ドクトリン Stage4：実会戦の敗者を<b>除去せず原隊へ帰す</b>opt-in版。敗者は撤退して生き延びた
        /// ぶん（戦闘前兵力×<see cref="DefeatedRetreatSurvivorRatio"/>）を盤面に残し（再建可能）、敗北の代償
        /// （撤退損耗で兵力目減り＋将帥の声望/政治的代償の通知）を科す。<see cref="ApplyHandoffResult"/> の
        /// 既定（敗者除去）と分けることで既存契約を壊さない（Galaxy View 等が望めば本版へ差し替える）。
        /// </summary>
        public static bool ApplyHandoffResultWithRetreat(StrategicFleetRegistry reg) => ApplyHandoffResult(reg, true);

        /// <summary>
        /// 実会戦結果の反映本体。<paramref name="keepDefeatedLoser"/>=true なら敗者を原隊へ帰し代償を科す（Stage4）。
        /// false なら従来どおり敗者除去（後方互換）。複数艦隊の会戦は按分版へ委譲。
        /// </summary>
        private static bool ApplyHandoffResult(StrategicFleetRegistry reg, bool keepDefeatedLoser)
        {
            if (reg == null || !BattleHandoff.Pending || !BattleHandoff.Resolved) return false;
            if (BattleHandoff.IsMultiFleet) return ApplyMultiHandoffResult(reg); // 複数艦隊の会戦は按分で反映
            StrategicFleet a = reg.GetFleet(BattleHandoff.fleetIdA);
            StrategicFleet b = reg.GetFleet(BattleHandoff.fleetIdB);
            var r = new CorridorBattleResult(BattleHandoff.sideAWon, BattleHandoff.survivorStrength);
            BattleHandoff.Clear();
            if (a == null || b == null) return false;
            if (keepDefeatedLoser)
            {
                StrategicFleet loser = r.attackerWon ? b : a;
                int loserSurvivor = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, loser.strength) * DefeatedRetreatSurvivorRatio));
                ApplyBattleResult(reg, a, b, r, loserSurvivor);
            }
            else
            {
                ApplyBattleResult(reg, a, b, r);
            }
            return true;
        }

        /// <summary>
        /// 同一回廊上の2艦隊が「回廊内で接触したか」を位置で判定する（IsOnSameCorridor が前提）。
        /// 回廊エッジ {min,max} 上の進行位置で、正面から来る2艦は交差した瞬間、同方向は近接で接触とみなす。
        /// これにより複数艦が同じ回廊に同時に居られ、ぶつかった瞬間だけ戦闘になる。
        /// </summary>
        private static bool FleetsCollided(StrategicFleet a, StrategicFleet b)
        {
            const float eps = 0.04f;
            int min = Mathf.Min(a.currentSystemId, a.destinationSystemId);

            // 各艦の「エッジ min からの進行位置（0..1）」
            bool aFromMin = a.currentSystemId == min;
            bool bFromMin = b.currentSystemId == min;
            float fa = aFromMin ? a.Progress : 1f - a.Progress;
            float fb = bFromMin ? b.Progress : 1f - b.Progress;

            if (aFromMin == bFromMin) return Mathf.Abs(fa - fb) <= eps; // 同方向＝近接で接触

            // 正面衝突：min 側から来た艦が max 側から来た艦に追いついた（交差した）瞬間
            float fromMin = aFromMin ? fa : fb;
            float fromMax = aFromMin ? fb : fa;
            return fromMin >= fromMax - eps;
        }

        /// <summary>
        /// 前線回廊への侵攻＝回廊戦闘を起動・解決して戦略状態へ書き戻す（C-3 抽象版）。
        /// fromSystemId（攻撃側星系）と toSystemId（敵星系）が前線回廊でつながっていること、
        /// fromSystem に「toSystem 所有者と敵対する停泊艦隊」が居ることが前提。
        /// 攻撃側勝利：防衛艦を除去、toSystem を攻撃側勢力が占領、生存攻撃艦が toSystem へ前進。
        /// 防衛側勝利：攻撃艦を除去。兵力は ResolveCorridorBattle で按分し、残存0は相打ち除去。
        /// 起動条件を満たさなければ false。
        /// </summary>
        public static bool EngageFrontline(GalaxyMap map, StrategicFleetRegistry reg,
            int fromSystemId, int toSystemId, out CorridorBattleResult result)
        {
            result = new CorridorBattleResult(false, 0);
            if (map == null || reg == null) return false;

            Corridor c = map.GetCorridor(fromSystemId, toSystemId);
            if (c == null || !IsFtlBlocked(map, c)) return false; // 前線でなければ侵攻ではない
            StarSystem to = map.GetSystem(toSystemId);
            if (to == null) return false;

            var attackers = new List<StrategicFleet>();
            foreach (var f in reg.FleetsAt(fromSystemId))
                if (FactionRelations.IsHostile(null, f.faction, null, to.owner)) attackers.Add(f);
            if (attackers.Count == 0) return false; // 攻撃できる艦隊がいない

            Faction attackerFaction = attackers[0].faction;
            var defenders = reg.FleetsAt(toSystemId);

            int aStr = SumStrength(attackers);
            int dStr = SumStrength(defenders);
            result = ResolveCorridorBattle(aStr, dStr);

            List<StrategicFleet> winners = result.attackerWon ? attackers : defenders;
            List<StrategicFleet> losers = result.attackerWon ? defenders : attackers;
            int winnerTotal = result.attackerWon ? aStr : dStr;

            foreach (var l in losers) reg.Remove(l);
            ScaleStrength(winners, result.survivorStrength, winnerTotal);
            foreach (var wn in new List<StrategicFleet>(winners))
                if (wn.strength <= 0) reg.Remove(wn);

            if (result.attackerWon)
            {
                to.owner = attackerFaction;
                foreach (var a in attackers)
                    if (reg.GetFleet(a.id) != null) { a.currentSystemId = toSystemId; a.destinationSystemId = toSystemId; }
            }
            return true;
        }

        private static int SumStrength(List<StrategicFleet> fleets)
        {
            int s = 0;
            foreach (var f in fleets) if (f != null) s += f.strength;
            return s;
        }

        private static void ScaleStrength(List<StrategicFleet> fleets, int survivor, int total)
        {
            if (total <= 0) { foreach (var f in fleets) if (f != null) f.strength = 0; return; }
            foreach (var f in fleets)
                if (f != null) f.strength = (int)System.Math.Round((double)f.strength * survivor / total);
        }
    }
}
