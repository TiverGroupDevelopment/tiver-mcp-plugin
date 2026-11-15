import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { listProfiles, rulesRoot } from "../utils/familyRules.js";

export function registerListFamilyRuleProfilesTool(server: McpServer) {
  server.tool(
    "list_family_rule_profiles",
    "List project rule profiles for family modelling (rules/<project>/profile.json): name, scope and when to apply. Profiles are opt-in: apply one only when the user names that project or profile; otherwise model without project rules.",
    {},
    async () => {
      const profiles = listProfiles().map((p) => ({
        id: p.profile.id,
        name: p.profile.name,
        version: p.profile.version,
        scope: p.profile.scope,
        activation: p.profile.activation,
        revitVersion: p.profile.revitVersion,
        folder: p.dir,
      }));
      return {
        content: [{ type: "text" as const, text: JSON.stringify({ root: rulesRoot(), profiles }, null, 2) }],
      };
    }
  );
}
