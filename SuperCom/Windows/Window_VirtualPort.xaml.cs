﻿using SuperCom.Config;
using SuperCom.Entity;
using SuperControls.Style;
using SuperControls.Style.Windows;
using SuperUtils.IO;
using SuperUtils.Windows.WindowRegistry;
using SuperUtils.WPF.VisualTools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SuperCom.Windows
{
    /// <summary>
    /// Interaction logic for Window_VirtualPort.xaml
    /// </summary>
    public partial class Window_VirtualPort : BaseWindow
    {
        private const string PORT_PREFIX_A = "CNCA";
        private const string PORT_PREFIX_B = "CNCB";
        private const string PORT_PREFIX = "COM";
        private const int SHOW_NEW_PORT_INTERVAL = 100;

        private static readonly string COM_0_COM_INSTALLED_PATH =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Installer", "setup.exe");

        #region "属性"

        private bool _IsCom0ConInstalled = false;
        public bool IsCom0ConInstalled {
            get { return _IsCom0ConInstalled; }
            set {
                _IsCom0ConInstalled = value;
                RaisePropertyChanged();
            }
        }
        private bool _IsCom0ConExeExists = false;
        public bool IsCom0ConExeExists {
            get { return _IsCom0ConExeExists; }
            set {
                _IsCom0ConExeExists = value;
                RaisePropertyChanged();
            }
        }
        private bool _Saving = false;
        public bool Saving {
            get { return _Saving; }
            set {
                _Saving = value;
                RaisePropertyChanged();
            }
        }
        private bool _AddingPort = false;
        public bool AddingPort {
            get { return _AddingPort; }
            set {
                _AddingPort = value;
                RaisePropertyChanged();
            }
        }
        private bool _DeletingPort = false;
        public bool DeletingPort {
            get { return _DeletingPort; }
            set {
                _DeletingPort = value;
                RaisePropertyChanged();
            }
        }
        private bool _ListingPort = false;
        public bool ListingPort {
            get { return _ListingPort; }
            set {
                _ListingPort = value;
                RaisePropertyChanged();
            }
        }
        private string _Com0ConInstalledPath = ConfigManager.VirtualPortSettings.Com0ConInstalledPath;
        public string Com0ConInstalledPath {
            get { return _Com0ConInstalledPath; }
            set {
                _Com0ConInstalledPath = value;
                RaisePropertyChanged();
                ConfigManager.VirtualPortSettings.Com0ConInstalledPath = value;
                ConfigManager.VirtualPortSettings.Save();
            }
        }
        private ObservableCollection<VirtualPort> _CurrentVirtualPorts;
        public ObservableCollection<VirtualPort> CurrentVirtualPorts {
            get { return _CurrentVirtualPorts; }
            set {
                _CurrentVirtualPorts = value;
                RaisePropertyChanged();
            }
        }

        #endregion


        public Window_VirtualPort()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        public async void Init()
        {
            CurrentVirtualPorts = new ObservableCollection<VirtualPort>();
            InstalledApp app = RegistryHelper.GetInstalledApp(VirtualPortManager.COM_0_COM_PROGRAM_NAME);
            if (app != null) {
                IsCom0ConInstalled = true;
                string path = System.IO.Path.Combine(app.InstallLocation,
                    VirtualPortManager.COM_0_COM_PROGRAM_EXE_NAME);
                if (!File.Exists(Com0ConInstalledPath) && File.Exists(path))
                    Com0ConInstalledPath = path;
                ListingPort = true;
                IsCom0ConExeExists = File.Exists(Com0ConInstalledPath);
                VirtualPortManager.Init(Com0ConInstalledPath);
                List<VirtualPort> virtualPorts = await VirtualPortManager.ListAllPort();
                foreach (var item in virtualPorts) {
                    CurrentVirtualPorts.Add(item);
                }
                await Task.Delay(100);
                ListingPort = false;
            }
        }


        private void InstallCom0Com(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(COM_0_COM_INSTALLED_PATH)) {
                MessageCard.Error($"不存在：{COM_0_COM_INSTALLED_PATH}");
                return;
            }
            FileHelper.TryOpenFile(COM_0_COM_INSTALLED_PATH);
            _ = (bool)new MsgBox("安装完成后重新打开虚拟串口").ShowDialog(this);
            this.Close();
        }

        private void BaseWindow_ContentRendered(object sender, EventArgs e)
        {
            Init();
        }


        private void SelectPath(object sender, RoutedEventArgs e)
        {
            string filePath = FileHelper.SelectFile(this, "setupc.exe|*.exe");
            if (File.Exists(filePath) && filePath.EndsWith("setupc.exe")) {
                Com0ConInstalledPath = filePath;
                MessageNotify.Success("设置成功");
                Init();
            } else
                MessageNotify.Error("必须是 setupc.exe");

        }

        private async void DeletePort(object sender, RoutedEventArgs e)
        {
            if (!(bool)new MsgBox("确定删除该串口对？").ShowDialog(this))
                return;

            bool deleted = false;
            if (sender is FrameworkElement ele && ele.Tag != null) {
                string id = ele.Tag.ToString();
                if (string.IsNullOrEmpty(id))
                    return;
                id = id.Replace(PORT_PREFIX_A, "").Replace(PORT_PREFIX_B, "");
                int.TryParse(id, out int n);
                if (n >= 0) {
                    DeletingPort = true;
                    deleted = await VirtualPortManager.DeletePort(n);
                }
            }


            if (deleted && CurrentVirtualPorts != null && CurrentVirtualPorts.Count > 1) {
                int selectedIndex = dataGrid.SelectedIndex;
                int small = -1, large = -1;
                if (selectedIndex % 2 == 0) {
                    large = selectedIndex + 1;
                    small = selectedIndex;
                } else {
                    small = selectedIndex - 1;
                    large = selectedIndex;
                }
                if (small >= 0 && large < CurrentVirtualPorts.Count) {
                    CurrentVirtualPorts.RemoveAt(large);
                    CurrentVirtualPorts.RemoveAt(small);
                    RefreshPorts();
                }
            }
            DeletingPort = false;
        }

        private void RefreshVirtualPort(object sender, RoutedEventArgs e)
        {
            Init();
        }



        private async void SaveChanges(object sender, RoutedEventArgs e)
        {
            bool ret = await SaveChanges();
            if (ret)
                this.Close();
        }
        private async void ApplyChanges(object sender, RoutedEventArgs e)
        {
            await SaveChanges();
        }

        public async Task<bool> SaveChanges()
        {
            // 检查是否输入

            foreach (var item in CurrentVirtualPorts) {
                if (string.IsNullOrEmpty(item.Name)) {
                    MessageNotify.Error("存在未填写的串口号");
                    return false;
                }
                if (!VirtualPort.IsProperPortName(item.Name)) {
                    MessageNotify.Error("串口号填写错误");
                    return false;
                }
                if (!VirtualPort.IsProperNumber(item)) {
                    MessageNotify.Error("数值填写有误");
                    return false;
                }
                item.Name = item.Name.ToUpper();
            }

            long count = CurrentVirtualPorts.Select(arg => arg.Name).ToHashSet().Count();
            if (count != CurrentVirtualPorts.Count) {
                MessageNotify.Error("存在重复串口号");
                return false;
            }

            List<VirtualPort> AllPorts = await VirtualPortManager.ListAllPort();
            List<VirtualPort> CurrentPorts = CurrentVirtualPorts.ToList();
            Saving = true;
            // 更新
            List<VirtualPort> toChange = new List<VirtualPort>();
            foreach (var item in CurrentPorts) {
                VirtualPort virtualPort = AllPorts.FirstOrDefault(arg => arg.Name.Equals(item.Name));
                if (!item.Equals(virtualPort))
                    toChange.Add(item);
            }
            if (toChange.Count == 0) {
                Saving = false;
                MessageNotify.Info("无改动项");
                return false;
            }
            bool success = await VirtualPortManager.UpdatePorts(toChange);
            Saving = false;
            if (success) {
                MessageNotify.Success("成功");
                Init();
                return true;
            }

            MessageNotify.Error("失败");
            return false;
        }

        private async void AddNewVirtualPort(object sender, RoutedEventArgs e)
        {
            string nameA = portNameA.Text;
            string nameB = portNameB.Text;

            if (!VirtualPort.IsProperPortName(nameA) ||
                !VirtualPort.IsProperPortName(nameB)) {
                MessageNotify.Error("串口号填写错误");
                return;
            }
            nameA = nameA.ToUpper().Trim();
            nameB = nameB.ToUpper().Trim();

            // 检查是否存在相同的
            List<VirtualPort> virtualPorts = await VirtualPortManager.ListAllPort();
            List<string> list = virtualPorts.Select(arg => arg.Name).ToList();
            if (list.Contains(nameA) || list.Contains(nameB)) {
                MessageNotify.Error("已存在串口");
                return;
            }

            // 执行 cmd 命令
            VirtualPort portA = new VirtualPort(nameA);
            VirtualPort portB = new VirtualPort(nameB);
            AddingPort = true;
            bool success = await VirtualPortManager.InsertPort(portA, portB);
            if (!success) {
                MessageNotify.Error("添加失败");
                AddingPort = false;
                return;
            }
            Init();
            newVirtualPortGrid.Visibility = Visibility.Collapsed;
            AddingPort = false;
            MessageNotify.Success("添加成功！");
            RefreshPorts();
        }


        private Window GetWindowByName(string name)
        {
            foreach (Window item in App.Current.Windows) {
                if (item.Name.Equals(name))
                    return item;
            }
            return null;
        }

        private void RefreshPorts()
        {
            MainWindow window = GetWindowByName("mainWindow") as MainWindow;
            window?.RefreshPortsStatus(null, null);
        }

        private void CloseNewVirtualPortGrid(object sender, RoutedEventArgs e)
        {
            newVirtualPortGrid.Visibility = Visibility.Collapsed;

        }

        private async void ShowNewVirtualPortGrid(object sender, RoutedEventArgs e)
        {
            newVirtualPortGrid.Visibility = Visibility.Visible;
            portNameA.Text = PORT_PREFIX;
            portNameB.Text = PORT_PREFIX;
            await Task.Delay(SHOW_NEW_PORT_INTERVAL);
            portNameA.SetFocus();
        }

        private void portNameA_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab) {
                portNameB.SetFocus();
                e.Handled = true;
            } else if (e.Key == Key.Enter) {
                AddNewVirtualPort(null, null);
            }


        }

        private void portNameB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab) {
                portNameA.SetFocus();
                e.Handled = true;
            } else if (e.Key == Key.Enter) {
                AddNewVirtualPort(null, null);
            }


        }
    }
}
