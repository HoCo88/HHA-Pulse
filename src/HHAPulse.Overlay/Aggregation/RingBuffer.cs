namespace HHAPulse.Overlay.Aggregation;

public sealed class RingBuffer<T>
{
    private readonly T[] items;
    private int nextIndex;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        items = new T[capacity];
    }

    public int Capacity => items.Length;

    public int Count { get; private set; }

    public void Add(T item)
    {
        items[nextIndex] = item;
        nextIndex = (nextIndex + 1) % items.Length;
        if (Count < items.Length)
        {
            Count++;
        }
    }

    public T[] ToArray()
    {
        var result = new T[Count];
        if (Count == 0)
        {
            return result;
        }

        var start = Count == items.Length ? nextIndex : 0;
        for (var i = 0; i < Count; i++)
        {
            result[i] = items[(start + i) % items.Length];
        }

        return result;
    }
}
