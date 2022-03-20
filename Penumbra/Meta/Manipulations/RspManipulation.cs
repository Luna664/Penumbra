using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct RspManipulation : IMetaManipulation< RspManipulation >
{
    public readonly float Entry;

    [JsonConverter( typeof( StringEnumConverter ) )]
    public readonly SubRace SubRace;

    [JsonConverter( typeof( StringEnumConverter ) )]
    public readonly RspAttribute Attribute;

    public RspManipulation( SubRace subRace, RspAttribute attribute, float entry )
    {
        Entry     = entry;
        SubRace   = subRace;
        Attribute = attribute;
    }

    public override string ToString()
        => $"Rsp - {SubRace.ToName()} - {Attribute.ToFullString()}";

    public bool Equals( RspManipulation other )
        => SubRace    == other.SubRace
         && Attribute == other.Attribute;

    public override bool Equals( object? obj )
        => obj is RspManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( ( int )SubRace, ( int )Attribute );

    public int CompareTo( RspManipulation other )
    {
        var s = SubRace.CompareTo( other.SubRace );
        return s != 0 ? s : Attribute.CompareTo( other.Attribute );
    }

    public int FileIndex()
        => CharacterUtility.HumanCmpIdx;

    public bool Apply( CmpFile file )
    {
        var value = file[ SubRace, Attribute ];
        if( value == Entry )
        {
            return false;
        }

        file[ SubRace, Attribute ] = Entry;
        return true;
    }
}