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
using Microsoft.Graph.Drives.Item.Items;
using Microsoft.Graph.Drives.Item.Items.Item.CreateLink;
using Microsoft.Graph;

namespace Cliver
{
    public partial class OneDrive
    {
        abstract public class Item
        {
            public static Item New(OneDrive oneDrive, DriveItem driveItem)
            {
                if (driveItem == null)
                    return null;
                if (driveItem.File != null)
                    return new File(oneDrive, driveItem);
                if (driveItem.Folder != null)
                    return new Folder(oneDrive, driveItem);
                throw new Exception("Unknown DriveItem object type: " + driveItem.ToStringByJson());
            }

            async static public Task<Item> GetAsync(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return await oneDrive.GetItemAsync(linkOrEncodedLinkOrShareId);
            }
            static public Item Get(OneDrive oneDrive, string linkOrEncodedLinkOrShareId)
            {
                return RunSync(() => GetAsync(oneDrive, linkOrEncodedLinkOrShareId));
            }

            protected Item(OneDrive oneDrive, DriveItem driveItem)
            {
                OneDrive = oneDrive;
                DriveItem = driveItem;
                ItemId = DriveItem.Id;
                set();
            }

            void set()
            {
                Match m = Regex.Match(ItemId, @"(.*)\!");//on personal OneDrive DriveItem.Id contains driveId
                if (m.Success)
                    DriveId = m.Groups[1].Value;
                else
                    DriveId = DriveItem.ParentReference?.DriveId;//!!!does not work for Root and such
                if (DriveId == null)
                    throw new Exception("Could not get DriveId from DriveItem:\r\n" + DriveItem.ToStringByJson());
            }

            public OneDrive OneDrive { get; private set; }

            public string DriveId { get; private set; }

            public string ItemId { get; private set; }

            async public Task<DriveItem> DriveItemAsync()
            {
                if (driveItem == null)
                    driveItem = await GetDriveItemAsync();
                return driveItem;
            }
            DriveItem driveItem = null;
            public DriveItem DriveItem
            {
                get
                {
                    if (driveItem == null)
                        driveItem = RunSync(DriveItemAsync);
                    return driveItem;
                }
                set
                {
                    driveItem = value;
                }
            }

            public Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder DriveItemRequestBuilder
            {
                get
                {
                    if (itemRequestBuilder == null)
                        itemRequestBuilder = OneDrive.Client.Drives[DriveId].Items[ItemId];
                    return itemRequestBuilder;
                }
            }
            Microsoft.Graph.Drives.Item.Items.Item.DriveItemItemRequestBuilder itemRequestBuilder;

            public enum LinkRoles
            {
                view, edit, embed
            }

            public enum LinkScopes
            {
                anonymous, organization
            }

            async public Task<SharingLink> GetLinkAsync(LinkRoles linkRole, string password = null, DateTimeOffset? expirationDateTime = null, LinkScopes? linkScopes = null, string message = null, bool? retainInheritedPermissions = null)
            {
                var requestBody = new CreateLinkPostRequestBody
                {
                    Type = linkRole.ToString(),
                    Password = password,
                    Scope = linkScopes.ToString(),
                    RetainInheritedPermissions = retainInheritedPermissions,
                };
                Permission p = await DriveItemRequestBuilder.CreateLink.PostAsync(requestBody);
                return p.Link;
            }
            public SharingLink GetLink(LinkRoles linkRole, string password = null, DateTimeOffset? expirationDateTime = null, LinkScopes? linkScopes = null, string message = null, bool? retainInheritedPermissions = null)
            {
                lock (this)
                {
                    return RunSync(() => GetLinkAsync(linkRole, password, expirationDateTime, linkScopes, message, retainInheritedPermissions));
                }
            }

            async public Task<string> WebViewLinkAsync()
            {
                if (viewLink == null)
                    viewLink = (await GetLinkAsync(LinkRoles.view)).WebUrl;
                return viewLink;
            }
            string viewLink;
            public string WebViewLink
            {
                get
                {
                    if (viewLink == null)
                        viewLink = RunSync(WebViewLinkAsync);
                    return viewLink;
                }
            }

            async public Task<DriveItem> GetDriveItemAsync(string[] select = null, string[] expand = null/*, string selectWithoutPrefix = null, string expandWithoutPrefix = null*/)
            {
                return await DriveItemRequestBuilder.GetAsync(
                    rc =>
                    {
                        rc.QueryParameters.Select = select;//new string[] { "id", "createdDateTime" }
                        rc.QueryParameters.Expand = expand;
                    }
                );
            }
            public DriveItem GetDriveItem(string[] select = null, string[] expand = null/*, string selectWithoutPrefix = null, string expandWithoutPrefix = null*/)
            {
                return RunSync(() => GetDriveItemAsync(select, expand));
            }

