using System.Collections.ObjectModel;

namespace AetherVPN.Services;

public class LogService
{
    private const int MaxLogLines = 1000;
    private readonly object _lock = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public string LogText
    {
        get
        {
            lock (_lock)
            {
                return string.Join(Environment.NewLine, LogLines);
            }
        }
    }

    public void AddLine(string line)
    {
        lock (_lock)
        {
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            while (LogLines.Count > MaxLogLines)
            {
                LogLines.RemoveAt(0);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            LogLines.Clear();
        }
    }

    public void SaveToFile(string filePath)
    {
        lock (_lock)
        {
            File.WriteAllLines(filePath, LogLines);
        }
    }
}
