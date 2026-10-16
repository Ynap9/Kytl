using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants.Documents;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class TimestampClient : ITimestampClient
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(SigningConstants.TsaTimeoutSeconds)
        };

        private static readonly SemaphoreSlim Throttle =
            new(SigningConstants.MaxTsaConcurrency, SigningConstants.MaxTsaConcurrency);

        public async Task<byte[]> RequestTokenAsync(byte[] signatureValue, CancellationToken cancellationToken = default)
        {
            var hash = SHA256.HashData(signatureValue);

            var nonce = RandomNumberGenerator.GetBytes(8);
            var request = Rfc3161TimestampRequest.CreateFromHash(
                hash,
                HashAlgorithmName.SHA256,
                requestedPolicyId: null,
                nonce: nonce,
                requestSignerCertificates: true);

            using var content = new ByteArrayContent(request.Encode());
            content.Headers.ContentType = new MediaTypeHeaderValue(SigningConstants.TsaContentType);

            byte[] replyBytes;
            await Throttle.WaitAsync(cancellationToken);
            try
            {
                using var response = await Http.PostAsync(SigningConstants.TsaUrl, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"TSA trả về HTTP {(int)response.StatusCode}. Không lấy được dấu thời gian.");
                }

                replyBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            finally
            {
                Throttle.Release();
            }

            var token = request.ProcessResponse(replyBytes, out _);
            return token.AsSignedCms().Encode();
        }
    }
}
