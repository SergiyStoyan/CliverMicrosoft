//Author: Sergey Stoyan
//        systoyan@gmail.com
//        sergiy.stoyan@outlook.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Identity.Client;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cliver
{
    public class MicrosoftDataStoreUserSettings : Cliver.UserSettings
    {
        /// <summary>
        /// The user's microsoft account chosen latest.
        /// </summary>
        [JsonProperty]
        public string MicrosoftAccount { get; internal set; }

        /// <summary>
        /// Used for Lock/Unlock items
        /// </summary>
        [JsonProperty]
        internal Dictionary<string, Dictionary<string, List<string>>> ItemIds2PermissionIds2Roles = new Dictionary<string, Dictionary<string, List<string>>>();

        /// <summary>
        /// (!)This object is a cache storage by GraphServiceClient and must not be accessed from outside.
        /// </summary>
        [JsonProperty]
        protected JObject MicrosoftCache
        {
            get
            {
                if (microsoftCacheBytes == null)
                    return null;
                using (var stream = new MemoryStream(microsoftCacheBytes))
                using (var reader = new StreamReader(stream, System.Text.Encoding.ASCII))
                    //return System.Text.Json.JsonSerializer.Deserialize(stream, typeof(object));//!!!MSAL seems to use Newtonsoft.Json serialization and do not understand System.Text.Json
                    return JsonSerializer.Create().Deserialize(reader, typeof(JObject)) as JObject;
            }
            set
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (TextWriter w = new StreamWriter(stream, System.Text.Encoding.ASCII))//!!!if UTF8 then the writer will 3 bytes BYTE ORDER MARK in the beginning of the stream
                    {
                        //System.Text.Json.JsonSerializer.Serialize(stream, value, value.GetType(), new System.Text.Json.JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull | System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault });
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Formatting = Formatting.None;//!!! it must be without formatting!
                        serializer.NullValueHandling = NullValueHandling.Ignore;
                        serializer.TypeNameHandling = TypeNameHandling.None;
                        serializer.DefaultValueHandling = DefaultValueHandling.Include;
                        serializer.Serialize(w, value);
                    }
                    microsoftCacheBytes = stream.ToArray();
                }
            }
        }
        byte[] microsoftCacheBytes;

        //public string Account()
        //{
        //    return (string)MicrosoftCache?["Account"]?.First()?.First()?["username"];
        //}

        //public string UserName()
        //{
        //    return (string)MicrosoftCache?["Account"]?.First()?.First()?["name"];
        //}

        internal void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            if (microsoftCacheBytes == null)
                return;
            args.TokenCache.DeserializeMsalV3(microsoftCacheBytes, shouldClearExistingCache: true);
        }

        internal void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (!args.HasStateChanged)
                return;
            microsoftCacheBytes = args.TokenCache.SerializeMsalV3();
            Save();
        }

        public void ClearMicrosoftAccount()
        {
            microsoftCacheBytes = null;
            MicrosoftAccount = null;
        }
    }

    //class Cache : ITokenCache
    //{
    //    public void Deserialize(byte[] msalV2State)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void DeserializeAdalV3(byte[] adalV3State)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void DeserializeMsalV2(byte[] msalV2State)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void DeserializeMsalV3(byte[] msalV3State, bool shouldClearExistingCache = false)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void DeserializeUnifiedAndAdalCache(CacheData cacheData)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public byte[] Serialize()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public byte[] SerializeAdalV3()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public byte[] SerializeMsalV2()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public byte[] SerializeMsalV3()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public CacheData SerializeUnifiedAndAdalCache()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void SetAfterAccess(TokenCacheCallback afterAccess)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void SetAfterAccessAsync(Func<TokenCacheNotificationArgs, Task> afterAccess)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void SetBeforeAccess(TokenCacheCallback beforeAccess)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void SetBeforeAccessAsync(Func<TokenCacheNotificationArgs, Task> beforeAccess)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void SetBeforeWrite(TokenCacheCallback beforeWrite)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void SetBeforeWriteAsync(Func<TokenCacheNotificationArgs, Task> beforeWrite)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}