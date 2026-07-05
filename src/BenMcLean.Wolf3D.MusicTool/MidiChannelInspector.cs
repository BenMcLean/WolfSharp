using BenMcLean.Wolf3D.Assets.Sound;

namespace BenMcLean.Wolf3D.MusicTool;

internal static class MidiChannelInspector
{
	public static IReadOnlyList<string> BuildReport(string midiPath)
	{
		using FileStream stream = File.OpenRead(midiPath);
		Midi midi = Midi.Parse(stream);

		ChannelStats[] channels = Enumerable.Range(0, 16).Select(_ => new ChannelStats()).ToArray();
		HashSet<int> drumNotes = [];
		int eventIndex = 0;

		foreach (Midi.MidiEvent midiEvent in midi.Events)
		{
			eventIndex++;
			switch (midiEvent)
			{
				case Midi.ProgramChangeEvent programChange:
					channels[programChange.Channel].Programs.Add(programChange.Program + 1);
					break;
				case Midi.NoteOnEvent noteOn when noteOn.Velocity > 0:
					if (noteOn.Channel == 9)
					{
						drumNotes.Add(noteOn.Note);
						break;
					}

					ChannelStats stats = channels[noteOn.Channel];
					stats.NoteCount++;
					stats.MinNote = Math.Min(stats.MinNote, noteOn.Note);
					stats.MaxNote = Math.Max(stats.MaxNote, noteOn.Note);
					if (stats.FirstNoteEventIndex == 0)
						stats.FirstNoteEventIndex = eventIndex;
					break;
			}
		}

		List<string> lines = [];
		for (int channel = 0; channel < 16; channel++)
		{
			if (channel == 9)
				continue;

			ChannelStats stats = channels[channel];
			if (stats.NoteCount == 0)
				continue;

			string programs = stats.Programs.Count == 0
				? "(default 001)"
				: string.Join(", ", stats.Programs.OrderBy(program => program).Select(program => program.ToString("D3")));
			lines.Add(
				$"MIDI channel {channel + 1:D2}: programs {programs}; note-ons {stats.NoteCount}; range {stats.MinNote}-{stats.MaxNote}; first note event {stats.FirstNoteEventIndex}");
		}

		lines.Add($"Drum notes on MIDI channel 10: {(drumNotes.Count == 0 ? "(none)" : string.Join(", ", drumNotes.OrderBy(note => note).Select(note => note.ToString("D3"))))}");
		return lines;
	}

	private sealed class ChannelStats
	{
		public HashSet<int> Programs { get; } = [];
		public int NoteCount { get; set; }
		public int MinNote { get; set; } = int.MaxValue;
		public int MaxNote { get; set; } = int.MinValue;
		public int FirstNoteEventIndex { get; set; }
	}
}
