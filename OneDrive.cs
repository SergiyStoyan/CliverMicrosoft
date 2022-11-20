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
    public partial class OneDrive : MicrosoftService
    {
        public OneDrive(string clientId, string[] scopes, MicrosoftUserSettings microsoftUserSettings, string tenantId = "common")
            : base(clientId, scopes, microsoftUserSettings, tenantId)
        {
        }

        public void test(string itemId)
        {
            var i = Task.Run(() =>
            {/*
              .../me/drive/root/children
Drive on SharePoint's sites works in the same way, but instead of me you should provide global Id of the site you want to access (global Id is <hostName>,<siteCollectionId>,<siteId>).
In conclusion: this endpoint gives us a list of files on a specified site's default drive:
.../Sharepoint/sites/<hostName>,<siteCollectionId>,<siteId>/drive/root/children
If you want to access files on a specific list, all you need is the id of the list:
.../Sharepoint/sites/<hostName>,<siteCollectionId>,<siteId>/lists/<listId>/drive/root/children
                */

                string siteId = Regex.Replace(itemId, @"(?<=.*?sharepoint.com).*$", "");
                //Log.Inform(site)
                string driveId = Regex.Replace(itemId, @"(\!.*)", "");
                IDriveItemRequestBuilder driveItemRequestBuilder = Client.Sites[siteId].Drives[driveId].Items[itemId];
                return driveItemRequestBuilder.Request().Select("id, publication").GetAsync();

                //return getDriveItemRequestBuilder(itemId).Request().Select("id, Shared, CreatedBy, CreatedByUser, name").GetAsync();
                //return Client.Shares[itemId].DriveItem.Request().Select("id, name, shared").GetAsync();
            }).Result;
        }

        public Item GetItemByPath(string path)
        {
            lock (this)
            {
                DriveItem driveItem = Task.Run(() =>
                {
                    return Client.Me.Drive.Root.ItemWithPath(path).Request().GetAsync();
                }).Result;
                return Item.Get(this, driveItem);
            }
        }

        //public bool LockItem(string itemId, bool changePermissionsIfCheckOutIsNotSupported)
        //{
        //    lock (this)
        //    {
        //        var s = CheckOut(itemId);
        //        if (s == ItemCheckStatus.CheckedOut)
        //            return true;
        //        if (s == ItemCheckStatus.CheckedIn)
        //            return false;
        //        if (!changePermissionsIfCheckOutIsNotSupported)
        //            return false;
        //        changePermissions(itemId, true);
        //        return true;
        //    }
        //}

        //public bool UnlockItem(string itemId)
        //{
        //    lock (this)
        //    {
        //        var s = CheckIn(itemId);
        //        if (s == ItemCheckStatus.CheckedIn)
        //            return true;
        //        if (s == ItemCheckStatus.CheckedOut)
        //            return false;
        //        changePermissions(itemId, false);
        //        return true;
        //    }
        //}

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
        public Item GetItemByLink(string linkOrEncodedLinkOrShareId)
        {
            lock (this)
            {
                DriveItem driveItem = Task.Run(() =>
                {
                    return Client.Shares[GetEncodedLinkOrShareId(linkOrEncodedLinkOrShareId)].DriveItem.Request()/*.Select("id, name, shared, remoteItem")*/.GetAsync();
                }).Result;
                return Item.Get(this, driveItem);
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
        static public string GetEncodedLinkOrShareId(string linkOrEncodedLinkOrShareId)
        {
            if (Regex.IsMatch(linkOrEncodedLinkOrShareId, @"^(u|s)\!"))
                return linkOrEncodedLinkOrShareId;
            string base64Value = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(linkOrEncodedLinkOrShareId));
            return "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
        }

        public File UploadFile(string localFile, string remoteFolder, string remoteFileName = null)
        {
            lock (this)
            {
                Folder d = CreateFolder(remoteFolder);
                return d.UploadFile(localFile, remoteFileName);
            }
        }

        public Folder CreateFolder(string remoteFolder)
        {
            throw new NotImplementedException();
            lock (this)
            {
                var i = new DriveItem
                {
                    Name = "New Folder",
                    Folder = new Microsoft.Graph.Folder { },
                    AdditionalData = new Dictionary<string, object>()
                    {
                        {"@microsoft.graph.conflictBehavior", "rename"}
                    }
                };
                DriveItem driveItem = Task.Run(() =>
                {
                    return Client.Me.Drive.Root.ItemWithPath("parentId").Children.Request().AddAsync(i);
                }).Result;
                return new Folder(this, driveItem);
            }
        }
    }
}