/*
KINEMATICSOUP CONFIDENTIAL
 Copyright(c) 2014-2025 KinematicSoup Technologies Incorporated 
 All Rights Reserved.

NOTICE:  All information contained herein is, and remains the property of 
KinematicSoup Technologies Incorporated and its suppliers, if any. The 
intellectual and technical concepts contained herein are proprietary to 
KinematicSoup Technologies Incorporated and its suppliers and may be covered by
U.S. and Foreign Patents, patents in process, and are protected by trade secret
or copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
KinematicSoup Technologies Incorporated.
*/

namespace KS.Reactor.Client.Unity
{
    /// <summary>Menu name constants.</summary>
    public class ksMenuNames
    {
        /// <summary>Reactor menu name</summary>
        public const string REACTOR = "Reactor/";
        /// <summary>Create context menu</summary>
        public const string CREATE = "Assets/Create/Reactor/";
        /// <summary>Hiearchy context menu</summary>
        public const string HIERARCHY = "GameObject/Reactor/";
        /// <summary>
        /// Use with <see cref="UnityEngine.AddComponentMenu"/> to hide a component from the Add Component menu.
        /// </summary>
        public const string HIDDEN = "GameObject/";
    }

    /// <summary>
    /// Menu group priority constants. Lower priority shows first. Groups must be separated by more than 10 to have a
    /// line drawn between them.
    /// </summary>
    public class ksMenuGroups
    {
        public const int CLIENT = 0;
        public const int COMMON = 20;
        public const int SERVER = 40;
        public const int FOLDER = 60;
        public const int BUILTIN_ASSETS = 80;
        public const int SCRIPT_ASSETS = 100;
        public const int TEST_GROUP = 120;
    }
}
