# ExpressionEngine Manual

A small, safe library for evaluating math and logic expressions with input variables, custom functions and precise error messages. This manual is the complete reference. The same text is built into the playground: type `:manual` there to read it, `:search text` to find something.

## Introduction

ExpressionEngine turns text such as `price * (1 + taxRate) - discount` into a result, using values that **you** supply at run time. Use it when a formula, rule or condition has to come from configuration, a database or a user instead of being compiled into your program: pricing rules, validation rules, report columns, feature flags, spreadsheet-style fields, scoring models.

```csharp
using ExpressionEngine;

double total = Engine.Default.EvaluateNumber(
    "price * (1 + taxRate) - discount",
    new { price = 100, taxRate = 0.2, discount = 5 });   // 115
```

What you get:

- **Numbers and booleans.** Arithmetic, comparisons, `&&` `||` `!` and the `a ? b : c` conditional, with the precedence you expect (`-2^2` is `-4`, `^` is right-associative).
- **Input variables.** Bind from a `Variables` set, an anonymous object or class, a dictionary, or your own `IVariableSource`.
- **Functions and constants.** `sqrt`, `max`, `round`, `clamp`, `pi` and more are built in; adding your own takes one line.
- **Precise errors.** Every error carries a position, line and column; unknown variables carry the variable name.
- **Introspection.** Ask a parsed expression which variables it needs, walk its syntax tree with a visitor, or print it back in normalized form.
- **Safe on untrusted input.** No code execution, no reflection into methods, hard limits on length and nesting. Hostile input fails with an exception, never with a hang or a crash.
- **Fast when reused.** Parsed expressions are immutable and thread-safe; evaluating one takes about 100 ns.
- **No dependencies.** Plain C# source you can read in an afternoon.

## Getting the engine into your project

ExpressionEngine is distributed as **source code**, not as a package. You copy it into your own solution and it becomes part of your assembly: no NuGet feed, no extra DLL, nothing to version separately.

### Copy the files

Copy the folder `src/ExpressionEngine` (the `.cs` files and the `Syntax` subfolder) into your project, for example to `YourProject/Vendor/ExpressionEngine/`. Skip `bin`, `obj` and `ExpressionEngine.csproj`: those exist only to build and test the library on its own. Your project builds the files as part of itself.

Prefer to keep it as a separate assembly? Add `src/ExpressionEngine/ExpressionEngine.csproj` to your solution and reference it as a project, or include the repository as a git submodule and do either of the above.

### Requirements

- **C# 10 or later** (Visual Studio 2022, .NET 6 SDK or newer). The files use file-scoped namespaces. For old-style projects set `<LangVersion>latest</LangVersion>`.
- **A runtime that implements .NET Standard 2.0**: .NET Framework 4.7.2 and later, .NET Core 2.0 and later, .NET 5 and later. The library is built and tested on .NET 10 and also compiled for .NET Standard 2.0.
- **No packages.** Nothing outside the base class library is used.
- **Nullable annotations** are declared in each file (`#nullable enable`), so they work whatever your project setting is.

One file deserves a note: `Polyfills.cs` supplies two nullable-analysis attributes that older runtimes lack. On .NET Core 3.0 and later it compiles to nothing. If your project already defines `NotNullWhenAttribute` and `MaybeNullWhenAttribute`, delete `Polyfills.cs`.

### Namespaces and visibility

The code lives in two namespaces: `ExpressionEngine` (everything you normally use) and `ExpressionEngine.Syntax` (the syntax tree). All types you need are `public`. If you do not want to expose them from your own assembly, change the `public` modifiers to `internal` after copying.

### License

ExpressionEngine is MIT licensed. When you copy the files, keep the license notice (the `LICENSE` file, or its text in your own third-party notices).

## Quick start

### Evaluate in one line

```csharp
using ExpressionEngine;

double area = Engine.Default.EvaluateNumber("pi * r^2", new { r = 10 });      // 314.159...
bool adult  = Engine.Default.EvaluateBoolean("age >= 18", new { age = 21 });  // true
```

`EvaluateNumber` and `EvaluateBoolean` check the result type for you. `Evaluate` returns a `Value` that can be either kind:

