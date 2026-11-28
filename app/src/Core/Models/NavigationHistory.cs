namespace PCManager.Core.Models;

public sealed class NavigationHistory<T>
    where T : class
{
    private readonly Stack<T> _back = new();
    private readonly Stack<T> _forward = new();

    public bool CanGoBack => _back.Count > 0;
    public bool CanGoForward => _forward.Count > 0;

    public void Push(T item)
    {
        _back.Push(item);
        _forward.Clear();
    }

    public bool TryBack(T current, out T? previous)
    {
        if (_back.Count == 0)
        {
            previous = null;
            return false;
        }

        previous = _back.Pop();
        _forward.Push(current);
        return true;
    }

    public bool TryForward(T current, out T? next)
    {
        if (_forward.Count == 0)
        {
            next = null;
            return false;
        }

        next = _forward.Pop();
        _back.Push(current);
        return true;
    }

    public void Clear()
    {
        _back.Clear();
        _forward.Clear();
    }
}
