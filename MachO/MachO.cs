using CodeSign.MachO.Commands;
using CodeSign.Streams;

namespace CodeSign.MachO
{
    public class MachO
    {
        private readonly Stream stream;
        private readonly bool isLittleEndian;

        public MachO(FatArchHeader? fatArchHeader, IMachHeader machHeader, bool isLittleEndian, Stream stream)
        {
            FatArchHeader = fatArchHeader;
            Header = machHeader;
            this.isLittleEndian = isLittleEndian;
            this.stream = stream;
        }

        public FatArchHeader? FatArchHeader { get; private set; }

        public IMachHeader Header { get; private set; }

        public IList<LoadCommand> LoadCommands { get; } = new List<LoadCommand>();

        /// <summary>
        /// Get the lowest file offset of any section in the file. This allows calculating space that is
        /// reserved for adding new load commands (header pad).
        /// </summary>
        public ulong GetLowestSectionFileOffset()
        {
            ulong lowestFileOffset = (ulong)stream.Length;

            foreach (var loadCommand in LoadCommands)
            {
                if (loadCommand is Segment segment)
                {
                    foreach (var section in segment.Sections)
                    {
                        if (section.Size != 0 &&
                            section.Type != SectionType.ZeroFill &&
                            section.Type != SectionType.GBZeroFill &&
                            section.Type != SectionType.ThreadLocalZeroFill &&
                            section.FileOffset < lowestFileOffset)
                        {
                            lowestFileOffset = section.FileOffset;
                        }
                    }
                }
                else if (loadCommand is Segment64 segment64)
                {
                    foreach (var section in segment64.Sections)
                    {
                        if (section.Size != 0 &&
                            section.Type != SectionType.ZeroFill &&
                            section.Type != SectionType.GBZeroFill &&
                            section.Type != SectionType.ThreadLocalZeroFill &&
                            section.FileOffset < lowestFileOffset)
                        {
                            lowestFileOffset = section.FileOffset;
                        }
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
                (ulong)((Header is MachHeader64) ? MachHeader64.BinarySize : MachHeader.BinarySize) -
                Header.SizeOfCommands;
        }

        public ulong GetSigningLimit()
        {
            var codeSignature = LoadCommands.OfType<LinkEdit>().FirstOrDefault(c => c.Header.CommandType == LoadCommandType.CodeSignature);
            if (codeSignature != null)
            {
                // If code signature is present it has to be at the end of the file
                return codeSignature.LinkEditHeader.FileOffset;
            }
            else
            {
                // If no code signature is present then we return the whole file size
                return (ulong)stream.Length;
            }
        }

        public Stream GetStream()
        {
            return stream.Slice(0, stream.Length);
        }
    }
}