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

        string FormatTime(ref TimeSpan span)
        {
            string formatted = "";

            if (Math.Floor(span.TotalDays) == 0 && Math.Floor(span.TotalHours) == 0 && Math.Floor(span.TotalMinutes) == 0)
            {
                formatted = (int)span.TotalSeconds + "s";
            }
            else if(Math.Floor(span.TotalDays) == 0 && Math.Floor(span.TotalHours) == 0)
            {
                formatted = (int)span.TotalMinutes + "min";
            }
            else if(Math.Floor(span.TotalDays) == 0)
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

        public void UpdateTimeInfo()
        {
            ConnectedRealm r = Realm;
            string region = Region;

            string lastUpdate = "";


            if (DataSource.HasLocalData(r.id, region))
            {
                DateTime time = DataSource.GetLocalDataModified(r.id, region);
                TimeSpan diff = new TimeSpan(DateTime.UtcNow.Ticks - time.Ticks);
                lastUpdate = " - Updated " + FormatTime(ref diff) + " ago";
            }

            if (region.Equals(r.realms.JoinNames()) || r.id < 0)
            {
                serverName.Text = DataSource.REGION_KEY + " - " + region.ToUpper() + lastUpdate;
            }
            else
            {
                serverName.Text = region.ToUpper() + " - " + r.realms.JoinNames() + lastUpdate;
            }
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
