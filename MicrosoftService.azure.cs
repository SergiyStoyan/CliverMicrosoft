////Author: Sergiy Stoyan
////        systoyan@gmail.com
////        sergiy.stoyan@outlook.com
////        http://www.cliversoft.com
////********************************************************************************************
//using System;
//using Microsoft.Graph;
//using System.Text.RegularExpressions;
//using System.Linq;
//using Azure.Identity;
//using System.Threading.Tasks;
//using System.IO;

//namespace Cliver
//{
//    /// <summary>
//    /// !!!It needs Azure.Identity package.
//    /// !!!It does not allow to store the token in a custom cache.
//    /// </summary>
//    public class OneDrive2
//    {
//        public OneDrive2(string clientId, string[] scopes, string authenticationRecordFile, string tenantId = "common")
//        {
//            this.clientId = clientId;
//            this.scopes = scopes;
//            this.authenticationRecordFile = authenticationRecordFile;
//            this.tenantId = tenantId;
//        }
//        readonly string clientId;
//        readonly string[] scopes;
//        readonly string authenticationRecordFile;
//        readonly string tenantId;

//        public async Task createClient()
//        {
//            AuthenticationRecord authenticationRecord = null;

//            if (System.IO.File.Exists(authenticationRecordFile))
//                using (var s = new FileStream(authenticationRecordFile, FileMode.Open, FileAccess.Read))
//                    authenticationRecord = AuthenticationRecord.Deserialize(s);

//            var options = new InteractiveBrowserCredentialOptions
//            {
//                TenantId = tenantId,
//                ClientId = clientId,
//                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
//                RedirectUri = new Uri("http://localhost"),
//                AuthenticationRecord = authenticationRecord,
//                TokenCachePersistenceOptions = new TokenCachePersistenceOptions { /*UnsafeAllowUnencryptedStorage = true,*/ /*Name = "testName"*/ },
//            };
//            var interactiveCredential = new InteractiveBrowserCredential(options);
//            Azure.Core.TokenRequestContext context = new Azure.Core.TokenRequestContext(scopes);
//            //authenticationRecord = interactiveCredential.Authenticate(context);
//            Azure.Core.AccessToken token = interactiveCredential.GetToken(context);

//            using (var s = new FileStream(authenticationRecordFile, FileMode.Create, FileAccess.Write))
//                authenticationRecord.Serialize(s);

//            client = new GraphServiceClient(interactiveCredential);
//        }

//        GraphServiceClient client;
//    }
//}