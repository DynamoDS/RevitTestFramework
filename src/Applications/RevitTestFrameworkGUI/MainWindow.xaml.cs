using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RTF.Applications
{
    /// <summary>
    /// Interaction logic for View.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly RunnerViewModel vm;

        public MainWindow()
        {
            InitializeComponent();

            vm = new RunnerViewModel(new WpfContext());

            DataContext = vm;

            Closing += View_Closing;
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            vm.CleanupCommand.Execute();
        }

        private void TestDataTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            vm.SelectedItem = e.NewValue;
        }

        private void UpdateRequired(object sender, RoutedEventArgs e)
        {
            vm.UpdateCommand.Execute();
        }

        private void ProductSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vm.ChangeProductCommand.Execute(((ComboBox) sender).SelectedIndex);
        }

        private void Assembly_OnDrop(object sender, DragEventArgs e)
        {
            vm.SetAssemblyPathCommand.Execute(GetFirstFileFromDropPackage(e));
        }

        private void WorkingDir_OnDrop(object sender, DragEventArgs e)
        {
            vm.SetWorkingPathCommand.Execute(GetFirstFileFromDropPackage(e));
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private string GetFirstFileFromDropPackage(DragEventArgs e)
        {
            //http://stackoverflow.com/questions/5662509/drag-and-drop-files-into-wpf
            //http://stackoverflow.com/questions/4281857/wpf-drag-and-drop-to-a-textbox

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return string.Empty;
            }

            // Note that you can have more than one file.
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (!files.Any())
            {
                return string.Empty;
            }

            return files.First();
        }

        private void Results_OnDrop(object sender, DragEventArgs e)
        {
            vm.SetResultsPathCommand.Execute(GetFirstFileFromDropPackage(e));
        }
    }
}
