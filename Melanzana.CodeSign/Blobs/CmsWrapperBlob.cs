using System.Buffers.Binary;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Claunia.PropertyList;

namespace Melanzana.CodeSign.Blobs
{
    public class CmsWrapperBlob
    {
        /// <summary>
        /// The key usage extension defines the purpose (e.g., encipherment,
        /// signature, certificate signing) of the key contained in the
        /// certificate.
        /// </summary>
        /// <seealso href="https://tools.ietf.org/html/rfc2459#section-4.2.1.3"/>
        public const string KeyUsage = "2.5.29.15";

        /// <summary>
        /// The extended key usage field indicates one or more purposes for which the certified
        /// public key may be used, in addition to or in place of the basic
        /// purposes indicated in the key usage extension field.
        /// </summary>
        /// <seealso href="https://tools.ietf.org/html/rfc2459#section-4.2.1.13"/>
        public const string ExtendedKeyUsage = "2.5.29.37";

        /// <summary>
        /// An extended key usage value indicating the certificate may be used for
        /// signing of downloadable executable code.
        /// </summary>
        /// <seealso href="https://tools.ietf.org/html/rfc3280.html#section-4.2.1.13"/>
        public const string CodeSigning = "1.3.6.1.5.5.7.3.3";

        /// <summary>
        /// The OID of a certificate extension which indicates the certificate is inteded for iPhone Software Submission Signing.
        /// </summary>
        /// <seealso href="https://images.apple.com/certificateauthority/pdf/Apple_WWDR_CPS_v1.23.pdf"/>
        public const string SoftwareSubmissionSigning = "1.2.840.113635.100.6.1.4";

        /// <summary>
        /// The OID of a certificate extension which indicates the certificate is intended for iPhone Software Development Signing.
        /// </summary>
        /// <seealso href="https://images.apple.com/certificateauthority/pdf/Apple_WWDR_CPS_v1.23.pdf"/>
        public const string SoftwareDevelopmentSigning = "1.2.840.113635.100.6.1.2";

        /// <summary>
        /// The basic constraints extension identifies whether the subject of the
        /// certificate is a CA and the maximum depth of valid certification
        /// paths that include this certificate.
        /// </summary>
        /// <seealso href="https://tools.ietf.org/html/rfc3280.html#section-4.2.1.10"/>
        public const string BasicConstraints = "2.5.29.19";

        /// <summary>
        /// The certificate policies extension contains a sequence of one or more
        /// policy information terms.
        /// </summary>
        /// <seealso href="https://tools.ietf.org/html/rfc5280#section-4.2.1.4"/>
        public const string CertificatePolicies = "2.5.29.32";

        private readonly static string rootCertificatePath = "Melanzana.CodeSign.Certificates.RootCertificate.cer";
        private readonly static string g1IntermediateCertificatePath = "Melanzana.CodeSign.Certificates.IntermediateG1Certificate.cer";
        private readonly static string g3IntermediateCertificatePath = "Melanzana.CodeSign.Certificates.IntermediateG3Certificate.cer";

        private readonly static X509Certificate2 RootCertificate = GetManifestCertificate(rootCertificatePath);
        private readonly static X509Certificate2[] IntermediateCertificates = new X509Certificate2[]
        {
            GetManifestCertificate(g1IntermediateCertificatePath),
            GetManifestCertificate(g3IntermediateCertificatePath),
        };

        private static X509Certificate2 GetManifestCertificate(string name)
        {
            var memoryStream = new MemoryStream();
            using (var manifestStream = typeof(CmsWrapperBlob).Assembly.GetManifestResourceStream(name))
            {
                Debug.Assert(manifestStream != null);
                manifestStream.CopyTo(memoryStream);
            }
            return new X509Certificate2(memoryStream.ToArray());
        }

        public static byte[] Create(
            X509Certificate2? developerCertificate,
            AsymmetricAlgorithm? privateKey,
            byte[] dataToSign,
            HashType[] hashTypes,
            byte[][] cdHashes)
        {
            if (dataToSign == null)
                throw new ArgumentNullException(nameof(dataToSign));
            if (hashTypes == null)
                throw new ArgumentNullException(nameof(hashTypes));
            if (cdHashes == null)
                throw new ArgumentNullException(nameof(cdHashes));
            if (hashTypes.Length != cdHashes.Length)
                throw new ArgumentException($"Length of hashType ({hashTypes.Length} is different from length of cdHashes ({cdHashes.Length})");

            // Ad-hoc signature
            if (developerCertificate == null)
            {
                var adhocBlobBuffer = new byte[8];
                BinaryPrimitives.WriteUInt32BigEndian(adhocBlobBuffer.AsSpan(0, 4), (uint)BlobMagic.CmsWrapper);
                BinaryPrimitives.WriteUInt32BigEndian(adhocBlobBuffer.AsSpan(4, 4), (uint)adhocBlobBuffer.Length);
                return adhocBlobBuffer;
            }

            var certificatesList = new X509Certificate2Collection();

            // Try to build full chain

            if (GetCustomRootTrustChain(developerCertificate, out var customCertificates))
            {
                certificatesList.AddRange(customCertificates);
            }
            else if (GetDefaultChain(developerCertificate, out var defaultCertificates))
            {
                certificatesList.AddRange(defaultCertificates);
            }
            else if(GetManualChain(developerCertificate, out var manualCertificates))
            {
                certificatesList.AddRange(manualCertificates);
            }
            else
            {
                throw new Exception("Could not build the certificate chain for the developer certificate.");
            }

            var cmsSigner = privateKey == null ?
                new CmsSigner(developerCertificate) :
                new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, developerCertificate, privateKey);
            cmsSigner.Certificates.AddRange(certificatesList);
            cmsSigner.IncludeOption = X509IncludeOption.None;

