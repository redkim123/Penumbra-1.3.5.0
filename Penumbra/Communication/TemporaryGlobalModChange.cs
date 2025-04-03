using OtterGui.Classes;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a temporary mod for all collections is changed.
/// <list type="number">
///     <item>Parameter added, deleted or edited temporary mod.</item>
///     <item>Parameter is whether the mod was newly created.</item>
///     <item>Parameter is whether the mod was deleted.</item>
/// </list> </summary>
public sealed class TemporaryGlobalModChange()
    : EventWrapper<TemporaryMod, bool, bool, TemporaryGlobalModChange.Priority>(nameof(TemporaryGlobalModChange))
{
    public enum Priority
    {
        /// <seealso cref="Collections.Cache.CollectionCacheManager.OnGlobalModChange"/>
        CollectionCacheManager = 0,

        /// <seealso cref="Collections.Manager.TempCollectionManager.OnGlobalModChange"/>
        TempCollectionManager = 0,
    }
}
