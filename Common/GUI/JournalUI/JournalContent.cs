using System;
using ShatteredIllusion.Content.NPCs.BossAI.GreatAntlionCharger;
using Terraria;
using Terraria.ID;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    /// <summary>
    /// THE ONE FILE YOU EDIT TO CHANGE THE JOURNAL'S STRUCTURE.
    ///
    /// Add a page:      add  .Page("MyPage")  to a chapter, then write its text in the hjson (below).
    /// Remove a page:   delete its .Page(...) line. (Leftover hjson text is simply ignored.)
    /// Add a chapter:   j.Chapter("MyChapter").Page("First").Page("Second");
    /// Reorder:         move the lines. Page numbers, the table of contents and spreads update themselves.
    ///
    /// LOCKING (all of these are one-liners):
    ///   .Page("Id", p => NPC.downedBoss1)          page opens once the condition is true
    ///   .Page("Id", someUnlock)                    page opens when a shared, named unlock opens
    ///   j.Chapter("Id", p => NPC.downedBoss1)      whole chapter locked (its title shows as ??? in the contents)
    ///   [[UnlockId]] at the start of a paragraph   only that paragraph is locked (in the hjson body text)
    ///   Manual unlock: j.Unlock("Id") then, from anywhere on the client, JournalPlayer.Local.Unlock("Id");
    ///
    /// LOCALIZATION (Localization/en-US_Mods.ShatteredIllusion.Journal.hjson), under Mods.ShatteredIllusion.Journal:
    ///   Chapters.[Chapter].Title                    chapter name (contents page + default page heading)
    ///   Chapters.[Chapter].Pages.[Page].Body        page text. Blank line = paragraph break
    ///   Chapters.[Chapter].Pages.[Page].Title       optional heading (defaults to the chapter name)
    ///   Chapters.[Chapter].Pages.[Page].LockedHint  optional "how to unlock" text shown on the locked page
    ///   Unlocks.[UnlockId].Hint                     optional hint shared by everything behind that unlock
    ///
    /// Locked pages can still be turned to, but they are blank (hovering one shows its hint). When something unlocks,
    /// the player gets a toast and the journal button glows. The next time they look at that page it "remembers":
    /// a warm haze rises and the letters surface one by one, ghosted and drifting into focus (once per page).
    /// Pages keep their unlock state per player. Test with  /journal unlock Id  |  /journal lock Id  |  /journal reset
    /// </summary>
    internal static class JournalContent
    {
        public static void Define(JournalBuilder j)
        {
            // ---- Shared, named unlocks ----
            JournalUnlock kingSlime = j.Unlock("KingSlime", p => NPC.downedSlimeKing);
            JournalUnlock alan = j.Unlock("alan", p => DownedSystem.downedGreatAntlionCharger);
            JournalUnlock secretWord = j.Unlock("SecretWord");

            // A town NPC's page opens once they are in the world (or you've rescued them).
            Func<int, Func<bool>, Func<Player, bool>> town = (npc, saved) => p => NPC.AnyNPCs(npc) || (saved != null && saved());

            // ---- Chapters ----
            j.Chapter("HowToRead")
                .Page("Hello")
                .Page("Controls");

            j.Chapter("Entry1")
                .Page("Main")
                .Page("Parry");

            j.Chapter("Entry2")
                .Page("Main")
                .Page("KingSlime", kingSlime)
                .Page("Antlion", alan)
                .Page("EyeOfCthulhu", p => NPC.downedBoss1)
                .Page("EvilBoss", p => NPC.downedBoss2)
                .Page("QueenBee", p => NPC.downedQueenBee)
                .Page("Skeletron", p => NPC.downedBoss3)
                .Page("Deerclops", p => NPC.downedDeerclops)
                .Page("WallOfFlesh", p => Main.hardMode);

            j.Chapter("Entry3")
                .Page("Main")
                .Page("Guide", town(NPCID.Guide, null))
                .Page("Merchant", town(NPCID.Merchant, null))
                .Page("Nurse", town(NPCID.Nurse, null))
                .Page("Demolitionist", town(NPCID.Demolitionist, null))
                .Page("ArmsDealer", town(NPCID.ArmsDealer, null))
                .Page("Dryad", town(NPCID.Dryad, null))
                .Page("Angler", town(NPCID.Angler, () => NPC.savedAngler))
                .Page("Stylist", town(NPCID.Stylist, () => NPC.savedStylist))
                .Page("Golfer", town(NPCID.Golfer, () => NPC.savedGolfer))
                .Page("Zoologist", town(NPCID.BestiaryGirl, null))
                .Page("DyeTrader", town(NPCID.DyeTrader, null))
                .Page("Painter", town(NPCID.Painter, null))
                .Page("Mechanic", town(NPCID.Mechanic, () => NPC.savedMech))
                .Page("GoblinTinkerer", town(NPCID.GoblinTinkerer, () => NPC.savedGoblin))
                .Page("Tavernkeep", town(NPCID.DD2Bartender, () => NPC.savedBartender))
                .Page("WitchDoctor", town(NPCID.WitchDoctor, null))
                .Page("Clothier", town(NPCID.Clothier, null))
                .Page("PartyGirl", town(NPCID.PartyGirl, null));

            j.Chapter("Credits").StartOnLeft()
                .Page("Lead")
                .Page("Artists")
                .Page("Audio")
                .Page("Writers");

            /* DEMO chapter showing every kind of lock. Delete it (and its hjson block) once you've seen it work.
            j.Chapter("SealedPages").StartOnLeft()
                .Page("Open")
                .Page("Boss", kingSlime)                       // opens when King Slime has been defeated
                .Page("Manual", secretWord)                    // opens only through code
                .Page("Night", p => !Main.dayTime);            // opens the first time the player is in the world at night
            */
            j.Chapter("Credits")
                .Page("Lead")
                .Page("Artist");
        }
    }
}