            cmsSigner.SignedAttributes.Add(new Pkcs9SigningTime());

            // DER version of the hash attribute
            var values = new AsnEncodedDataCollection();
            var oid = new Oid("1.2.840.113635.100.9.2", null);
            var plistCdHashes = new NSArray();
            for (int i = 0; i < hashTypes.Length; i++)
            {
                var codeDirectoryAttrWriter = new AsnWriter(AsnEncodingRules.DER);
                using (codeDirectoryAttrWriter.PushSequence())
                {
                    codeDirectoryAttrWriter.WriteObjectIdentifier(hashTypes[i].GetOid());
                    codeDirectoryAttrWriter.WriteOctetString(cdHashes[i]);
                }
                values.Add(new AsnEncodedData(oid, codeDirectoryAttrWriter.Encode()));
                plistCdHashes.Add(new NSData(cdHashes[i].AsSpan(0, 20).ToArray()));
            }
            cmsSigner.SignedAttributes.Add(new CryptographicAttributeObject(oid, values));

            // PList version of the hash attribute
            var plistBytes = Encoding.UTF8.GetBytes(new NSDictionary() { ["cdhashes"] = plistCdHashes }.ToXmlPropertyList());
            var codeDirectoryPListAttrWriter = new AsnWriter(AsnEncodingRules.DER);
            codeDirectoryPListAttrWriter.WriteOctetString(plistBytes);
            cmsSigner.SignedAttributes.Add(new AsnEncodedData("1.2.840.113635.100.9.1", codeDirectoryPListAttrWriter.Encode()));

            var signedCms = new SignedCms(new ContentInfo(dataToSign), true);
            signedCms.ComputeSignature(cmsSigner);

            var encodedCms = signedCms.Encode();

            var blobBuffer = new byte[8 + encodedCms.Length];
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(0, 4), (uint)BlobMagic.CmsWrapper);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(4, 4), (uint)blobBuffer.Length);
            encodedCms.CopyTo(blobBuffer.AsSpan(8));

            return blobBuffer;
        }

        private static bool GetCustomRootTrustChain(X509Certificate2 developerCertificate, out X509Certificate2[] certificates)
        {
            var chain = new X509Chain();
            var chainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust };
            chainPolicy.CustomTrustStore.Add(RootCertificate);
            chainPolicy.CustomTrustStore.AddRange(IntermediateCertificates);
            chain.ChainPolicy = chainPolicy;
            if (chain.Build(developerCertificate))
            {
                certificates = chain.ChainElements.Select(e => e.Certificate).ToArray();
                return true;
            }
            else
            {
                certificates = Array.Empty<X509Certificate2>();
                return false;
            }
        }

        private static bool GetDefaultChain(X509Certificate2 developerCertificate, out X509Certificate2[] certificates)
        {
            var chain = new X509Chain();
            if (chain.Build(developerCertificate))
            {
                certificates = chain.ChainElements.Select(e => e.Certificate).ToArray();
                return true;
            }
            else
            {
                certificates = Array.Empty<X509Certificate2>();
                return false;
            }
        }

        private static bool GetManualChain(X509Certificate2 developerCertificate, out X509Certificate2[] certificates)
        {
            certificates = Array.Empty<X509Certificate2>();

            // The certificate must be issued by any of the intermediate certificates
            // TODO: we should also check on Authority Key Identifier
            var intermediate = IntermediateCertificates.SingleOrDefault(c => developerCertificate.IssuerName.Name == c.SubjectName.Name);
            if (intermediate == null)
            {
                return false;
            }

            // The certificate must be a signing certificate
            var keyUsageExtension = (X509KeyUsageExtension?)developerCertificate.Extensions[KeyUsage];
            if (keyUsageExtension == null || keyUsageExtension.KeyUsages != X509KeyUsageFlags.DigitalSignature)
            {
                return false;
            }

            // The certificate must be a code signing certificate
            var extendedKeyUsage = (X509EnhancedKeyUsageExtension?)developerCertificate.Extensions[ExtendedKeyUsage];
            if (extendedKeyUsage == null || extendedKeyUsage.EnhancedKeyUsages.Count != 1 || extendedKeyUsage.EnhancedKeyUsages[0].Value != CodeSigning)
            {
                return false;
            }

            // The certificate must be either a software submission or a software development certificate
            bool softwareSubmissionSigning = developerCertificate.Extensions[SoftwareSubmissionSigning] != null;
            bool softwareDevelopmentSigning = developerCertificate.Extensions[SoftwareDevelopmentSigning] != null;

            if (!softwareSubmissionSigning && !softwareDevelopmentSigning)
            {
                return false;
            }


            // The certificate cannot be a self-signed certificate or a root CA
            var basicConstraints = developerCertificate.Extensions[BasicConstraints] as X509BasicConstraintsExtension;
            if (basicConstraints != null && basicConstraints.CertificateAuthority)
            {
                return false;
            }

            // The certificate should adhere to the Apple certificate policy (but .NET does not contain classes for validating
            // certificate policies)
            var policiesExtension = developerCertificate.Extensions[CertificatePolicies];

            certificates = new X509Certificate2[]
            {
                RootCertificate,
                intermediate,
                developerCertificate,
            };

            return true;
        }
    }
}
