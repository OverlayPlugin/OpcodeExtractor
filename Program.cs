using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpcodeExtractor;

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
            description: "The opcode map to use.");
        dumpAllOpcodesArgument.SetDefaultValue(false);

        var rootCommand = new RootCommand("Map opcodes as defined in opcodeMapFile for executable gameExecutable");
        rootCommand.AddArgument(opcodeFileMapArgument);
        rootCommand.AddArgument(gameExecutableArgument);
        rootCommand.AddArgument(dumpAllOpcodesArgument);

        rootCommand.SetHandler((opcodeMapFile, gameExecutable, dumpAllOpcodes) =>
            {
                ExtractOpcodes(opcodeMapFile!, gameExecutable!, dumpAllOpcodes);
            },
            opcodeFileMapArgument, gameExecutableArgument, dumpAllOpcodesArgument);

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Map opcodes as defined in opcodeMapFile for executable gameExecutable
    /// </summary>
    /// <param name="opcodeMapFile">The opcode map to use</param>
    /// <param name="gameExecutable">The game executable to map</param>
    /// <param name="dumpAllOpcodes">Whether to dump all opcodes, or just the mapped opcodes</param>
    public static void ExtractOpcodes(FileInfo opcodeMapFile, FileInfo gameExecutable, bool dumpAllOpcodes)
    {
        var opcodeMapData = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(opcodeMapFile.FullName), new JsonSerializerOptions()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
        });
        if (opcodeMapData == null) return;

        var opcodeMethod = opcodeMapData["method"]?.ToString() ?? "";

        byte[] gameData = File.ReadAllBytes(gameExecutable.FullName);
        switch (opcodeMethod)
        {
            case "vtable":
                OpcodeExtractorVTable.Extract(opcodeMapData, gameData, dumpAllOpcodes);
                break;
        }
    }
}