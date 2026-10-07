#nullable enable
namespace RotationUS.Diagnostics;

internal sealed class ProfileLayouts
{
    private readonly string directory;
    internal string DiscoveryPath { get; }
    internal ProfileLayouts(string baseDirectory)
    {
        directory = Path.Combine(baseDirectory, "profiles");
        DiscoveryPath = Path.Combine(baseDirectory, "diagnostic-layout.json");
    }
    internal string PathFor(ClassProfile profile) => Path.Combine(directory, profile.Key + ".json");
    internal DiagnosticLayout Load(ClassProfile profile, DiagnosticLayout geometry)
    {
        string path = PathFor(profile);
        DiagnosticLayout result;
        if (File.Exists(path))
        {
            var saved = DiagnosticLayout.Load(path);
            if (saved.SpecId != profile.SpecId) throw new InvalidDataException("专精配置与当前职业不符：" + path);
            result = geometry.Copy(); result.Special = saved.Special; result.SpecialReferenceColor = saved.SpecialReferenceColor;
            bool compatible = PositionVerification.CompatibleSpecialGeometry(saved, geometry);
            result.SpecialEnabled = saved.SpecialEnabled && compatible;
            result.SpecialPoints = saved.SpecialPoints.ToDictionary(p => p.Key, p => p.Value.Copy());
            result.ThunderMode = saved.ThunderMode;
            if (!compatible)
            {
                result.SpecialReferenceColor = null;
                foreach (var point in result.SpecialPoints.Values)
                { point.Enabled = false; point.ReferenceColor = null; }
            }
        }
        else
        {
            result = geometry.Copy();
            if (geometry.SpecId != profile.SpecId) result.SpecialPoints.Clear();
            // Only migrate the previous protection-warrior special point to protection.
            if (profile.SpecId != 73 || geometry.SpecId is not (0 or 73))
            { result.SpecialEnabled = false; result.SpecialReferenceColor = null; result.Special = new(610, 8); }
        }
        result.SpecId = profile.SpecId;
        if (!profile.HasSpecial) { result.SpecialEnabled = false; result.SpecialReferenceColor = null; }
        if (profile.SpecId is not (71 or 72)) result.SpecialPoints.Clear();
        else result.SpecialEnabled = false;
        result.Validate(); return result;
    }
    internal void Save(ClassProfile profile, DiagnosticLayout layout)
    {
        if (layout.SpecId != profile.SpecId) throw new InvalidDataException("不能将其他专精的数据写入当前配置。");
        Directory.CreateDirectory(directory); layout.Save(PathFor(profile));
    }
    internal void SaveGeometry(DiagnosticLayout layout)
    {
        // A first launch on another class must not overwrite the old warrior point.
        if (!File.Exists(PathFor(ClassProfiles.Protection)) && File.Exists(DiscoveryPath))
        {
            var legacy = DiagnosticLayout.Load(DiscoveryPath);
            if (legacy.SpecId is 0 or 73)
                Save(ClassProfiles.Protection, Load(ClassProfiles.Protection, legacy));
        }
        var geometry = layout.Copy(); geometry.SpecId = 0;
        geometry.SpecialEnabled = false; geometry.SpecialReferenceColor = null;
        geometry.SpecialPoints.Clear(); geometry.ThunderMode = "presence-only";
        geometry.Save(DiscoveryPath);
    }
}
