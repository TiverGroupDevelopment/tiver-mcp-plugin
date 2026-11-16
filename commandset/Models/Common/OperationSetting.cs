using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Operation types that can be performed on elements
    /// </summary>
    public enum ElementOperationType
    {
        /// <summary>
        /// Select elements
        /// </summary>
        Select,

        /// <summary>
        /// Selection box
        /// </summary>
        SelectionBox,

        /// <summary>
        /// Set element color and fill
        /// </summary>
        SetColor,

        /// <summary>
        /// Set element transparency
        /// </summary>
        SetTransparency,

        /// <summary>
        /// Delete elements
        /// </summary>
        Delete,

        /// <summary>
        /// Hide elements
        /// </summary>
        Hide,

        /// <summary>
        /// Temporarily hide elements
        /// </summary>
        TempHide,

        /// <summary>
        /// Isolate elements (show only them)
        /// </summary>
        Isolate,

        /// <summary>
        /// Unhide elements
        /// </summary>
        Unhide,

        /// <summary>
        /// Reset isolation (show all elements)
        /// </summary>
        ResetIsolate,
    }


    /// <summary>
    /// Element operation settings
    /// </summary>
    public class OperationSetting
    {
        /// <summary>
        /// IDs of the elements to operate on
        /// </summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds = new List<int>();

        /// <summary>
        /// Action to perform; string value of the ElementOperationType enum
        /// </summary>
        [JsonProperty("action")]
        public string Action { get; set; } = "Select";

        /// <summary>
        /// Transparency value (0-100); higher means more transparent
        /// </summary>
        [JsonProperty("transparencyValue")]
        public int TransparencyValue { get; set; } = 50;

        /// <summary>
        /// Element color (RGB), red by default
        /// </summary>
        [JsonProperty("colorValue")]
        public int[] ColorValue { get; set; } = new int[] { 255, 0, 0 }; // red by default
    }
}
