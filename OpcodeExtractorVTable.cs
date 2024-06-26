using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpcodeExtractor;

public static class OpcodeExtractorVTable
{
    private static Regex WhitespaceRegex = new Regex("\\s");

    internal unsafe static Dictionary<int, string> Extract(JsonNode opcodeMapData, byte[] gameData, bool dumpAllOpcodes)
    {
        var signatureData = opcodeMapData["signature"]!;
        string signature = "";
        if (signatureData.GetValueKind() == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var sigLine in signatureData.AsArray())
            {
                signature += WhitespaceRegex.Replace(sigLine!.ToString(), "");
            }
        }
        else
        {
            signature = signatureData.ToString();
        }
        var matches = Common.SigScan(gameData, signature);
        if (matches.Count != 1)
        {
            Console.Error.WriteLine($"Invalid matches count {matches.Count} from SigScan");
            return [];
        }

        Dictionary<int, string> indexMap = [];

        var mapData = opcodeMapData["map"]!;
        if (mapData.GetValueKind() != System.Text.Json.JsonValueKind.Object)
        {
            Console.Error.WriteLine("Invalid data type for \"map\" in opcodes file");
            return [];
        }

        foreach (var entry in mapData.AsObject())
        {
            var entryIndex = (int)entry.Value!;
            indexMap[entryIndex] = entry.Key;
        }

        Console.WriteLine($"Scanning for opcode maps for {indexMap.Count} opcodes, dumping all: {dumpAllOpcodes}");

        var offset = matches[0];

        var opcodeMap = new Dictionary<int, string>();

        fixed (byte* ptr = gameData)
        {
            byte* funcPtr = ptr + offset;

            var switchTableOffset = *(sbyte*)(funcPtr + 15);
            var switchTableCount = *(int*)(funcPtr + 17);
            var defaultCaseAddr = offset + 23 + Common.ExtractRIPOffsetFromPtr(funcPtr + 23);
            var imageBaseOffset = offset + 30 + Common.ExtractRIPOffsetFromPtr(funcPtr + 30);
            var switchTableDataOffset = *(int*)(funcPtr + 40);
            var switchTableDataPtr = (int*)(ptr + imageBaseOffset + switchTableDataOffset);

            for (int i = 0; i <= switchTableCount; ++i)
            {
                var switchTableDataPtrValue = switchTableDataPtr[i];

                if (switchTableDataPtrValue + imageBaseOffset == defaultCaseAddr)
                    continue;
                var caseBodyPtr = ptr + imageBaseOffset + switchTableDataPtrValue;

                var opcode = i - switchTableOffset;
                var vfTableIndex = GetVFTableIndex(caseBodyPtr);

                if (indexMap.TryGetValue(vfTableIndex, out var name))
                {
                    opcodeMap[opcode] = name;
                    if (dumpAllOpcodes)
                    {
                        opcodeMap[opcode + 0x10000] = $"Index_{vfTableIndex}";
                    }
                }
                else if (dumpAllOpcodes)
                {
                    opcodeMap[opcode] = $"Index_{vfTableIndex}";
                }
            }
        }

        return opcodeMap;
    }

    private static unsafe int GetVFTableIndex(byte* caseBodyPtr)
    {
        int index;
        switch (caseBodyPtr[9])
        {
            // One-byte value
            case 0x60:
                index = *(caseBodyPtr + 10);
                break;
            // Four-byte value
            case 0xA0:
                index = *(int*)(caseBodyPtr + 10);
                break;
            default:
                return -1;
        }

        // Make sure we're divisible by 8
        if (index % 8 != 0)
            return -1;

        // First two VF table entries are constructor and destructor
        return (index / 8) - 2;
    }
}