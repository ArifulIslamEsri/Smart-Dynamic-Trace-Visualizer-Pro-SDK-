using System.Linq;
using System.Collections.Generic;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ProAppModule5
{
    internal class RunTraceButton : Button
    {
        protected override async void OnClick()
        {
            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;

                if (mapView == null)
                {
                    MessageBox.Show("No active map.");
                    return;
                }

                var map = mapView.Map;

                // ✅ STEP 1: Get selected feature
                var selection = map.GetSelection();

                if (selection.Count == 0)
                {
                    MessageBox.Show("❌ Please select a feature first.");
                    return;
                }

                var kvp = selection.ToDictionary().First();
                var layer = kvp.Key as FeatureLayer;
                var selectedIds = kvp.Value;

                var expandedIds = new HashSet<long>(selectedIds);

                // ✅ STEP 2: Get geometry of selected feature
                Geometry selectedGeometry = null;

                using (var table = layer.GetTable())
                using (var cursor = table.Search(
                    new QueryFilter()
                    {
                        WhereClause = $"OBJECTID = {selectedIds.First()}"
                    }))
                {
                    if (cursor.MoveNext())
                    {
                        using (var row = cursor.Current)
                        {
                            var feature = row as Feature;
                            if (feature != null)
                                selectedGeometry = feature.GetShape();
                        }
                    }
                }

                if (selectedGeometry == null)
                {
                    MessageBox.Show("Error reading geometry.");
                    return;
                }

                // ✅ STEP 3: BUFFER + dynamic expansion
                using (var table = layer.GetTable())
                {
                    var buffer = GeometryEngine.Instance.Buffer(selectedGeometry, 200); // change size if needed

                    var spatialFilter = new SpatialQueryFilter()
                    {
                        FilterGeometry = buffer,
                        SpatialRelationship = SpatialRelationship.Intersects
                    };

                    using (var cursor = table.Search(spatialFilter))
                    {
                        while (cursor.MoveNext())
                        {
                            using (var row = cursor.Current)
                            {
                                expandedIds.Add(row.GetObjectID());
                            }
                        }
                    }
                }

                // ✅ STEP 4: Apply selection (IMPORTANT FIX)
                var selectFilter = new QueryFilter()
                {
                    WhereClause = $"OBJECTID IN ({string.Join(",", expandedIds)})"
                };

                layer.Select(selectFilter);

                // ✅ STEP 5: Zoom
                mapView.ZoomToSelected();

                MessageBox.Show(
                    $"✅ TRACE COMPLETE\n\n" +
                    $"Start Feature: {selectedIds.Count}\n" +
                    $"Expanded Features: {expandedIds.Count}\n\n" +
                    $"✔ Dynamic\n✔ Buffer-based trace\n✔ Zoom applied",
                    "Smart Trace"
                );
            });
        }
    }
}