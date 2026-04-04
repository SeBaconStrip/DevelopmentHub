namespace ExamplePlugin.Backend;

public class CounterService
{
    private int _value;

    public int Value => _value;

    public int Add(int step) => Interlocked.Add(ref _value, step);
}
