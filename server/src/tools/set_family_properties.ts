import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerSetFamilyPropertiesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "set_family_properties",
    `Change family-level properties (Family Category and Parameters dialog). Some properties are read-only for certain categories or templates; the tool says which. Hosting cannot be changed: it is fixed by the template. ${sessionNote}`,
    {
      familyName: familyNameField,
      category: z
        .string()
        .optional()
        .describe("Family category: name as shown in Revit, or BuiltInCategory such as OST_GenericModel"),
      workPlaneBased: z.boolean().optional().describe("Work Plane-Based"),
      alwaysVertical: z.boolean().optional().describe("Always vertical"),
      shared: z.boolean().optional().describe("Shared (visible and schedulable when nested)"),
      roomCalculationPoint: z.boolean().optional().describe("Room Calculation Point"),
      cutWithVoidsWhenLoaded: z.boolean().optional().describe("Cut with Voids When Loaded"),
      omniClassNumber: z.string().optional().describe("OmniClass Number"),
      omniClassTitle: z.string().optional().describe("OmniClass Title"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
