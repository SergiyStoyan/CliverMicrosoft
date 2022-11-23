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
        abstract public class Item
        {
            public static Item Get(OneDrive oneDrive, DriveItem driveItem)
            {
                if (driveItem.File != null)
                    return new File(oneDrive, driveItem);
                if (driveItem.Folder != null)
                    return new Folder(oneDrive, driveItem);
                throw new Exception("Unknown DriveItem object type: " + driveItem.ToStringByJson());
            }

            //public static Item Create(OneDrive oneDrive, string driveId, string itemId)
            //{
            //    if (driveItem.File != null)
            //        return new File(oneDrive, driveItem);
            //    if (driveItem.Folder != null)
            //        return new Folder(oneDrive, driveItem);
            //    throw new Exception("Unknown DriveItem object type: " + driveItem.ToStringByJson());
            //}

            protected Item(OneDrive oneDrive, DriveItem driveItem)
            {
                OneDrive = oneDrive;
                DriveItem = driveItem;
                ItemId = DriveItem.Id;
                set();
            }

            //protected Item(OneDrive oneDrive, string driveId, string itemId)
            //{
            //    OneDrive = oneDrive;
            //    DriveId = driveId;
            //    ItemId = itemId;
            //    set();
            //}

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

            public DriveItem DriveItem
            {
                get
                {
                    if (driveItem == null)
                        driveItem = GetDriveItem();
                    return driveItem;
                }
                set
                {
                    driveItem = value;
                }
            }
            DriveItem driveItem = null;

            public IDriveItemRequestBuilder DriveItemRequestBuilder
            {
                get
                {
                    if (driveItemRequestBuilder == null)
                        driveItemRequestBuilder = OneDrive.Client.Me.Drives[DriveId].Items[ItemId];
                    return driveItemRequestBuilder;
                }
            }
            IDriveItemRequestBuilder driveItemRequestBuilder;

            public enum LinkRoles
            {
                view, edit, embed
            }

            public enum LinkScopes
            {
                anonymous, organization
            }

            public SharingLink GetLink(LinkRoles linkRole, string password = null, DateTimeOffset? expirationDateTime = null, LinkScopes? linkScopes = null, string message = null, bool? retainInheritedPermissions = null)
            {
                lock (this)
                {
                    Permission p = Task.Run(() =>
                    {
                        return DriveItemRequestBuilder.CreateLink(linkRole.ToString(), linkScopes.ToString(), expirationDateTime, password, message, retainInheritedPermissions).Request().PostAsync();
                    }).Result;
                    return p.Link;
                }
            }

            public DriveItem GetDriveItem(string select = null, string expand = null)
            {
                return Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[ItemId].Request().Select(select).Expand(expand).GetAsync();
                }).Result;
            }

            public Item GetParent()
            {
                if (DriveItem.ParentReference?.Id == null)
                    DriveItem.ParentReference = GetDriveItem("id, ParentReference").ParentReference;

                DriveItem parentDriveItem = Task.Run(() =>
                {
                    return OneDrive.Client.Me.Drives[DriveId].Items[DriveItem.ParentReference.Id].Request().GetAsync();
                }).Result;
                return Get(OneDrive, parentDriveItem);
            }

            public void Delete()
            {
                Task.Run(() =>
                {
                    DriveItemRequestBuilder.Request().DeleteAsync();
                }).Wait();
            }
        }
    }
}