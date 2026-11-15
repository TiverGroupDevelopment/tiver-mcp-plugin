import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerFamilyTool } from "../utils/familyTool.js";

export function registerExportFamilyLookupTablesTool(server: McpServer) {
  registerFamilyTool(
    server,
    "export_family_lookup_tables",
    "Batch-export the lookup tables (size tables) of family files to CSV: every .rfa in a folder and/or a list of files. Families are opened in the background and closed without saving (already open ones are read as they are); backup copies (*.0001.rfa) are skipped. One CSV per table, named after the table. Default output: <folder>/таблицы_выбора, encoding cp1251 (the encoding Revit 2019 imports).",
    {
      folder: z.string().optional().describe("Folder with .rfa files"),
      paths: z.array(z.string()).optional().describe("Individual .rfa files (in addition to or instead of folder)"),
      recursive: z.boolean().optional().describe("Include subfolders of folder (default false)"),
      exclude: z.array(z.string()).optional().describe("Skip files whose name contains any of these substrings, e.g. ['_инспекция']"),
      outDir: z.string().optional().describe("Output folder (default: <folder>/таблицы_выбора, or таблицы_выбора next to each file)"),
      encoding: z.enum(["cp1251", "utf8", "utf8bom"]).optional().describe("CSV encoding (default cp1251)"),
      overwrite: z.boolean().optional().describe("Replace existing CSV files (default true)"),
    }
  );
}
