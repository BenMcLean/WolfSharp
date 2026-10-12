namespace BenMcLean.Wolf3D.MusicTool;

internal static class MidiOp2Comparer
{
	public static IReadOnlyList<string> BuildReport(string midiPath, string op2Path, string referenceOp2Path)
	{
		MidiProgramUsage midiPrograms = MidiProgramInspector.Inspect(midiPath);
		IReadOnlyList<int> percussionNotes = MidiProgramInspector.InspectPercussionNotes(midiPath);
		Op2Bank currentBank = Op2Bank.Load(op2Path);
		Op2Bank referenceBank = Op2Bank.Load(referenceOp2Path);
		int[] referencePrograms = [.. Enumerable.Range(0, Op2Bank.MelodicCount)
			.Where(index => !referenceBank.Patches[index].IsSilent())
			.Select(index => index + 1)];

		List<string> lines =
		[
			$"MIDI melodic GM programs in use: {FormatPrograms(midiPrograms.Programs)}",
			$"MIDI percussion notes in use: {FormatPrograms(percussionNotes)}",
			$"Reference melodic programs: {FormatPrograms(referencePrograms)}",
			string.Empty
		];

		int[] missingReferencePrograms = [.. referencePrograms.Except(midiPrograms.Programs).OrderBy(program => program)];
		if (missingReferencePrograms.Length > 0)
		{
			lines.Add($"Missing original melodic programs from this MIDI: {FormatPrograms(missingReferencePrograms)}");
			lines.Add(string.Empty);
		}

		foreach (int program in midiPrograms.Programs)
		{
			int bankIndex = program - 1;
			string channels = midiPrograms.ProgramChannels.TryGetValue(program, out IReadOnlyList<int>? usedChannels)
				? string.Join(", ", usedChannels)
				: "unknown";

			if (bankIndex < 0 || bankIndex >= Op2Bank.MelodicCount)
			{
				lines.Add($"Program {program:D3} on MIDI channels {channels}: OUT OF RANGE for melodic OP2.");
				continue;
			}

			Op2Patch currentPatch = currentBank.Patches[bankIndex];
			if (currentPatch.IsSilent())
			{
				lines.Add($"Program {program:D3} on MIDI channels {channels}: CURRENT OP2 SLOT IS SILENT.");
				continue;
			}

			Op2Patch sameProgramReference = referenceBank.Patches[bankIndex];
			if (!sameProgramReference.IsSilent() && PatchesEqual(currentPatch, sameProgramReference))
			{
				lines.Add($"Program {program:D3} on MIDI channels {channels}: EXACT SAME as original program {program:D3}.");
				continue;
			}

			int? equivalentReferenceProgram = referencePrograms
				.FirstOrDefault(referenceProgram => PatchesEqual(currentPatch, referenceBank.Patches[referenceProgram - 1]));
			if (equivalentReferenceProgram is not 0)
			{
				lines.Add($"Program {program:D3} on MIDI channels {channels}: EXACT PATCH MATCH, but it matches original program {equivalentReferenceProgram:D3} instead.");
				continue;
			}

			if (!sameProgramReference.IsSilent())
			{
				lines.Add($"Program {program:D3} on MIDI channels {channels}: DIFFERENT from original program {program:D3}.");
				lines.Add($"  Changed fields: {DescribeDifferences(currentPatch, sameProgramReference)}");
			}
			else
			{
				lines.Add($"Program {program:D3} on MIDI channels {channels}: NO ORIGINAL WONDERIN slot for this program, and it does not exactly match any original melodic patch.");
			}
		}

		return lines;
	}

	private static string FormatPrograms(IEnumerable<int> values)
	{
		int[] array = [.. values];
		return array.Length == 0
			? "(none)"
			: string.Join(", ", array.Select(value => value.ToString("D3")));
	}

	private static bool PatchesEqual(Op2Patch left, Op2Patch right) =>
		left.Flags == right.Flags &&
		left.FineTune == right.FineTune &&
		left.NoteNumber == right.NoteNumber &&
		VoicesEqual(left.Voice1, right.Voice1) &&
		VoicesEqual(left.Voice2, right.Voice2);

	private static bool VoicesEqual(Op2Voice left, Op2Voice right) =>
		left.ModChar == right.ModChar &&
		left.ModAttack == right.ModAttack &&
		left.ModSustain == right.ModSustain &&
		left.ModWave == right.ModWave &&
		left.ModScale == right.ModScale &&
		left.ModLevel == right.ModLevel &&
		left.Feedback == right.Feedback &&
		left.CarChar == right.CarChar &&
		left.CarAttack == right.CarAttack &&
		left.CarSustain == right.CarSustain &&
		left.CarWave == right.CarWave &&
		left.CarScale == right.CarScale &&
		left.CarLevel == right.CarLevel &&
		left.Reserved == right.Reserved &&
		left.NoteOffset == right.NoteOffset;

