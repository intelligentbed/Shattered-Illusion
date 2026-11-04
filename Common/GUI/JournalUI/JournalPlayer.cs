using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    internal enum JournalState
    {
        /// <summary>Readable text.</summary>
        Open,
        /// <summary>Still sealed: shown as shimmering glyphs.</summary>
        Locked,
        /// <summary>Unlocked, but the player hasn't watched the reveal yet. The journal plays it when it's next visible.</summary>
        Revealing,
    }

    /// <summary>
    /// Per-player journal progress: which unlocks are open and which reveals the player has already seen.
    /// Purely client-side; it reads synced world flags (NPC.downedX etc.) rather than sending packets.
    /// </summary>
    internal class JournalPlayer : ModPlayer
    {
        public static JournalPlayer Local => Main.LocalPlayer.GetModPlayer<JournalPlayer>();

        /// <summary>Set when a world is entered; unlock polling waits briefly so synced world flags have arrived.</summary>
        public static uint WorldEnterTick;

        private const int SettleTicks = 90;
        private const int PollInterval = 20;

        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private readonly HashSet<string> _revealed = new HashSet<string>();
        private bool _initialized;

        /// <summary>True when something is unlocked that the player hasn't watched open yet (drives the button glow).</summary>
        public bool HasNews { get; private set; }

        public IEnumerable<string> UnlockedIds => _unlocked;

        // ------------------------------------------------------------------ save / load

        public override void SaveData(TagCompound tag)
        {
            tag["unlocked"] = _unlocked.ToList();
            tag["revealed"] = _revealed.ToList();
            tag["initialized"] = _initialized;
        }

        public override void LoadData(TagCompound tag)
        {
            _unlocked.Clear();
            _revealed.Clear();
            foreach (string id in tag.GetList<string>("unlocked")) _unlocked.Add(id);
            foreach (string key in tag.GetList<string>("revealed")) _revealed.Add(key);
            _initialized = tag.GetBool("initialized");
            HasNews = false;
        }

        // ------------------------------------------------------------------ public API

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        /// <summary>Opens an unlock (manual or automatic). Safe to call repeatedly.</summary>
        public void Unlock(string id)
        {
            if (!JournalRegistry.Unlocks.TryGetValue(id, out JournalUnlock unlock))
            {
                JournalBuilder.Warn($"Unlock('{id}') was called but no unlock with that id is declared in JournalContent.");
                return;
            }

            Grant(unlock, false);
        }

        /// <summary>Debug: seals an unlock again and makes its reveal replay.</summary>
        public void Relock(string id)
        {
            _unlocked.Remove(id);
            _revealed.RemoveWhere(key => key.EndsWith("#" + id, StringComparison.Ordinal));

            foreach (JournalPage page in JournalRegistry.Pages)
            {
                if (!page.IsPadding && page.Locks.Any(l => l.Id == id))
                {
                    _revealed.Remove(page.Key);
                }
            }

            RecomputeNews();
        }

        /// <summary>Debug: forget everything.</summary>
        public void ResetAll()
        {
            _unlocked.Clear();
            _revealed.Clear();
            RecomputeNews();
        }

        // ------------------------------------------------------------------ state queries

        public JournalState StateOf(JournalPage page)
        {
            if (page.IsPadding || page.Locks.Length == 0)
            {
                return JournalState.Open;
            }

            for (int i = 0; i < page.Locks.Length; i++)
            {
                if (!_unlocked.Contains(page.Locks[i].Id))
                {
                    return JournalState.Locked;
                }
            }

            return _revealed.Contains(page.Key) ? JournalState.Open : JournalState.Revealing;
        }

        public JournalState StateOfGate(JournalPage page, string gateId)
        {
            if (!JournalRegistry.Unlocks.ContainsKey(gateId) || !_unlocked.Contains(gateId))
            {
                return JournalState.Locked;
            }

            return _revealed.Contains(page.Key + "#" + gateId) ? JournalState.Open : JournalState.Revealing;
        }

        public bool ChapterHasNews(JournalChapter chapter)
        {
            foreach (JournalPage page in chapter.Pages)
            {
                if (PageHasNews(page))
                {
                    return true;
                }
            }
            return false;
        }

        private bool PageHasNews(JournalPage page)
        {
            JournalState state = StateOf(page);
            if (state == JournalState.Revealing)
            {
                return true;
            }

            if (state != JournalState.Open)
            {
                return false;
            }

            foreach (BodyParagraph paragraph in JournalText.Body(page))
            {
                if (paragraph.GateId != null && StateOfGate(page, paragraph.GateId) == JournalState.Revealing)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Called by the journal once a reveal animation has finished.</summary>
        public void MarkRevealed(string key)
        {
            if (_revealed.Add(key))
            {
                RecomputeNews();
            }
        }

        public void RecomputeNews()
        {
            HasNews = false;
            foreach (JournalChapter chapter in JournalRegistry.Chapters)
            {
                if (ChapterHasNews(chapter))
                {
                    HasNews = true;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ polling

        public override void PostUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || JournalRegistry.Unlocks.Count == 0)
            {
                return;
            }

            if (Main.GameUpdateCount - WorldEnterTick < SettleTicks)
            {
                return;
            }

            if (_initialized && (Main.GameUpdateCount + (uint)Player.whoAmI) % PollInterval != 0)
            {
                return;
            }

            Poll();
        }

        private void Poll()
        {
            // The very first poll for a character opens whatever is already true silently, so a veteran
            // character doesn't get a wall of reveals the moment this system ships.
            bool silent = !_initialized;

            foreach (JournalUnlock unlock in JournalRegistry.Unlocks.Values)
            {
                if (unlock.Condition == null || _unlocked.Contains(unlock.Id))
                {
                    continue;
                }

                bool met = false;
                try
                {
                    met = unlock.Condition(Player);
                }
                catch (Exception e)
                {
                    JournalBuilder.Warn($"Condition for unlock '{unlock.Id}' threw: {e.Message}");
                }

                if (met)
                {
                    Grant(unlock, silent);
                }
            }

            if (!_initialized)
            {
                _initialized = true;
                if (silent)
                {
                    MarkEverythingRevealed();
                }
            }
        }

        private void Grant(JournalUnlock unlock, bool silent)
        {
            if (!_unlocked.Add(unlock.Id))
            {
                return;
            }

            if (!silent)
            {
                JournalPage page = JournalRegistry.Pages.FirstOrDefault(pg => !pg.IsPadding && pg.Locks.Length > 0 && pg.Locks[pg.Locks.Length - 1].Id == unlock.Id);
                JournalChapter chapter = JournalRegistry.ChaptersUsing(unlock.Id).FirstOrDefault();
                JournalToast.Push(page != null
                    ? JournalText.PageTitle(page) ?? JournalText.ChapterTitle(page.Chapter)
                    : chapter != null ? JournalText.ChapterTitle(chapter) : null);
            }

            RecomputeNews();
        }

        private void MarkEverythingRevealed()
        {
            foreach (JournalPage page in JournalRegistry.Pages)
            {
                if (page.IsPadding)
                {
                    continue;
                }

                _revealed.Add(page.Key);
                foreach (BodyParagraph paragraph in JournalText.Body(page))
                {
                    if (paragraph.GateId != null)
                    {
                        _revealed.Add(page.Key + "#" + paragraph.GateId);
                    }
                }
            }

            RecomputeNews();
        }
    }

    /// <summary>Dev/test command, shipped on purpose: /journal unlock|lock|reset|list</summary>
    public class JournalCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "journal";
        public override string Usage => "/journal <unlock|lock|list|reset> [id|all]";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            JournalPlayer jp = JournalPlayer.Local;
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
            string id = args.Length > 1 ? args[1] : null;

            switch (verb)
            {
                case "unlock" when id == "all":
                    foreach (string unlockId in JournalRegistry.Unlocks.Keys.ToList())
                    {
                        jp.Unlock(unlockId);
                    }
                    break;
                case "unlock" when id != null:
                    if (!JournalRegistry.Unlocks.ContainsKey(id)) { caller.Reply($"No unlock '{id}'. Try /journal list", Color.OrangeRed); return; }
                    jp.Unlock(id);
                    break;
                case "lock" when id == "all":
                    foreach (string unlockId in JournalRegistry.Unlocks.Keys.ToList())
                    {
                        jp.Relock(unlockId);
                    }
                    break;
                case "lock" when id != null:
                    jp.Relock(id);
                    break;
                case "reset":
                    jp.ResetAll();
                    break;
                case "list":
                    foreach (JournalUnlock u in JournalRegistry.Unlocks.Values)
                    {
                        caller.Reply($"{(jp.IsUnlocked(u.Id) ? "[x]" : "[ ]")} {u.Id}{(u.IsManual ? " (manual)" : string.Empty)}");
                    }
                    break;
                default:
                    caller.Reply(Usage);
                    break;
            }
        }
    }
}
