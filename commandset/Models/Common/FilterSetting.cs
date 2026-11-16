using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Filter settings - supports combined filter conditions
    /// </summary>
    public class FilterSetting
    {
        /// <summary>
        /// Revit built-in category name to filter by (e.g. "OST_Walls").
        /// If null or empty, no category filtering is applied.
        /// </summary>
        [JsonProperty("filterCategory")]
        public string FilterCategory { get; set; } = null;
        /// <summary>
        /// Revit element type name to filter by (e.g. "Wall" or "Autodesk.Revit.DB.Wall").
        /// If null or empty, no type filtering is applied.
        /// </summary>
        [JsonProperty("filterElementType")]
        public string FilterElementType { get; set; } = null;
        /// <summary>
        /// ElementId value of the family type (FamilySymbol) to filter by.
        /// If 0 or negative, no family filtering is applied.
        /// Note: this filter applies only to element instances, not to type elements.
        /// </summary>
        [JsonProperty("filterFamilySymbolId")]
        public int FilterFamilySymbolId { get; set; } = -1;
        /// <summary>
        /// Whether to include element types (e.g. wall types, door types)
        /// </summary>
        [JsonProperty("includeTypes")]
        public bool IncludeTypes { get; set; } = false;
        /// <summary>
        /// Whether to include element instances (e.g. placed walls, doors)
        /// </summary>
        [JsonProperty("includeInstances")]
        public bool IncludeInstances { get; set; } = true;
        /// <summary>
        /// Whether to return only elements visible in the current view.
        /// Note: this filter applies only to element instances, not to type elements.
        /// </summary>
        [JsonProperty("filterVisibleInCurrentView")]
        public bool FilterVisibleInCurrentView { get; set; }
        /// <summary>
        /// Minimum point of the spatial bounding box filter (units: mm).
        /// If set together with BoundingBoxMax, elements intersecting this box are returned.
        /// </summary>
        [JsonProperty("boundingBoxMin")]
        public JZPoint BoundingBoxMin { get; set; } = null;
        /// <summary>
        /// Maximum point of the spatial bounding box filter (units: mm).
        /// If set together with BoundingBoxMin, elements intersecting this box are returned.
        /// </summary>
        [JsonProperty("boundingBoxMax")]
        public JZPoint BoundingBoxMax { get; set; } = null;
        /// <summary>
        /// Maximum number of elements
        /// </summary>
        [JsonProperty("maxElements")]
        public int MaxElements { get; set; } = 50;
        /// <summary>
        /// Validates the filter settings and checks for conflicts
        /// </summary>
        /// <returns>true if the settings are valid, otherwise false</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            // At least one element kind must be selected
            if (!IncludeTypes && !IncludeInstances)
            {
                errorMessage = "Invalid filter settings: must include element types or element instances (or both)";
                return false;
            }

            // At least one filter condition must be specified
            if (string.IsNullOrWhiteSpace(FilterCategory) &&
                string.IsNullOrWhiteSpace(FilterElementType) &&
                FilterFamilySymbolId <= 0)
            {
                errorMessage = "Invalid filter settings: at least one filter condition must be specified (category, element type or family type)";
                return false;
            }

            // Check conflicts between type elements and some filters
            if (IncludeTypes && !IncludeInstances)
            {
                List<string> invalidFilters = new List<string>();
                if (FilterFamilySymbolId > 0)
                    invalidFilters.Add("family instance filter");
                if (FilterVisibleInCurrentView)
                    invalidFilters.Add("view visibility filter");
                if (invalidFilters.Count > 0)
                {
                    errorMessage = $"When filtering type elements only, the following filters do not apply: {string.Join(", ", invalidFilters)}";
                    return false;
                }
            }
            // Validate the spatial bounding box filter
            if (BoundingBoxMin != null && BoundingBoxMax != null)
            {
                // Min point must be less than or equal to max point
                if (BoundingBoxMin.X > BoundingBoxMax.X ||
                    BoundingBoxMin.Y > BoundingBoxMax.Y ||
                    BoundingBoxMin.Z > BoundingBoxMax.Z)
                {
                    errorMessage = "Invalid bounding box filter: min point coordinates must be less than or equal to max point coordinates";
                    return false;
                }
            }
            else if (BoundingBoxMin != null || BoundingBoxMax != null)
            {
                errorMessage = "Invalid bounding box filter: both min and max points must be set";
                return false;
            }
            return true;
        }
    }
}
