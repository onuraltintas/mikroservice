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
    private static readonly MethodInfo Replace = typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo Lower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    /// <summary>The folded form of a search term (the same folding the database applies to the columns).</summary>
    public static string Fold(string value)
    {
        foreach (var (from, to) in Map) value = value.Replace(from, to, StringComparison.Ordinal);
        return value.ToLowerInvariant();
    }

    /// <summary>Rows where any of the given text columns contains the term, Turkish case-insensitively.</summary>
    public static IQueryable<T> WhereContains<T>(this IQueryable<T> query, string? term, params Expression<Func<T, string?>>[] columns)
    {
        if (string.IsNullOrWhiteSpace(term)) return query;
        var folded = Expression.Constant(Fold(term.Trim()));
        var row = Expression.Parameter(typeof(T), "row");
        Expression? any = null;
        foreach (var column in columns)
        {
            var value = new Rebind(column.Parameters[0], row).Visit(column.Body)!;
            Expression text = value;
            foreach (var (from, to) in Map) text = Expression.Call(text, Replace, Expression.Constant(from), Expression.Constant(to));
            var test = Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, typeof(string))),
                Expression.Call(Expression.Call(text, Lower), Contains, folded));
            any = any is null ? test : Expression.OrElse(any, test);
        }
        return query.Where(Expression.Lambda<Func<T, bool>>(any!, row));
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
