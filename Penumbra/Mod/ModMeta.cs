using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;

namespace Penumbra.Mod;

// Contains descriptive data about the mod as well as possible settings and fileswaps.
public class ModMeta
{
    public uint FileVersion { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            _name     = value;
            LowerName = value.ToLowerInvariant();
        }
    }

    private string _name = "Mod";

    [JsonIgnore]
    public string LowerName { get; private set; } = "mod";

    private string _author = "";

    public string Author
    {
        get => _author;
        set
        {
            _author     = value;
            LowerAuthor = value.ToLowerInvariant();
        }
    }

    [JsonIgnore]
    public string LowerAuthor { get; private set; } = "";

    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Website { get; set; } = "";

    [JsonProperty( ItemConverterType = typeof( FullPath.FullPathConverter ) )]
    public Dictionary< Utf8GamePath, FullPath > FileSwaps { get; set; } = new();

    public Dictionary< string, OptionGroup > Groups { get; set; } = new();

    [JsonIgnore]
    private int FileHash { get; set; }

    [JsonIgnore]
    public bool HasGroupsWithConfig { get; private set; }

    public bool RefreshFromFile( FileInfo filePath )
    {
        var newMeta = LoadFromFile( filePath );
        if( newMeta == null )
        {
            return true;
        }

        if( newMeta.FileHash == FileHash )
        {
            return false;
        }

        FileVersion         = newMeta.FileVersion;
        Name                = newMeta.Name;
        Author              = newMeta.Author;
        Description         = newMeta.Description;
        Version             = newMeta.Version;
        Website             = newMeta.Website;
        FileSwaps           = newMeta.FileSwaps;
        Groups              = newMeta.Groups;
        FileHash            = newMeta.FileHash;
        HasGroupsWithConfig = newMeta.HasGroupsWithConfig;
        return true;
    }

    public static ModMeta? LoadFromFile( FileInfo filePath )
    {
        try
        {
            var text = File.ReadAllText( filePath.FullName );

            var meta = JsonConvert.DeserializeObject< ModMeta >( text,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore } );
            if( meta != null )
            {
                meta.FileHash = text.GetHashCode();
                meta.RefreshHasGroupsWithConfig();
            }

            return meta;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not load mod meta:\n{e}" );
            return null;
        }
    }

    public bool RefreshHasGroupsWithConfig()
    {
        var oldValue = HasGroupsWithConfig;
        HasGroupsWithConfig = Groups.Values.Any( g => g.Options.Count > 1 || g.SelectionType == SelectType.Multi && g.Options.Count == 1 );
        return oldValue != HasGroupsWithConfig;
    }


    public void SaveToFile( FileInfo filePath )
    {
        try
        {
            var text    = JsonConvert.SerializeObject( this, Formatting.Indented );
            var newHash = text.GetHashCode();
            if( newHash != FileHash )
            {
                File.WriteAllText( filePath.FullName, text );
                FileHash = newHash;
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write meta file for mod {Name} to {filePath.FullName}:\n{e}" );
        }
    }
}