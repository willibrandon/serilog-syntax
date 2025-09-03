namespace SerilogSyntax.Tests.Expressions;

/// <summary>
/// Shared test data for expression tests.
/// </summary>
internal static class ExpressionTestData
{
    // Filter expressions
    public const string SimpleFilter = "Level = 'Error' and StatusCode >= 400";
    public const string LikeFilter = "RequestPath like '/api%'";
    public const string NotLikeFilter = "Message not like '%debug%'";
    public const string InFilter = "User.Role in ['Admin', 'Moderator']";
    public const string NullCheckFilter = "Exception is not null";
    public const string CaseInsensitiveFilter = "Name = 'john' ci";
    public const string ComplexFilter = "RequestPath like '/api%' and StatusCode >= 400 and User.Role in ['Admin', 'Moderator']";
    
    // Expression templates
    public const string SimpleTemplate = "{@t:HH:mm:ss} {@l:u3} {@m}";
    public const string TemplateWithDirective = "[{@t:HH:mm:ss}] {#if Level = 'Error'}ERROR{#end} {@m}";
    public const string ComplexTemplate = "[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}({SourceContext}){#end} {@m}\n{@x}";
    public const string EachDirectiveTemplate = "{#each item in Items}{item.Name}: {item.Value}{#delimit}, {#end}";
    public const string NestedDirectiveTemplate = "{#if User is not null}{#if User.IsActive}Active: {User.Name}{#else}Inactive{#end}{#end}";
    
    // Property access patterns
    public const string SimpleProperty = "UserName";
    public const string NestedProperty = "Order.Customer.Address.City";
    public const string ArrayIndexing = "Items[0]";
    public const string DictionaryAccess = "@p['property-name']";
    public const string WildcardIndexing = "Items[?].Price";
    public const string AllIndexing = "Items[*]";
    
    // Functions
    public const string SimpleFunctionCall = "Length(Name)";
    public const string FunctionWithModifier = "StartsWith(Name, 'John') ci";
    public const string NestedFunctionCall = "Round(Length(Items) / 2, 0)";
    public const string FunctionWithMultipleArgs = "Replace(Message, 'error', 'warning')";
    
    // Built-in properties
    public const string BuiltinProperties = "@t @m @mt @l @x @p @i @r @tr @sp";
    public const string BuiltinWithFormat = "{@t:yyyy-MM-dd} {@l:u3}";
    
    // Literals
    public const string StringWithEscapedQuote = "'It''s working'";
    public const string HexNumber = "0xC0FFEE";
    public const string DecimalNumber = "-12.34";
    public const string BooleanLiterals = "true and false";
    public const string NullLiteral = "null";
    public const string ArrayLiteral = "[1, 'two', null]";
    public const string ObjectLiteral = "{a: 1, 'b c': 2, d}";
    
    // Spread operator
    public const string SpreadInArray = "[1, 2, ..others]";
    public const string SpreadInObject = "{a: 1, ..others}";
    
    // Mixed content
    public const string MixedTemplateAndExpression = "User {@p['user-id']} {#if Status = 'Active'}is active{#else}is inactive{#end}";
    
    // API calls
    public const string FilterByExcludingCall = "Filter.ByExcluding(\"RequestPath like '/health%'\")";
    public const string FilterByIncludingOnlyCall = ".Filter.ByIncludingOnly(\"Level = 'Error'\")";
    public const string ConditionalWriteCall = "WriteTo.Conditional(\"Environment = 'Production'\", writeTo => writeTo.File(\"prod.log\"))";
    public const string EnrichWhenCall = "Enrich.When(\"Level >= 'Warning'\", e => e.WithProperty(\"Alert\", true))";
    public const string EnrichWithComputedCall = "Enrich.WithComputed(\"ShortContext\", \"Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)\")";
    public const string ExpressionTemplateCall = "new ExpressionTemplate(\"{#if Status = 'Error'}[ERROR]{#end} {@m}\")";
}