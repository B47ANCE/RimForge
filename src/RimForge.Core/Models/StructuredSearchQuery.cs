using System.Text;

namespace RimForge.Core.Models;

public enum SearchLogicalOperator
{
    And,
    Or
}

public abstract record SearchExpression;
public sealed record SearchClauseExpression(SearchClause Clause) : SearchExpression;
public sealed record SearchBinaryExpression(SearchExpression Left, SearchLogicalOperator Operator, SearchExpression Right) : SearchExpression;
public sealed record SearchNotExpression(SearchExpression Operand) : SearchExpression;

public sealed record SearchClause(string Field, string Value, bool IsPlainText = false, string Operator = ":")
{
    public string DisplayText => IsPlainText ? Value : $"{Field}{Operator}{QuoteIfNeeded(Value)}";

    private static string QuoteIfNeeded(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}

public sealed class StructuredSearchQuery
{
    private static readonly Dictionary<string, string> FieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["identity"] = "identity",
        ["mod"] = "mod", ["name"] = "mod",
        ["package"] = "package", ["package-id"] = "package", ["id"] = "package",
        ["author"] = "author", ["creator"] = "author",
        ["workshop"] = "workshop", ["workshop-id"] = "workshop",
        ["source"] = "source", ["origin"] = "source",
        ["badge"] = "badge", ["evidence"] = "badge", ["type"] = "badge",
        ["requires"] = "requires", ["depends-on"] = "requires", ["needs"] = "requires",
        ["required-by"] = "required-by", ["dependents"] = "required-by", ["used-by"] = "required-by",
        ["incompatible"] = "incompatible", ["conflicts-with"] = "incompatible", ["conflict"] = "incompatible",
        ["supported-version"] = "supported-version", ["version"] = "supported-version", ["game-version"] = "supported-version",
        ["active"] = "active", ["enabled"] = "active",
        ["issue"] = "issue", ["issues"] = "issue", ["problem"] = "issue",
        ["favorite"] = "favorite", ["favourite"] = "favorite",
        ["profile"] = "profile"
    };

    private static readonly string[] CanonicalFields =
    [
        "mod", "package", "author", "workshop", "source", "badge", "requires", "required-by",
        "incompatible", "supported-version", "active", "issue", "favorite", "profile"
    ];

    public static StructuredSearchQuery Empty { get; } = new(Array.Empty<SearchClause>(), Array.Empty<string>(), null, false);

    public StructuredSearchQuery(IReadOnlyList<SearchClause> clauses, IReadOnlyList<string> errors)
        : this(clauses, errors, BuildImplicitExpression(clauses), false)
    {
    }

    private StructuredSearchQuery(IReadOnlyList<SearchClause> clauses, IReadOnlyList<string> errors, SearchExpression? expression, bool usesExplicitBooleanLogic)
    {
        Clauses = clauses;
        Errors = errors;
        Expression = expression;
        UsesExplicitBooleanLogic = usesExplicitBooleanLogic;
    }

    public IReadOnlyList<SearchClause> Clauses { get; }
    public IReadOnlyList<string> Errors { get; }
    public SearchExpression? Expression { get; }
    public bool UsesExplicitBooleanLogic { get; }
    public bool IsEmpty => Clauses.Count == 0;
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Chips => Clauses.Select(clause => clause.DisplayText).ToArray();

    public IEnumerable<SearchClause> For(string field) =>
        Clauses.Where(clause => clause.Field.Equals(field, StringComparison.OrdinalIgnoreCase));

    public bool Evaluate(Func<SearchClause, bool> evaluator)
    {
        if (!IsValid) return false;
        if (Expression is null) return true;
        return Evaluate(Expression, evaluator);
    }

