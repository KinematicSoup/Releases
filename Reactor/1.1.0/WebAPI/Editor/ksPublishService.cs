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

using KS.Unity.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KS.Unity;

namespace KS.Reactor.Client.Unity.Editor
{
    /// <summary>Manage state for publishing and running images.</summary>
    public class ksPublishService : ksSingleton<ksPublishService>
    {
        /// <summary>Sorting methods for images.</summary>
        public enum SortKey
        {
            /// <summary>Alphabetical sort on image name.</summary>
            NAME = 0,
            /// <summary>Reverse alphabetical sort on image name.</summary>
            NAME_REVERSE = 1,
            /// <summary>Alphabetical sort on image version.</summary>
            VERSION = 2,
            /// <summary>Reverse alphabetical sort on image version.</summary>
            VERSION_REVERSE = 3,
            /// <summary>Sort newest to oldest.</summary>
            NEWEST = 4,
            /// <summary>Sort oldest to newest.</summary>
            OLDEST = 5,
            /// <summary>Alphabetical sort on publisher name.</summary>
            PUBLISHER = 6,
            /// <summary>Reverse alphabetical sort on publisher name.</summary>
            PUBLISHER_REVERSE = 7
        }

        /// <summary>Contains information about a Reactor image.</summary>
        public class ImageInfo
        {
            private int m_id = -1;
            private string m_name;
            private string m_version;
            private string m_publishTime;
            private string m_publisher;
            private Dictionary<string, List<string>> m_scenes = new Dictionary<string, List<string>>();

            /// <summary>Image ID.</summary>
            public int Id
            {
                get { return m_id; }
                set { m_id = value; }
            }

            /// <summary>Image name.</summary>
            public string Name
            {
                get { return m_name == null ? "" : m_name; }
                set { m_name = value; }
            }

            /// <summary>Image version.</summary>
            public string Version
            {
                get { return m_version == null ? "" : m_version; }
                set { m_version = value; }
            }

            /// <summary>Time the image was published in YYYY-MM-DD HH-mm-ss format.</summary>
            public string Time
            {
                get { return m_publishTime == null ? "" : m_publishTime; }
                set { m_publishTime = value; }
            }

            /// <summary>Name of the user who published the image.</summary>
            public string Publisher
            {
                get { return m_publisher == null ? "" : m_publisher; }
                set { m_publisher = value; }
            }

            /// <summary>A collection of scene names mapped to lists of rooms in those scenes.</summary>
            public Dictionary<string, List<string>> Scenes
            {
                get { return m_scenes; }
                set { m_scenes = value; }
            }
        }

        /// <summary>Project list update callback.</summary>
        /// <param name="error">error message, null if no errors were returned.</param>
        public delegate void UpdateProjectsCallback(string error);

        /// <summary>Project selection callback.</summary>
        /// <param name="projectId">Selected project ID.</param>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void SelectProjectCallback(int projectId, string error);

        /// <summary>Image list update callback.</summary>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void UpdateImagesCallback(string error);

        /// <summary>Image publishing callback.</summary>
        /// <param name="imageId">Image ID.</param>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void PublishCallback(string imageId, string error);

        /// <summary>Server list update callback.</summary>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void UpdateServersCallback(string error);

        /// <summary>Start server callback.</summary>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void StartServerCallback(string error);

        /// <summary>Stop server callback.</summary>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void StopServerCallback(string error);

        /// <summary>Virtual machine line update callback.</summary>
        /// <param name="error">Error message, null if no errors were returned.</param>
        public delegate void UpdateVirtualMachinesCallback(string error);

        /// <summary>Bind image callback.</summary>
        /// <param name="imageId">Id of the bound image.</param>
        public delegate void BindImageCallback(int imageId);

        /// <summary>Invoked when the projects list is updated or there are errors when fetching projects.</summary>
        public event UpdateProjectsCallback OnUpdateProjects;

        /// <summary>Invoked when the selected project changes.</summary>
        public event SelectProjectCallback OnSelectProject;

        /// <summary>Invoked when the image list is updated or there are errors when fetching images.</summary>
        public event UpdateImagesCallback OnUpdateImages;

