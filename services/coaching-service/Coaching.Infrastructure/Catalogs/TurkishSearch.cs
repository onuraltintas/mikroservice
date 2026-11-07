using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Coaching.Infrastructure.Catalogs;

/// <summary>
/// KM7: Turkish case-insensitive search that does not depend on the database collation. PR #128 review M1: it also ignores the
/// Turkish marks, like the client's fold(): "karsiyaka" finds "Karşıyaka" and "KARŞIYAKA", "isiklar" finds "IŞIKLAR", "kazim"
/// finds "Kâzım"; "türev" finds "TÜREV" and "turev". The term is folded here (<see cref="Fold"/>) and turned into a regular
/// expression in which every letter accepts its Turkish forms in both cases ("[sşSŞ]"); the database applies it to the column
/// as it is stored (PostgreSQL "~", no lower() or replace() chain, so the folding stays one call deep).
/// </summary>
public static class TurkishSearch
{
    /// <summary>G4 (#43): characters that separate words besides spaces ("Prof.Dr.", "(Kız)", "Mesleki-Teknik").</summary>
    private static readonly string[] Separators = ["(", ")", "-", "/", ".", ",", "'"];
    /// <summary>G4 (#43): a search uses at most this many words (each one is a condition in the query).</summary>
    public const int MaxWords = 8;
    /// <summary>The forms a folded letter stands for (the dotted/dotless i, the Turkish marks and the circumflex).</summary>
    private static readonly Dictionary<char, string> Forms = new()
    {
        ['a'] = "aâAÂ", ['c'] = "cçCÇ", ['g'] = "gğGĞ", ['i'] = "iıîIİÎ", ['o'] = "oöOÖ", ['s'] = "sşSŞ", ['u'] = "uüûUÜÛ"
    };
    private static readonly MethodInfo IsMatch = typeof(Regex).GetMethod(nameof(Regex.IsMatch), [typeof(string), typeof(string)])!;

    /// <summary>
    /// The folded form of a text: Turkish lower case without marks ("KARŞIYAKA" → "karsiyaka", "Kâzım" → "kazim",
    /// "Afyonkarahi̇sar" → "afyonkarahisar"), the same as the client's fold().
    /// </summary>
    public static string Fold(string value)
    {
        var lower = value.Replace("İ", "i", StringComparison.Ordinal).Replace("I", "ı", StringComparison.Ordinal)
            .ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var text = new StringBuilder(lower.Length);
        foreach (var c in lower)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) text.Append(c == 'ı' ? 'i' : c);
        return text.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// The pattern that finds a folded term in a stored text: each letter accepts its Turkish forms in both cases (an i also
    /// with a combining dot after it), anything else stands for itself. Valid for .NET and PostgreSQL regular expressions.
    /// </summary>
    public static string Pattern(string folded)
    {
        var pattern = new StringBuilder();
        foreach (var c in folded)
        {
            if (Forms.TryGetValue(c, out var forms)) pattern.Append('[').Append(forms).Append(']').Append(c == 'i' ? "\u0307?" : "");
            else if (char.IsLetter(c) && char.ToUpperInvariant(c) != c) pattern.Append('[').Append(c).Append(char.ToUpperInvariant(c)).Append(']');
            else if (char.IsLetterOrDigit(c) || c == ' ' || c > 127) pattern.Append(c);
            else pattern.Append('\\').Append(c);
        }
        return pattern.ToString();
    }

    /// <summary>G4 (#43): the folded words of a text ("Karşıyaka  Anadolu" → "karsiyaka", "anadolu"), at most <see cref="MaxWords"/>.</summary>
    public static IReadOnlyList<string> Words(string? term) => Split(term).Take(MaxWords).ToArray();

    private static IEnumerable<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var folded = Fold(text);
        foreach (var separator in Separators) folded = folded.Replace(separator, " ", StringComparison.Ordinal);
        return folded.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
    }

    /// <summary>Rows where any of the given text columns contains the term, Turkish case- and mark-insensitively.</summary>
    public static IQueryable<T> WhereContains<T>(this IQueryable<T> query, string? term, params Expression<Func<T, string?>>[] columns)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var pattern = Expression.Constant(Pattern(Fold(term.Trim())));
        return query.Where(AnyColumn(columns, text => Expression.Call(IsMatch, text, pattern)));
    }

    /// <summary>
    /// G4 (#43): rows where every word of the term is in one of the columns, Turkish case-insensitively. Words may be in any
    /// order and in different columns: "karşıyaka anadolu" finds "Karşıyaka Cihat Kora Anadolu Lisesi" (and "Cihat Kora Anadolu
    /// Lisesi" in the district KARŞIYAKA). Each word is one more condition of the same shape as <see cref="WhereContains{T}"/>.
    /// </summary>
    public static IQueryable<T> WhereEveryWord<T>(this IQueryable<T> query, string? term, params Expression<Func<T, string?>>[] columns)
    {
        foreach (var word in Words(term)) query = query.WhereContains(word, columns);
        return query;
    }

    /// <summary>
    /// G4 (#43): every word of the term starts a word of one of the texts ("lise 7" → "Anadolu Lise 7", not "Anadolu Lise 17");
    /// used in memory to list the closest matches first.
    /// </summary>
    public static bool EveryWordStarts(string? term, params string?[] texts)
    {
        var words = texts.SelectMany(Split).ToArray();
        return Words(term).All(word => words.Any(x => x.StartsWith(word, StringComparison.Ordinal)));
    }

    /// <summary>One test per column on its stored text, joined with OR; a null column never matches.</summary>
    private static Expression<Func<T, bool>> AnyColumn<T>(Expression<Func<T, string?>>[] columns, Func<Expression, Expression> test)
    {
        var row = Expression.Parameter(typeof(T), "row");
        Expression? any = null;
        foreach (var column in columns)
        {
            var value = new Rebind(column.Parameters[0], row).Visit(column.Body)!;
            var condition = Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, typeof(string))), test(value));
            any = any is null ? condition : Expression.OrElse(any, condition);
        }
        return Expression.Lambda<Func<T, bool>>(any!, row);
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
