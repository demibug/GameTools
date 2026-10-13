#nullable enable
namespace RotationUS.Diagnostics;

// Independent streams retain their last state, not a permanent set of messages:
// A -> B -> A is meaningful, even when other streams log between these changes.
internal sealed class StateChangeLog
{
    private readonly Dictionary<string,string> previous=new();
    internal bool Changed(string key,string state)
    {
        if(previous.TryGetValue(key,out var old) && old==state) return false;
        previous[key]=state;return true;
    }
}
