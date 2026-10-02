using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

public class SessionTests
{
    [Fact]
    public void FailedDefinitionLeavesTheSessionUntouched()
    {
        var session = new Session();
        session.DefineFunction("good", new[] { "x" }, "x + 1");

        Assert.Throws<InvalidOperationException>(() => session.DefineFunction("bad", new[] { "x" }, "x + outside"));
        Assert.Throws<ParseException>(() => session.DefineFunction("worse", new[] { "x" }, "x +"));

        Assert.Equal(2, session.Evaluate("good(1)").AsNumber());
        Assert.False(session.IsUserFunction("bad"));
        Assert.False(session.IsUserFunction("worse"));
    }

    [Fact]
    public void DefinitionsSurviveOptionChanges()
    {
        var session = new Session();
        session.DefineFunction("inc", new[] { "n" }, "n + 1");

        session.SetAllowNonFinite(true);
        session.SetIgnoreCase(true);
        session.SetAllowNonFinite(false);

        Assert.Equal(2, session.Evaluate("inc(1)").AsNumber());
        Assert.True(session.IsUserFunction("inc"));
    }

    [Fact]
    public void ParameterNamesFollowTheCaseSettingWhenDefining()
    {
        var session = new Session();
        Assert.Throws<InvalidOperationException>(() => session.DefineFunction("addOne", new[] { "N" }, "n + 1"));

        session.SetIgnoreCase(true);
        session.DefineFunction("addOne", new[] { "N" }, "n + 1");

        Assert.Equal(2, session.Evaluate("ADDONE(1)").AsNumber());
    }

    [Fact]
    public void CaseInsensitivityAppliesToParametersAfterTheSwitch()
    {
        var session = new Session();
        session.DefineFunction("inc", new[] { "n" }, "n + 1");
        session.SetIgnoreCase(true);

        Assert.Equal(6, session.Evaluate("INC(5)").AsNumber());
    }

    [Fact]
    public void ConstantsAreFixedAtDefinitionTime()
    {
        var session = new Session();
        session.Variables["base"] = 10;
        session.DefineConstant("limit", "base * 3");
        session.Variables["base"] = 1;

        Assert.Equal(30, session.Evaluate("limit").AsNumber());
        Assert.Contains("limit", session.UserConstantNames);
    }

    [Fact]
    public void RedefiningAConstantReplacesIt()
    {
        var session = new Session();
        session.DefineConstant("k", "1");
        session.DefineConstant("k", "2");

        Assert.Equal(2, session.Evaluate("k").AsNumber());
        Assert.Single(session.UserConstantNames);
    }

    [Fact]
    public void InvalidConstantNamesAreRejected()
    {
        var session = new Session();

        Assert.Throws<ArgumentException>(() => session.DefineConstant("true", "1"));
        Assert.Throws<ArgumentException>(() => session.DefineConstant("1x", "1"));
    }

    [Fact]
    public void VariablesFollowTheEngineWhenCaseSensitivityChanges()
    {
        var session = new Session();
        session.Variables["Price"] = 5;

        Assert.Throws<UnknownVariableException>(() => session.Evaluate("price"));
        session.SetIgnoreCase(true);
        Assert.Equal(5, session.Evaluate("price").AsNumber());
        session.SetIgnoreCase(false);
        Assert.Throws<UnknownVariableException>(() => session.Evaluate("price"));
        Assert.Equal(5, session.Evaluate("Price").AsNumber());
    }

    [Fact]
    public void BooleanResultsCanBeStoredAsVariables()
    {
        var session = new Session();
        session.Variables["flag"] = session.Evaluate("1 < 2");

        Assert.True(session.Evaluate("flag && true").AsBoolean());
    }
}
