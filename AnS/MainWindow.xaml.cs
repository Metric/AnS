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

namespace AnS
{
    public class MainWindow : Window
    {
        static Dictionary<string, int> REGION_INDICES = new Dictionary<string, int>() { { "US", -1 }, { "EU", -2 } };
        const string LUA_NAME = "Data.lua";
        const bool IGNORE_DIFFS = true;

        private List<ConnectedRealm> USRealms;
        private List<ConnectedRealm> EURealms;
        private Dictionary<string, List<ConnectedRealm>> selected;
        private StackPanel listViewItems;
        private TextBlock statusText;
        private ProgressBar statusProgress;
        private DispatcherTimer timer;
        private DispatcherTimer realmTimer;
        private List<RealmItem> items;

        private TextBox search;
        private CheckBox includeRegion;

        private SystemTrayIcon sysIcon;
        private bool exit = false;
        private bool includeRegionData = true;
        private bool syncing = false;

        private string filter = "";
        private List<RealmItem> filteredItems;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            items = new List<RealmItem>();

            search.KeyUp += Search_KeyUp;
            includeRegion.Click += IncludeRegion_Click;
            includeRegion.IsChecked = true;
            includeRegionData = true;

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
                CreateRealmList(USRealms, "US");
                CreateRealmList(EURealms, "EU");
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

        private void IncludeRegion_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            includeRegionData = includeRegion.IsChecked == null ? false : includeRegion.IsChecked.Value;
            Sync();
        }

        private void Search_KeyUp(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if(e.Key == Avalonia.Input.Key.Enter)
            {
                filter = search.Text.ToLower();
                ApplySearchFilter();
            }
        }

        private void ApplySearchFilter()
        {
            if (string.IsNullOrEmpty(filter))
            {
                filteredItems = items.ToList();
            }
            else
            {
                filteredItems = items.FindAll(m => m.Region.ToLower() == filter || m.Realm.realms.JoinNames().ToLower().Contains(filter));
            }

            listViewItems.Children.Clear();
            listViewItems.Children.AddRange(filteredItems);
        }

