using System.Diagnostics;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSegment : MachLoadCommand
    {
        private Stream? dataStream;

        public MachSegment()
        {
        }

        public MachSegment(Stream stream)
        {
            dataStream = stream;
        }

        public ulong FileOffset { get; set; }

        internal ulong OriginalFileSize { get; set; }

        /// <remarks>
        /// We preserve the original FileSize when no editing on section contents was
        /// performed. ld64 aligns either to 16Kb or 4Kb page size based on compile time
        /// options. The __LINKEDIT segment is an exception that doesn't get aligned but
        /// since that one doesn't contain sections we don't do the special treatment.
        /// </remarks>
        public ulong FileSize
        {
            get
            {
                if (Sections.Count > 0)
                {
                    uint pageAligment = 0x4000 - 1;
                    if (Sections.Any(s => s.HasContentChanged))
                    {
                        return ((Sections.Where(s => s.IsInFile).Select(s => s.FileOffset + s.Size).Max() + pageAligment - 1) & ~(pageAligment - 1)) - FileOffset;
                    }
                    else
                    {
                        return OriginalFileSize;
                    }
                }

                return (ulong)(dataStream?.Length ?? 0);
            }
        }

        public string Name { get; set; } = string.Empty;

        public ulong Address { get; set; }

        public ulong Size { get; set; }

        public MachVmProtection MaximalProtection { get; set; }

        public MachVmProtection InitialProtection { get; set; }

        public uint Flags { get; set; }

        public IList<MachSection> Sections { get; } = new List<MachSection>();

        public Stream GetReadStream()
        {
            if (Sections.Count != 0)
            {
                throw new NotSupportedException("Segment can only be read directly if there are no sections");
            }

            if (FileSize == 0 || dataStream == null)
            {
                return Stream.Null;
            }

            return dataStream.Slice(0, (long)this.FileSize);
        }

        public Stream GetWriteStream()
        {
            if (Sections.Count != 0)
            {
                throw new NotSupportedException("Segment can only be written to directly if there are no sections");
            }

            dataStream = new UnclosableMemoryStream();
            return dataStream;
        }

        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile)
            => objectFile.Is64Bit ? MachLoadCommandType.Segment64 : MachLoadCommandType.Segment;

        public override int GetCommandSize(MachObjectFile objectFile)
        {
            if (objectFile.Is64Bit)
            {
                return LoadCommandHeader.BinarySize + Segment64Header.BinarySize + Sections.Count * Section64Header.BinarySize;
            }

            return LoadCommandHeader.BinarySize + SegmentHeader.BinarySize + Sections.Count * SectionHeader.BinarySize;
        }
    }
}