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

            public enum CheckStatus
            {
                NotSupported,
                CheckedOutByNotMe,
                CheckedIn,
                CheckedOut,
            }
            public CheckStatus GetCheckStatus()
            {
                lock (this)
                {
                    var i = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.Request().Select("id, publication").GetAsync();
                    }).Result;
                    //Log.Inform0(i.ToStringByJson());
                    if (i.Publication == null)//if NULL then checkout is not supported
                        return CheckStatus.NotSupported;
                    string s = i.Publication.Level.ToLower();
                    if (s == "published")
                        return CheckStatus.CheckedIn;
                    if (s == "checkout")
                        return CheckStatus.CheckedOut;
                    throw new Exception("Unknown Publication.Level: " + s);
                }
            }

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// </summary>
            public CheckStatus CheckOut()
            {
                lock (this)
                {
                    CheckStatus cs = GetCheckStatus();
                    if (cs == CheckStatus.NotSupported)
                        return cs;
                    if (cs == CheckStatus.CheckedOut)
                        return CheckStatus.CheckedOutByNotMe;

                    Task.Run(() =>
                    {
                        DriveItemRequestBuilder.Checkout().Request().PostAsync();//not supported for a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
                    }).Wait();

                    return GetCheckStatus();
                }
            }

            /// <summary>
            /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            /// </summary>
            /// <param name="comment"></param>
            public CheckStatus CheckIn(string comment = null)
            {
                if (comment == null)
                    comment = "by " + Log.ProgramName;
                lock (this)
                {
                    Task.Run(() =>
                    {
                        DriveItemRequestBuilder.Checkin(/*"published"*/null, comment).Request().PostAsync();//not supported for a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
                    }).Wait();

                    return GetCheckStatus();
                }
            }

            public void Download(string localFile)
            {
                lock (this)
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
            }

            public void Upload(string localFile)
            {
                lock (this)
                {
                    using (Stream s = System.IO.File.OpenRead(localFile))
                    {
                        DriveItem = Task.Run(() =>
                        {
                            return DriveItemRequestBuilder.Content.Request().PutAsync<DriveItem>(s);
                        }).Result;
                    }
                }
            }
        }
    }
}