using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Claunia.PropertyList;
using Melanzana.MachO;
using Melanzana.MachO.Commands;
using Melanzana.CodeSign.Blobs;
using Melanzana.Streams;

namespace Melanzana.CodeSign
{
    public class Signer
    {
        private readonly ProvisioningProfile provisioningProfile;
        private readonly X509Certificate2 certificate;
        private readonly Entitlements entitlements;

        public Signer(
            ProvisioningProfile provisioningProfile,
            X509Certificate2 certificate)
        {
            this.provisioningProfile = provisioningProfile;
            this.certificate = certificate;

            // TODO: Entitlement merging
            this.entitlements = new Entitlements(provisioningProfile.Entitlements);
        }

        private void SignMachO(Bundle bundle, string executable, byte[]? resourceSealBytes)
        {
            var teamId = provisioningProfile.TeamIdentifiers.First();

            var requirementsBlob = RequirementsBlob.CreateDefault(
                bundle.BundleIdentifier,
                certificate.GetNameInfo(X509NameType.SimpleName, false));
            var entitlementsBlob = EntitlementsBlob.Create(entitlements);
            var entitlementsDerBlob = EntitlementsDerBlob.Create(entitlements);

            var hashTypes = new[] { HashType.SHA1, HashType.SHA256 };

            ExecutableSegmentFlags executableSegmentFlags = 0;
            executableSegmentFlags |= entitlements.GetTaskAllow ? ExecutableSegmentFlags.AllowUnsigned : 0;
            executableSegmentFlags |= entitlements.RunUnsignedCode ? ExecutableSegmentFlags.AllowUnsigned : 0;
            executableSegmentFlags |= entitlements.Debugger ? ExecutableSegmentFlags.Debugger : 0;
            executableSegmentFlags |= entitlements.DynamicCodeSigning ? ExecutableSegmentFlags.Jit : 0;
            executableSegmentFlags |= entitlements.SkipLibraryValidation ? ExecutableSegmentFlags.SkipLibraryValidation : 0;
            executableSegmentFlags |= entitlements.CanLoadCdHash ? ExecutableSegmentFlags.CanLoadCdHash : 0;
            executableSegmentFlags |= entitlements.CanExecuteCdHash ? ExecutableSegmentFlags.CanExecuteCdHash : 0;

            var s = File.OpenRead(Path.Combine(bundle.BundlePath, executable));
            foreach (var machO in MachOReader.Read(s))
            {
                //var headerPad = machO.GetHeaderPad();

                long signatureSizeEstimate = 18000; // Blob Wrapper (CMS)
                var cdBuilders = new CodeDirectoryBuilder[hashTypes.Length];
                var cdHashes = new byte[hashTypes.Length][];
                for (int i = 0; i < hashTypes.Length; i++)
                {
                    cdBuilders[i] = new CodeDirectoryBuilder(machO, bundle.BundleIdentifier, teamId)
                    {
                        HashType = hashTypes[i],
                    };

                    cdBuilders[i].ExecutableSegmentFlags |= executableSegmentFlags;

                    cdBuilders[i].SetSpecialSlotData(CodeDirectorySpecialSlot.Requirements, requirementsBlob);
                    if (entitlementsBlob != null)
                        cdBuilders[i].SetSpecialSlotData(CodeDirectorySpecialSlot.Entitlements, entitlementsBlob);
                    if (entitlementsDerBlob != null)
                        cdBuilders[i].SetSpecialSlotData(CodeDirectorySpecialSlot.EntitlementsDer, entitlementsDerBlob);
                    if (resourceSealBytes != null)
                        cdBuilders[i].SetSpecialSlotData(CodeDirectorySpecialSlot.ResourceDirectory, resourceSealBytes);

                    signatureSizeEstimate += cdBuilders[i].Size(CodeDirectoryVersion.HighestVersion);
                }

                signatureSizeEstimate += requirementsBlob.Length;
                signatureSizeEstimate += entitlementsBlob?.Length ?? 0;
                signatureSizeEstimate += entitlementsBlob?.Length ?? 0;

                var codeSignatureCommand = machO.LoadCommands.OfType<LinkEdit>().FirstOrDefault(c => c.Header.CommandType == LoadCommandType.CodeSignature);
                if (codeSignatureCommand == null)
                {
                    // TODO: Create code signature section
                    throw new NotImplementedException();
                }
                else if (codeSignatureCommand.LinkEditHeader.FileSize <= signatureSizeEstimate)
                {
                    // TODO: Resize the code signature section
                    //throw new NotImplementedException();
                }

                var blobs = new List<(CodeDirectorySpecialSlot Slot, byte[] Data)>();
                var codeDirectory = cdBuilders[0].Build();

                blobs.Add((CodeDirectorySpecialSlot.CodeDirectory, codeDirectory));
                blobs.Add((CodeDirectorySpecialSlot.Requirements, requirementsBlob));
                if (entitlementsBlob != null)
                    blobs.Add((CodeDirectorySpecialSlot.Entitlements, entitlementsBlob));
                if (entitlementsDerBlob != null)
                    blobs.Add((CodeDirectorySpecialSlot.EntitlementsDer, entitlementsDerBlob));

                var hasher = hashTypes[0].GetIncrementalHash();
                hasher.AppendData(codeDirectory);
                cdHashes[0] = hasher.GetHashAndReset();
                for (int i = 1; i < hashTypes.Length; i++)
                {
                    byte[] alternativeCodeDirectory = cdBuilders[i].Build();
                    blobs.Add((CodeDirectorySpecialSlot.AlternativeCodeDirectory + i - 1, alternativeCodeDirectory));
                    hasher = hashTypes[i].GetIncrementalHash();
                    hasher.AppendData(alternativeCodeDirectory);
                    cdHashes[i] = hasher.GetHashAndReset();
                }

                var cmsWrapperBlob = CmsWrapperBlob.Create(certificate, codeDirectory, hashTypes, cdHashes);
                blobs.Add((CodeDirectorySpecialSlot.CmsWrapper, cmsWrapperBlob));

                // FIXME: Adjust the size to match LinkEdit section?
                long size = blobs.Sum(b => b.Data != null ? b.Data.Length + 8 : 0);

                var machOStream = machO.GetStream();
                var tempFile = File.OpenWrite(Path.Combine(bundle.BundlePath, executable + ".signed"));
                machOStream.Slice(0, codeSignatureCommand.LinkEditHeader.FileOffset).CopyTo(tempFile);
                var booBuffer = new byte[12 + (blobs.Count * 8)];
                BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(0, 4), (uint)BlobMagic.EmbeddedSignature);
                BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(4, 4), /*(uint)(12 + size)*/(uint)(machOStream.Length - codeSignatureCommand.LinkEditHeader.FileOffset));
                BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(8, 4), (uint)blobs.Count);
                int booBufferOffset = 12;
                int dataOffset = booBuffer.Length;
                foreach (var blob in blobs)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(booBufferOffset, 4), (uint)blob.Slot);
                    BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(booBufferOffset + 4, 4), (uint)dataOffset);
                    dataOffset += blob.Data.Length;
                    booBufferOffset += 8;
                }
                tempFile.Write(booBuffer);
                foreach (var blob in blobs)
                {
                    tempFile.Write(blob.Data);
                }
                tempFile.Write(new byte[machOStream.Length - tempFile.Position]);
                tempFile.Close();
                //tempFile.Write(new b)
                //var csStream = machO.GetStream().Slice(codeSignatureCommand.LinkEditHeader.FileOffset, codeSignatureCommand.LinkEditHeader.FileSize);
            }
        }

        public void Sign(Bundle bundle)
        {
            var resourceSeal = BuildResourceSeal(bundle);
            var resourceSealBytes = Encoding.UTF8.GetBytes(resourceSeal.ToXmlPropertyList());

            Directory.CreateDirectory(Path.Combine(bundle.BundlePath, "_CodeSignature"));
            File.WriteAllBytes(Path.Combine(bundle.BundlePath, "_CodeSignature", "CodeResources"), resourceSealBytes);

            if (bundle.MainExecutable != null)
            {
                SignMachO(bundle, bundle.MainExecutable, resourceSealBytes);
            }
        }

        private static NSDictionary BuildResourceRulesPList(IEnumerable<ResourceRule> rules)
        {
            var rulesPList = new NSDictionary();

            foreach (var rule in rules)
            {
                if (rule.Weight == 1 && !rule.IsNested && !rule.IsOmitted && !rule.IsOptional)
                {
                    rulesPList.Add(rule.Pattern, true);
                }
                else
                {
                    var rulePList = new NSDictionary();
                    if (rule.Weight != 1)
                        rulePList.Add("weight", (double)rule.Weight);
                    if (rule.IsOmitted)
                        rulePList.Add("omit", true);
                    if (rule.IsOptional)
                        rulePList.Add("optional", true);
                    if (rule.IsNested)
                        rulePList.Add("nested", true);
                    rulesPList.Add(rule.Pattern, rulePList);
                }
            }

            return rulesPList;
        }

        private static NSDictionary BuildResourceSeal(Bundle bundle)
        {
            var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];

            var resourceBuilder = new ResourceBuilder();
            bundle.AddResourceRules(resourceBuilder, useV2Rules: true);

            var rules2 = BuildResourceRulesPList(resourceBuilder.Rules);
            var files2 = new NSDictionary();
            foreach (var resourceAndRule in resourceBuilder.Scan(bundle.BundlePath))
            {
                var files2Value = new NSDictionary(2);

                Debug.Assert(resourceAndRule.Rule.IsNested == resourceAndRule.Info is DirectoryInfo);

                if (resourceAndRule.Info is DirectoryInfo)
                {
                    // TODO: Nested signature on macOS (eg. Framework)
                    throw new NotImplementedException();
                }
                else
                {
                    if (resourceAndRule.Rule.IsOptional)
                    {
                        files2Value.Add("optional", true);
                    }

                    using (var fileStream = File.OpenRead(resourceAndRule.Info.FullName))
                    {
                        int bytesRead;
                        while ((bytesRead = fileStream.Read(buffer)) > 0)
                        {
                            sha1.AppendData(buffer.AsSpan(0, bytesRead));
                            sha256.AppendData(buffer.AsSpan(0, bytesRead));
                        }
                    }

                    files2Value.Add("hash", new NSData(sha1.GetHashAndReset()));
                    files2Value.Add("hash2", new NSData(sha256.GetHashAndReset()));
                }

                files2.Add(resourceAndRule.Path, files2Value);
            };

            // Version 1 resources
            resourceBuilder = new ResourceBuilder();
            bundle.AddResourceRules(resourceBuilder, useV2Rules: false);

            var rules = BuildResourceRulesPList(resourceBuilder.Rules);
            var files = new NSDictionary();
            foreach (var resourceAndRule in resourceBuilder.Scan(bundle.BundlePath))
            {
                Debug.Assert(resourceAndRule.Info is FileInfo);

                if (files2.TryGetValue(resourceAndRule.Path, out var files2Value))
                {
                    files.Add(resourceAndRule.Path, ((NSDictionary)files2Value)["hash"]);
                }
                else
                {
                    using (var fileStream = File.OpenRead(resourceAndRule.Info.FullName))
                    {
                        int bytesRead;
                        while ((bytesRead = fileStream.Read(buffer)) > 0)
                        {
                            sha1.AppendData(buffer.AsSpan(0, bytesRead));
                        }
                    }

                    files.Add(resourceAndRule.Path, new NSData(sha1.GetHashAndReset()));
                }
            }

            // Write down the rules and hashes
            return new NSDictionary { { "files", files }, { "files2", files2 }, { "rules", rules }, { "rules2", rules2 } };
        }
    }
}
