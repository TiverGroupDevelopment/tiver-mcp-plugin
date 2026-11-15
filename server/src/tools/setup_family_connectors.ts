import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { familyNameField, overwriteProjectValuesField, registerFamilyTool, sessionNote, vector3 } from "../utils/familyTool.js";

export function registerSetupFamilyConnectorsTool(server: McpServer) {
  registerFamilyTool(
    server,
    "setup_family_connectors",
    `Set up MEP connectors of a family after create_family_connector: link connectors in pairs so the flow passes through the part (two connectors are linked automatically), assign the primary connector, set the system classification (e.g. Fitting for fittings and valves), flip directions, and rotate every connector about its own axis so its Y axis points up (+Z), which keeps the part from twisting around the pipe on insertion. Returns each connector's direction, Y axis, link and classification. ${sessionNote}`,
    {
      familyName: familyNameField,
      link: z
        .array(z.tuple([z.number().int(), z.number().int()]))
        .optional()
        .describe("Pairs of connector ids to link, e.g. [[101, 102]]. Default: the only two connectors"),
      autoLink: z.boolean().optional().describe("Link the two connectors when there are exactly two and link is not given (default true)"),
      primary: z.number().int().optional().describe("Connector id to make primary (default: first of the first pair)"),
      systemClassification: z
        .string()
        .optional()
        .describe("System classification for all connectors: Fitting (fittings, valves), Global, DomesticColdWater, SupplyAir, ..."),
      flip: z.array(z.number().int()).optional().describe("Connector ids whose direction to flip"),
      orientUp: z.boolean().optional().describe("Rotate connectors so their Y axis points up (default true)"),
      up: vector3.optional().describe("'Up' direction for orientUp (default [0, 0, 1])"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
