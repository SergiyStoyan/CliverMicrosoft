//********************************************************************************************
//Author: Sergiy Stoyan
//        s.y.stoyan@gmail.com, sergiy.stoyan@outlook.com, stoyan@cliversoft.com
//        http://www.cliversoft.com
//********************************************************************************************
using System.IO;

namespace Cliver
{
    public abstract class MicrosoftUserSettings : MicrosoftSettings
    {
        /// <summary>
        /// Storage folder for this Settings located in LocalApplicationData.
        /// </summary>
        sealed public override string __StorageDir { get; protected set; } = StorageDir;
        /// <summary>
        /// Storage folder for this Settings located in LocalApplicationData.
        /// </summary>
        public static readonly string StorageDir = Log.AppCompanyUserDataDir + Path.DirectorySeparatorChar + Config.CONFIG_FOLDER_NAME;
    }
}