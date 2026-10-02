using System.Globalization;

namespace ExpressionEngine.Tests;

public class ValueTests
{
    [Fact]
    public void NumbersAndBooleansConvertImplicitly()
    {
        Value number = 5;
        Value boolean = true;

        Assert.True(number.IsNumber);
        Assert.Equal(ValueKind.Number, number.Kind);
        Assert.Equal(5, number.AsNumber());
        Assert.True(boolean.IsBoolean);
        Assert.Equal(ValueKind.Boolean, boolean.Kind);
        Assert.True(boolean.AsBoolean());
    }

    [Fact]
    public void DefaultValueIsTheNumberZero()
    {
        Value value = default;

        Assert.True(value.IsNumber);
        Assert.Equal(0, value.AsNumber());
    }

    [Fact]
    public void WrongKindAccessThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Value.FromBoolean(true).AsNumber());
        Assert.Throws<InvalidOperationException>(() => Value.FromNumber(1).AsBoolean());
        Assert.Throws<InvalidOperationException>(() => (double)Value.FromBoolean(false));
        Assert.Throws<InvalidOperationException>(() => (bool)Value.FromNumber(0));
        Assert.Equal(2.5, (double)Value.FromNumber(2.5));
        Assert.False((bool)Value.FromBoolean(false));
    }

    [Fact]
    public void ValuesWithDifferentKindsAreNeverEqual()
    {
        Assert.NotEqual(Value.FromNumber(1), Value.FromBoolean(true));
        Assert.NotEqual(Value.FromNumber(0), Value.FromBoolean(false));
        Assert.Equal(Value.FromNumber(3), Value.FromNumber(3));
        Assert.True(Value.FromBoolean(true) == true);
        Assert.True(Value.FromNumber(3) != Value.FromNumber(4));
        Assert.Equal(Value.FromNumber(3).GetHashCode(), Value.FromNumber(3).GetHashCode());
        Assert.False(Value.FromNumber(3).Equals("3"));
    }

    [Fact]
    public void FormattingIsInvariantAndRoundTrips()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Assert.Equal("1.5", Value.FromNumber(1.5).ToString());
            Assert.Equal("0.30000000000000004", Value.FromNumber(0.1 + 0.2).ToString());
            Assert.Equal("true", Value.FromBoolean(true).ToString());
            Assert.Equal("false", Value.FromBoolean(false).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
