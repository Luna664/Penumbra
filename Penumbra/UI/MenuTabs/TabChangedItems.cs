using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private class TabChangedItems
    {
        private const    string            LabelTab = "Changed Items";
        private readonly SettingsInterface _base;

        private string _filter      = string.Empty;
        private string _filterLower = string.Empty;

        public TabChangedItems( SettingsInterface ui )
            => _base = ui;

        public void Draw()
        {
            if( !ImGui.BeginTabItem( LabelTab ) )
            {
                return;
            }

            var modManager = Penumbra.ModManager;
            var items      = modManager.Collections.ActiveCollection.Cache?.ChangedItems ?? new Dictionary< string, object? >();
            var forced     = modManager.Collections.ForcedCollection.Cache?.ChangedItems ?? new Dictionary< string, object? >();

            using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

            ImGui.SetNextItemWidth( -1 );
            if( ImGui.InputTextWithHint( "##ChangedItemsFilter", "Filter...", ref _filter, 64 ) )
            {
                _filterLower = _filter.ToLowerInvariant();
            }

            if( !ImGui.BeginTable( "##ChangedItemsTable", 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, AutoFillSize ) )
            {
                return;
            }

            raii.Push( ImGui.EndTable );

            var list = items.AsEnumerable();
            if( forced.Count > 0 )
            {
                list = list.Concat( forced ).OrderBy( kvp => kvp.Key );
            }

            if( _filter.Any() )
            {
                list = list.Where( kvp => kvp.Key.ToLowerInvariant().Contains( _filterLower ) );
            }

            foreach( var (name, data) in list )
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                _base.DrawChangedItem( name, data, ImGui.GetStyle().ScrollbarSize );
            }
        }
    }
}