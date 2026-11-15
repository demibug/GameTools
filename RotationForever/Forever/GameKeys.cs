#nullable enable
using System.Runtime.InteropServices;

namespace RotationForever;

internal static class GameKeys
{
    internal readonly record struct KeyStroke(ushort Key, bool Up);
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

    internal static KeyStroke[] Sequence(PaladinAction action)
    {
        (ushort key, bool shift) = action switch
        {
            PaladinAction.Seal => ((ushort)0x31, true),         // Shift+1
            PaladinAction.CrusaderSeal => ((ushort)0x32, true), // Shift+2
            PaladinAction.Judgement => ((ushort)0x37, false),   // 7
            PaladinAction.HolyStrike => ((ushort)0x38, false),  // 8
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        return shift
            ? [new(0xA0, false), new(key, false), new(key, true), new(0xA0, true)]
            : [new(key, false), new(key, true)];
    }

    internal static bool Press(PaladinAction action) => Press(action, Send);

    internal static bool Press(PaladinAction action, Func<KeyStroke[], uint> send)
    {
        var sequence = Sequence(action);
        uint sent = send(sequence);
        if (sent == sequence.Length) return true;
        // Release only keys pressed by the accepted prefix, in reverse order.
        // A partially accepted chord must not leave our Shift held down.
        var held = new List<ushort>();
        foreach (var stroke in sequence.Take((int)sent))
            if (stroke.Up) held.Remove(stroke.Key); else held.Add(stroke.Key);
        if (held.Count > 0) send(held.AsEnumerable().Reverse().Select(key => new KeyStroke(key, true)).ToArray());
        return false;
    }

    private static uint Send(KeyStroke[] sequence)
    {
        var inputs = sequence.Select(stroke => Keyboard(stroke.Key, stroke.Up ? KeyUp : 0)).ToArray();
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input Keyboard(ushort key, uint flags) =>
        new() { Type = InputKeyboard, Data = new() { Key = new() { VirtualKey = key, Flags = flags } } };
}
