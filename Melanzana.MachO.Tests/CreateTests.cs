using Xunit;
using System.IO;
using System.Linq;
using Melanzana.Streams;
using System;

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

        [Fact]
        public void CreateObjectFile()
        {
            var objectFile = new MachObjectFile(Stream.Null);

            objectFile.CpuType = MachCpuType.Arm64;
            objectFile.CpuSubType = 0;
            objectFile.FileType = MachFileType.Object;
            objectFile.Flags = MachHeaderFlags.SubsectionsViaSymbols;
            objectFile.IsLittleEndian = true;

            var segment = new MachSegment
            {
                Name = "",
                InitialProtection = MachVmProtection.Execute | MachVmProtection.Read | MachVmProtection.Write,
                MaximumProtection = MachVmProtection.Execute | MachVmProtection.Read | MachVmProtection.Write,
            };

            var textSection = new MachSection
            {
                SectionName = "__text",
                SegmentName = "__TEXT",
                Log2Alignment = 2,
                Type = MachSectionType.Regular,
                Attributes = MachSectionAttributes.SomeInstructions | MachSectionAttributes.PureInstructions,
            };

            var compactUnwindSection = new MachSection
            {
                SectionName = "__compact_unwind",
                SegmentName = "__LD",
                Log2Alignment = 3,
                Type = MachSectionType.Regular,
                Attributes = MachSectionAttributes.Debug,
            };

            segment.Sections.Add(textSection);
            segment.Sections.Add(compactUnwindSection);
            objectFile.LoadCommands.Add(segment);

            using (var textWriter = textSection.GetWriteStream())
            {
                textWriter.Write(new byte[] { 0xff, 0x43, 0x00, 0xd1 }); // sub sp, sp, #0x10
                textWriter.Write(new byte[] { 0x00, 0x00, 0x80, 0x52 }); // mov w0, #0
                textWriter.Write(new byte[] { 0xff, 0x0f, 0x00, 0xb9 }); // str wzr, [sp, #0xc]
                textWriter.Write(new byte[] { 0xff, 0x43, 0x00, 0x91 }); // add sp, sp, #0x10
                textWriter.Write(new byte[] { 0xc0, 0x03, 0x5f, 0xd6 }); // ret
            }

            using (var compactUnwindWriter = compactUnwindSection.GetWriteStream())
            using (var compactUnwindRelocWriter = compactUnwindSection.GetRelocationWriter(objectFile))
            {
                // Address of _main
                compactUnwindRelocWriter.AddRelocation(new MachRelocation
                {
                    Address = 0,
                    SymbolOrSectionIndex = 1, // TODO: Better symbolic reference?
                    Length = 8,
                    RelocationType = MachRelocationType.Arm64Unsigned,
                    IsExternal = false,
                    IsPCRelative = false,
                });
                compactUnwindWriter.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); 

                compactUnwindWriter.Write(new byte[] { 0x14, 0x00, 0x00, 0x00 }); // Length of _main
                compactUnwindWriter.Write(new byte[] { 0x00, 0x10, 0x00, 0x02 }); // Unwind code
                compactUnwindWriter.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // No personality
                compactUnwindWriter.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }); // No LSDA
            }

            objectFile.LoadCommands.Add(new MachBuildVersion
            {
                TargetPlatform = MachPlatform.MacOS,
                MinimumPlatformVersion = new Version(12, 0, 0),
                SdkVersion = new Version(12, 0, 0),
            });

            var symbolTable = new MachSymbolTable();
            objectFile.LoadCommands.Add(symbolTable);
            using (var symbolTableWriter = symbolTable.GetWriter(objectFile))
            {
                // FIXME: Values
                symbolTableWriter.AddSymbol(new MachSymbol { Name = "ltmp0", Section = textSection, Value = 0, Descriptor = 0, Type = MachSymbolType.Section });
                symbolTableWriter.AddSymbol(new MachSymbol { Name = "ltmp1", Section = compactUnwindSection, Value = 0x18, Descriptor = 0, Type = MachSymbolType.Section });
                symbolTableWriter.AddSymbol(new MachSymbol { Name = "_main", Section = textSection, Value = 0, Descriptor = 0, Type = MachSymbolType.Section | MachSymbolType.External });
                objectFile.LoadCommands.Add(symbolTableWriter.CreateDynamicLinkEditSymbolTable());
            }

            objectFile.UpdateLayout();

            // Ensure that write doesn't crash
            var binaryFile = new MemoryStream();            
            MachWriter.Write(binaryFile, objectFile);
            Assert.Equal(528, binaryFile.Length);

            // Ensure that the file can be read again
            binaryFile.Position = 0;
            _ = MachReader.Read(binaryFile);
        }
    }
}
