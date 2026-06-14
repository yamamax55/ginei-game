using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        /// <summary>
        /// 加齢/老衰デモ用の提督ロスターを用意する（TIME-6 #952・LIFE-2 #152）。各勢力に若年・老齢を混ぜ、
        /// 暦の年境界で <see cref="AnnualLifecycleRules.ProcessMortality"/> により老衰死しうる。配下の継承は後段。
        /// </summary>
        private void SetupPersonnel()
        {
            campaignYear = TimeDisplay.StartYear; // 開始暦（宇宙暦SE796）と揃える
            // 朝廷の権威は StrategySession に持たせ Battle 往復・セーブで永続（無ければ既定0.35＝武家政権相当で起こす）。
            courtAuthority = StrategySession.CourtAuthority ?? (StrategySession.CourtAuthority = new CourtAuthority(0.35f));
            commanders = new List<Person>();
            civilians = new List<Person>();
            if (StrategySession.PendingPeople != null)
            {
                // ロード復元：保存済みロスターを採用（軍人=提督名簿／文民=文官名簿に振り分け）。
                var loaded = StrategySession.PendingPeople;
                int maxId = 0;
                for (int i = 0; i < loaded.Count; i++)
                {
                    Person p = loaded[i];
                    if (p == null) continue;
                    if (p.role == PersonRole.軍人) commanders.Add(p); else civilians.Add(p);
                    if (p.id > maxId) maxId = p.id;
                }
                nextPersonId = maxId + 1;
                StrategySession.PendingPeople = null; // 消費（再構築は一度きり）
            }
            else
            {
                int y = campaignYear;
                int id = 1;
                // 各勢力：壮年（当面は死ににくい）＋老齢（老衰しうる）
                commanders.Add(new Person(id++, "ミッターマイアー", Faction.帝国, PersonRole.軍人) { birthYear = y - 39, rankTier = 8 });
                commanders.Add(new Person(id++, "メックリンガー", Faction.帝国, PersonRole.軍人) { birthYear = y - 79, rankTier = 8 });
                commanders.Add(new Person(id++, "アッテンボロー", Faction.同盟, PersonRole.軍人) { birthYear = y - 41, rankTier = 7 });
                commanders.Add(new Person(id++, "ビュコック", Faction.同盟, PersonRole.軍人) { birthYear = y - 88, rankTier = 9 });
                id = SeedFoundingYouth(id, y); // 世代交代ループの種＝結婚適齢の若者（男女）
                id = SeedDemoCivilService(id, y); // 指導者/政治家/文官/官僚/技術者をシード（人事観測層のテスト）
                nextPersonId = id; // 卒業生はこの続き番号で採番
            }

            // 特殊作戦部隊（#SOF・SEAL型選抜）：勢力ごとに候補を多段の苛烈な選抜で篩い、認定者を SOF 出身にする。
            RunSofSelection();

            // 士官学校（#155 LIFE-5）：各勢力に1校。質に差を付ける（名門は良将を出す）。
            academies = new List<Academy>
            {
                new Academy(schoolId: 1, faction: Faction.帝国, name: "帝国士官学校", capacity: 6, quality: 0.6f),
                new Academy(schoolId: 2, faction: Faction.同盟, name: "同盟士官学校", capacity: 6, quality: 0.55f),
            };

            // 大学（#156/#157 LIFE-6/7）：各勢力に文官大学＋帝国に工科大学（テクノクラート）。文民/技術者を輩出。
            // civilians は上で初期化済（ロード復元 or 空）。ここでは再生成しない。
            universities = new List<University>
            {
                new University(schoolId: 3, faction: Faction.帝国, name: "帝国大学", track: CareerTrack.科挙, capacity: 6, quality: 0.6f),
                new University(schoolId: 4, faction: Faction.同盟, name: "自由惑星同盟大学", track: CareerTrack.科挙, capacity: 6, quality: 0.6f),
                new University(schoolId: 5, faction: Faction.帝国, name: "帝国工科大学", track: CareerTrack.テクノクラート, capacity: 4, quality: 0.6f),
            };

            // 高校（中等教育の土台）：帝国は選別的（進学率低・質高）、同盟は大衆教育（進学率高）。
            highSchools = new List<HighSchool>
            {
                new HighSchool(schoolId: 10, faction: Faction.帝国, name: "帝国高等学校", enrollmentRate: 0.5f, quality: 0.6f),
                new HighSchool(schoolId: 11, faction: Faction.同盟, name: "同盟公立高校", enrollmentRate: 0.75f, quality: 0.5f),
            };
            // 中学校（前期中等教育）：高校より進学率高め（裾野）。中学校→高校→上級学校で進学率が複利。
            middleSchools = new List<MiddleSchool>
            {
                new MiddleSchool(schoolId: 12, faction: Faction.帝国, name: "帝国中等学校", enrollmentRate: 0.8f, quality: 0.55f),
                new MiddleSchool(schoolId: 13, faction: Faction.同盟, name: "同盟公立中学校", enrollmentRate: 0.95f, quality: 0.5f),
            };
            // 小学校（初等教育の根）：ほぼ全員（義務教育）。就学率が教育チェーンの根を成す。
            elementarySchools = new List<ElementarySchool>
            {
                new ElementarySchool(schoolId: 20, faction: Faction.帝国, name: "帝国国民学校", enrollmentRate: 0.9f, quality: 0.55f),
                new ElementarySchool(schoolId: 21, faction: Faction.同盟, name: "同盟公立小学校", enrollmentRate: 0.99f, quality: 0.5f),
            };
            // 幼稚園（就学前教育＝教育チェーンの最下根）。
            kindergartens = new List<Kindergarten>
            {
                new Kindergarten(schoolId: 22, faction: Faction.帝国, name: "帝国幼稚園", enrollmentRate: 0.6f, quality: 0.55f),
                new Kindergarten(schoolId: 23, faction: Faction.同盟, name: "同盟幼稚園", enrollmentRate: 0.8f, quality: 0.5f),
            };
            // 保育園（保育＝労働参加↑/出生率↑・教育とは別軸）。同盟は福祉手厚く整備率高め。
            nurseries = new List<Nursery>
            {
                new Nursery(schoolId: 24, faction: Faction.帝国, name: "帝国保育所", coverage: 0.4f),
                new Nursery(schoolId: 25, faction: Faction.同盟, name: "同盟公立保育園", coverage: 0.7f),
            };
            // 高専（中学校→高専の実務技術者路・高校を経ない別ルート・#157）。
            colleges = new List<TechnicalCollege>
            {
                new TechnicalCollege(schoolId: 14, faction: Faction.帝国, name: "帝国高等専門学校", capacity: 5, quality: 0.6f),
                new TechnicalCollege(schoolId: 15, faction: Faction.同盟, name: "同盟工業高専", capacity: 5, quality: 0.55f),
            };
            // 短大／専門学校（高校卒後2年制・中堅人材＝官界/現場の裾野・#156/#157）。
            juniorColleges = new List<JuniorCollege>
            {
                new JuniorCollege(schoolId: 16, faction: Faction.帝国, name: "帝国短期大学", capacity: 6, quality: 0.5f),
                new JuniorCollege(schoolId: 17, faction: Faction.同盟, name: "同盟短期大学", capacity: 6, quality: 0.5f),
            };
            vocationalSchools = new List<VocationalSchool>
            {
                new VocationalSchool(schoolId: 18, faction: Faction.帝国, name: "帝国専門学校", capacity: 6, quality: 0.5f),
                new VocationalSchool(schoolId: 19, faction: Faction.同盟, name: "同盟専門学校", capacity: 6, quality: 0.5f),
            };
        }

        /// <summary>その勢力の高校（中等教育）を返す（無ければ null＝教育の制約なし）。</summary>
        private HighSchool HighSchoolOf(Faction faction)
        {
            if (highSchools == null) return null;
            for (int i = 0; i < highSchools.Count; i++)
                if (highSchools[i] != null && highSchools[i].faction == faction) return highSchools[i];
            return null;
        }

        /// <summary>その勢力の中学校（前期中等教育）を返す（無ければ null）。</summary>
        private MiddleSchool MiddleSchoolOf(Faction faction)
        {
            if (middleSchools == null) return null;
            for (int i = 0; i < middleSchools.Count; i++)
                if (middleSchools[i] != null && middleSchools[i].faction == faction) return middleSchools[i];
            return null;
        }

        /// <summary>その勢力の小学校（初等教育）を返す（無ければ null）。</summary>
        private ElementarySchool ElementarySchoolOf(Faction faction)
        {
            if (elementarySchools == null) return null;
            for (int i = 0; i < elementarySchools.Count; i++)
                if (elementarySchools[i] != null && elementarySchools[i].faction == faction) return elementarySchools[i];
            return null;
        }

        /// <summary>その勢力の幼稚園（就学前教育）を返す（無ければ null）。</summary>
        private Kindergarten KindergartenOf(Faction faction)
        {
            if (kindergartens == null) return null;
            for (int i = 0; i < kindergartens.Count; i++)
                if (kindergartens[i] != null && kindergartens[i].faction == faction) return kindergartens[i];
            return null;
        }

        /// <summary>その勢力の保育園の出生率倍率（無ければ1.0）。</summary>
        private float NurseryFertilityOf(Faction faction)
        {
            if (nurseries == null) return 1f;
            for (int i = 0; i < nurseries.Count; i++)
                if (nurseries[i] != null && nurseries[i].faction == faction)
                    return NurseryRules.FertilityFactor(nurseries[i].coverage);
            return 1f;
        }

        /// <summary>その勢力の保育園の労働参加倍率（無ければ1.0）＝候補/徴募プールに掛ける。</summary>
        private float NurseryLaborOf(Faction faction)
        {
            if (nurseries == null) return 1f;
            for (int i = 0; i < nurseries.Count; i++)
                if (nurseries[i] != null && nurseries[i].faction == faction)
                    return NurseryRules.LaborParticipationFactor(nurseries[i].coverage);
            return 1f;
        }

        /// <summary>
        /// 教育チェーン（中学校→高校）を解決し、上級学校の候補母数倍率（進学率の複利）と実効教育質（質の段階的上乗せ）を返す。
        /// 学校が無い段は素通り（倍率1・据え置き＝後方互換）。
        /// </summary>
        private void ResolveEducation(Faction faction, float baseQuality, out float enrollFactor, out float effectiveQuality)
            => ResolveEducation(faction, baseQuality, true, out enrollFactor, out effectiveQuality);

        /// <summary>
        /// 教育チェーンを解決。<paramref name="includeHighSchool"/>=false は高校を経ない路（高専＝中学校→高専）＝高校段を素通り。
        /// </summary>
        private void ResolveEducation(Faction faction, float baseQuality, bool includeHighSchool,
            out float enrollFactor, out float effectiveQuality)
        {
            enrollFactor = 1f;
            effectiveQuality = baseQuality;
            if (includeHighSchool)
            {
                HighSchool hs = HighSchoolOf(faction);
                if (hs != null)
                {
                    enrollFactor *= HighSchoolRules.EducationFactor(hs.enrollmentRate);
                    effectiveQuality = HighSchoolRules.EffectiveIntakeQuality(effectiveQuality, hs.quality);
                }
            }
            MiddleSchool ms = MiddleSchoolOf(faction);
            if (ms != null)
            {
                enrollFactor *= MiddleSchoolRules.EducationFactor(ms.enrollmentRate);
                effectiveQuality = MiddleSchoolRules.EffectiveIntakeQuality(effectiveQuality, ms.quality);
            }
            // 小学校（初等教育の根）は学術路/実務路を問わず常にチェーンに入る。
            ElementarySchool es = ElementarySchoolOf(faction);
            if (es != null)
            {
                enrollFactor *= ElementarySchoolRules.EducationFactor(es.enrollmentRate);
                effectiveQuality = ElementarySchoolRules.EffectiveIntakeQuality(effectiveQuality, es.quality);
            }
            // 幼稚園（就学前教育の最下根）も常にチェーンに入る。
            Kindergarten kg = KindergartenOf(faction);
            if (kg != null)
            {
                enrollFactor *= KindergartenRules.EducationFactor(kg.enrollmentRate);
                effectiveQuality = KindergartenRules.EffectiveIntakeQuality(effectiveQuality, kg.quality);
            }
        }

        /// <summary>
        /// 教育オブザーバ（<see cref="EducationObserverOverlay"/>）向けの読み取りダンプ。教育データは GalaxyView 内
        /// （<see cref="SetupPersonnel"/> で構築）にあるため、ここで勢力ごとに整形して観測層へ渡す（観測専用＝状態は変えない）。
        /// </summary>
        public string BuildEducationDump()
        {
            var sb = new System.Text.StringBuilder(2048);
            sb.Append("<b>教育オブザーバ</b>　教育チェーン（幼→小→中→高→上級）と人材供給　(U で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");
            if (DemoFactions == null || DemoFactions.Length == 0)
            {
                sb.Append("\n<color=#ffcc66>教育データがありません（戦略マップ未起動）。</color>");
                return sb.ToString();
            }
            for (int i = 0; i < DemoFactions.Length; i++) AppendEducationFaction(sb, DemoFactions[i]);
            sb.Append("\n<color=#6f8a9a>※ 教育チェーンが普及・充実するほど候補母数（進学度）と実効素質が上がり、良い士官/官吏が育つ。</color>");
            return sb.ToString();
        }

        private void AppendEducationFaction(System.Text.StringBuilder sb, Faction fac)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>\n");

            // 基礎教育チェーン（就学率・質）
            Kindergarten kg = KindergartenOf(fac);
            ElementarySchool es = ElementarySchoolOf(fac);
            MiddleSchool ms = MiddleSchoolOf(fac);
            HighSchool hs = HighSchoolOf(fac);
            AppendEduStage(sb, "  幼稚園", kg?.enrollmentRate, kg?.quality);
            AppendEduStage(sb, "  小学校", es?.enrollmentRate, es?.quality);
            AppendEduStage(sb, "  中学校", ms?.enrollmentRate, ms?.quality);
            AppendEduStage(sb, "  高校　", hs?.enrollmentRate, hs?.quality);

            // 派生：基礎素質0.5 をチェーンに通したときの候補母数倍率・実効素質（実挙動と同じ ResolveEducation）。
            ResolveEducation(fac, 0.5f, true, out float ef, out float eq);
            sb.Append("    <color=#9fb0c0>進学度(候補母数×)</color> <color=#ffd28a>×")
              .Append(ef.ToString("0.00")).Append("</color>")
              .Append("　<color=#9fb0c0>実効素質(0.5→)</color> <color=#a0e0a0>")
              .Append(eq.ToString("0.00")).Append("</color>\n");

            AppendEduUpper(sb, fac);
        }

        /// <summary>基礎教育1段（就学率・質の小バー）。学校が無ければ「なし」。</summary>
        private void AppendEduStage(System.Text.StringBuilder sb, string label, float? enroll, float? quality)
        {
            sb.Append(label);
            if (enroll == null) { sb.Append("  <color=#6f8a9a>（なし）</color>\n"); return; }
            sb.Append("  就学 "); AppendEduMiniBar(sb, enroll.Value, "#7fd4ff");
            sb.Append("  質 ");   AppendEduMiniBar(sb, quality ?? 0f, "#a0e0a0");
            sb.Append('\n');
        }

        private void AppendEduMiniBar(System.Text.StringBuilder sb, float v01, string hex)
        {
            v01 = Mathf.Clamp01(v01);
            const int w = 8;
            int f = Mathf.RoundToInt(v01 * w);
            sb.Append("<color=").Append(hex).Append('>');
            for (int i = 0; i < w; i++) sb.Append(i < f ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00"));
        }

        /// <summary>上級学校（士官学校/大学/高専/短大/専門）を1行で列挙。</summary>
        private void AppendEduUpper(System.Text.StringBuilder sb, Faction fac)
        {
            sb.Append("    <color=#9fb0c0>上級:</color>");
            bool any = false;
            if (academies != null)
                foreach (var a in academies) if (a != null && a.faction == fac)
                { sb.Append(" [士官学校 定員").Append(a.capacity).Append("/質").Append(a.quality.ToString("0.0")).Append(']'); any = true; }
            if (universities != null)
                foreach (var u in universities) if (u != null && u.faction == fac)
                { sb.Append(" [").Append(u.track == CareerTrack.テクノクラート ? "工科大" : "大学").Append(" 定員").Append(u.capacity).Append("/質").Append(u.quality.ToString("0.0")).Append(']'); any = true; }
            if (colleges != null)
                foreach (var c in colleges) if (c != null && c.faction == fac)
                { sb.Append(" [高専 定員").Append(c.capacity).Append("/質").Append(c.quality.ToString("0.0")).Append(']'); any = true; }
            if (juniorColleges != null)
                foreach (var j in juniorColleges) if (j != null && j.faction == fac)
                { sb.Append(" [短大 定員").Append(j.capacity).Append(']'); any = true; }
            if (vocationalSchools != null)
                foreach (var v in vocationalSchools) if (v != null && v.faction == fac)
                { sb.Append(" [専門 定員").Append(v.capacity).Append(']'); any = true; }
            if (!any) sb.Append(" <color=#6f8a9a>なし</color>");
            sb.Append('\n');
        }

        /// <summary>
        /// 文民・指導者ロスターのデモシード（人事観測層のテスト＝指導者/政治家/文官/官僚/技術者を見やすく揃える）。
        /// 全員 <see cref="PersonRole.文民"/>＝<see cref="isSovereign"/>/<see cref="isPolitician"/> と文才/技才で
        /// <see cref="PersonVocationRules.VocationOf"/> が 君主/政治家/文官/技術者 に振り分ける。返り値は次の人物 id。
        /// </summary>
        private int SeedDemoCivilService(int id, int year)
        {
            if (DemoFactions == null || civilians == null) return id;
            foreach (Faction fac in DemoFactions)
            {
                // 指導者：君主/元首
                string sov = fac == Faction.帝国 ? "皇帝（デモ）" : "最高評議会議長（デモ）";
                civilians.Add(new Person(id++, sov, fac, PersonRole.文民)
                { isSovereign = true, birthYear = year - 50, leadership = 82, operation = 78, intelligence = 75 });

                // 指導者：政治家（民意と票で生き死にする・GOV-6）
                civilians.Add(new Person(id++, $"{fac}の政治家A", fac, PersonRole.文民)
                { isPolitician = true, birthYear = year - 48, leadership = 66, operation = 70, intelligence = 72 });
                civilians.Add(new Person(id++, $"{fac}の政治家B", fac, PersonRole.文民)
                { isPolitician = true, birthYear = year - 56, leadership = 60, operation = 66, intelligence = 68 });

                // 文民：文官・官僚（行政の主流）
                civilians.Add(new Person(id++, $"{fac}の文官（次官）", fac, PersonRole.文民)
                { birthYear = year - 45, operation = 75, intelligence = 70 });
                civilians.Add(new Person(id++, $"{fac}の官僚（局長）", fac, PersonRole.文民)
                { birthYear = year - 52, operation = 68, intelligence = 66 });
                civilians.Add(new Person(id++, $"{fac}の官僚（事務官）", fac, PersonRole.文民)
                { birthYear = year - 38, operation = 60, intelligence = 63 });

                // 文民：技術者（テクノクラート＝技才が文才以上）
                civilians.Add(new Person(id++, $"{fac}の技術官僚", fac, PersonRole.文民)
                { birthYear = year - 42, operation = 55, intelligence = 56, research = 76, engineering = 72, planning = 62, production = 66 });
            }
            return id;
        }

        // ===== 戦略デモの初期軍備（艦隊台帳・編制ツリー・指揮班）＝艦艇/軍事観測層を満たす =====

        private static readonly string[] ImperialAdmiralNames =
            { "ロイエンタール", "ミッターマイアー", "ワーレン", "ビッテンフェルト", "ケンプ", "ルッツ",
              "メックリンガー", "ファーレンハイト", "シュタインメッツ", "ミュラー", "アイゼナッハ", "ベルゲングリューン" };
        private static readonly string[] AllianceAdmiralNames =
            { "ヤン", "ビュコック", "アッテンボロー", "ウランフ", "ボロディン", "ムライ",
              "フィッシャー", "パエッタ", "アップルトン", "モートン", "チュンウーチェン", "ルグランジュ" };

        /// <summary>
        /// 戦略デモの初期軍備を勢力ごとにシードする（観測層を満たす＝ステラテジーのテスト）。
        /// 既に現役艦隊がある勢力は据え置き（実シナリオ・往復保持を尊重）。会戦入場で <see cref="FleetRoster"/> は
        /// クリアされるため、戦略へ戻るたびに空なら再シードする。<see cref="AdmiralData"/> はデモ用に実行時生成。
        /// </summary>
        private void SeedDemoMilitary()
        {
            if (DemoFactions == null) return;
            for (int i = 0; i < DemoFactions.Length; i++) SeedFactionMilitary(DemoFactions[i]);
        }

        private void SeedFactionMilitary(Faction fac)
        {
            // 冪等：既に現役艦隊があるならシードしない。
            IReadOnlyList<FleetUnitData> existing = FleetRoster.AllFleets(fac);
            if (existing != null)
                for (int i = 0; i < existing.Count; i++)
                    if (existing[i] != null && existing[i].IsActive) return;

            string[] names = fac == Faction.帝国 ? ImperialAdmiralNames : AllianceAdmiralNames;
            int ni = 0;
            AdmiralData NextAdmiral(int tier, int boost)
            {
                string nm = ni < names.Length ? names[ni] : names[ni % names.Length] + "・" + (ni / names.Length + 1);
                ni++;
                return MakeDemoAdmiral(nm, tier, fac, boost);
            }

            int reqFleet = OrderOfBattle.RequiredTier(EchelonType.艦隊);
            int reqCorps = OrderOfBattle.RequiredTier(EchelonType.軍団);
            int reqArmy = OrderOfBattle.RequiredTier(EchelonType.軍集団);

            // 軍集団（最上位）＝総司令。配下を付ける前に配属するので tier ゲートのみで通る。
            string armyName = fac == Faction.帝国 ? "帝国宇宙艦隊総軍" : "同盟宇宙艦隊総軍";
            MilitaryFormation army = OrderOfBattle.Create(EchelonType.軍集団, fac, armyName);
            OrderOfBattle.AssignCommander(army.id, NextAdmiral(reqArmy, 25));

            // 2軍団×2艦隊（編制ツリー）。
            int[][] fleetStrength = { new[] { 3000, 2600 }, new[] { 2400, 2000 } };
            for (int c = 0; c < 2; c++)
            {
                MilitaryFormation corps = OrderOfBattle.Create(EchelonType.軍団, fac, $"第{c + 1}軍団");
                OrderOfBattle.AssignCommander(corps.id, NextAdmiral(reqCorps + (c == 0 ? 1 : 0), 15));
                OrderOfBattle.AttachFormation(army.id, corps.id);

                for (int k = 0; k < 2; k++)
                {
                    FleetUnitData fleet = FleetRoster.CreateFleet(fac);
                    fleet.baseStrength = fleetStrength[c][k];
                    FleetRoster.AssignAdmiral(fleet, NextAdmiral(reqFleet, 8), reqFleet);
                    CommandStaffRules.AssignVice(fleet, NextAdmiral(reqFleet - 1, 0));
                    CommandStaffRules.AssignChief(fleet, NextAdmiral(reqFleet - 2, 0));
                    OrderOfBattle.AttachFleet(corps.id, fleet.fleetNumber);
                }
            }

            // 予備（直轄・梯団に属さない）艦隊を1つ＝編制外も観測できる。
            FleetUnitData reserve = FleetRoster.CreateFleet(fac);
            reserve.baseStrength = 1200;
            FleetRoster.AssignAdmiral(reserve, NextAdmiral(reqFleet, 4), reqFleet);
        }

        /// <summary>デモ用の提督（AdmiralData）を実行時生成する。能力は階級ブーストで緩く差をつける。</summary>
        private AdmiralData MakeDemoAdmiral(string name, int tier, Faction fac, int boost)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = name; a.rankTier = tier; a.faction = fac;
            int b = Mathf.Clamp(55 + boost, 40, 98);
            a.leadership = b;
            a.attack = Mathf.Clamp(b - 5, 40, 98);
            a.defense = Mathf.Clamp(b - 3, 40, 98);
            a.mobility = Mathf.Clamp(b - 2, 40, 98);
            a.operation = Mathf.Clamp(b - 8, 40, 98);
            a.intelligence = Mathf.Clamp(b - 6, 40, 98);
            return a;
        }

        /// <summary>
        /// 暦の年境界ごとに人物を1年ぶん老衰判定する（TIME-6 #952・LIFE-2 #152）。死亡した提督はHUDで告知する。
        /// 純ロジックは <see cref="AnnualLifecycleRules"/> に委譲（乱数は決定論のため roll を渡す）。継承は後段。
        /// </summary>
        // 勢力の教育シグナル（高校の普及率×質）＝POP労働技能の上限（#2034 SKILL-3）に使う。未設定は既定。
        private void EducationSignalOf(Faction f, out float enrollment, out float quality)
        {
            enrollment = 0.7f; quality = 0.55f; // 既定
            if (highSchools != null)
                for (int i = 0; i < highSchools.Count; i++)
                    if (highSchools[i] != null && highSchools[i].faction == f)
                    { enrollment = highSchools[i].enrollmentRate; quality = highSchools[i].quality; return; }
        }

        /// <summary>
        /// 特殊作戦部隊（#SOF・SEAL型選抜）：勢力ごとに軍人候補を選抜スコアで多段に篩い（基礎→地獄週→卒業）、
        /// 認定者を SOF 出身にする（提督として能力上昇＝戦闘で常時+5%・側背/包囲で+20%）。開始時に一度。
        /// </summary>
        private void RunSofSelection()
        {
            if (commanders == null) return;
            var byFaction = new Dictionary<Faction, List<SofCandidate>>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.role != PersonRole.軍人) continue;
                float score = SpecialForcesRules.SelectionScore(c.leadership, c.mobility, c.attack);
                if (!byFaction.TryGetValue(c.faction, out var list)) { list = new List<SofCandidate>(); byFaction[c.faction] = list; }
                list.Add(new SofCandidate(c.id, score));
            }
            foreach (var kv in byFaction)
            {
                List<int> passed = SpecialForcesRules.Funnel(kv.Value);
                for (int j = 0; j < passed.Count; j++)
                {
                    Person p = ResolveCommander(passed[j]);
                    if (p == null) continue;
                    p.isSpecialForces = true;
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{p.faction} {p.name} が特殊作戦部隊の選抜を突破（SOF認定）");
                }
            }
        }

        // 叙勲の配線パラメータ（#2263・デモ既定）
        private const int MaxMedalsPerCommander = 5;  // 1人あたり叙勲数の上限（乱発防止）
        private const int CommissionAge = 22;          // 任官年齢（在役年数の起点）

        /// <summary>
        /// 戦略の年次叙勲（#2263）。武勲ある将官（中将以上）へ階級に応じて叙勲し、
        /// 恩給見込み（`RetirementRules.PensionFactor` × 勲章の `MedalRegistry.PensionFactor`）と名誉を通知する。
        /// 史実：勲章は恩給（年金）と名誉を増す。乱発防止に1人あたり上限＋階級依存の決定論抽選。
        /// </summary>
        private void RunAnnualMedalTick()
        {
            if (commanders == null) return;
            var rp = RetirementRules.RetireParams.Default;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.IsDeceased) continue;
                if (c.rankTier < 7) continue;                                  // 中将以上の将官が対象
                if (MedalRegistry.Count(c.id) >= MaxMedalsPerCommander) continue; // 上限

                // 階級が高いほど叙勲されやすい（決定論抽選）。
                float roll = Mathf.Abs(Mathf.Sin(c.id * 12.9898f + campaignYear * 78.233f));
                roll -= Mathf.Floor(roll);
                if (roll > c.rankTier / 20f) continue;

                MedalKind kind = c.rankTier >= 9 ? MedalKind.勲功章 : MedalKind.武功章;
                float merit = Mathf.Clamp(c.rankTier * 10f, 0f, 100f);
                Decoration d = MedalRegistry.Award(c.id, kind, merit, campaignYear, $"{c.name} の武勲");

                int age = LifecycleRules.Age(c, campaignYear);
                int years = Mathf.Max(0, age - CommissionAge);
                float pension = RetirementRules.PensionFactor(years, rp) * MedalRegistry.PensionFactor(c.id);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{c.faction} {c.name} に{kind}{d.grade}を叙勲。恩給見込み {pension:0.00}（勲章×{MedalRegistry.PensionFactor(c.id):0.00}）・名誉{MedalRegistry.Prestige(c.id):0}");
            }
        }

        // 宗教/文化の配線パラメータ（#172-175/#194・デモ既定）
        private const float RulerFaithDevotion = 0.6f;      // 支配勢力の信仰の強さ（デモ既定）
        private const float ReligionStabilityScale = 10f;   // 信仰の社会効果→安定度への反映スケール
        private const float SeparatismStabilityScale = 5f;  // 分離主義→安定度低下スケール

        private void RunAnnualLifecycleTick()
        {
            campaignYear++;

            // 惑星の人口を1年ぶん動かす（出生・死亡・加齢・LIFE-3 #153）。安定度で出生/死亡が増減＝荒れた星系は人口が減る。
            // Province は StrategySession で永続＝年を跨いで人口が積み上がる。
            if (provinces != null)
                foreach (var kv in provinces)
                {
                    if (kv.Value == null) continue;
                    // 保育園（保育）で出生率↑、POP男女比の偏りで出生率↓（番が組みにくい）＝所有勢力/惑星の状態で出生を増減。
                    StarSystem sys = map != null ? map.GetSystem(kv.Key) : null;
                    float fert = (sys != null ? NurseryFertilityOf(sys.owner) : 1f)
                               * SexRules.BalanceFactor(FemaleShareOf(kv.Value));
                    var baseRates = DemographicsRules.VitalRates.Default;
                    var rates = new DemographicsRules.VitalRates(
                        baseRates.birthRate * fert, baseRates.youthAging, baseRates.workAging, baseRates.elderMortality);
                    PopulationDynamicsRules.TickYear(kv.Value, rates);

                    // POP の労働技能を1年ぶん形成（教育→OJT・#2034 配線）。教育の普及/質で上限が決まり、年々熟練が積み上がる。
                    float enroll, qual;
                    EducationSignalOf(sys != null ? sys.owner : Faction.帝国, out enroll, out qual);
                    PopLaborTickRules.TickYear(kv.Value, enroll, qual, EducationLevel.高等, PopLaborTickRules.DefaultLearnRate);

                    // 労働市場を1年ぶん（POPLAB-2/3/6 + SKILL-5 配線）：安定度#109 連動の需要へ職業配分が収束＝不安定で失業↑。
                    // 戦時（前線星系）は生産労働→軍属（総力戦#96）。技能が高い大衆ほど速く再配置（リスキリング#2034）。
                    float overall = PopLaborTickRules.OverallSkill(kv.Value);
                    float flow = LaborMarketTickRules.ReskillingFlowRate(LaborMarketTickRules.DefaultFlowRate, overall);
                    float mob = (sys != null && HasHostileFleetAt(sys)) ? LaborMarketTickRules.WarMobilizationRate : 0f;
                    LaborMarketTickRules.TickYear(kv.Value, mob, flow);
                    // 賃金を1年ぶん（POPLAB-4 配線）：労働逼迫（就業率）×技能で賃金指数が動く。
                    LaborWageTickRules.TickYear(kv.Value, LaborWageTickRules.DefaultAdjustRate);

                    // POP の消費需要を1年ぶん（#2042 配線）：購買力(賃金#1969)×人口で需要、生産力(安定度#109)で供給→充足→生活水準#181・飢餓。
                    // 不安定/占領/補給切れで生産力が落ちると必需が不足し飢餓に。富裕(高賃金)ほど上位財の需要が増える。
                    float outFactor = GovernanceRules.OutputFactor(kv.Value);
                    float popC = kv.Value.population;
                    PopConsumptionTickRules.TickYear(kv.Value, kv.Value.wageIndex,
                        popC * outFactor, popC * outFactor * 0.4f, popC * outFactor * 0.15f);

                    // 宗教(#172-175 配線)：住民の信仰を1年ぶん進め、信仰の社会効果を安定度へ緩やかに反映。
                    // 統合が進んだ惑星は支配勢力の信仰と親和（affinityMatch）。基準値はTick側で非破壊。
                    bool affinity = kv.Value.integration > 0.5f;
                    ReligionTickRules.TickYear(kv.Value, RulerFaithDevotion, affinity);
                    kv.Value.stability = Mathf.Clamp(
                        kv.Value.stability + (ReligionTickRules.SocialFactor(kv.Value) - 1f) * ReligionStabilityScale,
                        0f, 100f);

                    // 文化・民族(#194 配線)：同化/分離を1年ぶん進め、分離主義が安定度を蝕む。
                    // 戦時(前線)・低統合は分離を促す。亡命#194 の移住と相補的。
                    bool atWarHere = sys != null && HasHostileFleetAt(sys);
                    CultureTickRules.TickYear(kv.Value, kv.Value.integration > 0.5f, atWarHere);
                    float separatism = CultureTickRules.SeparatismRisk(kv.Value);
                    if (separatism > 0f)
                        kv.Value.stability = Mathf.Clamp(kv.Value.stability - separatism * SeparatismStabilityScale, 0f, 100f);
                    if (separatism > 0.6f && sys != null)
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                            $"{sys.systemName}：分離主義が高まっている（{separatism:0.0}）");
                }

            // POP の引っ越し（移住・#194）：隣接星系間で住みよい星系（安定/統合が高い）へ住民が流れる＝荒れた星系は流出で痩せる。
            // 勢力をまたぐ流れ＝亡命（難民）。総量保存・StrategySession 永続で年を跨いで効く。
            if (map != null && provinces != null)
            {
                var migParams = PopulationMigrationRules.MigrationParams.Default;
                foreach (var s in map.systems)
                {
                    if (s == null || !provinces.TryGetValue(s.id, out var from) || from == null) continue;
                    System.Collections.Generic.List<int> neighbors = map.Neighbors(s.id);
                    for (int i = 0; i < neighbors.Count; i++)
                        if (provinces.TryGetValue(neighbors[i], out var to) && to != null)
                            PopulationMigrationRules.TickPair(from, to, migParams, 1f);
                }
            }

            // 代表生産チェーン（VCHAIN-6・#2091）：森林→木材→建材→住宅 を惑星ごとに年次で流し、住宅充足で生活水準を補正。
            RunSupplyChainTick();

            // 汎用BOM（BOM-6・#2098）：消費財（食品/衣類）をレシピで生産し、需要充足で生活水準を補正。
            RunBomConsumerTick();

            // SCM計画（SCM-6・#2105）：消費財需要をMRP展開し、原材料の逼迫（ボトルネック）を勢力ごとに通知（read-only）。
            RunScmPlanTick();

            // 外交（DIPLO-6・#2119）：勢力ペアの関係をドリフトし、AIが宣戦/講和/同盟を決める。
            RunDiplomacyTick();

            // 法の支配と法と秩序（LAW-6・#2126）：勢力の法の支配＋惑星の治安を解き、安定へ反映・抑圧を通知。
            RunLawTick();

            // 叙勲（#2263）：武勲ある将官へ年次で叙勲し、恩給見込み（勲章で増）・名誉を通知。
            RunAnnualMedalTick();

            // 財政の年（#161-163 配線）：予算編成→形式財政（債務/利払い）で予算と執行の1年を閉じる。
            RunFiscalYearTick();

            // 政体進化（#117 配線）：首長制→民主(立憲君主制/共和制)or独裁(共産主義/指導者独裁)へ社会シグナルで分岐進化。
            RunRegimeEvolutionTick();

            // 政党政治（#159 配線）：民主政治の勢力で政党制が成熟度に応じ二大政党へ収束し、衆参の選挙が回り、分断危機を通知。
            RunPoliticsTick();

            // 反乱（内政→戦略の創発ループ）：慢性的な不穏が積もると星系が離反＝内政の失敗が領土喪失に直結。
            RunRebellionTick();

            // キャンペーンの勝敗（遊べる縦スライスの核）：制覇/全制圧で勝利・滅亡/敵制覇で敗北。決着で時計を止めて終了画面。
            RunCampaignVictoryCheck();

            // 年境界の自動保存（決着後は保存しない＝終了状態で上書きしない）。閉じても進行が消えない。
            if (!campaignDecided) AutoSaveCampaign();

            // 世代交代（#159 配線）：成年が結婚し夫婦が子をなして名簿に加わる＝血統が年々更新される（死は下の老衰で）。
            RunGenerationTick();

            if (commanders == null) return;
            var deceased = AnnualLifecycleRules.ProcessMortality(
                commanders, campaignYear, 1, _ => UnityEngine.Random.value);
            for (int i = 0; i < deceased.Count; i++)
            {
                Person d = deceased[i];
                int age = LifecycleRules.Age(d, campaignYear);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{d.faction} {d.name} 提督 死去（享年 {age}）");

                // 配偶者を死別（生存配偶者の婚姻を解除）。
                if (d.spouseId >= 0)
                {
                    Person spouse = commanders.Find(x => x != null && x.id == d.spouseId);
                    PersonMarriageRules.Widow(spouse);
                }

                // ネームド資産の相続/没収（NASSET-4/6・#2063）：故人の固有資産を同勢力の最高位の存命司令へ相続、不在なら国家へ没収。
                var estate = NamedAssetRegistry.OwnedByPerson(d.id);
                if (estate.Count > 0)
                {
                    Person heir = FindHeir(d);
                    for (int e = 0; e < estate.Count; e++)
                    {
                        var asset = estate[e];
                        if (!AssetTransferRules.CanTransfer(asset)) continue; // 称号など不可は本人と消える
                        if (heir != null) AssetTransferRules.Inherit(asset, heir.id);
                        else AssetTransferRules.Confiscate(asset, d.faction);
                    }
                    string dest = heir != null ? $"{heir.name} が相続" : "国家へ没収";
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{d.name} の資産（{estate.Count}件）→ {dest}");
                }

                // 金融資産の相続（NFIN-6・#2070）：故人の保有持分を最高位の相続人へ、不在なら国家へ。
                var fin = FinancialHoldingRegistry.OwnedByPerson(d.id);
                if (fin.Count > 0)
                {
                    Person heir = FindHeir(d);
                    for (int e = 0; e < fin.Count; e++)
                    {
                        if (heir != null) { fin[e].ownerKind = AssetOwnerKind.人物; fin[e].ownerPersonId = heir.id; }
                        else { fin[e].ownerKind = AssetOwnerKind.国家; fin[e].ownerFaction = d.faction; }
                    }
                }

                // 不動産の細分化（NFIN-5/6・#2070＝分地相続）：故人の権利証を複数の相続人へ等分＝惑星の持分が細かく分かれる。
                var deeds = PropertyDeedRegistry.OwnedByPerson(d.id);
                if (deeds.Count > 0)
                {
                    var heirs = FindHeirs(d, 3); // 上位3名で分割（細分化傾向）
                    for (int e = 0; e < deeds.Count; e++)
                    {
                        if (heirs.Count > 0) PropertyFragmentationRules.FragmentOnInheritance(deeds[e], heirs);
                        else { deeds[e].ownerKind = AssetOwnerKind.国家; deeds[e].ownerFaction = d.faction; } // 相続人不在は国家へ（細分化せず）
                    }
                    if (heirs.Count > 1)
                        NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{d.name} の所領が {heirs.Count} 人へ分割相続（細分化）");
                }
            }

            // 人物の財産を1年ぶん（PFIN-6・#2056 配線）：俸給#1969 から特性で貯金/投資/浪費し財産が増減。
            // デモ：id で財産行動特性を割り振る。投資型は変動（暴落リスク#185）・浪費型は貯まらない・貯金型は堅実。
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.deathYear != 0) continue;
                c.financialTrait = (FinancialTrait)(System.Math.Abs(c.id) % 3); // 0貯金/1投資/2浪費
                float salary = 50f + c.rankTier * 50f; // 俸給 proxy（階級#14 比例・WAGE#1969）
                float ret = 0.05f + (c.financialTrait == FinancialTrait.投資 ? (UnityEngine.Random.value - 0.5f) * 0.6f : 0f); // 投資は±変動
                PersonFinanceTickRules.TickYear(c, salary, ret);
            }

            // ネームド資産（NASSET-6・#2063 配線）：人物/国家が固有名の資産（旗艦・宮殿等）を持ち、収益→財産・値上がり・相続。
            SeedNamedAssets();                                  // デモ資産シード（冪等）
            NamedAssetTickRules.TickYear(ResolveCommander);     // 純収益→所有者 wealth#2056・時価値上がり
            for (int f = 0; f < DemoFactions.Length; f++)       // 国家資産の純収益は国庫#163 相当へ（デモはログのみ）
            {
                float fInc = NamedAssetEffectRules.FactionAnnualIncome(DemoFactions[f]);
                if (fInc != 0f)
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{DemoFactions[f]} 国有資産収益 {fInc:0}");
            }

            // ネームド金融資産・不動産（NFIN-6・#2070 配線）：株式/債券/投資信託の配当・惑星所有権の地代→財産、紙くず化。
            SeedFinancialAssets();                                  // デモ金融/不動産シード（冪等）
            MaybeCrashAStock();                                     // 紙くず化デモ（暴落#185）
            NamedFinancialTickRules.TickYear(ResolveCommander);    // 配当/地代→所有者 wealth#2056
            for (int f = 0; f < DemoFactions.Length; f++)          // 国家の金融/不動産収益（デモはログ）
            {
                float fInc = NamedFinancialTickRules.FactionAnnualIncome(DemoFactions[f]);
                if (fInc != 0f)
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"{DemoFactions[f]} 金融/地代収益 {fInc:0}");
            }

            // 国家・惑星の行政物資消費（STATEDEM-6・#2077 配線）：産出を行政・インフラが消費し、不足で統治が逼迫＝安定度低下。
            RunStateConsumptionTick();

            // 士官学校（#155 LIFE-5 細分化）：各校が幼年学校→士官学校→大学校 の多段で篩い、任官者をロスターへ供給。
            if (academies != null && commanders.Count < OfficerRosterCap)
                for (int i = 0; i < academies.Count; i++)
                    if (academies[i] != null) RunMilitaryAcademy(academies[i]);

            // 退役（#530-536 配線）：階級別の停年に達した現役将校を退役へ（元帥は終身）。退役者は昇進・入校の対象外＝以後は老衰で退場。
            RunRetirementTick();

            // 陸軍大学校のエリート街道（#SCHOOL-AGE 配線）：現役将校を大学校へ入校（学校配属＝艦隊配属不可）→卒業で参謀＝恩賜の軍刀組→昇進優遇。
            RunWarCollegeCareerTick();

            // 人事の空席補充（#152）と捕虜の処遇（#154）：死亡/退役/捕虜で空いた要職を後任補充、捕虜は解放/登用/処断で処遇。
            RunPersonnelTurnoverTick();

            // 大学（文民/技術者の輩出・LIFE-6/7）も年境界で回す。
            RunUniversityTick();

            // 朝廷の権威の動態（官僚制基盤）：戦乱で武家台頭＝権威↓（戦国化）／平時は律令が回復。以後の考課・銓衡・内政に効く。
            RunCourtAuthorityTick();

            // 文官の官歴（官僚制基盤）：文民ネームドに位階を叙し、考課で叙位・五位の壁を回す（朝廷の権威で効く）。
            RunBureaucracyTick();

            // 文官の銓衡配属（官僚制基盤）：叙位された文官を官位相当＋考課で宰相（文官要職）へ任命する（式部省の選叙）。
            RunCivilAppointmentTick();

            // 総督（地方官）の銓衡配属（官僚制基盤）：所有星系ごとに官位相当の文官を配属＝受領/国司。
            RunGovernorAppointmentTick();

            // 省庁の配属（官僚制基盤・二官八省）：文民を省庁へ配属し、その行政が内政（中央監督）に効く。
            RunMinistryStaffingTick();
        }

        /// <summary>
        /// 退役（#530-536 配線）：現役将校が階級別の停年に達したら退役へ編入（元帥 tier は終身＝対象外）。
        /// 退役者はロスターに残り（資産・老衰死の対象）、昇進・大学校入校からは外れる＝現役→退役→死亡 の一方向。
        /// </summary>
        private void RunRetirementTick()
        {
            if (commanders == null) return;
            var prm = RetirementRules.RetireParams.Default;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.deathYear != 0) continue;
                if (c.serviceStatus != ServiceStatus.現役) continue;
                int age = LifecycleRules.Age(c, campaignYear);
                if (RetirementRules.ShouldRetireByAge(age, c.rankTier, prm))
                {
                    c.serviceStatus = ServiceStatus.退役;
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{c.faction} {c.name} 退役（停年・享年 {age}）");
                }
            }
        }

        // --- 陸軍大学校のエリート街道（#SCHOOL-AGE 配線） ---

        /// <summary>勢力の昇進ドクトリンを現在の政体形態から導く（民主＝実力主義／専制・君主・共産＝学閥主義＝政体が軍人事に効く）。</summary>
        private static PromotionDoctrine WarCollegeDoctrine(Faction f)
        {
            var camp = StrategySession.Campaign;
            FactionState s = camp != null ? CampaignRules.GetState(camp, f) : null;
            if (s != null) return GovernmentFormRules.PromotionDoctrineOf(s.governmentForm);
            return f == Faction.帝国 ? PromotionDoctrine.学閥主義 : PromotionDoctrine.実力主義; // フォールバック
        }

        /// <summary>
        /// 大学校入学→学校配属（艦隊配属不可）→卒業で大学校卒=参謀＝恩賜の軍刀組→昇進優遇 を年次で回す（#SCHOOL-AGE）。
        /// 数式・状態遷移は <see cref="WarCollegeCareerRules"/>（Core）へ委譲し、ここは起きた事象を通知へ流すだけ。
        /// </summary>
        private void RunWarCollegeCareerTick()
        {
            if (commanders == null) return;
            var events = new List<CareerEvent>();
            WarCollegeCareerRules.TickYear(commanders, campaignYear, WarCollegeDoctrine, events);
            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent e = events[i];
                string rank = RankSystem.ResolveRankNameOrDefault(null, e.rankTier);
                switch (e.kind)
                {
                    case CareerEventKind.入校:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{e.faction} {e.personName} 陸軍大学校へ入校（学校配属＝艦隊配属を離れる）");
                        break;
                    case CareerEventKind.卒業:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{e.faction} {e.personName} 陸軍大学校を卒業（参謀＝星）");
                        break;
                    case CareerEventKind.恩賜の軍刀:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{e.faction} {e.personName} 恩賜の軍刀組（大学校卒首席級）＝エリート街道へ");
                        break;
                    case CareerEventKind.昇進:
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{e.faction} {e.personName} {rank}へ昇進");
                        break;
                }
            }
        }

        // --- 財政の年（#161-163 配線）：予算編成→執行→債務で1年を閉じる ---

        /// <summary>
        /// 世代交代の年次 Tick（#159 配線）：名簿（commanders）の成年が結婚し、夫婦が子をなして名簿に加わる。
        /// 数値は <see cref="GenerationTickRules"/>（→PersonMarriageRules/ChildbirthRules/HeredityRules）へ委譲。死（老衰）は下流の ProcessMortality が担う。
        /// </summary>
        private void RunGenerationTick()
        {
            if (commanders == null) return;
            var res = GenerationTickRules.TickYear(
                commanders, campaignYear,
                () => nextPersonId++,
                () => UnityEngine.Random.value,
                new GenerationTickRules.GenerationParams(0.4f, OfficerRosterCap),
                ChildbirthRules.FertilityParams.Default,
                HeredityRules.HeredityParams.Default);

            if (res.marriages > 0 || res.births > 0)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"世代交代：{res.marriages} 組が結婚・{res.births} 人誕生（名簿 {commanders.Count} 名）");
        }

        /// <summary>世代交代ループの種＝結婚適齢の若者（男女）を勢力ごとに数名置く。これが結婚→出産→加齢→死で世代が回る。</summary>
        private int SeedFoundingYouth(int startId, int year)
        {
            int id = startId;
            Faction[] facs = { Faction.帝国, Faction.同盟 };
            for (int fi = 0; fi < facs.Length; fi++)
            {
                Faction fac = facs[fi];
                for (int k = 0; k < 4; k++) // 男2・女2
                {
                    Sex sex = (k % 2 == 0) ? Sex.男性 : Sex.女性;
                    commanders.Add(new Person(id++, $"{fac}の士{k + 1}", fac, PersonRole.軍人)
                    {
                        sex = sex,
                        birthYear = year - (20 + k), // 成年（20〜23歳）
                        rankTier = 0,
                        leadership = 45, attack = 45, defense = 45, mobility = 45, operation = 45, intelligence = 45,
                    });
                }
            }
            return id;
        }

        // 人材の男女比（デモ政策＝銀英伝風：帝国は家父長的で女性が少なく、同盟は平等で多め）。
        private const float ImperialFemaleShare = 0.08f;
        private const float AllianceFemaleShare = 0.35f;

        /// <summary>新任人材に性別を割り当てる（所有勢力の政策男女比・決定論 roll）。性的指向は別軸の検討項目（未実装）。</summary>
        private void AssignSexes(System.Collections.Generic.List<Person> people, Faction faction)
        {
            if (people == null) return;
            float fshare = faction == Faction.同盟 ? AllianceFemaleShare : ImperialFemaleShare;
            for (int i = 0; i < people.Count; i++)
                if (people[i] != null)
                    people[i].sex = UnityEngine.Random.value < fshare ? Sex.女性 : Sex.男性;
        }

        /// <summary>軍学校＝多段の選抜（幼年学校→士官学校→大学校・#155 細分化）。軍属層から入校し、任官者だけを士官名簿へ。</summary>
        private void RunMilitaryAcademy(Academy a)
        {
            // 中学校→高校 の教育チェーンが候補の母数（進学率の複利）と素質（質の上乗せ）を左右する。
            ResolveEducation(a.faction, a.quality, out float enroll, out float eq);
            int sitters = Mathf.Clamp(Mathf.FloorToInt(RecruitablePoolOf(a.faction) * enroll), 0, 20);
            if (sitters <= 0) return;
            var eff = new Academy(a.schoolId, a.faction, a.name, a.capacity, eq);
            var results = MilitaryAcademyRules.RunMilitarySession(eff, campaignYear, sitters, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += results.Count;
            AssignSexes(results, a.faction);

            int 退校 = 0, 幼 = 0, 士 = 0, 参 = 0;
            Person 首席 = null;
            for (int k = 0; k < results.Count; k++)
            {
                Person p = results[k];
                switch (p.militaryDegree)
                {
                    case MilitaryDegree.大学校卒: 参++; break;
                    case MilitaryDegree.士官学校卒: 士++; break;
                    case MilitaryDegree.幼年学校卒: 幼++; break;
                    default: 退校++; break;
                }
                if (MilitaryAcademyRules.IsCommissioned(p.militaryDegree))
                {
                    commanders.Add(p); // 任官（士官学校卒以上）のみ士官名簿へ
                    if (p.hammockNumber == 1) 首席 = p;
                }
            }
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{a.faction} {a.name} 入校{sitters}：参謀{参}/士官{士}/幼年{幼}/退校{退校}"
                + (首席 != null ? $"（首席 tier{首席.rankTier}）" : ""));
        }

        /// <summary>その勢力の徴募源（軍属 #96）＝所有星系の Province を合算（士官学校の輩出数の素・#155）。</summary>
        private float RecruitablePoolOf(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float part = FemaleMilitaryParticipationOf(faction); // 女性の軍参加政策（帝国は低い＝家父長制）
            float pool = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                {
                    // POP の性別構成で徴募源をゲート＝男性＋女性参加ぶんだけ軍に就ける。
                    float elig = SexRules.EligibleMilitaryFraction(FemaleShareOf(prov), part);
                    pool += OccupationRules.RecruitablePool(prov) * elig;
                }
            return pool * NurseryLaborOf(faction); // 保育園＝働く親が増える（労働参加）
        }

        // 女性の軍参加政策（デモ＝銀英伝風：帝国は家父長的で女性の軍参加が低く徴募源が細る／同盟は平等で全員）。
        private const float ImperialFemaleMilitaryParticipation = 0.1f;
        private const float AllianceFemaleMilitaryParticipation = 1f;
        private float FemaleMilitaryParticipationOf(Faction faction)
            => faction == Faction.同盟 ? AllianceFemaleMilitaryParticipation : ImperialFemaleMilitaryParticipation;

        /// <summary>惑星の女性割合（POP の男女比・コホート未設定なら均衡0.5）。</summary>
        private static float FemaleShareOf(Province prov)
            => prov != null && prov.demographics != null ? prov.demographics.femaleShare : SexRules.BalancedFemaleShare;

        /// <summary>その勢力の文民候補（官吏層 #110）＝所有星系の Province を合算（大学の輩出数の素・#156/#157）。</summary>
        private float CivilCandidatePoolOf(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float pool = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                    pool += OccupationRules.Workers(prov, Occupation.官吏);
            return pool * NurseryLaborOf(faction); // 保育園＝働く親が増える（労働参加）
        }

        /// <summary>
        /// 暦の年境界で大学が新任文民（文官/技術者）を輩出し文民ロスターへ供給する（#156/#157 LIFE-6/7）。
        /// 文民も老衰し（LIFE-2）、大学が補充する＝官界の世代交代。<see cref="OfficerAcademyRules"/> の文民版。
        /// </summary>
        private void RunUniversityTick()
        {
            if (civilians == null) return;
            // 文民の老衰（人事の世代交代）
            var deceased = AnnualLifecycleRules.ProcessMortality(civilians, campaignYear, 1, _ => UnityEngine.Random.value);
            for (int i = 0; i < deceased.Count; i++)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{deceased[i].faction} {deceased[i].name} 文官 死去（享年 {LifecycleRules.Age(deceased[i], campaignYear)}）");

            // 上級教育の卒業（官吏/工員層が支える・PERF上限で打ち止め）
            if (civilians.Count >= CivilRosterCap) return;
            if (universities != null)
                for (int i = 0; i < universities.Count; i++)
                {
                    University u = universities[i];
                    if (u == null) continue;
                    if (u.track == CareerTrack.科挙) RunImperialExam(u);
                    else RunTechnocratGraduation(u);
                }
            // 高専（中学校→高専の実務技術者路・高校を経ない別ルート）も年境界で輩出。
            if (colleges != null)
                for (int i = 0; i < colleges.Count; i++)
                    if (colleges[i] != null) RunTechnicalCollege(colleges[i]);
            // 短大／専門学校（高校卒後2年・中堅人材＝官界/現場の裾野）も輩出。
            if (juniorColleges != null)
                for (int i = 0; i < juniorColleges.Count; i++)
                    if (juniorColleges[i] != null) RunJuniorCollege(juniorColleges[i]);
            if (vocationalSchools != null)
                for (int i = 0; i < vocationalSchools.Count; i++)
                    if (vocationalSchools[i] != null) RunVocationalSchool(vocationalSchools[i]);
        }

        /// <summary>
        /// 文官の官歴を1年ぶん回す（官僚制基盤＝<see cref="BureaucracyCareerRules"/> へ委譲）。文民ネームドに位階を叙し、
        /// 考課（能×徳×績）で叙位／貶位する。<b>五位の壁</b>は朝廷の権威が高いとき（律令が機能）だけ越えられる
        /// ＝封建の世（権威低）では門閥以外は貴族へ上がれない。叙位の節目（五位突破）は通知へ。
        /// </summary>
        private void RunBureaucracyTick()
        {
            if (civilians == null || civilians.Count == 0) return;
            var changes = new List<BureaucracyCareerRules.CareerChange>();
            BureaucracyCareerRules.TickYear(
                civilians, courtAuthority != null ? courtAuthority.authority : 0f,
                campaignYear, BureaucracyCareerRules.CareerParams.Default, changes);

            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i].kind != BureaucracyCareerRules.CareerEventKind.五位突破) continue;
                Person p = FindCivilian(changes[i].personId);
                if (p == null) continue;
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{p.faction} {p.name} 叙従五位下＝貴族に列す（{JapaneseCourtRankRules.Name(changes[i].from)}→{JapaneseCourtRankRules.Name(changes[i].to)}）");
            }

            // 清廉度の動態（汚職）：監督（朝廷の権威）が弱いほど汚職が育つ＝名誉職化が腐敗を生む（考課の徳・内政に跳ね返る）。
            float authority = courtAuthority != null ? courtAuthority.authority : 0f;
            for (int i = 0; i < civilians.Count; i++)
            {
                Person c = civilians[i];
                if (c == null || c.role != PersonRole.文民 || c.merit == null) continue;
                c.merit.integrity = OfficialIntegrityRules.Tick(
                    c.merit.integrity, authority, OfficialIntegrityRules.IntegrityParams.Default);
            }
        }

        /// <summary>戦乱度＝前線/交戦の広がり（敵対艦隊が停泊する星系の割合）。朝廷の権威の動態の入力。</summary>
        private float WarIntensity()
        {
            if (map == null || map.systems.Count == 0) return 0f;
            int war = 0, total = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                total++;
                if (HasHostileFleetAt(s)) war++;
            }
            return total > 0 ? (float)war / total : 0f;
        }

        /// <summary>朝廷の権威を1年ぶん動かす（戦乱で武家台頭↓／平時は律令回復↑）。形骸化の段階が変われば通知。</summary>
        private void RunCourtAuthorityTick()
        {
            if (courtAuthority == null) return;
            RitsuryoPhase before = RitsuryoFormalizationRules.PhaseOf(courtAuthority.authority);
            CourtAuthorityRules.TickYear(courtAuthority, WarIntensity(), CourtAuthorityRules.AuthorityParams.Default);
            RitsuryoPhase after = RitsuryoFormalizationRules.PhaseOf(courtAuthority.authority);
            if (after != before)
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                    $"朝廷の権威が変動：{before}→{after}（官職の名実が{((int)after > (int)before ? "乖離" : "一致")}へ）");
        }

        private Person FindCivilian(int id)
        {
            if (civilians == null) return null;
            for (int i = 0; i < civilians.Count; i++)
                if (civilians[i] != null && civilians[i].id == id) return civilians[i];
            return null;
        }

        /// <summary>科挙＝多段の選抜（童試→郷試→会試→殿試・#156 細分化）。官吏層から受験し、進士だけを高官として登用する。</summary>
        private void RunImperialExam(University u)
        {
            ResolveEducation(u.faction, u.quality, out float enroll, out float eq);
            int sitters = Mathf.Clamp(Mathf.FloorToInt(CivilCandidatePoolOf(u.faction) * enroll), 0, 40);
            if (sitters <= 0) return;
            var eff = new University(u.schoolId, u.faction, u.name, u.track, u.capacity, eq);
            var results = ImperialExamRules.RunExamSession(eff, campaignYear, sitters, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += results.Count;
            AssignSexes(results, u.faction);

            int 生員 = 0, 挙人 = 0, 貢士 = 0, 進士 = 0;
            Person 状元 = null;
            for (int k = 0; k < results.Count; k++)
            {
                Person p = results[k];
                switch (p.examDegree)
                {
                    case ExamDegree.生員: 生員++; break;
                    case ExamDegree.挙人: 挙人++; break;
                    case ExamDegree.貢士: 貢士++; break;
                    case ExamDegree.進士:
                        進士++;
                        if (p.examRank == 1) 状元 = p;
                        civilians.Add(p); // 進士のみ高官として登用（科挙の狭き門）
                        break;
                }
            }
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{u.faction} {u.name} 科挙 受験{sitters}：進士{進士}/貢士{貢士}/挙人{挙人}/生員{生員}"
                + (状元 != null ? $"（状元 tier{状元.rankTier}）" : ""));
        }

        /// <summary>その勢力の技術者候補（工員層 #110）＝所有星系の Province を合算（高専の輩出数の素・#157）。</summary>
        private float TechnicalCandidatePoolOf(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float pool = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                    pool += OccupationRules.Workers(prov, Occupation.工員);
            return pool * NurseryLaborOf(faction); // 保育園＝働く親が増える（労働参加）
        }

        /// <summary>高専＝中学校から直接入る実務技術者路（高校を経ない・#157）。工員層から入学し技術者を文民ロスターへ。</summary>
        private void RunTechnicalCollege(TechnicalCollege c)
        {
            // 高専は高校を経ない＝中学校のみの教育チェーン（includeHighSchool:false）。
            ResolveEducation(c.faction, c.quality, false, out float enroll, out float eq);
            int intake = TechnicalCollegeRules.Intake(c, TechnicalCandidatePoolOf(c.faction) * enroll);
            if (intake <= 0) return;
            var eff = new TechnicalCollege(c.schoolId, c.faction, c.name, c.capacity, eq);
            var grads = TechnicalCollegeRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, c.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{c.faction} {c.name} {grads.Count}名 卒業（技術者）");
        }

        /// <summary>短大の卒業（高校卒後2年・行政中堅文民を官吏層から・#156）。</summary>
        private void RunJuniorCollege(JuniorCollege c)
        {
            ResolveEducation(c.faction, c.quality, out float enroll, out float eq); // 高校卒後＝高校チェーン込み
            int intake = JuniorCollegeRules.Intake(c, CivilCandidatePoolOf(c.faction) * enroll);
            if (intake <= 0) return;
            var eff = new JuniorCollege(c.schoolId, c.faction, c.name, c.capacity, eq);
            var grads = JuniorCollegeRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, c.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{c.faction} {c.name} {grads.Count}名 卒業（行政中堅）");
        }

        /// <summary>専門学校の卒業（高校卒後2年・実務specialist を工員層から・#157）。</summary>
        private void RunVocationalSchool(VocationalSchool s)
        {
            ResolveEducation(s.faction, s.quality, out float enroll, out float eq);
            int intake = VocationalSchoolRules.Intake(s, TechnicalCandidatePoolOf(s.faction) * enroll);
            if (intake <= 0) return;
            var eff = new VocationalSchool(s.schoolId, s.faction, s.name, s.capacity, eq);
            var grads = VocationalSchoolRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, s.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{s.faction} {s.name} {grads.Count}名 卒業（実務）");
        }

        /// <summary>テクノクラート大学の卒業（技術者を文民ロスターへ・#157）。</summary>
        private void RunTechnocratGraduation(University u)
        {
            ResolveEducation(u.faction, u.quality, out float enroll, out float eq);
            int intake = UniversityRules.Intake(u, CivilCandidatePoolOf(u.faction) * enroll);
            if (intake <= 0) return;
            var eff = new University(u.schoolId, u.faction, u.name, u.track, u.capacity, eq);
            var grads = UniversityRules.GraduateCohort(eff, campaignYear, intake, nextPersonId, _ => UnityEngine.Random.value);
            nextPersonId += grads.Count;
            AssignSexes(grads, u.faction);
            civilians.AddRange(grads);
            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{u.faction} {u.name} {grads.Count}名 卒業（{u.track}）");
        }

    }
}
