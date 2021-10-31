using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Claunia.PropertyList;
using CodeSign.MachO;
using CodeSign.MachO.Commands;
using CodeSign.Streams;

namespace CodeSign.Signing
{
    class Signer
    {
        private readonly ProvisioningProfile provisioningProfile;
        private readonly X509Certificate2 certificate;

        public Signer(
            ProvisioningProfile provisioningProfile,
            X509Certificate2 certificate)
        {
            this.provisioningProfile = provisioningProfile;
            this.certificate = certificate;
        }



        /*public void Sign(MachO machO)
        {

        }*/

        public void Sign(Bundle bundle)
        {
            if (bundle.MainExecutable != null)
            {
                // TODO: Entitlement merging

                var requirementsBlob = RequirementsBlob.CreateDefault(
                    bundle.BundleIdentifier,
                    certificate.GetNameInfo(X509NameType.SimpleName, false));

                var entitlementsBlob = EntitlementsBlob.Create(provisioningProfile.Entitlements);
                var entitlementsDerBlob = EntitlementsDerBlob.Create(provisioningProfile.Entitlements);

                var s = File.OpenRead(Path.Combine(bundle.BundlePath, bundle.MainExecutable));
                foreach (var machO in MachOReader.Read(s))
                {
                    var headerPad = machO.GetHeaderPad();

                    var cdBuilderSha1 = new CodeDirectoryBuilder(
                        machO,
                        bundle.BundleIdentifier,
                        provisioningProfile.TeamIdentifiers.First()) { HashType = CodeSigningHashType.SHA1 };

                    var cdBuilderSha256 = new CodeDirectoryBuilder(
                        machO,
                        bundle.BundleIdentifier,
                        provisioningProfile.TeamIdentifiers.First()) { HashType = CodeSigningHashType.SHA256 };

                    // Blobs:
                    // - CodeDirectory SHA-1
                    // - Requirements
                    // - Entitlements
                    // - Entitlements DER
                    // - CodeDirectory SHA-256
                    // - Blob Wrapper

                    var requirementsBytes = requirementsBlob.Emit();
                    var entitlementsBytes = entitlementsBlob?.Emit();
                    var entitlementsDerBytes = entitlementsDerBlob?.Emit();

                    cdBuilderSha1.SetSpecialSlotData(CodeDirectorySpecialSlot.Requirements, requirementsBytes);
                    cdBuilderSha1.SetSpecialSlotData(CodeDirectorySpecialSlot.Entitlements, entitlementsBytes);
                    cdBuilderSha1.SetSpecialSlotData(CodeDirectorySpecialSlot.EntitlementsDer, entitlementsDerBytes);
                    cdBuilderSha256.SetSpecialSlotData(CodeDirectorySpecialSlot.Requirements, requirementsBytes);
                    cdBuilderSha256.SetSpecialSlotData(CodeDirectorySpecialSlot.Entitlements, entitlementsBytes);
                    cdBuilderSha256.SetSpecialSlotData(CodeDirectorySpecialSlot.EntitlementsDer, entitlementsDerBytes);
                    // TODO: resource seal?

                    long signatureSizeEstimate = 18000; // Blob Wrapper (CMS)
                    signatureSizeEstimate += cdBuilderSha1.Size(CodeDirectoryVersion.HighestVersion);
                    signatureSizeEstimate += requirementsBytes.Length;
                    signatureSizeEstimate += entitlementsBytes?.Length ?? 0;
                    signatureSizeEstimate += entitlementsDerBytes?.Length ?? 0;
                    signatureSizeEstimate += cdBuilderSha256.Size(CodeDirectoryVersion.HighestVersion);

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

                    // TODO: Actually build and sign the blobs
                    var codeDirectorySha1Bytes = cdBuilderSha1.Build();
                    var codeDirectorySha256Bytes = cdBuilderSha256.Build();

                    var cmsWrapperBlob = CmsWrapperBlob.Create(certificate, codeDirectorySha1Bytes, SHA1.HashData(codeDirectorySha1Bytes), SHA256.HashData(codeDirectorySha256Bytes));
                    var cmsWrapperBytes = cmsWrapperBlob.Emit();

                    var blobs = new (CodeDirectorySpecialSlot Slot, byte[]? Data)[] {
                        (CodeDirectorySpecialSlot.CodeDirectory, codeDirectorySha1Bytes),
                        (CodeDirectorySpecialSlot.Requirements, requirementsBytes),
                        (CodeDirectorySpecialSlot.Entitlements, entitlementsBytes),
                        (CodeDirectorySpecialSlot.EntitlementsDer, entitlementsDerBytes),
                        (CodeDirectorySpecialSlot.CodeDirectory2, codeDirectorySha256Bytes),
                        (CodeDirectorySpecialSlot.CmsWrapper, cmsWrapperBytes),
                    };
                    long size = blobs.Sum(b => b.Data != null ? b.Data.Length + 8 : 0);

                    var machOStream = machO.GetStream();
                    File.Delete("signed");
                    var tempFile = File.OpenWrite("signed");
                    machOStream.Slice(0, codeSignatureCommand.LinkEditHeader.FileOffset).CopyTo(tempFile);
                    var booBuffer = new byte[12 + (blobs.Length * 8)];
                    BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(0, 4), (uint)CodeSigningBlobMagic.EmbeddedSignature);
                    BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(4, 4), (uint)(12 + size));
                    BinaryPrimitives.WriteUInt32BigEndian(booBuffer.AsSpan(8, 4), (uint)blobs.Length);
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
            var resourceSeal = BuildResourceSeal(bundle);
            File.WriteAllText("seal.txt", resourceSeal.ToXmlPropertyList());
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
