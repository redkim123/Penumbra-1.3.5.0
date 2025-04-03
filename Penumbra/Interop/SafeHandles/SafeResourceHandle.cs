using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.Interop.SafeHandles;

public unsafe class SafeResourceHandle : SafeHandle, ICloneable
{
    public ResourceHandle* ResourceHandle
        => (ResourceHandle*)handle;

    public override bool IsInvalid
        => handle == 0;

    public SafeResourceHandle(ResourceHandle* handle, bool incRef, bool ownsHandle = true)
        : base(0, ownsHandle)
    {
        if (incRef && !ownsHandle)
            throw new ArgumentException("Non-owning SafeResourceHandle with IncRef is unsupported");

        if (incRef && handle != null)
            handle->IncRef();
        SetHandle((nint)handle);
    }

    public SafeResourceHandle Clone()
        => new(ResourceHandle, true);

    object ICloneable.Clone()
        => Clone();

    public static SafeResourceHandle CreateInvalid()
        => new(null, false);

    protected override bool ReleaseHandle()
    {
        var handle = Interlocked.Exchange(ref this.handle, 0);
        if (handle != 0)
            ((ResourceHandle*)handle)->DecRef();

        return true;
    }
}
