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
            public static File Get(OneDrive oneDrive, Path file)
            {
                Item i = Item.Get(oneDrive, file);
                if (i == null)
                    return null;
                if (i is File)
                    return (File)i;
                throw new Exception("Path points not to a file: " + file);
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
            public CheckStatus GetCheckStatus()
            {
                if (OneDrive.CheckInIsSupported == false)
                    return CheckStatus.NotSupported;

                var i = GetDriveItem("id, publication");
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

            /// <summary>
            /// !!!TBF
            /// </summary>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public object GetCheckedOutUser()
            {
                if (SharepointIds == null)
                    throw new Exception("SharepointIds are NULL while the DriveItem status is CheckedOut.");
                //check if the item is checkedout by someone else
                var ls = OneDrive.Client.Sites[SharepointIds.SiteId].GetAsync().Result;//error: Unable to find target address

                FieldValueSet fieldValueSet = OneDrive.Client.Sites[SharepointIds.SiteId].Lists[ListItem.Id].Items[ItemId].Fields.GetAsync(
                    rc =>
                    {
                        rc.QueryParameters.Expand = new string[] { "CheckoutUser" };
                    }
                ).Result;

                Log.Debug0(fieldValueSet.AdditionalData.ToStringByJson());

                object checkoutUser = fieldValueSet.AdditionalData["CheckoutUser"];
                if (checkoutUser == null)
                    throw new Exception("Could not get checkoutUser for the DriveItem.");
                return checkoutUser;
            }

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// </summary>
            /// <param name="throwExceptionIfFailed"></param>
            public CheckStatus CheckOut(bool throwExceptionIfFailed = false)
            {
                CheckStatus cs = GetCheckStatus();
                if (cs == CheckStatus.NotSupported)
                    return cs;
                if (cs == CheckStatus.CheckedOut && CheckIn() != CheckStatus.CheckedIn)//!!!CheckIn() will create a new version. Find another way!
                    if (throwExceptionIfFailed)
                        throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + CheckStatus.CheckedOutByNotMe.ToString());
                    else
                        return CheckStatus.CheckedOutByNotMe;

                DriveItemRequestBuilder.Checkout.PostAsync().Wait();

                SleepRoutines.WaitForCondition(() =>
                {
                    cs = GetCheckStatus();
                    return cs == CheckStatus.CheckedOut;
                }, CheckStatusChangeTimeoutSecs * 1000, 1000, true, 2);
                if (cs != CheckStatus.CheckedOut && throwExceptionIfFailed)
                    throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + cs.ToString());

                return cs;
            }

            /// <summary>
            /// !!!TBF
            /// </summary>
            /// <returns></returns>
            public List<string> GetCurrentEditors()
            {
                //get who keeps it open (for Excel sheets):                    
                DriveItem di = GetDriveItem(null, "activities");
                Log.Debug0(di.AdditionalData.ToStringByJson());

                Log.Debug0(SharepointIds.ToStringByJson());
                //Log.Debug0(ListItem.SharepointIds.ToStringByJson());

                object activities = di.AdditionalData["activities"];
                Log.Debug0(activities.GetType().ToString());
                Log.Debug0(ListItem.AdditionalData.ToStringByJson());

                //var t = Task.Run(() =>
                //{
                //    //!!!GetActivitiesByInterval gives not user names
                //    return DriveItemRequestBuilder.GetActivitiesByInterval.GetAsGetActivitiesByIntervalGetResponseAsync(rc =>
                //    {
                //         rc.QueryParameters.
                //    }).(DateTime.Now.AddMinutes(-20).ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.AddMinutes(2).ToString("yyyy-MM-dd HH:mm:ss"), "hour").Request().GetAsync();
                //}).Result;
                //Log.Debug0(t.ToStringByJson());

                //FieldValueSet fieldValueSet = Task.Run(() =>
                //{
                //    var queryOptions = new List<QueryOption>() { new QueryOption("expand", "activities") };
                //    return OneDrive.Client.Sites[SharepointIds.SiteId].Lists[SharepointIds.ListItemId].Items[ItemId].Fields.Request(queryOptions).GetAsync();
                //}).Result;//!!!The problem seems to be because of missing oAuth permissions for Sites on the client.
                //Log.Debug0(fieldValueSet.AdditionalData.ToStringByJson());

                return new List<string> { "test" };
            }

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
            public CheckStatus CheckIn(string comment = null, bool throwExceptionIfFailed = false)
            {
                CheckStatus cs = GetCheckStatus();
                if (cs == CheckStatus.NotSupported || cs == CheckStatus.CheckedIn/*(!)otherwise it will create a new version even if it is checked-in*/)
                    return cs;

                if (comment == null)
                    comment = "by " + Log.ProgramName;
                var rb = new CheckinPostRequestBody
                {
                    Comment = comment,
                };
                DriveItemRequestBuilder.Checkin.PostAsync(rb).Wait();

                cs = CheckStatus.NotSupported;
                SleepRoutines.WaitForCondition(() =>
                {
                    cs = GetCheckStatus();
                    return cs == CheckStatus.CheckedIn;
                }, CheckStatusChangeTimeoutSecs * 1000, 1000, true, 2);
                if (cs != CheckStatus.CheckedIn && throwExceptionIfFailed)
                    throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + cs.ToString());
                return cs;
            }

            public string Download2Folder(string localFolder, string localFileName = null)
            {
                if (localFileName == null)
                    localFileName = DriveItem.Name;
                string localFile = localFolder + Path.DirectorySeparatorChar + localFileName;
                Download(localFile);
                return localFile;
            }

            public void Download(string localFile)
            {
                using (Stream s = DriveItemRequestBuilder.Content.GetAsync().Result)
                {
                    using (var fileStream = System.IO.File.Create(localFile))
                    {
                        //s.Seek(0, SeekOrigin.Begin);!!!not supported
                        s.CopyTo(fileStream);
                    }
                }
            }

            public void Upload(string localFile)
            {
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem = DriveItemRequestBuilder.Content.PutAsync(s).Result;
                }
            }
        }
    }
}