using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AnS.Data;
using System;

namespace AnS.UI
{
    public class RealmItem : UserControl
    {
        public delegate void Selected(string region, ConnectedRealm r);

        public event Selected OnSelect;
        public event Selected OnUnselect;

        private CheckBox activeCheck;
        private TextBlock serverName;

        public ConnectedRealm Realm { get; protected set; }
        public string Region { get; protected set; }

        public RealmItem()
        {
            InitializeComponent();
        }

        public RealmItem(ConnectedRealm r, string region, bool selected = false)
        {
            InitializeComponent();
            Realm = r;
            Region = region;

            activeCheck.IsChecked = selected;

            UpdateTimeInfo();
        }

        public void UpdateTimeInfo()
        {
            ConnectedRealm r = Realm;
            string region = Region;

            DateTime? modified;
            TimeSpan? span;
            string extraFormat = "";

            if (DataSource.Settings.mode == AnsMode.Full)
            {
                modified = DataSource.GetLocalDataModified(Realm.id, Region);
                if (modified != null)
                {
                    var now = DateTime.UtcNow;
                    span = new TimeSpan(now.Ticks - modified.Value.Ticks);
                    extraFormat = $"- Updated {span?.FormatTime()} ago";
                }
            }
            
            serverName.Text = region.ToUpper() + " - " + r.realms.JoinNames() + extraFormat;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            activeCheck = this.FindControl<CheckBox>("ActiveCheck");
            serverName = this.FindControl<TextBlock>("ServerName");

            activeCheck.Click += ActiveCheck_Click;
        }

        private void ActiveCheck_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (activeCheck.IsChecked != null && activeCheck.IsChecked.Value)
            {
                OnSelect?.Invoke(Region, Realm);
            }
            else
            {
                OnUnselect?.Invoke(Region, Realm);
            }
        }
    }
}
