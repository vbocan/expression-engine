namespace ExpressionEngine.Playground;

/// <summary>One runnable example: an expression, the variables it needs, and what it should produce.</summary>
internal sealed class Example
{
    public Example(
        int number,
        string category,
        string title,
        string expression,
        IReadOnlyList<(string Name, Value Value)> variables,
        string? result,
        string? errorContains,
        string note)
    {
        Number = number;
        Category = category;
        Title = title;
        Expression = expression;
        Variables = variables;
        Result = result;
        ErrorContains = errorContains;
        Note = note;
    }

    public int Number { get; }

    public string Category { get; }

    public string Title { get; }

    public string Expression { get; }

    public IReadOnlyList<(string Name, Value Value)> Variables { get; }

    /// <summary>The expected result, as printed, when the example succeeds.</summary>
    public string? Result { get; }

    /// <summary>Text the error message must contain when the example is meant to fail.</summary>
    public string? ErrorContains { get; }

    public string Note { get; }

    public bool ExpectsError => ErrorContains is not null;
}

internal readonly record struct ExampleOutcome(Value? Value, ExpressionException? Error)
{
    public bool Succeeded => Error is null;

    public string Describe() => Error is null ? Value!.Value.ToString() : Error.Message;

    /// <summary>Whether the outcome is what the example documents (true for the default engine).</summary>
    public bool Matches(Example example) => example.ExpectsError
        ? Error is not null && Error.Message.Contains(example.ErrorContains!, StringComparison.Ordinal)
        : Error is null && Value!.Value.ToString() == example.Result;
}

internal static class ExampleRunner
{
    /// <summary>Evaluates an example with its own variables, on top of <paramref name="engine"/>'s options.</summary>
    public static ExampleOutcome Run(Engine engine, Example example)
    {
        var variables = engine.CreateVariables();
        foreach (var (name, value) in example.Variables)
        {
            variables[name] = value;
        }

        try
        {
            return new ExampleOutcome(engine.Evaluate(example.Expression, variables), null);
        }
        catch (ExpressionException ex)
        {
            return new ExampleOutcome(null, ex);
        }
    }
}

/// <summary>
/// The examples shown by the playground. Every entry is executed by the test suite against the default engine,
/// so the documented results cannot drift from the real behavior.
/// </summary>
internal static class ExampleCatalog
{
    public static IReadOnlyList<Example> All { get; } = Build();

    public static IReadOnlyList<string> Categories { get; } = All.Select(e => e.Category).Distinct().ToArray();

