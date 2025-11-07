//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph.Models;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.Graph.Drives.Item.Items.Item.Checkin;

namespace Cliver
{
    public partial class OneDrive
    {
        public class File : Item
        {
            async public static Task<File> GetAsync(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                Item i = await Item.GetAsync(oneDrive, linkOrEncodedLinkOrShareId);
                if (i == null)
                    return null;
                if (i is File)
                    return (File)i;
                throw new Exception("Link points not to a file: " + linkOrEncodedLinkOrShareId);
            }
            public static File Get(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return RunSync(() => GetAsync(oneDrive, linkOrEncodedLinkOrShareId));
            }

            internal File(OneDrive oneDrive, DriveItem driveItem) : base(oneDrive, driveItem)
            {
            }

            public enum CheckStatus
            {
                NotSupported,
                CheckedOutByNotMe,
                CheckedIn,
                CheckedOut,
            }
            async public Task<CheckStatus> GetCheckStatusAsync()
            {
                if (OneDrive.CheckInIsSupported == false)
                    return CheckStatus.NotSupported;

                var i = await GetDriveItemAsync("id, publication");
                if (i.Publication == null/* checkout is not supported but as of 2024 it is not true anymore */ || string.IsNullOrWhiteSpace(i.Publication.VersionId))
                {
                    OneDrive.CheckInIsSupported = false;
                    return CheckStatus.NotSupported;
                }
                OneDrive.CheckInIsSupported = true;
                string s = i.Publication.Level.ToLower();
                if (s == "published")
                    return CheckStatus.CheckedIn;
                if (s == "checkout")
                {
                    //var user = getCheckedOutUser();
                    //if (user == "Me")
                    return CheckStatus.CheckedOut;
                    //return CheckStatus.CheckedOutByNotMe;
                }
                throw new Exception("Unknown Publication.Level: " + s);
            }
            public CheckStatus GetCheckStatus()
            {
                return OneDrive.RunSync(GetCheckStatusAsync);
            }

            /// <summary>
            /// !!!TBF
            /// </summary>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            async public Task<JObject> GetCheckedOutUserAsync()//To find the specific user, you must use the Microsoft Graph Activity Logs with a query like KQL or Log Analytics
                                                               //to find the checkout activity for that specific file, as there is no direct API to query for the user who checked out a file. 
            {
                //if (SharepointIds == null)
                //    throw new Exception("SharepointIds are NULL while the DriveItem status is CheckedOut.");
                var di = await GetDriveItemAsync("id", "activities");
                //https://stackoverflow.com/questions/51606008/ms-graph-rest-api-checkout-user
                /* "action": {
                "checkout": { }
            },
            "actor": {
                "user": {
                    "email": "XXX@XXX",
                    "displayName": "vladimir",
                    "self": {},
                    "userPrincipalName": "XXX@XXX
                }
            },*/
                var data = di.AdditionalData["activity"];
                JObject jo = JObject.FromObject(data);
                //.Select(a => new JProperty(a.Key, a.Value));
                JObject activities = new JObject(data);
                JObject action = (JObject)activities["action"];
                if (action["action"]?["checkout"] == null)
                    return null;
                return (JObject)activities["actor"]?["user"];
                //Log.Debug0(fieldValueSet.AdditionalData.ToStringByJson());

                //object checkoutUser = fieldValueSet.AdditionalData["CheckoutUser"];
                //if (checkoutUser == null)
                //    throw new Exception("Could not get checkoutUser for the DriveItem.");
            }
            public object GetCheckedOutUser()
            {
                return RunSync(GetCheckedOutUserAsync);
            }

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// </summary>
            /// <param name="throwExceptionIfFailed"></param>
            public async Task<CheckStatus> CheckOutAsync(bool throwExceptionIfFailed = false)
            {
                CheckStatus cs = await GetCheckStatusAsync();
                if (cs == CheckStatus.NotSupported)
                    return cs;
                if (cs == CheckStatus.CheckedOut && CheckIn() != CheckStatus.CheckedIn)//!!!CheckIn() will create a new version. Find another way!
                    if (throwExceptionIfFailed)
                        throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + CheckStatus.CheckedOutByNotMe.ToString());
                    else
                        return CheckStatus.CheckedOutByNotMe;

                await DriveItemRequestBuilder.Checkout.PostAsync();

                MicrosoftTrier mt = new MicrosoftTrier();// sometimes it has a delay in switching status
                await mt.RunAsync(async () => { cs = await GetCheckStatusAsync(); return cs == CheckStatus.CheckedOut ? new Object() : null; }, 2, CheckStatusChangeTimeoutSecs * 1000);
                if (cs != CheckStatus.CheckedOut && throwExceptionIfFailed)
                    throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + cs.ToString());

                return cs;
            }
            public CheckStatus CheckOut(bool throwExceptionIfFailed = false)
            {
                return RunSync(() => CheckOutAsync(throwExceptionIfFailed));
            }

