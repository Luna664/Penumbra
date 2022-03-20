using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.STD;
using Penumbra.GameData.ByteString;

namespace Penumbra.Interop.Loader;

public unsafe partial class ResourceLoader
{
    // A static pointer to the SE Resource Manager
    [Signature( "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 32 C0", ScanType = ScanType.StaticAddress, UseFlags = SignatureUseFlags.Pointer )]
    public static ResourceManager** ResourceManager;

    // Gather some debugging data about penumbra-loaded objects.
    public struct DebugData
    {
        public ResourceHandle*  OriginalResource;
        public ResourceHandle*  ManipulatedResource;
        public Utf8GamePath      OriginalPath;
        public FullPath         ManipulatedPath;
        public ResourceCategory Category;
        public object?          ResolverInfo;
        public uint             Extension;
    }

    private readonly SortedDictionary< FullPath, DebugData > _debugList  = new();
    private readonly List< (FullPath, DebugData?) >          _deleteList = new();

    public IReadOnlyDictionary< FullPath, DebugData > DebugList
        => _debugList;

    public void EnableDebug()
    {
        ResourceLoaded += AddModifiedDebugInfo;
    }

    public void DisableDebug()
    {
        ResourceLoaded -= AddModifiedDebugInfo;
    }

    private void AddModifiedDebugInfo( ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath, object? resolverInfo )
    {
        if( manipulatedPath == null )
        {
            return;
        }

        var crc              = ( uint )originalPath.Path.Crc32;
        var originalResource = ( *ResourceManager )->FindResourceHandle( &handle->Category, &handle->FileType, &crc );
        _debugList[ manipulatedPath.Value ] = new DebugData()
        {
            OriginalResource    = originalResource,
            ManipulatedResource = handle,
            Category            = handle->Category,
            Extension           = handle->FileType,
            OriginalPath        = originalPath.Clone(),
            ManipulatedPath     = manipulatedPath.Value,
            ResolverInfo        = resolverInfo,
        };
    }

    // Find a key in a StdMap.
    private static TValue* FindInMap< TKey, TValue >( StdMap< TKey, TValue >* map, in TKey key )
        where TKey : unmanaged, IComparable< TKey >
        where TValue : unmanaged
    {
        if( map == null || map->Count == 0 )
        {
            return null;
        }

        var node = map->Head->Parent;
        while( !node->IsNil )
        {
            switch( key.CompareTo( node->KeyValuePair.Item1 ) )
            {
                case 0: return &node->KeyValuePair.Item2;
                case < 0:
                    node = node->Left;
                    break;
                default:
                    node = node->Right;
                    break;
            }
        }

        return null;
    }

    // Iterate in tree-order through a map, applying action to each KeyValuePair.
    private static void IterateMap< TKey, TValue >( StdMap< TKey, TValue >* map, Action< TKey, TValue > action )
        where TKey : unmanaged
        where TValue : unmanaged
    {
        if( map == null || map->Count == 0 )
        {
            return;
        }

        for( var node = map->SmallestValue; !node->IsNil; node = node->Next() )
        {
            action( node->KeyValuePair.Item1, node->KeyValuePair.Item2 );
        }
    }


    // Find a resource in the resource manager by its category, extension and crc-hash
    public static ResourceHandle* FindResource( ResourceCategory cat, uint ext, uint crc32 )
    {
        var manager  = *ResourceManager;
        var category = ( ResourceGraph.CategoryContainer* )manager->ResourceGraph->ContainerArray + ( int )cat;
        var extMap   = FindInMap( category->MainMap, ext );
        if( extMap == null )
        {
            return null;
        }

        var ret = FindInMap( extMap->Value, crc32 );
        return ret == null ? null : ret->Value;
    }

    public delegate void ExtMapAction( ResourceCategory category, StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* graph );
    public delegate void ResourceMapAction( uint ext, StdMap< uint, Pointer< ResourceHandle > >* graph );
    public delegate void ResourceAction( uint crc32, ResourceHandle* graph );

    // Iteration functions through the resource manager.
    public static void IterateGraphs( ExtMapAction action )
    {
        var manager = *ResourceManager;
        foreach( var resourceType in Enum.GetValues< ResourceCategory >().SkipLast( 1 ) )
        {
            var graph = ( ResourceGraph.CategoryContainer* )manager->ResourceGraph->ContainerArray + ( int )resourceType;
            action( resourceType, graph->MainMap );
        }
    }

    public static void IterateExtMap( StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* map, ResourceMapAction action )
        => IterateMap( map, ( ext, m ) => action( ext, m.Value ) );

    public static void IterateResourceMap( StdMap< uint, Pointer< ResourceHandle > >* map, ResourceAction action )
        => IterateMap( map, ( crc, r ) => action( crc, r.Value ) );

    public static void IterateResources( ResourceAction action )
    {
        IterateGraphs( ( _, extMap )
            => IterateExtMap( extMap, ( _, resourceMap )
                => IterateResourceMap( resourceMap, action ) ) );
    }

    public void UpdateDebugInfo()
    {
        var manager = *ResourceManager;
        _deleteList.Clear();
        foreach( var data in _debugList.Values )
        {
            var regularResource  = FindResource( data.Category, data.Extension, ( uint )data.OriginalPath.Path.Crc32 );
            var modifiedResource = FindResource( data.Category, data.Extension, ( uint )data.ManipulatedPath.InternalName.Crc32 );
            if( modifiedResource == null )
            {
                _deleteList.Add( ( data.ManipulatedPath, null ) );
            }
            else if( regularResource != data.OriginalResource || modifiedResource != data.ManipulatedResource )
            {
                _deleteList.Add( ( data.ManipulatedPath, data with
                {
                    OriginalResource = regularResource,
                    ManipulatedResource = modifiedResource,
                } ) );
            }
        }

        foreach( var (path, data) in _deleteList )
        {
            if( data == null )
            {
                _debugList.Remove( path );
            }
            else
            {
                _debugList[ path ] = data.Value;
            }
        }
    }

    // Logging functions for EnableFullLogging.
    private static void LogPath( Utf8GamePath path, bool synchronous )
        => PluginLog.Information( $"[ResourceLoader] Requested {path} {( synchronous ? "synchronously." : "asynchronously." )}" );

    private static void LogResource( ResourceHandle* handle, Utf8GamePath path, FullPath? manipulatedPath, object? _ )
    {
        var pathString = manipulatedPath != null ? $"custom file {manipulatedPath} instead of {path}" : path.ToString();
        PluginLog.Information( $"[ResourceLoader] Loaded {pathString} to 0x{( ulong )handle:X}. (Refcount {handle->RefCount})" );
    }

    private static void LogLoadedFile( Utf8String path, bool success, bool custom )
        => PluginLog.Information( success
            ? $"[ResourceLoader] Loaded {path} from {( custom ? "local files" : "SqPack" )}"
            : $"[ResourceLoader] Failed to load {path} from {( custom ? "local files" : "SqPack" )}." );
}