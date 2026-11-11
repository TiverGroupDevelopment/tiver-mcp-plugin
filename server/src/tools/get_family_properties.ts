import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerGetFamilyPropertiesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_family_properties",
    `Read family-level properties: category, placement type, hosting, part type, work-plane-based, always vertical, shared, room calculation point, cut with voids when loaded, OmniClass, and the subcategories of the family category. ${sessionNote}`,
    {
      familyName: familyNameField,
    }
  );
}
