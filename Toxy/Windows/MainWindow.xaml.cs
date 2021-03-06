﻿using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;

using SharpTox.Core;
using Toxy.ViewModels;
using Toxy.Extensions;
using Toxy.Managers;
using System.Threading.Tasks;
using Squirrel;
using System.Collections.Generic;
using Toxy.Windows;

namespace Toxy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
#if X86 && !CANARY
        private const string _updateUrl = "http://update.toxing.me/toxy/x86/stable/";
#elif X64 && !CANARY
        private const string _updateUrl = "http://update.toxing.me/toxy/x64/stable/";
#elif X86 && CANARY
        private const string _updateUrl = "http://update.toxing.me/toxy/x86/canary/";
#elif X64 && CANARY
        private const string _updateUrl = "http://update.toxing.me/toxy/x64/canary/";
#endif

        public List<ConversationWindow> Children { get; private set; }

        private static MainWindow _instance;
        public static MainWindow Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MainWindow();
                    _instance.Closing += Instance_Closing;
                }

                return _instance;
            }
            private set
            {
                _instance = value;
                _instance.Closing += Instance_Closing;
            }
        }

        static void Instance_Closing(object sender, CancelEventArgs e)
        {
            //make absolutely sure we've disposed all audio/video resources
            Instance.ViewModel.CurrentSettingsView.Kill();
            ProfileManager.Instance.CallManager.Kill();

            //reverse iterate because closing the windows also removes them from the list
            for (int i = Instance.Children.Count - 1; i >= 0; i--)
                Instance.Children[i].Close();

            //get rid of the tray icon
            Instance.TrayIcon.Dispose();
        }

        public MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        public void Reload()
        {
            if (_instance.Visibility == Visibility.Visible)
            {
                //this feels like a hack
                var handler = (CancelEventHandler)((sender, e) =>
                {
                    _instance = new MainWindow();
                    _instance.Show();
                });

                _instance.Closing += handler;
                _instance.Close();
            }
            else
            {
                _instance.Show();
            }
        }

        public void CloseInstance()
        {
            _instance.Close();
            _instance = null;
        }

        public void AddChildWindow(ConversationWindow window)
        {
            if (!Children.Contains(window))
            {
                window.Closing += ChildWindow_Closing;
                Children.Add(window);
            }
        }

        private void ChildWindow_Closing(object sender, CancelEventArgs e)
        {
            var window = sender as ConversationWindow;
            if (window == null)
                return;

            window.Context.CurrentView.Window = null;

            if (Children.Contains(window))
                Children.Remove(window);
        }

        private MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();
            Children = new List<ConversationWindow>();

            this.FixBackground();

            //only check for updates once at launch (TODO: check periodically?)
            //TODO: move this someplace else
            //CheckForUpdates();
        }

        private async Task CheckForUpdates()
        {
            try
            {
                using (var mgr = new UpdateManager(_updateUrl))
                {
                    //
                    if (!mgr.IsInstalledApp)
                    {
                        Debugging.Write("Skipping update check, this is not an installed application.");
                        return;
                    }

                    var updateInfo = await mgr.CheckForUpdate();

                    Debugging.Write("Currently installed: " + updateInfo.CurrentlyInstalledVersion.Version);
                    Debugging.Write("Latest version: " + updateInfo.FutureReleaseEntry.Version);

                    if (updateInfo.CurrentlyInstalledVersion.Version < updateInfo.FutureReleaseEntry.Version)
                    {
                        //download the latest release so we can retrieve the release notes
                        await mgr.DownloadReleases(new[] { updateInfo.FutureReleaseEntry });

                        string msg = string.Format("There is a new update available for installation. The latest version is {0}. Would you like to update?\n\nChanges:\n{1}",
                            updateInfo.FutureReleaseEntry.Version,
                            updateInfo.FutureReleaseEntry.GetReleaseNotes(updateInfo.PackageDirectory));

                        var result = MessageBox.Show(msg, "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            await mgr.UpdateApp();
                            MessageBox.Show("Toxy has been updated. This update will be activated after Toxy has been restarted.", "Update successfully installed", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex) { Debugging.Write("Could not check for updates: " + ex.ToString()); }
        }

        private void ButtonGroup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                return;

            var group = ProfileManager.Instance.Tox.NewGroup();
            if (group < 0)
                return;

            var title = "New groupchat";
            ProfileManager.Instance.Tox.SetGroupTitle(group, title);

            var model = new GroupControlViewModel();
            model.ChatNumber = group;
            model.Name = title;

            ViewModel.CurrentFriendListView.AddObject(model);
            ViewModel.CurrentFriendListView.SortObject(model);
            ViewModel.CurrentFriendListView.SelectObject(model);
        }

        private void ButtonTransfers_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                return;

            DeselectFriendList();
        }

        private void ButtonAddFriend_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                return;

            ViewModel.CurrentView = new AddFriendViewModel();
            DeselectFriendList();
        }

        private void ButtonSettings_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                return;

            ViewModel.CurrentView = ViewModel.CurrentSettingsView;
            DeselectFriendList();
        }

        //TODO: make this work in xaml somehow?
        private void DeselectFriendList()
        {
            ViewModel.CurrentFriendListView.SelectedChat = null;
        }
    }
}