            async public Task<DriveItem> GetDriveItemAsync(string select, string expand = null)
            {
                return await GetDriveItemAsync(select?.Split(','), expand?.Split(','));
            }
            public DriveItem GetDriveItem(string select, string expand = null)
            {
                return RunSync(() => GetDriveItemAsync(select, expand));
            }

            async public Task<DriveItem> GetRootDriveItemAsync()
            {
                return await OneDrive.GetRootDriveItemAsync(DriveId);
            }
            public DriveItem GetRootDriveItem()
            {
                return RunSync(GetRootDriveItemAsync);
            }

            async public Task<Folder> GetParentFolderAsync(bool refresh = true)
            {
                if (refresh || DriveItem.ParentReference == null)
                    DriveItem.ParentReference = (await GetDriveItemAsync("ParentReference")).ParentReference;

                DriveItem parentDriveItem = await OneDrive.Client.Drives[DriveId].Items[DriveItem.ParentReference.Id].GetAsync();
                return (Folder)New(OneDrive, parentDriveItem);
            }
            public Folder GetParentFolder(bool refresh = true)
            {
                return RunSync(() => GetParentFolderAsync(refresh));
            }

            async public Task DeleteAsync()
            {
                await DriveItemRequestBuilder.DeleteAsync();
            }
            public void Delete()
            {
                RunSync(DeleteAsync);
            }

            //public void Rename()
            //{
            //    Task.Run(() =>
            //    {
            //        DriveItemRequestBuilder.Request()();
            //    }).Wait();
            //}

            /// <summary>
            /// Identifiers useful for SharePoint REST compatibility. Read-only.
            /// </summary>
            async public Task<SharepointIds> SharepointIdsAsync()
            {
                if (DriveItem.SharepointIds == null)
                    DriveItem.SharepointIds = (await GetDriveItemAsync("SharepointIds")).SharepointIds;
                return DriveItem.SharepointIds;
            }
            public SharepointIds SharepointIds
            {
                get
                {
                    if (DriveItem.SharepointIds == null)
                        DriveItem.SharepointIds = RunSync(SharepointIdsAsync);
                    return DriveItem.SharepointIds;
                }
            }

            /// <summary>
            /// For drives in SharePoint, the associated document library list item. Read-only. Nullable.
            /// </summary>
            async public Task<ListItem> ListItemAsync()
            {
                if (DriveItem.ListItem == null)
                    DriveItem.ListItem = (await GetDriveItemAsync("ListItem")).ListItem;
                return DriveItem.ListItem;
            }
            public ListItem ListItem
            {
                get
                {
                    if (DriveItem.ListItem == null)
                        DriveItem.ListItem = RunSync(ListItemAsync);
                    return DriveItem.ListItem;
                }
            }

            async public Task<List<Item>> SearchAsync(string query)
            {
                var driveItems = await DriveItemRequestBuilder.SearchWithQ(query).GetAsSearchWithQGetResponseAsync();
                return driveItems.Value.Select(a => New(OneDrive, a)).ToList();
            }
            public List<Item> Search(string query)
            {
                return RunSync(() => SearchAsync(query));
            }

            //public string GetPath(bool refresh = true)
            //{
            //    if (refresh)
            //    {
            //        DriveItem di = GetDriveItem("ParentReference, Name");
            //        DriveItem.ParentReference = di.ParentReference;
            //        DriveItem.Name = di.Name;
            //    }
            //    return DriveItem.ParentReference.Path + "/" + DriveItem.Name;
            //}

            async public Task<Item> GetAsync(string relativePath)
            {
                string escapedRelativePath = GetEscapedPath(relativePath);//(!)the API always tries to unescape

                DriveItem di = null;
                try
                {
                    di = await DriveItemRequestBuilder.ItemWithPath(escapedRelativePath).GetAsync();
                }
                catch (Exception e)
                {
                    for (; e != null; e = e.InnerException)
                        if (e is /*Microsoft.Graph.ServiceException*/ Microsoft.Kiota.Abstractions.ApiException ex && (int)System.Net.HttpStatusCode.NotFound == ex.ResponseStatusCode)
                            return null;
                    throw;
                }
                return New(OneDrive, di);
            }
            public Item Get(string relativePath)
            {
                return RunSync(() => GetAsync(relativePath));
            }
        }
    }
}