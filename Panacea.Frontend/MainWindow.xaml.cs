using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.FSharp.Collections;
using Panacea.Frontend.Extensions;
using Panacea.Lib;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;
using TreeView = System.Windows.Controls.TreeView;

namespace Panacea.Frontend
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _currentVfsFilePath = "";
        public MainWindow()
        {
            InitializeComponent();
        }

        #region XAML handlers
        private void ButtonFileOpenVfs_OnClick(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = System.IO.Path.GetFullPath(openFileDialog.FileName);
                var fileName = System.IO.Path.GetFileName(filePath);
                var fileExt = System.IO.Path.GetExtension(filePath);
                if (!fileExt.Equals(".vfs"))
                {
                    var result =
                        MessageBox.Show(
                            $"The provided file \"{fileName}\" does not have a VFS extension. Try to unpack anyway?",
                            "Open VFS",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Exclamation);

                    if (result == MessageBoxResult.No)
                        return;
                }

                MainContainer.Cursor = Cursors.Wait;

                using (Stream fileStream = File.OpenRead(filePath))
                {
                    try
                    {
                        var vfs = VFS.decode(fileStream);
                        VfsTreeItem rootView = new VfsTreeItem(vfs);
                        VfsTreeView.Items.Clear();
                        VfsTreeView.Items.Add(rootView);
                    }
                    catch (VFS.VFSReadException ex)
                    {
                        MessageBox.Show(
                            $"Opening the VFS failed with exception message \"{ex.Message}\".",
                            "Open VFS error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }

                MainContainer.Cursor = Cursors.Arrow;
                _currentVfsFilePath = filePath;
            }
        }

        private void ButtonFileExit_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ButtonHelpVisitGitHub_OnClick(object sender, RoutedEventArgs e)
        {
            string url = "https://www.github.com/EGBland/Panacea";
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }

        private void TreeViewVfs_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Console.WriteLine("selection changed");
            var selected = e.NewValue as VfsTreeItem;
            var selectedHeader = selected?.VfsHeader;
            Console.WriteLine(selected?.Name);

            if (selectedHeader?.IsFile ?? false)
            {
                DetailsNameTextBox.Text = selectedHeader.Name;
                DetailsNameTextBox.IsEnabled = true;
                DetailsSizeTextBox.Text = selectedHeader.Size.ToString();
                DetailsSizeTextBox.IsEnabled = false;
                DetailsSaveButton.IsEnabled = true;
            }
            else if (selectedHeader?.IsDir ?? false)
            {
                DetailsNameTextBox.Text = selectedHeader.Name;
                DetailsNameTextBox.IsEnabled = selectedHeader.IsSubdirHeader;
                DetailsSizeTextBox.Text = "";
                DetailsSizeTextBox.IsEnabled = false;
                DetailsSaveButton.IsEnabled = selectedHeader.IsSubdirHeader;
            }
            else
            {
                DetailsNameTextBox.Text = "";
                DetailsNameTextBox.IsEnabled = false;
                DetailsSizeTextBox.Text = "";
                DetailsSizeTextBox.IsEnabled = false;
                DetailsSaveButton.IsEnabled = false;
            }
        }

        private void TreeViewVfs_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);
            source = source as TreeViewItem;
            if (source == null) return;

            var treeView = sender as TreeView;
            if (treeView == null) return;

            var selected = treeView.SelectedItem as VfsTreeItem;

            ContextMenu rightClickMenu = new ContextMenu();

            MenuItem extractOption = new MenuItem { Header = "Extract..." };
            extractOption.Click += ExtractButton_OnClick;

            rightClickMenu.Items.Add(extractOption);
            
            rightClickMenu.IsOpen = true;
            Console.WriteLine(selected?.Name);
        }

        private void DetailsSaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            var selected = VfsTreeView.SelectedItem as VfsTreeItem;
            selected?.UpdateName("foobarbaz");
        }
        
        #endregion
        
        #region Code-behind handlers

        private void ExtractButton_OnClick(object sender, EventArgs e)
        {
            var selected = VfsTreeView.SelectedItem as VfsTreeItem;
            Console.WriteLine(selected?.Name);

            var header = selected?.VfsHeader;

            if (selected == null || header == null)
                return;

            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
                return;
            var basePath = dialog.SelectedPath;
            using var vfsDataStream = File.OpenRead(_currentVfsFilePath);
            foreach (var child in selected.Children)
                child.Extract(basePath, vfsDataStream);
        }

        #endregion
    }

    public class VfsTreeItem
    {
        public string Name => VfsHeader?.Name ?? "<null>";

        public bool IsDirty { get; private set; }
        
        public string Icon
        {
            get
            {
                if (VfsHeader?.IsDir ?? false)
                {
                    return "\xE188";
                }
                return Path.GetExtension(Name) switch
                {
                    ".vfs" => "\xE188",
                    ".tex" => "\xE155",
                    ".ft"  => "\xE185",
                    ".dat" => "\xE132",
                    _      => "\xE160"
                };
            }
        }

        public string IconColour => IsDirty ? "Red" : "Black";

        public VFS.Header? VfsHeader { get; set; }
        public ObservableCollection<VfsTreeItem> Children { get; set; }
        public VfsTreeItem(Tree.Tree<VFS.Header> vfs)
        {
            this.VfsHeader = vfs.Value.AsNullable();
            this.Children = new ObservableCollection<VfsTreeItem>();
            this.IsDirty = false;
            var childrenSorted = vfs.Children.ToList();
            childrenSorted.Sort((c1, c2) =>
            {
                var isFile1 = c1.Value.AsNullable()?.IsFile ?? false;
                var isFile2 = c2.Value.AsNullable()?.IsFile ?? false;
                if (isFile1 && !isFile2)
                {
                    return 1;
                }

                if (!isFile1 && isFile2)
                {
                    return -1;
                }

                var c1Name = c1.Value.AsNullable()?.Name ?? "";
                var c2Name = c2.Value.AsNullable()?.Name ?? "";
                return string.Compare(c1Name, c2Name, StringComparison.Ordinal);
            });
            
            childrenSorted.ToList().ForEach(child => this.Children.Add(new VfsTreeItem(child)));
        }

        public bool Extract(string cwd, Stream vfsDataStream)
        {
            if (VfsHeader == null)
                return false;

            bool result = true;
            if (VfsHeader.IsFile)
            {
                byte[] data = VFS.loadFrom(vfsDataStream, VfsHeader);
                var path = Path.Join(cwd, VfsHeader.Name);
                using (var outputStream = File.OpenWrite(path))
                {
                    try
                    {
                        outputStream.Write(data);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                        result = false;
                    }
                }
            }
            else
                foreach (var child in Children)
                    result &= child.Extract(Path.Join(cwd, VfsHeader.Name), vfsDataStream);

            return result;
        }

        public void UpdateName(string name)
        {
            this.IsDirty = true;
            this.VfsHeader = this.VfsHeader?.WithName(name);
        }
    }
}