using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦役セーブのファイル入出力＋SO解決（FND-2 #495・Unity層）。世界状態（<see cref="CampaignState"/>）を
    /// `persistentDataPath/campaign_save.json` にJSON保存/読込する（変換は <see cref="CampaignSerializer"/>）。
    /// 復元時に星系の所有 FactionData を `Resources/Factions` から<b>名前で解決</b>する（直列化は名前のみ＝SO参照を持たない）。
    /// 設定/戦績は `GameSettings`、会戦セットアップは `SaveManager`、戦役世界状態はこちら（FND-2＝CampaignState 一本化）。
    /// </summary>
    public static class CampaignSaveManager
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "campaign_save.json");

        /// <summary>戦役の世界状態をJSON保存する。</summary>
        public static void Save(CampaignState campaign)
        {
            if (campaign == null) return;
            string json = CampaignSerializer.ToJson(campaign, prettyPrint: true);
            File.WriteAllText(SavePath, json);
        }

        /// <summary>戦役の世界状態＋ネームド人物ロスターをJSON保存する（提督/文官を継続するための版）。</summary>
        public static void Save(CampaignState campaign, IEnumerable<Person> people)
        {
            if (campaign == null) return;
            File.WriteAllText(SavePath, CampaignSerializer.ToJson(campaign, people, prettyPrint: true));
        }

        /// <summary>セーブから人物ロスターのみ復元する（無ければ空）。世界状態は <see cref="Load"/>。</summary>
        public static List<Person> LoadPeople()
        {
            if (!File.Exists(SavePath)) return new List<Person>();
            CampaignSaveData save = CampaignSerializer.Parse(File.ReadAllText(SavePath));
            return CampaignSerializer.ReadPeople(save);
        }

        /// <summary>
        /// 要塞の駐留艦隊名簿（#40）から、いない艦隊のIDを取り除く。
        /// セーブ間で全滅・除去された艦隊のIDが残ると「駐留しているのに実体が無い」幽霊になるため、
        /// 艦隊レジストリを読み終えたあとに1回だけ通す。名簿が空の旧セーブでは何もしない。
        /// </summary>
        private static void PruneFortressGarrisons(GalaxyMap map, StrategicFleetRegistry reg)
        {
            if (map == null || map.corridors == null || reg == null) return;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                FortressGarrisonRules.PruneMissing(c.fortress, reg.GetFleet);
            }
        }

        /// <summary>
        /// 戦役の<b>全状態</b>（銀河/勢力/財政/政体/人物/戦略艦隊/統一時間）を保存する（continue・全永続化）。
        /// </summary>
        public static void SaveSession(CampaignState campaign, IEnumerable<Person> people, StrategicFleetRegistry reg, GameClock clock, Dictionary<int, Province> provinces = null, CourtAuthority court = null, ProtagonistCareerSave career = null)
        {
            if (campaign == null) return;
            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignSerializer.WritePeople(save, people);
            CampaignSerializer.WriteFleets(save, reg);
            CampaignSerializer.WriteProvinces(save, provinces);
            CampaignSerializer.WriteClock(save, clock);
            // #38：航行中の援軍も保存する（到着は絶対 game-秒なのでクロックと一緒に往復すれば残り時間が保たれる）。
            CampaignSerializer.WriteReinforcements(save, StrategySession.Reinforcements);
            // #稟議完成②：進行中の稟議と決裁カードも保存する（適用済みフラグごと＝ロード後に二重執行しない）。
            CampaignSerializer.WritePetitions(save.petitions, StrategySession.Petitions);
            CampaignSerializer.WritePetitions(save.fleetPetitions, StrategySession.FleetPetitions);
            CampaignSerializer.WriteDecisions(save, StrategySession.Decisions);
            if (court != null) save.courtAuthority = court.authority; // 朝廷の権威を永続（官僚制基盤）
            if (career != null && career.hasData) save.protagonistCareer = career; // 主人公の立身出世を永続（TKO #2477・P1-c）
            WriteAdmiralGrowth(save); // 全提督の会戦成長を安定キーで永続（ADM-2 #2303）
            File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
        }

        /// <summary>
        /// セーブから全状態を <see cref="StrategySession"/> へ復元する（Map/Reg/Campaign/Clock＋人物は PendingPeople へ）。
        /// 呼び出し側が Strategy シーンを再ロードして盤面を再構築する。成功で true。
        /// Province 内政（安定度/統合/経済/希少資源）も復元する（demographics/workforce/skills の細部は再構築）。
        /// </summary>
        public static bool LoadSession()
        {
            if (!File.Exists(SavePath)) return false;
            CampaignSaveData save = CampaignSerializer.Parse(File.ReadAllText(SavePath));
            if (save == null) return false;

            CampaignState campaign = CampaignSerializer.FromSaveData(save);
            ResolveFactionData(campaign, save);
            StrategySession.Map = campaign.map;
            StrategySession.Reg = CampaignSerializer.ReadFleets(save, campaign.map);
            // #40 駐留艦隊：セーブ間で消えた艦隊IDが名簿に残らないよう掃除する
            //（艦隊レジストリを読み終えた後でないと生死が判定できないのでここで行う）。
            PruneFortressGarrisons(campaign.map, StrategySession.Reg);
            StrategySession.Campaign = campaign;
            StrategySession.Clock = CampaignSerializer.ReadClock(save);
            // #38：援軍台帳を復元（旧セーブは空＝援軍なし）。台帳の現在時刻はクロックへ合わせる。
            StrategySession.Reinforcements = CampaignSerializer.ReadReinforcements(save, StrategySession.Clock);
            // #稟議完成②：稟議と決裁カードを復元（旧セーブは空＝案件なしで読める）。
            CampaignSerializer.ReadPetitions(save.petitions, StrategySession.Petitions);
            CampaignSerializer.ReadPetitions(save.fleetPetitions, StrategySession.FleetPetitions);
            StrategySession.Decisions = CampaignSerializer.ReadDecisions(save);
            StrategySession.Provinces = CampaignSerializer.ReadProvinces(save); // 内政を復元（空=後方互換）
            StrategySession.PendingPeople = CampaignSerializer.ReadPeople(save);
            StrategySession.CourtAuthority = new CourtAuthority(save.courtAuthority); // 朝廷の権威を復元（官僚制基盤）
            StrategySession.PendingProtagonistCareer = (save.protagonistCareer != null && save.protagonistCareer.hasData)
                ? save.protagonistCareer : null; // 主人公の立身出世を復元（TKO #2477・P1-c。ProtagonistCareerDirector が消費）
            ReadAdmiralGrowth(save); // 全提督の会戦成長を GrowthRegistry へ復元（ADM-2 #2303・安定キー）
            return true;
        }

        /// <summary>
        /// 全提督の会戦成長（<see cref="GrowthRegistry"/>）を安定キー（<see cref="AdmiralData.admiralName"/>）で書き出す（ADM-2 #2303）。
        /// 実行時キー（EntityKey）は不安定なため、`ContentDatabase` の全提督を走査し成長があるものだけ名前で保存する。
        /// </summary>
        private static void WriteAdmiralGrowth(CampaignSaveData save)
        {
            if (save == null) return;
            IReadOnlyList<AdmiralData> all = ContentDatabase.AllAdmirals();
            for (int i = 0; i < all.Count; i++)
            {
                AdmiralData a = all[i];
                if (a == null || string.IsNullOrEmpty(a.admiralName)) continue;
                Growth g = GrowthRegistry.Get(EntityKey.Of(a));
                if (g == null || g.experience <= 0f) continue;
                save.admiralGrowth.Add(new AdmiralGrowthSave
                {
                    admiralName = a.admiralName,
                    experience = g.experience,
                    archetype = (int)g.archetype,
                });
            }
        }

        /// <summary>セーブの提督成長を <see cref="GrowthRegistry"/> へ復元する（名前→AdmiralData を `ContentDatabase` で解決）。</summary>
        private static void ReadAdmiralGrowth(CampaignSaveData save)
        {
            if (save == null || save.admiralGrowth == null) return;
            for (int i = 0; i < save.admiralGrowth.Count; i++)
            {
                AdmiralGrowthSave e = save.admiralGrowth[i];
                if (e == null || string.IsNullOrEmpty(e.admiralName)) continue;
                AdmiralData a = ContentDatabase.AdmiralByName(e.admiralName);
                if (a == null) continue;
                GrowthRegistry.GetOrCreate(EntityKey.Of(a), (GrowthArchetype)e.archetype).experience = e.experience;
            }
        }

        /// <summary>セーブが存在するか。</summary>
        public static bool HasSave() => File.Exists(SavePath);

        /// <summary>戦役の世界状態を読み込む（無ければ null）。所有 FactionData は名前で解決する。</summary>
        public static CampaignState Load()
        {
            if (!File.Exists(SavePath)) return null;
            string json = File.ReadAllText(SavePath);
            CampaignSaveData save = CampaignSerializer.Parse(json);
            if (save == null) return null;

            CampaignState campaign = CampaignSerializer.FromSaveData(save);
            ResolveFactionData(campaign, save);
            return campaign;
        }

        /// <summary>セーブを削除する。</summary>
        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }

        /// <summary>
        /// 復元後、平データの factionName を頼りに星系の <see cref="StarSystem.ownerData"/> を解決する（FND-1 #496＝索引は
        /// <see cref="ContentDatabase"/> に集約）。直列化は名前のみ＝多勢力対応。見つからなければ enum owner のまま＝後方互換。
        /// </summary>
        private static void ResolveFactionData(CampaignState campaign, CampaignSaveData save)
        {
            if (campaign == null || campaign.map == null || save == null) return;
            for (int i = 0; i < save.systems.Count; i++)
            {
                StarSystemSave ss = save.systems[i];
                if (ss == null || string.IsNullOrEmpty(ss.ownerFactionName)) continue;
                StarSystem sys = campaign.map.GetSystem(ss.id);
                if (sys == null) continue;
                FactionData fd = ContentDatabase.FactionByName(ss.ownerFactionName);
                if (fd != null) sys.ownerData = fd;
            }
        }
    }
}
