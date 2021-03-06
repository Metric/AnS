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

        private SystemTrayIcon sysIcon;

        private bool exit = false;
        private bool includeRegionData = true;
        private bool syncing = false;
        private bool listModified = false;

        private string filter = "";

        private List<RealmItem> usFilteredItems;
        private List<RealmItem> euFilteredItems;

        private CancellationTokenSource ctk;

        private const int QUEUE_WAIT_TIME = 60 * 1000;

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

        private void IncludeRegion_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            includeRegionData = includeRegion.IsChecked == null ? false : includeRegion.IsChecked.Value;

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

        string FormatTime(ref TimeSpan span)
        {
            string formatted = "";

            if (Math.Floor(span.TotalDays) == 0 && Math.Floor(span.TotalHours) == 0 && Math.Floor(span.TotalMinutes) == 0)
            {
                formatted = (int)span.TotalSeconds + "s";
            }
            else if (Math.Floor(span.TotalDays) == 0 && Math.Floor(span.TotalHours) == 0)
            {
                formatted = (int)span.TotalMinutes + "min";
            }
            else if (Math.Floor(span.TotalDays) == 0)
            {
                formatted = (int)span.TotalHours + "hr";
                if (span.TotalHours > 1)
                {
                    formatted += "s";
                }
            }
            else
            {
                formatted = Math.Floor(span.TotalDays) + "day";
                if (span.TotalDays > 1)
                {
                    formatted += "s";
                }
            }

            return formatted;
        }

        private void RealmTimer_Tick(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                //last updated message
                if (syncing) return;

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

                if (ids.Count > 6 || ids.Count == 0 
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

                statusText.Text = "Updated " + FormatTime(ref span) + " ago";
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

        private void Sync()
        {
            DataSource.OnProgress += OnDownloadProgress;

            syncing = true;
            Dispatcher.UIThread.Post(() =>
            {
                statusText.Text = "Requesting Lua";
                statusProgress.Minimum = 0;
                statusProgress.Maximum = 1;
                statusProgress.IsVisible = false;
                statusProgress.Value = 0;
            });

            List<string> updated = new List<string>();
            Task.Run(async () =>
            {
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
                    DataSource.OnProgress -= OnDownloadProgress;
                    syncing = false;
                    return;
                }

                bool queued = false;
                bool success = GetLatest(out queued, listModified, ids.ToArray());

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
                        await Task.Delay(2000);
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

                Dispatcher.UIThread.Post(() =>
                {
                    statusText.Text = "";
                    statusProgress.Minimum = 0;
                    statusProgress.Maximum = 1;
                    statusProgress.IsVisible = false;
                    statusProgress.Value = 0;
                });

                syncing = false;

                RealmTimer_Tick(null, null);
            });
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
