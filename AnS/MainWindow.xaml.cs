using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AnS.Data;
using AnS.UI;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

namespace AnS
{
    public class MainWindow : Window
    {
        private List<ConnectedRealm> USRealms;
        private List<ConnectedRealm> EURealms;

        private Dictionary<string, List<ConnectedRealm>> selected;
        
        private StackPanel usListView;
        private StackPanel euListView;
        
        private TextBlock statusText;
        private ProgressBar statusProgress;
        
        private DispatcherTimer timer;
        private DispatcherTimer realmTimer;

        private List<RealmItem> usItems;
        private List<RealmItem> euItems;

        private TextBox search;

        private CheckBox includeRegion;
        private CheckBox fullMode;

        private SystemTrayIcon sysIcon;

        private bool exit = false;
        private bool includeRegionData = true;
        private bool syncing = false;
        private bool listModified = false;

        private string filter = "";

        private List<RealmItem> usFilteredItems;
        private List<RealmItem> euFilteredItems;

        private CancellationTokenSource ctk;

        private const int QUEUE_WAIT_TIME = 30 * 1000;
        private const int MAX_LUA_REALMS = 3;

        protected enum RealmRegion
        {
            US,
            EU
        }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            usItems = new List<RealmItem>();
            euItems = new List<RealmItem>();

            search.KeyUp += Search_KeyUp;

            includeRegion.Click += IncludeRegion_Click;
            includeRegionData = DataSource.Settings.includeRegion;
            includeRegion.IsChecked = includeRegionData;

            fullMode.Click += FullMode_Click;
            fullMode.IsChecked = DataSource.Settings.mode == AnsMode.Full;

            Task.Run(() =>
            {
                selected = DataSource.Selected;
                selected = selected ?? new Dictionary<string, List<ConnectedRealm>>();

                USRealms = DataSource.US;
                EURealms = DataSource.EU;

                USRealms.Sort((a, b) =>
                {
                    return a.realms.JoinNames().CompareTo(b.realms.JoinNames());
                });

                EURealms.Sort((a, b) =>
                {
                    return a.realms.JoinNames().CompareTo(b.realms.JoinNames());
                });

            }).ContinueWith(t =>
            {
                CreateRealmList(USRealms, usItems, usListView, RealmRegion.US);
                CreateRealmList(EURealms, euItems, euListView, RealmRegion.EU);

                Sync();

                timer = new DispatcherTimer();
                timer.Interval = new TimeSpan(1, 0, 0);
                timer.Tick += Timer_Tick;
                timer.Start();

                realmTimer = new DispatcherTimer();
                realmTimer.Interval = new TimeSpan(0, 0, 2, 0);
                realmTimer.Tick += RealmTimer_Tick;
                realmTimer.Start();

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        #region UI Setup / Filtering
        private void ApplySearchFilter()
        {
            if (string.IsNullOrEmpty(filter))
            {
                usFilteredItems = usItems.ToList();
                euFilteredItems = euItems.ToList();
            }
            else
            {
                usFilteredItems = usItems.FindAll(m => m.Realm.realms.JoinNames().ToLower().Contains(filter));
                euFilteredItems = euItems.FindAll(m => m.Realm.realms.JoinNames().ToLower().Contains(filter));
            }

            usListView.Children.Clear();
            euListView.Children.Clear();

            usListView.Children.AddRange(usFilteredItems);
            euListView.Children.AddRange(euFilteredItems);
        }

        private void CreateRealmList(List<ConnectedRealm> realms, List<RealmItem> items, StackPanel listView, RealmRegion region)
        {
            List<ConnectedRealm> select = null;
            selected.TryGetValue(region.ToString(), out select);
            select = select ?? new List<ConnectedRealm>();

            for (int i = 0; i < realms.Count; ++i)
            {
                ConnectedRealm r = realms[i];

                bool isSelected = select.Find(m => m.id == r.id) != null;
                RealmItem item = new RealmItem(r, region.ToString(), isSelected);
                item.OnSelect += Item_OnSelect;
                item.OnUnselect += Item_OnUnselect;
                listView.Children.Add(item);
                items.Add(item);
            }
        }
        #endregion

        #region Timers
        /// <summary>
        /// Updates the time information
        /// for LUA file status
        /// </summary>
        private void UpdateLuaTimeInfo()
        {
            List<int> ids = new List<int>();

            foreach (string region in selected.Keys)
            {
                var realms = selected[region];

                for (int i = 0; i < realms.Count; ++i)
                {
                    ids.Add(realms[i].id);
                }
            }

            if (includeRegionData)
            {
                ids.Add(-1);
            }

            if (ids.Count > MAX_LUA_REALMS || ids.Count == 0
            || (ids.Count <= 1 && includeRegionData))
            {
                statusText.Text = "";
                return;
            }

            var time = DataSource.GetLocalDataModified(ids.ToArray());

            if (time == null)
            {
                statusText.Text = "";
                return;
            }

            var now = DateTime.UtcNow;
            var span = new TimeSpan(now.Ticks - time.Value.Ticks);

            statusText.Text = "Updated " + span.FormatTime() + " ago";
        }

        /// <summary>
        /// Updates the time information
        /// for RAW DB Files
        /// </summary>
        private void UpdateRawTimeInfo()
        {
            for (int i = 0; i < euItems.Count; ++i)
            {
                euItems[i].UpdateTimeInfo();
            }
            for (int i = 0; i < usItems.Count; ++i)
            {
                usItems[i].UpdateTimeInfo();
            }
            statusText.Text = "";
        }

        private void RealmTimer_Tick(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                //last updated message
                if (syncing) return;

                if (DataSource.Settings.mode == AnsMode.Full)
                {
                    UpdateRawTimeInfo();
                    return;
                }

                UpdateLuaTimeInfo();
            });
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ctk != null)
                {
                    ctk.Cancel();
                    ctk = null;
                }