```csharp
Value result = Engine.Default.Evaluate("1 < 2");
result.IsBoolean;      // true
result.AsBoolean();    // true
```

### Parse once, evaluate many times

Parsing is the expensive part. When the same expression runs repeatedly, parse it once and keep the `Expression`:

```csharp
Expression price = Engine.Default.Parse("base * (1 + taxRate) - discount");

var vars = new Variables { ["base"] = 100, ["taxRate"] = 0.2, ["discount"] = 5 };
price.EvaluateNumber(vars);     // 115

vars["discount"] = 20;
price.EvaluateNumber(vars);     // 100
```

### Find out which inputs an expression needs

```csharp
Engine.Default.Parse("a * b + max(c, 1) + pi").Variables;   // ["a", "b", "c"]
```

Constants such as `pi` are not inputs. This is handy for validating user-written formulas or generating an input form.

### Handle mistakes in user input

```csharp
if (!Engine.Default.TryParse("2 * (3 + $)", out var expression, out var error))
{
    Console.WriteLine(error.Message);   // Unexpected character '$' (at column 10)
    // error.Position == 9, error.Line == 1, error.Column == 10
}
```

## How it works

```
 "price * (1 + tax)"
         |
         v
   +-----------+      +--------+      +-------------+
   |   Lexer   | ---> | Parser | ---> | Syntax tree |   immutable, thread-safe
   +-----------+      +--------+      +------+------+   (the Expression you keep)
                           ^                  |
          functions, constants, options       |  + your variables
                  (the Engine)                v
                                       +-------------+
                                       | Interpreter | ---> Value (number or boolean)
                                       +-------------+
```

- An **Engine** owns the functions, constants and options (case sensitivity, limits). `Engine.Default` has the math library; build your own with `EngineBuilder`. Engines are immutable and thread-safe.
- **Engine.Parse** produces an **Expression**. Function calls are bound and checked (name and argument count) at parse time, so those mistakes surface before any data is involved.
- **Expression.Evaluate** walks the tree with your variables. Nothing is cached or mutated, so one Expression can be evaluated from many threads at once.

## The expression language

### Values and literals

| Kind | Examples |
| --- | --- |
| Number (double precision) | `42`, `3.14`, `.5`, `5.`, `1e3`, `1.5E-2` |
| Boolean | `true`, `false` |
| Variable or constant | `price`, `taxRate`, `_tmp1`, `pi` |
| Function call | `sqrt(16)`, `max(a, b, c)`, `random()` |

Names start with a letter or underscore and continue with letters, digits or underscores. Whitespace, including newlines, is ignored between tokens. There is no implicit multiplication (`2x` is an error, write `2 * x`), no assignment and no string type.

### Operators

From highest to lowest precedence:

| Precedence | Operators | Operands | Associativity |
| :---: | --- | --- | --- |
| 1 | `( )` grouping, `f(...)` call | | |
| 2 | `^` power | numbers | right (`2^3^2` is `2^9`) |
| 3 | `+x` `-x` `!x` unary | `+` `-` numbers, `!` boolean | |
| 4 | `*` `/` `%` | numbers | left |
| 5 | `+` `-` | numbers | left |
| 6 | `<` `<=` `>` `>=` | numbers, giving a boolean | left |
| 7 | `==` `!=` | two numbers or two booleans, giving a boolean | left |
| 8 | `&&` | booleans | left, short-circuit |
| 9 | `\|\|` | booleans | left, short-circuit |
| 10 | `condition ? a : b` | boolean condition | right |

### Details worth knowing

- `-2^2` is `-4` (power binds tighter than unary minus), while `2^-2` is `0.25` (the exponent may carry a sign).
- `%` keeps the sign of the dividend: `-10 % 3` is `-1`.
- There is **no implicit conversion** between numbers and booleans. `1 && true`, truthiness tests such as `if (x)` and `1 == true` are errors, not surprises.
- `&&`, `||` and `?:` evaluate only what they need: `false && (1/0 > 0)` is `false`, and `true ? 1 : 1/0` is `1`.
- `==` on numbers is exact floating-point equality. Compare with a tolerance (`abs(a - b) < 1e-9`) when that matters.
- Numbers are IEEE-754 doubles, so `0.1 + 0.2` is `0.30000000000000004`. Use `round(x, digits)` for display.

