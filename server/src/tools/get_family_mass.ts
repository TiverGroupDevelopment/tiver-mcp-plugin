import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, overwriteProjectValuesField, registerFamilyTool, sessionNote } from "../utils/familyTool.js";

export function registerGetFamilyMassTool(server: McpServer) {
  registerFamilyTool(
    server,
    "get_family_mass",
    `Mass of the family: volume of every solid form times the density of its material (physical asset, set with create_family_materials densityKgM3) or densityKgM3. Use it to check a model against the drawing weight. With writeParameter the result is written to a Number family parameter in grams or kilograms (current type, or every type with allTypes). ${sessionNote}`,
    {
      familyName: familyNameField,
      densityKgM3: z.number().positive().optional().describe("Density for all forms, kg/m³ (overrides materials)"),
      writeParameter: z.string().optional().describe("Number family parameter to write the mass to"),
      unit: z.enum(["g", "kg"]).optional().describe("Unit of the written value (default g)"),
      allTypes: z.boolean().optional().describe("With writeParameter: compute and write for every family type"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
