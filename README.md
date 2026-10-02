<p align="center">
  <img src="assets/logo.svg" alt="ExpressionEngine logo: the syntax tree of x + 1" width="128" height="128">
</p>

<h1 align="center">ExpressionEngine</h1>

<p align="center">
  <b>Evaluate math &amp; logic expressions with input variables, custom functions and precise errors.</b><br>
  A small, safe C# library you copy into your project. Try it first in a Docker playground.
</p>

<p align="center">
  <a href="https://github.com/vbocan/expression-engine/actions/workflows/ci.yml"><img src="https://github.com/vbocan/expression-engine/actions/workflows/ci.yml/badge.svg?branch=master" alt="Build status"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/vbocan/expression-engine?color=blue" alt="MIT license"></a>
  <a href="#try-it-in-docker"><img src="https://img.shields.io/badge/docker-try%20it-2496ED?logo=docker&amp;logoColor=white" alt="Docker playground"></a>
  <img src="https://img.shields.io/badge/.NET-netstandard2.0%20%7C%2010.0-512BD4?logo=dotnet" alt="Targets netstandard2.0 and net10.0">
  <img src="https://img.shields.io/badge/dependencies-0-brightgreen" alt="Zero dependencies">
  <img src="https://img.shields.io/badge/distribution-source%20code-orange" alt="Distributed as source code">
  <a href="https://github.com/vbocan/expression-engine/stargazers"><img src="https://img.shields.io/github/stars/vbocan/expression-engine?style=flat&amp;logo=github" alt="GitHub stars"></a>
  <a href="https://github.com/vbocan/expression-engine/commits/master"><img src="https://img.shields.io/github/last-commit/vbocan/expression-engine?logo=git&amp;logoColor=white" alt="Last commit"></a>
</p>

<p align="center">
  <a href="#try-it-in-docker">Try it</a> ·
  <a href="#use-it-in-your-project">Use it</a> ·
  <a href="docs/MANUAL.md">Manual</a> ·
  <a href="#repository-layout">Layout</a> ·
  <a href="CONTRIBUTING.md">Contributing</a>
</p>

---

ExpressionEngine turns text such as `price * (1 + taxRate) - discount` into a result, using values **you** supply at run time. Use it when a formula, rule or condition has to come from configuration, a database or a user instead of being compiled into your program.

```csharp
using ExpressionEngine;

double total = Engine.Default.EvaluateNumber(
    "price * (1 + taxRate) - discount",
    new { price = 100, taxRate = 0.2, discount = 5 });   // 115
```

It handles numbers and booleans, comparisons, `&&` `||` `!`, the `a ? b : c` conditional, 30 built-in math functions, your own functions, and reports every mistake with a position. Untrusted input is safe: hostile text fails with an exception, never with a hang or a crash.

## Try it in Docker

Before you add anything to your own project, play with the engine. The image starts a REPL that shows **86 runnable examples** and has **the entire manual** built in.

```bash
docker build -t expression-engine .
docker run -it --rm expression-engine
```

The `-it` flags give the container a terminal. You land in a prompt under a numbered list of examples, grouped by topic (arithmetic, math functions, booleans, conditionals, variables, real-world formulas, business rules, and what each kind of error looks like). Type an expression, or use the commands:

| Command | What it does |
| --- | --- |
| `:run 12`, `:run Booleans`, `:run all` | Run examples and read what each one demonstrates |
| `:edit 56` | Load an example's variables and change its expression |
| `:manual`, `:manual functions`, `:search tolerance` | Read the whole manual in the terminal, one section at a time |
| `:ast 1 + 2 * x` | See how an expression is understood, as a tree |
| `:def hyp(a, b) = sqrt(a^2 + b^2)` | Define your own function and call it |
| `:set ignorecase on`, `:set nonfinite on` | Switch engine options and see what changes |
| `:help` | All commands and keys (history, Tab completion, line editing) |

A taste of a session:

```text
> r = 10
r = 10
> 2 * pi * r
62.83185307179586
> :def hyp(a, b) = sqrt(a^2 + b^2)
Defined hyp(a, b). Try it: hyp(1, 2)
> hyp(3, 4)
5
> 5 + $
error: Unexpected character '$' (at column 5)
  5 + $
      ^
> :ast 1 + 2 * r
normalized:  1 + 2 * r
variables:   r
functions:   (none)

Binary Add  (+)
├─ Number 1
└─ Binary Multiply  (*)
   ├─ Number 2
   └─ Variable r
```

