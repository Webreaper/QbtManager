using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace QbtManager
{
    public class ServerCertificateValidation
    {
        public ServerCertificateValidation()
        {
            ServicePointManager.ServerCertificateValidationCallback = CertValidationCallBack;
        }


        /// <summary>
        /// Mono doesn't have it's own cert store like .Net, so we need a bit of magic here. See
        /// https://stackoverflow.com/questions/4926676/mono-https-webrequest-fails-with-the-authentication-or-decryption-has-failed
        /// </summary>
        /// <returns><c>true</c>, if validation call back was certed, <c>false</c> otherwise.</returns>
        /// <param name="sender">Sender.</param>
        /// <param name="certificate">Certificate.</param>
        /// <param name="chain">Chain.</param>
        /// <param name="sslPolicyErrors">Ssl policy errors.</param>
        public static bool CertValidationCallBack(System.Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain,
            // look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        continue;
                    }
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                    bool chainIsValid = chain.Build((X509Certificate2)certificate);
                    if (!chainIsValid)
                    {
                        isOk = false;
                        break;
                    }
                }
            }
            return isOk;
        }

    }
}
