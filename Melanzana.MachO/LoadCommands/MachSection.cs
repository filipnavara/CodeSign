using System.ComponentModel;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSection
    {
        private Stream? dataStream;
        private ulong size;

        public MachSection()
        {
        }

        public MachSection(Stream stream)
        {
            dataStream = stream;
        }

        /// <summary>
        /// Gets or sets the name of this section.
        /// </summary>
        public string SectionName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the segment.
        /// </summary>
        /// <remarks>
        /// For fully linked executables or dynamic libraries this should always be the same as
        /// the name of the containing segment. However, intermediate object files
        /// (<see cref="MachFileType.Object"/>) use compact format where all sections are
        /// listed under single segment.
        /// </remarks>
        public string SegmentName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual address of this section.
        /// </summary>
        public ulong VirtualAddress { get; set; }

        public ulong Size
        {
            get => FileOffset != 0 && dataStream != null ? (ulong)dataStream.Length : size;
            set => size = value;
        }

        public uint FileOffset { get; set; }

        /// <summary>
        /// Gets or sets the alignment requirement of this section.
        /// </summary>
        public uint Log2Alignment { get; set; }

        /// <summary>
        /// Gets or sets the file offset to relocation entries of this section.
        /// </summary>
        public uint RelocationOffset { get; set; }

        /// <summary>
        /// Gets or sets the number of relocation entries of this section.
        /// </summary>
        public uint NumberOfReloationEntries { get; set; }

        internal uint Flags { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved1 { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved2 { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved3 { get; set; }

        public MachSectionAttributes Attributes
        {
            get => (MachSectionAttributes)(Flags & ~0xffu);
            set => Flags = (Flags & 0xffu) | (uint)value;
        }

        public MachSectionType Type
        {
            get => (MachSectionType)(Flags & 0xff);
            set => Flags = (Flags & ~0xffu) | (uint)value;
        }

        public bool IsInFile => Size > 0 && Type != MachSectionType.ZeroFill && Type != MachSectionType.GBZeroFill && Type != MachSectionType.ThreadLocalZeroFill;

        internal bool HasContentChanged { get; set; }

        public Stream GetReadStream()
        {
            if (Size == 0 || dataStream == null)
            {
                return Stream.Null;
            }

            return dataStream.Slice(0, (long)this.Size);
        }

        public Stream GetWriteStream()
        {
            HasContentChanged = true;
            dataStream = new UnclosableMemoryStream();
            return dataStream;
        }
    }
}