        /// <summary>Invoked when an image is published or there are errors when publishing.</summary>
        public event PublishCallback OnPublish;

        /// <summary>Invoked when the server list is updated or there are errors when fetching servers.</summary>
        public event UpdateServersCallback OnUpdateServers;

        /// <summary>Invoked when a start server command is complete or there is an error starting a server.</summary>
        public event StartServerCallback OnStartServer;

        /// <summary>Invoked when a stop server command is complete or there is an error stopping a server.</summary>
        public event StopServerCallback OnStopServer;

        /// <summary>Invoked when the virtual machine list is updated or there are errors when fetching virtual machines.</summary>
        public event UpdateVirtualMachinesCallback OnUpdateVirtualMachines;

        /// <summary>Invoked when an image is bound.</summary>
        public event BindImageCallback OnBindImage;

        // Unity editor update time
        [NonSerialized]
        private long m_lastTime = 0;
        [NonSerialized]
        private long m_deltaTime = 0;

        // Projects
        private const long PROJECT_REFRESH_TIME = 30;
        private int m_selectedProjectId = -1;
        [NonSerialized]
        private bool m_isFetchingProjects = false;
        [NonSerialized]
        private object m_projectsLock = new object();
        [NonSerialized]
        private long m_projectsFetchTime = 0;
        private Dictionary<int, ksProjectInfo> m_projects;

        // Images
        private const long IMAGE_REFRESH_TIME = 10;
        private List<ImageInfo> m_images;
        private ImageInfo m_boundImage;
        [NonSerialized]
        private bool m_isFetchingImages = false;
        [NonSerialized]
        private bool m_isPublishing = false;
        [NonSerialized]
        private object m_imagesLock = new object();
        [NonSerialized]
        private long m_imagesFetchTime = 0;

        // Servers
        private const long SERVER_REFRESH_TIME = 10;
        private ksJSON m_servers = null;
        [NonSerialized]
        private bool m_isFetchingServers = false;
        [NonSerialized]
        private bool m_isStarting = false;
        [NonSerialized]
        private bool m_isStopping = false;
        [NonSerialized]
        private object m_serversLock = new object();
        [NonSerialized]
        private long m_serversFetchTime = 0;

        // VirtualMachines
        private const long VIRTUALMACHINE_REFRESH_TIME = 10;
        private ksJSON m_virtualMachines = null;
        [NonSerialized]
        private bool m_isFetchingVirtualMachines = false;
        [NonSerialized]
        private object m_virtualMachinesLock = new object();
        [NonSerialized]
        private long m_virtualMachinesFetchTime = 0;

        // Sorting
        private SortKey m_sortKey;
        private static readonly string SORT_KEY = "com.kinematicsoup.image_sort";


        /// <summary>Reset fetch times.</summary>
        public void Refresh()
        {
            m_projectsFetchTime = 0;
            m_imagesFetchTime = 0;
            m_serversFetchTime = 0;
            m_virtualMachinesFetchTime = 0;
        }

        /// <summary>Check if we can publish an image.</summary>
        public bool CanMakeRequests
        {
            get
            {
                return !EditorApplication.isCompiling && ksEditorWebService.IsLoggedIn;
            }
        }

        /// <summary>Initialization.</summary>
        public void Start()
        {
            EditorApplication.update += Update;
            m_sortKey = (SortKey)EditorPrefs.GetInt(SORT_KEY, (int)SortKey.NEWEST);
        }

        /// <summary>Called by <see cref="EditorApplication.update"/>.</summary>
        public void Update()
        {
            m_deltaTime = DateTime.Now.Ticks - m_lastTime;
            m_lastTime = DateTime.Now.Ticks;

            if (m_deltaTime < 0)
            {
                m_deltaTime = 0;
            }

            if (CanMakeRequests)
            {
                FetchProjects();
                FetchImages();
                FetchServers();
                FetchVirtualMachines();
            }
        }

