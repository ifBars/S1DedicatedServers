using System.Runtime.CompilerServices;
using DedicatedServerMod.Server.Game.Patches.Gameplay;
using HarmonyLib;
using ScheduleOne.SpecialCustomers;

internal static class SpecialCustomerGroupTests
{
    internal static void Run(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(SpecialCustomerSelectionPatches)).Patch();
        harmony.CreateClassProcessor(typeof(SpecialCustomerLoadPatches)).Patch();
        var manager = new SpecialCustomerManager();
        manager._specialCustomerGroups.Add(new SpecialCustomerData { GroupId = "hippies" });
        if (manager.GetNextCustomerGroupOrdered() != "hippies")
            throw new InvalidOperationException("First-visit selection must match the asset ID exactly.");

        manager.SetData(new SpecialCustomerSaveData { GroupId = "Hippies" });
        if (!manager.ResolvedGroup)
            throw new InvalidOperationException("Existing mixed-case save IDs must resolve before native loading.");

        var unknown = new SpecialCustomerSaveData { GroupId = "custom-group" };
        manager.SetData(unknown);
        if (unknown.GroupId != "custom-group")
            throw new InvalidOperationException("Unknown IDs must be left to native load policy.");
        manager.SetData(null);
        Console.WriteLine("PASS|SpecialCustomerGroupTests|first-visit|save-recovery|unknown-id|null-data");
    }
}

namespace ScheduleOne.SpecialCustomers
{
    public sealed class SpecialCustomerData
    {
        public string GroupId { get; set; } = "";
    }

    public sealed class SpecialCustomerSaveData
    {
        public string GroupId { get; set; } = "";
    }

    public sealed class SpecialCustomerManager
    {
        public readonly List<SpecialCustomerData> _specialCustomerGroups = new();
        public bool ResolvedGroup { get; private set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetNextCustomerGroupOrdered() => "Hippies";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetData(SpecialCustomerSaveData? data) =>
            ResolvedGroup = _specialCustomerGroups.Any(group => group.GroupId == data?.GroupId);
    }
}
