import { z } from "zod";
import { existsSync, readFileSync } from "fs";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { checksWithGuids, loadProfile, parseSharedParameterFile, profilePath } from "../utils/familyRules.js";

export function registerGetFamilyRulesTool(server: McpServer) {
  server.tool(
    "get_family_rules",
    "Get the family modelling rules of a project profile: the rules text (what to do, how, exceptions), conventions (categories, part types, lookup key, fixed parameter values), the shared parameter file path and the required shared parameters with GUIDs. Call it before modelling a family for that project and follow it; check the result with check_family_rules. Only when the user names the project — profiles are not defaults.",
    {
      profile: z.string().describe("Profile id, folder or name, e.g. 'alterplast' or 'Альтерпласт' (see list_family_rule_profiles)"),
      includeText: z.boolean().optional().describe("Include the full rules text (default true)"),
    },
    async (args) => {
      try {
        const loaded = loadProfile(args.profile);
        const rulesPath = profilePath(loaded, loaded.profile.rulesFile ?? "ПРАВИЛА.md");
        const fopPath = profilePath(loaded, loaded.profile.sharedParameterFile);
        const { checks, missingInFop } = checksWithGuids(loaded);
        const fop = fopPath && existsSync(fopPath) ? parseSharedParameterFile(fopPath) : null;
        const required = ((checks as any).requiredParameters?.parameters ?? []).map((p: any) => ({
          ...p,
          dataType: fop?.get(p.name)?.dataType,
          fopGroup: fop?.get(p.name)?.group,
        }));

        const summary = {
          profile: loaded.profile.name,
          id: loaded.profile.id,
          version: loaded.profile.version,
          scope: loaded.profile.scope,
          activation: loaded.profile.activation,
          revitVersion: loaded.profile.revitVersion,
          sharedParameterFile: fopPath,
          requiredParameters: required,
          missingInSharedParameterFile: missingInFop,
          conventions: loaded.profile.conventions,
          automaticChecks: Object.entries(loaded.profile.checks ?? {}).map(([check, spec]) => ({ check, rule: (spec as any).rule })),
          manualRules: loaded.profile.manualRules ?? [],
          examples: loaded.profile.examples,
        };
        const content: { type: "text"; text: string }[] = [{ type: "text", text: JSON.stringify(summary, null, 2) }];
        if (args.includeText !== false && rulesPath && existsSync(rulesPath)) {
          content.push({ type: "text", text: readFileSync(rulesPath, "utf8").replace(/^﻿/, "") });
        }
        return { content };
      } catch (error) {
        return {
          content: [{ type: "text" as const, text: `get_family_rules failed: ${error instanceof Error ? error.message : String(error)}` }],
          isError: true,
        };
      }
    }
  );
}
