//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com
//        sergiy.stoyan@outlook.com
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
    public class OneDrive : MicrosoftService
    {
        public OneDrive(string clientId, string[] scopes, MicrosoftUserSettings microsoftUserSettings, string tenantId = "common")
            : base(clientId, scopes, microsoftUserSettings, tenantId)
        {
        }

        public void test(string itemId)
        {
            var i = Task.Run(() =>
            {
                return Client.Me.Drive.Items[itemId].Request().Select("id, Shared, CreatedBy, CreatedByUser, name").GetAsync();
            }).Result;
        }

        public DriveItem GetItemByPath(string path)
        {
            lock (this)
            {
                return Task.Run(() =>
                {
                    return Client.Me.Drive.Root.ItemWithPath(path).Request().GetAsync();
                }).Result;
            }
        }

        public bool LockItem(string itemId, bool changePermissionsIfCheckOutIsNotSupported)
        {
            lock (this)
            {
                var s = CheckOut(itemId);
                if (s == ItemCheckStatus.CheckedOut)
                    return true;
                if (s == ItemCheckStatus.CheckedIn)
                    return false;
                if (!changePermissionsIfCheckOutIsNotSupported)
                    return false;
                changePermissions(itemId, true);
                return true;
            }
        }

        public bool UnlockItem(string itemId)
        {
            lock (this)
            {
                var s = CheckIn(itemId, "by " + Log.ProgramName);
                if (s == ItemCheckStatus.CheckedIn)
                    return true;
                if (s == ItemCheckStatus.CheckedOut)
                    return false;
                changePermissions(itemId, false);
                return true;
            }
        }

        /// <summary>
        /// Locks by removing shared premissions.
        /// Drawbacks:
        /// - the owner still can change the item from beyond the app;
        /// Advantages:
        /// - easily works for folders and anything;
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="readOnly"></param>
        void changePermissions(string itemId, bool readOnly)
        {
            lock (this)
            {
                if (readOnly)
                {
                    var i = Task.Run(() =>
                    {
                        return Client.Me.Drive.Items[itemId].Request().Select("id, Shared").GetAsync();
                    }).Result;
                    var user = Task.Run(() =>
                    {
                        return Client.Me.Request().GetAsync();
                    }).Result;
                    if (i.Shared.Owner.User?.Id != user.Id)
                        throw new Exception("User " + user.DisplayName + " cannot change permissions of the item because it is not owned.");
                }

                if (!MicrosoftUserSettings.ItemIds2PermissionIds2Roles.TryGetValue(itemId, out var permissionIds2Roles))
                {
                    if (!readOnly)//it is a repeated unlock
                        return;
                    var ps = Task.Run(() =>
                    {
                        return Client.Me.Drive.Items[itemId].Permissions.Request().GetAsync();
                    }).Result;
                    permissionIds2Roles = new Dictionary<string, List<string>>();
                    foreach (var p in ps.Where(a => a.GrantedTo != null))
                        permissionIds2Roles[p.Id] = p.Roles.ToList();
                    MicrosoftUserSettings.ItemIds2PermissionIds2Roles[itemId] = permissionIds2Roles;
                    MicrosoftUserSettings.Save();
                }

                foreach (string permissionId in permissionIds2Roles.Keys)
                {
                    if (readOnly && permissionIds2Roles[permissionId].First(a => a != "read") == null)
                        continue;
                    Task.Run(() =>
                    {
                        Permission p2 = new Permission { Roles = readOnly ? new List<string> { "read" } : permissionIds2Roles[permissionId] };
                        return Client.Me.Drive.Items[itemId].Permissions[permissionId].Request().UpdateAsync(p2);
                    }).Wait();
                }

                if (!readOnly)
                {
                    MicrosoftUserSettings.ItemIds2PermissionIds2Roles.Remove(itemId);
                    MicrosoftUserSettings.Save();
                }
            }
        }

        public enum ItemCheckStatus
        {
            CheckOutIsNotSupported,
            CheckedIn,
            CheckedOut,
        }
        public ItemCheckStatus GetCheckStatus(string itemId)
        {
            lock (this)
            {
                var i = Task.Run(() =>
            {
                return Client.Me.Drive.Items[itemId].Request().Select("id, publication").GetAsync();
            }).Result;
                if (i.Publication == null)//if NULL then checkout is not supported
                    return ItemCheckStatus.CheckOutIsNotSupported;
                string s = i.Publication.Level.ToLower();
                if (s == "published")
                    return ItemCheckStatus.CheckedIn;
                if (s == "checkout")
                    return ItemCheckStatus.CheckedOut;
                throw new Exception("Unknown Publication.Level: " + s);
            }
        }

        /// <summary>
        /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
        /// </summary>
        /// <param name="itemId"></param>
        public ItemCheckStatus CheckOut(string itemId)
        {
            lock (this)
            {
                Task.Run(() =>
            {
                Client.Me.Drive.Items[itemId].Checkout().Request().PostAsync();//not supported for a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            }).Wait();

                return GetCheckStatus(itemId);
            }
        }

        /// <summary>
        /// (!)Not supported on a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="comment"></param>
        public ItemCheckStatus CheckIn(string itemId, string comment)
        {
            lock (this)
            {
                Task.Run(() =>
            {
                Client.Me.Drive.Items[itemId].Checkin("published", comment).Request().PostAsync();//not supported for a personal OneDrive: https://learn.microsoft.com/en-us/answers/questions/574546/is-checkin-checkout-files-supported-by-onedrive-pe.html
            }).Wait();

                return GetCheckStatus(itemId);
            }
        }

        public enum LinkRoles
        {
            view, edit, embed
        }

        public enum LinkScopes
        {
            anonymous, organization
        }

        public SharingLink GetLink(string itemId, LinkRoles linkRole, string password = null, DateTimeOffset? expirationDateTime = null, LinkScopes? linkScopes = null, string message = null, bool? retainInheritedPermissions = null)
        {
            lock (this)
            {
                Permission p = Task.Run(() =>
                {
                    return Client.Me.Drive.Items[itemId].CreateLink(linkRole.ToString(), linkScopes.ToString(), expirationDateTime, password, message, retainInheritedPermissions).Request().PostAsync();
                }).Result;
                return p.Link;
            }
        }

        /// <summary>
        /// Expected to work for links of any form:
        /// https://onedrive.live.com/redir?resid=1231244193912!12&authKey=1201919!12921!1
        /// https://onedrive.live.com/?cid=ACBC822AFFB88213&id=ACBC822AFFB88213%21102&parId=root&o=OneUp
        /// https://1drv.ms/x/s!AhOCuP8qgrysblVFtEANPUBlBu4
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        public DriveItem GetItemByLink(string link)
        {
            lock (this)
            {
                string base64Value = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(link));
                string s = "u!" + base64Value.TrimEnd('=').Replace('/', '_').Replace('+', '-');
                //string s = Regex.Replace(link, @"^.+\/|\?.*$", "", RegexOptions.IgnoreCase);
                return Task.Run(() =>
                {
                    return Client.Shares[s].DriveItem.Request().GetAsync();

                }).Result;
            }
        }

        public void DownloadFile(string itemId, string localFile)
        {
            lock (this)
            {
                using (Stream s = Task.Run(() =>
                    {
                        return Client.Me.Drive.Items[itemId].Content.Request().GetAsync();
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

        public DriveItem UploadFile(string itemId, string localFile)
        {
            lock (this)
            {
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    return Task.Run(() =>
                    {
                        return Client.Me.Drive.Items[itemId].Content.Request().PutAsync<DriveItem>(s);
                    }).Result;
                }
            }
        }

        public DriveItem UploadFile(string folderId, string remotefileName, string localFile)
        {
            lock (this)
            {
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    return Task.Run(() =>
                    {
                        return Client.Me.Drive.Items[folderId].ItemWithPath(remotefileName).Content.Request().PutAsync<DriveItem>(s);
                    }).Result;
                }
            }
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remoteFile"></param>
        /// <param name="localFile"></param>
        /// <returns></returns>
        public DriveItem CreateFile(string remoteFile, string localFile)
        {
            lock (this)
            {
                //Match m = Regex.Match(remoteFile, @"(^.*[\\\/]|^\s*)(.*$)");
                //if (!m.Success)
                //    throw new Exception("Remote file path is malformed: " + remoteFile);
                //string remoteFolder = m.Groups[1].Value;
                //string remotefileName = m.Groups[2].Value;
                //if(string.IsNullOrWhiteSpace(remoteFolder))
                //{ }
                //string folderId = GetItemByPath(remoteFolder)?.Id;
                //using (Stream s = System.IO.File.OpenRead(localFile))
                //{
                //    return Task.Run(() =>
                //    {
                //        return Client.Me.Drive.Items[folderId].ItemWithPath(remotefileName).Content.Request().PutAsync<DriveItem>(s);
                //    }).Result;
                //}
                using (Stream s = System.IO.File.OpenRead(localFile))
                {
                    return Task.Run(() =>
                    {
                        return Client.Me.Drive.Root.ItemWithPath(remoteFile).Content.Request().PutAsync<DriveItem>(s);
                    }).Result;
                }
            }
        }

        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="remoteFolder"></param>
        /// <returns></returns>
        public DriveItem CreateFolder(string remoteFolder)
        {
            lock (this)
            {
                var i = new DriveItem
                {
                    Name = "New Folder",
                    Folder = new Folder { },
                    AdditionalData = new Dictionary<string, object>()
                    {
                        {"@microsoft.graph.conflictBehavior", "rename"}
                    }
                };
                return Task.Run(() =>
                {
                    return Client.Me.Drive.Root.ItemWithPath("parentId").Children.Request().AddAsync(i);
                }).Result;
            }
        }
    }
}