            ///// <summary>
            ///// !!!TBF
            ///// </summary>
            ///// <returns></returns>
            //public List<string> GetCurrentEditors()
            //{
            //    //get who keeps it open (for Excel sheets):                    
            //    DriveItem di = GetDriveItem(null, "activities");
            //    Log.Debug0(di.AdditionalData.ToStringByJson());

            //    Log.Debug0(SharepointIds.ToStringByJson());
            //    //Log.Debug0(ListItem.SharepointIds.ToStringByJson());

            //    object activities = di.AdditionalData["activities"];
            //    Log.Debug0(activities.GetType().ToString());
            //    Log.Debug0(ListItem.AdditionalData.ToStringByJson());

            //    //var t = Task.Run(() =>
            //    //{
            //    //    //!!!GetActivitiesByInterval gives not user names
            //    //    return DriveItemRequestBuilder.GetActivitiesByInterval.GetAsGetActivitiesByIntervalGetResponseAsync(rc =>
            //    //    {
            //    //         rc.QueryParameters.
            //    //    }).(DateTime.Now.AddMinutes(-20).ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.AddMinutes(2).ToString("yyyy-MM-dd HH:mm:ss"), "hour").Request().GetAsync();
            //    //}).Result;
            //    //Log.Debug0(t.ToStringByJson());

            //    //FieldValueSet fieldValueSet = Task.Run(() =>
            //    //{
            //    //    var queryOptions = new List<QueryOption>() { new QueryOption("expand", "activities") };
            //    //    return OneDrive.Client.Sites[SharepointIds.SiteId].Lists[SharepointIds.ListItemId].Items[ItemId].Fields.Request(queryOptions).GetAsync();
            //    //}).Result;//!!!The problem seems to be because of missing oAuth permissions for Sites on the client.
            //    //Log.Debug0(fieldValueSet.AdditionalData.ToStringByJson());

            //    return new List<string> { "test" };
            //}

            /// <summary>
            /// Default time to wait for the check status value to change after check-in and check-out. 
            /// Sometimes it seems to need time to change.
            /// </summary>
            public int CheckStatusChangeTimeoutSecs = 1;

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// (!)It will not check-in if the file already is checked-in, which only makes difference if setting a comment.
            /// </summary>
            /// <param name="comment"></param>
            /// <param name="throwExceptionIfFailed"></param>
            async public Task<CheckStatus> CheckInAsync(string comment = null, bool throwExceptionIfFailed = false)
            {
                CheckStatus cs = await GetCheckStatusAsync();
                if (cs == CheckStatus.NotSupported || cs == CheckStatus.CheckedIn/*(!)otherwise it will create a new version even if it is checked-in*/)
                    return cs;

                if (comment == null)
                    comment = "by " + Log.ProgramName;
                var rb = new CheckinPostRequestBody
                {
                    Comment = comment,
                };
                await DriveItemRequestBuilder.Checkin.PostAsync(rb);

                cs = CheckStatus.NotSupported;
                MicrosoftTrier mt = new MicrosoftTrier();// sometimes it has a delay in switching status
                await mt.RunAsync(async () => { cs = await GetCheckStatusAsync(); return cs == CheckStatus.CheckedIn ? new Object() : null; }, 2, CheckStatusChangeTimeoutSecs * 1000);
                if (cs != CheckStatus.CheckedIn && throwExceptionIfFailed)
                    throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + cs.ToString());
                return cs;
            }
            public CheckStatus CheckIn(string comment = null, bool throwExceptionIfFailed = false)
            {
                return RunSync(() => CheckInAsync(comment, throwExceptionIfFailed));
            }

            async public Task<string> Download2FolderAsync(string localFolder, string localFileName = null)
            {
                if (localFileName == null)
                    localFileName = DriveItem.Name;
                string localFile = localFolder + Path.DirectorySeparatorChar + localFileName;
                await DownloadAsync(localFile);
                return localFile;
            }
            public string Download2Folder(string localFolder, string localFileName = null)
            {
                return RunSync(() => Download2FolderAsync(localFolder, localFileName));
            }

            async public Task DownloadAsync(string localFile)
            {
                using (Stream s = await DriveItemRequestBuilder.Content.GetAsync())
                {
                    using (var fileStream = System.IO.File.Create(localFile))
                    {
                        //s.Seek(0, SeekOrigin.Begin);!!!not supported
                        s.CopyTo(fileStream);
                    }
                }
            }
            public void Download(string localFile)
            {
                RunSync(() => DownloadAsync(localFile));
            }

            async public Task UploadAsync(string localFile)
            {
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem = await DriveItemRequestBuilder.Content.PutAsync(s);
                }
            }
            public void Upload(string localFile)
            {
                RunSync(() => UploadAsync(localFile));
            }
        }
    }
}