        private void RealmTimer_Tick(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateRealmList();
            });
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Sync();
            });
        }

        private void UpdateRealmList()
        {
            for(int i = 0; i < items.Count; ++i)
            {
                RealmItem item = items[i];
                item.UpdateTimeInfo();
            }
        }

        private void CreateRealmList(List<ConnectedRealm> realms, string region)
        {
            List<ConnectedRealm> select = null;
            selected.TryGetValue(region, out select);
            select = select ?? new List<ConnectedRealm>();

            /*int regionIndex = -1;
            REGION_INDICES.TryGetValue(region, out regionIndex);
            RealmItem regionRealm = new RealmItem(new ConnectedRealm()
            {
                id = regionIndex,
                realms = new List<Realm>(new Realm[] { new Realm() { id = regionIndex, name = new Dictionary<string, string>() { { "us", region } } } })
            }, region, select.Find(m => m.id == regionIndex) != null);
            regionRealm.OnSelect += Item_OnSelect;
            regionRealm.OnUnselect += Item_OnUnselect;
            listViewItems.Children.Add(regionRealm);
            items.Add(regionRealm);*/

            for (int i = 0; i < realms.Count; ++i)
            {
                ConnectedRealm r = realms[i];

                bool isSelected = select.Find(m => m.id == r.id) != null;
                RealmItem item = new RealmItem(r, region, isSelected);
                item.OnSelect += Item_OnSelect;
                item.OnUnselect += Item_OnUnselect;
                listViewItems.Children.Add(item);
                items.Add(item);
            }
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

            Sync(r, region);
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

            Sync(r, region);
        }

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
                string fpath = Path.Combine(wowPath, LUA_NAME);
                using (FileStream fs = new FileStream(fpath, FileMode.Create, FileAccess.Write, FileShare.None))
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

        private void Sync(ConnectedRealm r, string region)
        {
            syncing = true;

            Dispatcher.UIThread.Post(() =>
            {
                statusText.Text = "Syncing";
                statusProgress.Value = 0;
            });

            List<string> updated = new List<string>();
            Task.Run(() =>
            {
                try
                {
                    if (includeRegionData && GetLatest(REGION_INDICES[region.ToUpper()], region))
                    {
                        updated.Add(region);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }

                try { 
                    if (GetLatest(r.id, region))
                    {
                        if (r.id < 0)
                        {
                            updated.Add(DataSource.REGION_KEY + " - " + r.realms.JoinNames());
                        }
                        else
                        {
                            updated.Add(region + " - " + r.realms.JoinNames());
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }

                if (updated.Count > 0)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = "Finalizing";
                    });

                    CreateLua();

                    if (sysIcon != null && sysIcon.IsActive)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            sysIcon.ShowInfo(updated[0]);
                        });
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    statusText.Text = "";
                    statusProgress.Value = 0;
                });

                syncing = false;
            });
        }

        private void Sync()
        {
            syncing = true;
            Dispatcher.UIThread.Post(() =>
            {
                statusText.Text = "Syncing";
            });

            List<string> updated = new List<string>();
            Task.Run(async () =>
            {
                int totalCount = selected.Count + 1;
                int c = 0;

                HashSet<string> regionDownloaded = new HashSet<string>();

                foreach (string k in selected.Keys)
                {
                    List<ConnectedRealm> realms = selected[k];
                    totalCount += realms.Count;
                    ++c;

                    try
                    {
                        if (!regionDownloaded.Contains(k))
                        {
                            if (includeRegionData && GetLatest(REGION_INDICES[k.ToUpper()], k))
                            {
                                updated.Add(k);
                            }

                            regionDownloaded.Add(k);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                    }

                    for (int i = 0; i < realms.Count; ++i)
                    {
                        ConnectedRealm r = realms[i];
                        ++c;

                        try
                        {
                            if (GetLatest(r.id, k))
                            {
                                //if (r.id < 0)
                                //{
                                //    updated.Add(DataSource.REGION_KEY + " - " + r.realms.JoinNames());
                                //}
                                //else
                                //{
                                    updated.Add(k + " - " + r.realms.JoinNames());
                                //}
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.ToString());
                        }

                        Dispatcher.UIThread.Post(() =>
                        {
                            statusProgress.Value = (float)c / (float)totalCount;
                        });
                    }
                }

                //if (updated.Count > 0)
                //{
                    Dispatcher.UIThread.Post(() =>
                    {
                        statusText.Text = "Finalizing";
                    });

                    CreateLua();
                //}

                Dispatcher.UIThread.Post(() =>
                {
                    statusText.Text = "";
                    statusProgress.Value = 0;
                });

                if (sysIcon != null && sysIcon.IsActive)
                {
                    for (int i = 0; i < updated.Count; ++i)
                    {
                        string msg = updated[i];
                        Dispatcher.UIThread.Post(() =>
                        {
                            sysIcon.ShowInfo(msg);
                        });
                        await Task.Delay(2000);
                    }
                }

                syncing = false;
            });
        }

        private bool GetLatest(int id, string region)
        {
            if (!DataSource.HasLocalData(id, region))
            {
                Debug.WriteLine("No data exists, getting new.");
                if (DataSource.GetServerData(id, region))
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine("failed to get server data");
                }

                return false;
            }

            if (DataSource.HasLocalData(id, region))
            {
                DateTime localModified = DataSource.GetLocalDataModified(id, region);
                DateTime? serverModified = DataSource.GetServerDataModified(id, region);

                if (serverModified == null)
                {
                    return false;
                }

                Debug.WriteLine("local modified: " + localModified.ToString());
                Debug.WriteLine("server modified: " + serverModified.Value.ToString());

                if (localModified < serverModified.Value)
                {
                    int hourDiff = 0;
                    int dayDiff = 0;

                    if (serverModified.Value.Hour < localModified.Hour)
                    {
                        hourDiff = serverModified.Value.Hour - localModified.Hour + 24;
                    }
                    else
                    {
                        hourDiff = serverModified.Value.Hour - localModified.Hour;
                    }

                    if ((int)serverModified.Value.DayOfWeek < (int)localModified.DayOfWeek)
                    {
                        dayDiff = (int)serverModified.Value.DayOfWeek - (int)localModified.DayOfWeek + 7;
                    }
                    else
                    {
                        dayDiff = (int)serverModified.Value.DayOfWeek - (int)localModified.DayOfWeek;
                    }

                    if (hourDiff > 4 || dayDiff > 1 || IGNORE_DIFFS)
                    {
                        Debug.WriteLine("too much difference between local and server time. Downloading full.");
                        //too much difference just pull the full latest
                        if (DataSource.GetServerData(id, region))
                        {
                            return true;
                        }

                        return false;
                    }
                    else if(hourDiff <= 4 && hourDiff > 0 && dayDiff <= 1)
                    {
                        bool mergeFailed = false;
                        int hour = (localModified.Hour + 1) % 24;
                        for (int i = 0; i < hourDiff; ++i)
                        {
                            int rhour = (hour + i) % 24;
                            Debug.WriteLine("trying to get data diff: " + rhour);
                            if (DataSource.GetServerDataDiff(id, region, rhour))
                            {
                                Debug.WriteLine("trying to merge diff");
                                if (!DataSource.Merge(id, region))
                                {
                                    Debug.WriteLine("merge failed");
                                    mergeFailed = true;
                                    //if we failed to merge
                                    //then just pull full
                                    if (DataSource.GetServerData(id, region))
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        Debug.WriteLine("failed to get server data on merge");
                                    }
                                    break;
                                }
                                Debug.WriteLine("merge successful");
                            }
                            else
                            {
                                Debug.WriteLine("failed to get data diff");
                                mergeFailed = true;
                                if (DataSource.GetServerData(id, region))
                                {
                                    return true;
                                }
                                else
                                {
                                    Debug.WriteLine("failed to get server data on non merge");
                                }
                                break;
                            }
                        }

                        if(!mergeFailed)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void LoadIcon()
        {
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            this.Icon = new WindowIcon(embeddedProvider.GetFileInfo("Icons\\ansicon.png").CreateReadStream());
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            LoadIcon();

            sysIcon = new SystemTrayIcon();
            sysIcon.OnShow += SysIcon_OnShow;
            sysIcon.OnExit += SysIcon_OnExit;

            this.Closing += MainWindow_Closing;

            listViewItems = this.FindControl<StackPanel>("ListViewItems");
            statusText = this.FindControl<TextBlock>("StatusText");
            statusProgress = this.FindControl<ProgressBar>("StatusProgress");
            search = this.FindControl<TextBox>("Search");
            includeRegion = this.Find<CheckBox>("IncludeRegion");
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
