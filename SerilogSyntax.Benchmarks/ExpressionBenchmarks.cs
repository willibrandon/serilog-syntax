using System.Linq;
using BenchmarkDotNet.Attributes;
using SerilogSyntax.Expressions;

namespace SerilogSyntax.Benchmarks;

[MemoryDiagnoser]
public class ExpressionBenchmarks
{
    private const string SimpleFilter = "Level = 'Error'";
    private const string ComplexFilter = "RequestPath like '/api%' and StatusCode >= 400 and User.Role in ['Admin', 'Moderator']";
    private const string ComplexTemplate = "[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}({SourceContext}){#end} {@m}\n{@x}";
    private const string NestedProperty = "Order.Customer.Address.City";
    private const string FunctionExpression = "StartsWith(User.Name, 'John') ci and Length(User.Email) > 5";
    
    private const string FilterCallLine = "Filter.ByExcluding(\"RequestPath like '/health%'\")";
    private const string TemplateCallLine = "new ExpressionTemplate(\"{#if Status = 'Error'}[ERROR]{#end} {@m}\")";
    
    [Benchmark]
    public void DetectContext_Simple()
    {
        _ = ExpressionDetector.GetContext(FilterCallLine, 20);
    }
    
    [Benchmark]
    public void DetectContext_WithCache()
    {
        // This should hit the cache after first call
        for (int i = 0; i < 100; i++)
        {
            _ = ExpressionDetector.GetContext(FilterCallLine, 20);
        }
    }
    
    [Benchmark]
    public void IsExpressionTemplate_Simple()
    {
        _ = ExpressionDetector.IsExpressionTemplate("{UserName} logged in");
    }
    
    [Benchmark]
    public void IsExpressionTemplate_Complex()
    {
        _ = ExpressionDetector.IsExpressionTemplate(ComplexTemplate);
    }
    
    [Benchmark]
    public void IsFilterExpression_Simple()
    {
        _ = ExpressionDetector.IsFilterExpression(SimpleFilter);
    }
    
    [Benchmark]
    public void IsFilterExpression_Complex()
    {
        _ = ExpressionDetector.IsFilterExpression(ComplexFilter);
    }
    
    [Benchmark]
    public void TokenizeExpression_Simple()
    {
        var tokenizer = new ExpressionTokenizer(SimpleFilter);
        _ = tokenizer.Tokenize().ToList();
    }
    
    [Benchmark]
    public void TokenizeExpression_Complex()
    {
        var tokenizer = new ExpressionTokenizer(ComplexFilter);
        _ = tokenizer.Tokenize().ToList();
    }
    
    [Benchmark]
    public void TokenizeExpression_NestedProperty()
    {
        var tokenizer = new ExpressionTokenizer(NestedProperty);
        _ = tokenizer.Tokenize().ToList();
    }
    
    [Benchmark]
    public void TokenizeExpression_WithFunctions()
    {
        var tokenizer = new ExpressionTokenizer(FunctionExpression);
        _ = tokenizer.Tokenize().ToList();
    }
    
    [Benchmark]
    public void ParseExpression_Simple()
    {
        var parser = new ExpressionParser(SimpleFilter);
        _ = parser.Parse().ToList();
    }
    
    [Benchmark]
    public void ParseExpression_Complex()
    {
        var parser = new ExpressionParser(ComplexFilter);
        _ = parser.Parse().ToList();
    }
    
    [Benchmark]
    public void ParseExpressionTemplate_Simple()
    {
        var parser = new ExpressionParser("{@t} {@m}");
        _ = parser.ParseExpressionTemplate().ToList();
    }
    
    [Benchmark]
    public void ParseExpressionTemplate_WithDirectives()
    {
        var parser = new ExpressionParser(ComplexTemplate);
        _ = parser.ParseExpressionTemplate().ToList();
    }
    
    [Benchmark]
    public void FullPipeline_FilterExpression()
    {
        // Detect context
        _ = ExpressionDetector.GetContext(FilterCallLine, 20);
        
        // Tokenize
        var tokenizer = new ExpressionTokenizer(ComplexFilter);
        _ = tokenizer.Tokenize().ToList();
        
        // Parse
        var parser = new ExpressionParser(ComplexFilter);
        _ = parser.Parse().ToList();
    }
    
    [Benchmark]
    public void FullPipeline_ExpressionTemplate()
    {
        // Detect context
        _ = ExpressionDetector.GetContext(TemplateCallLine, 30);
        
        // Parse template
        var parser = new ExpressionParser(ComplexTemplate);
        _ = parser.ParseExpressionTemplate().ToList();
    }
    
    [Benchmark]
    public void CacheHitRate_Detection()
    {
        // Clear cache first
        ExpressionDetector.ClearCache();
        
        // First calls will miss cache
        for (int i = 0; i < 10; i++)
        {
            _ = ExpressionDetector.GetContext(FilterCallLine, 20);
            _ = ExpressionDetector.GetContext(TemplateCallLine, 30);
        }
        
        // These should all hit cache
        for (int i = 0; i < 90; i++)
        {
            _ = ExpressionDetector.GetContext(FilterCallLine, 20);
            _ = ExpressionDetector.GetContext(TemplateCallLine, 30);
        }
    }
    
    [Benchmark]
    public void TokenizeExpression_LongString()
    {
        var longExpression = string.Join(" and ", Enumerable.Range(1, 50).Select(i => $"Property{i} = 'Value{i}'"));
        var tokenizer = new ExpressionTokenizer(longExpression);
        var tokens = tokenizer.Tokenize().ToList();
    }
    
    [Benchmark]
    public void ParseExpression_ManyProperties()
    {
        var expression = string.Join(" or ", Enumerable.Range(1, 20).Select(i => $"User.Prop{i}.SubProp{i}"));
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
    }
}