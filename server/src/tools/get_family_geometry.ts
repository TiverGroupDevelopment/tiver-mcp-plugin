import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool } from "../utils/familyTool.js";

export function registerGetFamilyGeometryTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_family_geometry",
    "List forms (extrusions, blends, revolutions, sweeps) with ids, bounding boxes in mm and parameters that can be associated with family parameters; also reference planes, levels and materials of the family.",
    {
      familyName: familyNameField,
    }
  );
}
