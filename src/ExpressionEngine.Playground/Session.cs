namespace ExpressionEngine.Playground;

/// <summary>
/// The state of a playground session: engine options, user-defined functions and constants, and variables.
/// <see cref="Engine"/> is immutable, so changing an option or a definition builds a fresh engine.
/// </summary>
internal sealed class Session
{
    private sealed record FunctionSpec(string Name, IReadOnlyList<string> Parameters, string Body);

    private readonly List<FunctionSpec> _functions = new();
    private readonly List<KeyValuePair<string, double>> _constants = new();
    private readonly HashSet<string> _userFunctionNames = new(StringComparer.Ordinal);

    public Session(bool ignoreCase = false)
    {
        IgnoreCase = ignoreCase;
        (Engine, Variables) = Build(ignoreCase, allowNonFinite: false, _functions, _constants, null);
    }

    public bool IgnoreCase { get; private set; }

    public bool AllowNonFinite { get; private set; }

    public Engine Engine { get; private set; }

    public Variables Variables { get; private set; }

    /// <summary>Whether <paramref name="name"/> is a function defined with <c>:def</c> (as opposed to a built-in).</summary>
    public bool IsUserFunction(string name) => _userFunctionNames.Contains(name);

    public IReadOnlyList<string> UserConstantNames => _constants.Select(c => c.Key).ToArray();

    public Value Evaluate(string text) => Engine.Evaluate(text, Variables);

    public void SetIgnoreCase(bool value) => Apply(value, AllowNonFinite, _functions, _constants);

    public void SetAllowNonFinite(bool value) => Apply(IgnoreCase, value, _functions, _constants);

    /// <summary>Defines (or redefines) a function such as <c>hyp(a, b) = sqrt(a^2 + b^2)</c>.</summary>
    public void DefineFunction(string name, IReadOnlyList<string> parameters, string body)
    {
        var functions = _functions.Where(f => f.Name != name).ToList();
        functions.Add(new FunctionSpec(name, parameters, body));
        Apply(IgnoreCase, AllowNonFinite, functions, _constants);
    }

    /// <summary>Defines (or redefines) a named constant whose value is the result of <paramref name="expression"/> right now.</summary>
    public void DefineConstant(string name, string expression)
    {
        var value = Evaluate(expression);
        if (!value.IsNumber)
        {
            throw new InvalidOperationException("A constant must be a number, but the expression gives a Boolean.");
        }

        var constants = _constants.Where(c => c.Key != name).ToList();
        constants.Add(new KeyValuePair<string, double>(name, value.AsNumber()));
        Apply(IgnoreCase, AllowNonFinite, _functions, constants);
    }

    public void Reset()
    {
        _functions.Clear();
        _constants.Clear();
        _userFunctionNames.Clear();
        IgnoreCase = false;
        AllowNonFinite = false;
        (Engine, Variables) = Build(false, false, _functions, _constants, null);
    }

    private void Apply(
        bool ignoreCase,
        bool allowNonFinite,
        IReadOnlyList<FunctionSpec> functions,
        IReadOnlyList<KeyValuePair<string, double>> constants)
    {
        // Build everything first: if a definition no longer parses under the new options, the session stays untouched.
        var (engine, variables) = Build(ignoreCase, allowNonFinite, functions, constants, Variables);

        IgnoreCase = ignoreCase;
        AllowNonFinite = allowNonFinite;
        Engine = engine;
        Variables = variables;

        if (!ReferenceEquals(functions, _functions))
        {
            _functions.Clear();
            _functions.AddRange(functions);
        }

        if (!ReferenceEquals(constants, _constants))
        {
            _constants.Clear();
            _constants.AddRange(constants);
        }

        _userFunctionNames.Clear();
        foreach (var function in _functions)
        {
            _userFunctionNames.Add(function.Name);
        }
    }

    private static (Engine Engine, Variables Variables) Build(
        bool ignoreCase,
        bool allowNonFinite,
        IReadOnlyList<FunctionSpec> functions,
        IReadOnlyList<KeyValuePair<string, double>> constants,
        Variables? previous)
    {
        EngineBuilder NewBuilder()
        {
            var builder = new EngineBuilder()
                .AddMathFunctions()
                .AddMathConstants()
                .CaseInsensitive(ignoreCase)
                .AllowNonFiniteResults(allowNonFinite);
            foreach (var constant in constants)
            {
                builder.AddConstant(constant.Key, constant.Value);
            }

            return builder;
        }

        // Each definition is parsed by an engine that knows only the functions defined before it,
        // which is why a user function cannot call itself or one defined later.
        var defined = new List<FunctionDefinition>();
        foreach (var spec in functions)
        {
            var scope = NewBuilder();
            foreach (var earlier in defined)
            {
                scope.AddFunction(earlier);
            }

            defined.Add(Compile(spec, scope.Build(), ignoreCase));
        }

        var finalBuilder = NewBuilder();
        foreach (var definition in defined)
        {
            finalBuilder.AddFunction(definition);
        }

        var engine = finalBuilder.Build();
        var variables = engine.CreateVariables();
        if (previous is not null)
        {
            foreach (var pair in previous)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        return (engine, variables);
    }

    private static FunctionDefinition Compile(FunctionSpec spec, Engine scope, bool ignoreCase)
    {
        var body = scope.Parse(spec.Body);
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var parameters = spec.Parameters.ToArray();

        var stranger = body.Variables.FirstOrDefault(v => !parameters.Contains(v, comparer));
        if (stranger is not null)
        {
            throw new InvalidOperationException(
                $"The body of '{spec.Name}' uses '{stranger}', which is not one of its parameters. "
                + "Functions can only use their own parameters, constants and functions defined earlier.");
        }

        return new FunctionDefinition(spec.Name, parameters.Length, parameters.Length, args =>
        {
            var locals = new Variables(ignoreCase);
            for (var i = 0; i < parameters.Length; i++)
            {
                locals[parameters[i]] = args[i];
            }

            return body.EvaluateNumber(locals);
        });
    }
}
