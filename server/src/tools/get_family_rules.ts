import { z } from "zod";
import { existsSync, readFileSync } from "fs";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  SECTIONS,
  checkSection,
  checksWithGuids,
  dataRoot,
  resolveDataPath,
  describeLayers,
  parseSharedParameterFile,
  resolveRequestedProfile,
} from "../utils/familyRules.js";

export function registerGetFamilyRulesTool(server: McpServer) {
  server.tool(
    "get_family_rules",
    "Get the family modelling rules of a profile with everything it inherits (global rules, packs, parent project profile): the rules text of every layer from general to specific (what to do, how, exceptions), conventions (categories, part types, lookup key, fixed parameter values), the shared parameter file path and the required shared parameters with GUIDs. One call returns all layers — do not request parent profiles separately. Call it before modelling a family and follow it; check the result with check_family_rules. profile is optional: without it only the global rules are returned. Pass a project profile only when the user names that project.",
    {
      profile: z
        .string()
        .optional()
        .describe("Profile id, folder or name, e.g. 'alterplast' or 'Альтерпласт' (see list_family_rule_profiles). Omit for the global rules only."),
      section: z
        .string()
        .optional()
        .describe(`Only this section of the rules: ${Object.keys(SECTIONS).join(", ")}. Omit for everything.`),
      includeText: z.boolean().optional().describe("Include the full rules text (default true)"),
    },
    async (args) => {
      try {
        const { resolved, source } = resolveRequestedProfile(args.profile);
        const section = args.section?.trim().toLowerCase();
        if (section && !SECTIONS[section]) {
          throw new Error(`Раздел '${args.section}' не найден. Разделы: ${Object.keys(SECTIONS).join(", ")}`);
        }

        const fopPath = resolved.sharedParameterFile;
        const { checks, missingInFop } = checksWithGuids(resolved);
        const fop = fopPath && existsSync(fopPath) ? parseSharedParameterFile(fopPath) : null;
        const required = ((checks as any).requiredParameters?.parameters ?? []).map((p: any) => ({
          ...p,
          dataType: fop?.get(p.name)?.dataType,
          fopGroup: fop?.get(p.name)?.group,
        }));

        const automaticChecks = Object.entries(resolved.checks)
          .map(([check, spec]) => ({
            check,
            rule: (spec as any).rule,
            section: checkSection(check, spec),
            from: resolved.checkOrigin[check],
          }))
          .filter((item) => !section || item.section === section);
        const manualRules = resolved.manualRules.filter((rule) => !section || !rule.section || rule.section === section);

        const summary = {
          profile: resolved.profile.name,
          id: resolved.profile.id,
          version: resolved.profile.version,
          selectedBy: source,
          layers: describeLayers(resolved),
          section: section ? { key: section, title: SECTIONS[section] } : undefined,
          scope: resolved.profile.scope,
          activation: resolved.profile.activation,
          revitVersion: resolved.revitVersion,
          sharedParameterFile: fopPath,
          requiredParameters: !section || section === "parameters" ? required : undefined,
          missingInSharedParameterFile: missingInFop,
          conventions: resolved.conventions,
          automaticChecks,
          manualRules,
          // Эталоны: в профиле — относительно корня данных (REVIT_MCP_DATA_DIR), здесь — полные пути.
          examples: resolved.profile.examples
            ? Object.fromEntries(Object.entries(resolved.profile.examples).map(([kind, file]) => [kind, resolveDataPath(file)]))
            : undefined,
          dataRoot: dataRoot(),
        };
        const content: { type: "text"; text: string }[] = [{ type: "text", text: JSON.stringify(summary, null, 2) }];

        if (args.includeText !== false) {
          // Тексты всех слоёв, от общего к частному. Слой с одним файлом на все разделы
          // отдаётся целиком и при запросе раздела: разрезать его по разделам нечем.
          const texts = resolved.texts.filter((text) => !section || text.section === null || text.section === section);
          const existing = texts.filter((text) => existsSync(text.path));
          if (existing.length > 1) {
            content.push({
              type: "text",
              text: "Ниже тексты правил по слоям, от общего к частному. При расхождении действует более поздний (частный) слой.",
            });
          }
          for (const text of existing) {
            const title = text.section ? `${text.layerName} (${text.layer}) — ${SECTIONS[text.section] ?? text.section}` : `${text.layerName} (${text.layer})`;
            content.push({ type: "text", text: `<!-- слой: ${title} -->\n${readFileSync(text.path, "utf8").replace(/^﻿/, "")}` });
          }
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
