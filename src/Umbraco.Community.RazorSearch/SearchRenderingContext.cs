namespace Umbraco.Community.RazorSearch;

public static class SearchRenderingContext
{
    private static readonly AsyncLocal<bool> State = new();

    public static bool IsActive => State.Value;

    internal static IDisposable Activate()
    {
        bool original = State.Value;
        State.Value = true;
        return new Reset(() => State.Value = original);
    }

    private sealed class Reset(Action reset) : IDisposable
    {
        public void Dispose() => reset();
    }
}