## Built-in constants and functions

### Constants

| Name | Value |
| --- | --- |
| `pi` | the ratio of a circle's circumference to its diameter, 3.14159... |
| `e` | Euler's number, 2.71828... |
| `tau` | 2 times pi |

A variable you supply with the same name takes precedence over a constant.

### Functions

| Category | Functions |
| --- | --- |
| Basics | `abs(x)` `sign(x)` `min(a, b, ...)` `max(a, b, ...)` `clamp(x, lo, hi)` |
| Rounding | `floor(x)` `ceil(x)` `trunc(x)` `round(x)` `round(x, digits)` (half away from zero, `digits` 0 to 15) |
| Powers and logs | `sqrt(x)` `cbrt(x)` `pow(x, y)` `exp(x)` `ln(x)` `log(x)` (base 10) `log(x, base)` `log10(x)` |
| Trigonometry | `sin` `cos` `tan` `asin` `acos` `atan` `atan2(y, x)` `sinh` `cosh` `tanh` (radians), `deg(x)` `rad(x)` |
| Aggregates | `sum(a, b, ...)` `avg(a, b, ...)` |

`min`, `max`, `sum` and `avg` take any number of arguments (at least one). Function arguments must be numbers.

## Providing variables

Variables are looked up **by the name used in the expression**, first in what you supply and then in the engine's constants. Pick whichever source suits your data.

### Variables: an explicit set

```csharp
var vars = new Variables { ["price"] = 100, ["taxRate"] = 0.2, ["isMember"] = true };
Engine.Default.EvaluateNumber("isMember ? price * 0.9 : price", vars);    // 90
```

Numbers and booleans convert to `Value` implicitly, so no wrapping is needed.

### Any object: anonymous types and your own classes

The public readable **properties** of the object are the variables:

```csharp
record Order(int Quantity, decimal UnitPrice, bool Express);

var cost = Engine.Default.Parse("Quantity * UnitPrice + (Express ? 15 : 0)");
cost.EvaluateNumber(new Order(3, 9.5m, true));     // 43.5
```

By default names are case-sensitive, so use the property names exactly (`Quantity`), or build the engine with `CaseInsensitive()` to write `quantity`. Supported property types are all the .NET numeric types (`int`, `long`, `decimal`, `double` and so on) and `bool`. A `null` or unsupported property (such as `string`) raises an `EvaluationException` naming the property, but only when the expression actually uses it.

### Dictionaries

`Dictionary<string, double>`, `Dictionary<string, int>`, `Dictionary<string, decimal>`, `Dictionary<string, object>` (holding numbers and booleans), their read-only interfaces, and `Hashtable` all work directly:

```csharp
Engine.Default.EvaluateNumber("a + b", new Dictionary<string, double> { ["a"] = 3, ["b"] = 4 });   // 7
```

Dictionaries use their own key comparer, so give them `StringComparer.OrdinalIgnoreCase` if you want case-insensitive keys.

### Your own source: IVariableSource

For anything else (a database row, a JSON document, a lazily computed value), implement one method:

```csharp
class RowSource : IVariableSource
{
    private readonly IDataRecord _row;
    public RowSource(IDataRecord row) => _row = row;

    public bool TryGetValue(string name, out Value value)
    {
        var ordinal = FindColumn(name);          // your lookup
        if (ordinal < 0) { value = default; return false; }   // reported as an unknown variable
        value = _row.GetDouble(ordinal);
        return true;
    }
}
```

Returning `false` makes the engine report an `UnknownVariableException` with the variable's position. If your source throws, the exception is wrapped in an `EvaluationException` that carries the position and the original exception as `InnerException`.

## Custom functions and constants

### Add your own

Build an Engine with the functions you need. Overloads cover 0 to 3 arguments, and a variadic form covers the rest:

