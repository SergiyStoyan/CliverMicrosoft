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
using Microsoft.Graph.Models;

namespace Cliver
{
    /// <summary>
    /// (!!!)Microsoft.Graph 4,5 have the issue with hanging on GetAsync().Result in some methods. 
    /// It happens due to using HttpClient. 
    /// See: https://stackoverflow.com/questions/10343632/httpclient-getasync-never-returns-when-using-await-async
    /// My following tests failed:
    /// GetAsync().Result;
    /// GetAsync().ConfigureAwait(false).GetAwaiter().GetResult(); (confirmed by https://stackoverflow.com/questions/54372407/still-confused-on-configureawaitfalse-used-with-getawaiter-and-getresult-in-c)
    /// The only reliable solution is wraping the calls like this: Task.Run(() => { return client.Users.Request().GetAsync();}).Result;
    /// Otherwise it must called in async/await mode.
    /// Which exactly methods do not hang is unclear, so almost all the calls are wrapped.
    /// See: https://stackoverflow.com/questions/55944518/microsoft-graph-api-call-hangs-indefinitely      
    /// https://stackoverflow.com/questions/55105321/microsoft-graph-getasync-hangs-indefinitely
    /// </summary>
    public partial class OneDrive : MicrosoftService
    {
        public OneDrive(MicrosoftSettings microsoftSettings) : base(microsoftSettings)
        {
        }
        public bool? CheckInIsSupported { get; internal set; } = null;

        //        public void test(string itemId)
        //        {
        //            var i = Task.Run(() =>
        //            {/*
        //              .../me/drive/root/children
        //Drive on SharePoint's sites works in the same way, but instead of me you should provide global Id of the site you want to access (global Id is <hostName>,<siteCollectionId>,<siteId>).
        //In conclusion: this endpoint gives us a list of files on a specified site's default drive:
        //.../Sharepoint/sites/<hostName>,<siteCollectionId>,<siteId>/drive/root/children
        //If you want to access files on a specific list, all you need is the id of the list:
        //.../Sharepoint/sites/<hostName>,<siteCollectionId>,<siteId>/lists/<listId>/drive/root/children
        //                */

        //                string siteId = Regex.Replace(itemId, @"(?<=.*?sharepoint.com).*$", "");
        //                //Log.Inform(site)
        //                string driveId = Regex.Replace(itemId, @"(\!.*)", "");
        //                IDriveItemRequestBuilder driveItemRequestBuilder = Client.Sites[siteId].Drives[driveId].Items[itemId];
        //                return driveItemRequestBuilder.Request().Select("id, publication").GetAsync();

        //                //return getDriveItemRequestBuilder(itemId).Request().Select("id, Shared, CreatedBy, CreatedByUser, name").GetAsync();
        //                //return Client.Shares[itemId].DriveItem.Request().Select("id, name, shared").GetAsync();
        //            }).Result;
        //        }

        public Drive UserDrive
        {
            get
            {
                if (_UserDrive != null)
                    _UserDrive = Task.Run(() => { return Client.Users[User.Id].Drive.GetAsync(); }).Result;
                return _UserDrive;
            }
        }
        Drive _UserDrive;


        public DriveItem GetRootDriveItem(string driveId)
        {
            return Task.Run(() => { return Client.Drives[driveId].Root.GetAsync(); }).Result;
        }

        /// <summary>
        /// !!!when 'Can view' a user still can hange the file! Probabaly it is due to 'anybody with this link can edit the file'
        /// 
        /// Locks by removing shared premissions.
        /// (!)The owner remains to be able to edit.
        /// Drawbacks:
        /// - can be called only by the owner;
        /// - the owner still can change the item from outside the app;
        /// Advantages:
        /// - easily works for folders and anything;
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="readOnly"></param>
        //void changePermissions(string itemId, bool readOnly)
        //{
        //    lock (this)
        //    {
        //        try
        //        {
        //            if (!MicrosoftUserSettings.ItemIds2PermissionIds2Roles.TryGetValue(itemId, out var permissionIds2Roles))
        //            {
        //                if (!readOnly)//it is a repeated unlock
        //                    return;
        //                var ps = Task.Run(() =>
        //                {
        //                    return getDriveItemRequestBuilder(itemId).Permissions.Request().GetAsync();
        //                }).Result;
        //                permissionIds2Roles = new Dictionary<string, List<string>>();
        //                foreach (var p in ps.Where(a => a.GrantedTo != null))
        //                    permissionIds2Roles[p.Id] = p.Roles.ToList();
        //                MicrosoftUserSettings.ItemIds2PermissionIds2Roles[itemId] = permissionIds2Roles;
        //                MicrosoftUserSettings.Save();
        //            }

        //            foreach (string permissionId in permissionIds2Roles.Keys)
        //            {
        //                if (readOnly && permissionIds2Roles[permissionId].First(a => a != "read") == null)
        //                    continue;
        //                Task.Run(() =>
        //                {
        //                    Permission p2 = new Permission { Roles = readOnly ? new List<string> { "read" } : permissionIds2Roles[permissionId] };
        //                    return getDriveItemRequestBuilder(itemId).Permissions[permissionId].Request().UpdateAsync(p2);
        //                }).Wait();
        //            }

