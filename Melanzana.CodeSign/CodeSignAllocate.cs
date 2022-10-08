using Melanzana.MachO;
using Melanzana.Streams;

namespace Melanzana.CodeSign
{
    /// <summary>
    /// Rewriter for the Mach-O or universal binaries that resizes the code signature section
    /// and all related linker commands to make space for new signature.
    /// </summary>
    public class CodeSignAllocate
    {
        public IList<MachObjectFile> objectFiles;

        public CodeSignAllocate(IList<MachObjectFile> objectFiles)
        {
            this.objectFiles = objectFiles;
        }

        public void SetArchSize(MachObjectFile machO, uint codeSignatureSize)
        {
            // Page alignment
            codeSignatureSize = (codeSignatureSize + 0x3fffu) & ~0x3fffu;

            UpdateCodeSignatureLayout(machO, codeSignatureSize);
        }

        public string Allocate()
        {
            var tempFileName = Path.GetTempFileName();
            using var output = File.OpenWrite(tempFileName);
            MachWriter.Write(output, objectFiles);
            return tempFileName;
        }

        private static void UpdateCodeSignatureLayout(MachObjectFile machO, uint codeSignatureSize)
        {
            var linkEditSegment = machO.LoadCommands.OfType<MachSegment>().First(s => s.Name == "__LINKEDIT");
            var codeSignatureCommand = machO.LoadCommands.OfType<MachCodeSignature>().FirstOrDefault();
            uint oldSignatureSize;

            if (codeSignatureCommand != null)
            {
                // If there was previously a code signature we go and resize the existing one. We also need
                // to resize the __LINKEDIT segment to match. The segment is always the last one in the
                // file.
                oldSignatureSize = codeSignatureCommand.FileSize;
                codeSignatureCommand.Data.Size = codeSignatureSize;
            }
            else
            {
                // Create new code signature command
                var codeSignatureData = new MachLinkEditData
                {
                    FileOffset = (uint)machO.GetSigningLimit(),
                    Size = codeSignatureSize
                };

                codeSignatureCommand = new MachCodeSignature(codeSignatureData);
                machO.LoadCommands.Add(codeSignatureCommand);
                oldSignatureSize = 0;

                // The __LINKEDIT segment is computed automatically to accomodate the newly created command
            }
        }
    }
}