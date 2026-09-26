namespace Directory.Church;

public readonly struct Optional<T>
{
    public Optional(T? value)
    {
        HasValue = true;
        Value = value;
    }

    public bool HasValue { get; }

    public T? Value { get; }

    public T? Or(T? current) => HasValue ? Value : current;
}