The same image also works without the interactive prompt:

```bash
docker run --rm expression-engine "2 * pi * 10"      # evaluate one expression
docker run --rm expression-engine --manual            # print the manual
docker run --rm expression-engine --manual functions  # print one section
docker run --rm expression-engine --examples          # list the examples
```

No Docker? Run the playground with the .NET 10 SDK: `dotnet run --project src/ExpressionEngine.Playground`.

## Use it in your project

ExpressionEngine is **distributed as source code**, not as a package. Copy the folder [`src/ExpressionEngine`](src/ExpressionEngine) (the `.cs` files and the `Syntax` subfolder) into your project and it becomes part of your assembly. There is no feed to configure and no extra DLL to deploy.

- **Requirements:** C# 10 or later, and a runtime that implements .NET Standard 2.0 (.NET Framework 4.7.2+, .NET Core 2.0+, .NET 5+). It is built and tested on .NET 10 and also compiled for .NET Standard 2.0. No packages are needed.
- **Skip** `bin`, `obj` and `ExpressionEngine.csproj`: those only build and test the library on its own. Prefer a separate assembly? Reference the `.csproj` as a project, or add this repository as a git submodule.
- **Keep the license.** MIT: retain the notice in [LICENSE](LICENSE) with the copied files.

Then use it:

```csharp
using ExpressionEngine;

// Parse once, evaluate many times (parsing is the expensive part)
Expression price = Engine.Default.Parse("base * (1 + taxRate) - discount");
var vars = new Variables { ["base"] = 100, ["taxRate"] = 0.2, ["discount"] = 5 };
price.EvaluateNumber(vars);                      // 115

// Which inputs does an expression need?
price.Variables;                                 // ["base", "taxRate", "discount"]

// Your own functions and constants
var engine = new EngineBuilder()
    .AddMathFunctions().AddMathConstants()
    .AddFunction("vat", amount => amount * 0.19)
    .Build();
engine.EvaluateNumber("vat(price)", new { price = 200 });   // 38

// Mistakes carry a position instead of just a message
if (!engine.TryParse("2 * (3 + $)", out _, out var error))
    Console.WriteLine(error.Message);            // Unexpected character '$' (at column 10)
```

The [manual](docs/MANUAL.md) covers everything: the full language and operator precedence, every built-in function, the four ways to supply variables, custom functions, engine options, error handling, the syntax tree and visitor, safety guarantees and performance.

## Highlights

- **Numbers and booleans** with the precedence you expect (`-2^2` is `-4`; `^` is right-associative) and no surprising implicit conversions.
- **Four ways to bind variables:** a `Variables` set, any object's properties (including anonymous types), dictionaries, or your own `IVariableSource`.
- **Errors you can show to users:** position, line and column on every error; unknown variables carry their name; `TryParse` and `TryEvaluate` avoid exceptions entirely.
- **Introspection:** list the variables an expression uses, walk its immutable syntax tree with a visitor, print it back in normalized form.
- **Safe on untrusted input:** no code execution, no reflection into methods, limits on length and nesting that also guard the real stack. A randomized test feeds it tens of thousands of garbage strings.
- **Fast when reused:** a parsed `Expression` is immutable, thread-safe and evaluates in roughly 100 ns (see the manual for measurements).

## Repository layout

| Path | Contents |
| --- | --- |
| [`src/ExpressionEngine`](src/ExpressionEngine) | **The library: this is what you copy** |
| [`src/ExpressionEngine.Playground`](src/ExpressionEngine.Playground) | The REPL, examples and manual viewer (what the Docker image runs) |
| [`docs/MANUAL.md`](docs/MANUAL.md) | The complete manual (also built into the playground) |
| [`tests`](tests) | xUnit tests: engine, errors, safety, randomized tests, and the playground. Every playground example and every manual code sample is executed by a test |
| [`benchmarks`](benchmarks) | BenchmarkDotNet suite |
| [`Dockerfile`](Dockerfile) | Builds the playground image |

## Build and test from source

```bash
git clone https://github.com/vbocan/expression-engine.git
cd expression-engine
dotnet build
dotnet test
```

See [CONTRIBUTING.md](CONTRIBUTING.md) to contribute and [CHANGELOG.md](CHANGELOG.md) for what changed.

## License

[MIT](LICENSE) © Valer Bocan
