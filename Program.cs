using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpcodeExtractor;

public enum OutputFormat
{
    All = -1,
    FFXIV_ACT_Plugin,
    OverlayPlugin
}

public class Program
{
    static async Task<int> Main(string[] args)
    {
        var opcodeFileMapArgument = new Argument<FileInfo?>(
            name: "opcodeMapFile",
            description: "The opcode map to use.");
        var gameExecutableArgument = new Argument<FileInfo?>(
            name: "gameExecutable",
            description: "The game executable to map.");
        var dumpAllOpcodesArgument = new Argument<bool>(
            name: "dumpAllOpcodes",
            description: "Should all opcodes be dumped or just those specified in the map file. Default = \"False\"");
        var outputFormatArgument = new Argument<OutputFormat>(
            name: "outputFormat",
            description: "Which output format to use. Default = \"All\"");
        var inputMapKeyArgument = new Argument<string>(
            name: "inputMapKey",
            description: "Which opcode map key to use. Default = \"FFXIV_ACT_Plugin\"");
        dumpAllOpcodesArgument.SetDefaultValue(false);
        outputFormatArgument.SetDefaultValue(OutputFormat.All);
        inputMapKeyArgument.SetDefaultValue("FFXIV_ACT_Plugin");

        var rootCommand = new RootCommand("Map opcodes as defined in opcodeMapFile for executable gameExecutable");
        rootCommand.AddArgument(opcodeFileMapArgument);
        rootCommand.AddArgument(gameExecutableArgument);
        rootCommand.AddArgument(dumpAllOpcodesArgument);
        rootCommand.AddArgument(outputFormatArgument);
        rootCommand.AddArgument(inputMapKeyArgument);

        rootCommand.SetHandler((opcodeMapFile, gameExecutable, dumpAllOpcodes, outputFormat, inputMapKey) =>
            {
                var opcodes = ExtractOpcodes(opcodeMapFile!, gameExecutable!, dumpAllOpcodes, inputMapKey);
                OutputOpcodes(opcodes, outputFormat);
            },
            opcodeFileMapArgument, gameExecutableArgument, dumpAllOpcodesArgument, outputFormatArgument, inputMapKeyArgument);

        return await rootCommand.InvokeAsync(args);
    }

    private static void OutputOpcodes(Dictionary<int, string> opcodes, OutputFormat outputFormat)
    {
        if (outputFormat == OutputFormat.All || outputFormat == OutputFormat.FFXIV_ACT_Plugin)
        {
            OutputOpcodesForFFXIV_ACT_Plugin(opcodes);
        }
        if (outputFormat == OutputFormat.All || outputFormat == OutputFormat.OverlayPlugin)
        {
            OutputOpcodesForOverlayPlugin(opcodes);
        }
    }

    private static void OutputOpcodesForFFXIV_ACT_Plugin(Dictionary<int, string> opcodes)
    {
        foreach (var entry in opcodes)
        {
            Console.WriteLine($"{entry.Value}|{entry.Key:x}");
        }
    }

    private static void OutputOpcodesForOverlayPlugin(Dictionary<int, string> opcodes)
    {
        Dictionary<string, Dictionary<string, int>> overlayPluginMap = [];

        foreach (var entry in opcodes)
        {
            Dictionary<string, int> opEntry = new Dictionary<string, int>()
            {
                ["opcode"] = entry.Key,
                ["size"] = 0,
            };

            overlayPluginMap[entry.Value] = opEntry;
        }

        Console.WriteLine(JsonSerializer.Serialize(overlayPluginMap, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
    }

    /// <summary>
    /// Map opcodes as defined in opcodeMapFile for executable gameExecutable
    /// </summary>
    /// <param name="opcodeMapFile">The opcode map to use</param>
    /// <param name="gameExecutable">The game executable to map</param>
    /// <param name="dumpAllOpcodes">Whether to dump all opcodes, or just the mapped opcodes</param>
    public static Dictionary<int, string> ExtractOpcodes(FileInfo opcodeMapFile, FileInfo gameExecutable, bool dumpAllOpcodes, string inputMapKey)
    {
        var opcodeMapData = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(opcodeMapFile.FullName), new JsonSerializerOptions()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
        });
        if (opcodeMapData == null) return [];

        var opcodeMethod = opcodeMapData["method"]?.ToString() ?? "";

        byte[] gameData = File.ReadAllBytes(gameExecutable.FullName);
        switch (opcodeMethod)
        {
            case "vtable":
                return OpcodeExtractorVTable.Extract(opcodeMapData, gameData, dumpAllOpcodes, inputMapKey);
        }
        return [];
    }
}