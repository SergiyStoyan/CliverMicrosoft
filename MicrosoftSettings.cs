//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
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
    public abstract class MicrosoftSettings : Settings
    {
        /// <summary> 
        /// Multi-tenant apps can use "common", single-tenant apps must use the tenant ID from the Azure portal
        /// </summary>
        [JsonIgnore]
        public virtual string TenantId { get; } = "common";

        /// <summary>
        /// Applicaion's client ID obtained from https://portal.azure.com/
        /// </summary>
        [JsonIgnore]
        public abstract string ClientId { get; }

        /// <summary>
        /// Permission scopes for the application.
        /// </summary>
        [JsonIgnore]
        public abstract string[] Scopes { get; }

        /// <summary>
        /// The user's microsoft account chosen latest.
        /// </summary>
        [JsonProperty]
        public string MicrosoftAccount { get; internal set; }

        protected override void Loaded()
        {
            if (MicrosoftCache == null)
            {
                microsoftCacheBytes = null;
                return;
            }

            if (Endec != null)
            {
                if (MicrosoftCache is string)
                    microsoftCacheBytes = Endec.Decrypt<byte[]>((string)MicrosoftCache);
                else
                {
                    if (MicrosoftCache is JObject)//if Endec was set recently
                    {
                        microsoftCacheBytes = getBytes(MicrosoftCache);
                        Save();
                    }
                    else
                        throw new Exception("MicrosoftCache is an unexpected type: " + MicrosoftCache.GetType());
                }
            }
            else
            {
                if (MicrosoftCache is JObject)
                    microsoftCacheBytes = getBytes(MicrosoftCache);
                else
                    throw new Exception("MicrosoftCache is an unexpected type: " + MicrosoftCache.GetType() + "\r\nConsider removing the config file: " + __Info.File);
            }
        }

        static byte[] getBytes(object @object)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (TextWriter w = new StreamWriter(stream, System.Text.Encoding.ASCII))//!!!if UTF8 then the writer will add 3 bytes BYTE ORDER MARK in the beginning of the stream
                {
                    //System.Text.Json.JsonSerializer.Serialize(stream, value, value.GetType(), new System.Text.Json.JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull | System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault });
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.None;//!!! it must be without formatting!
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    serializer.TypeNameHandling = TypeNameHandling.None;
                    serializer.DefaultValueHandling = DefaultValueHandling.Include;
                    serializer.Serialize(w, @object);
                }
                return stream.ToArray();
            }
        }

        protected override void Saving()
        {
            if (microsoftCacheBytes == null)
            {
                MicrosoftCache = null;
                return;
            }

            if (Endec != null)
                MicrosoftCache = Endec.Encrypt(microsoftCacheBytes);
            else
                using (var stream = new MemoryStream(microsoftCacheBytes))
                using (var reader = new StreamReader(stream, System.Text.Encoding.ASCII))
                    //return System.Text.Json.JsonSerializer.Deserialize(stream, typeof(object));//!!!MSAL seems to use Newtonsoft.Json serialization and not understand System.Text.Json
                    MicrosoftCache = JsonSerializer.Create().Deserialize(reader, typeof(JObject)) as JObject;
        }

        /// <summary>
        /// Set this object in the child class if the cache must be stored encrypted.
        /// </summary>
        virtual protected StringEndec Endec { get; set; } = null;

        /// <summary>
        /// (!)This object is a cache storage by GraphServiceClient and must not be accessed from outside.
        /// </summary>
        [JsonProperty]
        object MicrosoftCache;

        byte[] microsoftCacheBytes;

        public JObject GetMicrosoftCacheClone()
        {
            if (microsoftCacheBytes == null)
                return null;
            using (var stream = new MemoryStream(microsoftCacheBytes))
            using (var reader = new StreamReader(stream, System.Text.Encoding.ASCII))
                return JsonSerializer.Create().Deserialize(reader, typeof(JObject)) as JObject;
        }
        //public string Account()
        //{
        //    return (string)GetMicrosoftCacheClone()?["Account"]?.First()?.First()?["username"];
        //}

        //public string UserName()
        //{
        //    return (string)GetMicrosoftCacheClone()?["Account"]?.First()?.First()?["name"];
        //}

        internal void BeforeAccessMicrosoftCache(TokenCacheNotificationArgs args)
        {
            args.TokenCache.DeserializeMsalV3(microsoftCacheBytes, shouldClearExistingCache: true);
        }

        internal void AfterAccessMicrosoftCache(TokenCacheNotificationArgs args)
        {
            if (!args.HasStateChanged)
                return;
            //(!)Requesting scopes that span multiple resources causes refreshing the access token each call: https://github.com/AzureAD/microsoft-authentication-library-for-js/issues/1240#issuecomment-618030246
            //(!)Frequent refreshing will lead to throttling by the server!
            microsoftCacheBytes = args.TokenCache.SerializeMsalV3();
            Save();
        }

        public void ClearMicrosoftAccount()
        {
            microsoftCacheBytes = null;
            MicrosoftAccount = null;
        }

        /// <summary>
        /// Creates a new instance of the given Settings field with cloned values.
        /// (!)The new instance shares the same __Info and Endec objects with the original instance.
        /// </summary>
        /// <typeparam name="S"></typeparam>
        /// <param name="jsonSerializerSettings">allows to customize cloning</param>
        /// <returns></returns>
        virtual public S Clone<S>(JsonSerializerSettings jsonSerializerSettings = null) where S : MicrosoftSettings, new()
        {
            S s = ((S)this).CreateClone(jsonSerializerSettings);
            s.Endec = Endec;
            s.Loaded();
            return s;
        }

        protected void ImportMicrosoftCacheFrom(MicrosoftSettings microsoftSettings)
        {
            microsoftCacheBytes = microsoftSettings.microsoftCacheBytes;
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