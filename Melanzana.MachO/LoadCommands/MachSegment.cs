using Melanzana.Streams;

namespace Melanzana.MachO
{
    /// <summary>
    /// Defines a segment in the object file.
    /// <summary>
    /// <remarks>
    /// Segments represent how parts of the object file are mapped into virtual memory.
    ///
    /// Segment may contain zero or more sections. Some segments have special properties by
    /// convention:
    ///
    /// - The `__TEXT` segment has to start at file offset zero and it contains the file
    ///   header and load commands. These metadata are not part of any of the segment's
    ///   sections though.
    /// - The `__LINKEDIT` segment needs to be the last segment in the file. It should not
    ///   contain any sections. It's contents are mapped by other load commands, such as
    ///   symbol table, function starts and code signature (collectively derived from
    ///   <see cref="MachLinkEdit"/> class).
    /// </remarks>
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

        /// <summary>
        /// Gets the position of the segement in the object file.
        /// </summary>
        /// <remarks>
        /// The position is relative to the beginning of the architecture-specific object
        /// file. In fat binaries it needs to be adjusted to account for the envelope.
        /// </remarks>
        public ulong FileOffset { get; set; }

        internal ulong OriginalFileSize { get; set; }

        /// <summary>
        /// Gets the size of the segement in the file.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the name of this segment.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual address of this section.
        /// </summary>
        public ulong VirtualAddress { get; set; }

        /// <summary>
        /// Gets or sets the size in bytes occupied in memory by this segment.
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// Gets or sets the maximum permitted protection of this segment.
        /// <summary>
        public MachVmProtection MaximumProtection { get; set; }

        /// <summary>
        /// Gets or sets the initial protection of this segment.
        /// <summary>
        public MachVmProtection InitialProtection { get; set; }

        public MachSegmentFlags Flags { get; set; }

        /// <summary>
        /// List of sections contained in this segment.
        /// <summary>
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

        /// <summary>
        /// Gets the stream for updating the contents of this segment if it has no sections.
        /// <summary>
        /// <remarks>
        /// This method is primarily useful for the `__LINKEDIT` segment. The other primary
        /// segments (`__TEXT`, `__DATA`) are divided into sections and each section has to
        /// be updated individually.
        /// </remarks>
        public Stream GetWriteStream()
        {
            if (Sections.Count != 0)
            {
                throw new NotSupportedException("Segment can only be written to directly if there are no sections");
            }

            dataStream = new UnclosableMemoryStream();
            return dataStream;
        }
    }
}