        //            if (!readOnly)
        //            {
        //                MicrosoftUserSettings.ItemIds2PermissionIds2Roles.Remove(itemId);
        //                MicrosoftUserSettings.Save();
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            for (Exception ee = e; ee != null; ee = e.InnerException)
        //            {
        //                Microsoft.Graph.ServiceException se = ee as Microsoft.Graph.ServiceException;
        ///*Microsoft.Graph.ServiceException*/        Microsoft.Kiota.Abstractions.ApiException
        //                if (se?.Error.Code == "itemNotFound")
        //                    throw new Exception("User " + User.DisplayName + " cannot change permissions of the item[id=" + itemId + "] because it is not owned.", e);
        //            }
        //            throw;
        //        }
        //    }
        //}

        /// <summary>
        /// It works for either shared or not shared items.
        /// Expected to work for links of any form:
        /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
        /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
        /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
        /// </summary>
        /// <param name="linkOrEncodedLinkOrShareId"></param>
        /// <returns></returns>
        public Item GetItem(string linkOrEncodedLinkOrShareId)
        {
            DriveItem di = null;
            try
            {
                di = Task.Run(() => { return Client.Shares[GetEncodedLinkOrShareId(linkOrEncodedLinkOrShareId)].DriveItem.GetAsync(); }).Result;
            }
            catch (Exception e)
            {
                for (; e != null; e = e.InnerException)
                    if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException ex && (int)System.Net.HttpStatusCode.NotFound == ex.ResponseStatusCode)
                        return null;
                throw;
            }
            return Item.New(this, di);
        }

        public Item GetItemByRootPath(string rootPath)
        {
            string escapedRelativePath = GetEscapedPath(rootPath);//(!)the API always tries to unescape
            DriveItem di = null;
            try
            {
                di = Task.Run(() => { return Client.Drives[UserDrive.Id].Root.ItemWithPath(escapedRelativePath).GetAsync(); }).Result;
            }
            catch (Exception e)
            {
                for (; e != null; e = e.InnerException)
                    if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException ex && (int)System.Net.HttpStatusCode.NotFound == ex.ResponseStatusCode)
                        return null;
                throw;
            }
            return Item.New(this, di);
        }

        public Folder GetFolder(string linkOrEncodedLinkOrShareId)
        {
            return (Folder)GetItem(linkOrEncodedLinkOrShareId);
        }

        public File GetFile(string linkOrEncodedLinkOrShareId)
        {
            return (File)GetItem(linkOrEncodedLinkOrShareId);
        }

        /// <summary>
        /// (!)OneDrive API always tries to url-unescape path arguments.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetEscapedPath(string path)
        {
            //return Regex.Replace(path, @"\%", @"%25");

            if (!path.Contains('%'))//(!)The server always tries to url-decode
                return path;
            string[] ps = path.Split('\\', '/');
            for (int i = 0; i < ps.Length; i++)
                ps[i] = Uri.EscapeDataString(ps[i]);
            return string.Join("\\", ps);
        }

        /// <summary>
        /// Provides argument for Client.Shares[shareIdOrEncodedSharingUrl].
        /// Expected to work for links of any form:
        /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
        /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
        /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
        /// Encoded link or shareId is retruned unchanged.
        /// </summary>
        /// <param name="linkOrEncodedLinkOrShareId"></param>
        /// <returns></returns>
        static public string GetEncodedLinkOrShareId(string linkOrEncodedLinkOrShareId)
        {
            if (Regex.IsMatch(linkOrEncodedLinkOrShareId, @"^\s*(u|s)\!"))
                return linkOrEncodedLinkOrShareId;
            string base64Value = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(linkOrEncodedLinkOrShareId));
            return "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
        }

        public static bool SplitPath(string relativePath, out string parentFolder, out string itemName, bool removeTrailingSeparator = true)
        {
            Match m = Regex.Match(relativePath, @"(.*)[\\\/]+([^\\]+)$");
            if (!m.Success)
            {
                parentFolder = null;
                itemName = clear(relativePath);
                return false;
            }
            parentFolder = clear(m.Groups[1].Value);
            itemName = clear(m.Groups[2].Value);
            return true;

            string clear(string fd)
            {
                if (removeTrailingSeparator)
                    return fd.TrimEnd('\\', '/');
                return fd;
            }
        }

        /// <summary>
        /// Provides argument for Client.Shares[shareIdOrEncodedSharingUrl].
        /// Expected to work for links of any form:
        /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
        /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
        /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
        /// Encoded link or shareId is retruned unchanged.
        /// </summary>
        /// <param name="linkOrEncodedLinkOrShareId"></param>
        /// <returns></returns>
        public static bool IsLinkOneDrive(string linkOrEncodedLinkOrShareId)
        {
            return Regex.IsMatch(linkOrEncodedLinkOrShareId, @"^\s*(https\://(onedrive\.live\.com|1drv\.ms)[\/\?]|u!)", RegexOptions.IgnoreCase);
        }
    }
}