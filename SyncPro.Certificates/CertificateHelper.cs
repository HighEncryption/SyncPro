namespace SyncPro.Certificates
{
    using System;
    using System.Security.Cryptography.X509Certificates;

    using CERTENROLLLib;

    public static class CertificateHelper
    {
        public static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
        {
            var distinguishedName = new CX500DistinguishedName();
            distinguishedName.Encode(
                "CN=" + subjectName, 
                X500NameFlags.XCN_CERT_NAME_STR_NONE);

            CCspInformations objCSPs = new CCspInformations();
            CCspInformation objCSP = new CCspInformation();

            objCSP.InitializeFromName(
                "Microsoft Enhanced RSA and AES Cryptographic Provider");

            objCSPs.Add(objCSP);

            // Build the private key
            CX509PrivateKey privateKey = new CX509PrivateKey();

            privateKey.MachineContext = false;
            privateKey.Length = 2048;
            privateKey.CspInformations = objCSPs;
            privateKey.KeySpec = X509KeySpec.XCN_AT_KEYEXCHANGE;
            privateKey.KeyUsage = X509PrivateKeyUsageFlags.XCN_NCRYPT_ALLOW_ALL_USAGES;
            privateKey.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;

            // Create the private key in the CSP's protected storage
            privateKey.Create();

            // Build the algorithm identifier
            var hashobj = new CObjectId();
            hashobj.InitializeFromAlgorithmName(
                ObjectIdGroupId.XCN_CRYPT_HASH_ALG_OID_GROUP_ID,
                ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY,
                AlgorithmFlags.AlgorithmFlagsNone, 
                "SHA256");

            // Create the self-signing request from the private key
            var certificateRequest = new CX509CertificateRequestCertificate();
            certificateRequest.InitializeFromPrivateKey(
                X509CertificateEnrollmentContext.ContextUser, 
                privateKey, 
                string.Empty);

            certificateRequest.Subject = distinguishedName;
            certificateRequest.Issuer = distinguishedName;
            certificateRequest.NotBefore = DateTime.Now.AddDays(-1);
            certificateRequest.NotAfter = DateTime.Now.AddYears(100);
            certificateRequest.HashAlgorithm = hashobj;

            certificateRequest.Encode();

            var enrollment = new CX509Enrollment();

            // Load the certificate request
            enrollment.InitializeFromRequest(certificateRequest); 
            enrollment.CertificateFriendlyName = subjectName;

            // Output the request in base64 and install it back as the response
            string csr = enrollment.CreateRequest();

            // Install the response
            enrollment.InstallResponse(
                InstallResponseRestrictionFlags.AllowUntrustedCertificate,
                csr, 
                EncodingType.XCN_CRYPT_STRING_BASE64, 
                string.Empty);

            // Get the new certificate without the private key
            byte[] certificateData = Convert.FromBase64String(enrollment.Certificate);

            return new X509Certificate2(certificateData);
        }
    }
}
