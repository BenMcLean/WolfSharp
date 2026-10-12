namespace BenMcLean.Wolf3D.MusicTool;

internal static class Op2ProgramCopier
{
	public static IReadOnlyList<string> Copy(string srcOp2Path, string dstOp2Path, string mapText)
	{
		Op2Bank source = Op2Bank.Load(srcOp2Path);
		Op2Bank destination = Op2Bank.Load(dstOp2Path);
		List<(int DestinationProgram, int SourceProgram)> mappings = ParseMappings(mapText);
		List<string> lines = [];

		foreach ((int destinationProgram, int sourceProgram) in mappings)
		{
			int dstIndex = destinationProgram - 1;
			int srcIndex = sourceProgram - 1;
			ValidateMelodicProgram(destinationProgram, dstIndex);
			ValidateMelodicProgram(sourceProgram, srcIndex);

			destination.Patches[dstIndex] = source.Patches[srcIndex];
			destination.Names[dstIndex] = source.Names[srcIndex];
			lines.Add($"Copied source program {sourceProgram:D3} -> destination program {destinationProgram:D3}.");
		}

		destination.Save(dstOp2Path);
		lines.Add($"Wrote {Path.GetFullPath(dstOp2Path)}");
		return lines;
	}

	private static List<(int DestinationProgram, int SourceProgram)> ParseMappings(string mapText)
	{
		List<(int DestinationProgram, int SourceProgram)> mappings = [];
		foreach (string rawEntry in mapText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			string[] parts = rawEntry.Split('=', StringSplitOptions.TrimEntries);
			int destinationProgram;
			int sourceProgram;
			if (parts.Length == 1)
			{
				destinationProgram = ParseProgram(parts[0]);
				sourceProgram = destinationProgram;
			}
			else if (parts.Length == 2)
			{
				destinationProgram = ParseProgram(parts[0]);
				sourceProgram = ParseProgram(parts[1]);
			}
			else
			{
				throw new ArgumentException($"Invalid mapping '{rawEntry}'.");
			}

			mappings.Add((destinationProgram, sourceProgram));
		}

		return mappings;
	}

	private static int ParseProgram(string text)
	{
		if (!int.TryParse(text, out int program))
			throw new ArgumentException($"Invalid GM program '{text}'.");
		return program;
	}

	private static void ValidateMelodicProgram(int program, int index)
	{
		if (index < 0 || index >= Op2Bank.MelodicCount)
			throw new ArgumentOutOfRangeException(nameof(program), $"Program {program:D3} is outside the melodic OP2 range.");
	}
}
