# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-10-02

First release under the new name and home. This is a ground-up rewrite of the original prototype.
ExpressionEngine is distributed as source code: copy `src/ExpressionEngine` into your project.

### Added
- `Engine`, `EngineBuilder` and `Expression`: parse once, evaluate many times; immutable and thread-safe.
- Boolean values and operators: comparisons, `&&`, `||`, `!`, and the `?:` conditional.
- `^` (power, right-associative) and `%` (remainder) operators; scientific notation (`1e3`); `true`/`false` literals.
- Standard math library (30 functions and the constants `pi`, `e`, `tau`), custom functions of any arity, custom constants.
- Variables from `Variables`, anonymous objects and classes, dictionaries, or a custom `IVariableSource`.
- `Expression.Variables` and `Expression.Functions` to discover what an expression needs.
- Public, immutable syntax tree with source spans, `INodeVisitor<T>`, and normalized printing via `Node.ToString()`.
- `ParseException`, `EvaluationException` and `UnknownVariableException` with position, line and column; `TryParse` and `TryEvaluate`.
- Safety limits (`WithMaxDepth`, `WithMaxLength`), checked at parse time and backed by real stack checks.
- Case-insensitive mode and an opt-in mode that allows `NaN`/infinity results.
- The playground (`src/ExpressionEngine.Playground`): a REPL with a line editor (history, Tab completion), 86 runnable
  examples, the full manual built in, `:ast`, `:def`, `:const` and `:set` commands, and one-shot and `--manual`/`--examples` modes.
- `Dockerfile` that builds and runs the playground; CI builds it and smoke-tests it.
- `docs/MANUAL.md`, the complete manual. Every code sample in it and every playground example is verified by a test.
- Targets `netstandard2.0` and `net10.0`; every library file declares `#nullable enable` so it can be copied into any project.

### Fixed (compared with the original prototype)
- An unrecognized character such as `$` made the tokenizer loop forever (`5 + $`).
- Deeply nested input (about 2,000 parentheses) crashed the process with an uncatchable stack overflow.
- Malformed numbers (`1.2.3`, `.`, `a.b`, Unicode digits) surfaced as raw `FormatException`s.
- Evaluating a variable or function without a context threw `NullReferenceException`.
- Reflection binding failed on `int` properties and overloaded methods, was case-sensitive, and exposed every public method of `object`.
- Unknown names raised `InvalidDataException`, an I/O exception type.
- Division by zero silently returned infinity or NaN.
- Error messages had no position information and contained typos.

### Changed
- Namespace layout and public API are new. The original `IContext`, `ReflectionContext`, `Parser`, `Tokenizer`, `Token` and `SyntaxException` types no longer exist.
- Target frameworks moved from the out-of-support `net7.0` to `netstandard2.0` and `net10.0`.
