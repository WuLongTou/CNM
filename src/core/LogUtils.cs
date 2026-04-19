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
        GD.Print(string.Format(format, args));
    }

    public static void PrintErr(object message)
    {
        GD.PrintErr(message);
    }

    public static void PrintErr(string format, params object[] args)
    {
        GD.PrintErr(string.Format(format, args));
    }
}
