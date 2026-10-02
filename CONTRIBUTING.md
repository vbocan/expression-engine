# Contributing

Thanks for your interest in ExpressionEngine! Bug reports, ideas and pull requests are welcome.

## Getting set up

You need the [.NET SDK](https://dotnet.microsoft.com/download) version pinned in `global.json` (10.0 or newer).

```bash
git clone https://github.com/vbocan/expression-engine.git
cd expression-engine
dotnet build
dotnet test
```

## Making a change

1. **Open an issue first** for anything beyond a small fix, so we can agree on the approach. The project aims to stay a small, safe expression evaluator, not a scripting language.
2. **Write the test first.** A bug fix should come with a test that fails before and passes after. New behavior needs tests for the happy path *and* for the errors.
3. **Keep the build clean.** Warnings are errors, public members need XML documentation, and the library must keep compiling for both `netstandard2.0` and `net10.0` (avoid APIs missing from `netstandard2.0`, such as `Math.Clamp`).
4. **Keep the library copyable.** It is distributed as source: no package dependencies, `#nullable enable` at the top of every file, explicit `using` directives, and nothing that needs project-level settings.
5. **Update the docs.** The manual is `docs/MANUAL.md`; it is embedded in the playground. If you change behavior or a code sample there, update the matching test in `tests/ExpressionEngine.Tests/ManualExamples.cs`. Add a line to `CHANGELOG.md`.
6. **Examples are tested.** Every playground example (`ExampleCatalog.cs`) is run by a test against the default engine. Add new ones there with their expected result.

## Working on the playground

```bash
dotnet run --project src/ExpressionEngine.Playground          # the REPL
docker build -t expression-engine . && docker run -it --rm expression-engine
```

The line editor, REPL commands, manual viewer and example catalog all have tests that run without a terminal (`tests/ExpressionEngine.Tests/Playground`). Please try interactive changes in a real terminal too, ideally through Docker, since the key handling cannot be fully covered by tests.

## Design principles

- **Untrusted input must be safe.** The lexer must always make progress, recursion must be bounded, and bad input must produce an `ExpressionException`, never a hang, a crash or a framework exception. `SafetyTests` includes a randomized test that guards this.
- **Errors point somewhere.** Every error carries a position.
- **No hidden magic.** No implicit number/boolean conversion, no silent `NaN`.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
