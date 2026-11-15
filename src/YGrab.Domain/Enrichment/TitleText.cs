// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace YGrab.Domain;

/// <summary>
/// Folds the titles and the artist names of two catalogs to the same text, so that they compare equal.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Normalize"/> keeps the version words of a title, such as live, mix or 5.1, so two
/// recordings with different versions stay different. It removes:
/// </para>
/// <list type="bullet">
/// <item><description>the credits in brackets, such as <c>(feat. A)</c>, <c>(ft. A)</c> and <c>(with A)</c></description></item>
/// <item><description>the brackets and the dash suffixes that name an edition, such as <c>(Deluxe)</c> and <c>- Remastered 2011</c></description></item>
/// </list>
/// <para>
/// <see cref="Plain"/> also removes the notes that a provider adds to a title, and a leading "the".
/// </para>
/// </remarks>
public static class TitleText
{
    private const string AlbumVersionNote = "album version";
    private const string ExplicitNote = "explicit";
    private const string LeadingThe = "the ";
    private const char TypographicApostrophe = '\u2019';
    private const char TypographicHyphen = '\u2010';
    private const char Apostrophe = '\'';
    private const char Hyphen = '-';
    private const char Underscore = '_';
    private const char ClosingParenthesis = ')';

    private static readonly string[] _creditPrefixes = ["feat. ", "feat ", "ft. ", "ft ", "with "];
    private static readonly SearchValues<char> _openers = SearchValues.Create("([");
    private static readonly SearchValues<char> _closers = SearchValues.Create(")]");
    private static readonly SearchValues<string> _editionWords = SearchValues.Create(
        ["remaster", "deluxe", "expanded", "anniversary", "edition", "bonus"],
        StringComparison.OrdinalIgnoreCase
    );

    private delegate int NoteLength(ReadOnlySpan<char> fromOpener);

    /// <summary>
    /// The text in lower case, without credits, edition notes and punctuation.
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var folded = text.Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant()
            .Replace(TypographicApostrophe, Apostrophe)
            .Replace(TypographicHyphen, Hyphen);

        var withoutCredits = Remove(folded, CreditLength);
        var withoutEdition = Remove(withoutCredits, EditionLength);
        var withoutSuffix = CutEditionSuffix(withoutEdition);

        return Words(withoutSuffix.Replace("&", " and ", StringComparison.Ordinal));
    }

    /// <summary>
    /// The text that two credits or two titles share, whatever note a provider adds.
    /// </summary>
    public static string Plain(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = Normalize(Remove(text, VersionNoteLength));
        return normalized.StartsWith(LeadingThe, StringComparison.Ordinal)
            ? normalized[LeadingThe.Length..]
            : normalized;
    }

    /// <summary>
    /// The title without the brackets that name an edition, or the title itself when nothing else is left.
    /// </summary>
    public static string StripEdition(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var stripped = Remove(title, EditionLength).Trim();
        return string.IsNullOrWhiteSpace(stripped) ? title : stripped;
    }

    private static string Remove(string text, NoteLength lengthOf)
    {
        var result = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            if (
                _openers.Contains(text[index]) && lengthOf(text.AsSpan(index)) is > 0 and var length
            )
            {
                while (result.Length > 0 && char.IsWhiteSpace(result[^1]))
                {
                    result.Length--;
                }

                index += length;
                continue;
            }

            result.Append(text[index]);
            index++;
        }

        return result.ToString();
    }

    private static int CreditLength(ReadOnlySpan<char> fromOpener)
    {
        var end = fromOpener[1..].IndexOfAny(_closers);
        return end >= 0 && IsCredit(fromOpener.Slice(1, end)) ? end + 2 : 0;
    }

    private static int EditionLength(ReadOnlySpan<char> fromOpener)
    {
        var end = fromOpener[1..].IndexOfAny(_closers);
        return end >= 0 && fromOpener.Slice(1, end).ContainsAny(_editionWords) ? end + 2 : 0;
    }

    // "(Album Version" takes everything up to its closing bracket and one more parenthesis, which
    // covers "(Album Version (Explicit))"
    private static int VersionNoteLength(ReadOnlySpan<char> fromOpener)
    {
        var note = fromOpener[1..];
        if (note.StartsWith(AlbumVersionNote, StringComparison.OrdinalIgnoreCase))
        {
            var length = 1 + AlbumVersionNote.Length;
            var end = fromOpener[length..].IndexOfAny(_closers);
            if (end < 0)
            {
                return fromOpener.Length;
            }

            length += end + 1;
            return length < fromOpener.Length && fromOpener[length] == ClosingParenthesis
                ? length + 1
                : length;
        }

        var closing = 1 + ExplicitNote.Length;
        return
            note.StartsWith(ExplicitNote, StringComparison.OrdinalIgnoreCase)
            && closing < fromOpener.Length
            && _closers.Contains(fromOpener[closing])
            ? closing + 1
            : 0;
    }

    private static bool IsCredit(ReadOnlySpan<char> inner)
    {
        foreach (var prefix in _creditPrefixes)
        {
            if (inner.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // " - Remastered 2011" ends the title, unless a later dash comes first
    private static string CutEditionSuffix(string text)
    {
        var from = 0;
        while (text.AsSpan(from).IndexOf(Hyphen) is >= 0 and var offset)
        {
            var dash = from + offset;
            from = dash + 1;
            if (
                dash == 0
                || from >= text.Length
                || !char.IsWhiteSpace(text[dash - 1])
                || !char.IsWhiteSpace(text[from])
            )
            {
                continue;
            }

            var segment = text.AsSpan(from);
            var next = segment.IndexOf(Hyphen);
            if ((next < 0 ? segment : segment[..next]).ContainsAny(_editionWords))
            {
                return text[..dash].TrimEnd();
            }
        }

        return text;
    }

    private static string Words(string text)
    {
        var result = new StringBuilder(text.Length);
        var separated = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsWordRune(rune))
            {
                separated = true;
                continue;
            }

            if (separated && result.Length > 0)
            {
                result.Append(' ');
            }

            separated = false;
            result.Append(rune);
        }

        return result.ToString();
    }

    private static bool IsWordRune(Rune rune) =>
        Rune.IsLetter(rune) || Rune.IsNumber(rune) || rune.Value is Underscore or Apostrophe;
}