        /// <summary>Get / Set the selected project.</summary>
        public int SelectedProjectId
        {
            get { return m_selectedProjectId; }
            set
            {
                if (m_selectedProjectId != value)
                {
                    if (m_projects != null && m_projects.ContainsKey(value))
                    {
                        m_selectedProjectId = value;
                        m_imagesFetchTime = 0;
                        m_serversFetchTime = 0;
                        m_virtualMachinesFetchTime = 0;
                        if (OnSelectProject != null)
                        {
                            OnSelectProject(m_selectedProjectId, null);
                        }
                    }
                    else
                    {
                        ksLog.Debug("The requested project does not exist");
                        OnSelectProject(-1, "Missing project " + value);
                    }
                }
            }
        }

        /// <summary>Get the project info for the current selected project.</summary>
        public ksProjectInfo SelectedProject
        {
            get
            {
                lock (m_projectsLock)
                {
                    if (m_projects != null && m_projects.ContainsKey(m_selectedProjectId))
                    {
                        return m_projects[m_selectedProjectId];
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Get a sorted list of project information. Null if the projects haven't been fetched at least once.
        /// </summary>
        public List<ksProjectInfo> Projects
        {
            get
            {
                if (m_projects == null)
                {
                    return null;
                }
                List<ksProjectInfo> projects = new List<ksProjectInfo>();
                lock (m_projectsLock)
                {
                    foreach (KeyValuePair<int, ksProjectInfo> pair in m_projects)
                    {
                        projects.Add(pair.Value);
                    }
                    projects.Sort((ksProjectInfo a, ksProjectInfo b) =>
                    {
                        int cmp = a.CompanyName.CompareTo(b.CompanyName);
                        if (cmp == 0)
                        {
                            return a.Name.CompareTo(b.Name);
                        }
                        else
                        {
                            return cmp;
                        }
                    });
                    return projects;
                }
            }
        }

        /// <summary>Gets the bound image id.</summary>
        /// <returns>Id of the bound image, or -1 if there is no bound image.</returns>
        public int GetBoundImageId()
        {
            if (!string.IsNullOrEmpty(ksReactorConfig.Instance.Build.ImageBinding))
            {
                string[] parts = ksReactorConfig.Instance.Build.ImageBinding.Split('.');
                int id;
                if (parts.Length < 3 || !int.TryParse(parts[2], out id))
                {
                    id = -1;
                }
                return id;
            }
            return -1;
        }

        /// <summary>Binds the project to an image.</summary>
        /// <param name="imageId">Image to bind to.</param>
        public void BindImage(int imageId)
        {
            if (SelectedProjectId <= 0)
            {
                return;
            }
            ksReactorConfig.Instance.Build.ImageBinding = 
                SelectedProject.CompanyId + "." + SelectedProjectId + "." + imageId;
            EditorUtility.SetDirty(ksReactorConfig.Instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Update bound image
            m_boundImage = null;
            if (m_images != null)
            {
                foreach (ImageInfo info in m_images)
                {
                    if (info.Id == imageId)
                    {
                        m_boundImage = info;
                        break;
                    }
                }
            }
            if (OnBindImage != null)
            {
                OnBindImage(imageId);
            }
        }

        /// <summary>Fetch a list a projects that the current user is assigned to.</summary>
        private void FetchProjects()
        {
            if (m_projectsFetchTime > 0)
            {
                m_projectsFetchTime -= m_deltaTime;
            }

            if (m_projectsFetchTime <= 0 && !m_isFetchingProjects && OnUpdateProjects != null)
            {
                m_isFetchingProjects = true;
                m_projectsFetchTime = PROJECT_REFRESH_TIME * TimeSpan.TicksPerSecond;
                ksEditorWebService.Send(
                    new ksJSON(),
                    ksReactorConfig.Instance.Urls.Publishing + "/v3/projects/list",
                    OnFetchProjects
                );
            }
        }

        /// <summary>Handle the result of a fetch projects request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnFetchProjects(ksJSON response, string error)
        {
            m_isFetchingProjects = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                lock (m_projectsLock)
                {
                    if (m_projects == null)
                    {
                        m_projects = new Dictionary<int, ksProjectInfo>();
                    }
                    else
                    {
                        m_projects.Clear();
                    }
                }
                if (OnUpdateProjects != null)
                {
                    OnUpdateProjects("An error was encountered while fetching a list of projects. " + error);
                }
                ksLog.Debug("An error was encountered while fetching a list of projects. " + error);
                return;
            }

            // Update project list
            lock (m_projectsLock)
            {
                if (m_projects == null)
                {
                    m_projects = new Dictionary<int, ksProjectInfo>();
                }
                else
                {
                    m_projects.Clear();
                }
                foreach (KeyValuePair<string, ksJSON> pair in response.Fields)
                {
                    ksProjectInfo info = new ksProjectInfo();
                    info.Id = pair.Value.GetField("id", 0);
                    info.Name = pair.Value.GetField("name", "Project " + info.Id);
                    info.CompanyId = pair.Value.GetField("companyId", -1);
                    info.CompanyName = pair.Value.GetField("companyName", "Account " + info.CompanyId);
                    m_projects.Add(info.Id, info);
                }
            }

            // Validate selected project
            bool selectProject;
            lock (m_projectsLock)
            {
                selectProject = UpdateProjectSelection();
            }

            // Events
            if (OnUpdateProjects != null)
            {
                OnUpdateProjects(null);
            }

            if (selectProject)
            {
                m_imagesFetchTime = 0;
                m_serversFetchTime = 0;
                m_virtualMachinesFetchTime = 0;
                if (OnSelectProject != null)
                {
                    OnSelectProject(m_selectedProjectId, null);
                }
            }
        }

        /// <summary>Get the image list. Null if the image list hasn't been fetched at least once.</summary>
        public List<ImageInfo> Images
        {
            get { return m_images; }
        }

        /// <summary>The bound image</summary>
        public ImageInfo BoundImage
        {
            get { return m_boundImage; }
        }

        /// <summary>Determines how images are sorted.</summary>
        public SortKey Sorting
        {
            get { return m_sortKey; }
            set
            {
                if (m_sortKey != value)
                {
                    m_sortKey = value;
                    EditorPrefs.SetInt(SORT_KEY, (int)value);
                    lock (m_imagesLock)
                    {
                        SortImages();
                    }
                }
            }
        }

        /// <summary>Sets the image <see cref="Sorting"/>.</summary>
        /// <param name="sorting">
        /// Sorting key to set. If <see cref="Sorting"/> is already this value, sets it to
        /// <paramref name="reverseSorting"/> instead.
        /// </param>
        /// <param name="reverseSorting">
        /// If <see cref="Sorting"/> is already set to <paramref name="sorting"/>, sets it to this value instead.
        /// </param>
        public void SetSorting(SortKey sorting, SortKey reverseSorting)
        {
            if (Sorting == sorting)
            {
                Sorting = reverseSorting;
            }
            else
            {
                Sorting = sorting;
            }
        }

        /// <summary>
        /// Selects a project if no project is selected or the selected project does not exist. If there is an image
        /// binding for an image in a valid project, selects that project. Otherwise, selects the first project, or
        /// selects nothing if there are no projects.
        /// </summary>
        /// <returns>True if the selected project was changed.</returns>
        private bool UpdateProjectSelection()
        {
            if (m_projects == null || m_projects.Count == 0)
            {
                if (m_selectedProjectId != -1)
                {
                    m_selectedProjectId = -1;
                    return true;
                }
                return false;
            }

            if (m_projects.ContainsKey(m_selectedProjectId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(ksReactorConfig.Instance.Build.ImageBinding))
            {
                string[] parts = ksReactorConfig.Instance.Build.ImageBinding.Split('.');
                if (parts.Length > 1 && int.TryParse(parts[1], out m_selectedProjectId) && m_projects.ContainsKey(m_selectedProjectId))
                {
                    return true;
                }
            }

            m_selectedProjectId = m_projects.Keys.First();
            return true;

        }

        /// <summary>Request a list of images.</summary>
        private void FetchImages()
        {
            if (!m_isFetchingProjects && m_imagesFetchTime > 0)
            {
                m_imagesFetchTime -= m_deltaTime;
            }

            if (m_imagesFetchTime <= 0 && !m_isFetchingImages && OnUpdateImages != null)
            {
                m_isFetchingImages = true;
                m_imagesFetchTime = IMAGE_REFRESH_TIME * TimeSpan.TicksPerSecond;

                ksJSON request = new ksJSON();
                request["projectId"] = m_selectedProjectId;
                request["format"] = "map";
                ksEditorWebService.Send(request,
                    ksReactorConfig.Instance.Urls.Publishing + "/v3/images/list", OnFetchImages);
            }
        }

        /// <summary>Handle the results of a fetch image list request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnFetchImages(ksJSON response, string error)
        {
            m_isFetchingImages = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                lock (m_imagesLock)
                {
                    m_images = new List<ImageInfo>();
                }
                if (OnUpdateImages != null)
                {
                    OnUpdateImages("An error was encountered while fetching a list of images. " + error);
                }
                return;
            }

            // Update image list
            int boundImageId = GetBoundImageId();
            m_boundImage = null;
            lock (m_imagesLock)
            {
                m_images = new List<ImageInfo>();
                if (response != null)
                {
                    foreach (KeyValuePair<string, ksJSON> imagePair in response.Fields)
                    {
                        ImageInfo info = new ImageInfo();
                        info.Id = imagePair.Value.GetField("id", -1);
                        info.Name = imagePair.Value.GetField("name", null);
                        info.Version = imagePair.Value.GetField("version", null);
                        info.Time = imagePair.Value.GetField("time", null);
                        info.Publisher = imagePair.Value.GetField("publisher", null);

                        ksJSON scenes = null;
                        imagePair.Value.GetField("scenes", ref scenes);
                        if (scenes != null && scenes.Count > 0) {
                            foreach (KeyValuePair<string, ksJSON> sceneData in scenes.Fields)
                            {
                                List<string> rooms = new List<string>();
                                foreach(KeyValuePair<string, ksJSON> roomData in sceneData.Value.Fields)
                                {
                                    rooms.Add(roomData.Key);
                                }
                                rooms.Sort();
                                info.Scenes.Add(sceneData.Key, rooms);
                            }
                        }
                        m_images.Add(info);
                        if (info.Id == boundImageId)
                        {
                            m_boundImage = info;
                        }
                    }
                    SortImages();
                }
            }

            // Events
            if (OnUpdateImages != null)
            {
                OnUpdateImages(null);
            }
        }

        /// <summary>Sorts the image list based on the <see cref="Sorting"/> key.</summary>
        private void SortImages()
        {
            if (m_images == null)
            {
                return;
            }
            m_images.Sort((ImageInfo a, ImageInfo b) => {
                int cmp = 0;
                bool reverse = false;
                switch (m_sortKey)
                {
                    case SortKey.NAME:
                    {
                        cmp = a.Name.CompareTo(b.Name);
                        break;
                    }
                    case SortKey.NAME_REVERSE:
                    {
                        cmp = b.Name.CompareTo(a.Name);
                        reverse = true;
                        break;
                    }
                    case SortKey.VERSION:
                    {
                        cmp = a.Version.CompareTo(b.Version);
                        break;
                    }
                    case SortKey.VERSION_REVERSE:
                    {
                        cmp = b.Version.CompareTo(a.Version);
                        reverse = true;
                        break;
                    }
                    case SortKey.NEWEST:
                    {
                        cmp = b.Time.CompareTo(a.Time);
                        break;
                    }
                    case SortKey.OLDEST:
                    {
                        cmp = a.Time.CompareTo(b.Time);
                        break;
                    }
                    case SortKey.PUBLISHER:
                    {
                        cmp = a.Publisher.CompareTo(b.Publisher);
                        break;
                    }
                    case SortKey.PUBLISHER_REVERSE:
                    {
                        cmp = b.Publisher.CompareTo(a.Publisher);
                        reverse = true;
                        break;
                    }
                }
                if (cmp != 0)
                {
                    return cmp;
                }
                // Sort by name, followed by version if the sort key values matched.
                if (m_sortKey != SortKey.NAME && m_sortKey != SortKey.NAME_REVERSE)
                {
                    cmp = a.Name.CompareTo(b.Name);
                    if (cmp != 0)
                    {
                        return reverse ? -cmp : cmp;
                    }
                }
                if (m_sortKey != SortKey.VERSION && m_sortKey != SortKey.VERSION_REVERSE)
                {
                    cmp = a.Version.CompareTo(b.Version);
                    return reverse ? -cmp : cmp;
                }
                return 0;
            });
        }

        /// <summary>Can we send a publish request?</summary>
        public bool CanPublish
        {
            get { return CanMakeRequests && !m_isPublishing; }
        }

        /// <summary>Publish a Reactor runtime and configs as an image.</summary>
        /// <param name="name">Image name</param>
        /// <param name="version">Image version</param>
        /// <param name="sceneData">Image scene data</param>
        public bool PublishImage(string name, string version, ksJSON sceneData)
        {
            if (CanMakeRequests && !m_isPublishing)
            {
                if (!Directory.Exists(ksPaths.ImageDir))
                {
                    if (OnPublish != null)
                    {
                        OnPublish(null, "Unable to publish images. See console log for more information.");
                    }
                    return false;
                }

                m_isPublishing = true;
                ksJSON request = new ksJSON();
                request["projectId"] = m_selectedProjectId;
                request["name"] = name;
                request["version"] = version;
                request["server"] = ksReactorConfig.Instance.Version;
                request["scenes"] = sceneData;

                DirectoryInfo dirInfo = new DirectoryInfo(ksPaths.ImageDir);
                FileInfo[] imageFiles = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where((FileInfo file) => !file.Name.StartsWith("KSServerRuntime.Local.")).ToArray();

                ksEditorWebService.Upload(
                    request,
                    imageFiles,
                    ksReactorConfig.Instance.Urls.Publishing + "/v3/images/publish",
                    OnPublishImages
                );
                return true;
            }

            return false;
        }

        /// <summary>Handle the results of a publish image request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnPublishImages(ksJSON response, string error)
        {
            m_isPublishing = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                lock (m_imagesLock)
                {
                    if (m_images != null)
                    {
                        m_images.Clear();
                    }
                }
                if (OnPublish != null)
                {
                    OnPublish(null, "An error was encountered while publishing an image. " + error);
                }
                return;
            }

            // Events
            m_imagesFetchTime = 0;
            if (OnPublish != null)
            {
                OnPublish(response["id"], null);
            }
        }

        /// <summary>Get the server list.</summary>
        public ksJSON Servers
        {
            get
            {
                lock (m_serversLock)
                {
                    return m_servers;
                }
            }
        }

        /// <summary>Request a list of running servers.</summary>
        private void FetchServers()
        {
            if (!m_isFetchingProjects && m_serversFetchTime > 0)
            {
                m_serversFetchTime -= m_deltaTime;
            }

            if (m_serversFetchTime <= 0 && !m_isFetchingServers && OnUpdateServers != null)
            {
                m_isFetchingServers = true;
                m_serversFetchTime = SERVER_REFRESH_TIME * TimeSpan.TicksPerSecond; ;

                ksJSON request = new ksJSON();

                request["projectId"] = m_selectedProjectId;
                ksEditorWebService.Send(request,
                    ksReactorConfig.Instance.Urls.Publishing + "/v3/instances/list", OnFetchServers);
            }
        }

        /// <summary>Handle the results of fetch servers request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnFetchServers(ksJSON response, string error)
        {
            m_isFetchingServers = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                lock (m_serversLock)
                {
                    m_servers = null;
                }
                if (OnUpdateServers != null)
                {
                    OnUpdateServers("An error was encountered while fetching a list of servers. " + error);
                }
                return;
            }

            // Update image list
            lock (m_serversLock)
            {
                m_servers = response;
            }

            // Events
            if (OnUpdateServers != null)
            {
                OnUpdateServers(null);
            }
        }

        /// <summary>Can we send a launch request?</summary>
        public bool CanStartServer
        {
            get { return CanMakeRequests && !m_isStarting; }
        }

        /// <summary>Start a server.</summary>
        /// <param name="imageId">Image ID</param>
        /// <param name="scene">Scene name</param>
        /// <param name="room">Room name</param>
        /// <param name="name">Instance name</param
        /// <param name="location">Server location</param>
        /// <param name="keepAlive">Keep alive</param>
        /// <param name="isPublic">Public state of the server.</param>
        /// <returns>True if the request was sent.</returns>
        public bool StartServer(string imageId, string scene, string room, string name, string location, bool keepAlive, bool isPublic)
        {
            if (CanMakeRequests && !m_isStarting)
            {
                m_isStarting = true;
                ksJSON request = new ksJSON();
                request["imageId"] = imageId;
                request["scene"] = scene;
                request["room"] = room;
                request["name"] = (!string.IsNullOrEmpty(name)) ? name : imageId + "."+scene+"."+room;
                request["location"] = location;
                request["keepAlive"] = keepAlive;
                request["isPublic"] = isPublic;
                ksEditorWebService.Send(request, ksReactorConfig.Instance.Urls.Publishing + "/v3/instances/start", OnStartServers);
                return true;
            }
            return false;
        }

        /// <summary>Handle the results of a start server request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnStartServers(ksJSON response, string error)
        {
            m_isStarting = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                if (OnStartServer != null)
                {
                    OnStartServer("An error was encountered while launching a server. " + error);
                }
                return;
            }

            // Events
            m_serversFetchTime = 0;
            if (OnStartServer != null)
            {
                OnStartServer(null);
            }
        }

        /// <summary>Stop a server.</summary>
        /// <param name="instanceId">Instance ID</param>
        /// <returns>True if the request was sent.</returns>
        public bool StopServer(string instanceId)
        {
            if (CanMakeRequests && !m_isStopping)
            {
                m_isStopping = true;
                ksJSON request = new ksJSON();
                request["instanceId"] = instanceId;
                ksEditorWebService.Send(request, ksReactorConfig.Instance.Urls.Publishing + "/v3/instances/stop", OnStopServers);
                return true;
            }
            return false;
        }


        /// <summary>Handle the results of a stop server request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnStopServers(ksJSON response, string error)
        {
            m_isStopping = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                if (OnStopServer != null)
                {
                    OnStopServer("An error was encountered while stopping a server. " + error);
                }
                return;
            }

            // Events
            m_serversFetchTime = 0;
            if (OnStopServer != null)
            {
                OnStopServer(null);
            }
        }

