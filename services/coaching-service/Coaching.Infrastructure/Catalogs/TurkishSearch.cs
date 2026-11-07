using System.Linq.Expressions;
using System.Reflection;

namespace Coaching.Infrastructure.Catalogs;

/// <summary>
/// KM7: Turkish case-insensitive search that does not depend on the database collation. Both sides are folded the same way:
/// the Turkish capitals (Ç Ğ İ I Ö Ş Ü) and ı are mapped explicitly, the rest goes through lower(). "türev" finds "Türev"
/// and "TÜREV"; "ışık" finds "IŞIK". EF Core translates the folding to nested replace()/lower() calls.
/// </summary>
public static class TurkishSearch
{
    private static readonly (string From, string To)[] Map =
        [("İ", "i"), ("I", "i"), ("ı", "i"), ("Ç", "ç"), ("Ğ", "ğ"), ("Ö", "ö"), ("Ş", "ş"), ("Ü", "ü"), ("i̇", "i")];
    /// <summary>G4 (#43): characters that separate words besides spaces ("Prof.Dr.", "(Kız)", "Mesleki-Teknik").</summary>
    private static readonly string[] Separators = ["(", ")", "-", "/", ".", ",", "'"];
    /// <summary>G4 (#43): a search uses at most this many words (each one is a condition in the query).</summary>
    public const int MaxWords = 8;
    private static readonly MethodInfo Replace = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo Lower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    /// <summary>The folded form of a search term (the same folding the database applies to the columns).</summary>
    public static string Fold(string value)
    {
        foreach (var (from, to) in Map) value = value.Replace(from, to, StringComparison.Ordinal);
        return value.ToLowerInvariant();
    }

    /// <summary>G4 (#43): the folded words of a text ("Karşıyaka  Anadolu" → "karşiyaka", "anadolu"), at most <see cref="MaxWords"/>.</summary>
    public static IReadOnlyList<string> Words(string? term) => Split(term).Take(MaxWords).ToArray();

    private static IEnumerable<string> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var folded = Fold(text);
        foreach (var separator in Separators) folded = folded.Replace(separator, " ", StringComparison.Ordinal);
        return folded.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
    }

    /// <summary>Rows where any of the given text columns contains the term, Turkish case-insensitively.</summary>
    public static IQueryable<T> WhereContains<T>(this IQueryable<T> query, string? term, params Expression<Func<T, string?>>[] columns)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var folded = Expression.Constant(Fold(term.Trim()));
        return query.Where(AnyColumn(columns, text => Expression.Call(text, Contains, folded)));
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

    /// <summary>One test per column on its folded (replace + lower) text, joined with OR; a null column never matches.</summary>
    private static Expression<Func<T, bool>> AnyColumn<T>(Expression<Func<T, string?>>[] columns, Func<Expression, Expression> test)
    {
        var row = Expression.Parameter(typeof(T), "row");
        Expression? any = null;
        foreach (var column in columns)
        {
            var value = new Rebind(column.Parameters[0], row).Visit(column.Body)!;
            Expression text = value;
            foreach (var (from, to) in Map) text = Expression.Call(text, Replace, Expression.Constant(from), Expression.Constant(to));
            var condition = Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, typeof(string))), test(Expression.Call(text, Lower)));
            any = any is null ? condition : Expression.OrElse(any, condition);
        }
        return Expression.Lambda<Func<T, bool>>(any!, row);
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
