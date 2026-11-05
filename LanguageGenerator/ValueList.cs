using System.Collections;
using System.Runtime.CompilerServices;

namespace LanguageGenerator;

/// <summary>
/// A read-only list that's equal to another with the same items, so that a record holding one keeps
/// its value equality without an Equals written by hand.
/// </summary>
//its default is the empty list, so a record can take one as an optional parameter, and collection
//expressions build one
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Create))]
public readonly struct ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[]? items;

    public ValueList(IEnumerable<T> items) => this.items = [.. items];

    private T[] Items => items ?? [];

    public int Count => Items.Length;
    public T this[int index] => Items[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(ValueList<T> other) => Items.SequenceEqual(other.Items);
    public override bool Equals(object? obj) => obj is ValueList<T> other && Equals(other);
    public override int GetHashCode() => Items.Aggregate(0, HashCodeOf);
    private static int HashCodeOf(int hash, T item) => HashCode.Combine(hash, item);

    public static bool operator ==(ValueList<T> left, ValueList<T> right) => left.Equals(right);
    public static bool operator !=(ValueList<T> left, ValueList<T> right) => !left.Equals(right);
}

public static class ValueList
{
    public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => new(items.ToArray());
}
