using Xunit;
using System.IO;
using System.Linq;
using Melanzana.Streams;

namespace Melanzana.MachO.Tests
{
    public class CreateTests
    {
        [Fact]
        public void CreateExecutable()
        {
            // Let's try to build a new macho!
            var objectFile = new MachObjectFile(Stream.Null);

            // Header
            objectFile.CpuType = MachCpuType.Arm64;
            objectFile.FileType = MachFileType.Execute;
            objectFile.Flags = MachHeaderFlags.PIE | MachHeaderFlags.TwoLevel | MachHeaderFlags.DynamicLink | MachHeaderFlags.NoUndefinedReferences;

            // Segments
            var pageZeroSegement = new MachSegment
            {
                Name = "__PAGEZERO",
                VirtualAddress = 0,
                Size = 0x100000000
            };
            var textSegment = new MachSegment
            {
                Name = "__TEXT",
                FileOffset = 0,
                VirtualAddress = 0x100000000,
                Size = 0x4000,
                InitialProtection = MachVmProtection.Execute | MachVmProtection.Read,
                MaximumProtection = MachVmProtection.Execute | MachVmProtection.Read,
            };
            var textSection = new MachSection
            {
                SectionName = "__text",
                SegmentName = "__TEXT",
                Log2Alignment = 2,
                Type = MachSectionType.Regular,
                Attributes = MachSectionAttributes.SomeInstructions | MachSectionAttributes.PureInstructions,
            };
            using (var textWriter = textSection.GetWriteStream())
            {
                textWriter.Write(new byte[] { 0x00, 0x00, 0x80, 0x52 }); // mov w0, #0
                textWriter.Write(new byte[] { 0xc0, 0x03, 0x5f, 0xd6 }); // ret
                textSection.FileOffset = 0x4000u - (uint)textWriter.Position;
                textSection.VirtualAddress = textSegment.VirtualAddress + textSection.FileOffset;
            }
            var linkEditSegment = new MachSegment
            {
                Name = "__LINKEDIT",
                VirtualAddress = textSection.VirtualAddress + textSection.Size,
                // FileOffset = 
                // FileSize =
                InitialProtection = MachVmProtection.Read,
                MaximumProtection = MachVmProtection.Read,
            };

            // TODO: This test is incomplete. We should have a layout calculator and a validation.
        }
    }
}
