using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Files;

/// <summary>
/// EST Structure:
/// 1x [NumEntries : UInt32]
/// Apparently entries need to be sorted.
/// #NumEntries x [SetId : UInt16] [RaceId : UInt16]
/// #NumEntries x [SkeletonId : UInt16]
/// </summary>
public sealed unsafe class EstFile : MetaBaseFile
{
    private const ushort EntryDescSize = 4;
    private const ushort EntrySize     = 2;
    private const int    IncreaseSize  = 512;

    public int Count
        => *(int*)Data;

    private int Size
        => 4 + Count * (EntryDescSize + EntrySize);

    public enum EstEntryChange
    {
        Unchanged,
        Changed,
        Added,
        Removed,
    }

    public EstEntry this[GenderRace genderRace, PrimaryId setId]
    {
        get
        {
            var (idx, exists) = FindEntry(genderRace, setId);
            if (!exists)
                return EstEntry.Zero;

            return *(EstEntry*)(Data + EntryDescSize * (Count + 1) + EntrySize * idx);
        }
        set => SetEntry(genderRace, setId, value);
    }

    private void InsertEntry(int idx, GenderRace genderRace, PrimaryId setId, EstEntry skeletonId)
    {
        if (Length < Size + EntryDescSize + EntrySize)
            ResizeResources(Length + IncreaseSize);

        var control = (Info*)(Data + 4);
        var entries = (EstEntry*)(control + Count);

        for (var i = Count - 1; i >= idx; --i)
            entries[i + 3] = entries[i];

        entries[idx + 2] = skeletonId;

        for (var i = idx - 1; i >= 0; --i)
            entries[i + 2] = entries[i];

        for (var i = Count - 1; i >= idx; --i)
            control[i + 1] = control[i];

        control[idx] = new Info(genderRace, setId);

        *(int*)Data = Count + 1;
    }

    private void RemoveEntry(int idx)
    {
        var control = (Info*)(Data + 4);
        var entries = (ushort*)(control + Count);

        for (var i = idx; i < Count; ++i)
            control[i] = control[i + 1];

        for (var i = 0; i < idx; ++i)
            entries[i - 2] = entries[i];

        for (var i = idx; i < Count - 1; ++i)
            entries[i - 2] = entries[i + 1];

        entries[Count - 3] = 0;
        entries[Count - 2] = 0;
        entries[Count - 1] = 0;
        *(int*)Data        = Count - 1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 4)]
    private struct Info : IComparable<Info>
    {
        public readonly PrimaryId  SetId;
        public readonly GenderRace GenderRace;

        public Info(GenderRace gr, PrimaryId setId)
        {
            GenderRace = gr;
            SetId      = setId;
        }

        public int CompareTo(Info other)
        {
            var genderRaceComparison = GenderRace.CompareTo(other.GenderRace);
            return genderRaceComparison != 0 ? genderRaceComparison : SetId.Id.CompareTo(other.SetId.Id);
        }
    }

    private static (int, bool) FindEntry(ReadOnlySpan<Info> data, GenderRace genderRace, PrimaryId setId)
    {
        var idx = data.BinarySearch(new Info(genderRace, setId));
        return idx < 0 ? (~idx, false) : (idx, true);
    }

    private (int, bool) FindEntry(GenderRace genderRace, PrimaryId setId)
    {
        var span = new ReadOnlySpan<Info>(Data + 4, Count);
        return FindEntry(span, genderRace, setId);
    }

    public EstEntryChange SetEntry(GenderRace genderRace, PrimaryId setId, EstEntry skeletonId)
    {
        var (idx, exists) = FindEntry(genderRace, setId);
        if (exists)
        {
            var value = *(EstEntry*)(Data + 4 * (Count + 1) + 2 * idx);
            if (value == skeletonId)
                return EstEntryChange.Unchanged;

            if (skeletonId == EstEntry.Zero)
            {
                RemoveEntry(idx);
                return EstEntryChange.Removed;
            }

            *(EstEntry*)(Data + 4 * (Count + 1) + 2 * idx) = skeletonId;
            return EstEntryChange.Changed;
        }

        if (skeletonId == EstEntry.Zero)
            return EstEntryChange.Unchanged;

        InsertEntry(idx, genderRace, setId, skeletonId);
        return EstEntryChange.Added;
    }

    public override void Reset()
    {
        var (d, length) = DefaultData;
        var data = (byte*)d;
        MemoryUtility.MemCpyUnchecked(Data, data, length);
        MemoryUtility.MemSet(Data + length, 0, Length - length);
    }

    public EstFile(MetaFileManager manager, EstType estType)
        : base(manager, manager.MarshalAllocator, (MetaIndex)estType)
    {
        var length = DefaultData.Length;
        AllocateData(length + IncreaseSize);
        Reset();
    }

    public EstEntry GetDefault(GenderRace genderRace, PrimaryId setId)
        => GetDefault(Manager, Index, genderRace, setId);

    public static EstEntry GetDefault(MetaFileManager manager, CharacterUtility.InternalIndex index, GenderRace genderRace, PrimaryId primaryId)
    {
        var data  = (byte*)manager.CharacterUtility.DefaultResource(index).Address;
        var count = *(int*)data;
        var span  = new ReadOnlySpan<Info>(data + 4, count);
        var (idx, found) = FindEntry(span, genderRace, primaryId.Id);
        if (!found)
            return EstEntry.Zero;

        return *(EstEntry*)(data + 4 + count * EntryDescSize + idx * EntrySize);
    }

    public static EstEntry GetDefault(MetaFileManager manager, MetaIndex metaIndex, GenderRace genderRace, PrimaryId primaryId)
        => GetDefault(manager, CharacterUtility.ReverseIndices[(int)metaIndex], genderRace, primaryId);

    public static EstEntry GetDefault(MetaFileManager manager, EstType estType, GenderRace genderRace, PrimaryId primaryId)
        => GetDefault(manager, (MetaIndex)estType, genderRace, primaryId);

    public static EstEntry GetDefault(MetaFileManager manager, EstIdentifier identifier)
        => GetDefault(manager, identifier.FileIndex(), identifier.GenderRace, identifier.SetId);
}
