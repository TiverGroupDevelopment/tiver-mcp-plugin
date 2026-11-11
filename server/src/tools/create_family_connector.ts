import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
} from "../utils/familyTool.js";

export function registerCreateFamilyConnectorTool(server: McpServer) {
  registerFamilyTool(
    server,
    "create_family_connector",
    `Add an MEP connector (duct, pipe, electrical, conduit, cable tray) on a planar face of a form in a family. The family category must support connectors (mechanical equipment, pipe fittings, electrical fixtures and so on). Returns the connector parameters that can be driven by family parameters via associate_family_parameters. ${sessionNote}`,
    {
      familyName: familyNameField,
      domain: z.enum(["duct", "pipe", "electrical", "conduit", "cabletray"]),
      face: z
        .object({
          elementId: z.number().int().describe("Form id"),
          faceIndex: z.number().int().describe("Planar face index from get_family_geometry with detail: true"),
        })
        .describe("The face that carries the connector"),
      systemType: z
        .string()
        .optional()
        .describe(
          "Duct: SupplyAir, ReturnAir, ExhaustAir, OtherAir, Global, Fitting. Pipe: SupplyHydronic, ReturnHydronic, Sanitary, DomesticColdWater, DomesticHotWater, Global, Fitting and others. Electrical: PowerBalanced, PowerUnBalanced, Data, Communication and others. Default: Global (duct, pipe), PowerBalanced (electrical)."
        ),
      shape: z.enum(["Round", "Rectangular", "Oval"]).optional().describe("Duct connector profile (default Round)"),
      diameter: z.number().positive().optional().describe("Diameter for round connectors, mm"),
      width: z.number().positive().optional().describe("Width for rectangular or oval connectors, mm"),
      height: z.number().positive().optional().describe("Height for rectangular or oval connectors, mm"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
