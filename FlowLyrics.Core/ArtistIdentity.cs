using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FlowLyrics.Core;

public sealed record CharacterCredit(string Character, string VoiceActor);

/// <summary>Matching-only identities. Never use tokenized credits as display metadata.</summary>
public sealed class ArtistIdentity
{
	public string WholeArtist { get; }
	public IReadOnlyList<CharacterCredit> CharacterCredits { get; }
	public IReadOnlyList<string> SearchCredits { get; }
	public IReadOnlySet<string> PerformerTokens { get; }
	private readonly HashSet<string> _wholeAliases;

	private ArtistIdentity(string whole, List<CharacterCredit> characters, List<string> aliases, IEnumerable<string> performers)
	{
		WholeArtist = whole;
		CharacterCredits = characters;
		SearchCredits = aliases.Where(value => value != whole).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		_wholeAliases = aliases.Append(whole).Select(MetadataNormalizer.ComparisonKey).Where(key => key.Length > 0).ToHashSet(StringComparer.Ordinal);
		PerformerTokens = performers.Select(MetadataNormalizer.ComparisonKey).Where(key => key.Length > 0).ToHashSet(StringComparer.Ordinal);
	}

	public static ArtistIdentity Parse(string? value)
	{
		string whole = MetadataNormalizer.NormalizeWhitespace(value);
		List<CharacterCredit> characters = new();
		List<string> aliases = new();
		Regex creditPattern = new(@"(?<character>[^,、;:()]+?)\s*\(\s*CV\s*[.:：]?\s*(?<actor>[^()]+)\)",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		MatchCollection credits = creditPattern.Matches(whole);
		foreach (Match credit in credits)
			characters.Add(new(credit.Groups["character"].Value.Trim(), credit.Groups["actor"].Value.Trim()));
		if (characters.Count > 0)
		{
			string prefix = whole[..credits[0].Index].Trim();
			Match groupCredit = Regex.Match(prefix, @"^(?<group>[^():,;、]+?)\s*[:(]\s*$");
			string remaining = creditPattern.Replace(whole[credits[0].Index..], "").Trim(' ', ',', '、', ';', '(', ')');
			// An incomplete CV list cannot erase other credited performers or prove a unit alias.
			if (remaining.Length > 0 || (prefix.Length > 0 && !groupCredit.Success))
				return new(whole, characters, aliases, new[] { whole });
			string performers = string.Join(", ", characters.Select(credit => credit.VoiceActor));
			aliases.Add(performers);
			// A group is an alias only when the same credit explicitly supplies its members.
			string group = groupCredit.Success ? groupCredit.Groups["group"].Value.Trim() : string.Empty;
			if (group.Length > 0) aliases.Add(group);
			return new(whole, characters, aliases, characters.Select(credit => credit.VoiceActor));
		}
		if (MetadataNormalizer.TryBilingualTitle(whole, out string primary, out string alternate))
		{
			aliases.Add(primary);
			aliases.Add(alternate);
		}
		string[] tokens = Regex.Split(whole,
			@"\s*(?:,|、|&|/|;|×|\bfeat(?:uring)?\.?\s|\bft\.?\s)\s*", RegexOptions.IgnoreCase);
		return new(whole, characters, aliases, tokens);
	}

	public bool StronglyMatches(ArtistIdentity other)
	{
		if (_wholeAliases.Overlaps(other._wholeAliases)) return true;
		// Full sets only: punctuation in band names cannot authorize a single member.
		return PerformerTokens.Count > 0 && PerformerTokens.SetEquals(other.PerformerTokens);
	}
}