```csharp
var engine = new EngineBuilder()
    .AddMathFunctions()                                     // sqrt, max, round, ...
    .AddMathConstants()                                     // pi, e, tau
    .AddFunction("vat", amount => amount * 0.19)
    .AddFunction("discount", (amount, percent) => amount * (1 - percent / 100))
    .AddFunction("lerp", (a, b, t) => a + (b - a) * t)
    .AddFunction("count", 0, int.MaxValue, args => args.Length)   // name, min, max arguments
    .AddConstant("shipping", 4.90)
    .Build();

engine.EvaluateNumber("vat(discount(price, 10)) + shipping", new { price = 200 });   // 39.1
```

Build the engine once and reuse it: engines are immutable and thread-safe.

Things to know:

- Argument **count is checked when the expression is parsed**, and unknown function names are reported then too (`Unknown function 'foo' (at column 5)`).
- Your function receives plain `double` values. Passing a boolean to a function is an `EvaluationException`.
- An exception thrown inside your function is wrapped in an `EvaluationException` that points at the call, with your exception as `InnerException`.
- Adding a function with the name of an existing one **replaces** it, so you can override built-ins.
- A new `EngineBuilder()` starts **empty**. Call `AddMathFunctions()` and `AddMathConstants()` if you want the standard library.
- Names must start with a letter or underscore, and `true` and `false` are reserved.

## Configuring the engine

| Builder method | Effect | Default |
| --- | --- | --- |
| `AddMathFunctions()` and `AddMathConstants()` | Standard math library | not added (but present in `Engine.Default`) |
| `AddFunction(...)` and `AddConstant(...)` | Your own functions and constants | none |
| `CaseInsensitive()` | `SQRT(X)` equals `sqrt(x)`; also `TRUE` and `False`; object properties and `engine.CreateVariables()` match case-insensitively | case-sensitive |
| `AllowNonFiniteResults()` | `1/0`, overflow and `sqrt(-1)` yield infinity or NaN instead of raising an error | errors |
| `WithMaxDepth(n)` | Maximum nesting depth | 256 |
| `WithMaxLength(n)` | Maximum expression length in characters | 10,000 |

```csharp
var engine = new EngineBuilder()
    .AddMathFunctions().AddMathConstants()
    .CaseInsensitive()
    .Build();

engine.EvaluateNumber("SQRT(Area)", new { area = 16 });   // 4
```

When you use `CaseInsensitive()` with a `Variables` set, create it with `engine.CreateVariables()` so the set follows the same rule.

### About non-finite results

By default an operation that would yield infinity or NaN is an error (`Division by zero`, `Result of 'sqrt' is not a number`, `Result is too large to represent`). Silent NaN values tend to spread through a calculation and surface far from their cause. Call `AllowNonFiniteResults()` if you prefer IEEE-754 behavior. In the playground, `:set nonfinite on` lets you compare both.

### About the depth limit

The depth limit bounds how deeply parentheses, prefix operators and long operator chains may nest. Because `a + b + c + ...` builds a tree as deep as the chain is long, an expression with more than 256 terms in one chain exceeds the default. Raise it with `WithMaxDepth` if you generate such expressions. The parser and evaluator also check the real remaining stack, so even an absurdly high limit ends in an exception rather than a crash.

## Error handling

Everything the engine reports derives from `ExpressionException`:

| Exception | Raised when | Extra information |
| --- | --- | --- |
| `ParseException` | The text is not valid: bad character, malformed number, unbalanced parenthesis, unknown function, wrong argument count, expression too long or too deep | |
| `EvaluationException` | A valid expression cannot be evaluated: type mismatch, division by zero, a function failed, a variable source threw | `InnerException` when user code threw |
| `UnknownVariableException` (an `EvaluationException`) | A variable is not supplied and is not a constant | `VariableName` |

Each carries `Position` (zero-based character index), `Length`, `Line`, `Column` and `ExpressionText`, and the message already ends with the location: `Unexpected character '$' (at column 5)`.

```csharp
try
{
    Engine.Default.Evaluate("1 + (2 * true)");
}
catch (ExpressionException ex)
{
    Console.WriteLine(ex.Message);   // Operator '*' needs Number operands but got Number and Boolean (at column 6)

    // Underline the offending part:
    Console.WriteLine(ex.ExpressionText);
    Console.WriteLine(new string(' ', ex.Position) + new string('^', Math.Max(1, ex.Length)));
}
```

