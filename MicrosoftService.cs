//Author: Sergiy Stoyan
//        systoyan@gmail.com
//        sergiy.stoyan@outlook.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using System.Collections.Generic;

namespace Cliver
{
    public class MicrosoftService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="scopes"></param>
        /// <param name="microsoftUserSettings"></param>
        /// <param name="tenantId">
        /// Multi-tenant apps can use "common",
        /// single-tenant apps must use the tenant ID from the Azure portal
        /// </param>
        public MicrosoftService(string clientId, IEnumerable<string> scopes, MicrosoftUserSettings microsoftUserSettings, string tenantId = "common")
        {
            ClientId = clientId;
            Scopes = scopes;
            MicrosoftUserSettings = microsoftUserSettings;
            TenantId = tenantId;

            Client = createClient();
        }
        public readonly string ClientId;
        public readonly IEnumerable<string> Scopes;
        public readonly MicrosoftUserSettings MicrosoftUserSettings;
        public readonly string TenantId;

        public string MicrosoftAccount
        {
            get
            {
                if (account == null)
                    Authenticate();
                return account?.Username;
            }
        }

        public GraphServiceClient Client { get; private set; }

        GraphServiceClient createClient()
        {
            application = PublicClientApplicationBuilder.Create(ClientId)
            .WithTenantId(TenantId)
            .WithRedirectUri("http://localhost")//to use the default browser
            .Build();

            //var storageProperties = new Microsoft.Identity.Client.Extensions.Msal.StorageCreationPropertiesBuilder(PathRoutines.GetFileName(TokenFile), PathRoutines.GetFileDir(TokenFile))
            //    .WithUnprotectedFile()//!!!non-encrypted!!!
            //    .Build();
            //var cacheHelper = await Microsoft.Identity.Client.Extensions.Msal.MsalCacheHelper.CreateAsync(storageProperties);
            //cacheHelper.RegisterCache(application.UserTokenCache);

            application.UserTokenCache.SetAfterAccess(MicrosoftUserSettings.AfterAccessNotification);
            application.UserTokenCache.SetBeforeAccess(MicrosoftUserSettings.BeforeAccessNotification);
            //application.UserTokenCache.SetBeforeWrite((TokenCacheNotificationArgs a) => { });
            //application.UserTokenCache.SetCacheOptions(new CacheOptions { UseSharedCache = false });

            account = Task.Run(() => application.GetAccountsAsync()).Result.FirstOrDefault();
            return new GraphServiceClient(new DelegateAuthenticationProvider(async (requestMessage) =>
            {
                await authenticate();
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", authenticationResult.AccessToken);
            }));
        }
        IPublicClientApplication application;
        IAccount account = null;
        async Task authenticate()
        {
            try
            {
                authenticationResult = await application.AcquireTokenSilent(Scopes, account).ExecuteAsync();
            }
            catch (MsalUiRequiredException e)
            {
                //if (e.ErrorCode != MsalError.InvalidGrantError && e.ErrorCode != MsalError.UserNullError /* || e.Classification == UiRequiredExceptionClassification.None*/)
                //    throw;
                OnInteractiveAuthentication?.Invoke();
                authenticationResult = await application.AcquireTokenInteractive(Scopes).ExecuteAsync();
                account = authenticationResult?.Account;

                if (MicrosoftUserSettings.MicrosoftAccount != account.Username)
                {
                    MicrosoftUserSettings.MicrosoftAccount = account.Username;
                    MicrosoftUserSettings.Save();
                }
            }
        }
        AuthenticationResult authenticationResult = null;

        public Action OnInteractiveAuthentication = null;

        public void Authenticate()
        {
            //Task.Run(() => authenticate()).Wait();!!!on the client's computer it gave:
            //ActiveX control '8856f961-340a-11d0-a96b-00c04fd705a2' cannot be instantiated because the current thread is not in a single-threaded apartment. 
            var t = ThreadRoutines.StartTrySta(authenticate().Wait);
            t.Join();
        }

        public TimeSpan Timeout
        {
            get
            {
                return Client.HttpProvider.OverallTimeout;
            }
            set
            {
                Client.HttpProvider.OverallTimeout = value;
            }
        }

        public User GetUser(string userId = null)
        {
            return Task.Run(() =>
            {
                if (userId == null)
                    return Client.Me.Request().GetAsync();
                else
                    return Client.Users[userId].Request().GetAsync();
            }).Result;
        }
    }
}