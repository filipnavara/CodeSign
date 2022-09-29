using Melanzana.MachO;
using Iced.Intel;
using System.Diagnostics;
using System.Buffers.Binary;

namespace Melanzana.Unwind
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var inputFile = File.OpenRead(args[0]);
            var objectFiles = MachReader.Read(inputFile).ToList();

            var formatter = new NasmFormatter();
            formatter.Options.DigitSeparator = "";
            formatter.Options.FirstOperandCharIndex = 10;

            foreach (var objectFile in objectFiles)
            {
                var compactUnwindSection = objectFile.LoadCommands.OfType<MachSegment>().SelectMany(seg => seg.Sections).First(sec => sec.SegmentName == "__LD" && sec.SectionName == "__compact_unwind");
                var ehFrameSection = objectFile.LoadCommands.OfType<MachSegment>().SelectMany(seg => seg.Sections).First(sec => sec.SegmentName == "__TEXT" && sec.SectionName == "__eh_frame");

                if (compactUnwindSection != null && ehFrameSection != null)
                {
                    var ehFrame = new DwarfEhFrame(ehFrameSection.GetReadStream(), ehFrameSection.VirtualAddress);

                    using var compactUnwindStream = compactUnwindSection.GetReadStream();
                    var compactUnwindReader = new BinaryReader(compactUnwindStream);
                    
                    while (compactUnwindStream.Position < compactUnwindStream.Length)
                    {
                        var rangeStart = compactUnwindReader.ReadUInt64(); // Assume 64-bit binaries
                        var rangeLength = compactUnwindReader.ReadUInt32();
                        var encoding = compactUnwindReader.ReadUInt32();
                        var personality = compactUnwindReader.ReadUInt64();
                        var lsda = compactUnwindReader.ReadUInt64();


                        ulong lastPrologCodeOffset = 0;
                        var dwarfCfi = ehFrame.EnumerateCfi(rangeStart).ToList();
                        foreach (var cfi in dwarfCfi)
                        {
                            lastPrologCodeOffset = Math.Max(lastPrologCodeOffset, cfi.codeOffset);
                        }

                        using var codeStream = objectFile.GetStreamAtVirtualAddress(rangeStart, rangeLength);
                        var code = new byte[rangeLength];
                        codeStream.ReadExactly(code);

                        var codeCfi = EnumerateCfi(code, lastPrologCodeOffset);

                        if (!dwarfCfi.SequenceEqual(codeCfi))
                        {
                            Console.WriteLine($"-- {rangeStart:X16}");
                            foreach (var cfi in dwarfCfi)
                                Console.WriteLine($"{cfi.codeOffset} {cfi.cfiRegister} {cfi.cfiRegisterOffset:X8}");
                            foreach (var cfi in codeCfi)
                                Console.WriteLine($"{cfi.codeOffset} {cfi.cfiRegister} {cfi.cfiRegisterOffset:X8}");

                            var codeReader = new ByteArrayCodeReader(code);
                            var decoder = Decoder.Create(IntPtr.Size * 8, codeReader);

                            var instructions = new InstructionList();
                            while (codeReader.CanReadByte)
                                decoder.Decode(out instructions.AllocUninitializedElement());

                            var output = new StringOutput();
                            ulong codeOffset = 0;
                            // Use InstructionList's ref iterator (C# 7.3) to prevent copying 32 bytes every iteration
                            foreach (ref var instr in instructions)
                            {
                                if (codeOffset >= lastPrologCodeOffset)
                                    break;
                                // Don't use instr.ToString(), it allocates more, uses masm syntax and default options
                                formatter.Format(instr, output);
                                Console.Write($"{instr.IP:X16} ");
                                for (int i = 0; i < instr.Length; i++)
                                {
                                    Console.Write(code[(int)instr.IP + i].ToString("X2"));
                                }
                                Console.Write(new string(' ', 16 * 2 - instr.Length * 2));
                                Console.WriteLine($"{output.ToStringAndReset()}");
                                codeOffset += (ulong)instr.Length;
                            }
                        }
                    }
                }
            }
        }

        private static bool IS_REX_PREFIX(byte b) => (b & 0xf0) == 0x40;

        public static IEnumerable<(ulong codeOffset, ulong cfiRegister, ulong cfiRegisterOffset)> EnumerateCfi(byte[] code, ulong prologLength)
        {
            // Initially the return address is at RSP+8
            yield return (0, 7, 8);

            int codeOffset = 0;
            ulong cfiRegister = 7;
            ulong cfiRegisterOffset = 8;
            while (codeOffset < (int)prologLength)
            {
                if ((code[codeOffset] & 0xf8) == 0x50) // POP
                {
                    codeOffset++;
                    if (cfiRegister == 7)
                        cfiRegisterOffset += 8;
                }
                else if (IS_REX_PREFIX(code[codeOffset]) && (code[codeOffset + 1] & 0xf8) == 0x50)
                {
                    codeOffset += 2;
                    if (cfiRegister == 7)
                        cfiRegisterOffset += 8;
                }
                else if ((code[codeOffset] & 0xf8) == 0x48 && // SIZE64_PREFIX
                    code[codeOffset + 1] == 0x83 &&
                    code[codeOffset + 2] == 0xec) // ADD_IMM8_OP
                {
                    // sub rsp, imm8
                    cfiRegisterOffset += code[codeOffset + 3];
                    codeOffset += 4;
                }
                else if ((code[codeOffset] & 0xf8) == 0x48 && // SIZE64_PREFIX
                    code[codeOffset + 1] == 0x81 &&
                    code[codeOffset + 2] == 0xec) // ADD_IMM32_OP
                {
                    // sub rsp, imm32
                    cfiRegisterOffset += BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(codeOffset + 3));
                    codeOffset += 7;
                }
                else if ((code[codeOffset] & 0xf8) == 0x48 && // SIZE64_PREFIX
                    code[codeOffset + 1] == 0x8d &&
                    code[codeOffset + 2] == 0x6c &&
                    code[codeOffset + 3] == 0x24)
                {
                    // lea rbp,[rsp+IMM8]
                    cfiRegister = 6; // RBP
                    cfiRegisterOffset -= code[codeOffset + 4];
                    codeOffset += 5;
                }
                else if ((code[codeOffset] & 0xf8) == 0x48 && // SIZE64_PREFIX
                    code[codeOffset + 1] == 0x8d &&
                    code[codeOffset + 2] == 0xac &&
                    code[codeOffset + 3] == 0x24)
                {
                    // lea rbp,[rsp+IMM32]
                    cfiRegister = 6; // RBP
                    cfiRegisterOffset -= BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(codeOffset + 4));
                    codeOffset += 8;
                }
                else
                {
                    //Debug.Fail("TODO");
                    yield break;
                }
                yield return ((ulong)codeOffset, cfiRegister, cfiRegisterOffset);
            }
        }
    }
}