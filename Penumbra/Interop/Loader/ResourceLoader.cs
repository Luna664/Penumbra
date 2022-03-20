using System;
using Dalamud.Utility.Signatures;
using Penumbra.GameData.ByteString;

namespace Penumbra.Interop.Loader;

public unsafe partial class ResourceLoader : IDisposable
{
    // Toggle whether replacing paths is active, independently of hook and event state.
    public bool DoReplacements { get; private set; }

    // Hooks are required for everything, even events firing.
    public bool HooksEnabled { get; private set; }

    // This Logging just logs all file requests, returns and loads to the Dalamud log.
    // Events can be used to make smarter logging.
    public bool IsLoggingEnabled { get; private set; }

    public void EnableFullLogging()
    {
        if( IsLoggingEnabled )
        {
            return;
        }

        IsLoggingEnabled  =  true;
        ResourceRequested += LogPath;
        ResourceLoaded    += LogResource;
        FileLoaded        += LogLoadedFile;
        EnableHooks();
    }

    public void DisableFullLogging()
    {
        if( !IsLoggingEnabled )
        {
            return;
        }

        IsLoggingEnabled  =  false;
        ResourceRequested -= LogPath;
        ResourceLoaded    -= LogResource;
        FileLoaded        -= LogLoadedFile;
    }

    public void EnableReplacements()
    {
        if( DoReplacements )
        {
            return;
        }

        DoReplacements = true;
        EnableTexMdlTreatment();
        EnableHooks();
    }

    public void DisableReplacements()
    {
        if( !DoReplacements )
        {
            return;
        }

        DoReplacements = false;
        DisableTexMdlTreatment();
    }

    public void EnableHooks()
    {
        if( HooksEnabled )
        {
            return;
        }

        HooksEnabled = true;
        ReadSqPackHook.Enable();
        GetResourceSyncHook.Enable();
        GetResourceAsyncHook.Enable();
    }

    public void DisableHooks()
    {
        if( !HooksEnabled )
        {
            return;
        }

        HooksEnabled = false;
        ReadSqPackHook.Disable();
        GetResourceSyncHook.Disable();
        GetResourceAsyncHook.Disable();
    }

    public ResourceLoader( Penumbra _ )
    {
        SignatureHelper.Initialise( this );
    }

    // Event fired whenever a resource is requested.
    public delegate void ResourceRequestedDelegate( Utf8GamePath path, bool synchronous );
    public event ResourceRequestedDelegate? ResourceRequested;

    // Event fired whenever a resource is returned.
    // If the path was manipulated by penumbra, manipulatedPath will be the file path of the loaded resource.
    // resolveData is additional data returned by the current ResolvePath function and is user-defined.
    public delegate void ResourceLoadedDelegate( Structs.ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        object? resolveData );

    public event ResourceLoadedDelegate? ResourceLoaded;


    // Event fired whenever a resource is newly loaded.
    // Success indicates the return value of the loading function (which does not imply that the resource was actually successfully loaded)
    // custom is true if the file was loaded from local files instead of the default SqPacks.
    public delegate void FileLoadedDelegate( Utf8String path, bool success, bool custom );
    public event FileLoadedDelegate? FileLoaded;

    // Customization point to control how path resolving is handled.
    public Func< Utf8GamePath, (FullPath?, object?) > ResolvePath { get; set; } = DefaultReplacer;

    public void Dispose()
    {
        DisableFullLogging();
        DisposeHooks();
        DisposeTexMdlTreatment();
    }
}