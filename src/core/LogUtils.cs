namespace ZombieWars.Core;

using Godot;

/// <summary>
/// Logging utility — wraps Godot's GD.Print so all C# code uses a consistent
/// logging interface instead of scattering GD.Print calls.
/// </summary>
public static class LogUtils
{
    public static void Print(object message)
    {
        GD.Print(message);
    }

    public static void Print(string format, params object[] args)
    {
        // 无参时透传：插值字符串里的字面 {}（如 JSON 日志）会被 string.Format 当占位符而抛 FormatException
        GD.Print(args is { Length: > 0 } ? string.Format(format, args) : format);
    }

    public static void PrintErr(object message)
    {
        GD.PrintErr(message);
    }

    public static void PrintErr(string format, params object[] args)
    {
        // 同 Print：无参时透传，避免消息中的字面 {} 触发 FormatException
        GD.PrintErr(args is { Length: > 0 } ? string.Format(format, args) : format);
    }
}
