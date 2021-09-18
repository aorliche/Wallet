using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VirtualWallet
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class ConsoleWindow : Window
    {
        public int lineNo = 0;
        public static ConsoleWindow cons;

        public ConsoleWindow()
        {
            InitializeComponent();
            cons = this;
        }

        public void ClearConsole(object sender, RoutedEventArgs e)
        {
            Console.Clear();
        }

        public void HideOnClose(object sender, CancelEventArgs e)
        {
            Visibility = Visibility.Hidden;
            e.Cancel = true;
        }

        public static void WriteLine(Object o)
        {
            cons.Console.Text += (cons.lineNo++) + ". [" + DateTime.Now + "] " + o.ToString() + "\n";
        }
    }
}
