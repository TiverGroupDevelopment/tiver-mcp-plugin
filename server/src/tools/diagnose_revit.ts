import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerDiagnoseRevitTool(server: McpServer) {
  registerFamilyTool(
    server,
    "diagnose_revit",
    "Diagnose Revit when tools fail or time out: open modal dialogs (read even while Revit is blocked), Revit version and build, short curve tolerance, family template and shared parameter paths, open documents (active, modified, pinned family session), and registered updaters (third-party updaters can roll back changes). Read-only.",
    {}
  );
}