Prefer not to use exceptions for control flow? `Engine.TryParse(text, out expression, out error)` and `Expression.TryEvaluate(variables, out value, out error)` report the same information without throwing.

### Common messages

| Message | Meaning |
| --- | --- |
| `Unexpected character '$'` | A character that is not part of the language |
| `Invalid number '1.2.3'` | A malformed number literal (also `2x`, `.`, `2e`) |
| `Unexpected end of expression` | The expression stops where more is needed |
| `Expected ')' to close the parenthesis opened at column N` | An unclosed parenthesis |
| `Unknown function 'foo'` | No function with that name (names are case-sensitive by default) |
| `Function 'sqrt' expects 1 argument but got 2` | Wrong number of arguments |
| `Unknown variable 'x'` | The expression uses a variable that was not supplied |
| `Operator '+' needs Number operands but got Boolean and Number` | A type mismatch |
| `Cannot compare a Number with a Boolean using '=='` | Comparing different kinds of value |
| `Division by zero` | The divisor of `/` or `%` is zero |

## The syntax tree

`Expression.Root` is an immutable tree of `Node` objects: `NumberNode`, `BooleanNode`, `VariableNode`, `UnaryNode`, `BinaryNode`, `ConditionalNode` and `CallNode`. Each node knows where it came from (`Start`, `Length`), and `INodeVisitor<T>` lets you analyze or translate it. For instance, to render an expression in prefix notation:

```csharp
using ExpressionEngine.Syntax;

class Prefix : INodeVisitor<string>
{
    public string Visit(NumberNode n) => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Visit(BooleanNode n) => n.Value ? "true" : "false";
    public string Visit(VariableNode n) => n.Name;
    public string Visit(UnaryNode n) => $"({UnaryName(n)} {n.Operand.Accept(this)})";
    public string Visit(BinaryNode n) => $"({n.Operator} {n.Left.Accept(this)} {n.Right.Accept(this)})";
    public string Visit(ConditionalNode n) => $"(if {n.Condition.Accept(this)} {n.WhenTrue.Accept(this)} {n.WhenFalse.Accept(this)})";
    public string Visit(CallNode n) => $"({n.Name} {string.Join(" ", n.Arguments.Select(a => a.Accept(this)))})";
    private static string UnaryName(UnaryNode n) => n.Operator.ToString().ToLowerInvariant();   // plus, negate, not
}

Engine.Default.Parse("1 + 2 * x").Root.Accept(new Prefix());   // (Add 1 (Multiply 2 x))
```

`Node.ToString()` prints the tree back as normalized text, with canonical spacing and only the parentheses that are needed:

```csharp
Engine.Default.Parse("((1+2)*3)  -  (4)").Root.ToString();   // "(1 + 2) * 3 - 4"
```

In the playground, `:ast expression` draws the tree, which is the quickest way to see how precedence was applied.

## Untrusted input

ExpressionEngine is designed so that expression text from users, files or the network can be evaluated safely:

| Risk | What the engine does |
| --- | --- |
| Code execution | Expressions are data interpreted by a tree walker. There is no eval, no compilation and no way to reach arbitrary methods. Only the functions **you register** can be called. |
| Reflection | Reflection is used only to read the **public properties** of the variables object you pass in (and only when you pass an object). Methods, fields and indexers are never invoked. |
| Stack exhaustion, a crash .NET cannot catch | Nesting depth is capped (default 256), and the parser and evaluator also check the real remaining stack. |
| Oversized input | Length is capped (default 10,000 characters). |
| Infinite loops | The lexer consumes input on every step; unrecognized characters are a `ParseException`. A randomized test throws tens of thousands of garbage strings at the engine to keep it that way. |
| Silent NaN or infinity | Reported as errors by default. |

What the engine cannot protect you from is what **you** plug into it: a registered function that is slow, blocks or has side effects will be called by any expression that uses it, so keep your functions pure and fast. Evaluation cost grows with expression size, which the length limit bounds; lower `WithMaxLength` for stricter budgets.

