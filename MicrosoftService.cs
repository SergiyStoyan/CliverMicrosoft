//Author: Sergey Stoyan
//        systoyan@gmail.com
//        sergiy.stoyan@outlook.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace Cliver
{
    public class MicrosoftService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="scopes"></param>
        /// <param name="microsoftDataStoreUserSettings"></param>
        /// <param name="tenantId">
        /// Multi-tenant apps can use "common",
        /// single-tenant apps must use the tenant ID from the Azure portal
        /// </param>
        public MicrosoftService(string clientId, string[] scopes, MicrosoftDataStoreUserSettings microsoftDataStoreUserSettings, string tenantId = "common")
        {
            ClientId = clientId;
            Scopes = scopes;
            MicrosoftDataStoreUserSettings = microsoftDataStoreUserSettings;
            TenantId = tenantId;

            Client = createClient();
        }
        public readonly string ClientId;
        public readonly string[] Scopes;
        public MicrosoftDataStoreUserSettings MicrosoftDataStoreUserSettings;
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

            application.UserTokenCache.SetAfterAccess(MicrosoftDataStoreUserSettings.AfterAccessNotification);
            application.UserTokenCache.SetBeforeAccess(MicrosoftDataStoreUserSettings.BeforeAccessNotification);
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
                authenticationResult = await application.AcquireTokenInteractive(Scopes).ExecuteAsync();
                account = authenticationResult?.Account;

                if (MicrosoftDataStoreUserSettings.MicrosoftAccount != account.Username)
                {
                    MicrosoftDataStoreUserSettings.MicrosoftAccount = account.Username;
                    MicrosoftDataStoreUserSettings.Save();
                }
            }
        }
        AuthenticationResult authenticationResult = null;

        public void Authenticate()
        {
            Task.Run(() => authenticate()).Wait();
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