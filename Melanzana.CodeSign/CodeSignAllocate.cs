using System.Buffers.Binary;
using System.Diagnostics;
using Melanzana.MachO;
using Melanzana.MachO.Commands;

namespace Melanzana.CodeSign
{
    /// <summary>
    /// Rewriter for the Mach-O or universal binaries that resizes the code signature section
    /// and all related linker commands to make space for new signature.
    /// </summary>
    public class CodeSignAllocate
    {
        public IList<MachObjectFile> input;
        public string outputFile;
        public Dictionary<MachObjectFile, uint> codeSignatureSize = new();

        public CodeSignAllocate(IList<MachObjectFile> input, string outputFile)
        {
            this.input = input;
            this.outputFile = outputFile;
        }

        public void SetArchSize(MachObjectFile machO, uint codeSignatureSize)
        {
            // Page alignment
            codeSignatureSize = (codeSignatureSize + 0xfffu) & ~0xfffu;
            this.codeSignatureSize[machO] = codeSignatureSize;
        }

        public void Allocate()
        {
            using var output = File.OpenWrite(this.outputFile);

            // We try to output the same general structure as the original file. The FAT header
            // is preserved even if a single architecture was used. Unfortunately we don't keep
            // the information about the original endianness of the headers and hence we take
            // a shortcut here and always write it in Big Endian.
            if (this.input.Count > 1 || this.input[0].FatArchHeader != null)
            {
                var fatMagic = new byte[4];
                var fatHeader = new FatHeader { NumberOfFatArchitectures = (uint)input.Count };
                var fatHeaderBytes = new byte[FatHeader.BinarySize];
                var fatArchHeaderBytes = new byte[FatArchHeader.BinarySize];

                BinaryPrimitives.WriteUInt32BigEndian(fatMagic, (uint)MachMagic.FatMagicBigEndian);
                fatHeader.Write(fatHeaderBytes, isLittleEndian: false, out var _);
                output.Write(fatMagic);
                output.Write(fatHeaderBytes);

                uint offset = (uint)(FatHeader.BinarySize + input.Count * FatArchHeader.BinarySize);
                foreach (var machO in input)
                {
                    Debug.Assert(machO.FatArchHeader != null);

                    // TODO: Improve the size calculation to be more resilient
                    uint size = (uint)machO.GetSigningLimit();
                    if (this.codeSignatureSize.TryGetValue(machO, out var codeSignatureSize))
                        size += codeSignatureSize;

                    size += (uint)(1 << (int)machO.FatArchHeader.Alignment) - 1;
                    size -= size & ~((uint)(1 << (int)machO.FatArchHeader.Alignment) - 1);

                    var fatArchHeader = new FatArchHeader
                    {
                        CpuType = machO.Header.CpuType,
                        CpuSubType = machO.Header.CpuSubType,
                        Offset = offset,
                        Size = size,
                        Alignment = machO.FatArchHeader?.Alignment ?? 12, // Default to 4096-byte alignment
                    };

                    fatArchHeader.Write(fatArchHeaderBytes, isLittleEndian: false, out var _);
                    output.Write(fatArchHeaderBytes);
                }

                foreach (var machO in input)
                {
                    if (this.codeSignatureSize.TryGetValue(machO, out var codeSignatureSize))
                    {
                        UpdateCodeSignatureLayout(machO, codeSignatureSize);
                    }

                    WriteMachO(machO, output);
                }
            }
            else
            {
                var machO = input[0];

                if (this.codeSignatureSize.TryGetValue(machO, out var codeSignatureSize))
                {
                    UpdateCodeSignatureLayout(machO, codeSignatureSize);
                }

                WriteMachO(machO, output);
            }
        }

        private void UpdateCodeSignatureLayout(MachObjectFile machO, uint codeSignatureSize)
        {
            var linkEditSegment = machO.LoadCommands.OfType<ISegment>().First(s => s.Name == "__LINKEDIT");
            var codeSignatureCommand = machO.LoadCommands.OfType<LinkEdit>().FirstOrDefault(s => s.Header.CommandType == LoadCommandType.CodeSignature);

            if (codeSignatureCommand != null)
            {
                // If there was previously a code signature we go and resize the existing one. We also need
                // to resize the __LINKEDIT segment to match. The segment is always the last one in the
                // file.
                var oldSignatureSize = codeSignatureCommand.LinkEditHeader.FileSize;
                codeSignatureCommand.LinkEditHeader.FileSize = codeSignatureSize;
                if (linkEditSegment is Segment linkEditSegment32)
                    linkEditSegment32.SegmentHeader.FileSize += codeSignatureSize - oldSignatureSize;
                else if (linkEditSegment is Segment64 linkEditSegment64)
                    linkEditSegment64.SegmentHeader.FileSize += codeSignatureSize - oldSignatureSize;
            }
            else
            {
                // Create new code signature command
                codeSignatureCommand = new LinkEdit(LoadCommandType.CodeSignature, (uint)machO.GetSigningLimit(), codeSignatureSize);
                machO.LoadCommands.Add(codeSignatureCommand);

                if (machO.Header is MachHeader header32)
                {
                    header32.NumberOfCommands++;
                    header32.SizeOfCommands += codeSignatureCommand.Header.CommandSize;
                }
                else if (machO.Header is MachHeader64 header64)
                {
                    header64.NumberOfCommands++;
                    header64.SizeOfCommands += codeSignatureCommand.Header.CommandSize;
                }

                // Update __LINKEDIT segment to include the newly created command
                if (linkEditSegment is Segment linkEditSegment32)
                    linkEditSegment32.SegmentHeader.FileSize += codeSignatureSize;
                else if (linkEditSegment is Segment64 linkEditSegment64)
                    linkEditSegment64.SegmentHeader.FileSize += codeSignatureSize;
            }
        }

        private void WriteMachO(MachObjectFile machO, Stream stream)
        {
            var machMagic = new byte[4];
            var machHeaderBuffer = new byte[MachHeader64.BinarySize];
            // TODO: Allow explicit endianness switch?
            BinaryPrimitives.WriteUInt64BigEndian(machMagic, machO.Header is MachHeader64 ? (uint)MachMagic.MachHeader64BigEndian : (uint)MachMagic.MachHeaderBigEndian);

            stream.Write(machMagic);
            int bytesWritten = 0;
            if (machO.Header is MachHeader header32)
                header32.Write(machHeaderBuffer, isLittleEndian: false, out bytesWritten);
            else if (machO.Header is MachHeader64 header64)
                header64.Write(machHeaderBuffer, isLittleEndian: false, out bytesWritten);
            stream.Write(machHeaderBuffer.AsSpan(0, bytesWritten));

            foreach (var loadCommand in machO.LoadCommands)
            {
                switch (loadCommand)
                {
                    case Segment segment:
                        // TODO:
                        break;

                    case Segment64 segment64:
                        // TODO:
                        break;

                    case LinkEdit linkEdit:
                        // TODO:
                        break;

                    case UnsupportedLoadCommand unsupportedLoadCommand:
                        //machO.Header.
                        // TODO
                        break;
                }
            }

            // TODO: Write header padding

            // TODO: Write segments
        }
    }
}