    public static IReadOnlyList<string> GetSuggestions(string? text)
    {
        var tail = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        if (tail.Contains(':') || tail.Contains('>') || tail.Contains('<') || tail.StartsWith('-')) return Array.Empty<string>();
        return CanonicalFields
            .Where(field => field.StartsWith(tail, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .Select(field => field + ":")
            .ToArray();
    }

    public static StructuredSearchQuery Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Empty;

        var tokenization = Tokenize(text);
        var errors = new List<string>();
        if (tokenization.UnterminatedQuote) errors.Add("Unterminated quoted value.");

        var usesExplicit = tokenization.Tokens.Any(token => token.Kind is TokenKind.And or TokenKind.Or or TokenKind.Not or TokenKind.LeftParen or TokenKind.RightParen);
        if (!usesExplicit)
        {
            var clauses = new List<SearchClause>();
            foreach (var token in tokenization.Tokens.Where(token => token.Kind == TokenKind.Term))
            {
                var clause = ParseClause(token.Text, errors);
                if (clause is not null) clauses.Add(clause);
            }
            return new StructuredSearchQuery(clauses, errors, BuildImplicitExpression(clauses), false);
        }

        var parser = new ExpressionParser(tokenization.Tokens, errors);
        var expression = parser.Parse();
        return new StructuredSearchQuery(parser.Clauses, errors, expression, true);
    }

    private static SearchClause? ParseClause(string token, List<string> errors)
    {
        var negatedPrefix = token.StartsWith('-') && token.Length > 1;
        if (negatedPrefix) token = token[1..];

        var (field, op, value) = SplitClause(token);
        SearchClause clause;
        if (field is null)
        {
            clause = new SearchClause("identity", token, true);
        }
        else
        {
            if (!FieldAliases.TryGetValue(field, out var canonical))
            {
                errors.Add($"Unknown filter '{field}{op}'.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Filter '{field}{op}' needs a value.");
                return null;
            }
            clause = new SearchClause(canonical, value, false, op);
        }

        return negatedPrefix ? clause with { Operator = "!:" } : clause;
    }

    private static (string? Field, string Operator, string Value) SplitClause(string token)
    {
        foreach (var op in new[] { ">=", "<=", "!=", ":", ">", "<", "=" })
        {
            var index = token.IndexOf(op, StringComparison.Ordinal);
            if (index > 0) return (token[..index].Trim(), op, token[(index + op.Length)..].Trim());
        }
        return (null, string.Empty, token);
    }

    private static SearchExpression? BuildImplicitExpression(IReadOnlyList<SearchClause> clauses)
    {
        if (clauses.Count == 0) return null;

        var indexed = clauses.Select((clause, index) => new { clause, index });
        var groups = indexed
            .GroupBy(item => item.clause.IsPlainText ? $"identity#{item.index}" : item.clause.Field, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Select(item => WrapNegation(item.clause))
                .Aggregate((left, right) => new SearchBinaryExpression(left, SearchLogicalOperator.Or, right)))
            .ToArray();

        return groups.Aggregate((left, right) => new SearchBinaryExpression(left, SearchLogicalOperator.And, right));
    }

    private static SearchExpression WrapNegation(SearchClause clause) =>
        clause.Operator == "!:"
            ? new SearchNotExpression(new SearchClauseExpression(clause with { Operator = ":" }))
            : new SearchClauseExpression(clause);

    private static bool Evaluate(SearchExpression expression, Func<SearchClause, bool> evaluator) => expression switch
    {
        SearchClauseExpression clause => evaluator(clause.Clause),
        SearchNotExpression not => !Evaluate(not.Operand, evaluator),
        SearchBinaryExpression binary when binary.Operator == SearchLogicalOperator.And => Evaluate(binary.Left, evaluator) && Evaluate(binary.Right, evaluator),
        SearchBinaryExpression binary => Evaluate(binary.Left, evaluator) || Evaluate(binary.Right, evaluator),
        _ => false
    };

    private enum TokenKind { Term, And, Or, Not, LeftParen, RightParen }
    private sealed record Token(TokenKind Kind, string Text);

    private static (IReadOnlyList<Token> Tokens, bool UnterminatedQuote) Tokenize(string text)
    {
        var tokens = new List<Token>();
        var current = new StringBuilder();
        var quoted = false;

        void Flush()
        {
            if (current.Length == 0) return;
            var value = current.ToString();
            current.Clear();
            var kind = value.Equals("AND", StringComparison.OrdinalIgnoreCase) ? TokenKind.And
                : value.Equals("OR", StringComparison.OrdinalIgnoreCase) ? TokenKind.Or
                : value.Equals("NOT", StringComparison.OrdinalIgnoreCase) ? TokenKind.Not
                : TokenKind.Term;
            tokens.Add(new Token(kind, value));
        }

        foreach (var character in text)
        {
            if (character == '"') { quoted = !quoted; continue; }
            if (!quoted && character is '(' or ')')
            {
                Flush();
                tokens.Add(new Token(character == '(' ? TokenKind.LeftParen : TokenKind.RightParen, character.ToString()));
                continue;
            }
            if (char.IsWhiteSpace(character) && !quoted) { Flush(); continue; }
            current.Append(character);
        }
        Flush();
        return (tokens, quoted);
    }

    private sealed class ExpressionParser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private readonly List<string> _errors;
        private int _position;

        public ExpressionParser(IReadOnlyList<Token> tokens, List<string> errors)
        {
            _tokens = tokens;
            _errors = errors;
        }

        public List<SearchClause> Clauses { get; } = [];

        public SearchExpression? Parse()
        {
            var expression = ParseOr();
            if (_position < _tokens.Count) _errors.Add($"Unexpected token '{_tokens[_position].Text}'.");
            return expression;
        }

        private SearchExpression? ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenKind.Or))
            {
                var right = ParseAnd();
                if (left is null || right is null) { _errors.Add("OR requires expressions on both sides."); return left ?? right; }
                left = new SearchBinaryExpression(left, SearchLogicalOperator.Or, right);
            }
            return left;
        }

        private SearchExpression? ParseAnd()
        {
            var left = ParseUnary();
            while (_position < _tokens.Count && _tokens[_position].Kind is not TokenKind.Or and not TokenKind.RightParen)
            {
                Match(TokenKind.And); // explicit AND or implicit adjacency
                var right = ParseUnary();
                if (left is null || right is null) { _errors.Add("AND requires expressions on both sides."); return left ?? right; }
                left = new SearchBinaryExpression(left, SearchLogicalOperator.And, right);
            }
            return left;
        }

        private SearchExpression? ParseUnary()
        {
            if (Match(TokenKind.Not))
            {
                var operand = ParseUnary();
                return operand is null ? null : new SearchNotExpression(operand);
            }
            if (Match(TokenKind.LeftParen))
            {
                var expression = ParseOr();
                if (!Match(TokenKind.RightParen)) _errors.Add("Missing closing parenthesis.");
                return expression;
            }
            if (_position >= _tokens.Count || _tokens[_position].Kind != TokenKind.Term) return null;

            var raw = _tokens[_position++].Text;
            var clause = ParseClause(raw, _errors);
            if (clause is null) return null;
            Clauses.Add(clause);
            return WrapNegation(clause);
        }

        private bool Match(TokenKind kind)
        {
            if (_position >= _tokens.Count || _tokens[_position].Kind != kind) return false;
            _position++;
            return true;
        }
    }
}
