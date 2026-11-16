using System.Reflection;
using System.Runtime.InteropServices;

// General information about the assembly.
[assembly: AssemblyTitle("tiver-mcp-plugin")]
[assembly: AssemblyDescription("Revit add-in: MCP bridge between the agent and the Revit API")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("TiverGroup")]
[assembly: AssemblyProduct("tiver-mcp-plugin")]
[assembly: AssemblyCopyright("Copyright © TiverGroup 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Types in this assembly are not visible to COM components.
[assembly: ComVisible(false)]

// The GUID of the typelib if this project is exposed to COM.
[assembly: Guid("43cd0fd7-df41-4f64-92be-a0f78666d86f")]

// Version: Major.Minor.Build.Revision (scripts/release.ps1 updates it).
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

#if NET5_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]
#endif