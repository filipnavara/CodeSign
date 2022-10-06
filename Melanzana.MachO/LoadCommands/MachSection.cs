using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
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
            get => dataStream != null ? (ulong)dataStream.Length : size;
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

        internal MachSection? RelocationSection { get; set; }

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

        public IEnumerable<MachRelocation> GetRelocationReader(MachObjectFile objectFile)
        {
            var relocationStream =
                RelocationSection?.GetReadStream() ??
                objectFile.GetStreamAtFileOffset(RelocationOffset, NumberOfReloationEntries * 8);
            var relocationBuffer = new byte[8];
            
            for (uint i = 0; i < NumberOfReloationEntries; i++)            
            {
                relocationStream.ReadFully(relocationBuffer);
                int address =
                    objectFile.IsLittleEndian ?
                    BinaryPrimitives.ReadInt32LittleEndian(relocationBuffer) :
                    BinaryPrimitives.ReadInt32BigEndian(relocationBuffer);
                uint info =
                    objectFile.IsLittleEndian ?
                    BinaryPrimitives.ReadUInt32LittleEndian(relocationBuffer.AsSpan(4)) :
                    BinaryPrimitives.ReadUInt32BigEndian(relocationBuffer.AsSpan(4));
                yield return new MachRelocation
                {
                    Address = address,
                    SymbolOrSectionIndex = info & 0xff_ff_ff,
                    IsPCRelative = (info & 0x1_00_00_00) > 0,
                    Length = ((info >> 25) & 3) switch { 0 => 1, 1 => 2, 2 => 4, _ => 8 },
                    IsExternal = (info & 0x8_00_00_00) > 0,
                    RelocationType = (MachRelocationType)(info >> 28)
                };
            }
        }

        public MachRelocationWriter GetRelocationWriter(MachObjectFile objectFile)
        {
            ArgumentNullException.ThrowIfNull(objectFile);

            // We currently only support writing relocations to object files
            // and only if the relocations are part of the unlinked section.
            if (objectFile.FileType != MachFileType.Object)
            {
                throw new NotImplementedException();
            }

            objectFile.EnsureUnlinkedSegmentExists();
            Debug.Assert(objectFile.UnlinkedSegment != null);

            if (RelocationSection == null)
            {
                if (NumberOfReloationEntries == 0)
                {
                    // Create new section
                    RelocationSection = new MachSection { Type = MachSectionType.Regular };
                    objectFile.UnlinkedSegment.Sections.Add(RelocationSection);
                }
                else
                {
                    // Find existing section
                    RelocationSection = objectFile.UnlinkedSegment.Sections.First(s => s.FileOffset == RelocationOffset);
                }
            }

            return new MachRelocationWriter(objectFile, this, RelocationSection);
        }

        internal void UpdateLayout(MachObjectFile objectFile)
        {
            if (RelocationSection != null)
            {
                this.RelocationOffset = RelocationSection.FileOffset;                
            }
        }
    }
}