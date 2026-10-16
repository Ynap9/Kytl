using PdfSharp.Fonts;
using PdfSharp.Pdf.Signatures;
using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Constants.Documents;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class PdfSigner : IPdfSigner
    {
        private readonly ISigningKey _signingKey;
        private readonly ITimestampClient _timestampClient;
        private readonly ISignaturePlacementResolver _placementResolver;

        private readonly SemaphoreSlim _keyLock = new(1, 1);

        private static readonly object FontInitLock = new();
        private static bool _fontsInitialized;

        public PdfSigner(ISigningKey signingKey, ITimestampClient timestampClient,
            ISignaturePlacementResolver placementResolver)
        {
            _signingKey = signingKey;
            _timestampClient = timestampClient;
            _placementResolver = placementResolver;
        }

        public async Task SignAsync(string inputPath, string outputPath, PdfSignOptionsDto options,
            CancellationToken cancellationToken = default)
        {
            var (cert, privateKey) = _signingKey is StoreSigningKey store
                ? store.GetCached(options.Thumbprint)
                : (null, null);

            if (cert == null || privateKey == null)
            {
                throw new InvalidOperationException(
                    "Chưa kiểm tra chứng thư số trước khi ký. Gọi Verify() trước khi bắt đầu lô ký.");
            }

            lock (FontInitLock)
            {
                if (!_fontsInitialized)
                {
                    GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                    _fontsInitialized = true;
                }
            }

            var signedAt = DateTimeConstants.VietnamNow;
            var signer = new CadesSigner(cert, privateKey, _timestampClient, _keyLock, cancellationToken);

            var signedBytes = await PdfIncrementalSigner.SignAsync(
                await File.ReadAllBytesAsync(inputPath, cancellationToken),
                cert.GetNameInfo(X509NameType.SimpleName, false) ?? cert.Subject,
                string.IsNullOrWhiteSpace(options.Reason) ? SigningConstants.DefaultReason : options.Reason,
                string.IsNullOrWhiteSpace(options.Location) ? SigningConstants.DefaultLocation : options.Location,
                signedAt,
                await signer.GetSignatureSizeAsync(),
                options.Placements,
                options.FontFamily,
                options.FontSize,
                options.TextColor,
                options.SealImage,
                options.WetSignatureImage,
                _placementResolver,
                async (bytes, _) => await signer.GetSignatureAsync(new MemoryStream(bytes)),
                cancellationToken);

            var tempPath = outputPath + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(tempPath, signedBytes, cancellationToken);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(tempPath, outputPath);
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }
        }

        private sealed class CadesSigner : IDigitalSigner
        {
            private readonly X509Certificate2 _cert;
            private readonly AsymmetricAlgorithm _privateKey;
            private readonly ITimestampClient _timestampClient;
            private readonly SemaphoreSlim _keyLock;
            private readonly CancellationToken _cancellationToken;

            public CadesSigner(X509Certificate2 cert, AsymmetricAlgorithm privateKey,
                ITimestampClient timestampClient, SemaphoreSlim keyLock, CancellationToken cancellationToken)
            {
                _cert = cert;
                _privateKey = privateKey;
                _timestampClient = timestampClient;
                _keyLock = keyLock;
                _cancellationToken = cancellationToken;
            }

            public string CertificateName =>
                _cert.GetNameInfo(X509NameType.SimpleName, false) ?? _cert.Subject;

            public Task<int> GetSignatureSizeAsync() => Task.FromResult(32 * 1024);

            public async Task<byte[]> GetSignatureAsync(Stream stream)
            {
                var content = new byte[stream.Length];
                stream.Position = 0;
                var offset = 0;
                while (offset < content.Length)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var read = stream.Read(content, offset, content.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }

                if (offset != content.Length)
                {
                    throw new InvalidOperationException(
                        $"Chỉ đọc được {offset}/{content.Length} byte của vùng cần ký trong PDF.");
                }

                var cms = new SignedCms(new ContentInfo(content), detached: true);

                var cmsSigner = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _cert, _privateKey)
                {
                    DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"),
                    IncludeOption = X509IncludeOption.WholeChain
                };
                cmsSigner.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));

                var asn = new AsnWriter(AsnEncodingRules.DER);
                asn.PushSequence();
                asn.PushSequence();
                asn.PushSequence();
                asn.WriteOctetString(SHA256.HashData(_cert.RawData));
                asn.PopSequence();
                asn.PopSequence();
                asn.PopSequence();
                cmsSigner.SignedAttributes.Add(new AsnEncodedData(
                    new Oid(SigningConstants.OidSigningCertificateV2), asn.Encode()));

                await _keyLock.WaitAsync(_cancellationToken);
                try
                {
                    cms.ComputeSignature(cmsSigner);
                }
                finally
                {
                    _keyLock.Release();
                }

                var signerInfo = cms.SignerInfos[0];
                var token = await _timestampClient.RequestTokenAsync(signerInfo.GetSignature(), _cancellationToken);
                signerInfo.AddUnsignedAttribute(new AsnEncodedData(
                    new Oid(SignatureConstants.OidSignatureTimeStampToken), token));

                return cms.Encode();
            }
        }
    }
}
