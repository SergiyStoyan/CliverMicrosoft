//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using Microsoft.Graph;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Cliver
{
    public partial class OneDrive
    {
        public class File : Item
        {
            internal File(OneDrive oneDrive, DriveItem driveItem) : base(oneDrive, driveItem)
            {
            }

            //public bool IsCheckInSupported
            //{
            //    get
            //    {
            //        if (checkInIsSupported == null)
            //            checkInIsSupported = GetCheckStatus() != CheckStatus.NotSupported;
            //        return (bool)checkInIsSupported;
            //    }
            //}
            //bool? checkInIsSupported = null;

            public enum CheckStatus
            {
                NotSupported,
                CheckedOutByNotMe,
                CheckedIn,
                CheckedOut,
            }
            public CheckStatus GetCheckStatus()
            {
                var i = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.Request().Select("id, publication").GetAsync();
                }).Result;
                //Log.Debug0(i.ToStringByJson());
                if (i.Publication == null)//if NULL then checkout is not supported
                    return CheckStatus.NotSupported;
                string s = i.Publication.Level.ToLower();
                if (s == "published")
                    return CheckStatus.CheckedIn;
                if (s == "checkout")
                    return CheckStatus.CheckedOut;
                throw new Exception("Unknown Publication.Level: " + s);
            }

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// </summary>
            public CheckStatus CheckOut(bool throwExceptionIfFailed = false)
            {
                CheckStatus cs = GetCheckStatus();
                if (cs == CheckStatus.NotSupported)
                    return cs;
                if (cs == CheckStatus.CheckedOut && CheckIn() != CheckStatus.CheckedIn)
                    //TBD to improve diagnostic, get whom it checked out to:
                    //https://graph.microsoft.com/v1.0/sites/{site-id}/lists/{list-id}/items/{item-id}/fields/$expand=CheckoutUser
                    //OneDrive.Client.Me.Drives[DriveId].List.Items[ItemId].Fields.Request().Expand("CheckoutUser").GetAsync();!!!List is NULL
                    if (throwExceptionIfFailed)
                        throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + CheckStatus.CheckedOutByNotMe.ToString());
                    else
                        return CheckStatus.CheckedOutByNotMe;

                Task.Run(() =>
                {
                    DriveItemRequestBuilder.Checkout().Request().PostAsync();//not supported for a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
                }).Wait();

                cs = GetCheckStatus();
                if (cs != CheckStatus.CheckedOut && throwExceptionIfFailed)
                    throw new Exception(Cliver.Log.GetThisMethodName() + " failed on the file:\r\n" + DriveItem.WebUrl + "\r\nCheck status of the file: " + cs.ToString());
                return cs;
            }

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// </summary>
            /// <param name="comment"></param>
            public CheckStatus CheckIn(string comment = null, bool throwExceptionIfFailed = false)
            {
                if (comment == null)
                    comment = "by " + Log.ProgramName;
                Task.Run(() =>
                {
                    DriveItemRequestBuilder.Checkin(/*"published"*/null, comment).Request().PostAsync();//not supported for a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
                }).Wait();

                CheckStatus cs = GetCheckStatus();
                if (cs != CheckStatus.NotSupported && cs != CheckStatus.CheckedIn && throwExceptionIfFailed)
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
                using (Stream s = Task.Run(() =>
                {
                    return DriveItemRequestBuilder.Content.Request().GetAsync();
                }).Result
                    )
                {
                    using (var fileStream = System.IO.File.Create(localFile))
                    {
                        s.Seek(0, SeekOrigin.Begin);
                        s.CopyTo(fileStream);
                    }
                }
            }

            public void Upload(string localFile)
            {
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    DriveItem = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.Content.Request().PutAsync<DriveItem>(s);
                    }).Result;
                }
            }

            public Folder GetFolder()
            {
                return (Folder)GetParent();
            }
        }
    }
}