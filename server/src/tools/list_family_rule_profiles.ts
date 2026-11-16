import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { SECTIONS, listProfiles, parentsOf, rulesRoot } from "../utils/familyRules.js";

export function registerListFamilyRuleProfilesTool(server: McpServer) {
  server.tool(
    "list_family_rule_profiles",
    "List rule profiles for family modelling (rules/<profile>/profile.json): global rules, reusable packs and project profiles — name, kind, what each one extends, scope and when to apply — and the rule sections. Apply a project profile only when the user names that project; otherwise the global rules apply (get_family_rules and check_family_rules use them when profile is omitted).",
    {},
    async () => {
      const profiles = listProfiles().map((p) => ({
        id: p.profile.id,
        name: p.profile.name,
        kind: p.profile.kind ?? "project",
        status: p.profile.status ?? "validated",
        version: p.profile.version,
        extends: parentsOf(p.profile),
        scope: p.profile.scope,
        activation: p.profile.activation,
        revitVersion: p.profile.revitVersion,
        folder: p.dir,
      }));
      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify({ root: rulesRoot(), sections: SECTIONS, profiles }, null, 2),
          },
        ],
      };
    }
  );
}