## Performance

Parsing builds a tree and is the expensive step; evaluation is a short walk over it. Measured with BenchmarkDotNet's short job on a 13th Gen Intel Core i9-13900K (.NET 10), for `price * (1 + taxRate) - discount + max(quantity, 1) * 0.5`:

| Operation | Time | Allocated |
| --- | ---: | ---: |
| Evaluate a parsed expression (Variables) | about 100 ns | 136 B |
| Evaluate a parsed expression (anonymous object) | about 140 ns | 392 B |
| Parse only | about 580 ns | 2.1 KB |
| Parse and evaluate on every call | about 670 ns | 2.3 KB |

These are indicative (short job, noisy machine). Run them yourself with `dotnet run -c Release --project benchmarks/ExpressionEngine.Benchmarks -- --filter "*"`.

Practical advice: parse each expression once and cache the `Expression`; prefer `Variables` or an `IVariableSource` in hot paths. `Expression` and `Engine` are safe to share across threads.

## The playground

The playground is the program you are running now. It is a REPL for trying the engine before you copy it into a project.

### Starting it

With Docker (nothing else to install):

```
docker build -t expression-engine .
docker run -it --rm expression-engine
```

The `-it` flags give the container a terminal. Without Docker, run `dotnet run --project src/ExpressionEngine.Playground`.

### Typing expressions

Type an expression and press Enter. Assign a variable with `name = expression`. The result of the last expression is available as `ans`. Errors are shown with a caret under the position. The line editor supports the arrow keys (history with up and down), Home, End, Ctrl+A, Ctrl+E, Ctrl+U, Ctrl+K, Ctrl+W, and **Tab completion** of commands, functions, constants and variable names.

### Commands

| Command | What it does |
| --- | --- |
| `:examples [category]` | List the built-in examples, optionally for one category |
| `:run n` or `:run category` or `:run all` | Run example number n, a whole category, or every example, showing the result and a note |
| `:edit n` | Load the variables of example n and put its expression on the prompt for you to change |
| `:manual [section]` | Read this manual. No argument shows the table of contents; a number, a title or part of a title shows a section; `all` shows everything |
| `:search text` | Find text in the manual |
| `:ast expression` | Show the syntax tree, the normalized form, and the variables and functions an expression uses |
| `:vars` | List variables |
| `:unset name` | Remove a variable |
| `:clear` | Remove all variables |
| `:funcs` | List functions and constants |
| `:def f(a, b) = body` | Define your own function, for example `:def hyp(a, b) = sqrt(a^2 + b^2)` |
| `:const name = expression` | Define a constant from the value of an expression |
| `:set` | Show the options |
| `:set ignorecase on` or `off` | Switch case-insensitive names |
| `:set nonfinite on` or `off` | Allow infinity and NaN instead of errors |
| `:reset` | Forget variables, definitions and options |
| `:help` | Show the command summary |
| `:quit` | Leave (Ctrl+D also works) |

### User-defined functions

`:def` shows how `EngineBuilder.AddFunction` works. The body may use the parameters, constants and functions defined earlier. It cannot call itself or refer to other variables. Redefining a name replaces it.

### Command-line use

The same program evaluates one expression and exits, which is handy in scripts and in Docker:

```
expr "2 * pi * r" -v r=10
expr "price * (1 + tax) > 100" -v price=90 -v tax=0.2
expr --manual            (print the manual)
expr --manual functions  (print one section)
expr --examples          (list the examples)
```

Options: `-v name=value` (repeatable; numbers or `true` and `false`), `-i` for case-insensitive names, `--` to end options (for expressions that start with `-`). The exit code is 0 on success, 1 for an expression error and 2 for bad usage. With Docker: `docker run --rm expression-engine "2 * pi * 10"`.

## Limitations and ideas

Not in the current version: a `decimal` mode for money, strings, dotted paths such as `order.total`, binding methods by attribute, constant folding, compiling to delegates. By design the engine stays a safe, small expression evaluator and not a scripting language: no statements, loops, assignment or user-defined functions inside expressions (the playground's `:def` is a host feature that registers a function, not syntax).
