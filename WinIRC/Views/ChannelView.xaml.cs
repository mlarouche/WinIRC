using IrcClientCore;
using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using WinIRC.Handlers;
using WinIRC.Ui;

namespace WinIRC.Views
{
    public sealed partial class ChannelView : Page, INotifyPropertyChanged
    {
        private IrcUiHandler IrcHandler = IrcUiHandler.Instance;
        private ChannelStore store;
        private bool ChannelLoaded;

        public event PropertyChangedEventHandler PropertyChanged;

        public string currentChannel { get; set; }
        public string currentServer { get; set; }

        private ObservableCollection<Message> currentBuffer;
        public ObservableCollection<Message> CurrentBuffer
        {
            get => currentBuffer; set
            {
                currentBuffer = value;
                this.NotifyPropertyChanged();
            }
        }

        public AppWindow Window { get; }

        public ChannelView()
        {
            this.InitializeComponent();
            UpdateUi();
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ChannelView(string server, string channel) : this(server, channel, null) { 
        }

        public ChannelView(string server, string channel, AppWindow window)
        {
            this.InitializeComponent();

            if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Media.ThemeShadow"))
            {
                Shadow.Receivers.Add(Scroll);

                TopArea.Translation += new Vector3(0, 0, 12);
            }

            currentServer = server;
            currentChannel = channel;

            Unloaded += ChannelView_Unloaded;

            var visibility = window != null ? Visibility.Visible : Visibility.Collapsed;
            this.titlebar.Visibility = visibility;
            this.titlebar.Text = $"{currentChannel} | {currentServer}";
            this.Window = window;

            if (window == null && CompactToggle != null)
            {
                CompactToggle.Visibility = Visibility.Collapsed;   
            }

            MainPage.instance.UiUpdated += Instance_UiUpdated;
            SetChannel(server, channel);
        }

        private void Instance_UiUpdated(object sender, EventArgs e)
        {
            UpdateUi();
        }

        private void ChannelView_Unloaded(object sender, RoutedEventArgs e)
        {
            Unload();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var args = ((string[])e.Parameter);

            SetChannel(args[0], args[1]);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            var args = ((string[])e.Parameter);

            Unload();
        }

        private void Unload()
        {
            if (ChannelLoaded)
            {
                CurrentBuffer = null;
                store.TopicSetEvent -= ChannelView_TopicSetEvent;
                store = null;
                topicText.Text = "";
            }
        }

        internal void SetChannel(string server, string channel)
        {
            if (currentChannel != null && currentServer != null && CurrentBuffer != null && ChannelLoaded)
            {
                var chan = currentChannel == "Server"
                    ? IrcHandler.connectedServers[currentServer].ChannelList.ServerLog
                    : IrcHandler.connectedServers[currentServer].ChannelList[currentChannel];
                CurrentBuffer.CollectionChanged -= ChannelView_CollectionChanged;
                store.TopicSetEvent -= ChannelView_TopicSetEvent;
            }

            var servers = IrcHandler.connectedServers;

            if (!servers.ContainsKey(server))
            {
                return;
            }
            currentServer = server;
            currentChannel = channel;
            Channel channelStore = null;

            if (servers[server].ChannelList.Contains(channel))
            {
                channelStore = IrcHandler.connectedServers[currentServer].ChannelList[currentChannel];
            }
            else if (channel == "Server" || channel == "")
            {
                channelStore = IrcHandler.connectedServers[currentServer].ChannelList.ServerLog;
            }

            if (channel == null) return;

            CurrentBuffer = channelStore.Buffers as ObservableCollection<Message>;

            store = channelStore.Store;
            store.TopicSetEvent += ChannelView_TopicSetEvent;

            topicText.Text = store.Topic;

            CurrentBuffer.CollectionChanged += ChannelView_CollectionChanged;

            // ScrollToBottom();
            UpdateUi();

            ChannelLoaded = true;
        }

        private void ChannelView_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // ScrollToBottom();
        }

        private void ChannelView_TopicSetEvent(string topic)
        {
            topicText.Text = topic;
        }

        public TextBox GetInputBox()
        {
            return ircMsgBox;
        }

        public void ircMsgBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (currentChannel == null || currentServer == null || currentServer == "" || currentChannel == "")
            {
                return;
            }

            IrcHandler.IrcTextBoxHandler(ircMsgBox, e, currentServer, currentChannel);
        }


        private void TabButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (currentChannel == null || currentServer == null || currentServer == "" || currentChannel == "")
            {
                return;
            }

            IrcHandler.TabComplete(ircMsgBox, currentServer, currentChannel);
        }


        public void UpdateUi()
        {
            var uiMode = UIViewSettings.GetForCurrentView().UserInteractionMode;

            if (uiMode == Windows.UI.ViewManagement.UserInteractionMode.Touch)
            {
                TabButton.Width = 48;
            }
            else
            {
                TabButton.Width = 0;
            }

            if (Config.Contains(Config.FontFamily))
            {
                // this.messagesView.FontFamily = new FontFamily(Config.GetString(Config.FontFamily));
            }

            if (Config.Contains(Config.FontSize))
            {
                // this.messagesView.FontSize = Convert.ToDouble(Config.GetString(Config.FontSize));
            }

            if (currentChannel != "Server")
            {
                topicScroll.Visibility = MainPage.instance.ShowTopic ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                topicScroll.Visibility = Visibility.Collapsed;
            }
            var darktheme = Config.GetBoolean(Config.DarkTheme);
            this.RequestedTheme = darktheme ? ElementTheme.Dark : ElementTheme.Light;

            var sidebarColor = Config.GetBoolean(Config.DarkTheme) ? ColorUtils.ParseColor("#FF222222") : ColorUtils.ParseColor("#FFDDDDDD");
            Brush brush;
            try
            {
                if (Config.GetBoolean(Config.Blurred, true) && ApiInformation.IsTypePresent("Microsoft.UI.Xaml.Media.AcrylicBrush"))
                {
                    var source = Microsoft.UI.Xaml.Media.AcrylicBackgroundSource.Backdrop;
                    brush = new Microsoft.UI.Xaml.Media.AcrylicBrush
                    {
                        FallbackColor = sidebarColor,
                        BackgroundSource = source,
                        TintColor = sidebarColor,
                        TintOpacity = 0.55
                    };
                }
                else
                {
                    brush = new SolidColorBrush(sidebarColor);
                }
            }
            catch (Exception e)
            {
                brush = new SolidColorBrush(sidebarColor);
            }

            TopFillArea.Fill = brush;
            PaneContentGrid.Background = brush;

            if (this.Window != null)
            {
                Window.TitleBar.ButtonBackgroundColor = Window.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                Window.TitleBar.ButtonForegroundColor = Window.TitleBar.ButtonInactiveForegroundColor = darktheme ? Colors.White : Colors.Black;
                Window.TitleBar.ForegroundColor = darktheme ? Colors.White : Colors.Black;
                
            }
        }

        private void CompactToggle_Click(object sender, RoutedEventArgs e)
        {
            if (CompactToggle.IsChecked.Value)
            {
                Window.Presenter.RequestPresentation(AppWindowPresentationKind.CompactOverlay);
            }
            else
            {
                Window.Presenter.RequestPresentation(AppWindowPresentationKind.Default);
            }
        }
    }
}
