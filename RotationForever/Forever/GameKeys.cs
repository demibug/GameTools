#nullable enable
using System.Runtime.InteropServices;

namespace RotationForever;

internal static class GameKeys
{
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        internal uint Type;
        internal InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] internal KeyInput Key;
        // The native INPUT union is sized and aligned for MOUSEINPUT.
        [FieldOffset(0)] internal MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyInput
    {
        internal ushort VirtualKey, ScanCode;
        internal uint Flags, Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        internal int X, Y;
        internal uint Data, Flags, Time;
        internal UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    internal static bool Press(PaladinAction action)
    {
        ushort key = action switch
        {
            PaladinAction.Seal => 0x37,      // 7
            PaladinAction.Judgement => 0x38, // 8
            PaladinAction.HolyStrike => 0x7A, // F11
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        Input[] inputs = [Keyboard(key, 0), Keyboard(key, KeyUp)];
        int size = Marshal.SizeOf<Input>();
        uint sent = SendInput((uint)inputs.Length, inputs, size);
        if (sent == 1) SendInput(1, [inputs[1]], size);
        return sent == inputs.Length;
    }

    private static Input Keyboard(ushort key, uint flags) =>
        new() { Type = InputKeyboard, Data = new() { Key = new() { VirtualKey = key, Flags = flags } } };
}
