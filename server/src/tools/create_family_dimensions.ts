import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  referenceSpec,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

const dimension = z.object({
  references: z
    .array(referenceSpec)
    .min(2)
    .describe("Parallel planes to dimension, in order. Two for a labelled dimension, three or more for EQ."),
  label: z
    .string()
    .optional()
    .describe(
      "Family parameter (Length) that drives this dimension. Only for two references. The geometry moves to match the current value of the parameter."
    ),
  equal: z.boolean().optional().describe("Make segments equal (EQ); needs 3+ references"),
  locked: z.boolean().optional().describe("Lock the dimension at its current value"),
  offset: z
    .number()
    .optional()
    .describe("Shift of the dimension line from the first reference within the view, mm (default 0)"),
  view: z.string().optional().describe("View to place the dimension in; chosen automatically when omitted"),
});

export function registerCreateFamilyDimensionsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_dimensions",
    `Create linear dimensions between reference planes, levels or planar form faces in a family, optionally labelled with a family parameter. This is what makes a family parametric: a labelled dimension between two reference planes lets the parameter move them. ${sessionNote}`,
    {
      familyName: familyNameField,
      dimensions: z.array(dimension).min(1),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
