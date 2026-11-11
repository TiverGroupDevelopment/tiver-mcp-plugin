import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { familyNameField, sessionNote } from "../utils/familyTool.js";

type ExtractedImage = { data: string; mimeType: string };

// Достаёт imageBase64 из ответа плагина (на любой глубине) и убирает его из JSON,
// чтобы картинка ушла клиенту блоком image, а не строкой в тексте.
function extractImage(node: unknown, found: ExtractedImage[]): unknown {
  if (Array.isArray(node)) return node.map((item) => extractImage(item, found));
  if (node === null || typeof node !== "object") return node;

  const source = node as Record<string, unknown>;
  const result: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(source)) {
    if (key.toLowerCase() === "imagebase64" && typeof value === "string") {
      const mime = Object.entries(source).find(([k]) => k.toLowerCase() === "mimetype")?.[1];
      found.push({ data: value, mimeType: typeof mime === "string" ? mime : "image/png" });
      continue;
    }
    result[key] = extractImage(value, found);
  }
  return result;
}

export function registerCaptureFamilyViewTool(server: McpServer) {
  server.tool(
    "capture_family_view",
    `Render a family view to a PNG and return it as an image, to check the modelled geometry visually. Defaults to the first 3D view; pass a plan or elevation name for orthographic checks. Uses Revit image export, so it does not depend on the Revit window being visible. ${sessionNote}`,
    {
      familyName: familyNameField,
      view: z
        .string()
        .optional()
        .describe("View name (see get_family_geometry → views). Default: first 3D view"),
      pixels: z
        .number()
        .int()
        .min(256)
        .max(4096)
        .optional()
        .describe("Image width in pixels (default 1024)"),
    },
    async (args: Record<string, unknown>) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("capture_family_view", args);
        });

        const images: ExtractedImage[] = [];
        const stripped = extractImage(response, images);

        return {
          content: [
            ...images.map((image) => ({
              type: "image" as const,
              data: image.data,
              mimeType: image.mimeType,
            })),
            { type: "text" as const, text: JSON.stringify(stripped, null, 2) },
          ],
          isError: images.length === 0,
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text" as const,
              text: `capture_family_view failed: ${error instanceof Error ? error.message : String(error)}`,
            },
          ],
          isError: true,
        };
      }
    }
  );
}
