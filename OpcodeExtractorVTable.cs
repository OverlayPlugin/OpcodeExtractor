using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpcodeExtractor;

public static class OpcodeExtractorVTable
{
    private static Regex WhitespaceRegex = new Regex("\\s");

    internal unsafe static Dictionary<int, string> Extract(JsonNode opcodeMapData, byte[] gameData, bool dumpAllOpcodes, string inputMapKey)
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

        var mapData = opcodeMapData[inputMapKey]!;
        if (mapData == null || mapData.GetValueKind() != System.Text.Json.JsonValueKind.Object)
        {
            Console.Error.WriteLine("Invalid data type for \"map\" in opcodes file");
            return [];
        }

        foreach (var entry in mapData.AsObject())
        {
            var entryIndex = (int)entry.Value!;
            indexMap[entryIndex] = entry.Key;
        }

        if (!ReadJSONInt(opcodeMapData, "switchTableOffset_offset", out var switchTableOffset_offset)) return [];
        if (!ReadJSONInt(opcodeMapData, "switchTableCount_offset", out var switchTableCount_offset)) return [];
        if (!ReadJSONInt(opcodeMapData, "expectedSwitchTableCount", out var expectedSwitchTableCount)) return [];
        if (!ReadJSONInt(opcodeMapData, "defaultCaseAddr_offset", out var defaultCaseAddr_offset)) return [];
        if (!ReadJSONInt(opcodeMapData, "imageBaseOffset_offset", out var imageBaseOffset_offset)) return [];
        if (!ReadJSONInt(opcodeMapData, "switchTableDataOffset_offset", out var switchTableDataOffset_offset)) return [];

        Console.WriteLine($"Scanning for opcode maps for {indexMap.Count} opcodes, dumping all: {dumpAllOpcodes}");

        var offset = matches[0];

        var opcodeMap = new Dictionary<int, string>();

        fixed (byte* ptr = gameData)
        {
            byte* funcPtr = ptr + offset;

            var switchTableOffset = *(sbyte*)(funcPtr + switchTableOffset_offset);
            var switchTableCount = *(int*)(funcPtr + switchTableCount_offset);
            Console.WriteLine($"Expected {expectedSwitchTableCount} entries, found {switchTableCount}");
            if (expectedSwitchTableCount != switchTableCount)
            {
                Console.WriteLine("Switch table count mismatch, press any key to continue anyways");
                Console.ReadKey(true);
            }
            var defaultCaseAddr = offset + defaultCaseAddr_offset + Common.ExtractRIPOffsetFromPtr(funcPtr + defaultCaseAddr_offset);
            var imageBaseOffset = offset + imageBaseOffset_offset + Common.ExtractRIPOffsetFromPtr(funcPtr + imageBaseOffset_offset);
            var switchTableDataOffset = *(int*)(funcPtr + switchTableDataOffset_offset);
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

    private static bool ReadJSONInt(JsonNode opcodeMapData, string key, out int value)
    {
        var mapData = opcodeMapData[key]!;
        if (mapData.GetValueKind() != System.Text.Json.JsonValueKind.Number)
        {
            Console.Error.WriteLine($"Invalid data type for \"${key}\" in opcodes file");
            value = 0;
            return false;
        }

        value = mapData.GetValue<int>();
        return true;
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