                if (!syncing)
                {
                    Sync();
                }
            });
        }
        #endregion

        #region UI Interactions
        private void FullMode_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool fullModeChecked = fullMode.IsChecked != null ? fullMode.IsChecked.Value : false;
            if (fullModeChecked)
            {
                DataSource.Settings.mode = AnsMode.Full;
            }
            else
            {
                DataSource.Settings.mode = AnsMode.Limited;
            }

            DataSource.Store(DataSource.Settings);

            listModified = true;

            RealmTimer_Tick(null, null);

            if (ctk != null)
            {
                ctk.Cancel();
                ctk = null;
            }

            ctk = new CancellationTokenSource();

            Task.Delay(2000, ctk.Token).ContinueWith(t =>
            {
                if (t.IsCanceled || syncing)
                {
                    return;
                }

                Sync();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Search_KeyUp(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                filter = search.Text.ToLower();
                ApplySearchFilter();
            }
        }

        private void IncludeRegion_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            includeRegionData = includeRegion.IsChecked == null ? false : includeRegion.IsChecked.Value;

            DataSource.Settings.includeRegion = includeRegionData;

            DataSource.Store(DataSource.Settings);

            listModified = true;

            if (ctk != null)
            {
                ctk.Cancel();
                ctk = null;
            }

            ctk = new CancellationTokenSource();

            Task.Delay(2000, ctk.Token).ContinueWith(t =>
            {
                if (t.IsCanceled || syncing)
                {
                    return;
                }

                Sync();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Item_OnUnselect(string region, ConnectedRealm r)
        {
            List<ConnectedRealm> select = null;
            if (selected.TryGetValue(region, out select))
            {
                int idx = select.FindIndex(m => m.id == r.id);
                if (idx > -1)
                {
                    select.RemoveAt(idx);
                }

                Task.Run(() =>
                {
                    DataSource.Store(selected);
                });
            }

            listModified = true;

            if (ctk != null)
            {
                ctk.Cancel();
                ctk = null;
            }

            ctk = new CancellationTokenSource();

            Task.Delay(2000, ctk.Token).ContinueWith(t =>
            {
                if (t.IsCanceled || syncing)
                {
                    return;
                }

                Sync();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Item_OnSelect(string region, ConnectedRealm r)
        {
            List<ConnectedRealm> select = null;
            selected.TryGetValue(region, out select);
            select = select ?? new List<ConnectedRealm>();
            selected[region] = select;
            select.Add(r);

            Task.Run(() =>
            {
                DataSource.Store(selected);
            });

            listModified = true;

            if (ctk != null)
            {
                ctk.Cancel();
                ctk = null;
            }

            ctk = new CancellationTokenSource();

            Task.Delay(2000, ctk.Token).ContinueWith(t =>
            {
                if (t.IsCanceled || syncing)
                {
                    return;
                }

                Sync();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        #endregion

        #region Syncing
        private void CreateLua()
        {
            Debug.WriteLine("Building Lua");
            //write to proper data file
            string wowPath = DataSource.WoWPath;
            if (string.IsNullOrEmpty(wowPath))
            {
                Debug.WriteLine("WoW Data Path is null");
                return;
            }
            try
            {
                string fpath = DataSource.DataLuaPath;
                using (FileStream fs = new FileStream(fpath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    Lua.Build(selected, fs, includeRegionData);
                }
                Debug.WriteLine("Lua Built");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        private void OnDownloadProgress(float f)
        {
            Dispatcher.UIThread.Post(() =>
            {
                statusText.Text = "Downloading";
                statusProgress.Minimum = 0;
                statusProgress.Maximum = 1;
                statusProgress.IsVisible = true;
                statusProgress.Value = f;
            });
        }

        private async Task SyncLua()
        {
            bool success = false;
            List<string> updated = new List<string>();
            List<int> ids = new List<int>();

            foreach (string region in selected.Keys)
            {
                var realms = selected[region];

                for (int i = 0; i < realms.Count; ++i)
                {
                    ids.Add(realms[i].id);
                    updated.Add(region.ToUpper() + " - " + realms[i].realms.JoinNames());
                }
            }

            if (includeRegionData)
            {
                ids.Add(-1);
            }

            if (ids.Count > 6 || (ids.Count <= 1 && includeRegionData)
            || ids.Count == 0)
            {
                return;
            }

            success = GetLatest(out bool queued, listModified, ids.ToArray());

            while (queued)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    statusText.Text = "Waiting for Lua";
                    statusProgress.Minimum = 0;
                    statusProgress.Maximum = 1;
                    statusProgress.IsVisible = false;
                    statusProgress.Value = 0;
                });

                await Task.Delay(QUEUE_WAIT_TIME);
                success = GetLatest(out queued, listModified || queued, ids.ToArray());
            }

            if (sysIcon != null && sysIcon.IsActive && success)
            {
                for (int i = 0; i < updated.Count; ++i)
                {
                    string msg = updated[i];
                    Dispatcher.UIThread.Post(() =>
                    {
                        sysIcon.ShowInfo(msg);
                    });
                    await Task.Delay(1000);
                }
            }

            DataSource.OnProgress -= OnDownloadProgress;

            if (!success)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    statusText.Text = "Failed to Update";
                    statusProgress.Minimum = 0;
                    statusProgress.Maximum = 1;
                    statusProgress.IsVisible = false;
                    statusProgress.Value = 0;
                });

                await Task.Delay(2000);
            }
            else
            {
                listModified = false;
            }
        }

        private async Task SyncRaw()
        {
            bool success = false;
            List<string> updated = new List<string>();
            var keys = selected.Keys.ToList();

            for (int i = 0; i < keys.Count; ++i)
            {
                var k = keys[i];

                List<ConnectedRealm> realms = selected[k];

                try
                {
                    if (includeRegionData)
                    {
                        if (GetLatest(-1, k, listModified))
                        {
                            success = true;
                            updated.Add(k);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }

                for (int j = 0; j < realms.Count; ++j)
                {
                    var r = realms[j];

                    try
                    {
                        if (GetLatest(r.id, k, listModified))
                        {
                            success = true;
                            updated.Add(k + " - " + r.realms.JoinNames());
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                    }
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                statusText.Text = "Building Lua";
            });

            CreateLua();

            listModified = false;

            if (sysIcon != null && sysIcon.IsActive && success)
            {
                for (int i = 0; i < updated.Count; ++i)
                {
                    string msg = updated[i];
                    Dispatcher.UIThread.Post(() =>
                    {
                        sysIcon.ShowInfo(msg);
                    });
                    await Task.Delay(1000);
                }
            }
        }

        private void Sync()
        {
            DataSource.OnProgress += OnDownloadProgress;

            syncing = true;
            Dispatcher.UIThread.Post(() =>
            {
                statusText.Text = DataSource.Settings.mode == AnsMode.Limited ? "Requesting Lua" : "";
                statusProgress.Minimum = 0;
                statusProgress.Maximum = 1;
                statusProgress.IsVisible = false;
                statusProgress.Value = 0;
            });

            List<string> updated = new List<string>();
            Task.Run(async () =>
            {

                AnsMode mode = DataSource.Settings.mode;
                switch (mode)
                {
                    case AnsMode.Full:
                        await SyncRaw();
                        break;
                    case AnsMode.Limited:
                        await SyncLua();
                        break;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    DataSource.OnProgress -= OnDownloadProgress;
                    statusText.Text = "";
                    statusProgress.Minimum = 0;
                    statusProgress.Maximum = 1;
                    statusProgress.IsVisible = false;
                    statusProgress.Value = 0;

                    GC.Collect();
                });

                syncing = false;

                RealmTimer_Tick(null, null);
            });
        }

        private bool GetLatest(int id, string region, bool modified)
        {
            DateTime? localModified = DataSource.GetLocalDataModified(id, region);
            DateTime? serverModified = DataSource.GetServerDataModified(id, region);

            //file does not exist in this case
            if (serverModified == null)
            {
                return false;
            }

            if (localModified == null)
            {
                return DataSource.GetServerData(id, region);
            }

            if (localModified.Value < serverModified.Value || modified)
            {
                return DataSource.GetServerData(id, region);
            }

            return false;
        }

        private bool GetLatest(out bool queued, bool modified, params int[] id)
        {
            queued = false;

            if (id == null || id.Length == 0)
            {
                return false;
            }

            DateTime? localModified = DataSource.GetLocalDataModified(id);
            DateTime? serverModified = DataSource.GetServerDataModified(id);

            if (serverModified == null || localModified == null)
            {
                return DataSource.GetServerData(out queued, id);
            }

            Debug.WriteLine("local modified: " + localModified.ToString());
            Debug.WriteLine("server modified: " + serverModified.Value.ToString());

            if (localModified.Value < serverModified.Value || modified)
            {
                return DataSource.GetServerData(out queued, id);
            }

            return false;
        }
        #endregion

        private void LoadIcon()
        {
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            this.Icon = new WindowIcon(embeddedProvider.GetFileInfo("Icons\\ansicon.png").CreateReadStream());
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            LoadIcon();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                sysIcon = new SystemTrayIcon();
                sysIcon.OnShow += SysIcon_OnShow;
                sysIcon.OnExit += SysIcon_OnExit;

                this.Closing += MainWindow_Closing;
            }

            usListView = this.FindControl<StackPanel>("USListView");
            euListView = this.FindControl<StackPanel>("EUListView");

            statusText = this.FindControl<TextBlock>("StatusText");
            statusProgress = this.FindControl<ProgressBar>("StatusProgress");
            
            search = this.FindControl<TextBox>("Search");
            includeRegion = this.Find<CheckBox>("IncludeRegion");
            fullMode = this.Find<CheckBox>("FullMode");
        }

        private void SysIcon_OnExit()
        {
            exit = true;
            this.Close();
        }

        private void SysIcon_OnShow()
        {
            this.Show();
            this.BringIntoView();
            this.Activate();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!exit && sysIcon != null && sysIcon.IsActive)
            {
                e.Cancel = true;
                this.Hide();
                sysIcon.ShowInfo("AnS is still active in system tray. Use the icon to exit via right click.");
            }
        }
    }
}
