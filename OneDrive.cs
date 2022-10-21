//Author: Sergey Stoyan
//        systoyan@gmail.com
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
        public OneDrive(string clientId, string[] scopes, MicrosoftDataStoreUserSettings microsoftDataStoreUserSettings, string tenantId = "common")
            : base(clientId, scopes, microsoftDataStoreUserSettings, tenantId)
        {
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

        public void LockItem(string itemId)
        {
            changePermissions(itemId, true);
        }

        public void UnlockItem(string itemId)
        {
            changePermissions(itemId, false);
        }

        void lockingFile(string itemId, bool @lock)
        {
            //var ih = Task.Run(() =>
            //{
            //    return Client.Me.Drive.Root.ItemWithPath(file).Request().Select("Id, Publication, Permissions, Name, File, Folder, AdditionalData, Shared").Expand("Publication").GetAsync();
            //}).Result;

            //try
            //{
            //    Task.Run(() =>
            //    {
            //        if (@lock)
            //            Client.Me.Drive.Items[i.Id].Checkout().Request().PostAsync();
            //        else
            //            Client.Me.Drive.Items[i.Id].Checkin().Request().PostAsync();
            //    }).Wait();
            //}
            //catch (Exception e)
            //{

            //}


            //var ii = Task.Run(() =>
            //{
            //    //return Client.Me.Drive.Root.ItemWithPath(file).Request().Select("publication").GetAsync();
            //    return Client.Me.Drive.Items[i.Id].Request().Select("id, publication, permissions, name").GetAsync();
            //}).Result;

            //try
            //{
            //    Task.Run(() =>
            //    {
            //        Client.Me.Drive.Root.ItemWithPath(file).Checkin().Request().PostAsync();
            //    }).Wait();
            //}
            //catch (Exception e)
            //{

            //}

            //try
            //{
            //    Task.Run(() =>
            //    {
            //        Client.Me.Drive.Root.ItemWithPath(file).Checkin().Request().PostAsync();
            //    }).Wait();
            //}
            //catch (Exception e)
            //{

            //}
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
                if (!MicrosoftDataStoreUserSettings.ItemIds2PermissionIds2Roles.TryGetValue(itemId, out var permissionIds2Roles))
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
                    MicrosoftDataStoreUserSettings.ItemIds2PermissionIds2Roles[itemId] = permissionIds2Roles;
                    MicrosoftDataStoreUserSettings.Save();
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
                    MicrosoftDataStoreUserSettings.ItemIds2PermissionIds2Roles.Remove(itemId);
                    MicrosoftDataStoreUserSettings.Save();
                }
            }
        }

        public enum Roles
        {
            view, edit, embed
        }

        public enum Scopes
        {
            anonymous, organization
        }

        public SharingLink GetLink(string itemId, Roles role, string password = null, DateTimeOffset? expirationDateTime = null, Scopes? scope = null, string message = null, bool? retainInheritedPermissions = null)
        {
            lock (this)
            {
                Permission p = Task.Run(() =>
                {
                    return Client.Me.Drive.Items[itemId].CreateLink(role.ToString(), scope.ToString(), expirationDateTime, password, message, retainInheritedPermissions).Request().PostAsync();

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

        public DriveItem CreateFile(string remoteFile, string localFile)//TBD
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

        public DriveItem CreateFolder(string remoteFolder)//TBD
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