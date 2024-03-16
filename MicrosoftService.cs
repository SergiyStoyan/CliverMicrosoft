//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
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
        public MicrosoftService(MicrosoftSettings microsoftSettings)
        {
            MicrosoftSettings = microsoftSettings;
            Client = createClient();
        }
        public readonly MicrosoftSettings MicrosoftSettings;

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
            application = PublicClientApplicationBuilder.Create(MicrosoftSettings.ClientId)
            .WithTenantId(MicrosoftSettings.TenantId)
            .WithRedirectUri("http://localhost")//to use the default browser
            .Build();

            //var storageProperties = new Microsoft.Identity.Client.Extensions.Msal.StorageCreationPropertiesBuilder(PathRoutines.GetFileName(TokenFile), PathRoutines.GetFileDir(TokenFile))
            //    .WithUnprotectedFile()//!!!non-encrypted!!!
            //    .Build();
            //var cacheHelper = await Microsoft.Identity.Client.Extensions.Msal.MsalCacheHelper.CreateAsync(storageProperties);
            //cacheHelper.RegisterCache(application.UserTokenCache);

            application.UserTokenCache.SetAfterAccess(MicrosoftSettings.AfterAccessNotification);
            application.UserTokenCache.SetBeforeAccess(MicrosoftSettings.BeforeAccessNotification);
            //application.UserTokenCache.SetBeforeWrite((TokenCacheNotificationArgs a) => { });
            //application.UserTokenCache.SetCacheOptions(new CacheOptions { UseSharedCache = false });

            if (string.IsNullOrWhiteSpace(MicrosoftSettings.MicrosoftAccount))
                account = Task.Run(() => application.GetAccountsAsync()).Result.FirstOrDefault();
            else
                account = Task.Run(() => application.GetAccountsAsync()).Result.FirstOrDefault(a => a.Username == MicrosoftSettings.MicrosoftAccount);
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
                authenticationResult = await application.AcquireTokenSilent(MicrosoftSettings.Scopes, account).ExecuteAsync();
            }
            catch (MsalUiRequiredException e)
            {
                //if (e.ErrorCode != MsalError.InvalidGrantError && e.ErrorCode != MsalError.UserNullError /* || e.Classification == UiRequiredExceptionClassification.None*/)
                //    throw;
                OnInteractiveAuthentication?.Invoke();
                authenticationResult = await application.AcquireTokenInteractive(MicrosoftSettings.Scopes)
                    //.WithUseEmbeddedWebView(true)!!!intermittently gives the error (even when running in an STA thread): ActiveX control '8856f961-340a-11d0-a96b-00c04fd705a2' cannot be instantiated because the current thread is not in a single-threaded apartment. 
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync();
                account = authenticationResult?.Account;

                if (MicrosoftSettings.MicrosoftAccount != account.Username)
                {
                    MicrosoftSettings.MicrosoftAccount = account.Username;
                    MicrosoftSettings.Save();
                }
            }
        }
        AuthenticationResult authenticationResult = null;

        public Action OnInteractiveAuthentication = null;

        /*public void Authenticate2()//WithUseEmbeddedWebView(true)
        {
            Task.Run(async () => { await authenticate(); }).Wait();//!!!on the client's computer it gave: ActiveX control '8856f961-340a-11d0-a96b-00c04fd705a2' cannot be instantiated because the current thread is not in a single-threaded apartment. 

            //ThreadRoutines.StartTrySta(authenticate().Wait).Join();//!!!intermittently freezes

            //if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
            //TaskRoutines.RunSynchronously(authenticate);//!!!on the client's computer it gave: ActiveX control '8856f961-340a-11d0-a96b-00c04fd705a2' cannot be instantiated because the current thread is not in a single-threaded apartment. 
            //else
            //   ThreadRoutines.StartTrySta(() => { TaskRoutines.RunSynchronously(authenticate); }).Join();//feezes
        }*/
        public void Authenticate()//WithUseEmbeddedWebView(false)
        {
            //authenticate().Wait();//never returns from AcquireTokenInteractive()
            //Task.Run(async () => { await authenticate(); }).Wait();//freezes at OnInteractiveAuthentication() 
            //Task.Run(() => { authenticate(); }).Wait();//freezes at OnInteractiveAuthentication() 

            //ThreadRoutines.StartTrySta(authenticate().Wait).Join();//!!!intermittently freezes

            //if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
            TaskRoutines.RunSynchronously(authenticate);//???works reliably?
            //else
            //   ThreadRoutines.StartTrySta(() => { TaskRoutines.RunSynchronously(authenticate); }).Join();//feezes
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

        public User User
        {
            get
            {
                if (user == null)
                    user = GetUser(null);
                return user;
            }
        }
        User user = null;
    }
}