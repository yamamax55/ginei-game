namespace Ginei
{
    /// <summary>
    /// 主人公の立身出世（一人称・TKO #2477・P1-c #2477）の状態 ↔ セーブ平データ（<see cref="ProtagonistCareerSave"/>）の
    /// 変換（唯一の窓口・純ロジック）。階級/能力（<see cref="Person"/>）・武勲台帳（<see cref="MeritRecord"/>）・在席の主命
    /// （<see cref="SovereignMandate"/>）・月次ループ状態・会戦戦果インボックスを保存/復元する。各 Core 型を作り直さず
    /// フィールドを写すだけ（additive）。enum は int で持つ（JsonUtility 安全・前方互換）。決定論・test-first。
    /// </summary>
    public static class ProtagonistCareerSerializer
    {
        /// <summary>主人公の立身出世状態を平データへ写す（<paramref name="hero"/> 必須・他は null/0 可）。</summary>
        public static ProtagonistCareerSave Capture(Person hero, MeritRecord merit, SovereignMandate mandate,
            int lastCouncilMonth, int nextMandateId, float pendingBattleDamage, int pendingBattleVictories, int pendingBattleCount,
            Growth growth = null, ProtagonistChronicle chronicle = null)
        {
            if (hero == null) return new ProtagonistCareerSave { hasData = false };
            var d = new ProtagonistCareerSave
            {
                hasData = true,
                faction = (int)hero.faction,
                personId = hero.id,
                name = hero.name,
                rankTier = hero.rankTier,
                leadership = hero.leadership,
                attack = hero.attack,
                defense = hero.defense,
                mobility = hero.mobility,
                operation = hero.operation,
                intelligence = hero.intelligence,
                meritPoints = merit != null ? merit.points : 0f,
                meritExploitCount = merit != null ? merit.exploitCount : 0,
                meritPromotionsApplied = merit != null ? merit.meritPromotionsApplied : 0,
                lastCouncilMonth = lastCouncilMonth,
                nextMandateId = nextMandateId,
                pendingBattleDamage = pendingBattleDamage,
                pendingBattleVictories = pendingBattleVictories,
                pendingBattleCount = pendingBattleCount,
            };
            if (growth != null)
            {
                d.hasGrowth = true;
                d.growthExperience = growth.experience;
                d.growthArchetype = (int)growth.archetype;
            }
            if (mandate != null)
            {
                d.hasMandate = true;
                d.mandateId = mandate.id;
                d.mandateFaction = (int)mandate.faction;
                d.mandateIssuerId = mandate.issuerId;
                d.mandateAssigneeId = mandate.assigneeId;
                d.mandateKind = (int)mandate.kind;
                d.mandateStatus = (int)mandate.status;
                d.mandateIssuedMonth = mandate.issuedMonth;
                d.mandateDueMonth = mandate.dueMonth;
                d.mandateObjective = mandate.objective ?? "";
            }
            if (chronicle != null)
            {
                d.chronicleDropped = chronicle.droppedCount;
                for (int i = 0; i < chronicle.entries.Count; i++)
                {
                    ChronicleEntry e = chronicle.entries[i];
                    if (e == null) continue;
                    d.chronicle.Add(new ChronicleEntrySave { monthIndex = e.monthIndex, kind = (int)e.kind, note = e.note ?? "" });
                }
            }
            return d;
        }

        /// <summary>平データの一代記を既存の <see cref="ProtagonistChronicle"/> へ復元する（既存を消してから入れ直す）。</summary>
        public static void ApplyChronicle(ProtagonistCareerSave d, ProtagonistChronicle into)
        {
            if (into == null) return;
            into.Clear();
            if (d == null || !d.hasData || d.chronicle == null) return;
            for (int i = 0; i < d.chronicle.Count; i++)
            {
                ChronicleEntrySave e = d.chronicle[i];
                if (e == null) continue;
                into.entries.Add(new ChronicleEntry(e.monthIndex, (ChronicleEventKind)e.kind, e.note));
            }
            into.droppedCount = d.chronicleDropped;
        }

        /// <summary>平データの階級・能力を既存の <see cref="Person"/> へ反映する（基準は AdmiralData だが在席値を継続）。</summary>
        public static void ApplyPerson(ProtagonistCareerSave d, Person hero)
        {
            if (d == null || !d.hasData || hero == null) return;
            hero.rankTier = d.rankTier;
            hero.leadership = d.leadership;
            hero.attack = d.attack;
            hero.defense = d.defense;
            hero.mobility = d.mobility;
            hero.operation = d.operation;
            hero.intelligence = d.intelligence;
        }

        /// <summary>平データから武勲台帳を復元する（hasData=false なら空台帳）。</summary>
        public static MeritRecord ToMerit(ProtagonistCareerSave d)
        {
            var rec = new MeritRecord(d != null ? d.personId : 0);
            if (d != null && d.hasData)
            {
                rec.points = d.meritPoints;
                rec.exploitCount = d.meritExploitCount;
                rec.meritPromotionsApplied = d.meritPromotionsApplied;
            }
            return rec;
        }

        /// <summary>平データから在席の主命を復元する（無ければ null）。</summary>
        public static SovereignMandate ToMandate(ProtagonistCareerSave d)
        {
            if (d == null || !d.hasData || !d.hasMandate) return null;
            return new SovereignMandate
            {
                id = d.mandateId,
                faction = (Faction)d.mandateFaction,
                issuerId = d.mandateIssuerId,
                assigneeId = d.mandateAssigneeId,
                kind = (MandateKind)d.mandateKind,
                status = (MandateStatus)d.mandateStatus,
                issuedMonth = d.mandateIssuedMonth,
                dueMonth = d.mandateDueMonth,
                objective = d.mandateObjective ?? "",
            };
        }
    }
}
