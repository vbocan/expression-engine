#if !NETCOREAPP3_0_OR_GREATER
#nullable enable
namespace System.Diagnostics.CodeAnalysis
{
    // Nullable-analysis attributes that netstandard2.0, .NET Framework and .NET Core 2.x do not ship with.
    // Delete this file if your project already defines them (for example through a polyfill package).
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        public bool ReturnValue { get; }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        public bool ReturnValue { get; }
    }
}
#endif
