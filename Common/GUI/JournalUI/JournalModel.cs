using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ShatteredIllusion.Common.GUI.JournalUI
{
    // ============================================================================================
    //  Data model + registry for the journal.
    //
    //  You never need to touch this file to add content. Edit JournalContent.cs (declare chapters,
    //  pages and locks) and the localization hjson (write the text).
    // ============================================================================================

    /// <summary>
    /// Something a page, chapter or paragraph can be locked behind. Create one with
    /// <see cref="JournalBuilder.Unlock(string)"/> (manual) or
    /// <see cref="JournalBuilder.Unlock(string, Func{Player, bool})"/> (automatic, checked a few times a second).
    /// Unlocks are stored per player and never re-lock on their own.
    /// </summary>
    public sealed class JournalUnlock
    {
        public string Id { get; }

        /// <summary>Null for manual unlocks (those only open when you call <c>JournalPlayer.Local.Unlock(id)</c>).</summary>
        internal Func<Player, bool> Condition { get; }

        public bool IsManual => Condition == null;

        internal JournalUnlock(string id, Func<Player, bool> condition)
        {
            Id = id;
            Condition = condition;
        }
    }

    public sealed class JournalChapter
    {
        public string Id { get; internal set; }
        internal JournalUnlock[] Locks = Array.Empty<JournalUnlock>();
        internal readonly List<JournalPage> PageList = new List<JournalPage>();
        internal bool StartOnLeft;

        /// <summary>Index (into the flat page list) of this chapter's first page.</summary>
        public int FirstPageIndex { get; internal set; }

        public IReadOnlyList<JournalPage> Pages => PageList;
    }

    public sealed class JournalPage
    {
        public string Id { get; internal set; }
        public JournalChapter Chapter { get; internal set; }

        /// <summary>Position in the flat page list (0 = first page of the book, left side).</summary>
        public int Index { get; internal set; }

        /// <summary>A blank filler page inserted so a chapter can start on a left-hand page.</summary>
        public bool IsPadding { get; internal set; }

        /// <summary>Everything this page needs: its chapter's locks plus its own. Empty = always open.</summary>
        internal JournalUnlock[] Locks = Array.Empty<JournalUnlock>();

        /// <summary>Unique, stable key used for saving "already revealed" state: "Chapter.Page".</summary>
        public string Key => IsPadding ? null : Chapter.Id + "." + Id;

        public bool IsFirstInChapter => !IsPadding && Chapter.PageList.Count > 0 && Chapter.PageList[0] == this;
    }

    /// <summary>Fluent builder handed to <see cref="JournalContent.Define"/>.</summary>
    public sealed class JournalBuilder
    {
        internal readonly List<JournalChapter> Chapters = new List<JournalChapter>();
        internal readonly Dictionary<string, JournalUnlock> Unlocks = new Dictionary<string, JournalUnlock>();

        /// <summary>A manual unlock: opens only when you call <c>JournalPlayer.Local.Unlock(id)</c>.</summary>
        public JournalUnlock Unlock(string id) => Register(id, null);

        /// <summary>An automatic unlock: opens the first time <paramref name="condition"/> returns true for the local player.</summary>
        public JournalUnlock Unlock(string id, Func<Player, bool> condition) => Register(id, condition ?? throw new ArgumentNullException(nameof(condition)));

        public ChapterBuilder Chapter(string id) => AddChapter(id, null);
        public ChapterBuilder Chapter(string id, JournalUnlock lockedBy) => AddChapter(id, lockedBy);
        public ChapterBuilder Chapter(string id, Func<Player, bool> lockedUntil) => AddChapter(id, Register("Chapter." + id, lockedUntil));

        internal JournalUnlock Register(string id, Func<Player, bool> condition)
        {
            if (Unlocks.TryGetValue(id, out JournalUnlock existing))
            {
                Warn($"Unlock id '{id}' was declared twice; the first declaration is used.");
                return existing;
            }

            JournalUnlock unlock = new JournalUnlock(id, condition);
            Unlocks[id] = unlock;
            return unlock;
        }

        private ChapterBuilder AddChapter(string id, JournalUnlock lockedBy)
        {
            if (Chapters.Any(c => c.Id == id))
            {
                Warn($"Chapter id '{id}' was declared twice; the second declaration is ignored.");
                return new ChapterBuilder(this, new JournalChapter { Id = id });   // detached; swallowed
            }

            JournalChapter chapter = new JournalChapter { Id = id };
            if (lockedBy != null)
            {
                chapter.Locks = new[] { lockedBy };
            }

            Chapters.Add(chapter);
            return new ChapterBuilder(this, chapter);
        }

        internal static void Warn(string message)
        {
            ModContent.GetInstance<ShatteredIllusion>()?.Logger.Warn("[Journal] " + message);
        }
    }

    public sealed class ChapterBuilder
    {
        private readonly JournalBuilder _owner;
        private readonly JournalChapter _chapter;

        internal ChapterBuilder(JournalBuilder owner, JournalChapter chapter)
        {
            _owner = owner;
            _chapter = chapter;
        }

        /// <summary>Add an always-open page.</summary>
        public ChapterBuilder Page(string id) => AddPage(id, null);

        /// <summary>Add a page locked behind a shared, named unlock.</summary>
        public ChapterBuilder Page(string id, JournalUnlock lockedBy) => AddPage(id, lockedBy);

        /// <summary>Add a page that opens the first time the condition is true, e.g. <c>.Page("Secret", p => NPC.downedBoss1)</c>.</summary>
        public ChapterBuilder Page(string id, Func<Player, bool> lockedUntil)
            => AddPage(id, _owner.Register(_chapter.Id + "." + id, lockedUntil));

        /// <summary>Make this chapter begin on a left-hand page (a blank page is inserted if needed).</summary>
        public ChapterBuilder StartOnLeft()
        {
            _chapter.StartOnLeft = true;
            return this;
        }

        private ChapterBuilder AddPage(string id, JournalUnlock lockedBy)
        {
            if (_chapter.PageList.Any(p => p.Id == id))
            {
                JournalBuilder.Warn($"Page '{_chapter.Id}.{id}' was declared twice; the second declaration is ignored.");
                return this;
            }

            _chapter.PageList.Add(new JournalPage
            {
                Id = id,
                Chapter = _chapter,
                Locks = lockedBy == null ? _chapter.Locks : _chapter.Locks.Append(lockedBy).ToArray(),
            });
            return this;
        }
    }

    /// <summary>The built, flattened journal. Rebuilt on mod load.</summary>
    internal static class JournalRegistry
    {
        public static JournalChapter[] Chapters { get; private set; } = Array.Empty<JournalChapter>();
        public static JournalPage[] Pages { get; private set; } = Array.Empty<JournalPage>();
        public static Dictionary<string, JournalUnlock> Unlocks { get; private set; } = new Dictionary<string, JournalUnlock>();

        public static void Build()
        {
            JournalBuilder builder = new JournalBuilder();
            JournalContent.Define(builder);

            List<JournalPage> flat = new List<JournalPage>();
            List<JournalChapter> chapters = new List<JournalChapter>();

            foreach (JournalChapter chapter in builder.Chapters)
            {
                if (chapter.PageList.Count == 0)
                {
                    JournalBuilder.Warn($"Chapter '{chapter.Id}' has no pages and was skipped.");
                    continue;
                }

                if (chapter.StartOnLeft && flat.Count % 2 == 1)
                {
                    flat.Add(new JournalPage { Id = "~pad", Chapter = chapter, IsPadding = true, Index = flat.Count });
                }

                chapter.FirstPageIndex = flat.Count;
                foreach (JournalPage page in chapter.PageList)
                {
                    page.Index = flat.Count;
                    flat.Add(page);
                }
                chapters.Add(chapter);
            }

            Chapters = chapters.ToArray();
            Pages = flat.ToArray();
            Unlocks = builder.Unlocks;
        }

        public static void Clear()
        {
            Chapters = Array.Empty<JournalChapter>();
            Pages = Array.Empty<JournalPage>();
            Unlocks = new Dictionary<string, JournalUnlock>();
        }

        /// <summary>Chapters that contain a page opened by this unlock (used for the "Journal updated" toast).</summary>
        public static IEnumerable<JournalChapter> ChaptersUsing(string unlockId)
        {
            return Chapters.Where(c => c.PageList.Any(p => p.Locks.Any(l => l.Id == unlockId)));
        }
    }

    // ============================================================================================
    //  Localization access. Every string the journal shows lives in the hjson under
    //  Mods.ShatteredIllusion.Journal (see Localization/en-US_Mods.ShatteredIllusion.Journal.hjson).
    // ============================================================================================

    internal readonly struct BodyParagraph
    {
        public readonly string Text;
        /// <summary>Unlock id when the paragraph starts with [[UnlockId]]; null otherwise.</summary>
        public readonly string GateId;

        public BodyParagraph(string text, string gateId)
        {
            Text = text;
            GateId = gateId;
        }
    }

    internal static class JournalText
    {
        public const string Root = "Mods.ShatteredIllusion.Journal";

        private static readonly Regex GateRegex = new Regex(@"^\s*\[\[\s*([A-Za-z0-9_.\-]+)\s*\]\]\s?(.*)$", RegexOptions.Compiled);
        private static readonly Dictionary<JournalPage, BodyParagraph[]> BodyCache = new Dictionary<JournalPage, BodyParagraph[]>();
        private static string _cacheCulture;

        /// <summary>Changes whenever the language does; caches keyed on text should be dropped when it changes.</summary>
        public static string CultureStamp => Language.ActiveCulture?.Name ?? string.Empty;

        private static void EnsureFreshCache()
        {
            string culture = CultureStamp;
            if (culture != _cacheCulture)
            {
                _cacheCulture = culture;
                BodyCache.Clear();
            }
        }

        public static string Ui(string key) => Language.GetTextValue($"{Root}.UI.{key}");

        public static string Ui(string key, params object[] args) => Language.GetTextValue($"{Root}.UI.{key}", args);

        public static string ChapterTitle(JournalChapter chapter) => Language.GetTextValue($"{Root}.Chapters.{chapter.Id}.Title");

        private static string PageBase(JournalPage page) => $"{Root}.Chapters.{page.Chapter.Id}.Pages.{page.Id}";

        /// <summary>The page's own title, or null when it doesn't define one (then the chapter title is used).</summary>
        public static string PageTitle(JournalPage page)
        {
            string key = PageBase(page) + ".Title";
            return Language.Exists(key) ? Language.GetTextValue(key) : null;
        }

        public static string Heading(JournalPage page)
        {
            string title = PageTitle(page);
            if (title != null)
            {
                return title;
            }

            string chapterTitle = ChapterTitle(page.Chapter);
            return page.IsFirstInChapter ? chapterTitle : Ui("Continued", chapterTitle);
        }

        /// <summary>Body text split into paragraphs, with any [[UnlockId]] gate prefix peeled off.</summary>
        public static BodyParagraph[] Body(JournalPage page)
        {
            EnsureFreshCache();
            if (BodyCache.TryGetValue(page, out BodyParagraph[] cached))
            {
                return cached;
            }

            string raw = Language.GetTextValue(PageBase(page) + ".Body").Replace("\r", string.Empty).Trim('\n');
            string[] paragraphs = raw.Split('\n');
            BodyParagraph[] result = new BodyParagraph[paragraphs.Length];

            for (int i = 0; i < paragraphs.Length; i++)
            {
                Match match = GateRegex.Match(paragraphs[i]);
                result[i] = match.Success
                    ? new BodyParagraph(match.Groups[2].Value, match.Groups[1].Value)
                    : new BodyParagraph(paragraphs[i], null);
            }

            BodyCache[page] = result;
            return result;
        }

        /// <summary>What to tell the player about how to open a locked page.</summary>
        public static string LockedHint(JournalPage page)
        {
            string key = PageBase(page) + ".LockedHint";
            if (Language.Exists(key))
            {
                return Language.GetTextValue(key);
            }

            // Fall back to the hint of the first lock that has one (newest lock first: it's the most specific).
            for (int i = page.Locks.Length - 1; i >= 0; i--)
            {
                string hint = HintFor(page.Locks[i].Id);
                if (hint != null)
                {
                    return hint;
                }
            }

            return Ui("DefaultHint");
        }

        /// <summary>Hint for a named unlock (Journal.Unlocks.&lt;Id&gt;.Hint), or null if none was written.</summary>
        public static string HintFor(string unlockId)
        {
            string key = $"{Root}.Unlocks.{unlockId}.Hint";
            return Language.Exists(key) ? Language.GetTextValue(key) : null;
        }
    }
}
