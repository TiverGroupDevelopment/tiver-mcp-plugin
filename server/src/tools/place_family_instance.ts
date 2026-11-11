import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import {
  familyNameField,
  overwriteProjectValuesField,
  registerFamilyTool,
  sessionNote,
  vector3,
} from "../utils/familyTool.js";

export function registerPlaceFamilyInstanceTool(server: McpServer) {
  registerFamilyTool(
    server,
    "place_family_instance",
    `Place an instance of a nested family (loaded with load_nested_family) inside the family being edited. Returns the instance id and its parameters that can be driven by family parameters via associate_family_parameters. Hosted nested families need a host and are not supported here. ${sessionNote}`,
    {
      familyName: familyNameField,
      nestedFamily: z.string().describe("Name of the nested family"),
      type: z.string().optional().describe("Type name (default: first type)"),
      location: vector3.optional().describe("Insertion point [x, y, z], mm (default origin)"),
      rotation: z.number().optional().describe("Rotation about the vertical axis, degrees"),
      level: z.string().optional().describe("Level to place on, when the nested family requires one"),
      overwriteProjectValues: overwriteProjectValuesField,
    }
  );
}
