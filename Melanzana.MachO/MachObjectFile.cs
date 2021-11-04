using Melanzana.Streams;
using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public class MachObjectFile
    {
        private readonly Stream stream;

        public MachObjectFile(Stream stream)
        {
            this.stream = stream;
        }

        public bool Is64Bit { get; set; }

        public bool IsLittleEndian { get; set; }

        public MachCpuType CpuType { get; set; }

        public uint CpuSubType { get; set; }

        public MachFileType FileType { get; set; }

        public MachHeaderFlags Flags { get; set; }

        public IList<MachLoadCommand> LoadCommands { get; } = new List<MachLoadCommand>();

        /// <summary>
        /// Get the lowest file offset of any section in the file. This allows calculating space that is
        /// reserved for adding new load commands (header pad).
        /// </summary>
        public ulong GetLowestSectionFileOffset()
        {
            ulong lowestFileOffset = (ulong)stream.Length;

            foreach (var segment in LoadCommands.OfType<MachSegment>())
            {
                foreach (var section in segment.Sections)
                {
                    if (section.Size != 0 &&
                        section.Type != MachSectionType.ZeroFill &&
                        section.Type != MachSectionType.GBZeroFill &&
                        section.Type != MachSectionType.ThreadLocalZeroFill &&
                        section.FileOffset < lowestFileOffset)
                    {
                        lowestFileOffset = section.FileOffset;
                    }
                }
            }

            return lowestFileOffset;
        }

        public ulong GetHeaderPad()
        {
            var lowestSectionFileOffset = GetLowestSectionFileOffset();
            return
                lowestSectionFileOffset -
                (ulong)(Is64Bit ? MachHeader64.BinarySize : MachHeader.BinarySize) -
                4 - // size of header magic
                (ulong)LoadCommands.Sum(c => c.GetCommandSize(this));
        }

        public ulong GetSize()
        {
            // Assume the size is the highest file offset+size of any segment
            return LoadCommands.OfType<MachSegment>().Max(s => s.FileOffset + s.FileSize);
        }

        public ulong GetSigningLimit()
        {
            var codeSignature = LoadCommands.OfType<MachCodeSignature>().FirstOrDefault();
            if (codeSignature != null)
            {
                // If code signature is present it has to be at the end of the file
                return codeSignature.FileOffset;
            }
            else
            {
                // If no code signature is present then we return the whole file size
                return GetSize();
            }
        }

        public Stream GetOriginalStream()
        {
            if (stream == null)
                return Stream.Null;

            return stream.Slice(0, stream.Length);
        }
    }
}