using ktl.be.infrastructure.external.Documents.Dtos;
using ktl.be.infrastructure.external.Documents.Interfaces;
using ktl.be.shared.Constants;
using ktl.be.shared.Constants.Documents;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ktl.be.infrastructure.external.Documents.Implements
{
    public class StoreSigningKey : ISigningKey
    {
        private readonly object _cacheLock = new();
        private string? _cachedThumbprint;
        private X509Certificate2? _cachedCert;
        private TokenVerifyDto? _cachedResult;
        private RSA? _cachedRsa;
        private ECDsa? _cachedEcdsa;

        public TokenVerifyDto Verify(string thumbprint)
        {
            lock (_cacheLock)
            {
                if (_cachedResult != null &&
                    string.Equals(_cachedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    return _cachedResult;
                }

                ClearCacheInternal();

                var dto = new TokenVerifyDto { Thumbprint = thumbprint ?? string.Empty };

                var cert = ScanStoreForCertificate(thumbprint);
                if (cert == null)
                {
                    dto.FailedCheck = nameof(dto.FoundInStore);
                    dto.Reason = "Không tìm thấy chứng thư số trên máy. Kiểm tra USB token đã cắm và middleware (VGCA PKI Manager/bit4id xPKI) đã chạy chưa.";
                    return dto;
                }

                dto.FoundInStore = true;
                dto.CommonName = cert.GetNameInfo(X509NameType.SimpleName, false);
                dto.IssuerCommonName = cert.GetNameInfo(X509NameType.SimpleName, true);

                if (!cert.HasPrivateKey)
                {
                    dto.FailedCheck = nameof(dto.HasPrivateKey);
                    dto.Reason = "Chứng thư số không có khóa bí mật trên máy này nên không ký được.";
                    return dto;
                }
                dto.HasPrivateKey = true;

                var now = DateTimeConstants.VietnamNow;
                if (now < cert.NotBefore || now > cert.NotAfter)
                {
                    dto.FailedCheck = nameof(dto.NotExpired);
                    dto.Reason = $"Chứng thư số ngoài thời hạn hiệu lực ({cert.NotBefore:dd/MM/yyyy HH:mm:ss} - {cert.NotAfter:dd/MM/yyyy HH:mm:ss}).";
                    return dto;
                }
                dto.NotExpired = true;

                var keyUsage = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
                var allowsSigning = keyUsage == null
                    || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature)
                    || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation);
                if (!allowsSigning)
                {
                    dto.FailedCheck = nameof(dto.AllowsSigning);
                    dto.Reason = "Chứng thư số không được cấp quyền ký (KeyUsage thiếu digitalSignature/nonRepudiation).";
                    return dto;
                }
                dto.AllowsSigning = true;

                try
                {
                    var probe = RandomNumberGenerator.GetBytes(SigningConstants.PreflightTestDataSize);
                    var rsa = cert.GetRSAPrivateKey();
                    if (rsa != null)
                    {
                        if (rsa is RSACng rsaCng && SigningUiContext.WindowHandle != IntPtr.Zero)
                        {
                            try
                            {
                                var hwndBytes = IntPtr.Size == 8
                                    ? BitConverter.GetBytes(SigningUiContext.WindowHandle.ToInt64())
                                    : BitConverter.GetBytes(SigningUiContext.WindowHandle.ToInt32());
                                rsaCng.Key.SetProperty(
                                    new CngProperty("HWND Handle", hwndBytes, CngPropertyOptions.None));
                            }
                            catch
                            {
                            }
                        }

                        rsa.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        _cachedRsa = rsa;
                    }
                    else
                    {
                        var ecdsa = cert.GetECDsaPrivateKey()
                            ?? throw new InvalidOperationException("Chứng thư số không dùng thuật toán RSA hoặc ECDSA.");
                        ecdsa.SignData(probe, HashAlgorithmName.SHA256);
                        _cachedEcdsa = ecdsa;
                    }
                }
                catch (Exception ex)
                {
                    ClearCacheInternal();
                    dto.FailedCheck = nameof(dto.CanSignTest);
                    dto.Reason = $"Không dùng được khóa bí mật để ký (token chưa cắm, PIN bị hủy, hoặc middleware chưa sẵn sàng): {ex.Message}";
                    return dto;
                }
                dto.CanSignTest = true;

                if (!SignatureVerifier.IsTrustedSignerCert(cert, DateTime.UtcNow))
                {
                    ClearCacheInternal();
                    dto.FailedCheck = nameof(dto.TrustedChain);
                    dto.Reason = "Chứng thư số không thuộc CA Ban Cơ yếu (chain không về Root G1 lẫn Root G2 đã ghim). Chữ ký tạo bằng chứng thư này sẽ bị đánh giá là không hợp lệ.";
                    return dto;
                }
                dto.TrustedChain = true;
                dto.Valid = true;

                _cachedThumbprint = thumbprint;
                _cachedCert = cert;
                _cachedResult = dto;
                return dto;
            }
        }

        public byte[] SignData(string thumbprint, byte[] data)
        {
            lock (_cacheLock)
            {
                if (!string.Equals(_cachedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)
                    || (_cachedRsa == null && _cachedEcdsa == null))
                {
                    throw new InvalidOperationException(
                        "Chưa kiểm tra chứng thư số trước khi ký. Gọi Verify() trước khi bắt đầu lô ký.");
                }

                if (_cachedRsa != null)
                    return _cachedRsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                return _cachedEcdsa!.SignData(data, HashAlgorithmName.SHA256);
            }
        }

        internal (X509Certificate2? Cert, AsymmetricAlgorithm? Key) GetCached(string thumbprint)
        {
            lock (_cacheLock)
            {
                if (!string.Equals(_cachedThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase))
                    return (null, null);

                return (_cachedCert, (AsymmetricAlgorithm?)_cachedRsa ?? _cachedEcdsa);
            }
        }

        public bool IsTokenPresent(string thumbprint)
        {
            return ScanStoreForCertificate(thumbprint) != null;
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                ClearCacheInternal();
            }
        }

        private void ClearCacheInternal()
        {
            _cachedRsa?.Dispose();
            _cachedEcdsa?.Dispose();
            _cachedCert?.Dispose();
            _cachedRsa = null;
            _cachedEcdsa = null;
            _cachedCert = null;
            _cachedResult = null;
            _cachedThumbprint = null;
        }

        internal static X509Certificate2? ScanStoreForCertificate(string? thumbprint)
        {
            if (string.IsNullOrWhiteSpace(thumbprint)) return null;

            foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                try
                {
                    using var store = new X509Store(StoreName.My, location);
                    store.Open(OpenFlags.ReadOnly);
                    var found = store.Certificates
                        .OfType<X509Certificate2>()
                        .FirstOrDefault(c => string.Equals(c.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase));
                    if (found != null) return found;
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