	private static string DescribeDifferences(Op2Patch current, Op2Patch reference)
	{
		List<string> parts = [];
		AppendDifference(parts, "Flags", current.Flags, reference.Flags);
		AppendDifference(parts, "FineTune", current.FineTune, reference.FineTune);
		AppendDifference(parts, "NoteNumber", current.NoteNumber, reference.NoteNumber);
		AppendDifference(parts, "Voice1.ModChar", current.Voice1.ModChar, reference.Voice1.ModChar);
		AppendDifference(parts, "Voice1.ModAttack", current.Voice1.ModAttack, reference.Voice1.ModAttack);
		AppendDifference(parts, "Voice1.ModSustain", current.Voice1.ModSustain, reference.Voice1.ModSustain);
		AppendDifference(parts, "Voice1.ModWave", current.Voice1.ModWave, reference.Voice1.ModWave);
		AppendDifference(parts, "Voice1.ModScale", current.Voice1.ModScale, reference.Voice1.ModScale);
		AppendDifference(parts, "Voice1.ModLevel", current.Voice1.ModLevel, reference.Voice1.ModLevel);
		AppendDifference(parts, "Voice1.Feedback", current.Voice1.Feedback, reference.Voice1.Feedback);
		AppendDifference(parts, "Voice1.CarChar", current.Voice1.CarChar, reference.Voice1.CarChar);
		AppendDifference(parts, "Voice1.CarAttack", current.Voice1.CarAttack, reference.Voice1.CarAttack);
		AppendDifference(parts, "Voice1.CarSustain", current.Voice1.CarSustain, reference.Voice1.CarSustain);
		AppendDifference(parts, "Voice1.CarWave", current.Voice1.CarWave, reference.Voice1.CarWave);
		AppendDifference(parts, "Voice1.CarScale", current.Voice1.CarScale, reference.Voice1.CarScale);
		AppendDifference(parts, "Voice1.CarLevel", current.Voice1.CarLevel, reference.Voice1.CarLevel);
		AppendDifference(parts, "Voice1.Reserved", current.Voice1.Reserved, reference.Voice1.Reserved);
		AppendDifference(parts, "Voice1.NoteOffset", current.Voice1.NoteOffset, reference.Voice1.NoteOffset);
		AppendDifference(parts, "Voice2.ModChar", current.Voice2.ModChar, reference.Voice2.ModChar);
		AppendDifference(parts, "Voice2.ModAttack", current.Voice2.ModAttack, reference.Voice2.ModAttack);
		AppendDifference(parts, "Voice2.ModSustain", current.Voice2.ModSustain, reference.Voice2.ModSustain);
		AppendDifference(parts, "Voice2.ModWave", current.Voice2.ModWave, reference.Voice2.ModWave);
		AppendDifference(parts, "Voice2.ModScale", current.Voice2.ModScale, reference.Voice2.ModScale);
		AppendDifference(parts, "Voice2.ModLevel", current.Voice2.ModLevel, reference.Voice2.ModLevel);
		AppendDifference(parts, "Voice2.Feedback", current.Voice2.Feedback, reference.Voice2.Feedback);
		AppendDifference(parts, "Voice2.CarChar", current.Voice2.CarChar, reference.Voice2.CarChar);
		AppendDifference(parts, "Voice2.CarAttack", current.Voice2.CarAttack, reference.Voice2.CarAttack);
		AppendDifference(parts, "Voice2.CarSustain", current.Voice2.CarSustain, reference.Voice2.CarSustain);
		AppendDifference(parts, "Voice2.CarWave", current.Voice2.CarWave, reference.Voice2.CarWave);
		AppendDifference(parts, "Voice2.CarScale", current.Voice2.CarScale, reference.Voice2.CarScale);
		AppendDifference(parts, "Voice2.CarLevel", current.Voice2.CarLevel, reference.Voice2.CarLevel);
		AppendDifference(parts, "Voice2.Reserved", current.Voice2.Reserved, reference.Voice2.Reserved);
		AppendDifference(parts, "Voice2.NoteOffset", current.Voice2.NoteOffset, reference.Voice2.NoteOffset);

		return parts.Count == 0 ? "(no byte differences)" : string.Join(", ", parts);
	}

	private static void AppendDifference<T>(List<string> parts, string name, T current, T reference)
		where T : struct
	{
		if (EqualityComparer<T>.Default.Equals(current, reference))
			return;
		parts.Add($"{name}={current} (original {reference})");
	}
}