    public static IEnumerable<Example> InCategory(string category) =>
        All.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<Example> Build()
    {
        var list = new List<Example>();
        var category = string.Empty;

        void Section(string name) => category = name;

        void Ok(string title, string expression, string result, string note, params (string, Value)[] vars) =>
            list.Add(new Example(list.Count + 1, category, title, expression, vars, result, null, note));

        void Fails(string title, string expression, string errorContains, string note, params (string, Value)[] vars) =>
            list.Add(new Example(list.Count + 1, category, title, expression, vars, null, errorContains, note));

        Section("Arithmetic");
        Ok("Precedence", "1 + 2 * 3", "7", "Multiplication binds tighter than addition.");
        Ok("Parentheses", "(1 + 2) * 3", "9", "Parentheses override precedence.");
        Ok("Division", "10 / 4", "2.5", "Division always produces a fraction; there is no integer division.");
        Ok("Remainder", "10 % 4", "2", "% is the remainder.");
        Ok("Remainder of a negative", "-10 % 3", "-1", "The remainder keeps the sign of the dividend (like C#).");
        Ok("Power", "2 ^ 10", "1024", "^ raises to a power.");
        Ok("Power is right-associative", "2 ^ 3 ^ 2", "512", "Read as 2 ^ (3 ^ 2) = 2 ^ 9, not (2 ^ 3) ^ 2.");
        Ok("Power beats unary minus", "-2 ^ 2", "-4", "Read as -(2 ^ 2), the usual mathematical convention. Use (-2) ^ 2 for 4.");
        Ok("Negative exponent", "2 ^ -1", "0.5", "The exponent may carry a sign.");
        Ok("Subtracting a negative", "10 - -3", "13", "Unary minus can follow a binary operator.");
        Ok("Scientific notation", "1e3 + 1.5e-2", "1000.015", "Literals may use an exponent: 1e3 is 1000.");
        Ok("Floating point", "0.1 + 0.2", "0.30000000000000004", "Numbers are IEEE doubles, so some decimals are inexact. Round for display.");
        Ok("Rounding fixes it", "round(0.1 + 0.2, 2)", "0.3", "round(x, digits) rounds half away from zero.");

        Section("Math functions");
        Ok("Square root", "sqrt(16)", "4", "One argument.");
        Ok("Pythagoras", "sqrt(3 ^ 2 + 4 ^ 2)", "5", "Functions take full expressions as arguments.");
        Ok("Absolute value", "abs(-7.5)", "7.5", "");
        Ok("Largest of many", "max(3, 9, 4)", "9", "min, max, sum and avg take any number of arguments (at least one).");
        Ok("Smallest of many", "min(3, 9, 4)", "3", "");
        Ok("Sum", "sum(1, 2, 3, 4)", "10", "");
        Ok("Average", "avg(2, 4, 9)", "5", "");
        Ok("Clamp", "clamp(15, 0, 10)", "10", "clamp(value, lowest, highest) keeps a value inside a range.");
        Ok("Sign", "sign(-42)", "-1", "-1, 0 or 1.");
        Ok("Rounding down", "floor(-1.5)", "-2", "floor goes toward negative infinity.");
        Ok("Rounding up", "ceil(1.2)", "2", "");
        Ok("Cutting off the fraction", "trunc(-1.9)", "-1", "trunc goes toward zero.");
        Ok("Round half away from zero", "round(2.5)", "3", "Not banker's rounding: 2.5 gives 3 and -2.5 gives -3.");
        Ok("Round to digits", "round(1234.5678, 2)", "1234.57", "The second argument is the number of decimals (0 to 15).");
        Ok("Constants", "2 * pi * 10", "62.83185307179586", "pi, e and tau are built in. tau is 2 * pi.");
        Ok("Trigonometry", "round(sin(pi / 6), 6)", "0.5", "Angles are in radians; deg() and rad() convert.");
        Ok("Radians to degrees", "round(deg(atan2(1, 1)), 6)", "45", "atan2(y, x) is the angle of the point (x, y).");
        Ok("Logarithms", "round(log(8, 2), 6)", "3", "log(x) is base 10, log(x, base) any base, ln(x) is natural.");
        Ok("Exponential", "round(exp(1), 6)", "2.718282", "");
        Ok("Cube root", "round(cbrt(-27), 6)", "-3", "Unlike sqrt, cbrt accepts negative numbers.");

        Section("Booleans and comparisons");
        Ok("Greater than", "3 > 2", "true", "Comparisons produce a boolean, not a number.");
        Ok("Greater or equal", "2 >= 3", "false", "");
        Ok("Equality", "1 + 1 == 2", "true", "== and != compare two numbers, or two booleans.");
        Ok("Inequality", "1 != 1", "false", "");
        Ok("And", "5 > 3 && 2 > 1", "true", "&& and || work on booleans only.");
        Ok("Or", "false || 2 + 2 == 4", "true", "");
        Ok("Not", "!(1 < 2)", "false", "! negates a boolean.");
        Ok("Precedence of && and ||", "true || false && false", "true", "&& binds tighter than ||: read as true || (false && false).");
        Ok("Comparing exactly", "0.1 + 0.2 == 0.3", "false", "Number equality is exact. Compare with a tolerance instead: abs(a - b) < 1e-9.");
        Ok("Comparing with a tolerance", "abs(0.1 + 0.2 - 0.3) < 1e-9", "true", "");

        Section("Conditional (?:)");
        Ok("Pick a value", "3 > 2 ? 10 : 20", "10", "condition ? valueIfTrue : valueIfFalse");
        Ok("Chained", "x > 0 ? 1 : x < 0 ? -1 : 0", "-1", "Chains read right to left; this is a sign function.", ("x", -7));
        Ok("Short-circuit &&", "false && (1 / 0 > 0)", "false", "The right-hand side is not evaluated, so no division by zero.");
        Ok("Short-circuit ?:", "true ? 1 : 1 / 0", "1", "Only the chosen branch is evaluated.");
        Ok("Boolean result", "age >= 18 ? true : false", "true", "A conditional can produce booleans too (though 'age >= 18' alone is simpler).", ("age", 30));

        Section("Variables");
        Ok("Sales tax", "price * (1 + taxRate)", "120", "Variables are supplied when you evaluate. Here price = 100 and taxRate = 0.2.", ("price", 100), ("taxRate", 0.2));
        Ok("Circumference", "2 * pi * r", "62.83185307179586", "Constants and variables mix freely.", ("r", 10));
        Ok("Circle area", "pi * r ^ 2", "28.274333882308138", "", ("r", 3));
        Ok("Boolean variable", "isMember ? price * 0.9 : price", "72", "Variables can be numbers or booleans.", ("price", 80), ("isMember", true));
        Ok("Many variables", "(a + b + c) / 3", "4", "", ("a", 2), ("b", 4), ("c", 6));
        Ok("Variable beats constant", "e * 2", "10", "A variable with the same name as a constant wins: here e = 5, not 2.718...", ("e", 5));

        Section("Real-world formulas");
        Ok("Celsius to Fahrenheit", "c * 9 / 5 + 32", "212", "", ("c", 100));
        Ok("Body mass index", "round(weight / height ^ 2, 1)", "22.9", "", ("weight", 70), ("height", 1.75));
        Ok("Compound interest", "round(principal * (1 + rate) ^ years, 2)", "1628.89", "1000 at 5% for 10 years.", ("principal", 1000), ("rate", 0.05), ("years", 10));
        Ok("Loan payment", "round(amount * r / (1 - (1 + r) ^ -months), 2)", "443.21", "Monthly payment on a 10,000 loan, 6% a year, 24 months (r is the monthly rate).", ("amount", 10000), ("r", 0.005), ("months", 24));
        Ok("Percent change", "round((newValue - oldValue) / oldValue * 100, 1)", "25", "", ("oldValue", 80), ("newValue", 100));
        Ok("Distance between points", "sqrt((x2 - x1) ^ 2 + (y2 - y1) ^ 2)", "5", "", ("x1", 1), ("y1", 2), ("x2", 4), ("y2", 6));
        Ok("Quadratic equation", "(-b + sqrt(b ^ 2 - 4 * a * c)) / (2 * a)", "2", "One root of x^2 - 3x + 2.", ("a", 1), ("b", -3), ("c", 2));
        Ok("Volume discount", "qty >= 100 ? 0.15 : qty >= 50 ? 0.10 : qty >= 10 ? 0.05 : 0", "0.1", "A tier table as chained conditionals.", ("qty", 60));
        Ok("Weighted average", "round(score1 * 0.3 + score2 * 0.7, 2)", "8.12", "30% weight on the first score, 70% on the second.", ("score1", 7), ("score2", 8.6));
        Ok("Seconds to hours", "round(seconds / 3600, 2)", "1.5", "", ("seconds", 5400));

        Section("Business rules");
        Ok("Loan eligibility", "age >= 18 && (score >= 600 || hasCoSigner)", "true", "Rules are plain boolean expressions you can load from configuration.", ("age", 25), ("score", 580), ("hasCoSigner", true));
        Ok("Free shipping", "total >= 50 || isMember", "false", "", ("total", 30), ("isMember", false));
        Ok("Within budget", "cost <= budget * 1.1", "true", "Allow a 10% overrun.", ("cost", 105), ("budget", 100));
        Ok("Valid range", "value >= 0 && value <= 100", "false", "", ("value", 120));
        Ok("Late fee", "daysLate > 0 ? clamp(daysLate * 2, 5, 50) : 0", "50", "A fee of 2 per day, between 5 and 50.", ("daysLate", 40));
        Ok("Even or odd", "n % 2 == 0", "true", "", ("n", 18));

        Section("Understanding errors");
        Fails("Division by zero", "1 / 0", "Division by zero", "By default, errors beat silent infinity. Try ':set nonfinite on' and run it again.");
        Fails("Square root of a negative", "sqrt(-1)", "not a number", "Try ':set nonfinite on' to get NaN instead.");
        Fails("Unexpected end", "2 +", "Unexpected end of expression", "Every error says where it happened ('at column N'); the playground draws a caret under it.");
        Fails("Unexpected character", "2 $ 3", "Unexpected character '$'", "");
        Fails("Unclosed parenthesis", "(1 + 2", "Expected ')'", "The message points back at the parenthesis that was opened.");
        Fails("Malformed number", "1.2.3", "Invalid number '1.2.3'", "");
        Fails("Missing multiplication", "2x", "Invalid number '2x'", "There is no implicit multiplication; write 2 * x.");
        Fails("Single equals sign", "1 = 1", "Did you mean '=='?", "In the playground, 'x = 5' assigns a variable, but inside an expression '=' is not an operator.");
        Fails("Unknown variable", "x + 1", "Unknown variable 'x'", "Set one with 'x = 5', or run ':edit' on an example that defines variables.");
        Fails("Unknown function", "foo(1)", "Unknown function 'foo'", "Function names are checked when the expression is parsed.");
        Fails("Case matters", "SQRT(16)", "Unknown function 'SQRT'", "Names are case-sensitive by default. Try ':set ignorecase on'.");
        Fails("Wrong argument count", "sqrt(1, 2)", "expects 1 argument but got 2", "Argument counts are checked at parse time too.");
        Fails("Too few arguments", "max()", "expects at least 1 argument", "");
        Fails("Number plus boolean", "1 + true", "needs Number operands", "There are no implicit conversions between numbers and booleans.");
        Fails("Comparing different types", "1 == true", "Cannot compare a Number with a Boolean", "");
        Fails("Condition must be boolean", "5 ? 1 : 2", "must be a Boolean", "Write 5 != 0 ? 1 : 2 if that is what you mean.");

        return list;
    }
}