        /// <summary>Get the virtual machine list.</summary>
        public ksJSON VirtualMachines
        {
            get
            {
                lock (m_virtualMachinesLock)
                {
                    return m_virtualMachines;
                }
            }
        }

        /// <summary>Request a list of virtual machines.</summary>
        private void FetchVirtualMachines()
        {
            if (!m_isFetchingVirtualMachines && m_virtualMachinesFetchTime > 0)
            {
                m_virtualMachinesFetchTime -= m_deltaTime;
            }

            if (m_virtualMachinesFetchTime <= 0 && !m_isFetchingVirtualMachines && OnUpdateVirtualMachines != null)
            {
                m_isFetchingVirtualMachines = true;
                m_virtualMachinesFetchTime = VIRTUALMACHINE_REFRESH_TIME * TimeSpan.TicksPerSecond;

                ksJSON request = new ksJSON();
                request["projectId"] = m_selectedProjectId;
                ksEditorWebService.Send(request,
                    ksReactorConfig.Instance.Urls.Publishing + "/v3/instances/locations", OnFetchVirtualMachines);
            }
        }

        /// <summary>Handle the results of fetch virtual machines request.</summary>
        /// <param name="response">Response data</param>
        /// <param name="error">Error message</param>
        private void OnFetchVirtualMachines(ksJSON response, string error)
        {
            m_isFetchingVirtualMachines = false;

            // Error handling
            if (!string.IsNullOrEmpty(error))
            {
                lock (m_virtualMachinesLock)
                {
                    m_virtualMachines = null;
                }
                if (OnUpdateVirtualMachines != null)
                {
                    OnUpdateVirtualMachines("An error was encountered while fetching a list of virtual machines. " + error);
                }
                return;
            }

            // Update image list
            lock (m_virtualMachinesLock)
            {
                m_virtualMachines = response;
            }

            // Events
            if (OnUpdateVirtualMachines != null)
            {
                OnUpdateVirtualMachines(null);
            }
        }
    }
}
